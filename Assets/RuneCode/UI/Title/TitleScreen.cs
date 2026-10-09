using System;

using UnityEngine;

using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>타이틀 화면 View다. 시작 버튼 클릭을 알리고, 처리는 TitlePresenter가 한다.</summary>
    public sealed class TitleScreen : UiView
    {
        [Header("화면 구성")]
        [SerializeField] private Button _startButton;

        private TitlePresenter _presenter;

        /// <summary>시작 버튼을 눌렀을 때 알린다.</summary>
        public event Action StartClicked;

        /// <summary>시작 버튼을 이벤트로 연결하고 세션으로 Presenter를 만든다. 이미 초기화했으면 무시한다.</summary>
        public void Initialize(RuneCodeSession session)
        {
            if (_presenter != null) return;
            _startButton.onClick.AddListener(() => StartClicked?.Invoke());
            _presenter = new TitlePresenter(session, this);
        }
    }
}
