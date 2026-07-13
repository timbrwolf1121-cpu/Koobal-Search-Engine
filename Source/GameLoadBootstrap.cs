using UnityEngine;

namespace PartSearchSuggest
{
    /// <summary>
    /// Survives scene transitions so GameLoadIndexService.BuildIfNeeded can finish metadata +
    /// categorizer indexing if the initiating EveryScene host is destroyed mid-build.
    /// </summary>
    internal sealed class IndexBuildHost : MonoBehaviour
    {
        private static IndexBuildHost _instance;

        internal static IndexBuildHost Ensure()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var go = new GameObject("KoobalIndexBuildHost");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<IndexBuildHost>();
            return _instance;
        }

        internal static void Shutdown()
        {
            if (_instance == null)
            {
                return;
            }

            Destroy(_instance.gameObject);
            _instance = null;
        }
    }

    /// <summary>
    /// Persistent watcher: starts indexing as soon as KSP requests EDITOR (while the loading
    /// UI / prior scene is still up), so the hitch is not post-hangar-open.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public sealed class EditorLoadIndexWatcher : MonoBehaviour
    {
        private static EditorLoadIndexWatcher _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequested);
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            GameEvents.onGameSceneLoadRequested.Remove(OnGameSceneLoadRequested);
            _instance = null;
        }

        private static void OnGameSceneLoadRequested(GameScenes target)
        {
            if (target != GameScenes.EDITOR)
            {
                return;
            }

            if (HighLogic.CurrentGame == null)
            {
                return;
            }

            GameLoadIndexService.NotifyEditorLoadRequested();
            if (!GameLoadIndexService.ShouldBuildForCurrentScene())
            {
                return;
            }

            EditorBootstrap.LogAlways(
                "Editor scene requested — starting search index on loading buffer (before hangar).");
            GameLoadIndexService.EnsureBuildStarted(IndexBuildHost.Ensure());
        }
    }

    /// <summary>
    /// Runs on every scene transition. Clears indexes on main menu only; fallback-starts
    /// indexes if EDITOR is entered without a prior onGameSceneLoadRequested kick.
    /// EditorSearchHook must not start builds — interactive hangar is UI-hook only.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public sealed class GameLoadBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            // MAINMENU / SPACECENTER: tear down any leftover index host. Never create UI or
            // mutate CanvasScaler outside EDITOR — overlay is editor-scene-local only.
            if (HighLogic.LoadedScene == GameScenes.MAINMENU
                || HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
                IndexBuildHost.Shutdown();
                if (HighLogic.LoadedScene == GameScenes.MAINMENU)
                {
                    GameLoadIndexService.InvalidateForMainMenu();
                }
            }
        }

        private void Start()
        {
            if (!GameLoadIndexService.ShouldBuildForCurrentScene())
            {
                return;
            }

            // Persistent host — scene-local EveryScene addons are destroyed mid-build otherwise.
            IndexBuildHost host = IndexBuildHost.Ensure();
            GameLoadIndexService.EnsureBuildStarted(host);
        }
    }
}
