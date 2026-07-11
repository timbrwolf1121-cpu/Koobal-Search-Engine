using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP.UI.Screens;

namespace PartSearchSuggest
{
    internal sealed class SuggestionIndex
    {
        internal const int FrameSliceBatchSize = 75;

        // Lower score = stronger match.
        // Short queries (length ≤ 2): title/name-first so "sp" surfaces Spark above synonym-tag noise.
        // Longer queries (length ≥ 3): tag-weighted / v0.7-style so tag/metadata hits rank above title.
        // Shared mid-band (category/module/mod/tech/desc) is identical in both modes; only
        // Title/Name/Tag/AutoTag swap. Indexed PrefixScore/ContainsScore use the title-first table.
        private const int TitlePrefix = 0;
        private const int TitleContains = 1;
        private const int NamePrefix = 2;
        private const int NameContains = 3;
        private const int CategoryPrefix = 4;
        private const int CategoryContains = 5;
        private const int ModulePrefix = 6;
        private const int ModuleContains = 7;
        private const int ModNamePrefix = 8;
        private const int ModNameContains = 9;
        private const int ModFolderPrefix = 8;
        private const int ModFolderContains = 9;
        private const int ManufacturerPrefix = 8;
        private const int ManufacturerContains = 9;
        private const int ModAuthorPrefix = 10;
        private const int ModAuthorContains = 11;
        private const int TechPrefix = 10;
        private const int TechContains = 11;
        private const int TagPrefix = 14;
        private const int TagContains = 15;
        private const int AutoTagPrefix = 16;
        private const int AutoTagContains = 17;
        private const int DescriptionPrefix = 22;
        private const int DescriptionContains = 23;

        // Tag-weighted overrides (v0.7 / v0.8.5.0) — applied at score time when query length ≥ 3.
        private const int TagWeightedTagPrefix = 0;
        private const int TagWeightedTagContains = 1;
        private const int TagWeightedAutoTagPrefix = 2;
        private const int TagWeightedAutoTagContains = 3;
        private const int TagWeightedTitlePrefix = 14;
        private const int TagWeightedTitleContains = 15;
        private const int TagWeightedNamePrefix = 16;
        private const int TagWeightedNameContains = 17;

        // Queries at or below this length use title/name-first part scoring (e.g. "sp" → Spark).
        private const int TitleFirstMaxQueryLength = 2;

        // Enter-submit uses tighter matching than dropdown suggestions (no description-only hits).
        // Allow title/name/metadata/tag fields; description scores sit above this ceiling.
        internal const int EnterSearchMaxAggregateScore = AutoTagContains;

        /// <summary>
        /// Text scores at or above this are "weak" (loose tag/auto/description). Weak-only
        /// result sets show fewer rows instead of flooding the dropdown with mediocre hits.
        /// </summary>
        internal const int WeakResultScoreThreshold = TagContains;

        private const int WeakResultMaxRows = 4;

        private readonly List<IndexedPart> _parts = new List<IndexedPart>();

        public int PartCount => _parts.Count;

        public void Build()
        {
            _parts.Clear();
            ModMetadataCache.Build();
            PartPopularityPriors.EnsureLoaded();

            if (PartLoader.Instance == null || PartLoader.Instance.loadedParts == null)
            {
                EditorBootstrap.LogWarning("PartLoader not ready — suggestion index empty.");
                return;
            }

            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                _parts.Add(IndexPart(part));
            }

            PartSuitePreference.BuildFromAvailableParts();
            EditorBootstrap.Log("Indexed " + _parts.Count + " editor-available parts.");
        }

        public IEnumerator BuildCoroutine()
        {
            _parts.Clear();
            ModMetadataCache.Build();
            PartPopularityPriors.EnsureLoaded();

            if (PartLoader.Instance == null || PartLoader.Instance.loadedParts == null)
            {
                EditorBootstrap.LogWarning("PartLoader not ready — suggestion index empty.");
                yield break;
            }

            IReadOnlyList<AvailablePart> availableParts = EditorPartAvailability.GetAvailableParts();
            for (int i = 0; i < availableParts.Count; i++)
            {
                _parts.Add(IndexPart(availableParts[i]));
                if (i > 0 && i % FrameSliceBatchSize == 0)
                {
                    yield return null;
                }
            }

            PartSuitePreference.BuildFromAvailableParts();
            EditorBootstrap.Log("Indexed " + _parts.Count + " editor-available parts.");
        }

        public IEnumerable<PartSuggestion> Match(string query, int maxResults)
        {
            string trimmed = (query ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                yield break;
            }

            string[] words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool titleFirst = trimmed.Length <= TitleFirstMaxQueryLength;

            // ScorePart already returns Score < 0 when any query word matches no field, so a
            // separate WordsMatch pre-filter would just re-scan every field redundantly.
            List<ScoredPart> ranked = _parts
                .Select(entry => ScorePart(entry, words, titleFirst))
                .Where(match => match.Score >= 0)
                .OrderBy(match => RankWithPriors(match))
                .ThenBy(match => KindPriority(match.BestField, titleFirst))
                .ThenBy(match => match.Entry.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int effectiveMax = maxResults;
            if (ranked.Count > 0 && ranked[0].Score >= WeakResultScoreThreshold)
            {
                // Weak-result UX: only weak tag/auto/desc hits — show fewer rows, not 10 mediocre.
                effectiveMax = Math.Min(maxResults, WeakResultMaxRows);
            }

            int count = 0;
            foreach (ScoredPart match in ranked)
            {
                yield return new PartSuggestion
                {
                    Kind = SuggestionKind.Part,
                    QueryText = match.Entry.Part.name,
                    DisplayText = match.Entry.DisplayText,
                    MatchReason = FormatPartSubtitle(match.Entry, match.BestField),
                    Part = match.Entry.Part,
                    IsHistory = false,
                    // Always sit below first-class categorizer/metadata rows (RankScore ~0–20).
                    RankScore = RankWithPriors(match)
                };

                count++;
                if (count >= effectiveMax)
                {
                    yield break;
                }
            }
        }

        private static int RankWithPriors(ScoredPart match)
        {
            bool strongTitleOrName = IsStrongTitleOrNameMatch(match);
            int popularity = PartPopularityPriors.SoftBoost(
                match.Entry.Part,
                match.Score,
                strongTitleOrName);
            int personal = PartPersonalPriors.SoftBoost(match.Entry.Part);
            int suite = PartSuitePreference.SoftBoost(match.Entry.Part);

            // Soft priors only — scale text score so one step (×10) always beats max soft boost (~7).
            // Strong title/name matches get no popularity prior (never outranked by craft presence).
            if (strongTitleOrName)
            {
                popularity = 0;
            }

            return 100 + (match.Score * 10) - popularity - personal - suite;
        }

        private static bool IsStrongTitleOrNameMatch(ScoredPart match)
        {
            return match.BestField != null
                && (match.BestField.Kind == SearchFieldKind.Title
                    || match.BestField.Kind == SearchFieldKind.Name);
        }

        /// <summary>
        /// Parts matching Enter-submit rules: all query words required, metadata/title/name/tag
        /// prefix-or-contains only — excludes description-only loose stock-style hits.
        /// </summary>
        public IEnumerable<AvailablePart> GetEnterQueryMatches(string query)
        {
            string trimmed = (query ?? string.Empty).Trim();
            if (trimmed.Length < SuggestionQueryGuards.MinSuggestionQueryLength)
            {
                yield break;
            }

            string[] words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                yield break;
            }

            bool titleFirst = trimmed.Length <= TitleFirstMaxQueryLength;

            foreach (IndexedPart entry in _parts)
            {
                if (!EditorPartAvailability.IsAvailableInEditor(entry.Part))
                {
                    continue;
                }

                ScoredPart scored = ScorePart(entry, words, titleFirst);
                if (!QualifiesForEnterSearch(scored))
                {
                    continue;
                }

                yield return entry.Part;
            }
        }

        public int CountEnterQueryMatches(string query)
        {
            int count = 0;
            foreach (AvailablePart _ in GetEnterQueryMatches(query))
            {
                count++;
            }

            return count;
        }

        private static bool QualifiesForEnterSearch(ScoredPart scored)
        {
            if (scored.Score < 0 || scored.Score > EnterSearchMaxAggregateScore)
            {
                return false;
            }

            if (scored.BestField == null || scored.BestField.Kind == SearchFieldKind.Description)
            {
                return false;
            }

            return true;
        }

        private static IndexedPart IndexPart(AvailablePart part)
        {
            IndexedPart entry = new IndexedPart
            {
                Part = part,
                DisplayText = GetDisplayText(part)
            };

            AddField(entry, Clean(part.title), SearchFieldKind.Title, TitlePrefix, TitleContains);
            AddField(entry, Clean(part.name), SearchFieldKind.Name, NamePrefix, NameContains);
            AddTags(entry, part.tags, SearchFieldKind.Tag, TagPrefix, TagContains);
            AddField(entry, Clean(part.description), SearchFieldKind.Description, DescriptionPrefix, DescriptionContains);
            AddField(entry, Clean(part.typeDescription), SearchFieldKind.Description, DescriptionPrefix, DescriptionContains);
            AddField(entry, Clean(part.manufacturer), SearchFieldKind.Manufacturer, ManufacturerPrefix, ManufacturerContains);
            AddField(entry, Clean(part.TechRequired), SearchFieldKind.TechRequired, TechPrefix, TechContains);
            AddField(entry, Clean(part.resourceInfo), SearchFieldKind.Module, ModulePrefix, ModuleContains);
            AddField(entry, Clean(part.moduleInfo), SearchFieldKind.Module, ModulePrefix, ModuleContains);
            AddTags(entry, part.bulkheadProfiles, SearchFieldKind.Tag, TagPrefix, TagContains);

            string category = part.category.ToString();
            AddField(entry, category, SearchFieldKind.Category, CategoryPrefix, CategoryContains);
            AddField(entry, SplitCamelCase(category), SearchFieldKind.Category, CategoryPrefix, CategoryContains);

            if (part.partPrefab != null && part.partPrefab.partInfo != null)
            {
                AddTags(entry, part.partPrefab.partInfo.tags, SearchFieldKind.Tag, TagPrefix, TagContains);
            }

            if (part.moduleInfos != null)
            {
                foreach (AvailablePart.ModuleInfo moduleInfo in part.moduleInfos)
                {
                    if (moduleInfo == null)
                    {
                        continue;
                    }

                    string moduleName = Clean(moduleInfo.moduleName);
                    AddField(entry, moduleName, SearchFieldKind.Module, ModulePrefix, ModuleContains);
                    AddField(entry, SplitCamelCase(moduleName), SearchFieldKind.Module, ModulePrefix, ModuleContains);
                    AddField(entry, Clean(moduleInfo.moduleDisplayName), SearchFieldKind.Module, ModulePrefix, ModuleContains);
                    AddField(entry, Clean(moduleInfo.info), SearchFieldKind.Module, ModulePrefix + 2, ModuleContains + 2);
                    AddField(entry, Clean(moduleInfo.primaryInfo), SearchFieldKind.Module, ModulePrefix + 2, ModuleContains + 2);
                }
            }

            IndexModMetadata(entry, part);

            string autoTags = BasePartCategorizer.GeneratePartAutoTags(part);
            AddTags(entry, autoTags, SearchFieldKind.AutoTag, AutoTagPrefix, AutoTagContains);

            return entry;
        }

        private static void IndexModMetadata(IndexedPart entry, AvailablePart part)
        {
            ModMetadata metadata = ModMetadataCache.ResolveForPart(part);
            if (metadata == null || string.IsNullOrEmpty(metadata.FolderName))
            {
                return;
            }

            AddField(entry, metadata.DisplayName, SearchFieldKind.ModName, ModNamePrefix, ModNameContains);
            AddField(entry, SplitCamelCase(metadata.DisplayName), SearchFieldKind.ModName, ModNamePrefix, ModNameContains);
            AddField(entry, metadata.FolderName, SearchFieldKind.ModFolder, ModFolderPrefix, ModFolderContains);
            AddField(entry, SplitCamelCase(metadata.FolderName), SearchFieldKind.ModFolder, ModFolderPrefix, ModFolderContains);

            foreach (string token in AuthorTokenizer.Tokenize(metadata.Author))
            {
                AddField(entry, token, SearchFieldKind.ModAuthor, ModAuthorPrefix, ModAuthorContains);
            }

            foreach (string token in AuthorTokenizer.Tokenize(part.author))
            {
                AddField(entry, token, SearchFieldKind.ModAuthor, ModAuthorPrefix, ModAuthorContains);
            }
        }

        private static void AddTags(
            IndexedPart entry,
            string tags,
            SearchFieldKind kind,
            int prefixScore,
            int containsScore)
        {
            if (string.IsNullOrEmpty(tags))
            {
                return;
            }

            foreach (string tag in tags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                AddField(entry, Clean(tag), kind, prefixScore, containsScore);
            }
        }

        private static void AddField(
            IndexedPart entry,
            string value,
            SearchFieldKind kind,
            int prefixScore,
            int containsScore)
        {
            string cleaned = Clean(value);
            if (cleaned.Length < 2)
            {
                return;
            }

            entry.Fields.Add(new SearchField
            {
                Text = cleaned,
                Kind = kind,
                PrefixScore = prefixScore,
                ContainsScore = containsScore,
                StrictPrefixMatch = kind == SearchFieldKind.ModAuthor
            });
        }

        private static ScoredPart ScorePart(IndexedPart entry, string[] words, bool titleFirst)
        {
            int aggregateScore = -1;
            SearchField bestField = null;

            for (int w = 0; w < words.Length; w++)
            {
                string word = words[w];
                int wordBestScore = -1;
                SearchField wordBestField = null;

                for (int i = 0; i < entry.Fields.Count; i++)
                {
                    SearchField field = entry.Fields[i];
                    int score = ScoreField(field, word, titleFirst);
                    if (score < 0)
                    {
                        continue;
                    }

                    if (wordBestScore < 0
                        || score < wordBestScore
                        || (score == wordBestScore
                            && KindPriority(field, titleFirst) < KindPriority(wordBestField, titleFirst)))
                    {
                        wordBestScore = score;
                        wordBestField = field;
                    }
                }

                if (wordBestScore < 0)
                {
                    return new ScoredPart { Entry = entry, Score = -1, BestField = null };
                }

                if (aggregateScore < 0
                    || wordBestScore > aggregateScore
                    || (wordBestScore == aggregateScore
                        && KindPriority(wordBestField, titleFirst) < KindPriority(bestField, titleFirst)))
                {
                    aggregateScore = wordBestScore;
                    bestField = wordBestField;
                }
            }

            return new ScoredPart
            {
                Entry = entry,
                Score = aggregateScore,
                BestField = bestField
            };
        }

        private static int ScoreField(SearchField field, string word, bool titleFirst)
        {
            if (field == null || field.Text.Length < 2 || string.IsNullOrEmpty(word))
            {
                return -1;
            }

            ResolveFieldScores(field, titleFirst, out int prefixScore, out int containsScore);

            if (field.StrictPrefixMatch)
            {
                int authorRank = AuthorMatchHelper.ScoreQueryAgainstAuthorName(field.Text, word);
                if (authorRank < 0)
                {
                    return -1;
                }

                return authorRank == AuthorMatchHelper.FullTokenPrefixRank
                    ? prefixScore
                    : containsScore;
            }

            if (field.Text.StartsWith(word, StringComparison.OrdinalIgnoreCase))
            {
                return prefixScore;
            }

            if (field.Text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return containsScore;
            }

            return -1;
        }

        /// <summary>
        /// Title-first uses indexed scores. Tag-weighted remaps only Title/Name/Tag/AutoTag;
        /// shared kinds keep their indexed scores (including Module info +2 penalty).
        /// </summary>
        private static void ResolveFieldScores(
            SearchField field,
            bool titleFirst,
            out int prefixScore,
            out int containsScore)
        {
            if (titleFirst)
            {
                prefixScore = field.PrefixScore;
                containsScore = field.ContainsScore;
                return;
            }

            switch (field.Kind)
            {
                case SearchFieldKind.Tag:
                    prefixScore = TagWeightedTagPrefix;
                    containsScore = TagWeightedTagContains;
                    return;
                case SearchFieldKind.AutoTag:
                    prefixScore = TagWeightedAutoTagPrefix;
                    containsScore = TagWeightedAutoTagContains;
                    return;
                case SearchFieldKind.Title:
                    prefixScore = TagWeightedTitlePrefix;
                    containsScore = TagWeightedTitleContains;
                    return;
                case SearchFieldKind.Name:
                    prefixScore = TagWeightedNamePrefix;
                    containsScore = TagWeightedNameContains;
                    return;
                default:
                    prefixScore = field.PrefixScore;
                    containsScore = field.ContainsScore;
                    return;
            }
        }

        private static int KindPriority(SearchField field, bool titleFirst)
        {
            if (field == null)
            {
                return 99;
            }

            if (titleFirst)
            {
                switch (field.Kind)
                {
                    case SearchFieldKind.Title:
                    case SearchFieldKind.Name:
                        return 0;
                    case SearchFieldKind.Category:
                    case SearchFieldKind.Module:
                    case SearchFieldKind.ModName:
                    case SearchFieldKind.ModFolder:
                    case SearchFieldKind.Manufacturer:
                    case SearchFieldKind.TechRequired:
                        return 1;
                    case SearchFieldKind.ModAuthor:
                        return 2;
                    case SearchFieldKind.Tag:
                    case SearchFieldKind.AutoTag:
                        return 3;
                    case SearchFieldKind.Description:
                        return 4;
                    default:
                        return 5;
                }
            }

            // Tag-weighted / v0.7 tie-break: metadata and tags before title/name.
            switch (field.Kind)
            {
                case SearchFieldKind.Tag:
                case SearchFieldKind.AutoTag:
                case SearchFieldKind.Category:
                case SearchFieldKind.Module:
                case SearchFieldKind.ModName:
                case SearchFieldKind.ModFolder:
                case SearchFieldKind.Manufacturer:
                case SearchFieldKind.TechRequired:
                    return 0;
                case SearchFieldKind.ModAuthor:
                    return 1;
                case SearchFieldKind.Title:
                case SearchFieldKind.Name:
                    return 2;
                case SearchFieldKind.Description:
                    return 3;
                default:
                    return 4;
            }
        }

        private static string FormatMatchReason(SearchField field)
        {
            if (field == null || string.IsNullOrEmpty(field.Text))
            {
                return string.Empty;
            }

            switch (field.Kind)
            {
                case SearchFieldKind.Title:
                    return "title";
                case SearchFieldKind.Name:
                    return "name";
                case SearchFieldKind.Tag:
                    return "tag:" + field.Text;
                case SearchFieldKind.Category:
                    return "category:" + field.Text;
                case SearchFieldKind.Manufacturer:
                    return "manufacturer:" + field.Text;
                case SearchFieldKind.Module:
                    return "module:" + field.Text;
                case SearchFieldKind.TechRequired:
                    return "tech:" + field.Text;
                case SearchFieldKind.ModName:
                    return "mod:" + field.Text;
                case SearchFieldKind.ModFolder:
                    return "modfolder:" + field.Text;
                case SearchFieldKind.ModAuthor:
                    return "author:" + field.Text;
                case SearchFieldKind.Description:
                    return "description";
                case SearchFieldKind.AutoTag:
                    return "auto:" + field.Text;
                default:
                    return field.Text;
            }
        }

        /// <summary>
        /// Disambiguation subtitle: manufacturer / size / variant so Z-100 vs Z-400 is clear.
        /// Keeps a compact match-kind prefix for dedup redundancy checks (title/tag:/…).
        /// </summary>
        private static string FormatPartSubtitle(IndexedPart entry, SearchField bestField)
        {
            string matchKind = FormatMatchReason(bestField);
            if (entry == null || entry.Part == null)
            {
                return matchKind;
            }

            AvailablePart part = entry.Part;
            var bits = new List<string>(4);

            string manufacturer = Clean(part.manufacturer);
            if (manufacturer.Length > 0)
            {
                bits.Add(ShortenToken(manufacturer, 28));
            }

            string size = FirstBulkheadToken(part.bulkheadProfiles);
            if (size.Length > 0)
            {
                bits.Add(size);
            }

            string name = Clean(part.name);
            if (name.Length > 0)
            {
                // Variant / internal id — helps Restock duplicates and Z-100 vs Z-400 families.
                bits.Add(ShortenToken(name, 36));
            }

            if (bits.Count == 0)
            {
                return matchKind;
            }

            string detail = string.Join(" · ", bits.ToArray());
            if (string.IsNullOrEmpty(matchKind)
                || string.Equals(matchKind, "title", StringComparison.OrdinalIgnoreCase)
                || string.Equals(matchKind, "name", StringComparison.OrdinalIgnoreCase))
            {
                return detail;
            }

            // Preserve kind:prefix for EditorSearchHook redundancy checks (tag:/manufacturer:/…).
            if (matchKind.IndexOf(':') >= 0)
            {
                return matchKind + " · " + detail;
            }

            return matchKind + " · " + detail;
        }

        private static string FirstBulkheadToken(string profiles)
        {
            if (string.IsNullOrWhiteSpace(profiles))
            {
                return string.Empty;
            }

            string[] tokens = profiles.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return string.Empty;
            }

            return Clean(tokens[0]);
        }

        private static string ShortenToken(string value, int maxLen)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLen)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxLen - 1) + "…";
        }

        private static string GetDisplayText(AvailablePart part)
        {
            string title = Clean(part.title);
            if (title.Length > 0)
            {
                return title;
            }

            return Clean(part.name);
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().Trim('"');
        }

        private static string SplitCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length + 4);
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

        private enum SearchFieldKind
        {
            Title,
            Name,
            Tag,
            Description,
            Category,
            Manufacturer,
            Module,
            AutoTag,
            TechRequired,
            ModName,
            ModFolder,
            ModAuthor
        }

        private sealed class IndexedPart
        {
            public AvailablePart Part;
            public string DisplayText;
            public readonly List<SearchField> Fields = new List<SearchField>();
        }

        private sealed class SearchField
        {
            public string Text;
            public SearchFieldKind Kind;
            public int PrefixScore;
            public int ContainsScore;
            public bool StrictPrefixMatch;
        }

        private struct ScoredPart
        {
            public IndexedPart Entry;
            public int Score;
            public SearchField BestField;
        }
    }
}
