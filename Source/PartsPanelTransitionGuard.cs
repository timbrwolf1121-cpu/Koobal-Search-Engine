using System;
using HarmonyLib;
using KSP.UI;
using KSP.UI.Screens;

namespace PartSearchSuggest
{
    /// <summary>
    /// Blocks stock partsEditor Transition("In") while the search dropdown is open so the panel
    /// does not slide back during an active suggestion session.
    /// </summary>
    internal static class PartsPanelTransitionGuard
    {
        private const string StateIn = "In";

        internal static void ApplyPatches()
        {
            try
            {
                Harmony harmony = new Harmony("KoobalSearchEngine.PartsPanelTransitionGuard");
                HarmonyPatchHelper.PatchNestedTypes(harmony, typeof(PartsPanelTransitionGuard));
            }
            catch (Exception ex)
            {
                EditorBootstrap.LogWarning(
                    "PartsPanelTransitionGuard patch failed — slide-back guard disabled: " + ex.Message);
            }
        }

        private static bool ShouldBlockPartsEditorOpen(UIPanelTransition instance, string stateName)
        {
            if (!PartsPanelCollapseHelper.IsDropdownOpen)
            {
                return false;
            }

            if (!PartsPanelCollapseHelper.IsPartsEditorTransition(instance))
            {
                return false;
            }

            return string.Equals(stateName, StateIn, StringComparison.Ordinal);
        }

        [HarmonyPatch(typeof(UIPanelTransition), nameof(UIPanelTransition.Transition), typeof(string), typeof(Action))]
        private static class TransitionPatch
        {
            private static bool Prefix(UIPanelTransition __instance, string stateName)
            {
                if (!ShouldBlockPartsEditorOpen(__instance, stateName))
                {
                    return true;
                }

                EditorBootstrap.Log(
                    "PartsPanelCollapse: blocked stock partsEditor transition to "
                    + (stateName ?? "null")
                    + " while dropdown open.");
                return false;
            }
        }
    }
}
