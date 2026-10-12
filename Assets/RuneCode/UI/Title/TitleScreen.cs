using System;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>타이틀 화면 View다. 시작·저장 슬롯 관리 입력을 알리고 처리는 TitlePresenter가 한다.</summary>
    public sealed class TitleScreen : UiView
    {
        [Header("화면 구성")]
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _debugStartButton;

        private Button _saveManagementButton;
        private TitlePresenter _presenter;

        /// <summary>시작 버튼을 눌렀을 때 알린다.</summary>
        public event Action StartClicked;

        /// <summary>디버그 시작 버튼을 눌렀을 때 알린다.</summary>
        public event Action DebugStartClicked;

        /// <summary>저장 슬롯 관리 버튼을 눌렀을 때 알린다.</summary>
        public event Action SaveManagementClicked;

        /// <summary>시작 버튼과 슬롯 관리 버튼을 연결하고 세션·UI 관리자와 Presenter를 만든다.</summary>
        public void Initialize(RuneCodeSession session, UIManager ui)
        {
            if (_presenter != null) return;
            _startButton.onClick.AddListener(() => StartClicked?.Invoke());
            if (_debugStartButton != null) _debugStartButton.onClick.AddListener(() => DebugStartClicked?.Invoke());
            _saveManagementButton = CreateSaveManagementButton();
            _saveManagementButton.onClick.AddListener(() => SaveManagementClicked?.Invoke());
            _presenter = new TitlePresenter(session, ui, this);
        }

        /// <summary>일반 시작 버튼을 복제해 타이틀 메뉴의 저장 슬롯 관리 버튼을 만든다.</summary>
        private Button CreateSaveManagementButton()
        {
            Button button = Instantiate(_startButton, _startButton.transform.parent, false);
            button.name = "SaveManagementButton";
            RectTransform rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(154, -420);
            button.onClick.RemoveAllListeners();
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            label.text = GameData.L("save.manage");
            return button;
        }
    }
}
