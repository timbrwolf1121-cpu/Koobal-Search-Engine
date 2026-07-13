using System;
using System.Collections.Generic;

namespace PartSearchSuggest
{
    internal static class AuthorAttribution
    {
        public static void CollectPartAuthorTokens(AvailablePart part, ICollection<string> tokens)
        {
            if (part == null || tokens == null)
            {
                return;
            }

            ModMetadata metadata = ModMetadataCache.ResolveForPart(part);
            AuthorTokenizer.CollectTokens(metadata.Author, tokens);
            AuthorTokenizer.CollectTokens(part.author, tokens);

            if (!string.IsNullOrEmpty(metadata.FolderName))
            {
                ModMetadataCache.CollectModAuthorTokens(metadata.FolderName, tokens);
            }
        }

        public static HashSet<string> GetPartAuthorTokens(AvailablePart part)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectPartAuthorTokens(part, tokens);
            return tokens;
        }

        public static bool PartMatchesAuthorFilter(AvailablePart part, string authorFilterKey)
        {
            if (!EditorPartAvailability.IsAvailableInEditor(part) || string.IsNullOrWhiteSpace(authorFilterKey))
            {
                return false;
            }

            string canonical = AuthorCanonicalizer.GetCanonicalKey(authorFilterKey.Trim());
            if (canonical.Length < 2)
            {
                return false;
            }

            IReadOnlyCollection<string> aliasGroup = AuthorCanonicalizer.GetAliasGroup(canonical);
            if (aliasGroup.Count == 0)
            {
                return false;
            }

            HashSet<string> partTokens = GetPartAuthorTokens(part);
            foreach (string partToken in partTokens)
            {
                if (AliasGroupContains(aliasGroup, partToken))
                {
                    return true;
                }

                if (AuthorCanonicalizer.TokensAreEquivalent(partToken, canonical))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AliasGroupContains(IReadOnlyCollection<string> aliasGroup, string token)
        {
            if (aliasGroup == null || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            foreach (string alias in aliasGroup)
            {
                if (string.Equals(alias, token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static int CountPartsMatchingAuthorFilter(string authorFilterKey)
        {
            if (PartLoader.Instance == null || PartLoader.Instance.loadedParts == null)
            {
                return 0;
            }

            int matched = 0;
            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                if (PartMatchesAuthorFilter(part, authorFilterKey))
                {
                    matched++;
                }
            }

            return matched;
        }
    }
}
