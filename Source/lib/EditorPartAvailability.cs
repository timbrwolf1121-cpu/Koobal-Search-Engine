using System;
using System.Collections.Generic;
using System.Reflection;
using KSP.UI.Screens;

namespace PartSearchSuggest
{
    /// <summary>
    /// Mirrors stock VAB/SPH editor part list inclusion (EditorPartList.RefreshPartList /
    /// RefreshSearchList): ExcludeFilters (ResearchAndDevelopment.partTechAvailable),
    /// AmountAvailableFilter (amountAvailable &gt; 0), and BasePartCategorizer.PartMatchesSearch
    /// (category != none). Index, counts, dedup, and apply MUST all use this helper.
    /// Availability is computed once per editor session via <see cref="WarmCache"/>.
    /// </summary>
    internal static class EditorPartAvailability
    {
        private const string UnresearcheableTech = "Unresearcheable";

        private static MethodInfo _partTechAvailableMethod;
        private static readonly List<AvailablePart> CachedAvailableParts = new List<AvailablePart>();
        private static readonly HashSet<AvailablePart> CachedAvailableSet = new HashSet<AvailablePart>();
        private static bool _cacheWarmed;

        internal static void Invalidate()
        {
            _cacheWarmed = false;
            CachedAvailableParts.Clear();
            CachedAvailableSet.Clear();
        }

        internal static void WarmCache()
        {
            if (_cacheWarmed)
            {
                return;
            }

            CachedAvailableParts.Clear();
            CachedAvailableSet.Clear();

            if (PartLoader.Instance == null || PartLoader.Instance.loadedParts == null)
            {
                _cacheWarmed = true;
                return;
            }

            foreach (AvailablePart part in PartLoader.Instance.loadedParts)
            {
                if (ComputeAvailableInEditor(part))
                {
                    CachedAvailableParts.Add(part);
                    CachedAvailableSet.Add(part);
                }
            }

            _cacheWarmed = true;
        }

        internal static IReadOnlyList<AvailablePart> GetAvailableParts()
        {
            EnsureCache();
            return CachedAvailableParts;
        }

        internal static bool IsAvailableInEditor(AvailablePart part)
        {
            if (part == null)
            {
                return false;
            }

            EnsureCache();
            return CachedAvailableSet.Contains(part);
        }

        internal static int CountLoadedEditorParts()
        {
            EnsureCache();
            return CachedAvailableParts.Count;
        }

        private static void EnsureCache()
        {
            if (!_cacheWarmed)
            {
                WarmCache();
            }
        }

        private static bool ComputeAvailableInEditor(AvailablePart part)
        {
            if (part == null || part.partPrefab == null)
            {
                return false;
            }

            // Stock text search and auto-tag generation skip category none.
            if (part.category == PartCategories.none)
            {
                return false;
            }

            // EditorPartList.AmountAvailableFilter
            if (part.amountAvailable <= 0)
            {
                return false;
            }

            // EditorPartList.ExcludeFilters unresearchedTechFilter
            if (!IsPartTechAvailable(part))
            {
                return false;
            }

            return true;
        }

        private static bool IsPartTechAvailable(AvailablePart part)
        {
            ResearchAndDevelopment rd = ResearchAndDevelopment.Instance;
            if (rd != null)
            {
                MethodInfo method = GetPartTechAvailableMethod();
                if (method != null)
                {
                    try
                    {
                        return (bool)method.Invoke(rd, new object[] { part });
                    }
                    catch (Exception ex)
                    {
                        EditorBootstrap.LogWarning(
                            "partTechAvailable reflection failed for '"
                            + (part.name ?? string.Empty)
                            + "': "
                            + ex.Message);
                    }
                }
            }

            return IsPartTechAvailableStaticFallback(part);
        }

        private static bool IsPartTechAvailableStaticFallback(AvailablePart part)
        {
            string tech = part.TechRequired ?? string.Empty;
            if (tech.Length == 0)
            {
                return true;
            }

            if (string.Equals(tech, UnresearcheableTech, StringComparison.Ordinal))
            {
                return false;
            }

            // R&D not ready yet — only exclude known non-editor markers.
            return true;
        }

        private static MethodInfo GetPartTechAvailableMethod()
        {
            if (_partTechAvailableMethod == null)
            {
                _partTechAvailableMethod = typeof(ResearchAndDevelopment).GetMethod(
                    "partTechAvailable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            }

            return _partTechAvailableMethod;
        }
    }
}
