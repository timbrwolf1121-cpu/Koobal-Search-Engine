using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KSP.UI.Screens;

namespace PartSearchSuggest
{
    /// <summary>
    /// Resolves live PartCategorizer Category objects for stock categorizer filter suggestions
    /// so dropdown rows can reuse CustomCategoryIconHelper (button Icon / RawImage pipeline).
    /// </summary>
    internal static class StockCategorizerIconHelper
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const BindingFlags NestedTypeFlags = BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly Type CategoryType = typeof(PartCategorizer).GetNestedType(
            "Category",
            NestedTypeFlags);

        private static readonly FieldInfo CategoriesField = typeof(PartCategorizer).GetField(
            "categories",
            InstanceFlags);

        private static readonly FieldInfo SubcategoriesField = CategoryType?.GetField(
            "subcategories",
            InstanceFlags);

        private static readonly FieldInfo FiltersField = typeof(PartCategorizer).GetField(
            "filters",
            InstanceFlags);

        private static readonly FieldInfo FilterFunctionField = typeof(PartCategorizer).GetField(
            "filterFunction",
            InstanceFlags);

        private static readonly FieldInfo CategoryButtonField = CategoryType?.GetField(
            "button",
            InstanceFlags);

        private static readonly FieldInfo ButtonCategoryNameField = typeof(PartCategorizerButton).GetField(
            "categoryName",
            InstanceFlags);

        private static readonly FieldInfo ButtonDisplayNameField = typeof(PartCategorizerButton).GetField(
            "categorydisplayName",
            InstanceFlags);

        private static readonly PropertyInfo ButtonDisplayCategoryNameProperty = typeof(PartCategorizerButton).GetProperty(
            "displayCategoryName",
            InstanceFlags);

        private static readonly Dictionary<string, object> LiveCategoryCache =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private static bool _treeIndexed;

        internal static bool SupportsCategoryIconRail(PartSuggestion suggestion)
        {
            return suggestion != null && suggestion.ShouldShowCategoryIcon();
        }

        internal static bool TryResolveLiveCategory(PartSuggestion suggestion, out object liveCategory)
        {
            liveCategory = null;
            if (suggestion == null)
            {
                return false;
            }

            if (!suggestion.IsCategorizerFilter)
            {
                return false;
            }

            string cacheKey = BuildCacheKey(suggestion.Kind, suggestion.FilterKey);
            if (LiveCategoryCache.TryGetValue(cacheKey, out object cached))
            {
                liveCategory = cached;
                return liveCategory != null;
            }

            liveCategory = ResolveStockLiveCategory(suggestion);
            if (liveCategory != null)
            {
                LiveCategoryCache[cacheKey] = liveCategory;
            }

            return liveCategory != null;
        }

        internal static void EnsureCacheWarmed(IEnumerable<PartSuggestion> suggestions)
        {
            if (suggestions == null || PartCategorizer.Instance == null)
            {
                return;
            }

            EnsureCategoryTreeIndexed();

            int warmed = 0;
            foreach (PartSuggestion suggestion in suggestions)
            {
                if (!SupportsCategoryIconRail(suggestion))
                {
                    continue;
                }

                if (TryResolveLiveCategory(suggestion, out object liveCategory) && liveCategory != null)
                {
                    CustomCategoryIconHelper.CategoryIconSource source = CustomCategoryIconHelper.GetIconSource(
                        suggestion.IconName,
                        suggestion.FilterKey,
                        liveCategory);
                    if (source.IsValid)
                    {
                        warmed++;
                    }
                }
            }

            if (warmed > 0)
            {
                EditorBootstrap.Log(
                    "Stock categorizer icon cache warmed: "
                    + warmed
                    + " icons, "
                    + LiveCategoryCache.Count
                    + " live categories.");
            }
        }

        internal static void ClearCache()
        {
            LiveCategoryCache.Clear();
            _treeIndexed = false;
        }

        private static string BuildCacheKey(SuggestionKind kind, string filterKey)
        {
            return kind + "|" + (filterKey ?? string.Empty);
        }

        private static object ResolveStockLiveCategory(PartSuggestion suggestion)
        {
            PartCategorizer categorizer = PartCategorizer.Instance;
            if (categorizer == null)
            {
                return null;
            }

            EnsureCategoryTreeIndexed();

            switch (suggestion.Kind)
            {
                case SuggestionKind.FilterFunction:
                    return ResolveFunctionSubcategory(categorizer, suggestion.FilterKey)
                        ?? FindCategoryByLabels(suggestion.FilterKey, suggestion.DisplayText);

                case SuggestionKind.FilterCategory:
                    return FindCategoryByLabels(suggestion.FilterKey, suggestion.DisplayText);

                default:
                    return FindCategoryByLabels(suggestion.FilterKey, suggestion.DisplayText);
            }
        }

        private static object ResolveFunctionSubcategory(PartCategorizer categorizer, string filterKey)
        {
            if (string.IsNullOrWhiteSpace(filterKey))
            {
                return null;
            }

            string suffix = filterKey.StartsWith("filter", StringComparison.OrdinalIgnoreCase)
                ? filterKey.Substring("filter".Length)
                : filterKey;

            if (suffix.Length == 0)
            {
                return null;
            }

            string fieldName = "subcategoryFunction" + suffix;
            FieldInfo field = typeof(PartCategorizer).GetField(fieldName, InstanceFlags);
            return field?.GetValue(categorizer);
        }

        private static void EnsureCategoryTreeIndexed()
        {
            if (_treeIndexed)
            {
                return;
            }

            PartCategorizer categorizer = PartCategorizer.Instance;
            if (categorizer == null)
            {
                return;
            }

            _treeIndexed = true;

            IList categories = CategoriesField?.GetValue(categorizer) as IList;
            if (categories != null)
            {
                for (int i = 0; i < categories.Count; i++)
                {
                    IndexCategoryTree(categories[i]);
                }
            }

            IndexCategoryTree(FilterFunctionField?.GetValue(categorizer));

            foreach (SuggestionFilterRegistry.FunctionFilterDefinition definition in
                SuggestionFilterRegistry.GetFunctionFilters())
            {
                object subcategory = ResolveFunctionSubcategory(categorizer, definition.FilterKey);
                IndexCategoryLabels(subcategory, definition.FilterKey, definition.DisplayText);
            }

            IList filters = FiltersField?.GetValue(categorizer) as IList;
            if (filters != null)
            {
                for (int i = 0; i < filters.Count; i++)
                {
                    IndexFilterEntry(filters[i]);
                }
            }
        }

        private static void IndexCategoryTree(object category)
        {
            if (category == null)
            {
                return;
            }

            string displayName = GetButtonDisplayName(category);
            string categoryName = GetButtonCategoryName(category);
            IndexCategoryLabels(category, categoryName, displayName);

            IList subcategories = SubcategoriesField?.GetValue(category) as IList;
            if (subcategories == null)
            {
                return;
            }

            for (int i = 0; i < subcategories.Count; i++)
            {
                IndexCategoryTree(subcategories[i]);
            }
        }

        private static void IndexFilterEntry(object filterEntry)
        {
            if (filterEntry == null)
            {
                return;
            }

            if (CategoryType != null && CategoryType.IsInstanceOfType(filterEntry))
            {
                IndexCategoryTree(filterEntry);
                return;
            }

            PartCategorizerButton button = filterEntry as PartCategorizerButton;
            if (button != null)
            {
                IndexButtonLabels(button, filterEntry);
            }
        }

        private static void IndexCategoryLabels(object category, string categoryName, string displayName)
        {
            if (category == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                LiveCategoryCache["label|" + displayName.Trim()] = category;
            }

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                LiveCategoryCache["label|" + categoryName.Trim()] = category;
            }
        }

        private static void IndexButtonLabels(PartCategorizerButton button, object categoryOrButton)
        {
            if (button == null)
            {
                return;
            }

            string displayName = GetButtonDisplayName(button);
            string categoryName = GetButtonCategoryName(button);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                LiveCategoryCache["label|" + displayName.Trim()] = categoryOrButton;
            }

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                LiveCategoryCache["label|" + categoryName.Trim()] = categoryOrButton;
            }
        }

        private static object FindCategoryByLabels(string filterKey, string displayText)
        {
            object match = FindByLabel(displayText);
            if (match != null)
            {
                return match;
            }

            return FindByLabel(filterKey);
        }

        private static object FindByLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return null;
            }

            string trimmed = label.Trim();
            if (LiveCategoryCache.TryGetValue("label|" + trimmed, out object exact))
            {
                return exact;
            }

            string formatted = AuthorMatchHelper.FormatDisplayName(trimmed);
            if (!string.Equals(formatted, trimmed, StringComparison.OrdinalIgnoreCase)
                && LiveCategoryCache.TryGetValue("label|" + formatted, out object formattedMatch))
            {
                return formattedMatch;
            }

            return null;
        }

        private static string GetButtonDisplayName(object categoryOrButton)
        {
            PartCategorizerButton button = ResolveButton(categoryOrButton);
            return GetButtonDisplayName(button);
        }

        private static string GetButtonCategoryName(object categoryOrButton)
        {
            PartCategorizerButton button = ResolveButton(categoryOrButton);
            return GetButtonCategoryName(button);
        }

        private static PartCategorizerButton ResolveButton(object categoryOrButton)
        {
            if (categoryOrButton is PartCategorizerButton directButton)
            {
                return directButton;
            }

            return CategoryButtonField?.GetValue(categoryOrButton) as PartCategorizerButton;
        }

        private static string GetButtonDisplayName(PartCategorizerButton button)
        {
            if (button == null)
            {
                return string.Empty;
            }

            if (ButtonDisplayCategoryNameProperty != null)
            {
                string fromProperty = ButtonDisplayCategoryNameProperty.GetValue(button, null) as string;
                if (!string.IsNullOrWhiteSpace(fromProperty))
                {
                    return fromProperty.Trim();
                }
            }

            if (ButtonDisplayNameField != null)
            {
                string fromField = ButtonDisplayNameField.GetValue(button) as string;
                if (!string.IsNullOrWhiteSpace(fromField))
                {
                    return fromField.Trim();
                }
            }

            return string.Empty;
        }

        private static string GetButtonCategoryName(PartCategorizerButton button)
        {
            if (button == null || ButtonCategoryNameField == null)
            {
                return string.Empty;
            }

            return ButtonCategoryNameField.GetValue(button) as string ?? string.Empty;
        }
    }
}
