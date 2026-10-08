using System;
using System.IO;
using System.Linq;

using UnityEngine;

using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

namespace RuneCode
{
    public static class RuneCodeBuild
    {
        private const string SCENE_PATH = "Assets/Scenes/RuneCodePoC.unity";
        private const string FONT_PATH = "Assets/RuneCode/Resources/RuneCode/UIFont.asset";
        private const string BUILD_PATH = "Builds/RuneCodePoC-MagicRemake/RuneCodePoC.exe";

        /// <summary>기존 장면을 보존하여 PoC 전용 카메라와 앱이 연결된 장면 및 한글 글꼴을 생성한다.</summary>
        [MenuItem("Rune Code/Prepare PoC Scene")]
        public static void PrepareScene()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") == null)
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            GameData.Load();
            var font = PrepareFont();
            var existing = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(SCENE_PATH);
            if (existing.IsValid() && existing.isLoaded)
            {
                if (existing.isDirty) throw new InvalidOperationException("PoC 장면에 저장되지 않은 변경이 있습니다.");
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(existing);
                return;
            }
            if (File.Exists(SCENE_PATH))
            {
                var loaded = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Additive);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(loaded);
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var cameraObject = new GameObject("RuneCode Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.06f);
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            var applicationObject = new GameObject("RuneCode PoC", typeof(RuneCodeApp));
            var serialized = new SerializedObject(applicationObject.GetComponent<RuneCodeApp>());
            serialized.FindProperty("_font").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.SaveAssets();
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

        /// <summary>PoC 전용 장면을 Windows x64 실행 파일로 빌드하고 빌드 리포트를 기록한다.</summary>
        [MenuItem("Rune Code/Build Windows PoC")]
        public static void BuildWindows()
        {
            PrepareScene();
            Directory.CreateDirectory(Path.GetDirectoryName(BUILD_PATH));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SCENE_PATH },
                locationPathName = BUILD_PATH,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.DetailedBuildReport
            };
            var report = BuildPipeline.BuildPlayer(options);
            File.WriteAllText("Builds/RuneCodePoC-MagicRemake/build-report.txt",
                "Result: " + report.summary.result + "\nErrors: " + report.summary.totalErrors +
                "\nWarnings: " + report.summary.totalWarnings + "\nBytes: " + report.summary.totalSize +
                "\nDuration: " + report.summary.totalTime);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("PoC 빌드에 실패했습니다.");
        }
    }
}
