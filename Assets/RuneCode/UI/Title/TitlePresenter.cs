namespace RuneCode
{
    /// <summary>타이틀 시작 요청을 세션 모드 선택으로, 저장 관리 요청을 슬롯 팝업 표시로 처리한다.</summary>
    public sealed class TitlePresenter
    {
        private readonly RuneCodeSession _session;
        private readonly UIManager _ui;

        /// <summary>세션·UI 관리자와 타이틀 View를 받아 시작·슬롯 관리 이벤트를 구독한다.</summary>
        public TitlePresenter(RuneCodeSession session, UIManager ui, TitleScreen view)
        {
            _session = session;
            _ui = ui;
            view.StartClicked += StartNormal;
            view.DebugStartClicked += StartDebug;
            view.SaveManagementClicked += OpenSaveManagement;
        }

        /// <summary>기존 일반 저장을 열고 작업실로 전환한다.</summary>
        private void StartNormal()
        {
            _session.StartGame(false);
        }

        /// <summary>새 메모리 디버그 진행을 열고 작업실로 전환한다.</summary>
        private void StartDebug()
        {
            _session.StartGame(true);
        }

        /// <summary>타이틀 위에 저장 슬롯 관리 팝업을 연다.</summary>
        private void OpenSaveManagement()
        {
            _ui.OpenPopup<SaveSlotsPopup>().Configure(_session, _ui);
        }
    }
}
