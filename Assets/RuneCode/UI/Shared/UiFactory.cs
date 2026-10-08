using System;

using UnityEngine;

using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 좌상단 기준 좌표로 uGUI 요소를 생성하는 공용 헬퍼다.
    /// 에디터 레이아웃 빌더(씬·Prefab 베이크)와 런타임 목록 생성이 같은 모양을 쓰도록 공유한다.
    /// </summary>
    public sealed class UiFactory
    {
        private readonly TMP_FontAsset _font;
        private readonly GameObject _panelPrefab;
        private readonly GameObject _buttonPrefab;
        private readonly Func<GameObject, Transform, GameObject> _prefabInstantiator;

        public TMP_FontAsset Font => _font;
        public GameObject PanelPrefab => _panelPrefab;
        public GameObject ButtonPrefab => _buttonPrefab;

        /// <summary>공통 패널·버튼 Prefab과 텍스트 글꼴로 UI 생성기를 만든다.</summary>
        public UiFactory(TMP_FontAsset font, GameObject panelPrefab, GameObject buttonPrefab,
            Func<GameObject, Transform, GameObject> prefabInstantiator = null)
        {
            _font = font;
            _panelPrefab = panelPrefab;
            _buttonPrefab = buttonPrefab;
            _prefabInstantiator = prefabInstantiator;
        }

        /// <summary>1280×720 기준 Overlay Canvas와 중앙 정렬된 논리 화면 루트를 만들고 루트를 반환한다.</summary>
        public static RectTransform CreateCanvas(Transform parent, string name)
        {
            GameObject canvasTarget = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasTarget.transform.SetParent(parent, false);
            canvasTarget.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasTarget.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            RectTransform root = Rect(canvasTarget.transform, "LogicalScreen", 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            return root;
        }

        /// <summary>주어진 위치와 크기의 좌상단 기준 RectTransform을 생성한다.</summary>
        public static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            GameObject target = new GameObject(name, typeof(RectTransform));
            target.transform.SetParent(parent, false);
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        /// <summary>공통 패널 Prefab을 지정 위치에 배치하고 색상과 포인터 차단 여부를 설정한다.</summary>
        public RectTransform Panel(Transform parent, float x, float y, float width, float height, Color tint, string name = "Panel")
        {
            GameObject instance = InstantiatePrefab(_panelPrefab, parent, name);
            RectTransform rect = Place(instance, x, y, width, height);
            Image image = instance.GetComponent<Image>();
            image.color = tint;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>지정 위치에 글꼴, 크기와 색상을 설정한 TMP 텍스트를 생성한다.</summary>
        public TextMeshProUGUI Text(Transform parent, float x, float y, float width, float height, string value, float size, Color tint,
            FontStyles style = FontStyles.Normal, string name = "Label")
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSize = size;
            text.color = tint;
            text.fontStyle = style;
            text.text = value;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        /// <summary>강조 색 버튼을 생성하고 action이 있으면 실행 콜백을 연결한다. 에디터 베이크 시에는 action 없이 호출한다.</summary>
        public Button Button(Transform parent, float x, float y, float width, float height, string label, Action action = null,
            Color? tint = null, float size = 14, string name = "Button")
        {
            Color accent = tint ?? UiTheme.Muted;
            GameObject instance = InstantiatePrefab(_buttonPrefab, parent, name);
            RectTransform rect = Place(instance, x, y, width, height);
            Image image = instance.GetComponent<Image>();
            image.color = new Color(accent.r * 0.19f + 0.03f, accent.g * 0.19f + 0.05f, accent.b * 0.19f + 0.07f);
            image.raycastTarget = true;
            Button button = instance.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
            colors.pressedColor = new Color(0.65f, 0.85f, 1);
            button.colors = colors;
            if (action != null) button.onClick.AddListener(() => action());
            TextMeshProUGUI text = instance.GetComponentInChildren<TextMeshProUGUI>(true);
            text.font = _font;
            text.fontSize = size;
            text.color = accent;
            text.text = label;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Midline;
            return button;
        }

        /// <summary>문자 입력 필드를 TMP 뷰포트, 표시 문자와 안내 문구에 연결한다.</summary>
        public TMP_InputField Input(Transform parent, float x, float y, float width, float height, string value, string placeholder, string name = "Input")
        {
            RectTransform rect = Panel(parent, x, y, width, height, UiTheme.InputBackground, name);
            rect.GetComponent<Image>().raycastTarget = true;
            TMP_InputField field = rect.gameObject.AddComponent<TMP_InputField>();
            RectTransform viewport = Rect(rect, "TextViewport", 8, 4, width - 16, height - 8);
            viewport.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI text = Text(viewport, 0, 0, width - 16, height - 8, "", 13, Color.white);
            TextMeshProUGUI hint = Text(viewport, 0, 0, width - 16, height - 8, placeholder, 13, UiTheme.Muted);
            field.textViewport = viewport;
            field.textComponent = text;
            field.placeholder = hint;
            field.targetGraphic = rect.GetComponent<Image>();
            field.text = value;
            field.fontAsset = _font;
            return field;
        }

        /// <summary>마스킹된 스크롤 영역과 자동 높이의 세로 목록을 생성하고 콘텐츠를 반환한다.</summary>
        public static RectTransform ScrollList(Transform parent, string name, float x, float y, float width, float height)
        {
            RectTransform root = Rect(parent, name, x, y, width, height);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform viewport = Rect(root, "Viewport", 0, 0, width, height);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1, 1, 1, 0.005f);
            viewportImage.raycastTarget = true;
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            RectTransform content = Rect(viewport, "Content", 0, 0, width - 8, height);
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        /// <summary>지정된 부모 아래 공통 UI Prefab 인스턴스를 만들고 이름을 지정한다.</summary>
        private GameObject InstantiatePrefab(GameObject prefab, Transform parent, string name)
        {
            GameObject instance = _prefabInstantiator == null
                ? UnityEngine.Object.Instantiate(prefab, parent, false)
                : _prefabInstantiator(prefab, parent);
            instance.name = name;
            return instance;
        }

        /// <summary>새 Prefab 인스턴스를 좌상단 기준 좌표와 크기에 맞춰 배치한다.</summary>
        private static RectTransform Place(GameObject instance, float x, float y, float width, float height)
        {
            RectTransform rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        /// <summary>세로 목록 요소의 최소 높이와 원하는 높이를 고정한다.</summary>
        public static void Layout(GameObject target, float height)
        {
            LayoutElement layout = target.GetComponent<LayoutElement>();
            if (layout == null) layout = target.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
        }

        /// <summary>패널의 기존 자식을 비활성화한 뒤 제거한다. 편집 모드에서는 즉시 제거한다.</summary>
        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
        }

        /// <summary>현재 선택된 UI가 TMP 입력 필드이면 true를 반환해 단축키 처리를 막는다.</summary>
        public static bool IsTyping()
        {
            return EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
        }
    }
}
