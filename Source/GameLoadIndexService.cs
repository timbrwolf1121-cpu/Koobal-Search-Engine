using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;

namespace PartSearchSuggest
{
    /// <summary>
    /// Builds and holds per-save search indexes during the VAB/SPH loading transition —
    /// synchronously while the loading UI is up (or still on the prior scene after
    /// onGameSceneLoadRequested(EDITOR)), not after the hangar is interactive.
    /// Main menu performs no work. Only <see cref="GameLoadBootstrap"/> /
    /// <see cref="EditorBootstrap"/> start builds. Editor UI hook never starts index work.
    /// </summary>
    internal static class GameLoadIndexService
    {
        private static SuggestionIndex _partIndex;
        private static MetadataSuggestionIndex _metadataIndex;
        private static CategorizerSuggestionIndex _categorizerIndex;
        private static string _indexedSaveKey;
        private static bool _basicReady;
        private static bool _fullReady;
        private static bool _buildInProgress;

        /// <summary>
        /// Set when KSP requests a switch to EDITOR — allows indexing to start on the
        /// outgoing scene / loading buffer before the hangar camera is interactive.
        /// </summary>
        private static bool _editorLoadRequested;

        internal static SuggestionIndex PartIndex => _partIndex;

        internal static MetadataSuggestionIndex MetadataIndex => _metadataIndex;

        internal static CategorizerSuggestionIndex CategorizerIndex => _categorizerIndex;

        internal static bool IsBasicReady => _basicReady;

        internal static bool IsFullReady => _fullReady;

        internal static bool IsBuildInProgress => _buildInProgress;

        /// <summary>
        /// Editor load builds synchronously (see BuildIfNeeded). Any leftover coroutine
        /// path must never yield mid-index in EDITOR — returning a huge batch suppresses
        /// frame slices so work cannot spill into the interactive hangar.
        /// </summary>
        internal static int EffectiveFrameSliceBatchSize(int baseline)
        {
            if (HighLogic.LoadedScene == GameScenes.EDITOR || _editorLoadRequested)
            {
                return Math.Max(baseline, 1) * 100000;
            }

            return baseline;
        }

        /// <summary>
        /// Called from <see cref="GameEvents.onGameSceneLoadRequested"/> when target is EDITOR.
        /// Starts indexing before the hangar is shown.
        /// </summary>
        internal static void NotifyEditorLoadRequested()
        {
            if (HighLogic.CurrentGame == null)
            {
                return;
            }

            _editorLoadRequested = true;
        }

        /// <summary>
        /// True when indexes are still needed and we are either already in EDITOR or an
        /// EDITOR scene load has been requested (prior scene / loading buffer).
        /// </summary>
        internal static bool ShouldBuildForCurrentScene()
        {
            if (HighLogic.CurrentGame == null)
            {
                return false;
            }

            GameScenes scene = HighLogic.LoadedScene;
            if (scene == GameScenes.MAINMENU)
            {
                return false;
            }

            if (scene != GameScenes.EDITOR && !_editorLoadRequested)
            {
                return false;
            }

            string saveKey = GetSaveKey();
            if (string.IsNullOrEmpty(saveKey))
            {
                return false;
            }

            if (IsReadyForSave(saveKey))
            {
                return false;
            }

            return true;
        }

        internal static void InvalidateForMainMenu()
        {
            _buildInProgress = false;
            _editorLoadRequested = false;
            _partIndex = null;
            _metadataIndex = null;
            _categorizerIndex = null;
            _indexedSaveKey = null;
            _basicReady = false;
            _fullReady = false;
            EditorPartAvailability.Invalidate();
            EditorBootstrap.Log("Main menu — search indexes cleared (no indexing on main menu).");
        }

        /// <summary>
        /// Kick a single-flight build on the persistent host if needed.
        /// Safe to call from multiple addons — duplicates no-op while a build runs.
        /// When PartLoader is already ready, builds synchronously in this call (loading
        /// buffer / scene-load-requested) so the hitch cannot slip past hangar open.
        /// </summary>
        internal static void EnsureBuildStarted(MonoBehaviour host)
        {
            if (!ShouldBuildForCurrentScene())
            {
                return;
            }

            if (_buildInProgress)
            {
                return;
            }

            string saveKey = GetSaveKey();
            if (string.IsNullOrEmpty(saveKey) || IsReadyForSave(saveKey))
            {
                return;
            }

            if (PartLoader.Instance != null && PartLoader.Instance.loadedParts != null)
            {
                _buildInProgress = true;
                try
                {
                    _basicReady = false;
                    _fullReady = false;
                    _partIndex = null;
                    _metadataIndex = null;
                    _categorizerIndex = null;

                    EditorBootstrap.LogAlways(
                        "Building search index during VAB/SPH loading transition (sync)...");
                    RunSyncBuild(saveKey);
                }
                finally
                {
                    _buildInProgress = false;
                    if (_fullReady)
                    {
                        _editorLoadRequested = false;
                    }
                }

                return;
            }

            if (host == null)
            {
                host = IndexBuildHost.Ensure();
            }

            host.StartCoroutine(BuildIfNeeded(host));
        }

        internal static IEnumerator BuildIfNeeded(MonoBehaviour host)
        {
            // Single-flight: never clear this lock just because !_fullReady.
            // The previous "stale lock" clear raced WaitUntil yields and started 4× sync builds
            // (~14s each) after the VAB was already visible.
            if (_buildInProgress)
            {
                yield break;
            }

            string saveKey = GetSaveKey();
            if (string.IsNullOrEmpty(saveKey) || IsReadyForSave(saveKey))
            {
                yield break;
            }

            _buildInProgress = true;
            try
            {
                _basicReady = false;
                _fullReady = false;
                _partIndex = null;
                _metadataIndex = null;
                _categorizerIndex = null;

                EditorBootstrap.LogAlways(
                    "Building search index during VAB/SPH loading transition (sync)...");

                // Wait for PartLoader under the lock so no second coroutine enters RunSyncBuild.
                while (PartLoader.Instance == null || PartLoader.Instance.loadedParts == null)
                {
                    yield return null;
                }

                // Zero yields from here — hitch lands on loading buffer / prior scene,
                // not after hangar UI is interactive.
                RunSyncBuild(saveKey);
            }
            finally
            {
                _buildInProgress = false;
                if (_fullReady)
                {
                    _editorLoadRequested = false;
                }
            }
        }

        private static void RunSyncBuild(string saveKey)
        {
            ModMetadataCache.Build();

            EditorPartAvailability.Invalidate();
            var availabilityStopwatch = Stopwatch.StartNew();
            EditorPartAvailability.WarmCache();
            availabilityStopwatch.Stop();
            EditorBootstrap.Log(
                "Editor part availability cache warmed ("
                + EditorPartAvailability.CountLoadedEditorParts()
                + " parts) in "
                + availabilityStopwatch.ElapsedMilliseconds
                + "ms.");

            _partIndex = new SuggestionIndex();
            var suggestionStopwatch = Stopwatch.StartNew();
            _partIndex.Build();
            suggestionStopwatch.Stop();
            EditorBootstrap.Log("SuggestionIndex complete in " + suggestionStopwatch.ElapsedMilliseconds + "ms.");

            _basicReady = true;
            _indexedSaveKey = saveKey;
            EditorBootstrap.Log("Search ready (basic)");

            _metadataIndex = new MetadataSuggestionIndex();
            var metadataStopwatch = Stopwatch.StartNew();
            _metadataIndex.Build();
            metadataStopwatch.Stop();
            EditorBootstrap.LogAlways(
                "MetadataSuggestionIndex complete in "
                + metadataStopwatch.ElapsedMilliseconds
                + "ms.");

            _categorizerIndex = new CategorizerSuggestionIndex();
            var categorizerStopwatch = Stopwatch.StartNew();
            _categorizerIndex.Build();
            categorizerStopwatch.Stop();
            EditorBootstrap.LogAlways(
                "CategorizerSuggestionIndex complete ("
                + _categorizerIndex.EntryCount
                + " entries) in "
                + categorizerStopwatch.ElapsedMilliseconds
                + "ms.");

            if (_categorizerIndex.EntryCount <= 0)
            {
                EditorBootstrap.LogWarning(
                    "Categorizer suggestion index is empty — stock Function/Category rows will be missing.");
            }

            _fullReady = true;
            IndexDebugDump.LogIfEnabled(_partIndex, _metadataIndex, _categorizerIndex);
            EditorBootstrap.LogAlways("Search ready (full)");
        }

        /// <summary>
        /// Wait-only. Never starts index builds (hangar interactive path must stay free of load work).
        /// <see cref="GameLoadBootstrap"/> is the sole build initiator.
        /// </summary>
        internal static IEnumerator WaitUntilBasicReady(MonoBehaviour host)
        {
            if (_basicReady)
            {
                yield break;
            }

            float timeout = 120f;
            float elapsed = 0f;
            while (!_basicReady && elapsed < timeout)
            {
                elapsed += UnityEngine.Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_basicReady)
            {
                EditorBootstrap.LogWarning(
                    "Editor-load basic index not ready after wait — dropdown disabled (no interactive rebuild).");
            }
        }

        /// <summary>Wait-only for full metadata/categorizer indexes. Does not start builds.</summary>
        internal static IEnumerator WaitUntilFullReady(MonoBehaviour host)
        {
            if (_fullReady)
            {
                yield break;
            }

            yield return WaitUntilBasicReady(host);

            float timeout = 120f;
            float elapsed = 0f;
            while (!_fullReady && elapsed < timeout)
            {
                elapsed += UnityEngine.Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static bool IsReadyForSave(string saveKey)
        {
            // Require full readiness — basic-only would skip metadata/categorizer forever after a
            // mid-build scene transition, leaving stock Function/Category suggestions empty.
            return _fullReady
                && _basicReady
                && _categorizerIndex != null
                && _categorizerIndex.EntryCount > 0
                && !string.IsNullOrEmpty(_indexedSaveKey)
                && string.Equals(_indexedSaveKey, saveKey, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSaveKey()
        {
            Game game = HighLogic.CurrentGame;
            if (game == null)
            {
                return null;
            }

            string title = game.Title ?? string.Empty;
            string folder = HighLogic.SaveFolder ?? string.Empty;
            return folder + "|" + title;
        }
    }
}
