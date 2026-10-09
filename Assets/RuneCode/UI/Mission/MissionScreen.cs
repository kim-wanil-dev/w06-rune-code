using UnityEngine;

using TMPro;
using UnityEngine.InputSystem;

namespace RuneCode
{
    /// <summary>
    /// 미션 화면 View다. 경기장·HUD·결과 View를 묶고 진행은 MissionPresenter에 맡긴다.
    /// 표시할 때마다 새 미션을 시작하며, Esc는 UIManager가 열린 팝업이 없을 때 전달한다.
    /// </summary>
    public sealed class MissionScreen : UiView
    {
        [Header("연결")]
        [SerializeField] private RuneArenaGraphic _arena;
        [SerializeField] private MissionHud _hud;
        [SerializeField] private ResultPanel _resultPanel;
        [SerializeField] private TMP_FontAsset _font;

        private RuneCodeSession _session;
        private MissionPresenter _presenter;

        /// <summary>세션·UI 관리자로 Presenter를 만들고 경기장 그래픽을 연결한다. 이미 초기화했으면 무시한다.</summary>
        public void Initialize(RuneCodeSession session, UIManager ui)
        {
            if (_presenter != null) return;
            _session = session;
            _presenter = new MissionPresenter(session, ui, this, _hud, _resultPanel);
            _arena.Initialize(() => _presenter.Simulation, _font, () => _session.Save.ScreenShake, () => _session.Save.HitStop);
        }

        /// <summary>마우스가 경기장 안에 있으면 시뮬레이션 좌표의 조준점을 반환한다.</summary>
        public bool TryGetPointer(out SimVector pointer)
        {
            pointer = SimVector.Zero;
            Mouse mouse = Mouse.current;
            return mouse != null && _arena.TryGetPointer(mouse.position.ReadValue(), out pointer);
        }

        /// <summary>Esc 입력을 Presenter에 전달해 일시정지를 연다.</summary>
        public override bool HandleEscape() => _presenter != null && _presenter.HandleEscape();

        /// <summary>미션 화면에 들어올 때마다 새 미션을 시작한다.</summary>
        protected override void OnShown() => _presenter.Begin();

        /// <summary>미션 화면을 떠날 때 진행 중인 미션을 놓는다.</summary>
        protected override void OnHidden() => _presenter.End();

        void Update()
        {
            _presenter?.Tick(Time.unscaledDeltaTime);
        }
    }
}
