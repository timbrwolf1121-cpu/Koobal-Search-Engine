using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KSP.UI.Screens;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PartSearchSuggest
{
    /// <summary>
    /// 1.0 PartSearch icon virtualizer: keep a pool of
    /// <see cref="ExperimentalV09Gate.MaxVisibleIcons"/> live <see cref="EditorPartIcon"/>
    /// widgets, size the scroll content for the full filtered set, and rebind the pool as
    /// the user scrolls. Does not unload PartLoader data, does not skip PartCategorizer.Setup,
    /// and fail-opens to stock UpdatePartIcons.
    /// </summary>
    internal static class ExperimentalPartsListVirtualizer
    {
        private static readonly FieldInfo StateField =
            typeof(EditorPartList).GetField(
                "state",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo CategoryListField =
            typeof(EditorPartList).GetField(
                "categoryList",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo IconsField =
            typeof(EditorPartList).GetField(
                "icons",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo IconCacheField =
            typeof(EditorPartList).GetField(
                "iconCache",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo PartPrefabField =
            typeof(EditorPartList).GetField(
                "partPrefab",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo IconSizeField =
            typeof(EditorPartList).GetField(
                "iconSize",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo IconOverScaleField =
            typeof(EditorPartList).GetField(
                "iconOverScale",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo IconOverSpinField =
            typeof(EditorPartList).GetField(
                "iconOverSpin",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo ScrollRectStateField =
            typeof(EditorPartList).GetField(
                "scrollRectState",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly MethodInfo ClearAllItemsMethod =
            typeof(EditorPartList).GetMethod(
                "ClearAllItems",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly MethodInfo UpdatePartIconMethod =
            typeof(EditorPartList).GetMethod(
                "UpdatePartIcon",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(EditorPartIcon), typeof(AvailablePart), typeof(bool) },
                null);

        // Reused across scroll rebinds — UpdatePartIcon.Invoke allocated a new object[] per icon.
        private static readonly object[] UpdatePartIconArgs = new object[3];
        private static readonly object[] SetStatePartsArgs = { "parts" };
        private static readonly object BoxedTrue = true;
        private static readonly object BoxedFalse = false;
        private static MethodInfo _scrollRectSetStateMethod;

        private static bool _patchesApplied;
        private static RecycleSession _session;

        private sealed class RecycleSession
        {
            public EditorPartList List;
            public bool CustomCategory;
            public int WindowStart;
            public int Total;
            public int Columns;
            public float StrideX;
            public float StrideY;
            public float PaddingLeft;
            public float PaddingTop;
            public float PaddingBottom;
            public Vector2 CellSize;
            public GridLayoutGroup Grid;
            public bool GridWasEnabled;
            public ContentSizeFitter Fitter;
            public ContentSizeFitter.FitMode FitterVerticalWas;
            public bool FitterWasEnabled;
            public ScrollRect ScrollRect;
            public RectTransform PartGrid;
            public RectTransform Content;
            public UnityAction<Vector2> ScrollHandler;
            public bool SuppressScroll;
            public int LastLoggedWindow = int.MinValue;
            public float IconSize = 1f;
            public float IconOverScale = 1f;
            public float IconOverSpin = 0f;
            public bool IconMetricsReady;
        }

        private static readonly Vector2 IconAnchorMin = new Vector2(0f, 1f);
        private static readonly Vector2 IconAnchorMax = new Vector2(0f, 1f);
        private static readonly Vector2 IconPivot = new Vector2(0.5f, 0.5f);

        internal static void ApplyPatches()
        {
            if (!ExperimentalV09Gate.Enabled || _patchesApplied)
            {
                return;
            }

            try
            {
                Harmony harmony = new Harmony("KoobalSearchEngine.ExperimentalPartsListVirtualizer");
                HarmonyPatchHelper.PatchNestedTypes(harmony, typeof(ExperimentalPartsListVirtualizer));
                _patchesApplied = true;
                EditorBootstrap.Log(
                    "Scroll-recycle PartSearch virtualizer active (pool "
                    + ExperimentalV09Gate.MaxVisibleIcons
                    + " icons).");
            }
            catch (Exception ex)
            {
                EditorBootstrap.LogWarning(
                    "Scroll-recycle virtualizer patch failed — fail-open to stock: " + ex.Message);
            }
        }

        /// <summary>
        /// Harmony Prefix for <see cref="EditorPartList"/> UpdatePartIcons.
        /// Returns false to skip stock when virtualized path succeeds.
        /// </summary>
        [HarmonyPatch(typeof(EditorPartList), "UpdatePartIcons")]
        private static class UpdatePartIconsPatch
        {
            private static bool Prefix(EditorPartList __instance, bool customCategory)
            {
                if (!ShouldInterceptPartSearch(__instance))
                {
                    EndSession();
                    return true;
                }

                try
                {
                    if (TryUpdatePartIconsVirtualized(__instance, customCategory))
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    EditorBootstrap.LogWarningOnce(
                        "recycle.UpdatePartIcons",
                        "Scroll-recycle UpdatePartIcons failed — fail-open to stock: " + ex.Message);
                    EndSession();
                }

                return true;
            }
        }

        private static bool ShouldInterceptPartSearch(EditorPartList list)
        {
            if (!ExperimentalV09Gate.Enabled || list == null || StateField == null)
            {
                return false;
            }

            if (!StockSearchGuard.HasActiveCustomFilter)
            {
                return false;
            }

            object stateObj = StateField.GetValue(list);
            return stateObj is EditorPartList.State state
                && state == EditorPartList.State.PartSearch;
        }

        private static bool TryUpdatePartIconsVirtualized(EditorPartList list, bool customCategory)
        {
            if (CategoryListField == null
                || IconsField == null
                || IconCacheField == null
                || PartPrefabField == null
                || ClearAllItemsMethod == null
                || UpdatePartIconMethod == null)
            {
                EditorBootstrap.LogWarning(
                    "Scroll-recycle virtualizer reflection incomplete — fail-open to stock.");
                return false;
            }

            List<AvailablePart> categoryList =
                CategoryListField.GetValue(list) as List<AvailablePart>;
            if (categoryList == null || categoryList.Count == 0)
            {
                EndSession();
                return false;
            }

            int total = categoryList.Count;
            if (total <= ExperimentalV09Gate.MaxVisibleIcons)
            {
                // Small result set — stock path is fine (and keeps full behaviour).
                EndSession();
                return false;
            }

            if (PartCategorizer.Instance == null)
            {
                return false;
            }

            if (!TryBeginOrRefreshSession(list, customCategory, total))
            {
                EndSession();
                return false;
            }

            TrySetPartsScrollState(list);

            ApplyVirtualContentHeight(_session);
            _session.WindowStart = 0;
            if (!TryBindWindow(list, categoryList, customCategory, resetScrollToTop: true))
            {
                EndSession();
                return false;
            }

            // Verbose only — large-filter refreshes must not flood the player log.
            EditorBootstrap.Log(
                "Scroll-recycle PartSearch icons: pool "
                + ExperimentalV09Gate.MaxVisibleIcons
                + " of "
                + total
                + " (widgets only — PartLoader untouched).");
            return true;
        }

        /// <summary>
        /// Keep the parts list ScrollRect in the stock "parts" UIAnimator state. Cached
        /// SetState MethodInfo — lookup used to run on every UpdatePartIcons.
        /// </summary>
        private static void TrySetPartsScrollState(EditorPartList list)
        {
            if (list == null || ScrollRectStateField == null)
            {
                return;
            }

            try
            {
                object scrollState = ScrollRectStateField.GetValue(list);
                if (scrollState == null)
                {
                    return;
                }

                if (_scrollRectSetStateMethod == null)
                {
                    _scrollRectSetStateMethod = scrollState.GetType().GetMethod(
                        "SetState",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(string) },
                        null);
                }

                if (_scrollRectSetStateMethod == null)
                {
                    return;
                }

                _scrollRectSetStateMethod.Invoke(scrollState, SetStatePartsArgs);
            }
            catch (Exception ex)
            {
                EditorBootstrap.LogWarningOnce(
                    "recycle.SetState",
                    "Scroll-recycle SetState(parts) failed: " + ex.Message);
            }
        }

        private static bool TryBeginOrRefreshSession(
            EditorPartList list,
            bool customCategory,
            int total)
        {
            RectTransform partGrid = list.partGrid;
            ScrollRect scrollRect = list.partListScrollRect;
            if (partGrid == null || scrollRect == null)
            {
                return false;
            }

            GridLayoutGroup grid = partGrid.GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                EditorBootstrap.LogWarning(
                    "Scroll-recycle virtualizer: partGrid has no GridLayoutGroup — fail-open to stock.");
                return false;
            }

            if (_session != null && _session.List != list)
            {
                EndSession();
            }

            if (_session == null)
            {
                _session = new RecycleSession
                {
                    List = list,
                    Grid = grid,
                    GridWasEnabled = grid.enabled,
                    PartGrid = partGrid,
                    ScrollRect = scrollRect,
                    Content = scrollRect.content != null ? scrollRect.content : partGrid,
                    ScrollHandler = OnVirtualScroll
                };

                ContentSizeFitter fitter = partGrid.GetComponent<ContentSizeFitter>();
                if (fitter != null)
                {
                    _session.Fitter = fitter;
                    _session.FitterWasEnabled = fitter.enabled;
                    _session.FitterVerticalWas = fitter.verticalFit;
                }

                scrollRect.onValueChanged.AddListener(_session.ScrollHandler);
            }

            _session.CustomCategory = customCategory;
            _session.Total = total;
            _session.CellSize = grid.cellSize;
            _session.PaddingLeft = grid.padding.left;
            _session.PaddingTop = grid.padding.top;
            _session.PaddingBottom = grid.padding.bottom;
            _session.StrideX = grid.cellSize.x + grid.spacing.x;
            _session.StrideY = grid.cellSize.y + grid.spacing.y;
            if (_session.StrideX < 1f)
            {
                _session.StrideX = Mathf.Max(1f, grid.cellSize.x);
            }

            if (_session.StrideY < 1f)
            {
                _session.StrideY = Mathf.Max(1f, grid.cellSize.y);
            }

            _session.Columns = ResolveColumnCount(grid, partGrid);
            CacheIconMetrics(list, _session);
            grid.enabled = false;
            if (_session.Fitter != null)
            {
                _session.Fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                _session.Fitter.enabled = false;
            }

            return _session.Columns > 0;
        }

        private static void CacheIconMetrics(EditorPartList list, RecycleSession session)
        {
            if (session == null || session.IconMetricsReady || list == null)
            {
                return;
            }

            session.IconSize = IconSizeField != null ? (float)IconSizeField.GetValue(list) : 1f;
            session.IconOverScale = IconOverScaleField != null
                ? (float)IconOverScaleField.GetValue(list)
                : 1f;
            session.IconOverSpin = IconOverSpinField != null
                ? (float)IconOverSpinField.GetValue(list)
                : 0f;
            session.IconMetricsReady = true;
        }

        private static int ResolveColumnCount(GridLayoutGroup grid, RectTransform partGrid)
        {
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
                && grid.constraintCount > 0)
            {
                return grid.constraintCount;
            }

            float width = partGrid.rect.width;
            if (width > 1f)
            {
                float inner = width - grid.padding.left - grid.padding.right;
                float stride = grid.cellSize.x + grid.spacing.x;
                if (stride > 0.01f)
                {
                    int cols = Mathf.FloorToInt((inner + grid.spacing.x) / stride);
                    if (cols > 0)
                    {
                        return cols;
                    }
                }
            }

            // Typical stock editor part grid.
            return 4;
        }

        private static void ApplyVirtualContentHeight(RecycleSession session)
        {
            if (session == null || session.PartGrid == null)
            {
                return;
            }

            int rows = (session.Total + session.Columns - 1) / session.Columns;
            float height = session.PaddingTop
                + session.PaddingBottom
                + (rows * session.CellSize.y)
                + (Mathf.Max(0, rows - 1) * (session.StrideY - session.CellSize.y));

            session.PartGrid.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (session.Content != null && session.Content != session.PartGrid)
            {
                session.Content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
        }

        private static void OnVirtualScroll(Vector2 _)
        {
            RecycleSession session = _session;
            if (session == null || session.SuppressScroll || session.List == null)
            {
                return;
            }

            if (!ShouldInterceptPartSearch(session.List))
            {
                EndSession();
                return;
            }

            try
            {
                List<AvailablePart> categoryList =
                    CategoryListField.GetValue(session.List) as List<AvailablePart>;
                if (categoryList == null || categoryList.Count != session.Total)
                {
                    // Filter list changed under us — next UpdatePartIcons will rebuild.
                    return;
                }

                int newStart = ComputeWindowStart(session);
                if (newStart == session.WindowStart)
                {
                    return;
                }

                session.WindowStart = newStart;
                TryBindWindow(
                    session.List,
                    categoryList,
                    session.CustomCategory,
                    resetScrollToTop: false);
            }
            catch (Exception ex)
            {
                EditorBootstrap.LogWarningOnce(
                    "recycle.scrollRebind",
                    "Scroll-recycle rebind failed — ending session: " + ex.Message);
                EndSession();
            }
        }

        private static int ComputeWindowStart(RecycleSession session)
        {
            ScrollRect scroll = session.ScrollRect;
            RectTransform content = session.Content != null ? session.Content : session.PartGrid;
            RectTransform viewport = scroll.viewport != null
                ? scroll.viewport
                : scroll.transform as RectTransform;

            float contentH = content != null ? content.rect.height : 0f;
            float viewH = viewport != null ? viewport.rect.height : 0f;
            float scrollable = Mathf.Max(0f, contentH - viewH);
            float normalized = Mathf.Clamp01(scroll.verticalNormalizedPosition);
            float offset = (1f - normalized) * scrollable;

            int firstRow = session.StrideY > 0.01f
                ? Mathf.FloorToInt(offset / session.StrideY)
                : 0;
            // One-row buffer above the viewport when possible.
            int start = (firstRow - 1) * session.Columns;
            if (start < 0)
            {
                start = 0;
            }

            int maxStart = Math.Max(0, session.Total - ExperimentalV09Gate.MaxVisibleIcons);
            if (start > maxStart)
            {
                start = maxStart;
            }

            // Keep window aligned to the grid.
            start = (start / session.Columns) * session.Columns;
            if (start > maxStart)
            {
                start = maxStart;
            }

            return start;
        }

        private static bool TryBindWindow(
            EditorPartList list,
            List<AvailablePart> categoryList,
            bool customCategory,
            bool resetScrollToTop)
        {
            RecycleSession session = _session;
            if (session == null)
            {
                return false;
            }

            List<EditorPartIcon> icons =
                IconsField.GetValue(list) as List<EditorPartIcon>;
            Dictionary<string, EditorPartIcon> iconCache =
                IconCacheField.GetValue(list) as Dictionary<string, EditorPartIcon>;
            EditorPartIcon partPrefab =
                PartPrefabField.GetValue(list) as EditorPartIcon;

            if (icons == null || iconCache == null || partPrefab == null)
            {
                return false;
            }

            CacheIconMetrics(list, session);
            float iconSize = session.IconSize;
            float iconOverScale = session.IconOverScale;
            float iconOverSpin = session.IconOverSpin;

            session.SuppressScroll = true;
            try
            {
                ClearAllItemsMethod.Invoke(list, null);

                int start = session.WindowStart;
                int end = Math.Min(categoryList.Count, start + ExperimentalV09Gate.MaxVisibleIcons);
                UpdatePartIconArgs[2] = customCategory ? BoxedTrue : BoxedFalse;
                for (int i = start; i < end; i++)
                {
                    AvailablePart part = categoryList[i];
                    if (part == null || string.IsNullOrEmpty(part.name))
                    {
                        continue;
                    }

                    EditorPartIcon icon;
                    if (!iconCache.TryGetValue(part.name, out icon) || icon == null)
                    {
                        icon = UnityEngine.Object.Instantiate(partPrefab);
                        icon.Create(list, part, iconSize, iconOverScale, iconOverSpin);
                        iconCache[part.name] = icon;
                    }

                    UpdatePartIconArgs[0] = icon;
                    UpdatePartIconArgs[1] = part;
                    UpdatePartIconMethod.Invoke(list, UpdatePartIconArgs);
                    icons.Add(icon);
                    PositionIcon(session, icon, i);
                }

                ApplyVirtualContentHeight(session);

                if (resetScrollToTop && session.ScrollRect != null)
                {
                    session.ScrollRect.verticalNormalizedPosition = 1f;
                }

                if (session.LastLoggedWindow != start)
                {
                    session.LastLoggedWindow = start;
                    EditorBootstrap.Log(
                        "Scroll-recycle window start="
                        + start
                        + " showing "
                        + icons.Count
                        + " of "
                        + session.Total);
                }
            }
            finally
            {
                session.SuppressScroll = false;
            }

            return true;
        }

        private static void PositionIcon(RecycleSession session, EditorPartIcon icon, int index)
        {
            if (icon == null)
            {
                return;
            }

            RectTransform rt = icon.transform as RectTransform;
            if (rt == null)
            {
                return;
            }

            int col = index % session.Columns;
            int row = index / session.Columns;
            float x = session.PaddingLeft
                + (col * session.StrideX)
                + (session.CellSize.x * 0.5f);
            float y = -(session.PaddingTop
                + (row * session.StrideY)
                + (session.CellSize.y * 0.5f));

            rt.anchorMin = IconAnchorMin;
            rt.anchorMax = IconAnchorMax;
            rt.pivot = IconPivot;
            rt.sizeDelta = session.CellSize;
            rt.anchoredPosition = new Vector2(x, y);
            rt.localScale = Vector3.one;
        }

        private static void EndSession()
        {
            RecycleSession session = _session;
            if (session == null)
            {
                return;
            }

            _session = null;

            try
            {
                if (session.ScrollRect != null && session.ScrollHandler != null)
                {
                    session.ScrollRect.onValueChanged.RemoveListener(session.ScrollHandler);
                }
            }
            catch
            {
                // ignore teardown listener errors
            }

            try
            {
                if (session.Grid != null)
                {
                    session.Grid.enabled = session.GridWasEnabled;
                }

                if (session.Fitter != null)
                {
                    session.Fitter.verticalFit = session.FitterVerticalWas;
                    session.Fitter.enabled = session.FitterWasEnabled;
                }
            }
            catch
            {
                // ignore teardown layout restore errors
            }
        }
    }
}
