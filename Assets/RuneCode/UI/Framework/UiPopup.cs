using System;

using UnityEngine;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 팝업 View 기본형이다. UIManager의 Popup 층에 올라가 기존 화면 위에 표시되고, 딤 배경이 아래 입력을 막는다.
    /// 닫기 버튼과 Esc 닫기를 공통으로 처리하며 닫힐 때 Closed를 한 번 알린다.
    /// </summary>
    public abstract class UiPopup : UiView
    {
        [Header("팝업 공통")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private bool _isEscapeClosable = true;

        private UIManager _owner;
        private bool _isCloseButtonBound;

        /// <summary>팝업이 닫힐 때 한 번 알린다. 열 때마다 새로 구독한다.</summary>
        public event Action Closed;

        /// <summary>UIManager가 팝업을 열 때 소유자를 연결하고 닫기 버튼을 처음 한 번만 연결한다.</summary>
        internal void Bind(UIManager owner)
        {
            _owner = owner;
            if (_isCloseButtonBound || _closeButton == null) return;
            _closeButton.onClick.AddListener(Close);
            _isCloseButtonBound = true;
        }

        /// <summary>UIManager를 통해 팝업을 닫는다. 이미 닫혔으면 무시한다.</summary>
        public void Close()
        {
            if (!IsVisible) return;
            if (_owner != null) _owner.ClosePopup(this);
            else Hide();
        }

        /// <summary>Esc로 닫을 수 있는 팝업이면 닫고 입력을 소비한다.</summary>
        public override bool HandleEscape()
        {
            if (!_isEscapeClosable) return false;
            Close();
            return true;
        }

        /// <summary>숨기기 직전 Closed를 알리고 구독을 비운다. 다음에 열 때는 새 구독만 받는다.</summary>
        protected override void OnHidden()
        {
            Action closed = Closed;
            Closed = null;
            closed?.Invoke();
        }
    }
}
