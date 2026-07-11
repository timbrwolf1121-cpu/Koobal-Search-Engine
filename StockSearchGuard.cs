using HarmonyLib;
using KSP.UI.Screens;

namespace PartSearchSuggest
{
    /// <summary>
    /// Minimal race guard (v0.6.7-era): while Koobal is applying a suggestion filter (EnterSuppress)
    /// or a Koobal custom filter is active, block stock SearchStart and stock SearchFilterResult
    /// from overwriting the applied result. Koobal does NOT block stock typing, refresh, tab, or
    /// subassembly flows — those run 100% natively.
    ///
    /// IMPORTANT: never Harmony-skip <see cref="BasePartCategorizer"/> SearchRoutine.
    /// Prefix-skipping an IEnumerator method returns null; stock SearchStart then calls
    /// StartCoroutine(null) and throws ("routine is null"). Block void SearchStart instead
    /// while suppressed or a Koobal custom filter is active.
    ///
    /// Typing clears the custom-filter guard via
    /// <see cref="StockSearchHelper.CancelPendingStockSearchForTyping"/> before stock can
    /// SearchStart — so the NRE fix for post-apply typing stays intact without letting blur
    /// after ApplyPrecisePart / categorizer apply re-run loose stock PartMatchesSearch.
    /// </summary>
    internal static class StockSearchGuard
    {
        private const string StockTextSearchFilterId = "SearchFilter_";

        private static int _suppressDepth;
        private static string _activeCustomFilterId;

        internal static bool IsSuppressed => _suppressDepth > 0;

        internal static bool HasActiveCustomFilter =>
            !string.IsNullOrEmpty(_activeCustomFilterId);

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

            return IsSuppressed || HasActiveCustomFilter;
        }

        internal static void ApplyPatches()
        {
            try
            {
                Harmony harmony = new Harmony("KoobalSearchEngine.StockSearchGuard");
                HarmonyPatchHelper.PatchNestedTypes(harmony, typeof(StockSearchGuard));
                EditorBootstrap.Log("Stock search guard patches applied (minimal race guard).");
            }
            catch (System.Exception ex)
            {
                EditorBootstrap.LogWarning(
                    "StockSearchGuard patch failed — stock search race guard disabled: " + ex.Message);
            }
        }

        /// <summary>
        /// Skip stock SearchStart while mid-apply or a Koobal custom filter is active.
        /// Safe for a void method (unlike SearchRoutine). Typing clears the custom-filter
        /// guard first so subsequent SearchStart/SearchRoutine still get a real IEnumerator.
        ///
        /// Regression (v0.8.5.2 / keep in v0.8.6.0+): MUST keep HasActiveCustomFilter here.
        /// Clearing the active custom filter on SearchStart (blur after ApplyPrecisePart /
        /// categorizer apply) lets loose stock PartMatchesSearch overwrite the organic
        /// filter and flood unrelated parts. Do not "simplify" this Prefix to IsSuppressed-only.
        /// Typing still clears via StockSearchHelper.CancelPendingStockSearchForTyping.
        /// Never skip SearchRoutine (IEnumerator → null → StartCoroutine NRE).
        /// </summary>
        [HarmonyPatch(typeof(BasePartCategorizer), "SearchStart")]
        private static class BaseSearchStartPatch
        {
            private static bool Prefix()
            {
                // Organic apply rock-solid: block while suppressed OR custom filter active.
                if (IsSuppressed || HasActiveCustomFilter)
                {
                    EditorBootstrap.Log(
                        "Blocked stock SearchStart while custom filter is active"
                        + (IsSuppressed ? " (apply suppress)." : "."));
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
