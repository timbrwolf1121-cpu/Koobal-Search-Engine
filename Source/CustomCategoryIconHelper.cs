using System;
using System.Collections.Generic;
using System.Reflection;
using KSP.UI.Screens;
using RUI.Icons.Selectable;
using UnityEngine;
using UnityEngine.UI;

namespace PartSearchSuggest
{
    /// <summary>
    /// Resolves custom-category cfg icon names to textures for dropdown rows.
    /// Prefers stock PartCategorizerButton RawImage (texture + uvRect), then IconLoader.
    /// Uses iconSelected (light) for visibility on dark dropdown rows — iconNormal is dark/unselected.
    /// </summary>
    internal static class CustomCategoryIconHelper
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly FieldInfo IconLoaderField = typeof(PartCategorizer).GetField(
            "iconLoader",
            InstanceFlags);

        private static readonly MethodInfo GetIconMethod = typeof(IconLoader).GetMethod(
            "GetIcon",
            InstanceFlags,
            null,
            new[] { typeof(string) },
            null);

        private static readonly FieldInfo CategoryButtonField = typeof(PartCategorizer).GetNestedType(
            "Category",
            BindingFlags.NonPublic | BindingFlags.Public)?.GetField(
            "button",
            InstanceFlags);

        private static readonly FieldInfo CategoryIconField = typeof(PartCategorizer).GetNestedType(
            "Category",
            BindingFlags.NonPublic | BindingFlags.Public)?.GetField(
            "icon",
            InstanceFlags);

        private static readonly FieldInfo ButtonIconSpriteField = typeof(PartCategorizerButton).GetField(
            "iconSprite",
            InstanceFlags);

        private static readonly FieldInfo ButtonIconField = typeof(PartCategorizerButton).GetField(
            "icon",
            InstanceFlags);

        private static readonly PropertyInfo IconNameProperty = typeof(Icon).GetProperty(
            "name",
            InstanceFlags);

        private static readonly Dictionary<string, CategoryIconSource> SourceCache =
            new Dictionary<string, CategoryIconSource>(StringComparer.OrdinalIgnoreCase);

        internal readonly struct CategoryIconSource
        {
            public readonly Texture Texture;
            public readonly Rect UvRect;

            public CategoryIconSource(Texture texture, Rect uvRect)
            {
                Texture = texture;
                UvRect = uvRect;
            }

            public bool IsValid => Texture != null;
        }

        internal static void ClearCache()
        {
            SourceCache.Clear();
        }

        internal static CategoryIconSource GetIconSource(string iconName, string filterKey, object liveCategory = null)
        {
            string cacheKey = BuildCacheKey(iconName, filterKey);
            if (SourceCache.TryGetValue(cacheKey, out CategoryIconSource cached))
            {
                return cached;
            }

            CategoryIconSource source = ResolveIconSource(iconName, liveCategory);
            if (source.IsValid)
            {
                SourceCache[cacheKey] = source;
            }

            return source;
        }

        internal static string TryGetLiveCategoryIconName(object liveCategory)
        {
            if (liveCategory == null || CategoryIconField == null)
            {
                return null;
            }

            if (!(CategoryIconField.GetValue(liveCategory) is Icon categoryIcon))
            {
                return null;
            }

            return GetIconName(categoryIcon);
        }

        private static string BuildCacheKey(string iconName, string filterKey)
        {
            return (iconName ?? string.Empty) + "|" + (filterKey ?? string.Empty);
        }

        private static CategoryIconSource ResolveIconSource(string iconName, object liveCategory)
        {
            CategoryIconSource fromLive = LoadFromLiveCategory(liveCategory);
            if (fromLive.IsValid)
            {
                return fromLive;
            }

            return LoadFromIconLoader(iconName);
        }

        private static CategoryIconSource LoadFromIconLoader(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName))
            {
                return default;
            }

            IconLoader loader = GetIconLoader();
            if (loader == null || GetIconMethod == null)
            {
                return default;
            }

            Icon icon;
            try
            {
                icon = GetIconMethod.Invoke(loader, new object[] { iconName }) as Icon;
            }
            catch (Exception ex)
            {
                EditorBootstrap.LogWarning(
                    "Custom category icon load failed for '" + iconName + "': " + ex.Message);
                return default;
            }

            return CreateSourceFromIcon(icon, iconName);
        }

        private static CategoryIconSource LoadFromLiveCategory(object liveCategory)
        {
            if (liveCategory == null)
            {
                return default;
            }

            if (liveCategory is PartCategorizerButton directButton)
            {
                return LoadFromButton(directButton, TryGetButtonIconName(directButton));
            }

            string iconNameForLog = TryGetLiveCategoryIconName(liveCategory);

            // Prefer iconSelected from the category Icon — live button RawImage shows the
            // unselected (dark) art on inactive tabs, which vanishes on our dark rows.
            if (CategoryIconField?.GetValue(liveCategory) is Icon categoryIcon)
            {
                CategoryIconSource fromIcon = CreateSourceFromIcon(categoryIcon, iconNameForLog);
                if (fromIcon.IsValid)
                {
                    return fromIcon;
                }
            }

            PartCategorizerButton button = CategoryButtonField?.GetValue(liveCategory) as PartCategorizerButton;
            if (button == null)
            {
                return default;
            }

            return LoadFromButton(button, iconNameForLog);
        }

        private static CategoryIconSource LoadFromButton(PartCategorizerButton button, string iconNameForLog)
        {
            if (button == null)
            {
                return default;
            }

            if (ButtonIconField?.GetValue(button) is Icon buttonIcon)
            {
                CategoryIconSource fromIcon = CreateSourceFromIcon(buttonIcon, iconNameForLog);
                if (fromIcon.IsValid)
                {
                    return fromIcon;
                }
            }

            RawImage rawImage = TryGetButtonRawImage(button);
            if (rawImage != null && rawImage.texture != null && rawImage.enabled)
            {
                return new CategoryIconSource(rawImage.texture, rawImage.uvRect);
            }

            return default;
        }

        private static string TryGetButtonIconName(PartCategorizerButton button)
        {
            if (button == null || ButtonIconField == null)
            {
                return null;
            }

            if (ButtonIconField.GetValue(button) is Icon buttonIcon)
            {
                return GetIconName(buttonIcon);
            }

            return null;
        }

        private static RawImage TryGetButtonRawImage(PartCategorizerButton button)
        {
            if (button == null)
            {
                return null;
            }

            if (ButtonIconSpriteField != null)
            {
                RawImage fromField = ButtonIconSpriteField.GetValue(button) as RawImage;
                if (fromField != null)
                {
                    return fromField;
                }
            }

            return button.GetComponentInChildren<RawImage>(true);
        }

        private static IconLoader GetIconLoader()
        {
            PartCategorizer categorizer = PartCategorizer.Instance;
            if (categorizer == null || IconLoaderField == null)
            {
                return null;
            }

            return IconLoaderField.GetValue(categorizer) as IconLoader;
        }

        private static CategoryIconSource CreateSourceFromIcon(Icon icon, string iconNameForLog)
        {
            if (icon == null)
            {
                return default;
            }

            // Dropdown rows use a dark background — prefer light/selected icon art (stock toolbar uses
            // dark normal on light chrome and light selected on dark chrome).
            Texture texture = icon.iconSelected ?? icon.iconNormal;
            if (texture == null)
            {
                texture = icon.iconNormal;
            }

            if (texture == null)
            {
                if (!string.IsNullOrWhiteSpace(iconNameForLog))
                {
                    EditorBootstrap.LogWarning(
                        "Custom category icon has no texture for '" + iconNameForLog + "'.");
                }

                return default;
            }

            return new CategoryIconSource(texture, BuildFullUvRect(texture));
        }

        private static Rect BuildFullUvRect(Texture texture)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            return new Rect(0f, 0f, 1f, 1f);
        }

        private static string GetIconName(Icon icon)
        {
            if (icon == null)
            {
                return null;
            }

            if (IconNameProperty != null)
            {
                return IconNameProperty.GetValue(icon) as string;
            }

            FieldInfo nameField = typeof(Icon).GetField("name", InstanceFlags);
            return nameField?.GetValue(icon) as string;
        }
    }
}
