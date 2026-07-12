using System;
using System.Text;

namespace PartSearchSuggest
{
    /// <summary>
    /// Shared mod folder / suite matching for metadata index, apply, and dedup.
    /// </summary>
    internal static class ModFilterMatcher
    {
        internal static bool PartMatchesModFolder(AvailablePart part, string modFolder)
        {
            if (!EditorPartAvailability.IsAvailableInEditor(part) || string.IsNullOrWhiteSpace(modFolder))
            {
                return false;
            }

            string folder = ModMetadataCache.ExtractModFolderFromPart(part);
            return !string.IsNullOrEmpty(folder)
                && string.Equals(folder, modFolder.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        internal static int CountPartsMatchingModFolder(string modFolder)
        {
            if (string.IsNullOrWhiteSpace(modFolder)
                || PartLoader.Instance == null
                || PartLoader.Instance.loadedParts == null)
            {
                return 0;
            }

            int count = 0;
            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                if (PartMatchesModFolder(part, modFolder))
                {
                    count++;
                }
            }

            return count;
        }

        internal static int CountPartsMatchingModSuite(string query)
        {
            if (string.IsNullOrWhiteSpace(query)
                || PartLoader.Instance == null
                || PartLoader.Instance.loadedParts == null)
            {
                return 0;
            }

            int count = 0;
            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                if (PartMatchesModSuite(part, query))
                {
                    count++;
                }
            }

            return count;
        }

        internal static bool PartMatchesModSuite(AvailablePart part, string query)
        {
            if (part == null || string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            string[] words = query.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return false;
            }

            return PartMatchesModSuiteWords(part, words);
        }

        internal static bool PartMatchesModSuiteWords(AvailablePart part, string[] words)
        {
            if (part == null || words == null || words.Length == 0)
            {
                return false;
            }

            ModMetadata metadata = ModMetadataCache.ResolveForPart(part);
            string searchText = (metadata.DisplayName + " " + metadata.FolderName).ToLowerInvariant();
            string camelFolder = SplitCamelCase(metadata.FolderName).ToLowerInvariant();

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i].ToLowerInvariant();
                if (searchText.IndexOf(word, StringComparison.Ordinal) < 0
                    && camelFolder.IndexOf(word, StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }

            return true;
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
    }
}
