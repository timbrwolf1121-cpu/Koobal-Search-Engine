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
            "antenna", "battery", "capsule", "cargo", "claw", "cockpit", "command",
            "comm", "communication", "communications", "control", "cooling", "coupler",
            "coupling", "crew",
            "decouple", "decoupler", "decouplers", "dock", "docking",
            "electrical", "engine", "engines", "eva", "experiment",
            "fairing", "fuel", "fueltank", "gimbal", "gyro", "hab", "heat", "heatsink",
            "hinge", "intake",
            "intakes", "isru", "jet", "ladder", "landing", "leg", "legs",
            "lf", "liquid", "liquidfuel", "mono", "monoprop", "monopropellant", "motor",
            "nose", "ore", "oxidizer", "parachute", "parachutes", "payload", "pod", "pods",
            "power", "probe",
            "propellant", "propulsion", "radiator", "rcs", "relay", "robot", "robotics",
            "rocket", "rockomax", "rtg", "sas",
            "science", "sensor", "sensors", "servo", "solar", "solid", "solidfuel",
            "structural", "structure", "strut", "tank", "tanks", "thermal", "thruster",
            "truss", "utility",
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

                // Prefix of a category/alias token (e.g. "eng" → engine, "he" → heat→Thermal).
                // Length ≥ 2 so short predictive aliases still reserve stock Function/Category slots.
                foreach (string token in CategoryTokens)
                {
                    if (token.Length >= 4
                        && token.StartsWith(words[i], StringComparison.OrdinalIgnoreCase)
                        && words[i].Length >= SuggestionQueryGuards.MinSuggestionQueryLength)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
