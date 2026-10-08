using UnityEngine;

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace RuneCode
{
    /// <summary>화면별 레이아웃 빌더가 공유하는 글꼴 로드, 직렬화 참조 연결, Prefab·씬 저장 도구다.</summary>
    public static class LayoutUtility
    {
        public const string FONT_PATH = "Assets/RuneCode/Resources/RuneCode/UIFont.asset";
        public const string PREFAB_FOLDER = "Assets/RuneCode/Prefabs/UI";
        public const string PANEL_PREFAB_PATH = PREFAB_FOLDER + "/UiPanel.prefab";
        public const string BUTTON_PREFAB_PATH = PREFAB_FOLDER + "/UiButton.prefab";
        public const string ROW_PREFAB_PATH = PREFAB_FOLDER + "/UiRow.prefab";
        public const string SCENE_FOLDER = "Assets/Scenes";

        /// <summary>공통 UI Prefab을 준비하고 프로젝트 한글 글꼴·Prefab 인스턴스 생성기를 연결한 UiFactory를 반환한다.</summary>
        public static UiFactory CreateFactory()
        {
            GameData.Load();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            EnsureSharedPrefabs(font);
            GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PANEL_PREFAB_PATH);
            GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BUTTON_PREFAB_PATH);
            return new UiFactory(font, panelPrefab, buttonPrefab,
                (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent));
        }

        /// <summary>공유 패널·버튼 Prefab이 없을 때 기본 구조로 만들고 기존 편집 자산은 보존한다.</summary>
        private static void EnsureSharedPrefabs(TMP_FontAsset font)
        {
            EnsureFolder(PREFAB_FOLDER);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PANEL_PREFAB_PATH) == null)
            {
                GameObject panel = new GameObject("UiPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                RectTransform rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = new Vector2(200, 80);
                UnityEngine.UI.Image image = panel.GetComponent<UnityEngine.UI.Image>();
                image.color = UiTheme.Panel;
                image.raycastTarget = false;
                SavePrefab(panel, "UiPanel");
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BUTTON_PREFAB_PATH) == null)
            {
                GameObject buttonObject = new GameObject("UiButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
                RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
                buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0, 1);
                buttonRect.pivot = new Vector2(0, 1);
                buttonRect.sizeDelta = new Vector2(180, 40);
                UnityEngine.UI.Image image = buttonObject.GetComponent<UnityEngine.UI.Image>();
                image.color = new Color(UiTheme.Muted.r * 0.19f + 0.03f, UiTheme.Muted.g * 0.19f + 0.05f, UiTheme.Muted.b * 0.19f + 0.07f);
                image.raycastTarget = true;
                UnityEngine.UI.Button button = buttonObject.GetComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image;
                UnityEngine.UI.ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
                colors.pressedColor = new Color(0.65f, 0.85f, 1);
                button.colors = colors;

                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(buttonObject.transform, false);
                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.offsetMin = new Vector2(6, 0);
                labelRect.offsetMax = new Vector2(-6, 0);
                TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
                label.font = font;
                label.fontSize = 14;
                label.color = UiTheme.Muted;
                label.alignment = TextAlignmentOptions.Midline;
                label.raycastTarget = false;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Overflow;
                SavePrefab(buttonObject, "UiButton");
            }
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

        /// <summary>공통 버튼 Prefab을 감싼 목록 행 Prefab을 한 번 만들고 모든 반복 목록에서 공유한다.</summary>
        public static UiRow GetSharedRowPrefab(UiFactory ui)
        {
            UiRow existing = AssetDatabase.LoadAssetAtPath<UiRow>(ROW_PREFAB_PATH);
            if (existing != null) return existing;
            GameObject temp = new GameObject("Temp", typeof(RectTransform));
            UnityEngine.UI.Button button = ui.Button(temp.transform, 0, 0, 320, 44, "", null, UiTheme.Muted, 12, "Button");
            GameObject row = button.gameObject;
            row.transform.SetParent(null, false);
            Object.DestroyImmediate(temp);
            UiRow component = row.AddComponent<UiRow>();
            SetReference(component, "_button", button);
            SetReference(component, "_background", row.GetComponent<UnityEngine.UI.Image>());
            SetReference(component, "_label", row.GetComponentInChildren<TextMeshProUGUI>());
            return SavePrefab(row, "UiRow").GetComponent<UiRow>();
        }

        /// <summary>UI Prefab 자산을 부모 아래 연결된 인스턴스로 만들고 이름을 설정해 지정 컴포넌트를 반환한다.</summary>
        public static T InstantiatePrefab<T>(T prefab, Transform parent, string name) where T : Component
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            instance.name = name;
            return instance.GetComponent<T>();
        }

        /// <summary>화면 빌더의 좌상단 좌표와 크기를 RectTransform에 적용한다.</summary>
        public static void SetTopLeftRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
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
