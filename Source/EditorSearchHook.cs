using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KSP.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PartSearchSuggest
{
    internal sealed class SearchFieldPointerHandler : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        private EditorSearchHook _hook;

        internal void Initialize(EditorSearchHook hook)
        {
            _hook = hook;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _hook?.HandleSearchFieldPointerDown();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _hook?.HandleSearchFieldPointerClick();
        }
    }

    internal sealed class EditorSearchHook : MonoBehaviour
    {
        // Light installs keep a richer mix; heavy 150+ mod indexes shrink rows to cut Match cost.
        private const int MaxSuggestionsLight = 24;
        private const int MaxSuggestionsHeavy = 14;
        private const int MaxMetadataSuggestionsLight = 8;
        private const int MaxMetadataSuggestionsHeavy = 4;
        private const int MaxCategorizerCandidatesLight = 16;
        private const int MaxCategorizerCandidatesHeavy = 10;
        private const int MaxCategorizerSuggestionsLight = 8;
        private const int MaxCategorizerSuggestionsHeavy = 5;
        private const int MaxTokenCategorizerSuggestionsLight = 3;
        private const int MaxTokenCategorizerSuggestionsHeavy = 2;
        // ~2k editor parts ≈ typical 80–150 mod install; above that, use heavy caps.
        private const int HeavyIndexPartThreshold = 2000;
        private const int MinReservedFilterSlotsWhenCategorical = 3;
        private const int MinReservedPartSlots = 4;
        private const int MinReservedMetadataSlots = 1;
        // Coalesce per-key Match/ShowSuggestions so InputField stays responsive on large indexes.
        // Idle must cover a fast typist burst ("solar") so only the final query runs Match.
        private const float SuggestionRefreshDebounceSeconds = 0.150f;
        // Longer idle for backspace/delete — key-repeat often leaves a short gap after the first
        // delete where a full Match would otherwise hitch mid-hold.
        private const float SuggestionRefreshShortenDebounceSeconds = 0.200f;
        // Same lock id stock PartCategorizer.FocusSearchField / SearchField_OnClick uses.
        private const string SearchFieldTextInputLockId = "SearchFieldTextInput";

        private TMP_InputField _searchField;
        private RectTransform _fieldRect;
        private SearchDropdownPanel _dropdown;
        private SuggestionIndex _index;
        private MetadataSuggestionIndex _metadataIndex;
        private CategorizerSuggestionIndex _categorizerIndex;
        private SearchHistory _history;
        private bool _hooked;
        private bool _pointerHandlerAttached;
        private bool _readyForSuggestions;
        private bool _metadataIndexReady;
        private bool _categorizerIndexReady;
        private bool _fullSearchReady;
        private int _pointerGraceFramesRemaining;
        private string _lastCommittedQuery = string.Empty;
        private string _lastShownSuggestionQuery;
        private bool _applyingSuggestion;
        private Coroutine _pendingHideCoroutine;
        private Coroutine _pendingHistoryApplyCoroutine;
        private Coroutine _pendingSuggestionRefreshCoroutine;
        private int _hideRequestId;
        private int _suggestionRefreshRequestId;
        private int _lastValueChangedLength = -1;
        private string _pendingHistoryQuery;
        // Focus/click → caret at end; debounce suggestion refresh preserves mid-edit caret.
        private bool _preferCaretAtEndThisFocus;
        private bool _userAdjustedCaretThisFocus;
        // Fight TMP focus/select-all reset for a few frames after click/focus.
        private int _caretAtEndGraceFrames;

        private void Start()
        {
            StartCoroutine(InitializeWhenReady());
        }

        private void Update()
        {
            if (_pointerGraceFramesRemaining > 0)
            {
                --_pointerGraceFramesRemaining;
            }

            TrackCaretAdjustmentThisFocus();

            if (_dropdown == null || !_dropdown.IsVisible)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                DismissDropdown();
            }
        }

        private IEnumerator InitializeWhenReady()
        {
            // Indexes must finish on the VAB/SPH loading transition (sync BuildIfNeeded from
            // Awake). UI hook only after full ready — zero post-open index / finalize work.
            yield return GameLoadIndexService.WaitUntilFullReady(this);

            yield return new WaitUntil(() => PartCategorizer.Instance != null);
            yield return null;

            PartCategorizer categorizer = PartCategorizer.Instance;
            _searchField = categorizer.searchField;
            if (_searchField == null)
            {
                EditorBootstrap.LogWarning("PartCategorizer searchField is null.");
                yield break;
            }

            _fieldRect = _searchField.GetComponent<RectTransform>();
            _history = new SearchHistory();

            // Indexes were built during the loading transition. Interactive hangar is UI-hook only —
            // never start BuildIfNeeded here; never post-open layout finalize / auto-show.
            _index = GameLoadIndexService.PartIndex;
            _metadataIndex = GameLoadIndexService.MetadataIndex;
            _categorizerIndex = GameLoadIndexService.CategorizerIndex;

            if (_index == null)
            {
                EditorBootstrap.LogWarning(
                    "Part index unavailable after editor-load wait — dropdown disabled (no interactive rebuild).");
                yield break;
            }

            if (!GameLoadIndexService.IsFullReady)
            {
                EditorBootstrap.LogWarning(
                    "Full search index not ready after editor-load wait — dropdown disabled (no interactive rebuild).");
                yield break;
            }

            _dropdown = SearchDropdownPanel.Create(_fieldRect);
            if (_dropdown == null)
            {
                EditorBootstrap.LogWarning("SearchDropdownPanel.Create failed — dropdown disabled.");
                yield break;
            }

            _dropdown.OnSuggestionChosen += ApplySuggestion;
            _dropdown.OnDismissed += DismissDropdown;
            _dropdown.OnClearHistoryRequested += ClearSearchHistory;

            HookSearchField();
            AttachSearchFieldPointerHandler();
            _readyForSuggestions = true;
            _metadataIndexReady = _metadataIndex != null;
            _categorizerIndexReady = _categorizerIndex != null && _categorizerIndex.EntryCount > 0;
            _fullSearchReady = true;
            EditorBootstrap.Log("Search ready (full)");
            EditorBootstrap.Log("Hooked native editor search field.");
            IndexDebugDump.LogIfEnabled(_index, _metadataIndex, _categorizerIndex);
            // Do not auto-ShowSuggestions on editor-ready — that reflowed/shrunk stock UI.
        }

        private void TryShowDropdownIfSearchFieldActive(string source)
        {
            if (!_readyForSuggestions || _searchField == null || _applyingSuggestion)
            {
                return;
            }

            if (!_searchField.isFocused && !IsSearchFieldStillActive(_searchField))
            {
                return;
            }

            EditorBootstrap.Log(
                "TryShowDropdownIfSearchFieldActive("
                + source
                + "): search field already active — showing dropdown.");
            RequestShowFromInteraction(source);
        }

        private void HookSearchField()
        {
            if (_hooked || _searchField == null)
            {
                return;
            }

            // Prevent Selectable.OnMove from stealing Left/Right/Home/End away from caret edits.
            _searchField.navigation = new Navigation { mode = Navigation.Mode.None };

            _searchField.onValueChanged.AddListener(OnSearchValueChanged);
            _searchField.onSelect.AddListener(OnSearchSelected);
            _searchField.onDeselect.AddListener(OnSearchDeselected);
            _searchField.onEndEdit.AddListener(OnSearchEndEdit);
            _searchField.onSubmit.AddListener(OnSearchSubmit);
            _hooked = true;
        }

        /// <summary>
        /// Mirror stock PartCategorizer search focus: lock KEYBOARDINPUT so editor camera /
        /// MenuNavigation bindings cannot eat Left/Right/Home/End while the caret is active.
        /// </summary>
        private static void EnsureSearchFieldKeyboardLock()
        {
            InputLockManager.SetControlLock(ControlTypes.KEYBOARDINPUT, SearchFieldTextInputLockId);
        }

        private static void ReleaseSearchFieldKeyboardLock()
        {
            InputLockManager.RemoveControlLock(SearchFieldTextInputLockId);
        }

        private struct SearchFieldCaretState
        {
            public bool Valid;
            public bool WasFocused;
            public int Caret;
            public int SelectionAnchor;
            public int SelectionFocus;
        }

        private SearchFieldCaretState CaptureSearchFieldCaret()
        {
            SearchFieldCaretState state = default;
            if (_searchField == null)
            {
                return state;
            }

            state.Valid = true;
            state.WasFocused = _searchField.isFocused || IsSearchFieldStillActive(_searchField);
            state.Caret = _searchField.caretPosition;
            state.SelectionAnchor = _searchField.selectionAnchorPosition;
            state.SelectionFocus = _searchField.selectionFocusPosition;
            return state;
        }

        /// <summary>
        /// Suggestion UI rebuild must never rewrite field text or steal focus / KEYBOARDINPUT.
        /// Fresh focus/click restores caret at end; mid-edit arrow moves are preserved across
        /// suggestion debounce refreshes.
        /// </summary>
        private void RestoreSearchFieldCaret(SearchFieldCaretState state)
        {
            if (!state.Valid || _searchField == null)
            {
                return;
            }

            if (state.WasFocused)
            {
                EnsureSearchFieldKeyboardLock();
                PartsPanelCollapseHelper.EnsureSearchFieldInteractableWhileCollapsed();

                if (EventSystem.current != null
                    && EventSystem.current.currentSelectedGameObject != _searchField.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(_searchField.gameObject);
                }

                if (!_searchField.isFocused)
                {
                    _searchField.ActivateInputField();
                }
            }

            string text = _searchField.text ?? string.Empty;
            int max = text.Length;
            if (_preferCaretAtEndThisFocus && !_userAdjustedCaretThisFocus)
            {
                _searchField.caretPosition = max;
                _searchField.selectionAnchorPosition = max;
                _searchField.selectionFocusPosition = max;
            }
            else
            {
                int caret = Mathf.Clamp(state.Caret, 0, max);
                int anchor = Mathf.Clamp(state.SelectionAnchor, 0, max);
                int focus = Mathf.Clamp(state.SelectionFocus, 0, max);
                _searchField.caretPosition = caret;
                _searchField.selectionAnchorPosition = anchor;
                _searchField.selectionFocusPosition = focus;
            }

            _searchField.ForceLabelUpdate();
        }

        /// <summary>
        /// Click/focus default: caret + collapsed selection at end (avoid select-all / start).
        /// </summary>
        private void PlaceCaretAtEndForFreshFocus()
        {
            if (_searchField == null)
            {
                return;
            }

            _preferCaretAtEndThisFocus = true;
            _userAdjustedCaretThisFocus = false;
            // Enough frames for TMP OnSelect / ActivateInputField to finish fighting us.
            _caretAtEndGraceFrames = 3;

            ApplyCaretAtEndNow();
        }

        private void ApplyCaretAtEndNow()
        {
            if (_searchField == null)
            {
                return;
            }

            string text = _searchField.text ?? string.Empty;
            int end = text.Length;
            _searchField.caretPosition = end;
            _searchField.selectionAnchorPosition = end;
            _searchField.selectionFocusPosition = end;
            _searchField.ForceLabelUpdate();
        }

        private void TrackCaretAdjustmentThisFocus()
        {
            if (_searchField == null || !_searchField.isFocused)
            {
                return;
            }

            // During grace, re-assert end and ignore TMP's transient caret-at-0 / select-all.
            if (_caretAtEndGraceFrames > 0)
            {
                --_caretAtEndGraceFrames;
                if (_preferCaretAtEndThisFocus && !_userAdjustedCaretThisFocus)
                {
                    ApplyCaretAtEndNow();
                }

                return;
            }

            if (!_preferCaretAtEndThisFocus || _userAdjustedCaretThisFocus)
            {
                return;
            }

            string text = _searchField.text ?? string.Empty;
            int end = text.Length;
            int caret = _searchField.caretPosition;
            int anchor = _searchField.selectionAnchorPosition;
            int focus = _searchField.selectionFocusPosition;
            // Collapsed caret at end is still "prefer end"; anything else means the user moved it.
            if (caret != end || anchor != end || focus != end)
            {
                _userAdjustedCaretThisFocus = true;
            }
        }

        private void AttachSearchFieldPointerHandler()
        {
            if (_pointerHandlerAttached || _searchField == null)
            {
                return;
            }

            SearchFieldPointerHandler handler = _searchField.gameObject.GetComponent<SearchFieldPointerHandler>();
            if (handler == null)
            {
                handler = _searchField.gameObject.AddComponent<SearchFieldPointerHandler>();
            }

            handler.Initialize(this);
            _pointerHandlerAttached = true;
            _pointerGraceFramesRemaining = 2;
        }

        private void OnDestroy()
        {
            CancelPendingHide();
            CancelPendingSuggestionRefresh();
            _pendingHistoryQuery = null;
            if (_pendingHistoryApplyCoroutine != null)
            {
                StopCoroutine(_pendingHistoryApplyCoroutine);
                _pendingHistoryApplyCoroutine = null;
            }

            if (_dropdown != null)
            {
                _dropdown.HideWithoutHoldNotify();
                _dropdown.OnSuggestionChosen -= ApplySuggestion;
                _dropdown.OnDismissed -= DismissDropdown;
                _dropdown.OnClearHistoryRequested -= ClearSearchHistory;
                // Own overlay canvas is not under stock UI — destroy explicitly on editor exit.
                UnityEngine.Object.Destroy(_dropdown.gameObject);
                _dropdown = null;
            }

            PartsPanelCollapseHelper.ReleaseAllForEditorExit("EditorSearchHook.OnDestroy");

            if (_searchField != null && _hooked)
            {
                _searchField.onValueChanged.RemoveListener(OnSearchValueChanged);
                _searchField.onSelect.RemoveListener(OnSearchSelected);
                _searchField.onDeselect.RemoveListener(OnSearchDeselected);
                _searchField.onEndEdit.RemoveListener(OnSearchEndEdit);
                _searchField.onSubmit.RemoveListener(OnSearchSubmit);
            }
        }

        private void CancelPendingHide()
        {
            if (_pendingHideCoroutine != null)
            {
                ++_hideRequestId;
                StopCoroutine(_pendingHideCoroutine);
                _pendingHideCoroutine = null;
                EditorBootstrap.Log("CancelPendingHide: superseded stale hide coroutine.");
            }
        }

        private void CancelPendingSuggestionRefresh()
        {
            ++_suggestionRefreshRequestId;
            if (_pendingSuggestionRefreshCoroutine != null)
            {
                StopCoroutine(_pendingSuggestionRefreshCoroutine);
                _pendingSuggestionRefreshCoroutine = null;
            }
        }

        /// <summary>
        /// Typing path: debounce Match/ShowSuggestions so each keystroke does not hitch the
        /// InputField on a full metadata/categorizer/part scan. Applies to forward typing,
        /// backspace/delete, and clear — never Match on the key event itself. Click/focus/history
        /// still call <see cref="ShowSuggestions"/> immediately. Latest query wins; prior refresh
        /// is cancelled.
        /// </summary>
        private void ScheduleSuggestionRefresh(
            string query,
            bool preferHistory,
            string source,
            float debounceSeconds)
        {
            int requestId = ++_suggestionRefreshRequestId;
            if (_pendingSuggestionRefreshCoroutine != null)
            {
                StopCoroutine(_pendingSuggestionRefreshCoroutine);
                _pendingSuggestionRefreshCoroutine = null;
            }

            _pendingSuggestionRefreshCoroutine = StartCoroutine(
                DebouncedShowSuggestions(requestId, query, preferHistory, source, debounceSeconds));
        }

        private IEnumerator DebouncedShowSuggestions(
            int requestId,
            string query,
            bool preferHistory,
            string source,
            float debounceSeconds)
        {
            // All text changes share idle debounce (including empty → history). The old
            // empty/history one-frame coalesce still let short-query Match / stock SearchStop
            // hitch on backspace with large indexes.
            float wait = debounceSeconds > 0f ? debounceSeconds : SuggestionRefreshDebounceSeconds;
            float deadline = Time.unscaledTime + wait;
            while (Time.unscaledTime < deadline)
            {
                if (requestId != _suggestionRefreshRequestId)
                {
                    yield break;
                }

                yield return null;
            }

            if (requestId != _suggestionRefreshRequestId)
            {
                yield break;
            }

            if (_applyingSuggestion || _searchField == null)
            {
                yield break;
            }

            // Always refresh against the live field so a superseded key cannot paint stale rows.
            // Read-only — never assign back (that resets caret to end).
            string latest = _searchField.text ?? string.Empty;
            bool latestPreferHistory = string.IsNullOrWhiteSpace(latest);

            // Deferred stock SearchStop — clearing the box must not rebuild the parts list
            // on the delete key event itself (Harmony blocks empty OnValueChange while typing).
            if (latestPreferHistory)
            {
                StockSearchHelper.RestoreUnfilteredListAfterFieldCleared();
            }

            // Skip duplicate Match/Show when the settled query matches what is already painted.
            if (_dropdown != null
                && _dropdown.IsDropdownOpen
                && string.Equals(latest, _lastShownSuggestionQuery, StringComparison.Ordinal))
            {
                yield break;
            }

            // Keep _pendingSuggestionRefreshCoroutine set so Cancel can StopCoroutine mid-Match.
            yield return ShowSuggestionsIncremental(latest, latestPreferHistory, source, requestId);
            if (requestId == _suggestionRefreshRequestId)
            {
                _pendingSuggestionRefreshCoroutine = null;
            }
        }

        /// <summary>
        /// Typing settle path: metadata/categorizer stay sync (indexed counts, no PartLoader
        /// rescans); part Match is frame-sliced and cancelled if a newer key arrives.
        /// Prior dropdown rows stay visible until this paints.
        /// </summary>
        private IEnumerator ShowSuggestionsIncremental(
            string query,
            bool preferHistory,
            string source,
            int requestId)
        {
            if (_dropdown == null || !_readyForSuggestions || _applyingSuggestion)
            {
                yield break;
            }

            CancelPendingHide();
            SearchFieldCaretState caretBefore = CaptureSearchFieldCaret();
            string queryKey = query ?? string.Empty;

            ResolveSuggestionCaps(
                out int maxSuggestions,
                out int maxMetadata,
                out int maxCategorizerCandidates,
                out int maxCategorizer,
                out int maxTokenCategorizer);

            List<PartSuggestion> suggestions = new List<PartSuggestion>();
            bool queryEmpty = string.IsNullOrWhiteSpace(query);

            if (preferHistory)
            {
                foreach (string entry in _history.Match(query, maxSuggestions))
                {
                    suggestions.Add(new PartSuggestion
                    {
                        Kind = SuggestionKind.History,
                        QueryText = entry,
                        DisplayText = entry,
                        Part = null,
                        IsHistory = true,
                        RankScore = 0
                    });
                }
            }

            if (!queryEmpty)
            {
                List<PartSuggestion> metadataSuggestions = _metadataIndexReady
                    ? _metadataIndex.Match(query, maxMetadata).ToList()
                    : new List<PartSuggestion>();

                if (requestId != _suggestionRefreshRequestId)
                {
                    yield break;
                }

                List<PartSuggestion> categorizerSuggestions = _categorizerIndexReady
                    ? BudgetCategorizerSuggestions(
                        _categorizerIndex.Match(query, maxCategorizerCandidates).ToList(),
                        maxCategorizer,
                        maxTokenCategorizer)
                    : new List<PartSuggestion>();

                if (requestId != _suggestionRefreshRequestId)
                {
                    yield break;
                }

                bool categorical = SuggestionCategoricalQuery.LooksCategorical(query);
                int reservedFilters = categorical
                    ? MinReservedFilterSlotsWhenCategorical
                    : 1;
                int reservedMeta = MinReservedMetadataSlots;
                int reservedParts = MinReservedPartSlots;

                var takenFilters = new List<PartSuggestion>();
                for (int i = 0; i < categorizerSuggestions.Count && takenFilters.Count < maxCategorizer; i++)
                {
                    takenFilters.Add(categorizerSuggestions[i]);
                }

                var takenMeta = new List<PartSuggestion>();
                for (int i = 0; i < metadataSuggestions.Count && takenMeta.Count < maxMetadata; i++)
                {
                    takenMeta.Add(metadataSuggestions[i]);
                }

                int firstClassCount = takenMeta.Count + takenFilters.Count;
                int partBudget = maxSuggestions - firstClassCount;
                if (partBudget < reservedParts)
                {
                    while (partBudget < reservedParts && takenFilters.Count > reservedFilters)
                    {
                        takenFilters.RemoveAt(takenFilters.Count - 1);
                        partBudget++;
                    }

                    while (partBudget < reservedParts && takenMeta.Count > reservedMeta)
                    {
                        takenMeta.RemoveAt(takenMeta.Count - 1);
                        partBudget++;
                    }

                    if (partBudget < reservedParts)
                    {
                        partBudget = reservedParts;
                    }
                }

                if (partBudget < 6 && !categorical)
                {
                    partBudget = Math.Min(6, maxSuggestions - reservedFilters);
                    if (partBudget < reservedParts)
                    {
                        partBudget = reservedParts;
                    }
                }

                var partSuggestions = new List<PartSuggestion>(partBudget);
                yield return _index.MatchIncremental(
                    query,
                    partBudget,
                    partSuggestions,
                    () => requestId != _suggestionRefreshRequestId || _applyingSuggestion);

                if (requestId != _suggestionRefreshRequestId || _applyingSuggestion)
                {
                    yield break;
                }

                // Re-read live field — user may have typed during the slice; if so, abandon paint.
                if (_searchField != null
                    && !string.Equals(_searchField.text ?? string.Empty, queryKey, StringComparison.Ordinal))
                {
                    yield break;
                }

                for (int i = 0; i < partSuggestions.Count; i++)
                {
                    PartSuggestion part = partSuggestions[i];
                    if (IsRedundantPartSuggestion(part, takenMeta, takenFilters))
                    {
                        continue;
                    }

                    suggestions.Add(part);
                }

                suggestions.AddRange(takenMeta);
                suggestions.AddRange(takenFilters);

                suggestions = SuggestionDedupHelper.Dedup(suggestions, query);

                suggestions = suggestions
                    .Where(entry => entry.IsValid())
                    .OrderBy(entry => entry.RankScore)
                    .ThenBy(entry => StockTabSortPriority(entry))
                    .ThenBy(entry => SuggestionDedupHelper.GetKindPriority(entry))
                    .ThenBy(entry => entry.DisplayText, StringComparer.OrdinalIgnoreCase)
                    .Take(maxSuggestions)
                    .ToList();
            }

            if (requestId != _suggestionRefreshRequestId || _applyingSuggestion)
            {
                yield break;
            }

            // Re-capture after frame-sliced Match — arrows during refresh must not jump back.
            caretBefore = CaptureSearchFieldCaret();
            PaintSuggestionDropdown(
                suggestions,
                query,
                preferHistory,
                queryEmpty,
                source,
                queryKey,
                caretBefore);
        }

        private static bool IsSearchFieldStillActive(TMP_InputField searchField)
        {
            if (searchField == null)
            {
                return false;
            }

            if (searchField.isFocused)
            {
                return true;
            }

            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            return selected != null && selected == searchField.gameObject;
        }

        private void OnSearchSelected(string _)
        {
            EnsureSearchFieldKeyboardLock();
            PlaceCaretAtEndForFreshFocus();
            StockSearchHelper.CancelPendingStockSearchForTyping("search-field-focus");
            EditorBootstrap.Log("OnSearchSelected: field focused.");
            RequestShowFromInteraction("select");
        }

        private void OnSearchDeselected(string _)
        {
            if (IsSearchFieldStillActive(_searchField))
            {
                // Dropdown rebuild / collapse can fire a spurious deselect — keep lock + ignore.
                EnsureSearchFieldKeyboardLock();
                EditorBootstrap.Log("OnSearchDeselected: field still active — ignore stale blur.");
                return;
            }

            _preferCaretAtEndThisFocus = false;
            _userAdjustedCaretThisFocus = false;
            _caretAtEndGraceFrames = 0;
            ReleaseSearchFieldKeyboardLock();
            EditorBootstrap.Log("OnSearchDeselected: field blurred.");
        }

        internal void HandleSearchFieldPointerDown()
        {
            if (_pointerGraceFramesRemaining > 0)
            {
                EditorBootstrap.Log(
                    "HandleSearchFieldPointerDown: ignored — within post-hook grace period.");
                return;
            }

            PlaceCaretAtEndForFreshFocus();
            EditorBootstrap.Log("HandleSearchFieldPointerDown: show request.");
            RequestShowFromInteraction("pointer-down");
        }

        internal void HandleSearchFieldPointerClick()
        {
            // Re-assert end caret on click unless the user already moved it this focus session.
            if (!_userAdjustedCaretThisFocus)
            {
                PlaceCaretAtEndForFreshFocus();
            }

            EditorBootstrap.Log("HandleSearchFieldPointerClick: idempotent show request.");
            RequestShowFromInteraction("click");
        }

        private void RequestShowFromInteraction(string source)
        {
            if (_applyingSuggestion)
            {
                EditorBootstrap.Log("RequestShowFromInteraction(" + source + "): skipped — applying suggestion.");
                return;
            }

            CancelPendingHide();
            CancelPendingSuggestionRefresh();

            if (!_readyForSuggestions || _searchField == null)
            {
                EditorBootstrap.Log(
                    "RequestShowFromInteraction("
                    + source
                    + "): not ready — queue retry next frame.");
                StartCoroutine(RetryShowFromInteractionNextFrame(source));
                return;
            }

            ShowSuggestions(
                _searchField.text,
                preferHistory: string.IsNullOrWhiteSpace(_searchField.text),
                source: source);
        }

        private IEnumerator RetryShowFromInteractionNextFrame(string source)
        {
            const float maxWaitSeconds = 12f;
            float deadline = Time.unscaledTime + maxWaitSeconds;

            while (!_readyForSuggestions && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            if (_applyingSuggestion || _searchField == null)
            {
                yield break;
            }

            if (!_readyForSuggestions)
            {
                EditorBootstrap.Log(
                    "RetryShowFromInteraction("
                    + source
                    + "): still not ready after "
                    + maxWaitSeconds
                    + "s.");
                yield break;
            }

            EditorBootstrap.Log("RetryShowFromInteraction(" + source + "): retrying ShowSuggestions.");
            ShowSuggestions(
                _searchField.text,
                preferHistory: string.IsNullOrWhiteSpace(_searchField.text),
                source: "retry-" + source);
        }

        private void OnSearchValueChanged(string value)
        {
            if (_applyingSuggestion)
            {
                return;
            }

            string text = value ?? string.Empty;
            int length = text.Length;
            bool shortening = _lastValueChangedLength >= 0 && length < _lastValueChangedLength;
            _lastValueChangedLength = length;

            StockSearchHelper.CancelPendingStockSearchForTyping("value-changed");
            CancelPendingHide();
            // Cancel in-flight Match on every change (including backspace/delete). Never run
            // full sync Match on the key event — Schedule waits idle debounce first.
            CancelPendingSuggestionRefresh();
            float debounce = shortening
                ? SuggestionRefreshShortenDebounceSeconds
                : SuggestionRefreshDebounceSeconds;
            ScheduleSuggestionRefresh(
                text,
                preferHistory: string.IsNullOrWhiteSpace(text),
                source: "value-changed",
                debounceSeconds: debounce);
        }

        private void OnSearchSubmit(string value)
        {
            EditorBootstrap.Log("OnSearchSubmit (Enter): '" + (value ?? string.Empty) + "'.");

            CancelPendingSuggestionRefresh();

            string trimmed = (value ?? string.Empty).Trim();
            CommitSearchHistory(value);

            // Same hang risk as history: ApplyEnterSearch must not run while the dropdown
            // still owns/blocks partsEditor Transition("In"). Hide first, defer apply.
            if (_dropdown != null && _dropdown.IsDropdownOpen)
            {
                _dropdown.Hide();
            }
            else
            {
                ScheduleHideAfterSubmit("submit");
            }

            if (!_applyingSuggestion && trimmed.Length >= SuggestionQueryGuards.MinSuggestionQueryLength)
            {
                if (_pendingHistoryApplyCoroutine != null)
                {
                    StopCoroutine(_pendingHistoryApplyCoroutine);
                }

                _pendingHistoryApplyCoroutine = StartCoroutine(ApplyHistoryEnterDeferred(trimmed));
            }
        }

        private void OnSearchEndEdit(string value)
        {
            EditorBootstrap.Log("OnSearchEndEdit: '" + (value ?? string.Empty) + "'.");

            if (IsSearchFieldStillActive(_searchField))
            {
                EnsureSearchFieldKeyboardLock();
                EditorBootstrap.Log("OnSearchEndEdit: field still active — skip blur/hide schedule.");
                return;
            }

            ReleaseSearchFieldKeyboardLock();
            CancelPendingSuggestionRefresh();
            ScheduleHideAfterSubmit("end-edit");
            CommitSearchHistory(value);
        }

        private void CommitSearchHistory(string value)
        {
            if (_applyingSuggestion)
            {
                return;
            }

            string trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length < 2)
            {
                return;
            }

            if (string.Equals(trimmed, _lastCommittedQuery, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _history.Remember(trimmed);
            _lastCommittedQuery = trimmed;
            EditorBootstrap.Log("CommitSearchHistory: remembered '" + trimmed + "'.");
        }

        /// <summary>
        /// Additive history for clicked suggestions (display text). Typed Enter/submit still uses
        /// <see cref="CommitSearchHistory"/> unchanged. Dedupes / moves-to-top via Remember.
        /// </summary>
        private void RememberClickedSuggestion(string value)
        {
            string trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length < 2)
            {
                return;
            }

            _history.Remember(trimmed);
            _lastCommittedQuery = trimmed;
            EditorBootstrap.Log("RememberClickedSuggestion: remembered '" + trimmed + "'.");
        }

        private void ClearSearchHistory()
        {
            _history.Clear();
            _lastCommittedQuery = string.Empty;
            EditorBootstrap.Log("ClearSearchHistory: history cleared.");

            if (_searchField == null || _dropdown == null)
            {
                return;
            }

            CancelPendingSuggestionRefresh();

            if (string.IsNullOrWhiteSpace(_searchField.text))
            {
                ShowSuggestions(_searchField.text, preferHistory: true, source: "clear-history");
            }
            else if (_dropdown.IsDropdownOpen)
            {
                _dropdown.Hide();
            }
        }

        private void ScheduleHideAfterSubmit(string reason)
        {
            int requestId = ++_hideRequestId;
            CancelPendingHide();
            _pendingHideCoroutine = StartCoroutine(HideDropdownAfterSubmit(requestId, reason));
        }

        private IEnumerator HideDropdownAfterSubmit(int requestId, string reason)
        {
            yield return null;
            yield return null;

            if (requestId != _hideRequestId)
            {
                EditorBootstrap.Log(
                    "HideDropdownAfterSubmit("
                    + reason
                    + "): stale request "
                    + requestId
                    + " — skip.");
                yield break;
            }

            _pendingHideCoroutine = null;

            if (_dropdown != null && _dropdown.IsDropdownOpen && !_applyingSuggestion)
            {
                EditorBootstrap.Log("HideDropdownAfterSubmit(" + reason + "): hiding dropdown.");
                _dropdown.Hide();
            }
            else
            {
                EditorBootstrap.Log(
                    "HideDropdownAfterSubmit("
                    + reason
                    + "): no hide needed — open="
                    + (_dropdown != null && _dropdown.IsDropdownOpen)
                    + ", applying="
                    + _applyingSuggestion
                    + ".");
            }

            _applyingSuggestion = false;
        }

        private void DismissDropdown()
        {
            CancelPendingHide();
            CancelPendingSuggestionRefresh();
            _lastShownSuggestionQuery = null;

            if (_dropdown != null && _dropdown.IsDropdownOpen)
            {
                EditorBootstrap.Log("DismissDropdown: hiding dropdown.");
                _dropdown.Hide();
            }

            if (_searchField != null)
            {
                ReleaseSearchFieldKeyboardLock();
                _searchField.DeactivateInputField();
            }
        }

        private void ResolveSuggestionCaps(
            out int maxSuggestions,
            out int maxMetadata,
            out int maxCategorizerCandidates,
            out int maxCategorizer,
            out int maxTokenCategorizer)
        {
            int partCount = _index != null ? _index.PartCount : 0;
            bool heavy = partCount >= HeavyIndexPartThreshold;
            maxSuggestions = heavy ? MaxSuggestionsHeavy : MaxSuggestionsLight;
            maxMetadata = heavy ? MaxMetadataSuggestionsHeavy : MaxMetadataSuggestionsLight;
            maxCategorizerCandidates = heavy
                ? MaxCategorizerCandidatesHeavy
                : MaxCategorizerCandidatesLight;
            maxCategorizer = heavy ? MaxCategorizerSuggestionsHeavy : MaxCategorizerSuggestionsLight;
            maxTokenCategorizer = heavy
                ? MaxTokenCategorizerSuggestionsHeavy
                : MaxTokenCategorizerSuggestionsLight;
        }

        private void ShowSuggestions(string query, bool preferHistory, string source = "unknown")
        {
            if (_dropdown == null)
            {
                return;
            }

            if (!_readyForSuggestions)
            {
                EditorBootstrap.Log(
                    "ShowSuggestions("
                    + source
                    + "): index not ready — retry next frame for '"
                    + (query ?? string.Empty)
                    + "'.");
                StartCoroutine(RetryShowSuggestionsNextFrame(query, preferHistory, source));
                return;
            }

            CancelPendingHide();

            // Capture before Match/Show — dropdown rebuild + parts-panel collapse must not
            // drop KEYBOARDINPUT lock (breaks Left/Right/Home/End). Fresh focus prefers end;
            // debounce typing refreshes preserve mid-edit caret via RestoreSearchFieldCaret.
            SearchFieldCaretState caretBefore = CaptureSearchFieldCaret();
            string queryKey = query ?? string.Empty;

            ResolveSuggestionCaps(
                out int maxSuggestions,
                out int maxMetadata,
                out int maxCategorizerCandidates,
                out int maxCategorizer,
                out int maxTokenCategorizer);

            List<PartSuggestion> suggestions = new List<PartSuggestion>();

            bool queryEmpty = string.IsNullOrWhiteSpace(query);

            if (preferHistory)
            {
                foreach (string entry in _history.Match(query, maxSuggestions))
                {
                    suggestions.Add(new PartSuggestion
                    {
                        Kind = SuggestionKind.History,
                        QueryText = entry,
                        DisplayText = entry,
                        Part = null,
                        IsHistory = true,
                        RankScore = 0
                    });
                }
            }


            if (!string.IsNullOrWhiteSpace(query))
            {
                List<PartSuggestion> metadataSuggestions = _metadataIndexReady
                    ? _metadataIndex.Match(query, maxMetadata).ToList()
                    : new List<PartSuggestion>();

                List<PartSuggestion> categorizerSuggestions = _categorizerIndexReady
                    ? BudgetCategorizerSuggestions(
                        _categorizerIndex.Match(query, maxCategorizerCandidates).ToList(),
                        maxCategorizer,
                        maxTokenCategorizer)
                    : new List<PartSuggestion>();

                bool categorical = SuggestionCategoricalQuery.LooksCategorical(query);
                AssembleTypeAwareSuggestions(
                    suggestions,
                    metadataSuggestions,
                    categorizerSuggestions,
                    query,
                    categorical,
                    maxSuggestions,
                    maxMetadata,
                    maxCategorizer);

                suggestions = SuggestionDedupHelper.Dedup(suggestions, query);

                // IndexedPartCount makes IsValid O(1) — never RefreshVerifiedSubtitle (that used
                // to re-scan PartLoader per filter/mod row and hitch on words like "solar").
                suggestions = suggestions
                    .Where(entry => entry.IsValid())
                    .OrderBy(entry => entry.RankScore)
                    .ThenBy(entry => StockTabSortPriority(entry))
                    .ThenBy(entry => SuggestionDedupHelper.GetKindPriority(entry))
                    .ThenBy(entry => entry.DisplayText, StringComparer.OrdinalIgnoreCase)
                    .Take(maxSuggestions)
                    .ToList();
            }

            PaintSuggestionDropdown(
                suggestions,
                query,
                preferHistory,
                queryEmpty,
                source,
                queryKey,
                caretBefore);
        }

        private void PaintSuggestionDropdown(
            List<PartSuggestion> suggestions,
            string query,
            bool preferHistory,
            bool queryEmpty,
            string source,
            string queryKey,
            SearchFieldCaretState caretBefore)
        {
            bool showBrandingFooter = queryEmpty;

            if (suggestions.Count == 0 && !showBrandingFooter)
            {
                EditorBootstrap.Log(
                    "ShowSuggestions("
                    + source
                    + "): no matches for '"
                    + (query ?? string.Empty)
                    + "' — hiding dropdown.");
                if (_dropdown.IsDropdownOpen)
                {
                    _dropdown.Hide();
                }

                _lastShownSuggestionQuery = queryKey;
                RestoreSearchFieldCaret(caretBefore);
                return;
            }

            EditorBootstrap.Log(
                "ShowSuggestions("
                + source
                + "): "
                + suggestions.Count
                + " match(es) for '"
                + (query ?? string.Empty)
                + "'"
                + (showBrandingFooter ? " (branding footer)" : string.Empty)
                + (_dropdown.IsDropdownOpen ? " (refresh open dropdown)" : string.Empty)
                + ".");

            bool historyMode = preferHistory && queryEmpty && _history.Entries.Count > 0;
            bool showClearHistory = historyMode;
            string headerOverride = null;
            if (!historyMode && !queryEmpty && !_fullSearchReady)
            {
                headerOverride = "Loading suggestions…";
            }

            _dropdown.Show(
                suggestions,
                historyMode,
                showClearHistory,
                headerOverride,
                showBrandingFooter);

            _lastShownSuggestionQuery = queryKey;
            RestoreSearchFieldCaret(caretBefore);
        }

        private IEnumerator RetryShowSuggestionsNextFrame(string query, bool preferHistory, string source)
        {
            const float maxWaitSeconds = 12f;
            float deadline = Time.unscaledTime + maxWaitSeconds;

            while (!_readyForSuggestions && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            if (!_readyForSuggestions || _searchField == null || _applyingSuggestion)
            {
                yield break;
            }

            ShowSuggestions(query, preferHistory, "retry-" + source);
        }

        /// <summary>
        /// Prefer function/category/manufacturer/diameter rows, then fill remaining slots with
        /// tag/module/resource/tech so short categorical prefixes (e.g. "eng") keep Engines/Engine
        /// near the top instead of drowning in token matches.
        /// </summary>
        private static List<PartSuggestion> BudgetCategorizerSuggestions(
            List<PartSuggestion> candidates,
            int maxCategorizer,
            int maxTokenCategorizer)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return new List<PartSuggestion>();
            }

            var highValue = new List<PartSuggestion>(maxCategorizer);
            var tokenStyle = new List<PartSuggestion>(maxTokenCategorizer);

            for (int i = 0; i < candidates.Count; i++)
            {
                PartSuggestion candidate = candidates[i];
                if (candidate == null)
                {
                    continue;
                }

                if (IsHighValueCategorizerKind(candidate.Kind))
                {
                    if (highValue.Count < maxCategorizer)
                    {
                        highValue.Add(candidate);
                    }
                }
                else if (tokenStyle.Count < maxTokenCategorizer)
                {
                    tokenStyle.Add(candidate);
                }
            }

            var budgeted = new List<PartSuggestion>(maxCategorizer);
            budgeted.AddRange(highValue);
            for (int i = 0; i < tokenStyle.Count && budgeted.Count < maxCategorizer; i++)
            {
                budgeted.Add(tokenStyle[i]);
            }

            return budgeted;
        }

        private static bool IsHighValueCategorizerKind(SuggestionKind kind)
        {
            switch (kind)
            {
                case SuggestionKind.FilterFunction:
                case SuggestionKind.FilterCategory:
                case SuggestionKind.FilterManufacturer:
                case SuggestionKind.FilterDiameter:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Strong stock Function/Category matches (display/alias prefix boost → RankScore &lt; 0)
        /// sort ahead of parts/mods at the same band so Engines/Thermal stay visible near the top.
        /// </summary>
        private static int StockTabSortPriority(PartSuggestion suggestion)
        {
            if (suggestion == null)
            {
                return 1;
            }

            if ((suggestion.Kind == SuggestionKind.FilterFunction
                    || suggestion.Kind == SuggestionKind.FilterCategory)
                && suggestion.RankScore < 0)
            {
                return 0;
            }

            return 1;
        }

        /// <summary>
        /// Type-aware mix: when the query looks categorical, reserve filter slots so parts/mods
        /// don't starve Engines / Engine-style rows.
        /// </summary>
        private void AssembleTypeAwareSuggestions(
            List<PartSuggestion> destination,
            List<PartSuggestion> metadataSuggestions,
            List<PartSuggestion> categorizerSuggestions,
            string query,
            bool categorical,
            int maxSuggestions,
            int maxMetadata,
            int maxCategorizer)
        {
            int reservedFilters = categorical
                ? MinReservedFilterSlotsWhenCategorical
                : 1;
            int reservedMeta = MinReservedMetadataSlots;
            int reservedParts = MinReservedPartSlots;

            var takenFilters = new List<PartSuggestion>();
            for (int i = 0; i < categorizerSuggestions.Count && takenFilters.Count < maxCategorizer; i++)
            {
                takenFilters.Add(categorizerSuggestions[i]);
            }

            var takenMeta = new List<PartSuggestion>();
            for (int i = 0; i < metadataSuggestions.Count && takenMeta.Count < maxMetadata; i++)
            {
                takenMeta.Add(metadataSuggestions[i]);
            }

            int firstClassCount = takenMeta.Count + takenFilters.Count;
            int partBudget = maxSuggestions - firstClassCount;
            if (partBudget < reservedParts)
            {
                while (partBudget < reservedParts && takenFilters.Count > reservedFilters)
                {
                    takenFilters.RemoveAt(takenFilters.Count - 1);
                    partBudget++;
                }

                while (partBudget < reservedParts && takenMeta.Count > reservedMeta)
                {
                    takenMeta.RemoveAt(takenMeta.Count - 1);
                    partBudget++;
                }

                if (partBudget < reservedParts)
                {
                    partBudget = reservedParts;
                }
            }

            if (partBudget < 6 && !categorical)
            {
                partBudget = Math.Min(6, maxSuggestions - reservedFilters);
                if (partBudget < reservedParts)
                {
                    partBudget = reservedParts;
                }
            }

            List<PartSuggestion> partSuggestions = new List<PartSuggestion>(partBudget);
            _index.MatchInto(query, partBudget, partSuggestions);
            for (int i = 0; i < partSuggestions.Count; i++)
            {
                PartSuggestion part = partSuggestions[i];
                if (IsRedundantPartSuggestion(part, takenMeta, takenFilters))
                {
                    continue;
                }

                destination.Add(part);
            }

            destination.AddRange(takenMeta);
            destination.AddRange(takenFilters);
        }

        private static bool IsRedundantPartSuggestion(
            PartSuggestion part,
            List<PartSuggestion> metadataSuggestions,
            List<PartSuggestion> categorizerSuggestions)
        {
            if (part == null || part.Kind != SuggestionKind.Part || part.Part == null)
            {
                return false;
            }

            if (IsRedundantAgainstMetadata(part, metadataSuggestions))
            {
                return true;
            }

            return IsRedundantAgainstCategorizer(part, categorizerSuggestions);
        }

        private static bool IsRedundantAgainstMetadata(PartSuggestion part, List<PartSuggestion> metadataSuggestions)
        {
            if (metadataSuggestions == null)
            {
                return false;
            }

            string matchReason = part.MatchReason ?? string.Empty;

            for (int i = 0; i < metadataSuggestions.Count; i++)
            {
                PartSuggestion metadata = metadataSuggestions[i];
                if (metadata == null)
                {
                    continue;
                }

                if (metadata.Kind == SuggestionKind.ModAuthor
                    && matchReason.StartsWith("author:", System.StringComparison.OrdinalIgnoreCase))
                {
                    string authorValue = matchReason.Substring("author:".Length);
                    if (AuthorCanonicalizer.TokensAreEquivalent(authorValue, metadata.FilterKey))
                    {
                        return true;
                    }
                }

                if (metadata.Kind == SuggestionKind.ModName
                    && (matchReason.StartsWith("mod:", System.StringComparison.OrdinalIgnoreCase)
                        || matchReason.StartsWith("modfolder:", System.StringComparison.OrdinalIgnoreCase)))
                {
                    string partFolder = ModMetadataCache.ExtractModFolderFromPart(part.Part);
                    if (string.Equals(partFolder, metadata.FilterKey, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                if (metadata.Kind == SuggestionKind.ModSuite
                    && (matchReason.StartsWith("mod:", System.StringComparison.OrdinalIgnoreCase)
                        || matchReason.StartsWith("modfolder:", System.StringComparison.OrdinalIgnoreCase)))
                {
                    if (ModFilterMatcher.PartMatchesModSuite(part.Part, metadata.FilterKey))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsRedundantAgainstCategorizer(PartSuggestion part, List<PartSuggestion> categorizerSuggestions)
        {
            if (categorizerSuggestions == null || categorizerSuggestions.Count == 0)
            {
                return false;
            }

            string matchReason = part.MatchReason ?? string.Empty;

            for (int i = 0; i < categorizerSuggestions.Count; i++)
            {
                PartSuggestion categorizer = categorizerSuggestions[i];
                if (categorizer == null || string.IsNullOrWhiteSpace(categorizer.FilterKey))
                {
                    continue;
                }

                if (categorizer.Kind == SuggestionKind.FilterManufacturer
                    && matchReason.StartsWith("manufacturer:", System.StringComparison.OrdinalIgnoreCase))
                {
                    string manufacturerValue = matchReason.Substring("manufacturer:".Length);
                    if (string.Equals(manufacturerValue, categorizer.FilterKey, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                if (categorizer.Kind == SuggestionKind.FilterCategory
                    && matchReason.StartsWith("category:", System.StringComparison.OrdinalIgnoreCase))
                {
                    string categoryValue = matchReason.Substring("category:".Length);
                    if (CategoryMatchReasonEquals(categoryValue, categorizer.FilterKey, categorizer.DisplayText))
                    {
                        return true;
                    }
                }

                if (categorizer.Kind == SuggestionKind.FilterFunction
                    && (matchReason.StartsWith("category:", System.StringComparison.OrdinalIgnoreCase)
                        || matchReason.StartsWith("auto:", System.StringComparison.OrdinalIgnoreCase)
                        || matchReason.StartsWith("module:", System.StringComparison.OrdinalIgnoreCase)))
                {
                    if (PartFilterMatcher.PartMatchesFilter(
                        categorizer.Kind,
                        categorizer.FilterKey,
                        part.Part))
                    {
                        return true;
                    }
                }

                if (categorizer.Kind == SuggestionKind.FilterDiameter
                    && matchReason.StartsWith("tag:", System.StringComparison.OrdinalIgnoreCase))
                {
                    string tagValue = matchReason.Substring("tag:".Length);
                    if (string.Equals(tagValue, categorizer.FilterKey, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                if (categorizer.Kind == SuggestionKind.FilterTag
                    && matchReason.StartsWith("tag:", System.StringComparison.OrdinalIgnoreCase))
                {
                    string tagValue = matchReason.Substring("tag:".Length);
                    if (string.Equals(tagValue, categorizer.FilterKey, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                if (categorizer.Kind == SuggestionKind.FilterModule
                    && matchReason.StartsWith("module:", System.StringComparison.OrdinalIgnoreCase))
                {
                    string moduleValue = matchReason.Substring("module:".Length);
                    if (string.Equals(moduleValue, categorizer.FilterKey, System.StringComparison.OrdinalIgnoreCase)
                        || string.Equals(moduleValue, categorizer.DisplayText, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                if (categorizer.Kind == SuggestionKind.FilterTech
                    && matchReason.StartsWith("tech:", System.StringComparison.OrdinalIgnoreCase))
                {
                    string techValue = matchReason.Substring("tech:".Length);
                    if (string.Equals(techValue, categorizer.FilterKey, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CategoryMatchReasonEquals(string matchValue, string filterKey, string displayText)
        {
            if (string.Equals(matchValue, filterKey, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(matchValue, displayText, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(
                AuthorMatchHelper.FormatDisplayName(matchValue),
                displayText,
                System.StringComparison.OrdinalIgnoreCase);
        }

        private void ApplySuggestion(PartSuggestion suggestion)
        {
            _applyingSuggestion = true;
            CancelPendingHide();
            CancelPendingSuggestionRefresh();

            try
            {
                if (_searchField == null || suggestion == null)
                {
                    EditorBootstrap.LogWarning("ApplySuggestion skipped — field or suggestion invalid.");
                    return;
                }

                string displayText = suggestion.DisplayText ?? suggestion.QueryText ?? string.Empty;

                switch (suggestion.Kind)
                {
                    case SuggestionKind.History:
                        if (string.IsNullOrEmpty(suggestion.QueryText))
                        {
                            EditorBootstrap.LogWarning("ApplySuggestion skipped — history query invalid.");
                            return;
                        }

                        // Before apply: click must still land in history if Apply throws.
                        RememberClickedSuggestion(suggestion.QueryText);
                        // Defer ApplyEnterSearch until after dropdown Hide/Restore settles.
                        // One-frame defer alone was insufficient — wait for partsEditor idle
                        // (or ForceComplete) under Transition(In) force-allow, then tight filter.
                        _pendingHistoryQuery = suggestion.QueryText;
                        EditorBootstrap.Log(
                            "ApplySuggestion (history→enter deferred): '" + suggestion.QueryText + "'");
                        break;

                    case SuggestionKind.ModAuthor:
                        RememberClickedSuggestion(displayText);
                        EditorBootstrap.Log("ApplySuggestion (author): '" + displayText + "'");
                        StockSearchHelper.ApplyModAuthorFilter(suggestion.FilterKey ?? displayText, displayText);
                        break;

                    case SuggestionKind.ModName:
                        RememberClickedSuggestion(displayText);
                        EditorBootstrap.Log("ApplySuggestion (mod): '" + displayText + "'");
                        StockSearchHelper.ApplyModNameFilter(suggestion.FilterKey ?? displayText, displayText);
                        break;

                    case SuggestionKind.ModSuite:
                        RememberClickedSuggestion(displayText);
                        EditorBootstrap.Log("ApplySuggestion (suite): '" + displayText + "'");
                        StockSearchHelper.ApplyModSuiteFilter(suggestion.FilterKey ?? suggestion.QueryText ?? displayText, displayText);
                        break;

                    case SuggestionKind.FilterFunction:
                    case SuggestionKind.FilterManufacturer:
                    case SuggestionKind.FilterDiameter:
                    case SuggestionKind.FilterCategory:
                        RememberClickedSuggestion(displayText);
                        EditorBootstrap.Log("ApplySuggestion (categorizer): '" + displayText + "' kind=" + suggestion.Kind);
                        StockSearchHelper.ApplyCategorizerFilter(suggestion);
                        break;

                    case SuggestionKind.FilterModule:
                    case SuggestionKind.FilterResource:
                    case SuggestionKind.FilterTech:
                    case SuggestionKind.FilterTag:
                    {
                        // Same inclusive Enter predicate as typed Enter / history — facet-only
                        // filters drop title/name/tag/resource co-hits for the same token.
                        RememberClickedSuggestion(displayText);
                        string inclusiveQuery = suggestion.FilterKey ?? suggestion.QueryText ?? displayText;
                        EditorBootstrap.Log(
                            "ApplySuggestion (inclusive enter): '"
                            + inclusiveQuery
                            + "' kind="
                            + suggestion.Kind);
                        StockSearchHelper.ApplyEnterSearch(
                            inclusiveQuery,
                            _index,
                            _metadataIndex,
                            _categorizerIndex,
                            _metadataIndexReady,
                            _categorizerIndexReady);
                        break;
                    }
                    case SuggestionKind.Part:
                    default:
                        if (suggestion.Part == null)
                        {
                            EditorBootstrap.LogWarning("ApplySuggestion skipped — part suggestion invalid.");
                            return;
                        }

                        RememberClickedSuggestion(displayText);
                        EditorBootstrap.Log(
                            "ApplySuggestion (precise): '"
                            + displayText
                            + "' id="
                            + suggestion.Part.name);
                        StockSearchHelper.ApplyPrecisePart(suggestion.Part, displayText);
                        break;
                }
            }
            catch (Exception ex)
            {
                EditorBootstrap.LogWarning("ApplySuggestion failed — " + ex.Message);
                StockSearchHelper.RecoverAfterFailedApply();
                _pendingHistoryQuery = null;
            }
            finally
            {
                if (_dropdown != null && _dropdown.IsDropdownOpen)
                {
                    _dropdown.Hide();
                }

                if (_searchField != null)
                {
                    ReleaseSearchFieldKeyboardLock();
                    _searchField.DeactivateInputField();
                }

                string deferredHistoryQuery = _pendingHistoryQuery;
                _pendingHistoryQuery = null;
                _applyingSuggestion = false;

                if (!string.IsNullOrEmpty(deferredHistoryQuery))
                {
                    if (_pendingHistoryApplyCoroutine != null)
                    {
                        StopCoroutine(_pendingHistoryApplyCoroutine);
                    }

                    _pendingHistoryApplyCoroutine = StartCoroutine(
                        ApplyHistoryEnterDeferred(deferredHistoryQuery));
                }
            }
        }

        /// <summary>
        /// History rows use the same tight Enter filter as typed submit, but must not fight
        /// PartsPanelTransitionGuard / an in-flight partsEditor slide. Prior one-frame defer
        /// was not enough: Restore's Transition("In") still left Transitioning=true when
        /// ApplyEnterSearch hit SearchStop/Refresh, and swallowed Transition("In") could leave
        /// stock PanelTransitionIn waiting forever. Wait until the panel is idle (or force
        /// snap In), force-allow In for the apply, then install the tight custom filter.
        /// </summary>
        private IEnumerator ApplyHistoryEnterDeferred(string query)
        {
            // Let Hide/Restore and PointerDown unwind first.
            yield return null;

            _pendingHistoryApplyCoroutine = null;

            if (string.IsNullOrEmpty(query) || _applyingSuggestion)
            {
                yield break;
            }

            if (_dropdown != null && _dropdown.IsDropdownOpen)
            {
                _dropdown.Hide();
            }

            // Multi-frame wait until partsEditor is idle enough for SearchStop/Refresh.
            // Yields must stay outside try/catch (CS1626).
            const float maxWaitSeconds = 1.5f;
            float deadline = Time.unscaledTime + maxWaitSeconds;
            int frames = 0;
            while (PartsPanelCollapseHelper.IsPartsEditorBusy()
                   && Time.unscaledTime < deadline)
            {
                frames++;
                yield return null;
            }

            bool forceCompleted = false;
            if (PartsPanelCollapseHelper.IsPartsEditorBusy())
            {
                EditorBootstrap.LogWarning(
                    "ApplyHistoryEnterDeferred: partsEditor still busy after "
                    + maxWaitSeconds.ToString("F1")
                    + "s / "
                    + frames
                    + " frames — ForceCompletePartsEditorIn.");
                PartsPanelCollapseHelper.ForceCompletePartsEditorIn("history-apply");
                forceCompleted = true;
                yield return null;
            }
            else if (frames > 0)
            {
                EditorBootstrap.Log(
                    "ApplyHistoryEnterDeferred: waited "
                    + frames
                    + " frame(s) for partsEditor idle.");
            }

            _applyingSuggestion = true;
            using (PartsPanelTransitionGuard.EnterForceAllowInScope())
            {
                try
                {
                    if (forceCompleted && PartsPanelCollapseHelper.IsPartsEditorBusy())
                    {
                        PartsPanelCollapseHelper.ForceCompletePartsEditorIn("history-apply-retry");
                    }

                    EditorBootstrap.Log("ApplyHistoryEnterDeferred: '" + query + "'");
                    StockSearchHelper.ApplyEnterSearch(
                        query,
                        _index,
                        _metadataIndex,
                        _categorizerIndex,
                        _metadataIndexReady,
                        _categorizerIndexReady);
                }
                catch (Exception ex)
                {
                    EditorBootstrap.LogWarning("ApplyHistoryEnterDeferred failed — " + ex.Message);
                    StockSearchHelper.RecoverAfterFailedApply();
                }
                finally
                {
                    _applyingSuggestion = false;
                }
            }
        }
    }
}
