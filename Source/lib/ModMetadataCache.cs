using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace PartSearchSuggest
{
    internal sealed class ModMetadataCache
    {
        private static readonly Dictionary<string, ModMetadata> Cache =
            new Dictionary<string, ModMetadata>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, HashSet<string>> ModAuthorTokens =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private static bool _built;

        public static void Build()
        {
            if (_built)
            {
                return;
            }

            Cache.Clear();
            ModAuthorTokens.Clear();

            string gameDataPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData");
            if (!Directory.Exists(gameDataPath))
            {
                EditorBootstrap.LogWarning("GameData folder not found — mod metadata cache empty.");
                _built = true;
                return;
            }

            foreach (string versionFile in Directory.GetFiles(gameDataPath, "*.version", SearchOption.AllDirectories))
            {
                string modFolder = ResolveModFolderKey(gameDataPath, versionFile);
                if (string.IsNullOrEmpty(modFolder))
                {
                    continue;
                }

                ModMetadata parsed = ParseVersionFile(versionFile, modFolder);
                if (parsed == null)
                {
                    continue;
                }

                if (!Cache.TryGetValue(modFolder, out ModMetadata existing)
                    || PreferMetadata(parsed, existing))
                {
                    Cache[modFolder] = parsed;
                }
            }

            EnrichAuthorsFromCkan();

            _built = true;
            EditorBootstrap.Log("Cached metadata for " + Cache.Count + " mod folders.");
        }

        public static IEnumerable<ModMetadata> GetAllMods()
        {
            if (!_built)
            {
                Build();
            }

            return Cache.Values;
        }

        public static void RegisterModAuthorTokens(string modFolder, IEnumerable<string> tokens)
        {
            if (string.IsNullOrEmpty(modFolder) || tokens == null)
            {
                return;
            }

            if (!ModAuthorTokens.TryGetValue(modFolder, out HashSet<string> existing))
            {
                existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                ModAuthorTokens[modFolder] = existing;
            }

            foreach (string token in tokens)
            {
                if (!string.IsNullOrWhiteSpace(token))
                {
                    existing.Add(token.Trim());
                }
            }
        }

        public static void CollectModAuthorTokens(string modFolder, ICollection<string> tokens)
        {
            if (string.IsNullOrEmpty(modFolder) || tokens == null)
            {
                return;
            }

            if (ModAuthorTokens.TryGetValue(modFolder, out HashSet<string> modTokens))
            {
                foreach (string token in modTokens)
                {
                    tokens.Add(token);
                }
            }
        }

        public static ModMetadata ResolveForPart(AvailablePart part)
        {
            if (!_built)
            {
                Build();
            }

            string modFolder = ExtractModFolderFromPart(part);
            if (string.IsNullOrEmpty(modFolder))
            {
                return ModMetadata.Unknown;
            }

            if (Cache.TryGetValue(modFolder, out ModMetadata metadata))
            {
                return metadata;
            }

            return ModMetadata.FromFolderName(modFolder);
        }

        private static void EnrichAuthorsFromCkan()
        {
            IReadOnlyDictionary<string, string> ckanAuthors = CkanAuthorRegistry.LoadAuthorsByModFolder();
            if (ckanAuthors.Count == 0)
            {
                return;
            }

            int enriched = 0;
            foreach (KeyValuePair<string, string> entry in ckanAuthors)
            {
                if (string.IsNullOrEmpty(entry.Key) || string.IsNullOrEmpty(entry.Value))
                {
                    continue;
                }

                if (Cache.TryGetValue(entry.Key, out ModMetadata existing))
                {
                    string mergedAuthor = MergeAuthorMetadata(existing.Author, entry.Value);
                    if (string.Equals(mergedAuthor, existing.Author, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Cache[entry.Key] = new ModMetadata(
                        existing.FolderName,
                        existing.DisplayName,
                        mergedAuthor);
                    enriched++;
                }
                else
                {
                    Cache[entry.Key] = new ModMetadata(entry.Key, entry.Key, entry.Value);
                    enriched++;
                }
            }

            if (enriched > 0)
            {
                EditorBootstrap.Log("Enriched " + enriched + " mod folders with CKAN authors.");
            }
        }

        private static string MergeAuthorMetadata(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left))
            {
                return Clean(right);
            }

            if (string.IsNullOrWhiteSpace(right))
            {
                return Clean(left);
            }

            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AuthorTokenizer.CollectTokens(left, tokens);
            AuthorTokenizer.CollectTokens(right, tokens);

            if (tokens.Count == 0)
            {
                return string.Empty;
            }

            var merged = new List<string>(tokens);
            merged.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", merged);
        }

        private static bool PreferMetadata(ModMetadata candidate, ModMetadata existing)
        {
            if (candidate == null)
            {
                return false;
            }

            if (existing == null)
            {
                return true;
            }

            bool candidateHasDisplay = !string.IsNullOrEmpty(candidate.DisplayName)
                && !string.Equals(candidate.DisplayName, candidate.FolderName, StringComparison.OrdinalIgnoreCase);
            bool existingHasDisplay = !string.IsNullOrEmpty(existing.DisplayName)
                && !string.Equals(existing.DisplayName, existing.FolderName, StringComparison.OrdinalIgnoreCase);

            if (candidateHasDisplay && !existingHasDisplay)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(candidate.Author) && string.IsNullOrEmpty(existing.Author))
            {
                return true;
            }

            return false;
        }

        private static string ResolveModFolderKey(string gameDataPath, string versionFilePath)
        {
            string relative = GetRelativeGameDataPath(gameDataPath, versionFilePath);
            if (string.IsNullOrEmpty(relative))
            {
                return string.Empty;
            }

            int slash = relative.IndexOf('/');
            if (slash < 0)
            {
                return relative;
            }

            return relative.Substring(0, slash);
        }

        private static ModMetadata ParseVersionFile(string versionFilePath, string modFolder)
        {
            string text;
            try
            {
                text = File.ReadAllText(versionFilePath);
            }
            catch (Exception ex)
            {
                EditorBootstrap.LogWarning("Failed to read version file '" + versionFilePath + "': " + ex.Message);
                return ModMetadata.FromFolderName(modFolder);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return ModMetadata.FromFolderName(modFolder);
            }

            string trimmed = text.TrimStart();
            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                return ParseJsonVersion(text, modFolder);
            }

            return ParseCfgVersion(text, modFolder);
        }

        private static ModMetadata ParseJsonVersion(string text, string modFolder)
        {
            string displayName = ExtractJsonString(text, "NAME");
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = ExtractJsonString(text, "name");
            }

            string author = ExtractJsonAuthor(text);

            return new ModMetadata(modFolder, displayName, author);
        }

        private static ModMetadata ParseCfgVersion(string text, string modFolder)
        {
            string displayName = string.Empty;
            string author = string.Empty;

            foreach (string rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim().Trim('"');
                if (value.Length == 0)
                {
                    continue;
                }

                if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase))
                {
                    displayName = value;
                }
                else if (string.Equals(key, "author", StringComparison.OrdinalIgnoreCase))
                {
                    author = value;
                }
            }

            return new ModMetadata(modFolder, displayName, author);
        }

        private static string ExtractJsonAuthor(string text)
        {
            string author = ExtractJsonString(text, "AUTHOR");
            if (string.IsNullOrEmpty(author))
            {
                author = ExtractJsonString(text, "author");
            }

            if (!string.IsNullOrEmpty(author))
            {
                return author;
            }

            Match arrayMatch = Regex.Match(
                text,
                "\"(?:AUTHOR|author)\"\\s*:\\s*\\[([^\\]]*)\\]",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!arrayMatch.Success)
            {
                return string.Empty;
            }

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

        private static string ExtractJsonString(string text, string key)
        {
            Match match = Regex.Match(
                text,
                "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"",
                RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        internal static string ExtractModFolderFromPart(AvailablePart part)
        {
            if (part == null)
            {
                return string.Empty;
            }

            string path = part.partPath;
            if (string.IsNullOrEmpty(path))
            {
                path = part.partUrl;
            }

            if (string.IsNullOrEmpty(path))
            {
                path = part.configFileFullName;
            }

            return ExtractModFolderFromPath(path);
        }

        internal static string ExtractModFolderFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/');
            const string gameDataToken = "/GameData/";
            int gameDataIndex = normalized.IndexOf(gameDataToken, StringComparison.OrdinalIgnoreCase);
            if (gameDataIndex >= 0)
            {
                normalized = normalized.Substring(gameDataIndex + gameDataToken.Length);
            }

            normalized = normalized.TrimStart('/');
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            int slash = normalized.IndexOf('/');
            if (slash < 0)
            {
                return normalized;
            }

            return normalized.Substring(0, slash);
        }

        private static string GetRelativeGameDataPath(string gameDataPath, string filePath)
        {
            string fullGameData = Path.GetFullPath(gameDataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullFile = Path.GetFullPath(filePath);
            string prefix = fullGameData + Path.DirectorySeparatorChar;

            if (!fullFile.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return fullFile.Substring(prefix.Length).Replace('\\', '/');
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Trim('"');
        }
    }

    internal sealed class ModMetadata
    {
        public static readonly ModMetadata Unknown = new ModMetadata(string.Empty, string.Empty, string.Empty);

        public string FolderName { get; }
        public string DisplayName { get; }
        public string Author { get; }

        public ModMetadata(string folderName, string displayName, string author)
        {
            FolderName = Clean(folderName);
            DisplayName = Clean(displayName);
            Author = Clean(author);

            if (DisplayName.Length == 0 && FolderName.Length > 0)
            {
                DisplayName = FolderName;
            }
        }

        public static ModMetadata FromFolderName(string folderName)
        {
            return new ModMetadata(folderName, folderName, string.Empty);
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Trim('"');
        }
    }
}
