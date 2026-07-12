using System.Collections.Generic;

using System.IO;

using System.Linq;



namespace PartSearchSuggest

{

    internal sealed class SearchHistory

    {

        private const int MaxEntries = 12;

        private const string RelativePath = "GameData/KoobalSearchEngine/PluginData/History.cfg";

        private const string PriorRelativePath = "GameData/KoogleSearchEngine/PluginData/History.cfg";

        private const string LegacyRelativePath = "GameData/PartSearchSuggest/PluginData/History.cfg";

        private readonly List<string> _entries = new List<string>();

        private readonly string _path;



        public SearchHistory()

        {

            _path = Path.Combine(KSPUtil.ApplicationRootPath, RelativePath);

            MigrateLegacyConfig();

            Load();

        }



        public IReadOnlyList<string> Entries => _entries;



        public void Remember(string query)

        {

            string trimmed = (query ?? string.Empty).Trim();

            if (trimmed.Length < 2)

            {

                return;

            }



            _entries.RemoveAll(e => string.Equals(e, trimmed, System.StringComparison.OrdinalIgnoreCase));

            _entries.Insert(0, trimmed);



            while (_entries.Count > MaxEntries)

            {

                _entries.RemoveAt(_entries.Count - 1);

            }



            Save();

        }



        public IEnumerable<string> Match(string query, int maxResults)

        {

            string trimmed = (query ?? string.Empty).Trim();

            IEnumerable<string> source = _entries;



            if (!string.IsNullOrEmpty(trimmed))

            {

                source = _entries.Where(e => e.IndexOf(trimmed, System.StringComparison.OrdinalIgnoreCase) >= 0);

            }



            return source.Take(maxResults);

        }



        public void Clear()

        {

            if (_entries.Count == 0)

            {

                return;

            }



            _entries.Clear();

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

            _entries.Clear();

            if (!File.Exists(_path))

            {

                return;

            }



            foreach (string line in File.ReadAllLines(_path))

            {

                string entry = line.Trim();

                if (entry.Length > 0)

                {

                    _entries.Add(entry);

                }

            }

        }



        private void Save()

        {

            string dir = Path.GetDirectoryName(_path);

            if (!string.IsNullOrEmpty(dir))

            {

                Directory.CreateDirectory(dir);

            }



            File.WriteAllLines(_path, _entries);

        }

    }

}

