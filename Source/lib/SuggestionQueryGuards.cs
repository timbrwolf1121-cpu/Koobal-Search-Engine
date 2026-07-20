namespace PartSearchSuggest
{
    /// <summary>
    /// Guards against overly broad single-character metadata/filter suggestions.
    /// </summary>
    internal static class SuggestionQueryGuards
    {
        internal const int MinSuggestionQueryLength = 2;

        internal const float MaxBroadMatchFraction = 0.90f;

        internal static bool IsTooShortForBroadSuggestions(string query)
        {
            return (query ?? string.Empty).Trim().Length < MinSuggestionQueryLength;
        }

        internal static bool IsSingleCharacter(string value)
        {
            string trimmed = (value ?? string.Empty).Trim();
            return trimmed.Length == 1;
        }

        internal static bool IsOverlyBroad(int partCount)
        {
            int editorPartCount = EditorPartAvailability.CountLoadedEditorParts();
            if (partCount <= 0 || editorPartCount <= 0)
            {
                return false;
            }

            return (float)partCount / editorPartCount > MaxBroadMatchFraction;
        }

        internal static bool ShouldSuppressBroadSuggestion(string query, string filterKey, int partCount)
        {
            if (IsTooShortForBroadSuggestions(query) || IsSingleCharacter(filterKey))
            {
                return true;
            }

            return IsOverlyBroad(partCount);
        }
    }
}
