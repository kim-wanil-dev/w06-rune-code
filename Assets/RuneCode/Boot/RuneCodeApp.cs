using System;
using System.Collections;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEngine.InputSystem;

namespace RuneCode
{
    /// <summary>
    /// Boot 씬의 진입 컴포넌트다. 데이터·세이브·세션을 초기화하고 화면 전환 요청에 따라
    /// 기능 씬을 Additive로 교체한 뒤 로드된 씬 루트의 화면 컴포넌트에 세션을 전달한다.
    /// </summary>
    public sealed class RuneCodeApp : MonoBehaviour
    {
        private const string BOOT_SCENE = "Boot";

        [Header("마법 판정")]
        [Tooltip("켜면 폭발·잔류·공전의 사각형이 시전 방향과 관계없이 월드 축에 맞춰 똑바로 선다. 끄면 진행 방향으로 회전한다.")]
        [SerializeField] private bool _isAreaBoxUpright = true;

        private RuneCodeSession _session;
        private bool _isSwitching;

#if UNITY_EDITOR
        /// <summary>에디터에서 기능 씬을 직접 실행할 때 Boot 씬이 없으면 단독으로 로드해 부팅 흐름을 보장한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootScene()
        {
            if (SceneManager.GetSceneByName(BOOT_SCENE).isLoaded) return;
            SceneManager.LoadScene(BOOT_SCENE, LoadSceneMode.Single);
        }
#endif

        void Awake()
        {
            GameData.Load();
            if (SimulationCli.TryRunCommandLine()) { enabled = false; return; }
            Application.targetFrameRate = GameData.Balance.Sim.TickRate;
            PlayerSave save = SaveStore.Load();
            StageCatalog stages = RuneSimulation.LoadStageCatalog();
            bool isDebug = Environment.GetCommandLineArgs().Contains("-debug") || Application.absoluteURL.Contains("debug=1");
            _session = new RuneCodeSession(save, stages, isDebug, SaveStore.LastWarning, _isAreaBoxUpright);
            _session.ScreenRequested += OnScreenRequested;
            LocalTelemetry.Record(0, "session", "start");
            StartCoroutine(LoadScreenRoutine(AppScreen.Title));
        }

        void OnDestroy()
        {
            if (_session != null) _session.ScreenRequested -= OnScreenRequested;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || UiFactory.IsTyping()) return;
            if (keyboard.f8Key.wasPressedThisFrame) LocalTelemetry.DumpToConsole();
        }

        void OnApplicationQuit()
        {
            if (_session != null) _session.SaveAll();
        }

        /// <summary>전환 중이 아니면 화면 전환 코루틴을 시작한다. 전환 중에 온 요청은 무시한다.</summary>
        private void OnScreenRequested(AppScreen screen)
        {
            if (_isSwitching) return;
            StartCoroutine(LoadScreenRoutine(screen));
        }

        /// <summary>열려 있는 기능 씬을 언로드하고 대상 씬을 Additive로 로드한 뒤 루트의 화면 컴포넌트를 한 번 찾아 세션을 전달한다.</summary>
        private IEnumerator LoadScreenRoutine(AppScreen screen)
        {
            _isSwitching = true;
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                Scene loaded = SceneManager.GetSceneAt(index);
                if (!loaded.isLoaded || loaded.name == BOOT_SCENE) continue;
                yield return SceneManager.UnloadSceneAsync(loaded);
            }
            yield return SceneManager.LoadSceneAsync(SceneName(screen), LoadSceneMode.Additive);
            Scene target = SceneManager.GetSceneByName(SceneName(screen));
            if (target.IsValid() && target.isLoaded)
            {
                foreach (GameObject root in target.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out TitleScreen title)) title.Initialize(_session);
                    else if (root.TryGetComponent(out WorkshopScreen workshop)) workshop.Initialize(_session);
                    else if (root.TryGetComponent(out MissionScreen mission)) mission.Initialize(_session);
                }
            }
            _isSwitching = false;
        }

        /// <summary>화면 종류에 대응하는 기능 씬 이름을 반환한다.</summary>
        private static string SceneName(AppScreen screen)
        {
            switch (screen)
            {
                case AppScreen.Title: return "Title";
                case AppScreen.Workshop: return "Workshop";
                case AppScreen.Mission: return "Mission";
                default: return screen.ToString();
            }
        }
    }
}
