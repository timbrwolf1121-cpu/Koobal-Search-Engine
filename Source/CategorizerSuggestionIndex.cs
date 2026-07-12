using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PartSearchSuggest
{
    internal sealed class CategorizerSuggestionIndex
    {
        internal const int FrameSliceBatchSize = 100;

        private const int FunctionPrefixRank = 0;
        private const int CategoryPrefixRank = 1;
        private const int ManufacturerPrefixRank = 2;
        private const int DiameterExactRank = 3;
        private const int ModulePrefixRank = 4;
        private const int TagPrefixRank = 4;
        private const int ResourcePrefixRank = 5;
        private const int TechPrefixRank = 6;
        private const int ContainsRankOffset = 8;
        private const int MinTagPartCount = 3;

        private readonly List<CategorizerEntry> _entries = new List<CategorizerEntry>();

        public int EntryCount => _entries.Count;

        public void Build()
        {
            _entries.Clear();

            if (PartLoader.Instance == null || PartLoader.Instance.loadedParts == null)
            {
                EditorBootstrap.LogWarning("PartLoader not ready — categorizer suggestion index empty.");
                return;
            }

            AddFunctionEntries();
            AddCategoryEntries();
            AddManufacturerEntries();
            AddDiameterEntries();
            AddModuleEntries();
            AddTagEntries();
            AddResourceEntries();
            AddTechEntries();

            EditorBootstrap.Log("Indexed " + _entries.Count + " stock categorizer filter suggestions.");
        }

        public IEnumerator BuildCoroutine()
        {
            _entries.Clear();

            if (PartLoader.Instance == null || PartLoader.Instance.loadedParts == null)
            {
                EditorBootstrap.LogWarning("PartLoader not ready — categorizer suggestion index empty.");
                yield break;
            }

            AddFunctionEntries();
            yield return null;

            yield return AddCategoryEntriesCoroutine();
            yield return AddManufacturerEntriesCoroutine();
            yield return AddDiameterEntriesCoroutine();
            yield return AddModuleEntriesCoroutine();
            yield return AddTagEntriesCoroutine();
            yield return AddResourceEntriesCoroutine();
            yield return AddTechEntriesCoroutine();

            EditorBootstrap.Log("Indexed " + _entries.Count + " stock categorizer filter suggestions.");
        }

        public IEnumerable<PartSuggestion> Match(string query, int maxResults)
        {
            string trimmed = (query ?? string.Empty).Trim();
            if (trimmed.Length == 0 || maxResults <= 0)
            {
                yield break;
            }

            bool allowBroadFilters = !SuggestionQueryGuards.IsTooShortForBroadSuggestions(trimmed);

            string[] words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                yield break;
            }

            var results = new List<PartSuggestion>();
            for (int i = 0; i < _entries.Count; i++)
            {
                CategorizerEntry entry = _entries[i];
                if (!allowBroadFilters && IsBroadCategorizerKind(entry.Kind))
                {
                    continue;
                }

                if (SuggestionQueryGuards.IsSingleCharacter(entry.FilterKey))
                {
                    continue;
                }

                int rank = ScoreEntry(entry, words);
                if (rank < 0)
                {
                    continue;
                }

                if (SuggestionQueryGuards.ShouldSuppressBroadSuggestion(trimmed, entry.FilterKey, entry.PartCount))
                {
                    continue;
                }

                results.Add(new PartSuggestion
                {
                    Kind = entry.Kind,
                    QueryText = entry.DisplayText,
                    DisplayText = entry.DisplayText,
                    FilterKey = entry.FilterKey,
                    MatchReason = entry.KindLabel + " · " + entry.PartCount + " parts",
                    RankScore = rank,
                    Part = null,
                    IsHistory = false
                });
            }

            List<PartSuggestion> deduped = SuggestionDedupHelper.Dedup(results, trimmed);

            int count = 0;
            foreach (PartSuggestion suggestion in deduped
                .Where(entry => entry.IsValid())
                .OrderBy(entry => entry.RankScore)
                .ThenBy(entry => entry.DisplayText, StringComparer.OrdinalIgnoreCase))
            {
                yield return suggestion;
                count++;
                if (count >= maxResults)
                {
                    yield break;
                }
            }

            LogIntakeQueryMatches(trimmed, deduped, count);
        }

        /// <summary>
        /// Top stock categorizer filters for empty-query browse (no history yet).
        /// </summary>
        public IEnumerable<PartSuggestion> GetBrowseSuggestions(int maxResults)
        {
            if (maxResults <= 0 || _entries.Count == 0)
            {
                yield break;
            }

            int count = 0;
            foreach (CategorizerEntry entry in _entries
                .Where(candidate => candidate.PartCount > 0)
                .OrderByDescending(candidate => candidate.PartCount)
                .ThenBy(candidate => candidate.DisplayText, StringComparer.OrdinalIgnoreCase))
            {
                yield return new PartSuggestion
                {
                    Kind = entry.Kind,
                    QueryText = entry.DisplayText,
                    DisplayText = entry.DisplayText,
                    FilterKey = entry.FilterKey,
                    MatchReason = entry.KindLabel + " · " + entry.PartCount + " parts",
                    RankScore = count,
                    Part = null,
                    IsHistory = false
                };

                count++;
                if (count >= maxResults)
                {
                    yield break;
                }
            }
        }

        private void AddFunctionEntries()
        {
            foreach (SuggestionFilterRegistry.FunctionFilterDefinition definition in
                SuggestionFilterRegistry.GetFunctionFilters())
            {
                AddFunctionEntry(definition);
            }
        }

        private void AddFunctionEntry(SuggestionFilterRegistry.FunctionFilterDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.FilterKey))
            {
                return;
            }

            int partCount = PartFilterMatcher.CountMatchingFilter(
                SuggestionKind.FilterFunction,
                definition.FilterKey);
            if (partCount <= 0)
            {
                EditorBootstrap.Log(
                    "Skipped function suggestion '"
                    + definition.DisplayText
                    + "' — predicate matched 0 parts (key='"
                    + definition.FilterKey
                    + "').");
                return;
            }

            _entries.Add(new CategorizerEntry
            {
                Kind = SuggestionKind.FilterFunction,
                FilterKey = definition.FilterKey,
                DisplayText = definition.DisplayText,
                KindLabel = "function",
                PartCount = partCount,
                SearchTerms = BuildSearchTerms(definition.DisplayText, definition.SearchTerms)
            });
        }

        private void AddCategoryEntries()
        {
            var counts = new Dictionary<PartCategories, int>();
            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                if (part == null || part.category == PartCategories.none)
                {
                    continue;
                }

                if (counts.TryGetValue(part.category, out int existing))
                {
                    counts[part.category] = existing + 1;
                }
                else
                {
                    counts[part.category] = 1;
                }
            }

            AddCategoryEntriesFromCounts(counts);
        }

        private IEnumerator AddCategoryEntriesCoroutine()
        {
            var counts = new Dictionary<PartCategories, int>();
            IReadOnlyList<AvailablePart> availableParts = EditorPartAvailability.GetAvailableParts();
            for (int i = 0; i < availableParts.Count; i++)
            {
                AvailablePart part = availableParts[i];
                if (part == null || part.category == PartCategories.none)
                {
                    continue;
                }

                if (counts.TryGetValue(part.category, out int existing))
                {
                    counts[part.category] = existing + 1;
                }
                else
                {
                    counts[part.category] = 1;
                }

                if (i > 0 && i % FrameSliceBatchSize == 0)
                {
                    yield return null;
                }
            }

            AddCategoryEntriesFromCounts(counts);
            yield return null;
        }

        private void AddCategoryEntriesFromCounts(Dictionary<PartCategories, int> counts)
        {
            foreach (KeyValuePair<PartCategories, int> pair in counts.OrderBy(entry => entry.Key.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                if (pair.Value <= 0)
                {
                    continue;
                }

                string display = AuthorMatchHelper.FormatDisplayName(pair.Key.ToString());
                if (!TryAddVerifiedEntry(
                    SuggestionKind.FilterCategory,
                    pair.Key.ToString(),
                    display,
                    "category",
                    pair.Value,
                    BuildSearchTerms(display, SplitCamelCase(pair.Key.ToString())),
                    out CategorizerEntry entry))
                {
                    continue;
                }

                _entries.Add(entry);
            }
        }

        private void AddManufacturerEntries()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                if (part == null || string.IsNullOrWhiteSpace(part.manufacturer))
                {
                    continue;
                }

                string manufacturer = part.manufacturer.Trim();
                if (counts.TryGetValue(manufacturer, out int existing))
                {
                    counts[manufacturer] = existing + 1;
                }
                else
                {
                    counts[manufacturer] = 1;
                }
            }

            AddManufacturerEntriesFromCounts(counts);
        }

        private IEnumerator AddManufacturerEntriesCoroutine()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<AvailablePart> availableParts = EditorPartAvailability.GetAvailableParts();
            for (int i = 0; i < availableParts.Count; i++)
            {
                AvailablePart part = availableParts[i];
                if (part == null || string.IsNullOrWhiteSpace(part.manufacturer))
                {
                    continue;
                }

                string manufacturer = part.manufacturer.Trim();
                if (counts.TryGetValue(manufacturer, out int existing))
                {
                    counts[manufacturer] = existing + 1;
                }
                else
                {
                    counts[manufacturer] = 1;
                }

                if (i > 0 && i % FrameSliceBatchSize == 0)
                {
                    yield return null;
                }
            }

            AddManufacturerEntriesFromCounts(counts);
            yield return null;
        }

        private void AddManufacturerEntriesFromCounts(Dictionary<string, int> counts)
        {
            foreach (KeyValuePair<string, int> pair in counts.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (pair.Value <= 0)
                {
                    continue;
                }

                if (!TryAddVerifiedEntry(
                    SuggestionKind.FilterManufacturer,
                    pair.Key,
                    pair.Key,
                    "manufacturer",
                    pair.Value,
                    BuildSearchTerms(pair.Key, SplitCamelCase(pair.Key)),
                    out CategorizerEntry entry))
                {
                    continue;
                }

                _entries.Add(entry);
            }
        }

        private void AddDiameterEntries()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                if (part == null || string.IsNullOrWhiteSpace(part.bulkheadProfiles))
                {
                    continue;
                }

                AccumulateDiameterCounts(part, counts);
            }

            AddDiameterEntriesFromCounts(counts);
        }

        private IEnumerator AddDiameterEntriesCoroutine()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<AvailablePart> availableParts = EditorPartAvailability.GetAvailableParts();
            for (int i = 0; i < availableParts.Count; i++)
            {
                AvailablePart part = availableParts[i];
                if (part == null || string.IsNullOrWhiteSpace(part.bulkheadProfiles))
                {
                    continue;
                }

                AccumulateDiameterCounts(part, counts);

                if (i > 0 && i % FrameSliceBatchSize == 0)
                {
                    yield return null;
                }
            }

            AddDiameterEntriesFromCounts(counts);
            yield return null;
        }

        private static void AccumulateDiameterCounts(AvailablePart part, Dictionary<string, int> counts)
        {
            string[] tags = part.bulkheadProfiles.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tags.Length; i++)
            {
                string tag = tags[i].Trim();
                if (tag.Length == 0)
                {
                    continue;
                }

                if (counts.TryGetValue(tag, out int existing))
                {
                    counts[tag] = existing + 1;
                }
                else
                {
                    counts[tag] = 1;
                }
            }
        }

        private void AddDiameterEntriesFromCounts(Dictionary<string, int> counts)
        {
            foreach (KeyValuePair<string, int> pair in counts.OrderBy(entry => GetDiameterSortKey(entry.Key), StringComparer.OrdinalIgnoreCase))
            {
                if (pair.Value <= 0)
                {
                    continue;
                }

                string display = GetDiameterDisplayName(pair.Key);
                var diameterExtraTerms = new List<string> { pair.Key };
                diameterExtraTerms.AddRange(GetDiameterAliases(pair.Key));
                if (!TryAddVerifiedEntry(
                    SuggestionKind.FilterDiameter,
                    pair.Key,
                    display,
                    "diameter",
                    pair.Value,
                    BuildSearchTerms(display, diameterExtraTerms.ToArray()),
                    out CategorizerEntry entry))
                {
                    continue;
                }

                _entries.Add(entry);
            }
        }

        private void AddModuleEntries()
        {
            var counts = new Dictionary<string, ModuleEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                if (part?.moduleInfos == null)
                {
                    continue;
                }

                AccumulateModuleCounts(part, counts);
            }

            AddModuleEntriesFromCounts(counts);
        }

        private IEnumerator AddModuleEntriesCoroutine()
        {
            var counts = new Dictionary<string, ModuleEntry>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<AvailablePart> availableParts = EditorPartAvailability.GetAvailableParts();
            for (int i = 0; i < availableParts.Count; i++)
            {
                AvailablePart part = availableParts[i];
                if (part?.moduleInfos == null)
                {
                    continue;
                }

                AccumulateModuleCounts(part, counts);

                if (i > 0 && i % FrameSliceBatchSize == 0)
                {
                    yield return null;
                }
            }

            AddModuleEntriesFromCounts(counts);
            yield return null;
        }

        private static void AccumulateModuleCounts(AvailablePart part, Dictionary<string, ModuleEntry> counts)
        {
            for (int i = 0; i < part.moduleInfos.Count; i++)
            {
                AvailablePart.ModuleInfo moduleInfo = part.moduleInfos[i];
                if (moduleInfo == null)
                {
                    continue;
                }

                string key = !string.IsNullOrWhiteSpace(moduleInfo.moduleDisplayName)
                    ? moduleInfo.moduleDisplayName.Trim()
                    : moduleInfo.moduleName;

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!counts.TryGetValue(key, out ModuleEntry moduleEntry))
                {
                    moduleEntry = new ModuleEntry
                    {
                        FilterKey = PartFilterMatcher.ResolveModuleFilterKey(moduleInfo, key),
                        DisplayText = key
                    };
                    counts[key] = moduleEntry;
                }

                moduleEntry.PartCount++;
            }
        }

        private void AddModuleEntriesFromCounts(Dictionary<string, ModuleEntry> counts)
        {
            foreach (ModuleEntry moduleEntry in counts.Values
                .Where(entry => entry.PartCount >= 3)
                .OrderBy(entry => entry.DisplayText, StringComparer.OrdinalIgnoreCase))
            {
                if (!TryAddVerifiedEntry(
                    SuggestionKind.FilterModule,
                    moduleEntry.FilterKey,
                    moduleEntry.DisplayText,
                    "module",
                    moduleEntry.PartCount,
                    BuildSearchTerms(moduleEntry.DisplayText, SplitCamelCase(moduleEntry.DisplayText)),
                    out CategorizerEntry entry))
                {
                    continue;
                }

                _entries.Add(entry);
            }
        }

        /// <summary>
        /// Indexes cfg part tags (part.tags + prefab partInfo.tags) for organic filter suggestions.
        /// Query "intake" previously surfaced "Intakes" via SuggestionFilterRegistry filterIntake /
        /// FilterFunction + PartIsAirIntake (ModuleResourceIntake) — removed in v0.7.2.
        /// Intake rows now come from FilterTag on the literal tag token "intake" in part metadata.
        /// </summary>
        private void AddTagEntries()
        {
            var counts = new Dictionary<string, TagEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                CollectPartTagCounts(part, counts);
            }

            AddTagEntriesFromCounts(counts);
        }

        private IEnumerator AddTagEntriesCoroutine()
        {
            var counts = new Dictionary<string, TagEntry>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<AvailablePart> availableParts = EditorPartAvailability.GetAvailableParts();
            for (int i = 0; i < availableParts.Count; i++)
            {
                CollectPartTagCounts(availableParts[i], counts);

                if (i > 0 && i % FrameSliceBatchSize == 0)
                {
                    yield return null;
                }
            }

            AddTagEntriesFromCounts(counts);
            yield return null;
        }

        private void AddTagEntriesFromCounts(Dictionary<string, TagEntry> counts)
        {
            foreach (TagEntry tagEntry in counts.Values
                .Where(entry => entry.PartCount >= MinTagPartCount)
                .OrderBy(entry => entry.DisplayText, StringComparer.OrdinalIgnoreCase))
            {
                var extraTerms = new List<string> { tagEntry.FilterKey, SplitCamelCase(tagEntry.FilterKey) };
                if (!tagEntry.FilterKey.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                {
                    extraTerms.Add(tagEntry.FilterKey + "s");
                }

                if (!TryAddVerifiedEntry(
                    SuggestionKind.FilterTag,
                    tagEntry.FilterKey,
                    tagEntry.DisplayText,
                    "tag",
                    tagEntry.PartCount,
                    BuildSearchTerms(tagEntry.DisplayText, extraTerms.ToArray()),
                    out CategorizerEntry entry))
                {
                    continue;
                }

                _entries.Add(entry);
            }
        }

        private static void CollectPartTagCounts(AvailablePart part, Dictionary<string, TagEntry> counts)
        {
            var seenOnPart = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectTagsFromField(part.tags, seenOnPart, counts);
            if (part.partPrefab?.partInfo != null)
            {
                CollectTagsFromField(part.partPrefab.partInfo.tags, seenOnPart, counts);
            }
        }

        private static void CollectTagsFromField(
            string tagField,
            HashSet<string> seenOnPart,
            Dictionary<string, TagEntry> counts)
        {
            if (string.IsNullOrWhiteSpace(tagField))
            {
                return;
            }

            string[] tokens = tagField.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string tag = tokens[i].Trim();
                if (tag.Length < 3
                    || IsExcludedPartTag(tag)
                    || !SuggestionTokenQuality.IsSuggestionWorthyTag(tag)
                    || !seenOnPart.Add(tag))
                {
                    continue;
                }

                if (!counts.TryGetValue(tag, out TagEntry tagEntry))
                {
                    tagEntry = new TagEntry
                    {
                        FilterKey = tag,
                        DisplayText = GetTagDisplayName(tag)
                    };
                    counts[tag] = tagEntry;
                }

                tagEntry.PartCount++;
            }
        }

        private static bool IsExcludedPartTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return true;
            }

            string lower = tag.ToLowerInvariant();
            if (lower.StartsWith("#autoloc", StringComparison.Ordinal))
            {
                return true;
            }

            switch (lower)
            {
                case "size0":
                case "size1":
                case "size1p5":
                case "size2":
                case "size3":
                case "size4":
                case "srf":
                    return true;
                default:
                    return false;
            }
        }

        private static string GetTagDisplayName(string tag)
        {
            if (string.Equals(tag, "intake", StringComparison.OrdinalIgnoreCase))
            {
                return "Intakes";
            }

            return AuthorMatchHelper.FormatDisplayName(tag);
        }

        private void AddResourceEntries()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                AccumulateResourceCounts(part, counts);
            }

            AddResourceEntriesFromCounts(counts);
        }

        private IEnumerator AddResourceEntriesCoroutine()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<AvailablePart> availableParts = EditorPartAvailability.GetAvailableParts();
            for (int i = 0; i < availableParts.Count; i++)
            {
                AccumulateResourceCounts(availableParts[i], counts);

                if (i > 0 && i % FrameSliceBatchSize == 0)
                {
                    yield return null;
                }
            }

            AddResourceEntriesFromCounts(counts);
            yield return null;
        }

        private static void AccumulateResourceCounts(AvailablePart part, Dictionary<string, int> counts)
        {
            if (part == null || part.resourceInfos == null)
            {
                return;
            }

            // Only structured resource name/displayName — never tokenize free-form resourceInfo
            // prose (that produced dictionary-like junk suggestions from description text).
            for (int i = 0; i < part.resourceInfos.Count; i++)
            {
                AvailablePart.ResourceInfo resourceInfo = part.resourceInfos[i];
                if (resourceInfo == null)
                {
                    continue;
                }

                CollectStructuredResourceToken(resourceInfo.resourceName, counts);
                CollectStructuredResourceToken(resourceInfo.displayName, counts);
            }
        }

        private void AddResourceEntriesFromCounts(Dictionary<string, int> counts)
        {
            foreach (KeyValuePair<string, int> pair in counts
                .Where(entry => entry.Value >= 3)
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                string display = AuthorMatchHelper.FormatDisplayName(pair.Key);
                if (!TryAddVerifiedEntry(
                    SuggestionKind.FilterResource,
                    pair.Key,
                    display,
                    "resource",
                    pair.Value,
                    BuildSearchTerms(pair.Key, SplitCamelCase(pair.Key)),
                    out CategorizerEntry entry))
                {
                    continue;
                }

                _entries.Add(entry);
            }
        }

        private void AddTechEntries()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                if (part == null || string.IsNullOrWhiteSpace(part.TechRequired))
                {
                    continue;
                }

                string tech = part.TechRequired.Trim();
                if (counts.TryGetValue(tech, out int existing))
                {
                    counts[tech] = existing + 1;
                }
                else
                {
                    counts[tech] = 1;
                }
            }

            AddTechEntriesFromCounts(counts);
        }

        private IEnumerator AddTechEntriesCoroutine()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<AvailablePart> availableParts = EditorPartAvailability.GetAvailableParts();
            for (int i = 0; i < availableParts.Count; i++)
            {
                AvailablePart part = availableParts[i];
                if (part == null || string.IsNullOrWhiteSpace(part.TechRequired))
                {
                    continue;
                }

                string tech = part.TechRequired.Trim();
                if (counts.TryGetValue(tech, out int existing))
                {
                    counts[tech] = existing + 1;
                }
                else
                {
                    counts[tech] = 1;
                }

                if (i > 0 && i % FrameSliceBatchSize == 0)
                {
                    yield return null;
                }
            }

            AddTechEntriesFromCounts(counts);
            yield return null;
        }

        private void AddTechEntriesFromCounts(Dictionary<string, int> counts)
        {
            foreach (KeyValuePair<string, int> pair in counts.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (pair.Value <= 0)
                {
                    continue;
                }

                string display = AuthorMatchHelper.FormatDisplayName(pair.Key);
                if (!TryAddVerifiedEntry(
                    SuggestionKind.FilterTech,
                    pair.Key,
                    display,
                    "tech",
                    pair.Value,
                    BuildSearchTerms(display, SplitCamelCase(pair.Key)),
                    out CategorizerEntry entry))
                {
                    continue;
                }

                _entries.Add(entry);
            }
        }

        private static bool TryAddVerifiedEntry(
            SuggestionKind kind,
            string filterKey,
            string displayText,
            string kindLabel,
            int discoveredCount,
            string[] searchTerms,
            out CategorizerEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(filterKey) || discoveredCount <= 0)
            {
                return false;
            }

            if (DebugSettings.DumpIndexStats)
            {
                int verifiedCount = PartFilterMatcher.CountMatchingFilter(kind, filterKey);
                if (verifiedCount != discoveredCount)
                {
                    EditorBootstrap.LogWarning(
                        kindLabel
                        + " suggestion '"
                        + displayText
                        + "' count mismatch — discovered "
                        + discoveredCount
                        + ", predicate "
                        + verifiedCount
                        + " (key='"
                        + filterKey
                        + "').");
                }
            }

            entry = new CategorizerEntry
            {
                Kind = kind,
                FilterKey = filterKey,
                DisplayText = displayText,
                KindLabel = kindLabel,
                PartCount = discoveredCount,
                SearchTerms = searchTerms
            };
            return true;
        }

        private static void CollectStructuredResourceToken(string rawValue, Dictionary<string, int> counts)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return;
            }

            string token = rawValue.Trim().Trim(':');
            if (token.Length < 2 || !SuggestionTokenQuality.IsSuggestionWorthyResourceToken(token))
            {
                return;
            }

            // Reject multi-word prose accidentally stored as a display name.
            if (token.IndexOf(' ') >= 0 || token.IndexOf(',') >= 0)
            {
                return;
            }

            if (counts.TryGetValue(token, out int existing))
            {
                counts[token] = existing + 1;
            }
            else
            {
                counts[token] = 1;
            }
        }

        private static bool IsBroadCategorizerKind(SuggestionKind kind)
        {
            switch (kind)
            {
                case SuggestionKind.FilterTag:
                case SuggestionKind.FilterManufacturer:
                case SuggestionKind.FilterCategory:
                case SuggestionKind.FilterModule:
                case SuggestionKind.FilterResource:
                case SuggestionKind.FilterTech:
                    return true;
                default:
                    return false;
            }
        }

        private static int ScoreEntry(CategorizerEntry entry, string[] words)
        {
            int worstRank = -1;
            for (int i = 0; i < words.Length; i++)
            {
                int wordRank = ScoreWord(entry, words[i]);
                if (wordRank < 0)
                {
                    return -1;
                }

                if (wordRank > worstRank)
                {
                    worstRank = wordRank;
                }
            }

            return worstRank;
        }

        private static int ScoreWord(CategorizerEntry entry, string word)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return -1;
            }

            int best = -1;
            for (int i = 0; i < entry.SearchTerms.Length; i++)
            {
                int rank = ScoreTextMatch(entry.Kind, entry.SearchTerms[i], word);
                if (rank >= 0 && (best < 0 || rank < best))
                {
                    best = rank;
                }
            }

            return best;
        }

        private static int ScoreTextMatch(SuggestionKind kind, string candidate, string word)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return -1;
            }

            int prefixRank = GetPrefixRank(kind);
            int containsRank = prefixRank + ContainsRankOffset;

            if (candidate.StartsWith(word, StringComparison.OrdinalIgnoreCase))
            {
                return prefixRank;
            }

            if (candidate.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return containsRank;
            }

            foreach (string token in AuthorMatchHelper.SplitIntoWords(candidate))
            {
                if (token.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                {
                    return prefixRank + 1;
                }
            }

            return -1;
        }

        private static int GetPrefixRank(SuggestionKind kind)
        {
            switch (kind)
            {
                case SuggestionKind.FilterFunction:
                    return FunctionPrefixRank;
                case SuggestionKind.FilterCategory:
                    return CategoryPrefixRank;
                case SuggestionKind.FilterManufacturer:
                    return ManufacturerPrefixRank;
                case SuggestionKind.FilterDiameter:
                    return DiameterExactRank;
                case SuggestionKind.FilterModule:
                    return ModulePrefixRank;
                case SuggestionKind.FilterTag:
                    return TagPrefixRank;
                case SuggestionKind.FilterResource:
                    return ResourcePrefixRank;
                case SuggestionKind.FilterTech:
                    return TechPrefixRank;
                default:
                    return ContainsRankOffset;
            }
        }

        private static string[] BuildSearchTerms(string displayText, params string[] extraTerms)
        {
            var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddTerm(terms, displayText);
            AddTerm(terms, SplitCamelCase(displayText));

            if (extraTerms != null)
            {
                for (int i = 0; i < extraTerms.Length; i++)
                {
                    AddTerm(terms, extraTerms[i]);
                }
            }

            return terms.ToArray();
        }

        private static void AddTerm(HashSet<string> terms, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            terms.Add(value.Trim());
        }

        private static string GetDiameterDisplayName(string profileTag)
        {
            switch (profileTag.ToLowerInvariant())
            {
                case "size0":
                    return "0.625m";
                case "size1":
                    return "1.25m";
                case "size1p5":
                    return "1.875m";
                case "size2":
                    return "2.5m";
                case "size3":
                    return "3.75m";
                case "size4":
                    return "5m";
                case "srf":
                    return "Radial";
                default:
                    return profileTag;
            }
        }

        private static string[] GetDiameterAliases(string profileTag)
        {
            switch (profileTag.ToLowerInvariant())
            {
                case "size0":
                    return new[] { "0.625", "625", "size0", "tiny", "tinyrad" };
                case "size1":
                    return new[] { "1.25", "125", "size1" };
                case "size1p5":
                    return new[] { "1.875", "1875", "size1p5" };
                case "size2":
                    return new[] { "2.5", "25", "size2" };
                case "size3":
                    return new[] { "3.75", "375", "size3" };
                case "size4":
                    return new[] { "5", "5m", "size4" };
                case "srf":
                    return new[] { "radial", "srf", "surface" };
                default:
                    return new string[0];
            }
        }

        private static string GetDiameterSortKey(string profileTag)
        {
            switch (profileTag.ToLowerInvariant())
            {
                case "size0":
                    return "01";
                case "size1":
                    return "02";
                case "size1p5":
                    return "03";
                case "size2":
                    return "04";
                case "size3":
                    return "05";
                case "size4":
                    return "06";
                case "srf":
                    return "07";
                default:
                    return profileTag;
            }
        }

        private static void LogIntakeQueryMatches(string query, List<PartSuggestion> results, int emittedCount)
        {
            if (!DebugSettings.Verbose || !string.Equals(query, "intake", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (results.Count == 0)
            {
                EditorBootstrap.Log("Categorizer match 'intake': no rows indexed.");
                return;
            }

            var builder = new StringBuilder();
            builder.Append("Categorizer match 'intake': emitted ");
            builder.Append(emittedCount);
            builder.Append("/");
            builder.Append(results.Count);
            builder.Append(" — ");

            int limit = Math.Min(results.Count, 8);
            for (int i = 0; i < limit; i++)
            {
                PartSuggestion suggestion = results[i];
                if (i > 0)
                {
                    builder.Append("; ");
                }

                builder.Append("'");
                builder.Append(suggestion.DisplayText ?? string.Empty);
                builder.Append("' kind=");
                builder.Append(suggestion.Kind);
                builder.Append(" key='");
                builder.Append(suggestion.FilterKey ?? string.Empty);
                builder.Append("' rank=");
                builder.Append(suggestion.RankScore);
            }

            EditorBootstrap.Log(builder.ToString());
        }

        private static string SplitCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (i > 0 && char.IsUpper(c) && char.IsLower(value[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }

        private sealed class CategorizerEntry
        {
            public SuggestionKind Kind;
            public string FilterKey;
            public string DisplayText;
            public string KindLabel;
            public int PartCount;
            public string[] SearchTerms;
        }

        private sealed class ModuleEntry
        {
            public string FilterKey;
            public string DisplayText;
            public int PartCount;
        }

        private sealed class TagEntry
        {
            public string FilterKey;
            public string DisplayText;
            public int PartCount;
        }
    }
}
