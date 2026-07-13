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
        // Typing Match slices — always yield (never use EffectiveFrameSliceBatchSize, which
        // disables yields in EDITOR so index build cannot spill into the hangar).
        private const int MatchFrameSliceBatchSize = 280;

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
        // Structured resource names — Enter-eligible (same band as Module), not description prose.
        private const int ResourcePrefix = 6;
        private const int ResourceContains = 7;
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

        private readonly List<IndexedPart> _parts = new List<IndexedPart>();

        public int PartCount => _parts.Count;

        public void Build()
        {
            _parts.Clear();
            ModMetadataCache.Build();

            if (PartLoader.Instance == null || PartLoader.Instance.loadedParts == null)
            {
                EditorBootstrap.LogWarning("PartLoader not ready — suggestion index empty.");
                return;
            }

            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                _parts.Add(IndexPart(part));
            }

            EditorBootstrap.Log("Indexed " + _parts.Count + " editor-available parts.");
        }

        public IEnumerator BuildCoroutine()
        {
            _parts.Clear();
            ModMetadataCache.Build();

            if (PartLoader.Instance == null || PartLoader.Instance.loadedParts == null)
            {
                EditorBootstrap.LogWarning("PartLoader not ready — suggestion index empty.");
                yield break;
            }

            IReadOnlyList<AvailablePart> availableParts = EditorPartAvailability.GetAvailableParts();
            for (int i = 0; i < availableParts.Count; i++)
            {
                _parts.Add(IndexPart(availableParts[i]));
                if (i > 0 && i % GameLoadIndexService.EffectiveFrameSliceBatchSize(FrameSliceBatchSize) == 0)
                {
                    yield return null;
                }
            }

            EditorBootstrap.Log("Indexed " + _parts.Count + " editor-available parts.");
        }

        public IEnumerable<PartSuggestion> Match(string query, int maxResults)
        {
            var results = new List<PartSuggestion>(maxResults);
            MatchInto(query, maxResults, results);
            return results;
        }

        /// <summary>
        /// Sync fill of <paramref name="destination"/>. Used by click/focus; typing uses
        /// <see cref="MatchIncremental"/>.
        /// </summary>
        public void MatchInto(string query, int maxResults, List<PartSuggestion> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            string trimmed = (query ?? string.Empty).Trim();
            if (trimmed.Length == 0 || maxResults <= 0)
            {
                return;
            }

            string[] words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return;
            }

            bool titleFirst = trimmed.Length <= TitleFirstMaxQueryLength;
            var top = new List<ScoredPart>(maxResults);
            for (int i = 0; i < _parts.Count; i++)
            {
                ScoredPart scored = ScorePart(_parts[i], words, titleFirst, includeDescription: false);
                if (scored.Score < 0)
                {
                    continue;
                }

                InsertTopScored(top, scored, maxResults, titleFirst);
            }

            AppendScoredSuggestions(destination, top);
        }

        /// <summary>
        /// Frame-sliced Match for the typing path — yields so InputField never waits on a
        /// multi-thousand-part scan. Cancel via <paramref name="isCancelled"/>.
        /// </summary>
        public IEnumerator MatchIncremental(
            string query,
            int maxResults,
            List<PartSuggestion> destination,
            Func<bool> isCancelled)
        {
            if (destination == null)
            {
                yield break;
            }

            destination.Clear();
            string trimmed = (query ?? string.Empty).Trim();
            if (trimmed.Length == 0 || maxResults <= 0)
            {
                yield break;
            }

            string[] words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                yield break;
            }

            bool titleFirst = trimmed.Length <= TitleFirstMaxQueryLength;
            var top = new List<ScoredPart>(maxResults);
            // Fixed batch — do NOT use EffectiveFrameSliceBatchSize (that disables yields in EDITOR).
            const int batch = MatchFrameSliceBatchSize;

            for (int i = 0; i < _parts.Count; i++)
            {
                if (isCancelled != null && isCancelled())
                {
                    destination.Clear();
                    yield break;
                }

                ScoredPart scored = ScorePart(_parts[i], words, titleFirst, includeDescription: false);
                if (scored.Score >= 0)
                {
                    InsertTopScored(top, scored, maxResults, titleFirst);
                }

                if (i > 0 && (i % batch) == 0)
                {
                    yield return null;
                }
            }

            if (isCancelled != null && isCancelled())
            {
                destination.Clear();
                yield break;
            }

            AppendScoredSuggestions(destination, top);
        }

        private static void AppendScoredSuggestions(List<PartSuggestion> destination, List<ScoredPart> top)
        {
            for (int i = 0; i < top.Count; i++)
            {
                ScoredPart match = top[i];
                destination.Add(new PartSuggestion
                {
                    Kind = SuggestionKind.Part,
                    QueryText = match.Entry.Part.name,
                    DisplayText = match.Entry.DisplayText,
                    MatchReason = FormatMatchReason(match.BestField),
                    Part = match.Entry.Part,
                    IsHistory = false,
                    RankScore = 100 + match.Score,
                    IndexedPartCount = 1
                });
            }
        }

        private static void InsertTopScored(
            List<ScoredPart> top,
            ScoredPart scored,
            int maxResults,
            bool titleFirst)
        {
            if (top.Count == maxResults)
            {
                ScoredPart worst = top[top.Count - 1];
                if (CompareScored(scored, worst, titleFirst) >= 0)
                {
                    return;
                }

                top.RemoveAt(top.Count - 1);
            }

            int insertAt = top.Count;
            for (int i = 0; i < top.Count; i++)
            {
                if (CompareScored(scored, top[i], titleFirst) < 0)
                {
                    insertAt = i;
                    break;
                }
            }

            top.Insert(insertAt, scored);
        }

        private static int CompareScored(ScoredPart left, ScoredPart right, bool titleFirst)
        {
            int scoreCmp = left.Score.CompareTo(right.Score);
            if (scoreCmp != 0)
            {
                return scoreCmp;
            }

            int kindCmp = KindPriority(left.BestField, titleFirst)
                .CompareTo(KindPriority(right.BestField, titleFirst));
            if (kindCmp != 0)
            {
                return kindCmp;
            }

            return string.Compare(
                left.Entry.DisplayText,
                right.Entry.DisplayText,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Enter matches require strong evidence per query word: title/name/resource/tag/
        /// module-name (not module info prose), with whole-token or significant-field hits.
        /// Excludes description, auto-tag synonym spam, mod-author noise, and weak mid-string
        /// contains that flooded results (e.g. "antimatter" matching unrelated HTML/info).
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

                ScoredPart scored = ScorePart(
                    entry,
                    words,
                    titleFirst,
                    includeDescription: false,
                    enterStrict: true);
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

            if (scored.BestField == null)
            {
                return false;
            }

            // Enter evidence must sit on significant fields — not description / autotag / author.
            switch (scored.BestField.Kind)
            {
                case SearchFieldKind.Title:
                case SearchFieldKind.Name:
                case SearchFieldKind.Resource:
                case SearchFieldKind.Tag:
                case SearchFieldKind.Module:
                case SearchFieldKind.Category:
                case SearchFieldKind.Manufacturer:
                case SearchFieldKind.TechRequired:
                case SearchFieldKind.ModName:
                case SearchFieldKind.ModFolder:
                    return true;
                default:
                    return false;
            }
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
            AddStructuredResources(entry, part);
            // Free-form resourceInfo / moduleInfo HTML — treat as Description so Enter
            // cannot qualify on prose mentions (major source of irrelevant antimatter-style hits).
            AddField(entry, Clean(part.resourceInfo), SearchFieldKind.Description, DescriptionPrefix, DescriptionContains);
            AddField(entry, Clean(part.moduleInfo), SearchFieldKind.Description, DescriptionPrefix, DescriptionContains);
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
                    // Module info/primaryInfo prose → Description (dropdown may still use it;
                    // Enter excludes Description).
                    AddField(entry, Clean(moduleInfo.info), SearchFieldKind.Description, DescriptionPrefix, DescriptionContains);
                    AddField(entry, Clean(moduleInfo.primaryInfo), SearchFieldKind.Description, DescriptionPrefix, DescriptionContains);
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

        private static void AddStructuredResources(IndexedPart entry, AvailablePart part)
        {
            if (part == null)
            {
                return;
            }

            if (part.resourceInfos != null)
            {
                for (int i = 0; i < part.resourceInfos.Count; i++)
                {
                    AvailablePart.ResourceInfo resourceInfo = part.resourceInfos[i];
                    if (resourceInfo == null)
                    {
                        continue;
                    }

                    AddResourceField(entry, resourceInfo.resourceName);
                    AddResourceField(entry, resourceInfo.displayName);
                }
            }

            if (part.partPrefab == null || part.partPrefab.Resources == null)
            {
                return;
            }

            for (int i = 0; i < part.partPrefab.Resources.Count; i++)
            {
                PartResource resource = part.partPrefab.Resources[i];
                if (resource == null || resource.info == null)
                {
                    continue;
                }

                AddResourceField(entry, resource.info.name);
                AddResourceField(entry, resource.info.displayName);
            }
        }

        private static void AddResourceField(IndexedPart entry, string resourceName)
        {
            string cleaned = Clean(resourceName);
            if (cleaned.Length < 2)
            {
                return;
            }

            AddField(entry, cleaned, SearchFieldKind.Resource, ResourcePrefix, ResourceContains);
            AddField(
                entry,
                SplitCamelCase(cleaned),
                SearchFieldKind.Resource,
                ResourcePrefix,
                ResourceContains);
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

        private static ScoredPart ScorePart(
            IndexedPart entry,
            string[] words,
            bool titleFirst,
            bool includeDescription,
            bool enterStrict = false)
        {
            int aggregateScore = -1;
            SearchField bestField = null;

            // Best possible RankScore for this scoring mode — stop field scan once reached.
            int perfectScore = titleFirst ? TitlePrefix : TagWeightedTagPrefix;

            for (int w = 0; w < words.Length; w++)
            {
                string word = words[w];
                int wordBestScore = -1;
                SearchField wordBestField = null;

                for (int i = 0; i < entry.Fields.Count; i++)
                {
                    SearchField field = entry.Fields[i];
                    if (!includeDescription && field.Kind == SearchFieldKind.Description)
                    {
                        continue;
                    }

                    if (enterStrict && !IsEnterEligibleField(field))
                    {
                        continue;
                    }

                    // Dropdown: once we have a strong hit, skip synonym/author noise fields.
                    if (!enterStrict
                        && wordBestScore >= 0
                        && wordBestScore <= ModuleContains
                        && (field.Kind == SearchFieldKind.AutoTag
                            || field.Kind == SearchFieldKind.ModAuthor))
                    {
                        continue;
                    }

                    int score = enterStrict
                        ? ScoreFieldEnter(field, word, titleFirst)
                        : ScoreField(field, word, titleFirst);
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
                        if (wordBestScore == perfectScore)
                        {
                            break;
                        }
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

        /// <summary>
        /// Fields Enter may score. AutoTag / ModAuthor / Description are noise for filter lists.
        /// </summary>
        private static bool IsEnterEligibleField(SearchField field)
        {
            if (field == null)
            {
                return false;
            }

            switch (field.Kind)
            {
                case SearchFieldKind.Title:
                case SearchFieldKind.Name:
                case SearchFieldKind.Resource:
                case SearchFieldKind.Tag:
                case SearchFieldKind.Module:
                case SearchFieldKind.Category:
                case SearchFieldKind.Manufacturer:
                case SearchFieldKind.TechRequired:
                case SearchFieldKind.ModName:
                case SearchFieldKind.ModFolder:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Enter scoring: prefix always OK; contains only as whole-token / camelCase boundary
        /// (no weak mid-string hits inside unrelated compound strings).
        /// </summary>
        private static int ScoreFieldEnter(SearchField field, string word, bool titleFirst)
        {
            if (field == null || field.Text.Length < 2 || string.IsNullOrEmpty(word))
            {
                return -1;
            }

            ResolveFieldScores(field, titleFirst, out int prefixScore, out int containsScore);

            if (field.Text.StartsWith(word, StringComparison.OrdinalIgnoreCase))
            {
                return prefixScore;
            }

            if (word.Length >= 3 && FieldContainsSignificantToken(field.Text, word))
            {
                return containsScore;
            }

            return -1;
        }

        /// <summary>
        /// True when <paramref name="word"/> appears as a whole token, spaced token, or
        /// camelCase/PascalCase segment inside <paramref name="fieldText"/> (e.g. AntiMatter).
        /// Rejects tiny fragment embeds like "anti" inside unrelated longer words when word
        /// length is short — callers gate length; this enforces token boundaries.
        /// </summary>
        private static bool FieldContainsSignificantToken(string fieldText, string word)
        {
            if (string.IsNullOrEmpty(fieldText) || string.IsNullOrEmpty(word))
            {
                return false;
            }

            if (string.Equals(fieldText, word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Spaced / punctuated tokens (tags are usually space-split already, but titles aren't).
            int start = 0;
            while (start < fieldText.Length)
            {
                while (start < fieldText.Length && !IsTokenChar(fieldText[start]))
                {
                    start++;
                }

                if (start >= fieldText.Length)
                {
                    break;
                }

                int end = start + 1;
                while (end < fieldText.Length && IsTokenChar(fieldText[end]))
                {
                    end++;
                }

                int len = end - start;
                if (len == word.Length
                    && string.Compare(fieldText, start, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }

                // CamelCase segment inside this token (AntiMatter, antiMatterFuel).
                if (CamelCaseSegmentMatches(fieldText, start, end, word))
                {
                    return true;
                }

                start = end;
            }

            return false;
        }

        private static bool CamelCaseSegmentMatches(string text, int tokenStart, int tokenEnd, string word)
        {
            int segStart = tokenStart;
            while (segStart < tokenEnd)
            {
                int segEnd = segStart + 1;
                while (segEnd < tokenEnd)
                {
                    char c = text[segEnd];
                    char prev = text[segEnd - 1];
                    // Boundary: lower→Upper or letter→digit / digit→letter
                    bool boundary = (char.IsUpper(c) && char.IsLetter(prev) && !char.IsUpper(prev))
                        || (char.IsDigit(c) && char.IsLetter(prev))
                        || (char.IsLetter(c) && char.IsDigit(prev));
                    if (boundary)
                    {
                        break;
                    }

                    // Also split runs of UPPER then UpperLower (HTML → HTML / Parser-style):
                    if (char.IsUpper(c)
                        && char.IsUpper(prev)
                        && segEnd + 1 < tokenEnd
                        && char.IsLower(text[segEnd + 1]))
                    {
                        break;
                    }

                    segEnd++;
                }

                int segLen = segEnd - segStart;
                if (segLen == word.Length
                    && string.Compare(text, segStart, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }

                // Allow word to match concatenated camel segments from segStart (Anti + Matter).
                if (segLen < word.Length
                    && tokenEnd - segStart >= word.Length
                    && string.Compare(text, segStart, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0
                    && (segStart + word.Length >= tokenEnd
                        || IsCamelBoundary(text, segStart + word.Length, tokenEnd)))
                {
                    return true;
                }

                segStart = segEnd;
            }

            return false;
        }

        private static bool IsCamelBoundary(string text, int index, int tokenEnd)
        {
            if (index >= tokenEnd)
            {
                return true;
            }

            if (index <= 0)
            {
                return true;
            }

            char c = text[index];
            char prev = text[index - 1];
            return (char.IsUpper(c) && char.IsLetter(prev) && !char.IsUpper(prev))
                || (char.IsDigit(c) && char.IsLetter(prev))
                || (char.IsLetter(c) && char.IsDigit(prev));
        }

        private static bool IsTokenChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '-';
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
                    case SearchFieldKind.Resource:
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
                case SearchFieldKind.Resource:
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
                case SearchFieldKind.Resource:
                    return "resource:" + field.Text;
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
            Resource,
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
