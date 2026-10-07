using UnityEngine;

using TMPro;
using UnityEngine.InputSystem;

namespace RuneCode
{
    /// <summary>
    /// 미션 씬의 진입 컴포넌트다. 세션에서 시전 마법을 받아 MissionRun을 만들고,
    /// Esc 일시정지, 포인터 조준 수집, HUD 갱신과 결과 패널 표시를 진행한다.
    /// </summary>
    public sealed class MissionScreen : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private RuneArenaGraphic _arena;
        [SerializeField] private MissionHud _hud;
        [SerializeField] private PausePanel _pausePanel;
        [SerializeField] private ResultPanel _resultPanel;
        [SerializeField] private TMP_FontAsset _font;

        private RuneCodeSession _session;
        private MissionRun _run;
        private bool _isResultShown;

        /// <summary>모달이 하나라도 열려 포인터 조준을 막아야 하는지 반환한다.</summary>
        private bool IsModalOpen => (_pausePanel != null && _pausePanel.IsOpen) || (_hud != null && _hud.IsDebugModalOpen);

        /// <summary>세션을 받아 경기장과 하위 패널을 초기화하고 미션을 시작한다. 마법 준비에 실패하면 작업실 전환을 요청한다.</summary>
        public void Initialize(RuneCodeSession session)
        {
            _session = session;
            _arena.Initialize(GetRunSimulation, _font, () => _session.Save.ScreenShake, () => _session.Save.HitStop);
            _hud.Initialize(_session, this);
            _pausePanel.Initialize(_session, this);
            _resultPanel.Initialize(_session, this);
            if (!StartRun()) _session.RequestScreen(AppScreen.Workshop);
        }

        /// <summary>일시정지 상태를 바꾸고 일시정지 패널을 열거나 닫는다. 결과 표시 중에는 무시한다.</summary>
        public void TogglePause()
        {
            if (_run == null || _run.IsFinished) return;
            _hud.CloseDebugModal();
            _run.TogglePause();
            if (_run.IsPaused) _pausePanel.Open();
            else _pausePanel.Close();
        }

        /// <summary>현재 전투를 중단하여 획득 조각을 보존 비율로 정산한다. 결과 패널은 다음 갱신에서 표시된다.</summary>
        public void AbandonMission()
        {
            if (_run == null || _run.IsFinished) return;
            _run.AbandonMission();
        }

        /// <summary>정산된 결과의 스테이지를 다시 선택하여 같은 씬에서 새 미션을 시작한다. 준비에 실패하면 결과 패널을 유지한다.</summary>
        public void RetryStage()
        {
            if (_run == null || !_run.IsFinished) return;
            _session.SelectStage(_run.Simulation.StageNumber);
            StartRun();
        }

        void Update()
        {
            if (_session == null || _run == null) return;
            if (_run.IsFinished)
            {
                if (!_isResultShown) ShowResult();
                return;
            }
            HandleEscape();
            var isTyping = UiFactory.IsTyping();
            var hasPointer = CollectPointer(isTyping, out var pointer);
            _run.Step(isTyping, hasPointer, pointer, Time.unscaledDeltaTime);
            _hud.UpdateHud();
        }

        /// <summary>정산이 끝난 미션의 결과를 한 번만 표시하고 열린 모달을 닫는다.</summary>
        private void ShowResult()
        {
            _isResultShown = true;
            _pausePanel.Close();
            _hud.CloseDebugModal();
            _resultPanel.Show(_run);
        }

        /// <summary>세션에서 시전 마법을 준비받아 새 미션을 만들고 HUD와 패널을 시작 상태로 되돌린다.</summary>
        private bool StartRun()
        {
            if (!_session.TryPrepareMission(out CompiledSpell spell)) return false;
            _run = new MissionRun(_session, spell, _session.Spells.SpellName);
            _isResultShown = false;
            _pausePanel.Close();
            _hud.CloseDebugModal();
            _hud.BeginRun(_run);
            _resultPanel.Hide();
            return true;
        }

        /// <summary>Esc 입력으로 일시정지를 토글한다. 문자 입력 중에는 무시한다.</summary>
        private void HandleEscape()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || UiFactory.IsTyping()) return;
            if (keyboard.escapeKey.wasPressedThisFrame) TogglePause();
        }

        /// <summary>마우스 위치가 경기장 안이고 모달·문자 입력이 없으면 시뮬레이션 좌표의 조준점을 반환한다.</summary>
        private bool CollectPointer(bool isTyping, out SimVector pointer)
        {
            pointer = SimVector.Zero;
            var mouse = Mouse.current;
            if (isTyping || IsModalOpen || mouse == null || _arena == null) return false;
            return _arena.TryGetPointer(mouse.position.ReadValue(), out pointer);
        }

        /// <summary>경기장 그래픽이 그릴 현재 시뮬레이션을 반환한다.</summary>
        private RuneSimulation GetRunSimulation() => _run != null ? _run.Simulation : null;
    }
}
