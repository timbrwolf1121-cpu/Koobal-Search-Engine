using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PartSearchSuggest
{
    internal sealed class SearchHistory
    {
        private const string RelativePath = "GameData/KoobalSearchEngine/PluginData/History.cfg";
        private const string PriorRelativePath = "GameData/KoogleSearchEngine/PluginData/History.cfg";
        private const string LegacyRelativePath = "GameData/PartSearchSuggest/PluginData/History.cfg";

        private readonly SearchHistoryStore _store = new SearchHistoryStore();
        private readonly string _path;

        public SearchHistory()
        {
            _path = Path.Combine(KSPUtil.ApplicationRootPath, RelativePath);
            MigrateLegacyConfig();
            Load();
        }

        public IReadOnlyList<string> Entries => _store.Entries;

        public void Remember(string query)
        {
            Remember(query, force: false);
        }

        public void Remember(string query, bool force)
        {
            if (_store.Remember(query, force))
            {
                Save();
            }
        }

        public bool Remove(string query)
        {
            if (!_store.Remove(query))
            {
                return false;
            }

            Save();
            return true;
        }

        public IEnumerable<string> Match(string query, int maxResults)
        {
            return _store.Match(query, maxResults);
        }

        public void Clear()
        {
            if (_store.Entries.Count == 0)
            {
                return;
            }

            _store.Clear();
            Save();
        }

        private void MigrateLegacyConfig()
        {
            if (File.Exists(_path))
            {
                return;
            }

            string priorPath = Path.Combine(KSPUtil.ApplicationRootPath, PriorRelativePath);
            if (File.Exists(priorPath))
            {
                CopyConfigFile(priorPath, _path);
                EditorBootstrap.Log("Migrated search history from KoogleSearchEngine to KoobalSearchEngine.");
                return;
            }

            string legacyPath = Path.Combine(KSPUtil.ApplicationRootPath, LegacyRelativePath);
            if (!File.Exists(legacyPath))
            {
                return;
            }

            CopyConfigFile(legacyPath, _path);
            EditorBootstrap.Log("Migrated search history from PartSearchSuggest to KoobalSearchEngine.");
        }

        private static void CopyConfigFile(string sourcePath, string destPath)
        {
            string dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.Copy(sourcePath, destPath);
        }

        private void Load()
        {
            if (!File.Exists(_path))
            {
                _store.ReplaceAll(null);
                return;
            }

            _store.ReplaceAll(File.ReadAllLines(_path).Select(line => line.Trim()));
        }

        private void Save()
        {
            string dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllLines(_path, _store.Entries);
        }
    }
}
