using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PartSearchSuggest
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public sealed class EditorBootstrap : MonoBehaviour
    {
        private static bool _versionBannerLogged;
        private static readonly Dictionary<string, float> WarningOnceKeys =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private const float WarningOnceCooldownSeconds = 30f;

        private EditorSearchHook _hook;

        private void Awake()
        {
            LogVersionBannerOnce();
            StockSearchGuard.ApplyPatches();
            PartsPanelTransitionGuard.ApplyPatches();
            ExperimentalPartsListVirtualizer.ApplyPatches();

            // Fallback only — preferred kick is EditorLoadIndexWatcher (onGameSceneLoadRequested)
            // so indexing finishes on the loading buffer before the hangar is interactive.
            // EnsureBuildStarted is single-flight (no duplicate sync builds).
            if (GameLoadIndexService.ShouldBuildForCurrentScene())
            {
                IndexBuildHost host = IndexBuildHost.Ensure();
                GameLoadIndexService.EnsureBuildStarted(host);
            }
        }

        private void Start()
        {
            Log("Editor scene detected — attaching search UI (indexes built during VAB/SPH load).");
            _hook = gameObject.AddComponent<EditorSearchHook>();
        }

        private void OnDestroy()
        {
            PartsPanelCollapseHelper.ReleaseAllForEditorExit("EditorBootstrap.OnDestroy");
        }

        private static void LogVersionBannerOnce()
        {
            if (_versionBannerLogged)
            {
                return;
            }

            _versionBannerLogged = true;

            // Player-facing label (e.g. 0.8.5.2a) via InformationalVersion; assembly is numeric-only.
            string display = System.Reflection.Assembly
                .GetExecutingAssembly()
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .Select(a => a.InformationalVersion)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(display))
            {
                display = System.Reflection.Assembly
                    .GetExecutingAssembly()
                    .GetName()
                    .Version
                    .ToString();
            }

            LogAlways("Koobal Search Engine v" + display + " active.");
        }

        /// <summary>
        /// Verbose diagnostic log — suppressed unless DebugSettings verbose = true. Use for the
        /// high-volume per-keystroke / per-scene tracing that is only useful when debugging.
        /// </summary>
        internal static void Log(string message)
        {
            if (!DebugSettings.Verbose)
            {
                return;
            }

            Debug.Log("[Koobal] " + message);
        }

        /// <summary>Always-on log for concise, low-volume messages (startup banner, etc.).</summary>
        internal static void LogAlways(string message)
        {
            Debug.Log("[Koobal] " + message);
        }

        internal static void LogWarning(string message)
        {
            Debug.LogWarning("[Koobal] " + message);
        }

        /// <summary>
        /// Rate-limited warning: same key logs at most once per cooldown window.
        /// Use for paths that can fire repeatedly without being useful every time.
        /// </summary>
        internal static void LogWarningOnce(string key, string message)
        {
            if (string.IsNullOrEmpty(key))
            {
                LogWarning(message);
                return;
            }

            float now;
            try
            {
                now = Time.unscaledTime;
            }
            catch
            {
                now = 0f;
            }

            float last;
            if (WarningOnceKeys.TryGetValue(key, out last)
                && now - last < WarningOnceCooldownSeconds
                && last >= 0f)
            {
                return;
            }

            WarningOnceKeys[key] = now;
            LogWarning(message);
        }
    }
}
