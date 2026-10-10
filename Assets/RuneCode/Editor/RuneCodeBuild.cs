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
        private const string TMP_FALLBACK_FONT_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";
        private const string BUILD_PATH = "Builds/RuneCodePoC-ManualMods/RuneCodePoC.exe";
        private const string SAMPLE_SCENE_PATH = "Assets/Scenes/SampleScene.unity";

        /// <summary>
        /// 글꼴과 Boot 씬을 준비하고 화면·팝업 View Prefab(Resources/RuneCode/UI)을 베이크한 뒤 빌드 설정 씬 목록을 갱신하고 Boot 씬을 연다.
        /// 화면은 씬이 아니라 UIManager가 띄우는 Prefab이므로 빌드에는 Boot 씬만 들어간다.
        /// </summary>
        [MenuItem("Rune Code/Build UI")]
        public static void BuildUi()
        {
            EnsureTmpResources();
            GameData.Load();
            UpgradeTreeAssetBuilder.EnsureAsset();
            PrepareFont();
            PrepareBootScene();
            BuildViewPrefabs();
            UpdateBuildSceneList();
            EditorSceneManager.OpenScene(BOOT_SCENE_PATH, OpenSceneMode.Single);
        }

        /// <summary>없는 화면·팝업 Prefab을 만들고 기존 타이틀·작업실의 누락된 디버그 버튼 연결만 보충한다. 씬은 건드리지 않는다.</summary>
        public static void BuildViewPrefabs()
        {
            GameData.Load();
            UpgradeTreeAssetBuilder.EnsureAsset();
            PopupLayout.Build();
            SpellEditorLayout.BuildPopups();
            TitleLayout.Build();
            WorkshopLayout.Build();
            MissionLayout.Build();
            AssetDatabase.SaveAssets();
        }

        /// <summary>TMP 필수 리소스가 없으면 패키지 기본 리소스를 가져온다.</summary>
        private static void EnsureTmpResources()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") != null) return;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>
        /// UI 글꼴과 TMP 기본 대체 글꼴을 정적(Static) 아틀라스로 굽는다. 정적 글꼴은 실행 중 글리프가 추가되지 않아 에셋이 바뀌지 않는다.
        /// UI 글꼴에는 영문·숫자, 기호, 한글 자모, KS X 1001 한글 2,350자와 게임 문자열의 모든 글자를 넣는다. 새 글자가 필요하면 다시 실행한다.
        /// </summary>
        [MenuItem("Rune Code/Bake Static Fonts")]
        public static void BakeStaticFonts()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (font == null) font = PrepareFont();
            BakeStatic(font, UiCharacters());
            TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TMP_FALLBACK_FONT_PATH);
            if (fallback != null) SetStatic(fallback);
            AssetDatabase.SaveAssets();
        }

        /// <summary>UI 글꼴에 미리 넣을 글자(영문·숫자, 기호, 한글 자모·완성형, 게임 문자열 글자)를 중복 없이 반환한다.</summary>
        private static string UiCharacters()
        {
            string characters = string.Concat(Enumerable.Range(32, 95).Select(value => (char)value)) + UiFontCharacters.SYMBOLS
                + UiFontCharacters.HANGUL_JAMO + UiFontCharacters.HANGUL_SYLLABLES
                + string.Concat(Resources.LoadAll<TextAsset>("RuneCode").Select(asset => asset.text));
            return new string(characters.Where(character => !char.IsControl(character)).Distinct().ToArray());
        }

        /// <summary>글꼴을 잠시 동적으로 바꿔 글자를 추가하고 아틀라스 텍스처를 에셋에 포함한 뒤 정적으로 고정한다.</summary>
        private static void BakeStatic(TMP_FontAsset font, string characters)
        {
            font.atlasPopulationMode = AtlasPopulationMode.DynamicOS;
            font.TryAddCharacters(characters, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[Font] 원본 글꼴에 없는 글자: " + missing);
            foreach (Texture2D texture in font.atlasTextures)
            {
                if (texture != null && !AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
            }
            SetStatic(font);
        }

        /// <summary>글꼴을 정적 아틀라스로 고정하고 빌드 시 동적 데이터 정리를 끈다.</summary>
        private static void SetStatic(TMP_FontAsset font)
        {
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            SerializedObject serialized = new SerializedObject(font);
            SerializedProperty clearOnBuild = serialized.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearOnBuild != null) clearOnBuild.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(font);
        }

        /// <summary>UI 글꼴 자산이 없으면 Windows 시스템 한글 글꼴로 만들고, 있으면 정적 아틀라스로 다시 굽는다.</summary>
        private static TMP_FontAsset PrepareFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (existing != null)
            {
                BakeStatic(existing, UiCharacters());
                AssetDatabase.SaveAssets();
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

        /// <summary>빌드 설정 씬 목록을 Boot(enabled)와 기존처럼 disabled인 SampleScene 순으로 쓴다.</summary>
        private static void UpdateBuildSceneList()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(BOOT_SCENE_PATH, true) };
            if (EditorBuildSettings.scenes.Any(scene => scene.path == SAMPLE_SCENE_PATH))
                scenes.Add(new EditorBuildSettingsScene(SAMPLE_SCENE_PATH, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>UI Prefab을 다시 베이크한 뒤 Boot 씬을 Windows x64 실행 파일로 빌드하고 빌드 리포트를 기록한다.</summary>
        [MenuItem("Rune Code/Build Windows PoC")]
        public static void BuildWindows()
        {
            BuildUi();
            Directory.CreateDirectory(Path.GetDirectoryName(BUILD_PATH));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { BOOT_SCENE_PATH },
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
