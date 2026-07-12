using System;
using System.Reflection;
using KSP.UI.Screens;

namespace PartSearchSuggest
{
    /// <summary>
    /// Single predicate source for categorizer filter suggestions.
    /// Index build, click apply, and dedup MUST all call PartMatchesFilter / CountMatchingFilter.
    /// All matchers gate on EditorPartAvailability.IsAvailableInEditor first.
    /// </summary>
    internal static class PartFilterMatcher
    {
        internal static bool PartMatchesFilter(SuggestionKind kind, string filterKey, AvailablePart part)
        {
            if (!EditorPartAvailability.IsAvailableInEditor(part) || string.IsNullOrWhiteSpace(filterKey))
            {
                return false;
            }

            switch (kind)
            {
                case SuggestionKind.FilterFunction:
                    return PartMatchesFunctionFilter(filterKey, part);

                case SuggestionKind.FilterManufacturer:
                    return !string.IsNullOrWhiteSpace(part.manufacturer)
                        && string.Equals(part.manufacturer.Trim(), filterKey, StringComparison.OrdinalIgnoreCase);

                case SuggestionKind.FilterDiameter:
                    return PartHasBulkheadProfile(part, filterKey);

                case SuggestionKind.FilterCategory:
                    return PartMatchesCategory(filterKey, part);

                case SuggestionKind.FilterModule:
                    return PartHasModule(part, filterKey);

                case SuggestionKind.FilterResource:
                    return PartHasResource(part, filterKey);

                case SuggestionKind.FilterTech:
                    return !string.IsNullOrWhiteSpace(part.TechRequired)
                        && string.Equals(part.TechRequired.Trim(), filterKey, StringComparison.OrdinalIgnoreCase);

                case SuggestionKind.FilterTag:
                    return PartHasPartTag(part, filterKey);

                default:
                    return false;
            }
        }

        internal static int CountMatchingFilter(SuggestionKind kind, string filterKey)
        {
            if (string.IsNullOrWhiteSpace(filterKey)
                || PartLoader.Instance == null
                || PartLoader.Instance.loadedParts == null)
            {
                return 0;
            }

            int count = 0;
            foreach (AvailablePart part in EditorPartAvailability.GetAvailableParts())
            {
                if (PartMatchesFilter(kind, filterKey, part))
                {
                    count++;
                }
            }

            return count;
        }

        internal static Func<AvailablePart, bool> ResolvePredicate(SuggestionKind kind, string filterKey)
        {
            if (string.IsNullOrWhiteSpace(filterKey))
            {
                return null;
            }

            switch (kind)
            {
                case SuggestionKind.FilterFunction:
                case SuggestionKind.FilterManufacturer:
                case SuggestionKind.FilterDiameter:
                case SuggestionKind.FilterCategory:
                case SuggestionKind.FilterModule:
                case SuggestionKind.FilterResource:
                case SuggestionKind.FilterTech:
                case SuggestionKind.FilterTag:
                    return part => PartMatchesFilter(kind, filterKey, part);

                default:
                    return null;
            }
        }

        internal static bool PartHasPartTag(AvailablePart part, string tag)
        {
            if (part == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            if (PartHasTagInField(part.tags, tag))
            {
                return true;
            }

            Part prefab = part.partPrefab;
            return prefab?.partInfo != null && PartHasTagInField(prefab.partInfo.tags, tag);
        }

        internal static bool PartHasModule(AvailablePart part, string moduleKey)
        {
            if (part?.moduleInfos == null || string.IsNullOrWhiteSpace(moduleKey))
            {
                return false;
            }

            for (int i = 0; i < part.moduleInfos.Count; i++)
            {
                AvailablePart.ModuleInfo moduleInfo = part.moduleInfos[i];
                if (moduleInfo == null)
                {
                    continue;
                }

                if (string.Equals(moduleInfo.moduleName, moduleKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(moduleInfo.moduleDisplayName, moduleKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string ResolveModuleFilterKey(AvailablePart.ModuleInfo moduleInfo, string displayKey)
        {
            if (!string.IsNullOrWhiteSpace(moduleInfo?.moduleDisplayName))
            {
                return moduleInfo.moduleDisplayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(moduleInfo?.moduleName))
            {
                return moduleInfo.moduleName.Trim();
            }

            return displayKey;
        }

        private static bool PartMatchesFunctionFilter(string filterKey, AvailablePart part)
        {
            SuggestionFilterRegistry.FunctionFilterDefinition definition =
                SuggestionFilterRegistry.TryGetFunctionFilter(filterKey);

            string stockField = definition?.StockFieldName ?? filterKey;
            Func<AvailablePart, bool> stockPredicate = ResolveStockFunctionPredicate(stockField);
            if (stockPredicate != null)
            {
                return stockPredicate(part);
            }

            PartCategories? category = MapFunctionFieldToCategory(stockField);
            return category.HasValue && part.category == category.Value;
        }

        private static bool PartMatchesCategory(string filterKey, AvailablePart part)
        {
            if (!Enum.TryParse(filterKey, true, out PartCategories category))
            {
                return false;
            }

            return part.category == category;
        }

        private static Func<AvailablePart, bool> ResolveStockFunctionPredicate(string stockFilterField)
        {
            PartCategorizer categorizer = PartCategorizer.Instance;
            if (categorizer == null || string.IsNullOrWhiteSpace(stockFilterField))
            {
                return null;
            }

            FieldInfo field = typeof(PartCategorizer).GetField(
                stockFilterField,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
            {
                return null;
            }

            EditorPartListFilter<AvailablePart> stockFilter = field.GetValue(categorizer) as EditorPartListFilter<AvailablePart>;
            return stockFilter?.FilterCriteria;
        }

        private static PartCategories? MapFunctionFieldToCategory(string stockFilterField)
        {
            switch (stockFilterField)
            {
                case "filterEngine":
                    return PartCategories.Engine;
                case "filterControl":
                    return PartCategories.Control;
                case "filterFuelTank":
                    return PartCategories.FuelTank;
                case "filterStructural":
                    return PartCategories.Structural;
                case "filterAero":
                    return PartCategories.Aero;
                case "filterPods":
                    return PartCategories.Pods;
                case "filterElectrical":
                    return PartCategories.Electrical;
                case "filterScience":
                    return PartCategories.Science;
                case "filterUtility":
                    return PartCategories.Utility;
                case "filterThermal":
                    return PartCategories.Thermal;
                case "filterRobotics":
                    return PartCategories.Robotics;
                case "filterCommunication":
                    return PartCategories.Communication;
                case "filterCoupling":
                    return PartCategories.Coupling;
                case "filterCargo":
                    return PartCategories.Cargo;
                case "filterGround":
                    return PartCategories.Ground;
                case "filterPayload":
                    return PartCategories.Payload;
                default:
                    return null;
            }
        }

        private static bool PartHasBulkheadProfile(AvailablePart part, string profileTag)
        {
            if (part == null || string.IsNullOrWhiteSpace(profileTag))
            {
                return false;
            }

            string profiles = part.bulkheadProfiles ?? string.Empty;
            if (profiles.Length == 0)
            {
                return false;
            }

            string[] tags = profiles.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], profileTag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PartHasResource(AvailablePart part, string resourceKey)
        {
            if (part == null || string.IsNullOrWhiteSpace(resourceKey))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(part.resourceInfo)
                && part.resourceInfo.IndexOf(resourceKey, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (part.resourceInfos == null)
            {
                return false;
            }

            for (int i = 0; i < part.resourceInfos.Count; i++)
            {
                AvailablePart.ResourceInfo resourceInfo = part.resourceInfos[i];
                if (resourceInfo == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(resourceInfo.resourceName)
                    && string.Equals(resourceInfo.resourceName.Trim(), resourceKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(resourceInfo.displayName)
                    && string.Equals(resourceInfo.displayName.Trim(), resourceKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PartHasTagInField(string tagField, string tag)
        {
            if (string.IsNullOrWhiteSpace(tagField))
            {
                return false;
            }

            string[] tokens = tagField.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (string.Equals(tokens[i], tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
