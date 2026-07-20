using KSP.UI.TooltipTypes;
using UnityEngine;

namespace PartSearchSuggest
{
    internal static class UiTooltipHelper
    {
        private static TooltipController_Text _template;

        internal static void AttachTextTooltip(GameObject target, string text)
        {
            if (target == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            TooltipController_Text template = ResolveTemplate();
            if (template == null || template.prefab == null)
            {
                EditorBootstrap.LogWarning("UiTooltipHelper: no stock TooltipController_Text template found.");
                return;
            }

            TooltipController_Text tooltip = target.GetComponent<TooltipController_Text>();
            if (tooltip == null)
            {
                tooltip = target.AddComponent<TooltipController_Text>();
            }

            tooltip.prefab = template.prefab;
            tooltip.textString = text;
            tooltip.RequireInteractable = true;
        }

        private static TooltipController_Text ResolveTemplate()
        {
            if (_template != null)
            {
                return _template;
            }

            TooltipController_Text[] templates = UnityEngine.Object.FindObjectsOfType<TooltipController_Text>();
            for (int i = 0; i < templates.Length; i++)
            {
                TooltipController_Text candidate = templates[i];
                if (candidate != null && candidate.prefab != null)
                {
                    _template = candidate;
                    return _template;
                }
            }

            return null;
        }
    }
}
