using System;
using System.Collections.Generic;
using System.Text;

namespace PartSearchSuggest
{
    internal static class AuthorMatchHelper
    {
        // Lower score = stronger match.
        public const int FullTokenPrefixRank = 0;
        public const int WordPrefixRank = 1;
        public const int NoMatch = -1;

        public static bool QueryMatchesAuthorName(string authorName, string query)
        {
            return ScoreQueryAgainstAuthorName(authorName, query) >= 0;
        }

        public static int ScoreQueryAgainstAuthorName(string authorName, string query)
        {
            string cleanedAuthor = Clean(authorName);
            string cleanedQuery = Clean(query);

            if (cleanedAuthor.Length < 2 || cleanedQuery.Length < 2)
            {
                return NoMatch;
            }

            int best = NoMatch;
            foreach (string form in AuthorTokenizer.GetSearchableForms(cleanedAuthor))
            {
                int score = ScoreQueryAgainstForm(form, cleanedQuery);
                if (score >= 0 && (best < 0 || score < best))
                {
                    best = score;
                }
            }

            return best;
        }

        public static int ScoreQueryAgainstWords(string authorName, string[] words)
        {
            if (words == null || words.Length == 0)
            {
                return NoMatch;
            }

            int worstRank = NoMatch;
            for (int i = 0; i < words.Length; i++)
            {
                int wordRank = ScoreQueryAgainstAuthorName(authorName, words[i]);
                if (wordRank < 0)
                {
                    return NoMatch;
                }

                if (wordRank > worstRank)
                {
                    worstRank = wordRank;
                }
            }

            return worstRank;
        }

        private static int ScoreQueryAgainstForm(string form, string query)
        {
            if (form.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return FullTokenPrefixRank;
            }

            foreach (string word in SplitIntoWords(form))
            {
                if (word.Length >= 2 && word.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    return WordPrefixRank;
                }
            }

            return NoMatch;
        }

        internal static IEnumerable<string> SplitIntoWords(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (char.IsWhiteSpace(c) || c == '_' || c == '-' || c == '.')
                {
                    foreach (string word in FlushWord(builder))
                    {
                        yield return word;
                    }

                    continue;
                }

                if (i > 0 && char.IsUpper(c) && char.IsLower(value[i - 1]))
                {
                    foreach (string word in FlushWord(builder))
                    {
                        yield return word;
                    }
                }

                builder.Append(c);
            }

            foreach (string word in FlushWord(builder))
            {
                yield return word;
            }
        }

        private static IEnumerable<string> FlushWord(StringBuilder builder)
        {
            if (builder.Length == 0)
            {
                yield break;
            }

            string word = builder.ToString().Trim();
            builder.Clear();

            if (word.Length >= 2)
            {
                yield return word;
            }
        }

        public static string FormatDisplayName(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            string cleaned = Clean(token);
            if (cleaned.Length == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(cleaned.Length + 4);
            for (int i = 0; i < cleaned.Length; i++)
            {
                char c = cleaned[i];
                if (i > 0
                    && char.IsUpper(c)
                    && (char.IsLower(cleaned[i - 1]) || char.IsDigit(cleaned[i - 1])))
                {
                    builder.Append(' ');
                }

                builder.Append(c);
            }

            return builder.ToString().Trim();
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Trim('"', '\'', ' ');
        }
    }
}
