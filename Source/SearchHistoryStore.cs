using System;
using System.Collections.Generic;
using System.Linq;

namespace PartSearchSuggest
{
    /// <summary>
    /// In-memory recent-search list. File IO stays in <see cref="SearchHistory"/>.
    /// Dismissed queries stay out of Match until an explicit Enter/click Remember (force).
    /// </summary>
    internal sealed class SearchHistoryStore
    {
        public const int MaxEntries = 12;

        private readonly List<string> _entries = new List<string>();
        private readonly HashSet<string> _suppressRemember =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> Entries => _entries;

        public void ReplaceAll(IEnumerable<string> entries)
        {
            _entries.Clear();
            _suppressRemember.Clear();
            if (entries == null)
            {
                return;
            }

            foreach (string entry in entries)
            {
                string trimmed = (entry ?? string.Empty).Trim();
                if (trimmed.Length > 0)
                {
                    _entries.Add(trimmed);
                }
            }
        }

        public bool Remember(string query, bool force)
        {
            string trimmed = (query ?? string.Empty).Trim();
            if (trimmed.Length < 2)
            {
                return false;
            }

            if (!force && _suppressRemember.Contains(trimmed))
            {
                return false;
            }

            if (force)
            {
                _suppressRemember.Remove(trimmed);
            }

            _entries.RemoveAll(e => string.Equals(e, trimmed, StringComparison.OrdinalIgnoreCase));
            _entries.Insert(0, trimmed);
            while (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }

            return true;
        }

        public bool Remove(string query)
        {
            string trimmed = (query ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            int before = _entries.Count;
            _entries.RemoveAll(e => string.Equals(e, trimmed, StringComparison.OrdinalIgnoreCase));
            if (_entries.Count == before)
            {
                return false;
            }

            _suppressRemember.Add(trimmed);
            return true;
        }

        public IEnumerable<string> Match(string query, int maxResults)
        {
            string trimmed = (query ?? string.Empty).Trim();
            IEnumerable<string> source = _entries;
            if (!string.IsNullOrEmpty(trimmed))
            {
                source = _entries.Where(e => e.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return source.Take(maxResults);
        }

        public void Clear()
        {
            _entries.Clear();
            _suppressRemember.Clear();
        }
    }
}
