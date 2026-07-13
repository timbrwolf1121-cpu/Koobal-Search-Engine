using HarmonyLib;
using KSP.UI.Screens;

namespace PartSearchSuggest
{
    /// <summary>
    /// Stock search race + typing halt guard.
    ///
    /// Typing halt (v0.8.1.2 restore, NRE-safe): by default block stock
    /// <see cref="BasePartCategorizer.SearchField_OnValueChange"/> and void
    /// <see cref="BasePartCategorizer.SearchStart"/> so the parts list does not rebuild
    /// on every keystroke (including backspace-to-empty). Opt in via
    /// <see cref="EnterAllowStockTextSearch"/> for explicit stock text search
    /// (<c>ApplySearch</c>) and deferred empty-field <c>SearchStop</c> after suggestion
    /// debounce settles.
    ///
    /// Organic apply race (v0.8.5.2): while Koobal is applying a suggestion filter
    /// (EnterSuppress) or a Koobal custom filter is active, also block stock SearchStart
    /// and stock SearchFilterResult from overwriting the applied result.
    ///
    /// IMPORTANT: never Harmony-skip <see cref="BasePartCategorizer"/> SearchRoutine.
    /// Prefix-skipping an IEnumerator method returns null; stock SearchStart then calls
    /// StartCoroutine(null) and throws ("routine is null"). Block void SearchStart instead.
    ///
    /// Typing clears the custom-filter guard via
    /// <see cref="StockSearchHelper.CancelPendingStockSearchForTyping"/>; typing halt
    /// still prevents SearchStart until Enter / suggestion apply / explicit ApplySearch.
    /// </summary>
    internal static class StockSearchGuard
    {
        private const string StockTextSearchFilterId = "SearchFilter_";

        private static int _suppressDepth;
        private static int _allowStockTextSearchDepth;
        private static string _activeCustomFilterId;

        internal static bool IsSuppressed => _suppressDepth > 0;

        internal static bool IsStockTextSearchAllowed => _allowStockTextSearchDepth > 0;

        internal static bool HasActiveCustomFilter =>
            !string.IsNullOrEmpty(_activeCustomFilterId);

        /// <summary>
        /// True when Koobal has not opted in to stock text search (dropdown / Enter-only mode).
        /// </summary>
        private static bool ShouldBlockStockTypingPipeline => !IsStockTextSearchAllowed;

        internal readonly struct SuppressScope : System.IDisposable
        {
            private readonly bool _active;

            internal SuppressScope(bool active)
            {
                _active = active;
                if (_active)
                {
                    EnterSuppress();
                }
            }

            public void Dispose()
            {
                if (_active)
                {
                    ExitSuppress();
                }
            }
        }

        internal static SuppressScope EnterSuppressScope()
        {
            return new SuppressScope(true);
        }

        internal static void EnterSuppress()
        {
            _suppressDepth++;
        }

        internal static void ExitSuppress()
        {
            if (_suppressDepth > 0)
            {
                _suppressDepth--;
            }
        }

        internal static void EnterAllowStockTextSearch()
        {
            _allowStockTextSearchDepth++;
        }

        internal static void ExitAllowStockTextSearch()
        {
            if (_allowStockTextSearchDepth > 0)
            {
                _allowStockTextSearchDepth--;
            }
        }

        internal static void SetActiveCustomFilter(string filterId)
        {
            _activeCustomFilterId = filterId;
        }

        internal static void ClearActiveCustomFilter()
        {
            _activeCustomFilterId = null;
        }

        internal static bool ShouldBlockStockTextFilter(EditorPartListFilter<AvailablePart> filter)
        {
            if (filter == null)
            {
                return false;
            }

            if (!string.Equals(filter.ID, StockTextSearchFilterId, System.StringComparison.Ordinal))
            {
                return false;
            }

            return IsSuppressed || HasActiveCustomFilter || ShouldBlockStockTypingPipeline;
        }

        internal static void ApplyPatches()
        {
            try
            {
                Harmony harmony = new Harmony("KoobalSearchEngine.StockSearchGuard");
                HarmonyPatchHelper.PatchNestedTypes(harmony, typeof(StockSearchGuard));
                EditorBootstrap.Log(
                    "Stock search guard patches applied (typing halt + organic apply race).");
            }
            catch (System.Exception ex)
            {
                EditorBootstrap.LogWarning(
                    "StockSearchGuard patch failed — stock search race guard disabled: " + ex.Message);
            }
        }

        /// <summary>
        /// Block stock value-change → SearchStart while typing, including empty text.
        /// Empty-field SearchStop is deferred by EditorSearchHook after suggestion debounce
        /// (via EnterAllowStockTextSearch) so clearing the box does not hitch on the key event.
        /// </summary>
        [HarmonyPatch(typeof(BasePartCategorizer), "SearchField_OnValueChange")]
        private static class SearchFieldOnValueChangePatch
        {
            private static bool Prefix()
            {
                if (IsSuppressed)
                {
                    EditorBootstrap.Log(
                        "Blocked stock SearchField_OnValueChange during custom filter apply.");
                    return false;
                }

                if (IsStockTextSearchAllowed)
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Skip stock SearchStart while typing (no allow), mid-apply, or a Koobal custom
        /// filter is active. Safe for a void method (unlike SearchRoutine).
        /// </summary>
        [HarmonyPatch(typeof(BasePartCategorizer), "SearchStart")]
        private static class BaseSearchStartPatch
        {
            private static bool Prefix()
            {
                if (IsSuppressed || HasActiveCustomFilter)
                {
                    EditorBootstrap.Log(
                        "Blocked stock SearchStart while custom filter is active"
                        + (IsSuppressed ? " (apply suppress)." : "."));
                    return false;
                }

                if (ShouldBlockStockTypingPipeline)
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(PartCategorizer), "SearchFilterResult")]
        private static class SearchFilterResultPatch
        {
            private static bool Prefix(EditorPartListFilter<AvailablePart> filter)
            {
                if (ShouldBlockStockTextFilter(filter))
                {
                    EditorBootstrap.Log(
                        "Blocked stock SearchFilter_ overwrite (active custom filter='"
                        + (_activeCustomFilterId ?? string.Empty)
                        + "').");
                    return false;
                }

                if (filter != null && filter.ID != null && filter.ID.StartsWith("KoobalSearchEngine_", System.StringComparison.Ordinal))
                {
                    SetActiveCustomFilter(filter.ID);
                }
                else if (filter == null)
                {
                    ClearActiveCustomFilter();
                }

                return true;
            }
        }
    }
}
