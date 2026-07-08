using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PartSearchSuggest
{
    internal sealed class MetadataSuggestionIndex
    {
        internal const int FrameSliceBatchSize = 100;

        // Lower score = stronger match.
        private const int AuthorPrefixRank = 0;
        private const int AuthorWordPrefixRank = 1;
        private const int ModNameExactRank = 2;
        private const int ModNamePrefixRank = 3;
        private const int ModSuiteRank = 4;
        private const int ModViaAuthorPrefixRank = 5;
        private const int ModNameContainsRank = 6;

        private readonly List<AuthorEntry> _authors = new List<AuthorEntry>();
        private readonly List<ModEntry> _mods = new List<ModEntry>();

        public int AuthorCount => _authors.Count;
        public int ModCount => _mods.Count;

        public void Build()
        {
            _authors.Clear();
            _mods.Clear();

            ModMetadataCache.Build();

            if (PartLoader.Instance == null || PartLoader.Instance.loadedParts == null)
            {
                EditorBootstrap.LogWarning("PartLoader not ready — metadata suggestion index empty.");
                return;
            }

            BuildFromAvailableParts();

            EditorBootstrap.Log(
                "Indexed "
                + _authors.Count
                + " unique author tokens and "
                + _mods.Count
                + " mods for metadata suggestions.");
        }

        public IEnumerator BuildCoroutine()
        {
            _authors.Clear();
            _mods.Clear();

            ModMetadataCache.Build();

            if (PartLoader.Instance == null || PartLoader.Instance.loadedParts == null)
            {
                EditorBootstrap.LogWarning("PartLoader not ready — metadata suggestion index empty.");
                yield break;
            }

            IReadOnlyList<AvailablePart> availableParts = EditorPartAvailability.GetAvailableParts();
            yield return BuildFromAvailablePartsCoroutine(availableParts);

            EditorBootstrap.Log(
                "Indexed "
                + _authors.Count
                + " unique author tokens and "
                + _mods.Count
                + " mods for metadata suggestions.");
        }

        private void BuildFromAvailableParts()
        {
            var authorPartCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var modEntries = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
            var allAuthorTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            SeedModEntriesFromCache(modEntries, allAuthorTokens);

            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                AccumulatePartMetadata(part, modEntries, allAuthorTokens, authorPartCounts);
            }

            FinalizeMetadataIndex(modEntries, authorPartCounts, allAuthorTokens);
        }

        private IEnumerator BuildFromAvailablePartsCoroutine(IReadOnlyList<AvailablePart> availableParts)
        {
            var authorPartCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var modEntries = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
            var allAuthorTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            SeedModEntriesFromCache(modEntries, allAuthorTokens);
            yield return null;

            for (int i = 0; i < availableParts.Count; i++)
            {
                AccumulatePartMetadata(availableParts[i], modEntries, allAuthorTokens, authorPartCounts);
                if (i > 0 && i % FrameSliceBatchSize == 0)
                {
                    yield return null;
                }
            }

            FinalizeMetadataIndex(modEntries, authorPartCounts, allAuthorTokens);
        }

        private static void SeedModEntriesFromCache(
            Dictionary<string, ModEntry> modEntries,
            HashSet<string> allAuthorTokens)
        {
            foreach (ModMetadata metadata in ModMetadataCache.GetAllMods())
            {
                if (string.IsNullOrEmpty(metadata.FolderName))
                {
                    continue;
                }

                if (!modEntries.TryGetValue(metadata.FolderName, out ModEntry cachedMod))
                {
                    cachedMod = new ModEntry
                    {
                        FolderName = metadata.FolderName,
                        DisplayName = metadata.DisplayName,
                        AuthorTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    };
                    modEntries[metadata.FolderName] = cachedMod;
                }

                AuthorTokenizer.CollectTokens(metadata.Author, cachedMod.AuthorTokens);
                foreach (string token in cachedMod.AuthorTokens)
                {
                    allAuthorTokens.Add(token);
                }

                if (!string.IsNullOrEmpty(metadata.DisplayName)
                    && !string.Equals(metadata.DisplayName, metadata.FolderName, StringComparison.OrdinalIgnoreCase))
                {
                    cachedMod.DisplayName = metadata.DisplayName;
                }
            }
        }

        private static void AccumulatePartMetadata(
            AvailablePart part,
            Dictionary<string, ModEntry> modEntries,
            HashSet<string> allAuthorTokens,
            Dictionary<string, int> authorPartCounts)
        {
            if (part == null)
            {
                return;
            }

            ModMetadata metadata = ModMetadataCache.ResolveForPart(part);
            if (!string.IsNullOrEmpty(metadata.FolderName))
            {
                if (!modEntries.TryGetValue(metadata.FolderName, out ModEntry modEntry))
                {
                    modEntry = new ModEntry
                    {
                        FolderName = metadata.FolderName,
                        DisplayName = metadata.DisplayName,
                        AuthorTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    };
                    modEntries[metadata.FolderName] = modEntry;
                }

                modEntry.PartCount++;

                if (!string.IsNullOrEmpty(metadata.DisplayName)
                    && !string.Equals(metadata.DisplayName, metadata.FolderName, StringComparison.OrdinalIgnoreCase))
                {
                    modEntry.DisplayName = metadata.DisplayName;
                }

                AuthorTokenizer.CollectTokens(metadata.Author, modEntry.AuthorTokens);
                AuthorTokenizer.CollectTokens(part.author, modEntry.AuthorTokens);
                ModMetadataCache.RegisterModAuthorTokens(metadata.FolderName, modEntry.AuthorTokens);
            }

            var partAuthorTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AuthorAttribution.CollectPartAuthorTokens(part, partAuthorTokens);
            foreach (string token in partAuthorTokens)
            {
                allAuthorTokens.Add(token);
            }

            var canonicalAuthors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in partAuthorTokens)
            {
                canonicalAuthors.Add(AuthorCanonicalizer.GetCanonicalKey(token));
            }

            foreach (string canonical in canonicalAuthors)
            {
                if (canonical.Length < 2)
                {
                    continue;
                }

                if (authorPartCounts.TryGetValue(canonical, out int existing))
                {
                    authorPartCounts[canonical] = existing + 1;
                }
                else
                {
                    authorPartCounts[canonical] = 1;
                }
            }
        }

        private void FinalizeMetadataIndex(
            Dictionary<string, ModEntry> modEntries,
            Dictionary<string, int> authorPartCounts,
            HashSet<string> allAuthorTokens)
        {
            AuthorCanonicalizer.Build(allAuthorTokens);

            _mods.AddRange(modEntries.Values);
            foreach (ModEntry mod in _mods)
            {
                mod.PartCount = ModFilterMatcher.CountPartsMatchingModFolder(mod.FolderName);
            }

            _mods.RemoveAll(mod => mod.PartCount <= 0);

            _authors.AddRange(
                authorPartCounts
                    .Select(pair => new AuthorEntry
                    {
                        Name = pair.Key,
                        PartCount = AuthorAttribution.CountPartsMatchingAuthorFilter(pair.Key)
                    })
                    .Where(entry => entry.PartCount > 0)
                    .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase));
        }

        public IEnumerable<PartSuggestion> Match(string query, int maxResults)
        {
            string trimmed = (query ?? string.Empty).Trim();
            if (trimmed.Length == 0 || maxResults <= 0)
            {
                yield break;
            }

            bool allowBroadMetadata = !SuggestionQueryGuards.IsTooShortForBroadSuggestions(trimmed);

            string[] words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                yield break;
            }

            var results = new List<PartSuggestion>();
            var matchedAuthors = new List<(AuthorEntry Author, int Rank)>();
            var addedModFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (AuthorEntry author in _authors)
            {
                if (!allowBroadMetadata)
                {
                    continue;
                }

                int rank = ScoreAuthorMatch(author.Name, words);
                if (rank < 0)
                {
                    continue;
                }

                matchedAuthors.Add((author, rank));
                results.Add(new PartSuggestion
                {
                    Kind = SuggestionKind.ModAuthor,
                    QueryText = author.Name,
                    DisplayText = AuthorMatchHelper.FormatDisplayName(author.Name),
                    FilterKey = author.Name,
                    MatchReason = "author · " + author.PartCount + " parts",
                    RankScore = rank,
                    Part = null,
                    IsHistory = false
                });
            }

            LogAuthorMatches(trimmed, matchedAuthors);

            List<ModEntry> matchingMods = _mods
                .Where(mod => mod.PartCount > 0 && ModMatchesWords(mod, words))
                .ToList();

            if (allowBroadMetadata)
            {
                if (matchingMods.Count == 1)
                {
                    ModEntry mod = matchingMods[0];
                    PartSuggestion modSuggestion = CreateModNameSuggestion(mod, ModNameExactRank, trimmed);
                    if (modSuggestion != null)
                    {
                        addedModFolders.Add(mod.FolderName);
                        results.Add(modSuggestion);
                    }
                }
                else if (matchingMods.Count > 1)
                {
                    List<ModEntry> exactDisplayMatches = matchingMods
                        .Where(mod => DisplayNameMatchesAllWords(mod.DisplayName, words))
                        .ToList();

                    if (words.Length >= 3 && exactDisplayMatches.Count == 1)
                    {
                        ModEntry mod = exactDisplayMatches[0];
                        PartSuggestion modSuggestion = CreateModNameSuggestion(mod, ModNameExactRank, trimmed);
                        if (modSuggestion != null)
                        {
                            addedModFolders.Add(mod.FolderName);
                            results.Add(modSuggestion);
                        }
                    }
                    else
                    {
                        int suitePartCount = ModFilterMatcher.CountPartsMatchingModSuite(trimmed);
                        if (suitePartCount > 0
                            && !SuggestionQueryGuards.ShouldSuppressBroadSuggestion(trimmed, trimmed, suitePartCount))
                        {
                            results.Add(new PartSuggestion
                            {
                                Kind = SuggestionKind.ModSuite,
                                QueryText = trimmed,
                                DisplayText = BuildSuiteLabel(words),
                                FilterKey = trimmed,
                                MatchReason = "suite · " + matchingMods.Count + " mods · " + suitePartCount + " parts",
                                RankScore = ModSuiteRank,
                                Part = null,
                                IsHistory = false
                            });
                        }

                        foreach (ModEntry mod in matchingMods
                            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                            .Take(Math.Min(5, maxResults)))
                        {
                            PartSuggestion modSuggestion = CreateModNameSuggestion(mod, ScoreModNameMatch(mod, words), trimmed);
                            if (modSuggestion == null)
                            {
                                continue;
                            }

                            addedModFolders.Add(mod.FolderName);
                            results.Add(modSuggestion);
                        }
                    }
                }
            }

            if (allowBroadMetadata)
            {
                foreach ((AuthorEntry author, int rank) in matchedAuthors)
                {
                    IReadOnlyCollection<string> aliasGroup = AuthorCanonicalizer.GetAliasGroup(author.Name);
                    foreach (ModEntry mod in _mods)
                    {
                        if (mod.PartCount <= 0 || !ModHasAuthorToken(mod, aliasGroup))
                        {
                            continue;
                        }

                        if (addedModFolders.Contains(mod.FolderName))
                        {
                            continue;
                        }

                        int modRank = rank <= AuthorWordPrefixRank
                            ? ModViaAuthorPrefixRank
                            : ModNameContainsRank;

                        PartSuggestion modSuggestion = CreateModNameSuggestion(mod, modRank, trimmed);
                        if (modSuggestion == null)
                        {
                            continue;
                        }

                        results.Add(modSuggestion);
                        addedModFolders.Add(mod.FolderName);
                    }
                }
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
        }

        private static void LogAuthorMatches(string query, List<(AuthorEntry Author, int Rank)> matchedAuthors)
        {
            if (!DebugSettings.Verbose || query.Length < 3)
            {
                return;
            }

            if (matchedAuthors.Count == 0)
            {
                EditorBootstrap.Log("Author match '" + query + "': none");
                return;
            }

            var details = new StringBuilder();
            for (int i = 0; i < matchedAuthors.Count; i++)
            {
                if (i > 0)
                {
                    details.Append(", ");
                }

                (AuthorEntry author, int rank) = matchedAuthors[i];
                details.Append(author.Name);
                details.Append(" (");
                details.Append(author.PartCount);
                details.Append(" parts, rank ");
                details.Append(rank);
                details.Append(')');
            }

            EditorBootstrap.Log("Author match '" + query + "': " + details);
        }

        private static PartSuggestion CreateModNameSuggestion(ModEntry mod, int rankScore, string query)
        {
            if (mod == null || mod.PartCount <= 0)
            {
                return null;
            }

            // PartCount was resolved via ModFilterMatcher.CountPartsMatchingModFolder during
            // FinalizeMetadataIndex; reuse it instead of re-scanning every part per keystroke.
            int partCount = mod.PartCount;
            if (partCount <= 0)
            {
                return null;
            }

            if (SuggestionQueryGuards.ShouldSuppressBroadSuggestion(query, mod.FolderName, partCount))
            {
                return null;
            }

            if (rankScore < 0)
            {
                rankScore = ModNameContainsRank;
            }

            return new PartSuggestion
            {
                Kind = SuggestionKind.ModName,
                QueryText = mod.DisplayName,
                DisplayText = mod.DisplayName,
                FilterKey = mod.FolderName,
                MatchReason = "mod · " + partCount + " parts",
                RankScore = rankScore,
                Part = null,
                IsHistory = false
            };
        }

        private static bool ModMatchesWords(ModEntry mod, string[] words)
        {
            string searchText = BuildModSearchText(mod);
            for (int i = 0; i < words.Length; i++)
            {
                if (!TextContainsWord(searchText, words[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool DisplayNameMatchesAllWords(string displayName, string[] words)
        {
            string cleaned = Clean(displayName);
            if (cleaned.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < words.Length; i++)
            {
                if (cleaned.IndexOf(words[i], StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static int ScoreModNameMatch(ModEntry mod, string[] words)
        {
            int displayScore = ScoreTextMatch(mod.DisplayName, words);
            int folderScore = ScoreTextMatch(SplitCamelCase(mod.FolderName), words);
            int rawFolderScore = ScoreTextMatch(mod.FolderName, words);

            int best = displayScore;
            if (folderScore >= 0 && (best < 0 || folderScore < best))
            {
                best = folderScore;
            }

            if (rawFolderScore >= 0 && (best < 0 || rawFolderScore < best))
            {
                best = rawFolderScore;
            }

            return best;
        }

        private static int ScoreAuthorMatch(string authorName, string[] words)
        {
            int helperRank = AuthorMatchHelper.ScoreQueryAgainstWords(authorName, words);
            if (helperRank < 0)
            {
                IReadOnlyCollection<string> aliases = AuthorCanonicalizer.GetAliasGroup(authorName);
                foreach (string alias in aliases)
                {
                    if (string.Equals(alias, authorName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    helperRank = AuthorMatchHelper.ScoreQueryAgainstWords(alias, words);
                    if (helperRank >= 0)
                    {
                        break;
                    }
                }
            }

            if (helperRank < 0)
            {
                return -1;
            }

            return helperRank == AuthorMatchHelper.FullTokenPrefixRank
                ? AuthorPrefixRank
                : AuthorWordPrefixRank;
        }

        private static bool ModHasAuthorToken(ModEntry mod, IReadOnlyCollection<string> aliasGroup)
        {
            if (mod?.AuthorTokens == null || aliasGroup == null || aliasGroup.Count == 0)
            {
                return false;
            }

            foreach (string modToken in mod.AuthorTokens)
            {
                if (AliasGroupContains(aliasGroup, modToken))
                {
                    return true;
                }

                if (AliasGroupContains(aliasGroup, AuthorCanonicalizer.GetCanonicalKey(modToken)))
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

        private static int ScoreTextMatch(string value, string[] words)
        {
            string cleaned = Clean(value);
            if (cleaned.Length < 2)
            {
                return -1;
            }

            int worstRank = -1;
            for (int i = 0; i < words.Length; i++)
            {
                int wordRank = ScoreSingleWord(cleaned, words[i]);
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

        private static int ScoreSingleWord(string value, string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return -1;
            }

            if (value.StartsWith(word, StringComparison.OrdinalIgnoreCase))
            {
                return ModNamePrefixRank;
            }

            if (value.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ModNameContainsRank;
            }

            return -1;
        }

        private static bool TextContainsWord(string haystack, string word)
        {
            return !string.IsNullOrEmpty(haystack)
                && !string.IsNullOrEmpty(word)
                && haystack.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildModSearchText(ModEntry mod)
        {
            return Clean(mod.DisplayName)
                + " "
                + Clean(mod.FolderName)
                + " "
                + SplitCamelCase(mod.FolderName);
        }

        private static string BuildSuiteLabel(string[] words)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < words.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                string word = words[i];
                if (word.Length == 0)
                {
                    continue;
                }

                builder.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                {
                    builder.Append(word.Substring(1));
                }
            }

            return builder.ToString();
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Trim('"');
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

        private sealed class AuthorEntry
        {
            public string Name;
            public int PartCount;
        }

        private sealed class ModEntry
        {
            public string FolderName;
            public string DisplayName;
            public int PartCount;
            public HashSet<string> AuthorTokens;
        }
    }
}
