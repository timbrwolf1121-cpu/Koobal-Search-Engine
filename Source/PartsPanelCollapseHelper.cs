using System.Collections.Generic;
using System.Reflection;
using System.Text;
using KSP.UI;
using KSP.UI.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace PartSearchSuggest
{
    internal static class PartsPanelCollapseHelper
    {
        private const string ApproachPartsEditorTransition = "PartsEditorTransition";
        private const string ApproachDirectPosition = "DirectPosition";
        private const string ApproachHideFallback = "HideFallback";

        private const string StateIn = "In";
        private const string StateOut = "Out";
        private const float PositionVerifyMinDelta = 25f;

        private static bool _collapsed;
        private static string _approach = string.Empty;
        private static bool _loggedDiscovery;
        private static bool _dropdownOpen;

        public static bool IsDropdownOpen => _dropdownOpen;

        public static bool IsCollapsed => _collapsed;

        public static string Approach => _approach;

        private static UIPanelTransition _partsEditorTransition;
        private static string _savedTransitionState;

        private static RectTransform _panelTransform;
        private static Vector2 _savedAnchoredPosition;

        private static CanvasGroup _canvasGroup;
        private static float _savedCanvasAlpha;
        private static bool _savedCanvasBlocksRaycasts;
        private static bool _savedCanvasInteractable;

        public static void NotifyDropdownOpen(bool open)
        {
            _dropdownOpen = open;
        }

        /// <summary>
        /// Clears dropdown tracking when the editor scene unloads. Does not call Restore() —
        /// stock teardown owns panel and camera transitions on VAB/SPH exit.
        /// </summary>
        public static void ReleaseAllForEditorExit(string reason)
        {
            bool wasCollapsed = _collapsed;

            _dropdownOpen = false;
            ResetState();

            EditorBootstrap.Log(
                "PartsPanelCollapse: ReleaseAllForEditorExit("
                + (reason ?? "unknown")
                + ") wasCollapsed="
                + wasCollapsed
                + ".");
        }

        public static void Collapse()
        {
            if (_collapsed)
            {
                return;
            }

            LogDiscoveryOnce();

            if (TryCollapseViaPartsEditorTransition())
            {
                _collapsed = true;
                return;
            }

            if (TryCollapseViaDirectPosition())
            {
                _collapsed = true;
                return;
            }

            if (TryCollapseViaHideFallback())
            {
                _collapsed = true;
                return;
            }

            EditorBootstrap.LogWarning(
                "PartsPanelCollapse: all methods failed — forcing HideFallback on scroll rect.");
            if (TryEmergencyHideFallback())
            {
                _collapsed = true;
            }
        }

        public static void RestoreIfDropdownClosed(bool dropdownClosed)
        {
            if (!dropdownClosed)
            {
                EditorBootstrap.Log("PartsPanelCollapse: Restore suppressed — dropdown still open.");
                return;
            }

            Restore();
        }

        public static void Restore()
        {
            if (!_collapsed)
            {
                EditorBootstrap.Log("PartsPanelCollapse: Restore skipped — not collapsed.");
                return;
            }

            switch (_approach)
            {
                case ApproachPartsEditorTransition:
                    RestorePartsEditorTransition();
                    break;
                case ApproachDirectPosition:
                    RestoreDirectPosition();
                    break;
                case ApproachHideFallback:
                    RestoreHideFallback();
                    break;
            }

            EditorBootstrap.Log(
                "PartsPanelCollapse: restored via "
                + _approach
                + ", target="
                + GetTransformPath(_panelTransform)
                + ", transitionState="
                + (_partsEditorTransition != null ? _partsEditorTransition.State : "n/a")
                + ".");

            ResetState();
        }

        internal static bool IsPartsEditorTransition(UIPanelTransition transition)
        {
            if (transition == null)
            {
                return false;
            }

            EditorPanels panels = EditorPanels.Instance;
            return panels != null && ReferenceEquals(panels.partsEditor, transition);
        }

        private static void ResetState()
        {
            _collapsed = false;
            _approach = string.Empty;
            _partsEditorTransition = null;
            _savedTransitionState = null;
            _panelTransform = null;
            _canvasGroup = null;
        }

        private static bool TryCollapseViaPartsEditorTransition()
        {
            UIPanelTransition transition = GetPartsEditorTransition();
            if (transition == null)
            {
                EditorBootstrap.Log("PartsPanelCollapse: PartsEditorTransition skipped — partsEditor is null.");
                return false;
            }

            _partsEditorTransition = transition;
            _panelTransform = transition.panelTransform;
            _savedTransitionState = transition.State ?? StateIn;

            RectTransform panel = _panelTransform;
            Vector2 beforePos = panel != null ? panel.anchoredPosition : Vector2.zero;
            float beforeWorldMinX = panel != null ? GetWorldMinX(panel) : 0f;
            string beforeState = _savedTransitionState;

            if (beforeState == StateOut)
            {
                _approach = ApproachPartsEditorTransition;
                EditorBootstrap.Log(
                    "PartsPanelCollapse: Collapse method="
                    + _approach
                    + " target="
                    + GetTransformPath(panel)
                    + " beforeState="
                    + beforeState
                    + " afterState="
                    + beforeState
                    + " visible=false (already Out)");
                return true;
            }

            transition.Transition(StateOut);

            string afterState = transition.State ?? string.Empty;
            Vector2 afterPos = panel != null ? panel.anchoredPosition : Vector2.zero;
            float afterWorldMinX = panel != null ? GetWorldMinX(panel) : 0f;
            bool visible = IsPanelStillVisible(beforePos, afterPos, beforeWorldMinX, afterWorldMinX);
            bool success = afterState == StateOut || transition.Transitioning || !visible;

            if (success)
            {
                _approach = ApproachPartsEditorTransition;
                EditorBootstrap.Log(
                    "PartsPanelCollapse: Collapse method="
                    + _approach
                    + " target="
                    + GetTransformPath(panel)
                    + " beforeState="
                    + beforeState
                    + " afterState="
                    + afterState
                    + " beforePos="
                    + beforePos
                    + " afterPos="
                    + afterPos
                    + " transitioning="
                    + transition.Transitioning
                    + " visible="
                    + visible);
                return true;
            }

            EditorBootstrap.Log(
                "PartsPanelCollapse: PartsEditorTransition rejected — state="
                + afterState
                + " anchoredDelta="
                + Mathf.Abs(beforePos.x - afterPos.x).ToString("F1")
                + " worldDelta="
                + (beforeWorldMinX - afterWorldMinX).ToString("F1")
                + ", trying DirectPosition.");
            _partsEditorTransition = null;
            _panelTransform = null;
            _savedTransitionState = null;
            return false;
        }

        private static bool TryCollapseViaDirectPosition()
        {
            UIPanelTransition transition = GetPartsEditorTransition();
            if (transition == null || transition.panelTransform == null)
            {
                EditorBootstrap.Log("PartsPanelCollapse: DirectPosition skipped — partsEditor panelTransform is null.");
                return false;
            }

            Vector2? outPosition = GetStatePosition(transition, StateOut);
            if (outPosition == null)
            {
                EditorBootstrap.Log("PartsPanelCollapse: DirectPosition skipped — no Out state on partsEditor.");
                return false;
            }

            _partsEditorTransition = transition;
            _panelTransform = transition.panelTransform;
            _savedAnchoredPosition = _panelTransform.anchoredPosition;

            Vector2 beforePos = _savedAnchoredPosition;
            float beforeWorldMinX = GetWorldMinX(_panelTransform);

            _panelTransform.anchoredPosition = outPosition.Value;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelTransform);

            Vector2 afterPos = _panelTransform.anchoredPosition;
            float afterWorldMinX = GetWorldMinX(_panelTransform);
            bool visible = IsPanelStillVisible(beforePos, afterPos, beforeWorldMinX, afterWorldMinX);

            if (!visible)
            {
                _approach = ApproachDirectPosition;
                EditorBootstrap.Log(
                    "PartsPanelCollapse: Collapse method="
                    + _approach
                    + " target="
                    + GetTransformPath(_panelTransform)
                    + " beforePos="
                    + beforePos
                    + " afterPos="
                    + afterPos
                    + " outState="
                    + outPosition.Value
                    + " visible=false");
                return true;
            }

            _panelTransform.anchoredPosition = _savedAnchoredPosition;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelTransform);
            _partsEditorTransition = null;
            _panelTransform = null;

            EditorBootstrap.Log(
                "PartsPanelCollapse: DirectPosition rejected target="
                + GetTransformPath(transition.panelTransform)
                + " anchoredDelta="
                + Mathf.Abs(beforePos.x - afterPos.x).ToString("F1")
                + " worldDelta="
                + (beforeWorldMinX - afterWorldMinX).ToString("F1")
                + ", trying HideFallback.");
            return false;
        }

        private static bool TryCollapseViaHideFallback()
        {
            RectTransform target = ResolveHideFallbackTarget();
            if (target == null)
            {
                EditorBootstrap.Log("PartsPanelCollapse: HideFallback skipped — no hide target found.");
                return false;
            }

            return ApplyHideFallback(target);
        }

        private static bool TryEmergencyHideFallback()
        {
            EditorPartList partList = EditorPartList.Instance;
            if (partList == null || partList.partListScrollRect == null)
            {
                return false;
            }

            return ApplyHideFallback(partList.partListScrollRect.transform as RectTransform);
        }

        private static bool ApplyHideFallback(RectTransform target)
        {
            _panelTransform = target;
            _canvasGroup = target.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
            }

            _savedCanvasAlpha = _canvasGroup.alpha;
            _savedCanvasBlocksRaycasts = _canvasGroup.blocksRaycasts;
            _savedCanvasInteractable = _canvasGroup.interactable;

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            _approach = ApproachHideFallback;
            EditorBootstrap.Log(
                "PartsPanelCollapse: Collapse method="
                + _approach
                + " target="
                + GetTransformPath(target)
                + " before="
                + FormatRectSnapshot(target.anchoredPosition, target.offsetMin, target.offsetMax)
                + " after="
                + FormatRectSnapshot(target.anchoredPosition, target.offsetMin, target.offsetMax)
                + " visible=false");
            return true;
        }

        private static void RestorePartsEditorTransition()
        {
            if (_partsEditorTransition == null)
            {
                return;
            }

            if (_savedTransitionState == StateIn || string.IsNullOrEmpty(_savedTransitionState))
            {
                _partsEditorTransition.Transition(StateIn);
            }
            else
            {
                _partsEditorTransition.Transition(_savedTransitionState);
            }
        }

        private static void RestoreDirectPosition()
        {
            if (_panelTransform == null)
            {
                return;
            }

            _panelTransform.anchoredPosition = _savedAnchoredPosition;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelTransform);
        }

        private static void RestoreHideFallback()
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = _savedCanvasAlpha;
            _canvasGroup.blocksRaycasts = _savedCanvasBlocksRaycasts;
            _canvasGroup.interactable = _savedCanvasInteractable;
        }

        private static UIPanelTransition GetPartsEditorTransition()
        {
            EditorPanels panels = EditorPanels.Instance;
            return panels != null ? panels.partsEditor : null;
        }

        private static Vector2? GetStatePosition(UIPanelTransition transition, string stateName)
        {
            if (transition == null || transition.states == null)
            {
                return null;
            }

            for (int i = 0; i < transition.states.Length; i++)
            {
                UIPanelTransition.PanelPosition state = transition.states[i];
                if (state.name == stateName)
                {
                    return state.position;
                }
            }

            return null;
        }

        private static bool IsPanelStillVisible(
            Vector2 beforePos,
            Vector2 afterPos,
            float beforeWorldMinX,
            float afterWorldMinX)
        {
            float anchoredDelta = Mathf.Abs(beforePos.x - afterPos.x);
            float worldDelta = beforeWorldMinX - afterWorldMinX;
            return anchoredDelta < PositionVerifyMinDelta && worldDelta < PositionVerifyMinDelta;
        }

        private static RectTransform ResolveHideFallbackTarget()
        {
            UIPanelTransition transition = GetPartsEditorTransition();
            if (transition != null && transition.panelTransform != null)
            {
                return transition.panelTransform;
            }

            EditorPartList partList = EditorPartList.Instance;
            if (partList != null && partList.partListScrollRect != null)
            {
                return partList.partListScrollRect.transform as RectTransform;
            }

            return null;
        }

        private static void LogDiscoveryOnce()
        {
            if (_loggedDiscovery)
            {
                return;
            }

            _loggedDiscovery = true;

            EditorPanels panels = EditorPanels.Instance;
            if (panels != null)
            {
                LogAllEditorPanelTransitions(panels);
                LogTransitionStates("partsEditor", panels.partsEditor);
                LogTransitionStates("partsEditorModes", panels.partsEditorModes);
                LogTransitionStates("partcategorizerModes", panels.partcategorizerModes);
            }

            UIPanelTransition partsEditor = GetPartsEditorTransition();
            EditorBootstrap.Log(
                "PartsPanelCollapse: partsEditor panel="
                + GetTransformPath(partsEditor != null ? partsEditor.panelTransform : null)
                + ", currentState="
                + (partsEditor != null ? partsEditor.State : "null"));
        }

        private static void LogAllEditorPanelTransitions(EditorPanels panels)
        {
            FieldInfo[] fields = typeof(EditorPanels).GetFields(BindingFlags.Public | BindingFlags.Instance);
            StringBuilder summary = new StringBuilder("PartsPanelCollapse: EditorPanels UIPanelTransition fields=");
            bool first = true;
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType != typeof(UIPanelTransition))
                {
                    continue;
                }

                UIPanelTransition transition = field.GetValue(panels) as UIPanelTransition;
                if (!first)
                {
                    summary.Append(" | ");
                }

                first = false;
                summary.Append(field.Name)
                    .Append("=")
                    .Append(GetTransformPath(transition != null ? transition.panelTransform : null))
                    .Append(" state=")
                    .Append(transition != null ? transition.State : "null");
            }

            EditorBootstrap.Log(summary.ToString());
        }

        private static void LogTransitionStates(string label, UIPanelTransition transition)
        {
            if (transition == null || transition.states == null)
            {
                EditorBootstrap.Log("PartsPanelCollapse: " + label + " is null or has no states.");
                return;
            }

            for (int i = 0; i < transition.states.Length; i++)
            {
                UIPanelTransition.PanelPosition state = transition.states[i];
                EditorBootstrap.Log(
                    "PartsPanelCollapse: "
                    + label
                    + " state["
                    + i
                    + "] name='"
                    + (state.name ?? string.Empty)
                    + "', position="
                    + state.position
                    + ", panelTransform="
                    + GetTransformPath(transition.panelTransform)
                    + ", currentState='"
                    + (transition.State ?? string.Empty)
                    + "'.");
            }
        }

        private static string FormatRectSnapshot(Vector2 anchoredPosition, Vector2 offsetMin, Vector2 offsetMax)
        {
            return "("
                + anchoredPosition.x.ToString("F1")
                + ","
                + anchoredPosition.y.ToString("F1")
                + ",omin="
                + offsetMin
                + ",omax="
                + offsetMax
                + ")";
        }

        private static float GetWorldMinX(RectTransform target)
        {
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            float minX = corners[0].x;
            for (int i = 1; i < corners.Length; i++)
            {
                if (corners[i].x < minX)
                {
                    minX = corners[i].x;
                }
            }

            return minX;
        }

        private static string GetTransformPath(RectTransform rect)
        {
            if (rect == null)
            {
                return "null";
            }

            StringBuilder path = new StringBuilder(rect.name);
            Transform current = rect.parent;
            while (current != null)
            {
                path.Insert(0, current.name + "/");
                current = current.parent;
            }

            return path.ToString();
        }
    }
}
