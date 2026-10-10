using System;

namespace RuneCode
{
    /// <summary>설정 탭 Presenter다. 피드백 설정 저장, 공용 확인 팝업을 거친 저장 초기화, 디버그 지급·해금을 처리한다.</summary>
    public sealed class SettingsPresenter
    {
        private readonly RuneCodeSession _session;
        private readonly UIManager _ui;
        private readonly SettingsPanel _view;
        private readonly Action _onReset;
        private readonly Action _onProgressionChanged;

        /// <summary>
        /// 세션·UI 관리자·설정 View와 초기화 후 동작(에디터 탭 복귀), 디버그 변경 후 동작(헤더 갱신)을 받아 View 이벤트를 연결한다.
        /// </summary>
        public SettingsPresenter(RuneCodeSession session, UIManager ui, SettingsPanel view, Action onReset, Action onProgressionChanged)
        {
            _session = session;
            _ui = ui;
            _view = view;
            _onReset = onReset;
            _onProgressionChanged = onProgressionChanged;
            _view.Bind(session.IsDebugEnabled);
            _view.ScreenShakeClicked += ToggleScreenShake;
            _view.HitStopClicked += ToggleHitStop;
            _view.ResetSaveClicked += ConfirmReset;
            _view.DebugGrantClicked += DebugGrant;
            _view.DebugUnlockClicked += DebugUnlock;
            _view.DebugTreeResetClicked += ResetDebugTree;
        }

        /// <summary>화면 흔들림과 히트스톱 설정의 현재 값을 표시한다.</summary>
        public void Refresh()
        {
            _view.SetFeedback(_session.Save.ScreenShake, _session.Save.HitStop);
        }

        /// <summary>화면 흔들림 설정을 반전시켜 저장하고 문구를 갱신한다.</summary>
        private void ToggleScreenShake()
        {
            _session.SetSetting("screenShake", !_session.Save.ScreenShake);
            Refresh();
        }

        /// <summary>히트스톱 설정을 반전시켜 저장하고 문구를 갱신한다.</summary>
        private void ToggleHitStop()
        {
            _session.SetSetting("hitStop", !_session.Save.HitStop);
            Refresh();
        }

        /// <summary>공용 확인 팝업으로 저장 초기화를 묻고, 확인하면 진행을 초기화한 뒤 초기화 후 동작을 실행한다.</summary>
        private void ConfirmReset()
        {
            string titleKey = _session.IsDebugEnabled ? "ui.resetDebugSession" : "ui.resetSave";
            string messageKey = _session.IsDebugEnabled ? "ui.resetDebugSessionMessage" : "ui.resetSaveMessage";
            _ui.Confirm(GameData.L(titleKey), GameData.L(messageKey), GameData.L(titleKey), () =>
            {
                _session.ResetSave();
                _onReset();
            });
        }

        /// <summary>디버그 실행에서만 조각을 지급하고 진행 표시를 갱신한다.</summary>
        private void DebugGrant()
        {
            _session.DebugGrant();
            _onProgressionChanged();
        }

        /// <summary>디버그 실행에서만 모든 룬을 해금하고 진행 표시를 갱신한다.</summary>
        private void DebugUnlock()
        {
            _session.DebugUnlock();
            _onProgressionChanged();
        }

        /// <summary>디버그 세션의 스탯 강화 레벨을 초기화하고 트리·전투 표시를 갱신한다.</summary>
        private void ResetDebugTree()
        {
            _session.ResetDebugTree();
            _onProgressionChanged();
        }
    }
}
