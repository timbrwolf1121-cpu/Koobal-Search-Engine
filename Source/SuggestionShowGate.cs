using System;

namespace PartSearchSuggest
{
    /// <summary>
    /// Gates suggestion Show vs history hover/dismiss. Pure logic so dismiss/hover
    /// races can be unit-tested without Unity.
    /// </summary>
    internal sealed class SuggestionShowGate
    {
        public bool DismissedUntilSearchPointer { get; private set; }

        public string LastShownQuery { get; private set; }

        public void MarkDismissed()
        {
            DismissedUntilSearchPointer = true;
        }

        public void MarkSearchFieldPointer()
        {
            DismissedUntilSearchPointer = false;
        }

        public void NoteShown(string query)
        {
            LastShownQuery = query ?? string.Empty;
        }

        public void ClearLastShown()
        {
            LastShownQuery = null;
        }

        public bool ShouldBlockShow(string source)
        {
            if (!DismissedUntilSearchPointer)
            {
                return false;
            }

            return !IsExplicitSearchPointer(source);
        }

        public bool ShouldSkipRebuild(string query, bool dropdownOpen, string source)
        {
            if (!dropdownOpen)
            {
                return false;
            }

            // null = never painted / cleared after dismiss. Do not treat as "" or the first
            // history paint is skipped while the dropdown is already open.
            if (LastShownQuery == null)
            {
                return false;
            }

            if (IsForcedRefresh(source))
            {
                return false;
            }

            return string.Equals(query ?? string.Empty, LastShownQuery, StringComparison.Ordinal);
        }

        internal static bool IsExplicitSearchPointer(string source)
        {
            return source == "pointer-down"
                || source == "click"
                || source == "retry-pointer-down"
                || source == "retry-click";
        }

        internal static bool IsForcedRefresh(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            return source == "value-changed"
                || source == "clear-history"
                || source == "history-dismiss"
                || source.StartsWith("retry-value", StringComparison.Ordinal);
        }
    }
}
