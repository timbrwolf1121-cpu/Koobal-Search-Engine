using System;

using System.IO;

using System.Linq;



namespace PartSearchSuggest

{

    internal static class DebugSettings

    {

        private const string RelativePath = "GameData/KoobalSearchEngine/PluginData/DebugSettings.cfg";

        private const string PriorRelativePath = "GameData/KoogleSearchEngine/PluginData/DebugSettings.cfg";

        private const string LegacyRelativePath = "GameData/PartSearchSuggest/PluginData/DebugSettings.cfg";

        private static bool _loaded;

        private static bool _dumpIndexStats;

        private static bool _verbose;



        public static bool DumpIndexStats

        {

            get

            {

                EnsureLoaded();

                return _dumpIndexStats;

            }

        }



        /// <summary>
        /// Gates high-volume per-keystroke / per-scene diagnostic logging. Off by default so beta
        /// installs stay quiet; enable with "verbose = true" in DebugSettings.cfg when diagnosing.
        /// </summary>
        public static bool Verbose

        {

            get

            {

                EnsureLoaded();

                return _verbose;

            }

        }



        private static void EnsureLoaded()

        {

            if (_loaded)

            {

                return;

            }



            _loaded = true;

            MigrateLegacyConfig();

            string path = Path.Combine(KSPUtil.ApplicationRootPath, RelativePath);

            if (!File.Exists(path))

            {

                return;

            }



            foreach (string line in File.ReadAllLines(path))

            {

                string trimmed = line.Trim();

                if (trimmed.StartsWith("dumpIndexStats", StringComparison.OrdinalIgnoreCase))

                {

                    int eq = trimmed.IndexOf('=');

                    if (eq >= 0)

                    {

                        string value = trimmed.Substring(eq + 1).Trim();

                        _dumpIndexStats = value.Equals("true", StringComparison.OrdinalIgnoreCase)

                            || value.Equals("1", StringComparison.Ordinal);

                    }

                }

                else if (trimmed.StartsWith("verbose", StringComparison.OrdinalIgnoreCase))

                {

                    int eq = trimmed.IndexOf('=');

                    if (eq >= 0)

                    {

                        string value = trimmed.Substring(eq + 1).Trim();

                        _verbose = value.Equals("true", StringComparison.OrdinalIgnoreCase)

                            || value.Equals("1", StringComparison.Ordinal);

                    }

                }

            }

        }



        private static void MigrateLegacyConfig()

        {

            string path = Path.Combine(KSPUtil.ApplicationRootPath, RelativePath);

            if (File.Exists(path))

            {

                return;

            }



            string priorPath = Path.Combine(KSPUtil.ApplicationRootPath, PriorRelativePath);

            if (File.Exists(priorPath))

            {

                CopyConfigFile(priorPath, path);

                EditorBootstrap.Log("Migrated debug settings from KoogleSearchEngine to KoobalSearchEngine.");

                return;

            }



            string legacyPath = Path.Combine(KSPUtil.ApplicationRootPath, LegacyRelativePath);

            if (!File.Exists(legacyPath))

            {

                return;

            }



            CopyConfigFile(legacyPath, path);

            EditorBootstrap.Log("Migrated debug settings from PartSearchSuggest to KoobalSearchEngine.");

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

    }

}

