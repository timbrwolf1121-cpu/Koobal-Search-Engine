using System;
using System.Collections.Generic;

namespace PartSearchSuggest
{
    /// <summary>
    /// Removes suggestions that would filter to the same part set, keeping the highest-priority row.
    /// Priority: part &gt; mod name &gt; author &gt; suite &gt; function &gt; category &gt;
    /// manufacturer/diameter &gt; tag &gt; module &gt; resource/tech &gt; history.
    /// Stock categories must beat free-floating tags so "eng" keeps Engine/Engines.
    /// </summary>
    internal static class SuggestionDedupHelper
    {
        private static readonly Dictionary<string, string> SignatureCache = new Dictionary<string, string>(StringComparer.Ordinal);

        public static List<PartSuggestion> Dedup(IReadOnlyList<PartSuggestion> suggestions, string query)
        {
            if (suggestions == null || suggestions.Count <= 1)
            {
                return suggestions == null
                    ? new List<PartSuggestion>()
                    : new List<PartSuggestion>(suggestions);
            }

            var sorted = new List<PartSuggestion>(suggestions);
            sorted.Sort(ComparePriority);

            var kept = new List<PartSuggestion>(sorted.Count);
            var seenExactKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenPartSets = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < sorted.Count; i++)
            {
                PartSuggestion candidate = sorted[i];
                if (candidate == null)
                {
                    continue;
                }

                string exactKey = BuildExactKey(candidate);
                if (exactKey != null && !seenExactKeys.Add(exactKey))
                {
                    LogDedup(query, candidate, exactKey, "exact key");
                    continue;
                }

                // Stock Function vs Category tabs are distinct UI intents (Engines vs Engine).
                // Never collapse them against each other via part-set equality.
                if (IsStockTabKind(candidate.Kind))
                {
                    kept.Add(candidate);
                    continue;
                }

                string partSetKey = GetPartSetSignature(candidate);
                if (partSetKey != null && !seenPartSets.Add(partSetKey))
                {
                    LogDedup(query, candidate, partSetKey, "part set");
                    continue;
                }

                kept.Add(candidate);
            }

            return kept;
        }

        private static int ComparePriority(PartSuggestion left, PartSuggestion right)
        {
            int kindCompare = GetKindPriority(left).CompareTo(GetKindPriority(right));
            if (kindCompare != 0)
            {
                return kindCompare;
            }

            int rankCompare = left.RankScore.CompareTo(right.RankScore);
            if (rankCompare != 0)
            {
                return rankCompare;
            }

            return string.Compare(
                left.DisplayText,
                right.DisplayText,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStockTabKind(SuggestionKind kind)
        {
            return kind == SuggestionKind.FilterFunction
                || kind == SuggestionKind.FilterCategory;
        }

        internal static int GetKindPriority(PartSuggestion suggestion)
        {
            if (suggestion == null)
            {
                return 99;
            }

            switch (suggestion.Kind)
            {
                case SuggestionKind.Part:
                    return 0;
                case SuggestionKind.ModName:
                    return 1;
                case SuggestionKind.ModAuthor:
                    return 3;
                case SuggestionKind.ModSuite:
                    return 5;
                case SuggestionKind.FilterFunction:
                    return 6;
                case SuggestionKind.FilterCategory:
                    return 7;
                case SuggestionKind.FilterManufacturer:
                    return 8;
                case SuggestionKind.FilterDiameter:
                    return 9;
                case SuggestionKind.FilterTag:
                    return 10;
                case SuggestionKind.FilterModule:
                    return 11;
                case SuggestionKind.FilterResource:
                    return 12;
                case SuggestionKind.FilterTech:
                    return 13;
                case SuggestionKind.History:
                    return 14;
                default:
                    return 13;
            }
        }

        private static string BuildExactKey(PartSuggestion suggestion)
        {
            if (suggestion.Kind == SuggestionKind.Part)
            {
                return suggestion.Part != null
                    ? "part:" + suggestion.Part.name
                    : null;
            }

            if (suggestion.Kind == SuggestionKind.History)
            {
                return "history:" + (suggestion.QueryText ?? suggestion.DisplayText ?? string.Empty);
            }

            if (string.IsNullOrWhiteSpace(suggestion.FilterKey))
            {
                return null;
            }

            return suggestion.Kind + ":" + suggestion.FilterKey.Trim();
        }

        private static string GetPartSetSignature(PartSuggestion suggestion)
        {
            string cacheKey = BuildExactKey(suggestion);
            if (cacheKey != null && SignatureCache.TryGetValue(cacheKey, out string cached))
            {
                return cached;
            }

            string signature = ComputePartSetSignature(suggestion);
            if (cacheKey != null && signature != null)
            {
                SignatureCache[cacheKey] = signature;
            }

            return signature;
        }

        private static string ComputePartSetSignature(PartSuggestion suggestion)
        {
            if (suggestion == null)
            {
                return null;
            }

            if (suggestion.Kind == SuggestionKind.Part && suggestion.Part != null)
            {
                return "1:" + suggestion.Part.name.GetHashCode();
            }

            if (suggestion.Kind == SuggestionKind.History)
            {
                return null;
            }

            if (PartLoader.Instance == null || PartLoader.Instance.loadedParts == null)
            {
                return null;
            }

            var names = new List<string>();
            foreach (AvailablePart part in PartLoader.Instance.loadedParts)
            {
                if (EditorPartAvailability.IsAvailableInEditor(part) && PartMatchesSuggestion(part, suggestion))
                {
                    names.Add(part.name);
                }
            }

            if (names.Count == 0)
            {
                return suggestion.Kind + ":empty:" + (suggestion.FilterKey ?? suggestion.DisplayText ?? string.Empty);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);

            int hash = 17;
            for (int i = 0; i < names.Count; i++)
            {
                hash = (hash * 31) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(names[i]);
            }

            return names.Count + ":" + hash;
        }

        private static bool PartMatchesSuggestion(AvailablePart part, PartSuggestion suggestion)
        {
            if (part == null || suggestion == null)
            {
                return false;
            }

            switch (suggestion.Kind)
            {
                case SuggestionKind.Part:
                    return suggestion.Part != null
                        && string.Equals(part.name, suggestion.Part.name, StringComparison.Ordinal);

                case SuggestionKind.ModAuthor:
                    return AuthorAttribution.PartMatchesAuthorFilter(part, suggestion.FilterKey);

                case SuggestionKind.ModName:
                    return ModFilterMatcher.PartMatchesModFolder(part, suggestion.FilterKey);

                case SuggestionKind.ModSuite:
                    return ModFilterMatcher.PartMatchesModSuite(part, suggestion.FilterKey ?? suggestion.QueryText);

                case SuggestionKind.FilterFunction:
                case SuggestionKind.FilterManufacturer:
                case SuggestionKind.FilterDiameter:
                case SuggestionKind.FilterCategory:
                case SuggestionKind.FilterModule:
                case SuggestionKind.FilterResource:
                case SuggestionKind.FilterTech:
                case SuggestionKind.FilterTag:
                    return PartFilterMatcher.PartMatchesFilter(
                        suggestion.Kind,
                        suggestion.FilterKey,
                        part);

                default:
                    return false;
            }
        }

        private static void LogDedup(string query, PartSuggestion removed, string key, string reason)
        {
            if (!DebugSettings.Verbose)
            {
                return;
            }

            EditorBootstrap.Log(
                "Dedup '"
                + (query ?? string.Empty)
                + "': dropped '"
                + (removed.DisplayText ?? string.Empty)
                + "' kind="
                + removed.Kind
                + " key='"
                + (removed.FilterKey ?? string.Empty)
                + "' ("
                + reason
                + ": "
                + key
                + ")");
        }
    }
}
