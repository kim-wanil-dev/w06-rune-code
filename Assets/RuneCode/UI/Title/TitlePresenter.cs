namespace RuneCode
{
    /// <summary>타이틀 화면의 Presenter다. 일반·디버그 시작 요청을 세션 모드 선택과 작업실 전환으로 바꾼다.</summary>
    public sealed class TitlePresenter
    {
        private readonly RuneCodeSession _session;

        /// <summary>세션과 타이틀 View를 받아 시작 이벤트를 구독한다.</summary>
        public TitlePresenter(RuneCodeSession session, TitleScreen view)
        {
            _session = session;
            view.StartClicked += StartNormal;
            view.DebugStartClicked += StartDebug;
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
    }
}
