using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using KSP.UI.Screens;

using TMPro;

using UnityEngine;



namespace PartSearchSuggest

{

    internal static class StockSearchHelper

    {

        private const string PreciseFilterId = "KoobalSearchEngine_Precise";
        private const string EnterQueryFilterId = "KoobalSearchEngine_EnterQuery";
        private const string ModAuthorFilterId = "KoobalSearchEngine_ModAuthor";
        private const string ModNameFilterId = "KoobalSearchEngine_ModName";
        private const string ModSuiteFilterId = "KoobalSearchEngine_ModSuite";
        private const string CategorizerFilterIdPrefix = "KoobalSearchEngine_Categorizer";

        private static bool _enterSearchInProgress;



        private static readonly MethodInfo SearchFieldOnValueChange = typeof(BasePartCategorizer).GetMethod(

            "SearchField_OnValueChange",

            BindingFlags.Instance | BindingFlags.NonPublic);



        private static readonly MethodInfo SearchStart = typeof(BasePartCategorizer).GetMethod(

            "SearchStart",

            BindingFlags.Instance | BindingFlags.NonPublic);



        private static readonly MethodInfo SearchStop = typeof(BasePartCategorizer).GetMethod(

            "SearchStop",

            BindingFlags.Instance | BindingFlags.NonPublic);



        private static readonly MethodInfo SearchFilterResult = typeof(BasePartCategorizer).GetMethod(

            "SearchFilterResult",

            BindingFlags.Instance | BindingFlags.NonPublic);



        private static readonly FieldInfo SearchTimerField = typeof(BasePartCategorizer).GetField(

            "searchTimer",

            BindingFlags.Instance | BindingFlags.NonPublic);



        private static readonly FieldInfo SearchingField = typeof(PartCategorizer).GetField(

            "searching",

            BindingFlags.Instance | BindingFlags.NonPublic);



        private static readonly FieldInfo RefreshRequestedField = typeof(PartCategorizer).GetField(

            "refreshRequested",

            BindingFlags.Instance | BindingFlags.NonPublic);



        private static readonly FieldInfo SearchRoutineField = typeof(BasePartCategorizer).GetField(

            "searchRoutine",

            BindingFlags.Instance | BindingFlags.NonPublic);



        private static readonly MethodInfo SetTextWithoutNotify = typeof(TMP_InputField).GetMethod(

            "SetTextWithoutNotify",

            BindingFlags.Instance | BindingFlags.Public);



        public static void ApplySearch(string query)

        {

            PartCategorizer categorizer = PartCategorizer.Instance;

            if (categorizer == null || categorizer.searchField == null || string.IsNullOrEmpty(query))

            {

                EditorBootstrap.LogWarning("ApplySearch skipped — PartCategorizer or query invalid.");

                return;

            }



            StopActiveSearch(categorizer);

            StockSearchGuard.ClearActiveCustomFilter();

            // Opt in so typing-halt Harmony prefixes allow stock SearchField_OnValueChange / SearchStart.
            StockSearchGuard.EnterAllowStockTextSearch();
            try
            {
                // Clear first — stock SearchField_OnValueChange can append to existing text.
                SetSearchFieldText(categorizer.searchField, string.Empty);

                if (SearchFieldOnValueChange != null)
                {
                    SearchFieldOnValueChange.Invoke(categorizer, new object[] { query });
                }
                else
                {
                    categorizer.searchField.onValueChanged.Invoke(query);
                }

                SetSearchFieldText(categorizer.searchField, query);

                if (SearchTimerField != null)
                {
                    SearchTimerField.SetValue(categorizer, 0f);
                }

                SearchStart?.Invoke(categorizer, null);
                EditorBootstrap.Log("ApplySearch: stock filter triggered for '" + query + "'.");
            }
            finally
            {
                StockSearchGuard.ExitAllowStockTextSearch();
            }

        }



        /// <summary>

        /// Enter-key search: metadata-first when strong, otherwise Koobal part query filter

        /// (tighter than stock PartMatchesSearch). Stock text search is last-resort fallback only.

        /// </summary>

        public static void ApplyEnterSearch(

            string query,

            SuggestionIndex partIndex,

            MetadataSuggestionIndex metadataIndex,

            CategorizerSuggestionIndex categorizerIndex,

            bool metadataIndexReady,

            bool categorizerIndexReady)

        {
            if (_enterSearchInProgress)
            {
                EditorBootstrap.LogWarning("ApplyEnterSearch skipped — reentrancy blocked.");
                return;
            }

            string trimmed = (query ?? string.Empty).Trim();

            if (trimmed.Length < SuggestionQueryGuards.MinSuggestionQueryLength)

            {

                EditorBootstrap.Log("ApplyEnterSearch skipped — query too short.");

                return;

            }



            PartCategorizer categorizer = PartCategorizer.Instance;

            if (categorizer == null || categorizer.searchField == null)

            {

                EditorBootstrap.LogWarning("ApplyEnterSearch skipped — PartCategorizer unavailable.");

                return;

            }

            _enterSearchInProgress = true;
            // Suppress stock OnValueChange/SearchStart for the whole apply so empty-field
            // SearchStop and mid-apply text writes cannot re-enter the typing/search pipeline
            // while a Koobal custom filter is being installed (history click hang path).
            // Force-allow partsEditor Transition("In") so SearchStop/Refresh cannot deadlock
            // with PartsPanelTransitionGuard swallowing In while State stays Out.
            using (PartsPanelTransitionGuard.EnterForceAllowInScope())
            using (StockSearchGuard.EnterSuppressScope())
            {
                try
                {
                    // Prefer cancelling the stock SearchRoutine only — avoid SearchStop's
                    // SearchFilterResult(null)+refreshRequested dance while the parts panel
                    // may still be settling after dropdown Hide/Restore.
                    CancelSearchRoutine(categorizer);
                    StockSearchGuard.ClearActiveCustomFilter();

                    if (TryApplyEnterNonPartSuggestion(
                            trimmed,
                            metadataIndex,
                            categorizerIndex,
                            metadataIndexReady,
                            categorizerIndexReady))
                    {
                        SetSearchFieldText(categorizer.searchField, trimmed);
                        return;
                    }

                    if (partIndex != null && ApplyEnterPartQueryFilter(trimmed, partIndex))
                    {
                        return;
                    }

                    EditorBootstrap.Log(
                        "ApplyEnterSearch: no Koobal matches for '"
                        + trimmed
                        + "' — parts list unchanged (stock fallback disabled).");
                    SetSearchFieldText(categorizer.searchField, trimmed);
                }
                finally
                {
                    _enterSearchInProgress = false;
                }
            }
        }



        private static bool TryApplyEnterNonPartSuggestion(

            string query,

            MetadataSuggestionIndex metadataIndex,

            CategorizerSuggestionIndex categorizerIndex,

            bool metadataIndexReady,

            bool categorizerIndexReady)

        {

            const int maxNonPartRank = 50;

            PartSuggestion best = null;



            if (metadataIndexReady && metadataIndex != null)

            {

                best = PickBetterNonPartSuggestion(best, metadataIndex.Match(query, 1).FirstOrDefault());

            }



            if (categorizerIndexReady && categorizerIndex != null)

            {

                best = PickBetterNonPartSuggestion(best, categorizerIndex.Match(query, 1).FirstOrDefault());

            }



            if (best == null || best.Kind == SuggestionKind.Part || best.RankScore > maxNonPartRank)

            {

                return false;

            }



            if (!best.IsValid())

            {

                return false;

            }



            // Content facets (resource/tag/module/tech) are subsets of inclusive part matching.

            // Applying them alone on Enter drops title/name/tag/resource co-hits. Prefer the

            // inclusive Enter part filter for those; keep metadata-first only for categorical

            // navigation (mod/author/suite/function/category/manufacturer/diameter).

            if (IsInclusiveContentFacetKind(best.Kind))

            {

                EditorBootstrap.Log(

                    "ApplyEnterSearch: skipping metadata-first facet kind="

                    + best.Kind

                    + " display='"

                    + (best.DisplayText ?? string.Empty)

                    + "' — using inclusive part filter.");

                return false;

            }



            EditorBootstrap.Log(

                "ApplyEnterSearch (metadata-first): kind="

                + best.Kind

                + " display='"

                + (best.DisplayText ?? string.Empty)

                + "' rank="

                + best.RankScore

                + ".");



            ApplySuggestionFilter(best, query);

            return true;

        }



        private static PartSuggestion PickBetterNonPartSuggestion(PartSuggestion current, PartSuggestion candidate)

        {

            if (candidate == null || !candidate.IsValid() || candidate.Kind == SuggestionKind.Part)

            {

                return current;

            }



            if (current == null)

            {

                return candidate;

            }



            // Prefer categorical structured filters over synonym tags when ranks are close.

            if (IsCategoricalEnterKind(candidate.Kind)

                && IsTagLikeEnterKind(current.Kind)

                && candidate.RankScore <= current.RankScore + 10)

            {

                return candidate;

            }



            if (IsCategoricalEnterKind(current.Kind)

                && IsTagLikeEnterKind(candidate.Kind)

                && current.RankScore <= candidate.RankScore + 10)

            {

                return current;

            }



            if (candidate.RankScore < current.RankScore)

            {

                return candidate;

            }



            return current;

        }



        /// <summary>

        /// Facet kinds whose single-predicate apply is narrower than inclusive Enter matching

        /// (title/name/tags/resources/modules/manufacturer/tech). Enter/history must not

        /// short-circuit to these alone.

        /// </summary>

        private static bool IsInclusiveContentFacetKind(SuggestionKind kind)

        {

            switch (kind)

            {

                case SuggestionKind.FilterResource:

                case SuggestionKind.FilterTag:

                case SuggestionKind.FilterModule:

                case SuggestionKind.FilterTech:

                    return true;

                default:

                    return false;

            }

        }



        private static bool IsCategoricalEnterKind(SuggestionKind kind)

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



        private static bool IsTagLikeEnterKind(SuggestionKind kind)

        {

            return kind == SuggestionKind.FilterTag || kind == SuggestionKind.FilterTech;

        }

        private static void ApplySuggestionFilter(PartSuggestion suggestion, string displayText)

        {

            string label = string.IsNullOrWhiteSpace(displayText)

                ? suggestion.DisplayText ?? suggestion.QueryText ?? string.Empty

                : displayText;



            switch (suggestion.Kind)

            {

                case SuggestionKind.ModAuthor:

                    ApplyModAuthorFilter(suggestion.FilterKey ?? label, label);

                    break;



                case SuggestionKind.ModName:

                    ApplyModNameFilter(suggestion.FilterKey ?? label, label);

                    break;



                case SuggestionKind.ModSuite:

                    ApplyModSuiteFilter(suggestion.FilterKey ?? suggestion.QueryText ?? label, label);

                    break;



                case SuggestionKind.FilterFunction:

                case SuggestionKind.FilterManufacturer:

                case SuggestionKind.FilterDiameter:

                case SuggestionKind.FilterCategory:

                case SuggestionKind.FilterModule:

                case SuggestionKind.FilterResource:

                case SuggestionKind.FilterTech:

                case SuggestionKind.FilterTag:

                    ApplyCategorizerFilter(suggestion);

                    break;



                default:

                    EditorBootstrap.LogWarning(

                        "ApplyEnterSearch: unsupported non-part kind "

                        + suggestion.Kind

                        + " — skipped.");

                    break;

            }

        }



        private static bool ApplyEnterPartQueryFilter(string query, SuggestionIndex partIndex)

        {

            // Enter predicate: title/name/tag/module/resource significant fields

            // (whole-token / camelCase). Description and free-form HTML stay out.

            var matchNames = new HashSet<string>(StringComparer.Ordinal);

            int indexMatchCount = 0;



            foreach (AvailablePart part in partIndex.GetEnterQueryMatches(query))

            {

                if (part != null && !string.IsNullOrEmpty(part.name))

                {

                    if (matchNames.Add(part.name))

                    {

                        indexMatchCount++;

                    }

                }

            }



            // Union live facet predicates so Enter stays complete even if an indexed field

            // missed a structured resource/tag/module that PartFilterMatcher still sees.

            string trimmed = (query ?? string.Empty).Trim();

            int resourceExtra = 0;

            int tagExtra = 0;

            int moduleExtra = 0;



            if (trimmed.Length >= 3)

            {

                foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())

                {

                    if (part == null || string.IsNullOrEmpty(part.name) || matchNames.Contains(part.name))

                    {

                        continue;

                    }



                    if (PartFilterMatcher.PartMatchesFilter(SuggestionKind.FilterResource, trimmed, part))

                    {

                        matchNames.Add(part.name);

                        resourceExtra++;

                        continue;

                    }



                    if (PartFilterMatcher.PartMatchesFilter(SuggestionKind.FilterTag, trimmed, part))

                    {

                        matchNames.Add(part.name);

                        tagExtra++;

                        continue;

                    }



                    if (PartFilterMatcher.PartMatchesFilter(SuggestionKind.FilterModule, trimmed, part))

                    {

                        matchNames.Add(part.name);

                        moduleExtra++;

                    }

                }

            }



            if (matchNames.Count == 0)

            {

                EditorBootstrap.Log("ApplyEnterSearch: 0 Koobal part matches for '" + query + "'.");

                return false;

            }



            PartCategorizer categorizer = PartCategorizer.Instance;

            if (categorizer == null || categorizer.searchField == null)

            {

                return false;

            }



            if (matchNames.Count == 1)

            {

                AvailablePart single = null;

                foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())

                {

                    if (part != null && matchNames.Contains(part.name))

                    {

                        single = part;

                        break;

                    }

                }



                if (single != null)

                {

                    ApplyPrecisePart(single, query);

                    EditorBootstrap.Log("ApplyEnterSearch: single precise part match for '" + query + "'.");

                    return true;

                }

            }



            if (SearchFilterResult == null)

            {

                EditorBootstrap.LogWarning("ApplyEnterSearch: SearchFilterResult unavailable.");

                return false;

            }



            SetSearchFieldText(categorizer.searchField, string.Empty);



            EditorPartListFilter<AvailablePart> filter = new EditorPartListFilter<AvailablePart>(

                EnterQueryFilterId,

                candidate => candidate != null && matchNames.Contains(candidate.name),

                string.Empty);



            ApplyCustomFilter(categorizer, filter, "ApplyEnterSearch");

            SetSearchFieldText(categorizer.searchField, query);



            EditorBootstrap.Log(

                "ApplyEnterSearch: inclusive filter matched "

                + matchNames.Count

                + " part(s) for '"

                + query

                + "' (index="

                + indexMatchCount

                + ", +resource="

                + resourceExtra

                + ", +tag="

                + tagExtra

                + ", +module="

                + moduleExtra

                + "; no description-only).");



            return true;

        }



        /// Stops in-flight stock search while the user types — Koobal dropdown only.
        /// Stock SearchStart is also Harmony-blocked unless EnterAllowStockTextSearch
        /// (see StockSearchGuard typing halt). Clearing the custom-filter guard here keeps
        /// post-apply typing from staying stuck, without letting blur wipe an active apply.
        /// </summary>
        internal static void CancelPendingStockSearchForTyping(string reason)
        {
            PartCategorizer categorizer = PartCategorizer.Instance;
            if (categorizer == null)
            {
                return;
            }

            // Release custom-filter race guard on focus/typing. Do NOT clear on SearchStart
            // itself after apply — that lets loose PartMatchesSearch overwrite ApplyPrecisePart.
            if (!StockSearchGuard.IsSuppressed)
            {
                StockSearchGuard.ClearActiveCustomFilter();
            }

            CancelSearchRoutine(categorizer);
            PartCategorizerSearchFields.ResetTypingSearchFlags(categorizer);
        }

        /// <summary>
        /// After suggestion debounce settles on an empty field, restore the unfiltered parts
        /// list via stock SearchStop. Must not run on the delete key event itself — typing
        /// halt blocks empty <c>SearchField_OnValueChange</c> until this opt-in path.
        /// </summary>
        internal static void RestoreUnfilteredListAfterFieldCleared()
        {
            PartCategorizer categorizer = PartCategorizer.Instance;
            if (categorizer == null || categorizer.searchField == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(categorizer.searchField.text))
            {
                return;
            }

            StockSearchGuard.EnterAllowStockTextSearch();
            try
            {
                if (SearchFieldOnValueChange != null)
                {
                    SearchFieldOnValueChange.Invoke(categorizer, new object[] { string.Empty });
                }
                else
                {
                    SearchStop?.Invoke(categorizer, null);
                }

                EditorBootstrap.Log("RestoreUnfilteredListAfterFieldCleared: stock SearchStop allowed.");
            }
            catch (Exception ex)
            {
                EditorBootstrap.LogWarning(
                    "RestoreUnfilteredListAfterFieldCleared failed: " + ex.Message);
            }
            finally
            {
                StockSearchGuard.ExitAllowStockTextSearch();
            }
        }

        /// <summary>
        /// After a failed apply, restore PartCategorizer search flags so subsequent typing
        /// does not hit StartCoroutine(null) from a half-applied guard state.
        /// </summary>
        internal static void RecoverAfterFailedApply()
        {
            StockSearchGuard.ClearActiveCustomFilter();
            PartCategorizer categorizer = PartCategorizer.Instance;
            if (categorizer == null)
            {
                return;
            }

            CancelSearchRoutine(categorizer);
            PartCategorizerSearchFields.ResetTypingSearchFlags(categorizer);
        }



        public static void ApplyPrecisePart(AvailablePart part, string displayText)

        {

            PartCategorizer categorizer = PartCategorizer.Instance;

            if (categorizer == null || categorizer.searchField == null || part == null)

            {

                EditorBootstrap.LogWarning("ApplyPrecisePart skipped — PartCategorizer or part invalid.");

                return;

            }



            string partId = part.name ?? string.Empty;

            if (partId.Length == 0)

            {

                EditorBootstrap.LogWarning("ApplyPrecisePart skipped — part has no internal name.");

                return;

            }



            if (SearchFilterResult == null)

            {

                EditorBootstrap.LogWarning("ApplyPrecisePart: SearchFilterResult unavailable — falling back to name search.");

                ApplySearch(partId);

                return;

            }



            StopActiveSearch(categorizer);



            string queryText = string.IsNullOrWhiteSpace(displayText) ? GetDisplayText(part) : displayText.Trim();

            SetSearchFieldText(categorizer.searchField, string.Empty);



            string capturedPartId = partId;

            EditorPartListFilter<AvailablePart> filter = new EditorPartListFilter<AvailablePart>(

                PreciseFilterId,

                candidate => candidate != null && string.Equals(candidate.name, capturedPartId, StringComparison.Ordinal),

                string.Empty);



            ApplyCustomFilter(categorizer, filter, "ApplyPrecisePart");

            SetSearchFieldText(categorizer.searchField, queryText);



            EditorBootstrap.Log(

                "ApplyPrecisePart: exact filter for id='"

                + partId

                + "', display='"

                + queryText

                + "'.");

        }



        public static void ApplyModAuthorFilter(string author, string displayText)

        {

            PartCategorizer categorizer = PartCategorizer.Instance;

            if (categorizer == null || categorizer.searchField == null || string.IsNullOrWhiteSpace(author))

            {

                EditorBootstrap.LogWarning("ApplyModAuthorFilter skipped — PartCategorizer or author invalid.");

                return;

            }



            if (SearchFilterResult == null)

            {

                EditorBootstrap.LogWarning("ApplyModAuthorFilter: SearchFilterResult unavailable — falling back to text search.");

                ApplySearch(displayText ?? author);

                return;

            }



            StopActiveSearch(categorizer);



            string queryText = string.IsNullOrWhiteSpace(displayText) ? author.Trim() : displayText.Trim();

            SetSearchFieldText(categorizer.searchField, string.Empty);

            string capturedAuthor = author.Trim();

            int predicateMatches = 0;

            EditorPartListFilter<AvailablePart> filter = new EditorPartListFilter<AvailablePart>(

                ModAuthorFilterId,

                candidate =>

                {

                    if (!AuthorAttribution.PartMatchesAuthorFilter(candidate, capturedAuthor))

                    {

                        return false;

                    }



                    predicateMatches++;

                    return true;

                },

                string.Empty);



            EditorBootstrap.Log(

                "ApplyModAuthorFilter: begin author='"

                + capturedAuthor

                + "', searchField='"

                + (categorizer.searchField.text ?? string.Empty)

                + "'.");

            ApplyCustomFilter(categorizer, filter, "ApplyModAuthorFilter");

            SetSearchFieldText(categorizer.searchField, queryText);



            int expectedMatches = AuthorAttribution.CountPartsMatchingAuthorFilter(capturedAuthor);

            EditorBootstrap.Log(

                "ApplyModAuthorFilter: author='"

                + capturedAuthor

                + "' expected="

                + expectedMatches

                + " predicateInvokedMatches="

                + predicateMatches

                + ", display='"

                + queryText

                + "', searchFieldAfter='"

                + (categorizer.searchField.text ?? string.Empty)

                + "'.");

        }



        public static void ApplyModNameFilter(string modFolder, string displayText)

        {

            PartCategorizer categorizer = PartCategorizer.Instance;

            if (categorizer == null || categorizer.searchField == null || string.IsNullOrWhiteSpace(modFolder))

            {

                EditorBootstrap.LogWarning("ApplyModNameFilter skipped — PartCategorizer or mod folder invalid.");

                return;

            }



            if (SearchFilterResult == null)

            {

                EditorBootstrap.LogWarning("ApplyModNameFilter: SearchFilterResult unavailable — falling back to text search.");

                ApplySearch(displayText ?? modFolder);

                return;

            }



            StopActiveSearch(categorizer);



            string queryText = string.IsNullOrWhiteSpace(displayText) ? modFolder.Trim() : displayText.Trim();

            SetSearchFieldText(categorizer.searchField, string.Empty);

            string capturedFolder = modFolder.Trim();

            EditorPartListFilter<AvailablePart> filter = new EditorPartListFilter<AvailablePart>(

                ModNameFilterId,

                candidate => ModFilterMatcher.PartMatchesModFolder(candidate, capturedFolder),

                string.Empty);



            ApplyCustomFilter(categorizer, filter, "ApplyModNameFilter");

            SetSearchFieldText(categorizer.searchField, queryText);



            EditorBootstrap.Log(

                "ApplyModNameFilter: folder='"

                + capturedFolder

                + "', display='"

                + queryText

                + "'.");

        }



        public static void ApplyModSuiteFilter(string query, string displayText)

        {

            PartCategorizer categorizer = PartCategorizer.Instance;

            if (categorizer == null || categorizer.searchField == null || string.IsNullOrWhiteSpace(query))

            {

                EditorBootstrap.LogWarning("ApplyModSuiteFilter skipped — PartCategorizer or query invalid.");

                return;

            }



            if (SearchFilterResult == null)

            {

                EditorBootstrap.LogWarning("ApplyModSuiteFilter: SearchFilterResult unavailable — falling back to text search.");

                ApplySearch(displayText ?? query);

                return;

            }



            StopActiveSearch(categorizer);



            string queryText = string.IsNullOrWhiteSpace(displayText) ? query.Trim() : displayText.Trim();

            SetSearchFieldText(categorizer.searchField, string.Empty);

            string[] words = query.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            EditorPartListFilter<AvailablePart> filter = new EditorPartListFilter<AvailablePart>(

                ModSuiteFilterId,

                candidate => ModFilterMatcher.PartMatchesModSuiteWords(candidate, words),

                string.Empty);



            ApplyCustomFilter(categorizer, filter, "ApplyModSuiteFilter");

            SetSearchFieldText(categorizer.searchField, queryText);



            EditorBootstrap.Log(

                "ApplyModSuiteFilter: query='"

                + query.Trim()

                + "', display='"

                + queryText

                + "'.");

        }



        public static void ApplyCategorizerFilter(PartSuggestion suggestion)

        {

            PartCategorizer categorizer = PartCategorizer.Instance;

            if (categorizer == null || categorizer.searchField == null || suggestion == null)

            {

                EditorBootstrap.LogWarning("ApplyCategorizerFilter skipped — PartCategorizer or suggestion invalid.");

                return;

            }



            if (string.IsNullOrWhiteSpace(suggestion.FilterKey))

            {

                EditorBootstrap.LogWarning("ApplyCategorizerFilter skipped — filter key invalid.");

                return;

            }



            if (SearchFilterResult == null)

            {

                EditorBootstrap.LogWarning("ApplyCategorizerFilter: SearchFilterResult unavailable — falling back to text search.");

                ApplySearch(suggestion.DisplayText ?? suggestion.FilterKey);

                return;

            }



            Func<AvailablePart, bool> predicate = PartFilterMatcher.ResolvePredicate(

                suggestion.Kind,

                suggestion.FilterKey);

            if (predicate == null)

            {

                EditorBootstrap.LogWarning(

                    "ApplyCategorizerFilter skipped — no predicate for kind="

                    + suggestion.Kind

                    + " key='"

                    + suggestion.FilterKey

                    + "' path="

                    + SuggestionFilterRegistry.DescribeApplyPath(suggestion.Kind, suggestion.FilterKey)

                    + ".");

                return;

            }



            int expectedMatches = PartFilterMatcher.CountMatchingFilter(

                suggestion.Kind,

                suggestion.FilterKey);

            if (expectedMatches <= 0)

            {

                EditorBootstrap.LogWarning(

                    "ApplyCategorizerFilter skipped — predicate matched 0 parts for kind="

                    + suggestion.Kind

                    + " key='"

                    + suggestion.FilterKey

                    + "' display='"

                    + (suggestion.DisplayText ?? string.Empty)

                    + "'.");

                return;

            }



            StopActiveSearch(categorizer);



            string queryText = string.IsNullOrWhiteSpace(suggestion.DisplayText)

                ? suggestion.FilterKey.Trim()

                : suggestion.DisplayText.Trim();

            SetSearchFieldText(categorizer.searchField, string.Empty);



            string capturedKind = suggestion.Kind.ToString();

            string capturedKey = suggestion.FilterKey.Trim();

            Func<AvailablePart, bool> capturedPredicate = predicate;

            string filterId = BuildCategorizerFilterId(capturedKind, capturedKey);



            EditorPartListFilter<AvailablePart> filter = new EditorPartListFilter<AvailablePart>(

                filterId,

                candidate => candidate != null && capturedPredicate(candidate),

                string.Empty);



            ApplyCustomFilter(categorizer, filter, "ApplyCategorizerFilter");

            SetSearchFieldText(categorizer.searchField, queryText);



            EditorBootstrap.Log(

                "ApplyCategorizerFilter: kind="

                + capturedKind

                + " key='"

                + capturedKey

                + "', display='"

                + queryText

                + "', matched="

                + expectedMatches

                + " parts, path="

                + SuggestionFilterRegistry.DescribeApplyPath(suggestion.Kind, capturedKey)

                + ".");

        }



        private static string BuildCategorizerFilterId(string kind, string filterKey)

        {

            string safeKey = filterKey ?? string.Empty;

            var builder = new System.Text.StringBuilder(safeKey.Length);

            for (int i = 0; i < safeKey.Length; i++)

            {

                char c = safeKey[i];

                builder.Append(char.IsLetterOrDigit(c) ? c : '_');

            }



            return CategorizerFilterIdPrefix + "_" + kind + "_" + builder;

        }



        private static void ApplyCustomFilter(

            PartCategorizer categorizer,

            EditorPartListFilter<AvailablePart> filter,

            string logContext)

        {

            string fieldBefore = categorizer.searchField != null ? categorizer.searchField.text ?? string.Empty : string.Empty;



            if (SearchFilterResult == null)

            {

                EditorBootstrap.LogWarning(logContext + ": SearchFilterResult unavailable.");

                return;

            }



            StockSearchGuard.EnterSuppress();

            try

            {
                using (PartsPanelTransitionGuard.EnterForceAllowInScope())
                {
                    CancelSearchRoutine(categorizer);

                    // Do not call SearchStop here — SearchFilterResult(null) + refreshRequested
                    // races parts-panel restore / Transition guard and was a history-click hang
                    // contributor. Overwrite the active filter directly instead.

                    SearchFilterResult.Invoke(categorizer, new object[] { filter });

                    StockSearchGuard.SetActiveCustomFilter(filter.ID);

                    EditorBootstrap.Log(logContext + ": SearchFilterResult invoked, filterId='" + filter.ID + "'.");



                    if (SearchingField != null)

                    {

                        SearchingField.SetValue(categorizer, true);

                    }



                    if (RefreshRequestedField != null)

                    {

                        RefreshRequestedField.SetValue(categorizer, true);

                    }



                    if (categorizer.editorPartList != null)

                    {

                        categorizer.editorPartList.Refresh(EditorPartList.State.PartSearch);

                        string fieldAfter = categorizer.searchField != null ? categorizer.searchField.text ?? string.Empty : string.Empty;

                        EditorBootstrap.Log(

                            logContext

                            + ": Refresh(PartSearch) complete, searchField before='"

                            + fieldBefore

                            + "' after='"

                            + fieldAfter

                            + "'.");

                    }

                    else

                    {

                        EditorBootstrap.LogWarning(logContext + ": editorPartList null — parts list not refreshed.");

                    }
                }
            }

            finally

            {

                StockSearchGuard.ExitSuppress();

            }

        }



        private static void SetSearchFieldText(TMP_InputField field, string text)

        {

            if (field == null)

            {

                return;

            }



            if (SetTextWithoutNotify != null)

            {

                SetTextWithoutNotify.Invoke(field, new object[] { text ?? string.Empty });

                return;

            }



            field.text = text ?? string.Empty;

        }



        private static void CancelSearchRoutine(PartCategorizer categorizer)

        {

            if (categorizer == null || SearchRoutineField == null)

            {

                return;

            }



            Coroutine routine = SearchRoutineField.GetValue(categorizer) as Coroutine;

            if (routine == null)

            {

                return;

            }



            categorizer.StopCoroutine(routine);

            SearchRoutineField.SetValue(categorizer, null);

            EditorBootstrap.Log("CancelSearchRoutine: stopped pending stock SearchRoutine coroutine.");

        }



        private static void StopActiveSearch(PartCategorizer categorizer)

        {

            CancelSearchRoutine(categorizer);

            SearchStop?.Invoke(categorizer, null);



            if (SearchingField != null)

            {

                SearchingField.SetValue(categorizer, false);

            }



            if (SearchTimerField != null)

            {

                SearchTimerField.SetValue(categorizer, 0f);

            }

        }



        private static string GetDisplayText(AvailablePart part)

        {

            string title = part.title;

            if (!string.IsNullOrWhiteSpace(title))

            {

                return title.Trim().Trim('"');

            }



            return part.name ?? string.Empty;

        }

    }

}

