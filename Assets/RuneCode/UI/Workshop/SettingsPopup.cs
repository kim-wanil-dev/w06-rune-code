using System;

using UnityEngine;

namespace RuneCode
{
    /// <summary>작업실 위에 기존 설정 항목을 표시하고 닫기와 Esc를 공용 팝업 흐름으로 처리한다.</summary>
    public sealed class SettingsPopup : UiPopup
    {
        [Header("설정 내용")]
        [SerializeField] private SettingsPanel _panel;

        private SettingsPresenter _presenter;
        private Action _onReset;
        private Action _onProgressionChanged;

        /// <summary>세션과 설정 변경 콜백을 연결하고 현재 설정을 갱신한다. Presenter는 최초 표시 때만 만든다.</summary>
        public void Configure(RuneCodeSession session, UIManager ui, Action onReset, Action onProgressionChanged)
        {
            _onReset = onReset;
            _onProgressionChanged = onProgressionChanged;
            if (_presenter == null)
                _presenter = new SettingsPresenter(session, ui, _panel, ResetCompleted, ProgressionChanged);
            _presenter.Refresh();
        }

        /// <summary>저장 초기화가 완료되면 설정 팝업을 닫고 작업실 갱신 콜백을 실행한다.</summary>
        private void ResetCompleted()
        {
            Close();
            _onReset?.Invoke();
        }

        /// <summary>디버그 진행 변경을 현재 작업실의 갱신 콜백으로 전달한다.</summary>
        private void ProgressionChanged() => _onProgressionChanged?.Invoke();
    }
}
