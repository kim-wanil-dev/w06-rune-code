using System;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>목록 Prefab의 버튼 한 줄이다. 런타임에 문구, 강조 색, 높이와 클릭 동작을 설정한다.</summary>
    public sealed class UiRow : MonoBehaviour
    {
        [Header("구성 요소")]
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private TextMeshProUGUI _label;

        public Button Button => _button;
        public TextMeshProUGUI Label => _label;

        /// <summary>문구, 강조 색, 글자 크기와 행 높이를 적용하고 기존 클릭 리스너를 action으로 교체한다.</summary>
        public void Configure(string label, Color accent, float height, Action action, float fontSize = 12)
        {
            _label.text = label;
            _label.color = accent;
            _label.fontSize = fontSize;
            _background.color = new Color(accent.r * 0.19f + 0.03f, accent.g * 0.19f + 0.05f, accent.b * 0.19f + 0.07f);
            RectTransform rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            UiFactory.Layout(gameObject, height);
            _button.onClick.RemoveAllListeners();
            if (action != null) _button.onClick.AddListener(() => action());
        }
    }
}
