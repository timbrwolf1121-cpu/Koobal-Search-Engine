namespace PartSearchSuggest
{
    internal enum SuggestionKind
    {
        Part,
        ModAuthor,
        ModName,
        ModSuite,
        FilterFunction,
        FilterManufacturer,
        FilterDiameter,
        FilterCategory,
        FilterModule,
        FilterResource,
        FilterTech,
        FilterTag,
        History
    }

    internal sealed class PartSuggestion
    {
        public SuggestionKind Kind { get; set; } = SuggestionKind.Part;

        public string QueryText { get; set; }

        public string DisplayText { get; set; }

        public string MatchReason { get; set; }

        public string FilterKey { get; set; }

        /// <summary>Cfg icon name for category rows (resolved from live stock categorizer buttons).</summary>
        public string IconName { get; set; }

        public AvailablePart Part { get; set; }

        public bool IsHistory { get; set; }

        public int RankScore { get; set; } = 999;

        /// <summary>
        /// Part count from index build (≥0). When set, <see cref="IsValid"/> /
        /// <see cref="RefreshVerifiedSubtitle"/> must not re-scan loaded parts (typing path).
        /// -1 means unknown — fall back to live verification (apply / rare paths only).
        /// </summary>
        public int IndexedPartCount { get; set; } = -1;

        public bool IsMetadata =>
            Kind == SuggestionKind.ModAuthor
            || Kind == SuggestionKind.ModName
            || Kind == SuggestionKind.ModSuite;

        public bool IsCategorizerFilter =>
            Kind == SuggestionKind.FilterFunction
            || Kind == SuggestionKind.FilterManufacturer
            || Kind == SuggestionKind.FilterDiameter
            || Kind == SuggestionKind.FilterCategory
            || Kind == SuggestionKind.FilterModule
            || Kind == SuggestionKind.FilterResource
            || Kind == SuggestionKind.FilterTech
            || Kind == SuggestionKind.FilterTag;

        public bool IsFirstClass => IsMetadata || IsCategorizerFilter;

        /// <summary>
        /// True only for rows that map to a stock category/subcategory tab (display icon only).
        /// Predicate-based filter matches (tag, module, resource, etc.) are text-only.
        /// </summary>
        public bool ShouldShowCategoryIcon()
        {
            switch (Kind)
            {
                case SuggestionKind.FilterFunction:
                case SuggestionKind.FilterCategory:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Part count for UI / validity. Prefers <see cref="IndexedPartCount"/> so suggestion
        /// refresh never re-walks PartLoader. Live scan only when index count is unknown.
        /// Returns -1 for history rows.
        /// </summary>
        public int GetVerifiedPartCount()
        {
            if (Kind == SuggestionKind.History)
            {
                return -1;
            }

            if (Kind == SuggestionKind.Part)
            {
                return Part != null && EditorPartAvailability.IsAvailableInEditor(Part) ? 1 : 0;
            }

            if (IndexedPartCount >= 0)
            {
                return IndexedPartCount;
            }

            switch (Kind)
            {
                case SuggestionKind.ModAuthor:
                    return AuthorAttribution.CountPartsMatchingAuthorFilter(FilterKey ?? QueryText);

                case SuggestionKind.ModName:
                    return ModFilterMatcher.CountPartsMatchingModFolder(FilterKey);

                case SuggestionKind.ModSuite:
                    return ModFilterMatcher.CountPartsMatchingModSuite(FilterKey ?? QueryText);

                case SuggestionKind.FilterFunction:
                case SuggestionKind.FilterManufacturer:
                case SuggestionKind.FilterDiameter:
                case SuggestionKind.FilterCategory:
                case SuggestionKind.FilterModule:
                case SuggestionKind.FilterResource:
                case SuggestionKind.FilterTech:
                case SuggestionKind.FilterTag:
                    return PartFilterMatcher.CountMatchingFilter(Kind, FilterKey);

                default:
                    return -1;
            }
        }

        public bool IsValid()
        {
            if (Kind == SuggestionKind.History)
            {
                return !string.IsNullOrWhiteSpace(QueryText);
            }

            if (Kind == SuggestionKind.Part)
            {
                return Part != null && EditorPartAvailability.IsAvailableInEditor(Part);
            }

            if (IndexedPartCount >= 0)
            {
                return IndexedPartCount > 0;
            }

            return GetVerifiedPartCount() > 0;
        }

        /// <summary>
        /// Formats subtitle from <see cref="IndexedPartCount"/> when available. Does not live-scan
        /// unless the index count is unknown (apply / debug paths).
        /// </summary>
        public void RefreshVerifiedSubtitle()
        {
            if (Kind == SuggestionKind.History || Kind == SuggestionKind.Part)
            {
                return;
            }

            int count = GetVerifiedPartCount();
            if (count <= 0)
            {
                return;
            }

            switch (Kind)
            {
                case SuggestionKind.ModAuthor:
                    MatchReason = "author · " + count + " parts";
                    break;

                case SuggestionKind.ModName:
                    MatchReason = "mod · " + count + " parts";
                    break;

                case SuggestionKind.ModSuite:
                    MatchReason = "suite · " + count + " parts";
                    break;

                case SuggestionKind.FilterFunction:
                case SuggestionKind.FilterManufacturer:
                case SuggestionKind.FilterDiameter:
                case SuggestionKind.FilterCategory:
                case SuggestionKind.FilterModule:
                case SuggestionKind.FilterResource:
                case SuggestionKind.FilterTech:
                case SuggestionKind.FilterTag:
                    string kindLabel = GetCategorizerKindLabel();
                    if (kindLabel.Length > 0)
                    {
                        MatchReason = kindLabel + " · " + count + " parts";
                    }
                    break;
            }
        }

        private string GetCategorizerKindLabel()
        {
            if (MatchReason == null)
            {
                return string.Empty;
            }

            int separator = MatchReason.IndexOf('·');
            if (separator < 0)
            {
                return string.Empty;
            }

            return MatchReason.Substring(0, separator).Trim();
        }
    }
}
