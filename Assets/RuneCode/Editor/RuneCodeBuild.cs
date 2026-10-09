using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

using UnityEngine.InputSystem.UI;

namespace RuneCode
{
    public static class RuneCodeBuild
    {
        private const string BOOT_SCENE_PATH = "Assets/Scenes/Boot.unity";
        private const string FONT_PATH = "Assets/RuneCode/Resources/RuneCode/UIFont.asset";
        private const string BUILD_PATH = "Builds/RuneCodePoC-ManualMods/RuneCodePoC.exe";
        private const string SAMPLE_SCENE_PATH = "Assets/Scenes/SampleScene.unity";

        private static readonly string[] FEATURE_SCENE_PATHS = { "Assets/Scenes/Title.unity", "Assets/Scenes/Workshop.unity", "Assets/Scenes/Mission.unity" };

        /// <summary>글꼴과 Boot 씬을 준비하고 3개 기능 씬을 베이크한 뒤 빌드 설정 씬 목록을 갱신하고 Boot 씬을 연다.</summary>
        [MenuItem("Rune Code/Build Scenes")]
        public static void BuildScenes()
        {
            EnsureTmpResources();
            GameData.Load();
            PrepareFont();
            PrepareBootScene();
            TitleLayout.BuildScene();
            WorkshopLayout.BuildScene();
            MissionLayout.BuildScene();
            UpdateBuildSceneList();
            EditorSceneManager.OpenScene(BOOT_SCENE_PATH, OpenSceneMode.Single);
        }

        /// <summary>데이터를 읽고 미션 씬과 미션 월드 자산만 다시 베이크한다. 다른 씬, 글꼴 자산과 빌드 씬 목록은 바꾸지 않는다.</summary>
        [MenuItem("Rune Code/Build Mission Scene")]
        public static void BuildMissionScene()
        {
            GameData.Load();
            MissionLayout.BuildScene();
        }

        /// <summary>TMP 필수 리소스가 없으면 패키지 기본 리소스를 가져온다.</summary>
        private static void EnsureTmpResources()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") != null) return;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>Windows 시스템 한글 글꼴로 UI 문구를 미리 렌더한 TMP 자산을 생성한다.</summary>
        private static TMP_FontAsset PrepareFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (existing != null)
            {
                var additionalCharacters = string.Concat(Resources.LoadAll<TextAsset>("RuneCode").Select(asset => asset.text)) + "−–";
                existing.TryAddCharacters(new string(additionalCharacters.Distinct().ToArray()), out _);
                foreach (var texture in existing.atlasTextures)
                    if (texture != null && !AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, existing);
                EditorUtility.SetDirty(existing); AssetDatabase.SaveAssets();
                return existing;
            }
            var font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 36);
            if (font == null) throw new InvalidOperationException("Malgun Gothic 시스템 글꼴을 찾을 수 없습니다.");
            font.name = "Rune Code Korean UI";
            var characters = string.Concat(Enumerable.Range(32, 95).Select(value => (char)value)) +
                string.Concat(Resources.LoadAll<TextAsset>("RuneCode").Select(asset => asset.text)) + "▶◆◇○◎△□×→±°·…■●↗↺";
            font.TryAddCharacters(new string(characters.Distinct().ToArray()), out _);
            AssetDatabase.CreateAsset(font, FONT_PATH);
            font.material.name = "Rune Code UI SDF Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures)
            {
                texture.name = "Rune Code UI Atlas";
                AssetDatabase.AddObjectToAsset(texture, font);
            }
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        /// <summary>Boot 씬을 열어 앱 오브젝트·카메라·EventSystem(InputSystemUIInputModule)을 확인하고 없는 것만 만들어 저장한다.</summary>
        private static void PrepareBootScene()
        {
            Scene scene = SceneManager.GetSceneByPath(BOOT_SCENE_PATH);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(BOOT_SCENE_PATH, OpenSceneMode.Additive);
            if (!scene.IsValid()) throw new InvalidOperationException("Boot 씬을 열 수 없습니다: " + BOOT_SCENE_PATH);
            SceneManager.SetActiveScene(scene);

            RuneCodeApp app = SceneComponent<RuneCodeApp>(scene);
            if (app == null)
            {
                GameObject appObject = new GameObject("RuneCodeApp");
                appObject.AddComponent<RuneCodeApp>();
            }
            else if (app.gameObject.name != "RuneCodeApp")
            {
                app.gameObject.name = "RuneCodeApp";
            }

            if (SceneComponent<Camera>(scene) == null)
            {
                var cameraObject = new GameObject("RuneCode Camera", typeof(Camera));
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.035f, 0.06f);
                camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10);
            }

            EventSystem eventSystem = SceneComponent<EventSystem>(scene);
            if (eventSystem == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            else if (eventSystem.GetComponent<InputSystemUIInputModule>() == null && eventSystem.GetComponent<StandaloneInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, BOOT_SCENE_PATH);
            AssetDatabase.SaveAssets();
        }

        /// <summary>씬 안에서 지정 컴포넌트를 비활성 오브젝트까지 포함해 찾는다.</summary>
        private static T SceneComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        /// <summary>빌드 설정 씬 목록을 Boot·Title·Workshop·Mission(전부 enabled)과 기존처럼 disabled인 SampleScene 순으로 쓴다.</summary>
        private static void UpdateBuildSceneList()
        {
            var paths = new List<string> { BOOT_SCENE_PATH };
            paths.AddRange(FEATURE_SCENE_PATHS);
            var scenes = paths.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
            if (EditorBuildSettings.scenes.Any(scene => scene.path == SAMPLE_SCENE_PATH))
                scenes.Add(new EditorBuildSettingsScene(SAMPLE_SCENE_PATH, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>모든 씬을 다시 베이크한 뒤 4개 씬을 Windows x64 실행 파일로 빌드하고 빌드 리포트를 기록한다.</summary>
        [MenuItem("Rune Code/Build Windows PoC")]
        public static void BuildWindows()
        {
            BuildScenes();
            Directory.CreateDirectory(Path.GetDirectoryName(BUILD_PATH));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { BOOT_SCENE_PATH }.Concat(FEATURE_SCENE_PATHS).ToArray(),
                locationPathName = BUILD_PATH,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.DetailedBuildReport
            };
            var report = BuildPipeline.BuildPlayer(options);
            File.WriteAllText("Builds/RuneCodePoC-ManualMods/build-report.txt",
                "Result: " + report.summary.result + "\nErrors: " + report.summary.totalErrors +
                "\nWarnings: " + report.summary.totalWarnings + "\nBytes: " + report.summary.totalSize +
                "\nDuration: " + report.summary.totalTime);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("PoC 빌드에 실패했습니다.");
        }
    }
}
