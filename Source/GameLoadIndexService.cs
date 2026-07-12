using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;

namespace PartSearchSuggest
{
    /// <summary>
    /// Builds and holds per-save search indexes during the post-save-selection loading screen.
    /// Main menu performs no work. Only <see cref="GameLoadBootstrap"/> starts builds.
    /// Editor entry consumes pre-built indexes (UI hook only — never starts index work).
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

        internal static SuggestionIndex PartIndex => _partIndex;

        internal static MetadataSuggestionIndex MetadataIndex => _metadataIndex;

        internal static CategorizerSuggestionIndex CategorizerIndex => _categorizerIndex;

        internal static bool IsBasicReady => _basicReady;

        internal static bool IsFullReady => _fullReady;

        /// <summary>
        /// True for first post-save scene that still needs indexes. EDITOR is included only so
        /// save-resume into VAB/SPH can finish indexing on that load screen — not for KSC→hangar
        /// re-entry (IsReadyForSave short-circuits once built).
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

            string saveKey = GetSaveKey();
            if (string.IsNullOrEmpty(saveKey))
            {
                return false;
            }

            if (IsReadyForSave(saveKey))
            {
                return false;
            }

            return scene == GameScenes.SPACECENTER
                || scene == GameScenes.FLIGHT
                || scene == GameScenes.TRACKSTATION
                || scene == GameScenes.EDITOR;
        }

        internal static void InvalidateForMainMenu()
        {
            _buildInProgress = false;
            _partIndex = null;
            _metadataIndex = null;
            _categorizerIndex = null;
            _indexedSaveKey = null;
            _basicReady = false;
            _fullReady = false;
            EditorPartAvailability.Invalidate();
            EditorBootstrap.Log("Main menu — search indexes cleared (no indexing on main menu).");
        }

        internal static IEnumerator BuildIfNeeded(MonoBehaviour host)
        {
            // Scene hosts are destroyed on transition; a mid-build lock can be left stale.
            if (_buildInProgress && !_fullReady)
            {
                EditorBootstrap.Log("Clearing stale index-build lock from prior scene host.");
                _buildInProgress = false;
            }

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

                EditorBootstrap.Log("Building search index during game load...");

                yield return new WaitUntil(() => PartLoader.Instance != null && PartLoader.Instance.loadedParts != null);

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
                yield return _partIndex.BuildCoroutine();
                suggestionStopwatch.Stop();
                EditorBootstrap.Log("SuggestionIndex complete in " + suggestionStopwatch.ElapsedMilliseconds + "ms.");

                _basicReady = true;
                _indexedSaveKey = saveKey;
                EditorBootstrap.Log("Search ready (basic)");

                _metadataIndex = new MetadataSuggestionIndex();
                _categorizerIndex = new CategorizerSuggestionIndex();

                bool metadataDone = false;
                bool categorizerDone = false;
                host.StartCoroutine(BuildMetadataBackground(() => metadataDone = true));
                host.StartCoroutine(BuildCategorizerBackground(() => categorizerDone = true));
                yield return new WaitUntil(() => metadataDone && categorizerDone);

                _fullReady = true;
                IndexDebugDump.LogIfEnabled(_partIndex, _metadataIndex, _categorizerIndex);
                EditorBootstrap.Log("Search ready (full)");
            }
            finally
            {
                _buildInProgress = false;
            }
        }

        /// <summary>
        /// Wait-only. Never starts index builds (hangar must stay free of load work).
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
                    "Save-load basic index not ready after wait — dropdown disabled (no hangar rebuild).");
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

        private static IEnumerator BuildMetadataBackground(Action onComplete)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                yield return _metadataIndex.BuildCoroutine();
                stopwatch.Stop();
                EditorBootstrap.Log("MetadataSuggestionIndex complete in " + stopwatch.ElapsedMilliseconds + "ms.");
            }
            finally
            {
                onComplete?.Invoke();
            }
        }

        private static IEnumerator BuildCategorizerBackground(Action onComplete)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                yield return _categorizerIndex.BuildCoroutine();
                stopwatch.Stop();
                EditorBootstrap.Log("CategorizerSuggestionIndex complete in " + stopwatch.ElapsedMilliseconds + "ms.");
            }
            finally
            {
                onComplete?.Invoke();
            }
        }

        private static bool IsReadyForSave(string saveKey)
        {
            return _basicReady
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
