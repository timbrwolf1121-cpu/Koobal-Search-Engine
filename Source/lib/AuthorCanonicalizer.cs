using System;
using System.Collections.Generic;

namespace PartSearchSuggest
{
    internal static class AuthorCanonicalizer
    {
        private static readonly Dictionary<string, string> TokenToCanonical =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, HashSet<string>> CanonicalToAliases =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public static void Build(IEnumerable<string> allTokens)
        {
            TokenToCanonical.Clear();
            CanonicalToAliases.Clear();

            var formToTokens = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var uniqueTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawToken in allTokens)
            {
                string token = Clean(rawToken);
                if (token.Length < 2 || !uniqueTokens.Add(token))
                {
                    continue;
                }

                foreach (string form in GetLinkingForms(token))
                {
                    if (!formToTokens.TryGetValue(form, out List<string> linked))
                    {
                        linked = new List<string>();
                        formToTokens[form] = linked;
                    }

                    linked.Add(token);
                }
            }

            var grouped = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<string>> entry in formToTokens)
            {
                if (entry.Value.Count < 2)
                {
                    continue;
                }

                for (int i = 1; i < entry.Value.Count; i++)
                {
                    MergeTokens(grouped, entry.Value[0], entry.Value[i]);
                }
            }

            foreach (string token in uniqueTokens)
            {
                if (!grouped.TryGetValue(token, out HashSet<string> aliasSet))
                {
                    aliasSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { token };
                    grouped[token] = aliasSet;
                }

                string canonical = ChooseCanonical(aliasSet);
                foreach (string alias in aliasSet)
                {
                    TokenToCanonical[alias] = canonical;
                }

                if (!CanonicalToAliases.TryGetValue(canonical, out HashSet<string> canonicalSet))
                {
                    canonicalSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    CanonicalToAliases[canonical] = canonicalSet;
                }

                foreach (string alias in aliasSet)
                {
                    canonicalSet.Add(alias);
                }
            }
        }

        public static string GetCanonicalKey(string token)
        {
            string cleaned = Clean(token);
            if (cleaned.Length < 2)
            {
                return cleaned;
            }

            if (TokenToCanonical.TryGetValue(cleaned, out string canonical))
            {
                return canonical;
            }

            return cleaned;
        }

        public static IReadOnlyCollection<string> GetAliasGroup(string tokenOrCanonical)
        {
            string canonical = GetCanonicalKey(tokenOrCanonical);
            if (canonical.Length < 2)
            {
                return Array.Empty<string>();
            }

            if (CanonicalToAliases.TryGetValue(canonical, out HashSet<string> aliases))
            {
                return aliases;
            }

            return new[] { canonical };
        }

        public static bool TokensAreEquivalent(string left, string right)
        {
            return string.Equals(GetCanonicalKey(left), GetCanonicalKey(right), StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> GetLinkingForms(string token)
        {
            yield return token;
            yield return Normalize(token);

            foreach (string form in AuthorTokenizer.GetSearchableForms(token))
            {
                yield return form;
                yield return Normalize(form);
            }
        }

        private static void MergeTokens(
            Dictionary<string, HashSet<string>> grouped,
            string left,
            string right)
        {
            grouped.TryGetValue(left, out HashSet<string> leftSet);
            grouped.TryGetValue(right, out HashSet<string> rightSet);

            if (leftSet == null && rightSet == null)
            {
                grouped[left] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { left, right };
                return;
            }

            if (leftSet == null)
            {
                rightSet.Add(left);
                grouped[left] = rightSet;
                return;
            }

            if (rightSet == null)
            {
                leftSet.Add(right);
                grouped[right] = leftSet;
                return;
            }

            if (ReferenceEquals(leftSet, rightSet))
            {
                return;
            }

            foreach (string alias in rightSet)
            {
                leftSet.Add(alias);
            }

            foreach (string alias in rightSet)
            {
                grouped[alias] = leftSet;
            }
        }

        private static string ChooseCanonical(HashSet<string> aliases)
        {
            string best = null;
            foreach (string alias in aliases)
            {
                if (best == null
                    || alias.Length > best.Length
                    || (alias.Length == best.Length
                        && string.Compare(alias, best, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    best = alias;
                }
            }

            return best ?? string.Empty;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
            }

            return builder.ToString();
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Trim('"', '\'', ' ');
        }
    }
}
