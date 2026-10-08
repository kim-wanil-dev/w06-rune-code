using System;

using UnityEngine;

using TMPro;

namespace RuneCode
{
    /// <summary>강화 카드의 제목·현재 값·설명·구매 버튼 표시를 갱신하고 버튼 동작을 연결한다.</summary>
    public sealed class UpgradeCardView : MonoBehaviour
    {
        [Header("강화 내용")]
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _value;
        [SerializeField] private TextMeshProUGUI _description;

        [Header("구매 버튼")]
        [SerializeField] private UnityEngine.UI.Button _buyButton;
        [SerializeField] private UnityEngine.UI.Image _buyButtonImage;
        [SerializeField] private TextMeshProUGUI _buyLabel;

        /// <summary>성장 항목 구매 콜백으로 버튼 리스너를 교체한다.</summary>
        public void SetPurchaseAction(Action action)
        {
            _buyButton.onClick.RemoveAllListeners();
            if (action != null) _buyButton.onClick.AddListener(() => action());
        }

        /// <summary>강화 이름, 값, 설명과 비용 문구·강조 색을 화면에 반영한다.</summary>
        public void SetContent(string title, string value, string description, string purchaseLabel, Color accent)
        {
            _title.text = title;
            _value.text = value;
            _description.text = description;
            _buyLabel.text = purchaseLabel;
            _buyLabel.color = accent;
            _buyButtonImage.color = new Color(accent.r * 0.19f + 0.03f, accent.g * 0.19f + 0.05f, accent.b * 0.19f + 0.07f);
        }
    }
}
