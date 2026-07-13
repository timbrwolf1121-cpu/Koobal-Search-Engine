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

                if (ModuleNameMatches(moduleInfo.moduleName, moduleKey)
                    || ModuleNameMatches(moduleInfo.moduleDisplayName, moduleKey))
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

            string key = resourceKey.Trim();

            // Structured resource lists only — never free-form resourceInfo HTML (prose mentions
            // flood Enter filters with unrelated parts).

            if (part.resourceInfos != null)
            {
                for (int i = 0; i < part.resourceInfos.Count; i++)
                {
                    AvailablePart.ResourceInfo resourceInfo = part.resourceInfos[i];
                    if (resourceInfo == null)
                    {
                        continue;
                    }

                    if (ResourceNameMatches(resourceInfo.resourceName, key)
                        || ResourceNameMatches(resourceInfo.displayName, key))
                    {
                        return true;
                    }
                }
            }

            // Live prefab resources (IFS / delayed definition) — catch tanks stock lists miss.
            if (part.partPrefab != null && part.partPrefab.Resources != null)
            {
                for (int i = 0; i < part.partPrefab.Resources.Count; i++)
                {
                    PartResource resource = part.partPrefab.Resources[i];
                    if (resource == null || resource.info == null)
                    {
                        continue;
                    }

                    if (ResourceNameMatches(resource.info.name, key)
                        || ResourceNameMatches(resource.info.displayName, key))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Exact, prefix, or camelCase/whole-token contains (≥3 chars). No weak mid-string
        /// embeds inside unrelated names.
        /// </summary>
        private static bool ResourceNameMatches(string candidate, string key)
        {
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            string name = candidate.Trim();
            if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (key.Length < 3)
            {
                return false;
            }

            if (name.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return SignificantTokenContains(name, key);
        }

        private static bool PartHasTagInField(string tagField, string tag)
        {
            if (string.IsNullOrWhiteSpace(tagField))
            {
                return false;
            }

            string key = tag.Trim();
            string[] tokens = tagField.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.Equals(token, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Whole-token / camelCase only — no mid-string "matter" inside unrelated tags.
                if (key.Length >= 3
                    && (token.StartsWith(key, StringComparison.OrdinalIgnoreCase)
                        || SignificantTokenContains(token, key)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ModuleNameMatches(string candidate, string key)
        {
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            string name = candidate.Trim();
            string needle = key.Trim();
            if (string.Equals(name, needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (needle.Length < 3)
            {
                return false;
            }

            if (name.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return SignificantTokenContains(name, needle);
        }

        /// <summary>
        /// Whole-token or camelCase segment match (AntiMatter ↔ antimatter). Rejects weak
        /// substring embeds that are not token-aligned.
        /// </summary>
        private static bool SignificantTokenContains(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word) || word.Length < 3)
            {
                return false;
            }

            if (string.Equals(text, word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Strip spaces for camel compare of display names like "Anti Matter".
            string compact = text.Replace(" ", string.Empty);
            if (compact.Length >= word.Length
                && compact.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Require match to start at 0 or a camel/digit boundary.
                int idx = 0;
                while (idx <= compact.Length - word.Length)
                {
                    int found = compact.IndexOf(word, idx, StringComparison.OrdinalIgnoreCase);
                    if (found < 0)
                    {
                        break;
                    }

                    if (found == 0 || IsNameBoundary(compact, found))
                    {
                        int after = found + word.Length;
                        if (after >= compact.Length || IsNameBoundary(compact, after))
                        {
                            return true;
                        }
                    }

                    idx = found + 1;
                }
            }

            return false;
        }

        private static bool IsNameBoundary(string text, int index)
        {
            if (index <= 0 || index >= text.Length)
            {
                return true;
            }

            char c = text[index];
            char prev = text[index - 1];
            return (char.IsUpper(c) && char.IsLetter(prev) && !char.IsUpper(prev))
                || (char.IsDigit(c) && char.IsLetter(prev))
                || (char.IsLetter(c) && char.IsDigit(prev))
                || c == '_' || c == '-' || prev == '_' || prev == '-';
        }
    }
}
