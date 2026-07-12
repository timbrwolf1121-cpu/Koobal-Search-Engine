using System;
using System.Collections.Generic;
using KSP.UI.Screens;

namespace PartSearchSuggest
{
    /// <summary>
    /// Single source of truth for suggestion kind → filter key → apply mechanism.
    /// Index build, click apply, and dedup MUST all use PartFilterMatcher.PartMatchesFilter.
    /// </summary>
    internal static class SuggestionFilterRegistry
    {
        internal sealed class FunctionFilterDefinition
        {
            public string FilterKey;
            public string DisplayText;
            public string StockFieldName;
            public string[] SearchTerms;
            public string ApplyNotes;
        }

        private static readonly FunctionFilterDefinition[] FunctionFilters =
        {
            new FunctionFilterDefinition
            {
                FilterKey = "filterEngine",
                DisplayText = "Engines",
                StockFieldName = "filterEngine",
                SearchTerms = new[] { "engine", "engines", "propulsion", "motor" },
                ApplyNotes = "Stock PartCategorizer.filterEngine FilterCriteria (Engine + Propulsion w/ Engine module)"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterControl",
                DisplayText = "Control",
                StockFieldName = "filterControl",
                SearchTerms = new[] { "control", "rcs", "reaction", "sas", "probe" },
                ApplyNotes = "Stock filterControl; RCS lives under Control in stock UI"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterFuelTank",
                DisplayText = "Fuel Tanks",
                StockFieldName = "filterFuelTank",
                SearchTerms = new[] { "fuel", "tank", "tanks", "lf", "oxidizer" },
                ApplyNotes = "Stock filterFuelTank"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterStructural",
                DisplayText = "Structural",
                StockFieldName = "filterStructural",
                SearchTerms = new[] { "structural", "structure", "strut", "truss" },
                ApplyNotes = "Stock filterStructural"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterAero",
                DisplayText = "Aerodynamics",
                StockFieldName = "filterAero",
                SearchTerms = new[] { "aero", "aerodynamic", "wing", "fairing", "lifting" },
                ApplyNotes = "Stock filterAero (PartCategories.Aero); excludes intake-only search terms"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterPods",
                DisplayText = "Pods",
                StockFieldName = "filterPods",
                SearchTerms = new[] { "pod", "pods", "command", "cockpit", "hab" },
                ApplyNotes = "Stock filterPods"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterElectrical",
                DisplayText = "Electrical",
                StockFieldName = "filterElectrical",
                SearchTerms = new[] { "electrical", "battery", "solar", "rtg" },
                ApplyNotes = "Stock filterElectrical"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterScience",
                DisplayText = "Science",
                StockFieldName = "filterScience",
                SearchTerms = new[] { "science", "experiment", "lab" },
                ApplyNotes = "Stock filterScience"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterUtility",
                DisplayText = "Utility",
                StockFieldName = "filterUtility",
                SearchTerms = new[] { "utility", "ladder", "light", "dock" },
                ApplyNotes = "Stock filterUtility"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterThermal",
                DisplayText = "Thermal",
                StockFieldName = "filterThermal",
                SearchTerms = new[] { "thermal", "radiator", "heat" },
                ApplyNotes = "Stock filterThermal"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterRobotics",
                DisplayText = "Robotics",
                StockFieldName = "filterRobotics",
                SearchTerms = new[] { "robotics", "robot", "hinge", "servo" },
                ApplyNotes = "Stock filterRobotics"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterCommunication",
                DisplayText = "Communication",
                StockFieldName = "filterCommunication",
                SearchTerms = new[] { "communication", "comm", "antenna", "relay" },
                ApplyNotes = "Stock filterCommunication"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterCoupling",
                DisplayText = "Coupling",
                StockFieldName = "filterCoupling",
                SearchTerms = new[] { "coupling", "coupler", "docking", "decoupler" },
                ApplyNotes = "Stock filterCoupling"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterCargo",
                DisplayText = "Cargo",
                StockFieldName = "filterCargo",
                SearchTerms = new[] { "cargo", "container", "inventory" },
                ApplyNotes = "Stock filterCargo"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterGround",
                DisplayText = "Ground",
                StockFieldName = "filterGround",
                SearchTerms = new[] { "ground", "landing", "leg", "wheel" },
                ApplyNotes = "Stock filterGround"
            },
            new FunctionFilterDefinition
            {
                FilterKey = "filterPayload",
                DisplayText = "Payload",
                StockFieldName = "filterPayload",
                SearchTerms = new[] { "payload", "service", "bay" },
                ApplyNotes = "Stock filterPayload"
            }
        };

        internal static IEnumerable<FunctionFilterDefinition> GetFunctionFilters()
        {
            for (int i = 0; i < FunctionFilters.Length; i++)
            {
                yield return FunctionFilters[i];
            }
        }

        internal static FunctionFilterDefinition TryGetFunctionFilter(string filterKey)
        {
            if (string.IsNullOrWhiteSpace(filterKey))
            {
                return null;
            }

            for (int i = 0; i < FunctionFilters.Length; i++)
            {
                FunctionFilterDefinition definition = FunctionFilters[i];
                if (string.Equals(definition.FilterKey, filterKey, StringComparison.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }

            return null;
        }

        internal static string DescribeApplyPath(SuggestionKind kind, string filterKey)
        {
            switch (kind)
            {
                case SuggestionKind.FilterFunction:
                    FunctionFilterDefinition definition = TryGetFunctionFilter(filterKey);
                    return definition != null
                        ? "FilterFunction/" + filterKey + " → stock tab navigation (" + definition.DisplayText + ")"
                        : "FilterFunction/" + filterKey + " → stock tab navigation";

                case SuggestionKind.FilterManufacturer:
                    return "FilterManufacturer → manufacturer field exact match";

                case SuggestionKind.FilterDiameter:
                    return "FilterDiameter → bulkheadProfiles tag match";

                case SuggestionKind.FilterCategory:
                    return "FilterCategory → stock category tab navigation";

                case SuggestionKind.FilterModule:
                    return "FilterModule → moduleName or moduleDisplayName match (stock module filter parity)";

                case SuggestionKind.FilterResource:
                    return "FilterResource → resourceName/displayName exact match";

                case SuggestionKind.FilterTech:
                    return "FilterTech → TechRequired exact match";

                case SuggestionKind.FilterTag:
                    return "FilterTag → part.tags / partInfo.tags token match (same field as index)";

                default:
                    return kind + " → not a categorizer filter";
            }
        }
    }
}
