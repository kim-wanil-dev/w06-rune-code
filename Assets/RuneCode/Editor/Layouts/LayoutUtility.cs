using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

using TMPro;

namespace RuneCode
{
    /// <summary>화면별 레이아웃 빌더가 공유하는 글꼴 로드, 직렬화 참조 연결, Prefab·씬 저장 도구다.</summary>
    public static class LayoutUtility
    {
        public const string FONT_PATH = "Assets/RuneCode/Resources/RuneCode/UIFont.asset";
        public const string PREFAB_FOLDER = "Assets/RuneCode/Prefabs/UI";
        public const string SCENE_FOLDER = "Assets/Scenes";

        /// <summary>프로젝트 한글 UI 글꼴로 UiFactory를 만든다. 게임 데이터 문자열도 함께 로드한다.</summary>
        public static UiFactory CreateFactory()
        {
            GameData.Load();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            return new UiFactory(font);
        }

        /// <summary>대상 컴포넌트의 private 직렬화 필드에 오브젝트 참조를 기록한다.</summary>
        public static void SetReference(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[Layout] 필드를 찾을 수 없습니다: " + target.GetType().Name + "." + fieldName);
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>대상 컴포넌트의 private 직렬화 배열 필드에 오브젝트 참조 목록을 기록한다.</summary>
        public static void SetReferences(Object target, string fieldName, Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null || !property.isArray)
            {
                Debug.LogError("[Layout] 배열 필드를 찾을 수 없습니다: " + target.GetType().Name + "." + fieldName);
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>폴더가 없으면 상위부터 생성한다.</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        /// <summary>임시 오브젝트를 Prefab으로 저장하고 임시 오브젝트를 제거한 뒤 Prefab 자산을 반환한다.</summary>
        public static GameObject SavePrefab(GameObject source, string fileName)
        {
            EnsureFolder(PREFAB_FOLDER);
            string path = PREFAB_FOLDER + "/" + fileName + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            Object.DestroyImmediate(source);
            return prefab;
        }

        /// <summary>UiFactory 버튼 모양의 UiRow 행 Prefab을 만들어 저장한다.</summary>
        public static UiRow SaveRowPrefab(UiFactory ui, string fileName, float width, float height, float fontSize)
        {
            GameObject temp = new GameObject("Temp", typeof(RectTransform));
            UnityEngine.UI.Button button = ui.Button(temp.transform, 0, 0, width, height, "", null, UiTheme.Muted, fontSize, fileName);
            GameObject row = button.gameObject;
            row.transform.SetParent(null, false);
            Object.DestroyImmediate(temp);
            UiRow component = row.AddComponent<UiRow>();
            SetReference(component, "_button", button);
            SetReference(component, "_background", row.GetComponent<UnityEngine.UI.Image>());
            SetReference(component, "_label", row.GetComponentInChildren<TextMeshProUGUI>());
            return SavePrefab(row, fileName).GetComponent<UiRow>();
        }

        /// <summary>빈 씬을 단독으로 새로 만든다. 기존 같은 경로의 씬은 저장 시 덮어쓴다.</summary>
        public static Scene CreateEmptyScene()
        {
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>씬을 Assets/Scenes 아래 지정 이름으로 저장하고 경로를 반환한다.</summary>
        public static string SaveScene(Scene scene, string sceneName)
        {
            EnsureFolder(SCENE_FOLDER);
            string path = SCENE_FOLDER + "/" + sceneName + ".unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }
    }
}
