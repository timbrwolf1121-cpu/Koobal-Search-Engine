using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PartSearchSuggest
{
    internal static class CkanAuthorRegistry
    {
        private static readonly Regex InstalledFilesFolderPattern = new Regex(
            "\"GameData/([^\"/]+)\"",
            RegexOptions.Compiled);

        public static IReadOnlyDictionary<string, string> LoadAuthorsByModFolder()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string path = Path.Combine(KSPUtil.ApplicationRootPath, "CKAN", "registry.json");
            if (!File.Exists(path))
            {
                EditorBootstrap.Log("CKAN registry not found — skipping CKAN author enrichment.");
                return result;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                EditorBootstrap.LogWarning("Failed to read CKAN registry: " + ex.Message);
                return result;
            }

            string modulesBody = ExtractJsonObjectBody(text, "installed_modules");
            if (string.IsNullOrEmpty(modulesBody))
            {
                EditorBootstrap.LogWarning("CKAN registry has no installed_modules section.");
                return result;
            }

            foreach (string moduleBody in SplitTopLevelObjectEntries(modulesBody))
            {
                string folder = ExtractModFolder(moduleBody);
                string authors = ExtractSourceModuleAuthors(moduleBody);
                if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(authors))
                {
                    continue;
                }

                if (result.TryGetValue(folder, out string existing))
                {
                    result[folder] = MergeAuthorStrings(existing, authors);
                }
                else
                {
                    result[folder] = authors;
                }
            }

            EditorBootstrap.Log("CKAN author enrichment: " + result.Count + " mod folders with authors.");
            return result;
        }

        private static string ExtractModFolder(string moduleBody)
        {
            string installedFilesBody = ExtractJsonObjectBody(moduleBody, "installed_files");
            if (string.IsNullOrEmpty(installedFilesBody))
            {
                return string.Empty;
            }

            Match match = InstalledFilesFolderPattern.Match(installedFilesBody);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static string ExtractSourceModuleAuthors(string moduleBody)
        {
            string sourceModuleBody = ExtractJsonObjectBody(moduleBody, "source_module");
            if (string.IsNullOrEmpty(sourceModuleBody))
            {
                return string.Empty;
            }

            Match arrayMatch = Regex.Match(
                sourceModuleBody,
                "\"author\"\\s*:\\s*\\[([^\\]]*)\\]",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (arrayMatch.Success)
            {
                var authors = new List<string>();
                foreach (Match entry in Regex.Matches(arrayMatch.Groups[1].Value, "\"([^\"]+)\""))
                {
                    string value = entry.Groups[1].Value.Trim();
                    if (value.Length > 0)
                    {
                        authors.Add(value);
                    }
                }

                return authors.Count == 0 ? string.Empty : string.Join(", ", authors);
            }

            Match stringMatch = Regex.Match(
                sourceModuleBody,
                "\"author\"\\s*:\\s*\"([^\"]*)\"",
                RegexOptions.IgnoreCase);

            return stringMatch.Success ? stringMatch.Groups[1].Value.Trim() : string.Empty;
        }

        private static string MergeAuthorStrings(string left, string right)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in AuthorTokenizer.Tokenize(left))
            {
                tokens.Add(token);
            }

            foreach (string token in AuthorTokenizer.Tokenize(right))
            {
                tokens.Add(token);
            }

            if (tokens.Count == 0)
            {
                return string.Empty;
            }

            var merged = new List<string>(tokens);
            merged.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", merged);
        }

        private static string ExtractJsonObjectBody(string text, string key)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            string pattern = "\"" + Regex.Escape(key) + "\"\\s*:\\s*\\{";
            Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return string.Empty;
            }

            int start = match.Index + match.Length - 1;
            int end = FindMatchingBrace(text, start);
            if (end <= start)
            {
                return string.Empty;
            }

            return text.Substring(start + 1, end - start - 1);
        }

        private static IEnumerable<string> SplitTopLevelObjectEntries(string objectBody)
        {
            if (string.IsNullOrEmpty(objectBody))
            {
                yield break;
            }

            int index = 0;
            while (index < objectBody.Length)
            {
                int keyStart = objectBody.IndexOf('"', index);
                if (keyStart < 0)
                {
                    yield break;
                }

                int keyEnd = objectBody.IndexOf('"', keyStart + 1);
                if (keyEnd < 0)
                {
                    yield break;
                }

                int braceStart = objectBody.IndexOf('{', keyEnd);
                if (braceStart < 0)
                {
                    yield break;
                }

                int braceEnd = FindMatchingBrace(objectBody, braceStart);
                if (braceEnd <= braceStart)
                {
                    yield break;
                }

                yield return objectBody.Substring(braceStart, braceEnd - braceStart + 1);
                index = braceEnd + 1;
            }
        }

        private static int FindMatchingBrace(string text, int openBraceIndex)
        {
            if (openBraceIndex < 0 || openBraceIndex >= text.Length || text[openBraceIndex] != '{')
            {
                return -1;
            }

            int depth = 0;
            bool inString = false;
            for (int i = openBraceIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }
    }
}
