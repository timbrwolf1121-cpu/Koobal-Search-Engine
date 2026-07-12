using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace PartSearchSuggest
{
    internal static class AuthorTokenizer
    {
        private static readonly Regex AndPattern = new Regex(
            @"\s+and\s+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly char[] Separators = { ',', ';', '&', '/' };

        public static IEnumerable<string> Tokenize(string authorString)
        {
            if (string.IsNullOrWhiteSpace(authorString))
            {
                yield break;
            }

            string normalized = AndPattern.Replace(authorString.Trim(), ",");
            normalized = normalized
                .Replace('(', ',')
                .Replace(')', ',')
                .Replace('[', ',')
                .Replace(']', ',');

            foreach (string segment in normalized.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = Clean(segment);
                if (token.Length >= 2)
                {
                    yield return token;
                }
            }
        }

        public static IEnumerable<string> GetSearchableForms(string token)
        {
            string cleaned = Clean(token);
            if (cleaned.Length < 2)
            {
                yield break;
            }

            yield return cleaned;

            bool emittedWord = false;
            foreach (string word in AuthorMatchHelper.SplitIntoWords(cleaned))
            {
                if (!string.Equals(word, cleaned, StringComparison.OrdinalIgnoreCase))
                {
                    emittedWord = true;
                    yield return word;
                }
            }

            if (!emittedWord && cleaned.IndexOf(' ') >= 0)
            {
                foreach (string word in cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = Clean(word);
                    if (trimmed.Length >= 2
                        && !string.Equals(trimmed, cleaned, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return trimmed;
                    }
                }
            }
        }

        public static void CollectTokens(string authorString, ICollection<string> tokens)
        {
            if (tokens == null)
            {
                return;
            }

            foreach (string token in Tokenize(authorString))
            {
                tokens.Add(token);
            }
        }

        public static bool PartHasAuthor(AvailablePart part, string authorFilter)
        {
            return AuthorAttribution.PartMatchesAuthorFilter(part, authorFilter);
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Trim('"', '\'', ' ');
        }
    }
}
