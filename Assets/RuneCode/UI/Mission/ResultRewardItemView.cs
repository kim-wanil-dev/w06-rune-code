using System;

using UnityEngine;

using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>결과 보상 아이콘에 표시할 분류, 수량, 이름과 설명을 보관한다.</summary>
    public sealed class ResultRewardContent
    {
        private readonly bool _isModifier;
        private readonly int _count;
        private readonly string _title;
        private readonly string _description;

        public bool IsModifier => _isModifier;
        public int Count => _count;
        public string Title => _title;
        public string Description => _description;

        /// <summary>중앙 보상 표시용 아이콘의 종류와 문구를 저장한다.</summary>
        public ResultRewardContent(bool isModifier, int count, string title, string description)
        {
            _isModifier = isModifier;
            _count = count;
            _title = title;
            _description = description;
        }
    }

    /// <summary>결과 패널의 보상 아이콘·수량을 표시하고 포인터 호버를 상위 View에 전달한다.</summary>
    public sealed class ResultRewardItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color _modifierBackground = new Color(0.12f, 0.09f, 0.20f, 1f);
        private static readonly Color _modifierIcon = new Color(0.76f, 0.52f, 1f, 1f);
        private static readonly Color _fragmentBackground = new Color(0.25f, 0.20f, 0.12f, 1f);
        private static readonly Color _fragmentIcon = new Color(0.98f, 0.79f, 0.37f, 1f);
        private static readonly Color _countBadgeBackground = new Color(0.035f, 0.05f, 0.08f, 0.96f);

        [Header("아이콘 및 수량")]
        [SerializeField] private Image _background;
        [SerializeField] private UpgradeTreeNodeIconGraphic _icon;
        [SerializeField] private Image _countBadge;
        [SerializeField] private TextMeshProUGUI _countLabel;

        private bool _isModifier;
        private string _title;
        private string _description;
        private Action<ResultRewardItemView, bool> _hoverAction;

        /// <summary>보상 분류와 툴팁에 필요한 이름·설명을 반환한다.</summary>
        public bool IsModifier => _isModifier;
        public string Title => _title;
        public string Description => _description;

        /// <summary>보상 종류와 x수량을 표시하고 포인터 진입·이탈을 상위 결과 View에 연결한다.</summary>
        public void SetContent(ResultRewardContent content, Action<ResultRewardItemView, bool> hoverAction)
        {
            _isModifier = content.IsModifier;
            _title = content.Title;
            _description = content.Description;
            _hoverAction = hoverAction;
            _background.color = _isModifier ? _modifierBackground : _fragmentBackground;
            _icon.Configure(_isModifier ? UpgradeEffectType.RuneUnlock : UpgradeEffectType.ScrapGain,
                _isModifier ? "modifier" : null);
            _icon.color = _isModifier ? _modifierIcon : _fragmentIcon;
            _countBadge.color = _countBadgeBackground;
            _countLabel.text = "x" + content.Count;
        }

        /// <summary>포인터가 아이콘에 들어오면 보상 종류와 툴팁 정보를 상위 View로 전달한다.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            _hoverAction?.Invoke(this, true);
        }

        /// <summary>포인터가 아이콘을 벗어나면 상위 View에 툴팁 종료를 알린다.</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            _hoverAction?.Invoke(this, false);
        }
    }
}
