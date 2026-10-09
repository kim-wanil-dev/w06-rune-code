namespace RuneCode
{
    /// <summary>타이틀 화면의 Presenter다. 시작 요청을 세션의 작업실 화면 전환으로 바꾼다.</summary>
    public sealed class TitlePresenter
    {
        private readonly RuneCodeSession _session;

        /// <summary>세션과 타이틀 View를 받아 시작 이벤트를 구독한다.</summary>
        public TitlePresenter(RuneCodeSession session, TitleScreen view)
        {
            _session = session;
            view.StartClicked += RequestWorkshop;
        }

        /// <summary>세션에 작업실 화면 전환을 요청한다.</summary>
        private void RequestWorkshop()
        {
            _session.RequestScreen(AppScreen.Workshop);
        }
    }
}
