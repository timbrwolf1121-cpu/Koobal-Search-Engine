using System;
using System.Collections.Generic;

namespace PartSearchSuggest
{
    /// <summary>
    /// Detects category-like queries so ShowSuggestions can reserve filter slots
    /// (engines, aero, battery, …) without starving part / mod rows.
    /// </summary>
    internal static class SuggestionCategoricalQuery
    {
        private static readonly HashSet<string> CategoryTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "aero", "aerodynamic", "aerodynamics", "aircraft", "airplane",
            "antenna", "battery", "capsule", "cargo", "claw", "command",
            "communication", "communications", "control", "crew",
            "decouple", "decoupler", "decouplers", "dock", "docking",
            "electrical", "engine", "engines", "eva", "experiment",
            "fairing", "fuel", "fueltank", "gimbal", "heat", "intake",
            "intakes", "isru", "jet", "ladder", "leg", "legs",
            "liquid", "liquidfuel", "mono", "monoprop", "monopropellant",
            "nose", "ore", "oxidizer", "parachute", "parachutes", "probe",
            "propellant", "radiator", "rcs", "rocket", "rockomax", "sas",
            "science", "sensor", "sensors", "solar", "solid", "solidfuel",
            "structural", "strut", "tank", "tanks", "thermal", "utility",
            "wheel", "wheels", "wing", "wings"
        };

        internal static bool LooksCategorical(string query)
        {
            string trimmed = (query ?? string.Empty).Trim();
            if (trimmed.Length < SuggestionQueryGuards.MinSuggestionQueryLength)
            {
                return false;
            }

            string[] words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (CategoryTokens.Contains(words[i]))
                {
                    return true;
                }

                // Prefix of a category token (e.g. "eng" → engine).
                foreach (string token in CategoryTokens)
                {
                    if (token.Length >= 4
                        && token.StartsWith(words[i], StringComparison.OrdinalIgnoreCase)
                        && words[i].Length >= 3)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}