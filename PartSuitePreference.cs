using System;
using System.Collections.Generic;

namespace PartSearchSuggest
{
    /// <summary>
    /// When multiple installed parts share a display title (Restock-style families),
    /// lightly prefer the non-stock / installed-suite copy as a RankScore tie-break.
    /// Built once at index time from editor-available parts.
    /// </summary>
    internal static class PartSuitePreference
    {
        internal const int MaxSoftBoost = 2;

        private static readonly HashSet<string> DuplicateTitles =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> StockFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Squad",
            "SquadExpansion"
        };

        private static bool _built;

        internal static void BuildFromAvailableParts()
        {
            DuplicateTitles.Clear();
            _built = true;

            var titleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                if (part == null)
                {
                    continue;
                }

                string title = CleanTitle(part);
                if (title.Length < 2)
                {
                    continue;
                }

                if (titleCounts.TryGetValue(title, out int count))
                {
                    titleCounts[title] = count + 1;
                }
                else
                {
                    titleCounts[title] = 1;
                }
            }

            foreach (KeyValuePair<string, int> pair in titleCounts)
            {
                if (pair.Value > 1)
                {
                    DuplicateTitles.Add(pair.Key);
                }
            }

            EditorBootstrap.Log(
                "Part suite preference: "
                + DuplicateTitles.Count
                + " duplicate display titles among editor parts.");
        }

        internal static int SoftBoost(AvailablePart part)
        {
            if (!_built)
            {
                BuildFromAvailableParts();
            }

            if (part == null || DuplicateTitles.Count == 0)
            {
                return 0;
            }

            string title = CleanTitle(part);
            if (title.Length < 2 || !DuplicateTitles.Contains(title))
            {
                return 0;
            }

            string folder = ModMetadataCache.ExtractModFolderFromPart(part);
            if (string.IsNullOrEmpty(folder) || StockFolders.Contains(folder))
            {
                return 0;
            }

            // Installed suite / Restock-style replacement for a colliding stock title.
            return MaxSoftBoost;
        }

        private static string CleanTitle(AvailablePart part)
        {
            string title = (part.title ?? string.Empty).Trim();
            if (title.Length > 0)
            {
                return title;
            }

            return (part.name ?? string.Empty).Trim();
        }
    }
}
