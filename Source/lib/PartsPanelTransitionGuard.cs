using System;
using HarmonyLib;
using KSP.UI;
using UnityEngine;

namespace PartSearchSuggest
{
    /// <summary>
    /// Blocks stock partsEditor Transition("In") while the search dropdown is open so the panel
    /// does not slide back during an active suggestion session.
    ///
    /// Smoking gun for permanent freezes: silently swallowing Transition("In") leaves
    /// UIPanelTransition.State at "Out" and can leave Transitioning stuck if a prior
    /// coroutine was interrupted. Stock UIPanelTransitionManager.PanelTransitionIn then
    /// waits forever on AnyPanelsTransitioning(). Fail-safe: force-allow scopes, pending
    /// replay, and a hard unscaled-time timeout that stops blocking forever.
    /// </summary>
    internal static class PartsPanelTransitionGuard
    {
        private const string StateIn = "In";
        private const float MaxBlockSeconds = 2f;

        private static int _forceAllowInDepth;
        private static float _blockWindowStart = -1f;
        private static UIPanelTransition _pendingInPanel;
        private static Action _pendingInFinished;

        internal static bool IsForceAllowingIn => _forceAllowInDepth > 0;

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

        internal static void BeginForceAllowIn()
        {
            _forceAllowInDepth++;
            _blockWindowStart = -1f;
            FlushPendingIn("force-allow-begin");
        }

        internal static void EndForceAllowIn()
        {
            if (_forceAllowInDepth > 0)
            {
                _forceAllowInDepth--;
            }

            if (_forceAllowInDepth == 0)
            {
                FlushPendingIn("force-allow-end");
            }
        }

        internal readonly struct ForceAllowInScope : IDisposable
        {
            private readonly bool _active;

            internal ForceAllowInScope(bool active)
            {
                _active = active;
                if (_active)
                {
                    BeginForceAllowIn();
                }
            }

            public void Dispose()
            {
                if (_active)
                {
                    EndForceAllowIn();
                }
            }
        }

        internal static ForceAllowInScope EnterForceAllowInScope()
        {
            return new ForceAllowInScope(true);
        }

        /// <summary>
        /// Called when the dropdown closes so any swallowed Transition("In") is replayed
        /// immediately (not left stuck Out forever).
        /// </summary>
        internal static void NotifyDropdownClosed()
        {
            _blockWindowStart = -1f;
            FlushPendingIn("dropdown-closed");
        }

        private static void FlushPendingIn(string reason)
        {
            UIPanelTransition panel = _pendingInPanel;
            Action finished = _pendingInFinished;
            _pendingInPanel = null;
            _pendingInFinished = null;

            if (panel == null)
            {
                return;
            }

            try
            {
                EditorBootstrap.Log(
                    "PartsPanelCollapse: replaying queued partsEditor Transition(In) after "
                    + reason
                    + ".");
                // Use our snap helper — stock TransitionImmediate leaves Transitioning stuck.
                PartsPanelCollapseHelper.ForceCompletePartsEditorIn("queued-" + reason);
                finished?.Invoke();
            }
            catch (Exception ex)
            {
                EditorBootstrap.LogWarning(
                    "PartsPanelCollapse: pending Transition(In) replay failed — " + ex.Message);
            }
        }

        private static bool ShouldBlockPartsEditorOpen(UIPanelTransition instance, string stateName)
        {
            if (_forceAllowInDepth > 0)
            {
                return false;
            }

            if (!PartsPanelCollapseHelper.IsDropdownOpen)
            {
                _blockWindowStart = -1f;
                return false;
            }

            if (!PartsPanelCollapseHelper.IsPartsEditorTransition(instance))
            {
                return false;
            }

            if (!string.Equals(stateName, StateIn, StringComparison.Ordinal))
            {
                return false;
            }

            if (_blockWindowStart < 0f)
            {
                _blockWindowStart = Time.unscaledTime;
            }
            else if (Time.unscaledTime - _blockWindowStart >= MaxBlockSeconds)
            {
                EditorBootstrap.LogWarning(
                    "PartsPanelCollapse: Transition(In) block timed out after "
                    + MaxBlockSeconds.ToString("F1")
                    + "s — fail-open (clearing dropdown hold).");
                PartsPanelCollapseHelper.NotifyDropdownOpen(false);
                _blockWindowStart = -1f;
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(UIPanelTransition), nameof(UIPanelTransition.Transition), typeof(string), typeof(Action))]
        private static class TransitionPatch
        {
            private static bool Prefix(UIPanelTransition __instance, string stateName, Action onFinished)
            {
                if (!ShouldBlockPartsEditorOpen(__instance, stateName))
                {
                    return true;
                }

                _pendingInPanel = __instance;
                if (onFinished != null)
                {
                    _pendingInFinished = (Action)Delegate.Combine(_pendingInFinished, onFinished);
                }

                EditorBootstrap.Log(
                    "PartsPanelCollapse: blocked stock partsEditor transition to "
                    + (stateName ?? "null")
                    + " while dropdown open (queued for replay).");
                return false;
            }
        }

        [HarmonyPatch(typeof(UIPanelTransition), nameof(UIPanelTransition.TransitionImmediate), typeof(string), typeof(Action))]
        private static class TransitionImmediatePatch
        {
            private static bool Prefix(UIPanelTransition __instance, string stateName, Action onFinished)
            {
                if (!ShouldBlockPartsEditorOpen(__instance, stateName))
                {
                    return true;
                }

                _pendingInPanel = __instance;
                if (onFinished != null)
                {
                    _pendingInFinished = (Action)Delegate.Combine(_pendingInFinished, onFinished);
                }

                EditorBootstrap.Log(
                    "PartsPanelCollapse: blocked stock partsEditor TransitionImmediate(In) while dropdown open (queued).");
                return false;
            }
        }
    }
}
