using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PartSearchSuggest
{
    /// <summary>
    /// Soft craft-presence / career-essentials prior for part suggestion ranking.
    /// Loaded once from PluginData (fail-open if missing). Never beats strong exact
    /// title/name text matches — boost is capped and skipped for those hits.
    /// </summary>
    internal static class PartPopularityPriors
    {
        private const string RelativePath = "GameData/KoobalSearchEngine/PluginData/PartPopularity.cfg";

        /// <summary>Max RankScore subtract from popularity (lower RankScore = better).</summary>
        internal const int MaxSoftBoost = 3;

        private static readonly Dictionary<string, float> Weights =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> SpamExcluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "strutConnector",
            "strutCube",
            "fuelLine",
            "solarPanels5",
            "solarPanels1",
            "solarPanels2",
            "solarPanels3",
            "solarPanels4",
            "RCSBlock",
            "linearRcs",
            "launchClamp1"
        };

        private static bool _loaded;

        internal static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            Weights.Clear();

            try
            {
                string path = Path.Combine(KSPUtil.ApplicationRootPath, RelativePath);
                if (!File.Exists(path))
                {
                    EditorBootstrap.Log("PartPopularity.cfg missing — popularity prior disabled (fail-open).");
                    return;
                }

                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == '/' || line[0] == ';')
                    {
                        continue;
                    }

                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }

                    string name = line.Substring(0, eq).Trim();
                    string valueText = line.Substring(eq + 1).Trim();
                    if (name.Length == 0 || SpamExcluded.Contains(name))
                    {
                        continue;
                    }

                    if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out float weight))
                    {
                        continue;
                    }

                    if (weight <= 0f)
                    {
                        continue;
                    }

                    if (weight > 1f)
                    {
                        weight = 1f;
                    }

                    Weights[name] = weight;
                }

                EditorBootstrap.LogAlways(
                    "Loaded part popularity prior (" + Weights.Count + " entries).");
            }
            catch (Exception ex)
            {
                Weights.Clear();
                EditorBootstrap.LogWarning(
                    "PartPopularity.cfg load failed — prior disabled: " + ex.Message);
            }
        }

        /// <summary>
        /// Soft RankScore improvement (0..MaxSoftBoost). Zero for strong title/name matches
        /// so popularity never outranks exact title/name hits.
        /// </summary>
        internal static int SoftBoost(AvailablePart part, int textScore, bool strongTitleOrName)
        {
            EnsureLoaded();

            if (strongTitleOrName || part == null || string.IsNullOrEmpty(part.name))
            {
                return 0;
            }

            if (!Weights.TryGetValue(part.name, out float weight) || weight <= 0f)
            {
                return 0;
            }

            // Weaker text matches get less of the prior so soft boost stays a tie-break.
            float scale = textScore <= 5 ? 1f : 0.65f;
            int boost = (int)Math.Round(weight * MaxSoftBoost * scale);
            if (boost < 0)
            {
                return 0;
            }

            return boost > MaxSoftBoost ? MaxSoftBoost : boost;
        }
    }
}
