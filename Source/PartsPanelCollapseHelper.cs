using System;
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

        // Stock TransitionImmediate(Out→In) StopCoroutine's the slide without clearing
        // Transitioning / updating State — leaves PanelTransitionIn waiting forever and
        // SetInteractable(false) stuck. We snap via reflection instead.
        private static readonly PropertyInfo StateProperty =
            typeof(UIPanelTransition).GetProperty(
                "State",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly PropertyInfo TransitioningProperty =
            typeof(UIPanelTransition).GetProperty(
                "Transitioning",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo PanelCoroutineField =
            typeof(UIPanelTransition).GetField(
                "panelCoroutine",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo SetInteractableMethod =
            typeof(UITransitionBase).GetMethod(
                "SetInteractable",
                BindingFlags.Instance | BindingFlags.NonPublic);

        public static void NotifyDropdownOpen(bool open)
        {
            bool wasOpen = _dropdownOpen;
            _dropdownOpen = open;
            if (wasOpen && !open)
            {
                PartsPanelTransitionGuard.NotifyDropdownClosed();
            }
        }

        /// <summary>
        /// True when partsEditor is mid-slide (or still marked collapsed). Search apply that
        /// touches SearchStop/Refresh must wait until this is false or use ForceComplete.
        /// </summary>
        public static bool IsPartsEditorBusy()
        {
            if (_collapsed)
            {
                return true;
            }

            UIPanelTransition transition = GetPartsEditorTransition();
            return transition != null && transition.Transitioning;
        }

        /// <summary>
        /// Snap partsEditor to In and clear stuck Transitioning/interactable state.
        /// Do not use stock TransitionImmediate for Out→In — it StopCoroutine's without
        /// setting State or Transitioning=false (permanent UI hang).
        /// </summary>
        public static void ForceCompletePartsEditorIn(string reason)
        {
            using (PartsPanelTransitionGuard.EnterForceAllowInScope())
            {
                UIPanelTransition transition = _partsEditorTransition ?? GetPartsEditorTransition();
                if (transition == null)
                {
                    EditorBootstrap.Log(
                        "PartsPanelCollapse: ForceCompletePartsEditorIn("
                        + (reason ?? "unknown")
                        + ") skipped — no partsEditor.");
                    return;
                }

                try
                {
                    SnapPartsEditorToState(transition, StateIn);
                }
                catch (Exception ex)
                {
                    EditorBootstrap.LogWarning(
                        "PartsPanelCollapse: ForceCompletePartsEditorIn failed — " + ex.Message);
                }

                EditorBootstrap.Log(
                    "PartsPanelCollapse: ForceCompletePartsEditorIn("
                    + (reason ?? "unknown")
                    + ") state="
                    + (transition.State ?? "null")
                    + " transitioning="
                    + transition.Transitioning
                    + ".");
            }
        }

        private static void SnapPartsEditorToState(UIPanelTransition transition, string stateName)
        {
            if (transition == null || string.IsNullOrEmpty(stateName))
            {
                return;
            }

            Coroutine routine = PanelCoroutineField != null
                ? PanelCoroutineField.GetValue(transition) as Coroutine
                : null;
            if (routine != null)
            {
                transition.StopCoroutine(routine);
                PanelCoroutineField.SetValue(transition, null);
            }

            Vector2? position = GetStatePosition(transition, stateName);
            if (position != null && transition.panelTransform != null)
            {
                transition.panelTransform.anchoredPosition = position.Value;
            }

            if (StateProperty != null && StateProperty.CanWrite)
            {
                StateProperty.SetValue(transition, stateName, null);
            }

            if (TransitioningProperty != null && TransitioningProperty.CanWrite)
            {
                TransitioningProperty.SetValue(transition, false, null);
            }

            // Restore input after lockWhileTransitioning may have disabled it mid-slide.
            if (SetInteractableMethod != null)
            {
                bool inputLock = false;
                if (transition.states != null)
                {
                    for (int i = 0; i < transition.states.Length; i++)
                    {
                        if (transition.states[i].name == stateName)
                        {
                            inputLock = transition.states[i].inputLock;
                            break;
                        }
                    }
                }

                SetInteractableMethod.Invoke(transition, new object[] { !inputLock });
            }
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

        /// <summary>
        /// While the dropdown keeps partsEditor slid Out, stock may leave ctrlGroup.interactable
        /// false (Out.inputLock). That breaks TMP caret arrows on the search field which lives
        /// on the same panel — force interactable back on after collapse.
        /// </summary>
        public static void EnsureSearchFieldInteractableWhileCollapsed()
        {
            if (!_collapsed)
            {
                return;
            }

            UIPanelTransition transition = _partsEditorTransition ?? GetPartsEditorTransition();
            if (transition == null || SetInteractableMethod == null)
            {
                return;
            }

            try
            {
                SetInteractableMethod.Invoke(transition, new object[] { true });
            }
            catch (Exception ex)
            {
                EditorBootstrap.LogWarning(
                    "PartsPanelCollapse: EnsureSearchFieldInteractableWhileCollapsed failed — "
                    + ex.Message);
            }
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
                EnsureSearchFieldInteractableWhileCollapsed();
                return;
            }

            if (TryCollapseViaDirectPosition())
            {
                _collapsed = true;
                EnsureSearchFieldInteractableWhileCollapsed();
                return;
            }

            if (TryCollapseViaHideFallback())
            {
                _collapsed = true;
                EnsureSearchFieldInteractableWhileCollapsed();
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

            // Never use stock TransitionImmediate for Out→In (leaves Transitioning stuck).
            // Snap In under force-allow so the guard cannot swallow restore.
            string target =
                (_savedTransitionState == StateIn || string.IsNullOrEmpty(_savedTransitionState))
                    ? StateIn
                    : _savedTransitionState;

            if (string.Equals(target, StateIn, StringComparison.Ordinal))
            {
                SnapPartsEditorToState(_partsEditorTransition, StateIn);
            }
            else
            {
                using (PartsPanelTransitionGuard.EnterForceAllowInScope())
                {
                    SnapPartsEditorToState(_partsEditorTransition, target);
                }
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
