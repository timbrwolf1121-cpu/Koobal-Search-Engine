using System;
using System.Collections.Generic;

namespace PartSearchSuggest
{
    /// <summary>
    /// Light personal ranking prior from search-history clicks / remembered queries.
    /// Rebuilt cheaply from the in-memory history list (no craft scanning).
    /// </summary>
    internal static class PartPersonalPriors
    {
        internal const int MaxSoftBoost = 2;

        private static readonly HashSet<string> RememberedTokens =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static void RefreshFromHistory(IEnumerable<string> historyEntries)
        {
            RememberedTokens.Clear();
            if (historyEntries == null)
            {
                return;
            }

            foreach (string entry in historyEntries)
            {
                string trimmed = (entry ?? string.Empty).Trim();
                if (trimmed.Length < 2)
                {
                    continue;
                }

                RememberedTokens.Add(trimmed);

                // Also index last path segment / token for internal-name style history.
                int space = trimmed.LastIndexOf(' ');
                if (space > 0 && space < trimmed.Length - 1)
                {
                    RememberedTokens.Add(trimmed.Substring(space + 1));
                }
            }
        }

        internal static int SoftBoost(AvailablePart part)
        {
            if (part == null || RememberedTokens.Count == 0)
            {
                return 0;
            }

            string title = (part.title ?? string.Empty).Trim();
            string name = (part.name ?? string.Empty).Trim();

            if (title.Length > 0 && RememberedTokens.Contains(title))
            {
                return MaxSoftBoost;
            }

            if (name.Length > 0 && RememberedTokens.Contains(name))
            {
                return MaxSoftBoost;
            }

            // Prefix / contains against remembered queries (light).
            foreach (string token in RememberedTokens)
            {
                if (token.Length < 3)
                {
                    continue;
                }

                if (title.Length > 0
                    && (title.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0
                        || token.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return 1;
                }

                if (name.Length > 0
                    && (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0
                        || token.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return 1;
                }
            }

            return 0;
        }
    }
}
