using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartSearchSuggest
{
    /// <summary>
    /// Programmatic Google Catull-style parody wordmark for the empty-query dropdown footer.
    /// Renders multicolor "Koobal" via TMP rich text — no PNG, true transparency.
    /// </summary>
    internal static class KoobalWordmarkBuilder
    {
        // Footer branding — readable at 1080p without dominating suggestion rows.
        private const float WordmarkFontSize = 28f;
        private const float SubtitleFontSize = 12f;
        private const float MicroFontSize = 10f;
        private const float WordmarkCharSpacing = 1.5f;
        private const float WordmarkPreferredHeight = 30f;

        private static readonly Color SubtitleColor = new Color(0.70f, 0.78f, 0.90f, 1f);
        private static readonly Color MicroTextColor = new Color(0.60f, 0.68f, 0.78f, 0.95f);

        internal static string BuildKoobalRichText()
        {
            return "<color=#4285F4>K</color>"
                + "<color=#EA4335>o</color>"
                + "<color=#FBBC05>o</color>"
                + "<color=#34A853>b</color>"
                + "<color=#EA4335>a</color>"
                + "<color=#4285F4>l</color>";
        }

        internal static void BuildWordmarkOnly(Transform parent, TMP_FontAsset font)
        {
            CreateWordmarkLabel(parent, font);
        }

        internal static void BuildFullTagline(Transform parent, TMP_FontAsset font)
        {
            CreateWordmarkLabel(parent, font);
            CreateCaptionLabel(parent, font, "SEARCH ENGINE", SubtitleFontSize, SubtitleColor, 2f, 15f);
            CreateCaptionLabel(
                parent,
                font,
                "for Kerbal Space Program",
                MicroFontSize,
                MicroTextColor,
                0f,
                12f);
        }

        private static TextMeshProUGUI CreateWordmarkLabel(Transform parent, TMP_FontAsset font)
        {
            GameObject labelObject = new GameObject("KoobalWordmark", typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);

            LayoutElement layout = labelObject.AddComponent<LayoutElement>();
            layout.preferredHeight = WordmarkPreferredHeight;
            layout.minHeight = 24f;
            layout.flexibleWidth = 1f;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            ApplyFont(label, font);
            label.text = BuildKoobalRichText();
            label.richText = true;
            label.fontSize = WordmarkFontSize;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = WordmarkCharSpacing;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
            return label;
        }

        private static void CreateCaptionLabel(
            Transform parent,
            TMP_FontAsset font,
            string text,
            float fontSize,
            Color color,
            float topMargin,
            float preferredHeight)
        {
            GameObject labelObject = new GameObject("KoobalCaption", typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);

            LayoutElement layout = labelObject.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            layout.minHeight = preferredHeight - 2f;
            layout.flexibleWidth = 1f;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            ApplyFont(label, font);
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Normal;
            label.characterSpacing = 0.5f;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.margin = new Vector4(0f, topMargin, 0f, 0f);
            label.raycastTarget = false;
        }

        private static void ApplyFont(TextMeshProUGUI label, TMP_FontAsset font)
        {
            if (font != null)
            {
                label.font = font;
            }
        }
    }
}
