using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KSP.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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
        private const int MaxSuggestions = 24;
        private const int MaxMetadataSuggestions = 6;
        private const int MaxCategorizerSuggestions = 6;

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
        private string _originalSearchPlaceholder;
        private int _pointerGraceFramesRemaining;
        private string _lastCommittedQuery = string.Empty;
        private bool _applyingSuggestion;
        private Coroutine _pendingHideCoroutine;
        private int _hideRequestId;

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

            // Indexes are built only during save-load (GameLoadBootstrap). Hangar entry is UI hook only —
            // wait for readiness; never start BuildIfNeeded here (no hangar index/scan work).
            yield return GameLoadIndexService.WaitUntilBasicReady(this);

            _index = GameLoadIndexService.PartIndex;
            _metadataIndex = GameLoadIndexService.MetadataIndex;
            _categorizerIndex = GameLoadIndexService.CategorizerIndex;

            if (_index == null)
            {
                EditorBootstrap.LogWarning(
                    "Part index unavailable after save-load wait — dropdown disabled (no hangar rebuild).");
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
            _metadataIndexReady = GameLoadIndexService.IsFullReady;
            _categorizerIndexReady = GameLoadIndexService.IsFullReady;
            _fullSearchReady = GameLoadIndexService.IsFullReady;
            UpdateSearchFieldIndexingPlaceholder(!_fullSearchReady);
            EditorBootstrap.Log("Search ready (basic)");
            EditorBootstrap.Log("Hooked native editor search field.");
            TryShowDropdownIfSearchFieldActive("editor-ready");

            if (!_fullSearchReady)
            {
                StartCoroutine(WaitForFullIndexBackground());
            }
            else
            {
                IndexDebugDump.LogIfEnabled(_index, _metadataIndex, _categorizerIndex);
            }
        }

        private IEnumerator WaitForFullIndexBackground()
        {
            yield return GameLoadIndexService.WaitUntilFullReady(this);
            _metadataIndexReady = true;
            _categorizerIndexReady = true;
            TryFinalizeFullSearchReady();
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

        private void TryFinalizeFullSearchReady()
        {
            if (_fullSearchReady || !_metadataIndexReady || !_categorizerIndexReady)
            {
                return;
            }

            _fullSearchReady = true;
            IndexDebugDump.LogIfEnabled(_index, _metadataIndex, _categorizerIndex);
            UpdateSearchFieldIndexingPlaceholder(false);
            EditorBootstrap.Log("Search ready (full)");

            if (_dropdown == null || _searchField == null || !_dropdown.IsVisible)
            {
                return;
            }

            string query = _searchField.text ?? string.Empty;
            ShowSuggestions(
                query,
                preferHistory: string.IsNullOrWhiteSpace(query),
                source: "full-index-ready");
        }

        private void UpdateSearchFieldIndexingPlaceholder(bool indexing)
        {
            if (_searchField == null)
            {
                return;
            }

            TextMeshProUGUI placeholder = _searchField.placeholder as TextMeshProUGUI;
            if (placeholder == null)
            {
                return;
            }

            if (indexing)
            {
                if (_originalSearchPlaceholder == null)
                {
                    _originalSearchPlaceholder = placeholder.text;
                }

                placeholder.text = "Loading suggestions…";
                return;
            }

            if (_originalSearchPlaceholder != null)
            {
                placeholder.text = _originalSearchPlaceholder;
            }
        }

        private void HookSearchField()
        {
            if (_hooked || _searchField == null)
            {
                return;
            }

            _searchField.onValueChanged.AddListener(OnSearchValueChanged);
            _searchField.onSelect.AddListener(OnSearchSelected);
            _searchField.onDeselect.AddListener(OnSearchDeselected);
            _searchField.onEndEdit.AddListener(OnSearchEndEdit);
            _searchField.onSubmit.AddListener(OnSearchSubmit);
            _hooked = true;
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

            if (_dropdown != null)
            {
                _dropdown.HideWithoutHoldNotify();
                _dropdown.OnSuggestionChosen -= ApplySuggestion;
                _dropdown.OnDismissed -= DismissDropdown;
                _dropdown.OnClearHistoryRequested -= ClearSearchHistory;
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
            StockSearchHelper.CancelPendingStockSearchForTyping("search-field-focus");
            EditorBootstrap.Log("OnSearchSelected: field focused.");
            RequestShowFromInteraction("select");
        }

        private void OnSearchDeselected(string _)
        {
            if (IsSearchFieldStillActive(_searchField))
            {
                EditorBootstrap.Log("OnSearchDeselected: field still active — ignore stale blur.");
                return;
            }

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

            EditorBootstrap.Log("HandleSearchFieldPointerDown: show request.");
            RequestShowFromInteraction("pointer-down");
        }

        internal void HandleSearchFieldPointerClick()
        {
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

            StockSearchHelper.CancelPendingStockSearchForTyping("value-changed");
            CancelPendingHide();
            ShowSuggestions(value, preferHistory: string.IsNullOrWhiteSpace(value), source: "value-changed");
        }

        private void OnSearchSubmit(string value)
        {
            EditorBootstrap.Log("OnSearchSubmit (Enter): '" + (value ?? string.Empty) + "'.");

            if (!_applyingSuggestion)
            {
                StockSearchHelper.ApplyEnterSearch(
                    value,
                    _index,
                    _metadataIndex,
                    _categorizerIndex,
                    _metadataIndexReady,
                    _categorizerIndexReady);
            }

            CommitSearchHistory(value);
            ScheduleHideAfterSubmit("submit");
        }

        private void OnSearchEndEdit(string value)
        {
            EditorBootstrap.Log("OnSearchEndEdit: '" + (value ?? string.Empty) + "'.");

            if (IsSearchFieldStillActive(_searchField))
            {
                EditorBootstrap.Log("OnSearchEndEdit: field still active — skip blur/hide schedule.");
                return;
            }

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

        private void ClearSearchHistory()
        {
            _history.Clear();
            _lastCommittedQuery = string.Empty;
            EditorBootstrap.Log("ClearSearchHistory: history cleared.");

            if (_searchField == null || _dropdown == null)
            {
                return;
            }

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

            if (_dropdown != null && _dropdown.IsDropdownOpen)
            {
                EditorBootstrap.Log("DismissDropdown: hiding dropdown.");
                _dropdown.Hide();
            }

            if (_searchField != null)
            {
                _searchField.DeactivateInputField();
            }
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

            List<PartSuggestion> suggestions = new List<PartSuggestion>();

            bool queryEmpty = string.IsNullOrWhiteSpace(query);

            if (preferHistory)
            {
                foreach (string entry in _history.Match(query, MaxSuggestions))
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
                    ? _metadataIndex.Match(query, MaxMetadataSuggestions).ToList()
                    : new List<PartSuggestion>();

                List<PartSuggestion> categorizerSuggestions = _categorizerIndexReady
                    ? _categorizerIndex.Match(query, MaxCategorizerSuggestions).ToList()
                    : new List<PartSuggestion>();

                int firstClassCount = metadataSuggestions.Count
                    + categorizerSuggestions.Count;
                int partBudget = MaxSuggestions - firstClassCount;
                if (partBudget < 6)
                {
                    partBudget = 6;
                }

                List<PartSuggestion> partSuggestions = _index
                    .Match(query, partBudget)
                    .Where(part => !IsRedundantPartSuggestion(part, metadataSuggestions, categorizerSuggestions))
                    .ToList();

                suggestions.AddRange(metadataSuggestions);
                suggestions.AddRange(categorizerSuggestions);
                suggestions.AddRange(partSuggestions);

                suggestions = SuggestionDedupHelper.Dedup(suggestions, query);

                suggestions = suggestions
                    .Where(entry => entry.IsValid())
                    .Select(entry =>
                    {
                        entry.RefreshVerifiedSubtitle();
                        return entry;
                    })
                    .OrderBy(entry => entry.RankScore)
                    .ThenBy(entry => SuggestionDedupHelper.GetKindPriority(entry))
                    .ThenBy(entry => entry.DisplayText, StringComparer.OrdinalIgnoreCase)
                    .Take(MaxSuggestions)
                    .ToList();
            }

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

                        EditorBootstrap.Log("ApplySuggestion (stock): '" + suggestion.QueryText + "'");
                        StockSearchHelper.ApplySearch(suggestion.QueryText);
                        _history.Remember(suggestion.QueryText);
                        _lastCommittedQuery = suggestion.QueryText;
                        break;

                    case SuggestionKind.ModAuthor:
                        EditorBootstrap.Log("ApplySuggestion (author): '" + displayText + "'");
                        StockSearchHelper.ApplyModAuthorFilter(suggestion.FilterKey ?? displayText, displayText);
                        _history.Remember(displayText);
                        _lastCommittedQuery = displayText;
                        break;

                    case SuggestionKind.ModName:
                        EditorBootstrap.Log("ApplySuggestion (mod): '" + displayText + "'");
                        StockSearchHelper.ApplyModNameFilter(suggestion.FilterKey ?? displayText, displayText);
                        _history.Remember(displayText);
                        _lastCommittedQuery = displayText;
                        break;

                    case SuggestionKind.ModSuite:
                        EditorBootstrap.Log("ApplySuggestion (suite): '" + displayText + "'");
                        StockSearchHelper.ApplyModSuiteFilter(suggestion.FilterKey ?? suggestion.QueryText ?? displayText, displayText);
                        _history.Remember(displayText);
                        _lastCommittedQuery = displayText;
                        break;

                    case SuggestionKind.FilterFunction:
                    case SuggestionKind.FilterManufacturer:
                    case SuggestionKind.FilterDiameter:
                    case SuggestionKind.FilterCategory:
                    case SuggestionKind.FilterModule:
                    case SuggestionKind.FilterResource:
                    case SuggestionKind.FilterTech:
                    case SuggestionKind.FilterTag:
                        EditorBootstrap.Log("ApplySuggestion (categorizer): '" + displayText + "' kind=" + suggestion.Kind);
                        StockSearchHelper.ApplyCategorizerFilter(suggestion);
                        _history.Remember(displayText);
                        _lastCommittedQuery = displayText;
                        break;

                    case SuggestionKind.Part:
                    default:
                        if (suggestion.Part == null)
                        {
                            EditorBootstrap.LogWarning("ApplySuggestion skipped — part suggestion invalid.");
                            return;
                        }

                        EditorBootstrap.Log(
                            "ApplySuggestion (precise): '"
                            + displayText
                            + "' id="
                            + suggestion.Part.name);
                        StockSearchHelper.ApplyPrecisePart(suggestion.Part, displayText);
                        _history.Remember(displayText);
                        _lastCommittedQuery = displayText;
                        break;
                }
            }
            catch (Exception ex)
            {
                EditorBootstrap.LogWarning("ApplySuggestion failed — " + ex.Message);
                StockSearchHelper.RecoverAfterFailedApply();
            }
            finally
            {
                if (_dropdown != null && _dropdown.IsDropdownOpen)
                {
                    _dropdown.Hide();
                }

                if (_searchField != null)
                {
                    _searchField.DeactivateInputField();
                }

                _applyingSuggestion = false;
            }
        }
    }
}
