using System;

using UnityEngine;

using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RuneCode
{
    public sealed class UpgradeTreeNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("정사각형 노드")]
        [SerializeField] private Image _background;
        [SerializeField] private Outline _outline;
        [SerializeField] private UpgradeTreeNodeIconGraphic _icon;
        [SerializeField] private Image _spriteIcon;
        [SerializeField] private Button _button;

        [Header("상태 표시: 잠금 / 개방 / 일부 강화 / MAX")]
        [SerializeField] private UiStateVisual _visual;

        private Action _purchaseAction;
        private Action<bool> _hoverAction;

        /// <summary>정사각형 노드의 클릭을 강화 또는 룬 해금 요청에 연결한다.</summary>
        public void SetPurchaseAction(Action action)
        {
            _purchaseAction = action;
            _button.onClick.RemoveAllListeners();
            if (_purchaseAction != null) _button.onClick.AddListener(() => _purchaseAction());
        }

        /// <summary>포인터 진입·이탈을 상위 트리의 노드 이름·설명 표시로 전달한다.</summary>
        public void SetHoverAction(Action<bool> action)
        {
            _hoverAction = action;
        }

        /// <summary>아이콘, 노드 개방 상태, 완료 강조와 구매 가능 여부를 화면에 반영한다.</summary>
        public void SetContent(UpgradeEffectType effectType, string runeCategory, Sprite sprite, int level, bool isAvailable, bool isComplete, bool canPurchase)
        {
            _visual.Show(!isAvailable ? 0 : isComplete ? 3 : level > 0 ? 2 : 1);
            _spriteIcon.gameObject.SetActive(sprite != null);
            _icon.gameObject.SetActive(sprite == null);
            if (sprite == null)
            {
                _icon.Configure(effectType, runeCategory);
            }
            else
            {
                _spriteIcon.sprite = sprite;
            }
            _button.interactable = canPurchase;
        }

        /// <summary>노드 이름과 설명을 보여주도록 포인터 진입을 알린다.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            _hoverAction?.Invoke(true);
        }

        /// <summary>현재 노드에서 포인터가 벗어나면 이름과 설명 표시를 닫도록 알린다.</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            _hoverAction?.Invoke(false);
        }

    }
}
