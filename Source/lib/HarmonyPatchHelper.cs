using System;
using System.Reflection;
using HarmonyLib;

namespace PartSearchSuggest
{
    /// <summary>
    /// Applies Harmony patches declared on nested types under a container class only.
    /// Avoids PatchAll(assembly) so one guard's patches cannot affect another.
    /// </summary>
    internal static class HarmonyPatchHelper
    {
        internal static void PatchNestedTypes(Harmony harmony, Type containerType)
        {
            foreach (Type nested in containerType.GetNestedTypes(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
            {
                if (nested.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
                {
                    continue;
                }

                try
                {
                    harmony.CreateClassProcessor(nested).Patch();
                }
                catch (Exception ex)
                {
                    EditorBootstrap.LogWarning(
                        "Harmony patch skipped for "
                        + nested.Name
                        + ": "
                        + ex.Message);
                }
            }
        }
    }
}
