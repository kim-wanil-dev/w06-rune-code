using System;

using UnityEngine;

using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 에디터 탭 하단의 시험 도크 UI다. 경기장 표시, 시나리오·자동 발사·적응·배속 조작,
    /// 지표와 적응 문구 갱신, 포인터·키보드 입력을 담당하고 도크 구동은 DockRun이 맡는다.
    /// </summary>
    public sealed class DockPanel : MonoBehaviour
    {
        private static readonly string[] SCENARIOS = { "dummy_single", "dummy_line", "dummy_swarm", "aegis", "adapt_loop" };

        [Header("경기장")]
        [SerializeField] private RuneArenaGraphic _arena;
        [SerializeField] private TMP_FontAsset _font;

        [Header("조작")]
        [SerializeField] private Button _scenarioButton;
        [SerializeField] private Button _testButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _autoFireButton;
        [SerializeField] private Button _adaptationButton;
        [SerializeField] private Button _speedButton;

        [Header("표시")]
        [SerializeField] private TextMeshProUGUI _metrics;
        [SerializeField] private TextMeshProUGUI _adaptationText;
        [SerializeField] private SpellEditorPanel _spellEditor;

        private RuneCodeSession _session;
        private DockRun _run;
        private TextMeshProUGUI _scenarioLabelText;

        /// <summary>세션·도크 구동·편집 패널을 받아 경기장과 조작 리스너를 연결하고 세션이 보존 중인 시나리오로 도크를 시작한다.</summary>
        public void Initialize(RuneCodeSession session, DockRun run, SpellEditorPanel editor)
        {
            _session = session;
            _run = run;
            _spellEditor = editor;
            _arena.Initialize(() => _run.Dock, _font, () => _session.Save.ScreenShake, () => _session.Save.HitStop);
            _scenarioLabelText = _scenarioButton.GetComponentInChildren<TextMeshProUGUI>();
            _scenarioButton.onClick.AddListener(CycleScenario);
            _scenarioLabelText.text = GameData.L("scenario." + _run.Scenario);
            _testButton.onClick.AddListener(FireTest);
            _resetButton.onClick.AddListener(ResetDock);
            _autoFireButton.onClick.AddListener(ToggleAutoFire);
            _adaptationButton.onClick.AddListener(ToggleAdaptation);
            _speedButton.onClick.AddListener(CycleSpeed);
            _run.StartDock(_run.Scenario);
        }

        void Update()
        {
            if (_run == null || _run.Dock == null) return;
            RefreshMetrics();
            RefreshAdaptationText();
            StepWithInput();
        }

        void OnDisable()
        {
            if (_run != null) _run.Suspend();
        }

        /// <summary>현재 마법을 시험 도크에 한 번 시전한다.</summary>
        private void FireTest()
        {
            _run.FireDock();
        }

        /// <summary>도크를 현재 시나리오의 초기 상태로 되돌린다.</summary>
        private void ResetDock()
        {
            _run.ResetDock();
        }

        /// <summary>자동 시전 상태를 반전시키고 적응·배속을 유지한다.</summary>
        private void ToggleAutoFire()
        {
            _run.SetDockOptions(!_run.AutoFire, _run.AdaptationEnabled, _run.Speed);
        }

        /// <summary>도크 적응 학습을 켜거나 끊는다.</summary>
        private void ToggleAdaptation()
        {
            _run.SetDockAdaptation(!_run.Dock.Adaptation.Enabled);
        }

        /// <summary>시간 배속을 0.5 → 1 → 2 순서로 순환한다.</summary>
        private void CycleSpeed()
        {
            _run.SetDockOptions(_run.AutoFire, _run.AdaptationEnabled, _run.Speed == 1 ? 2 : _run.Speed == 2 ? 0.5f : 1);
        }

        /// <summary>시험 시나리오를 다음 항목으로 전환하고 독립 시뮬레이션을 다시 시작한다. 선택은 세션에 보존된다.</summary>
        private void CycleScenario()
        {
            int index = Array.IndexOf(SCENARIOS, _run.Scenario);
            string scenario = SCENARIOS[(index + 1) % SCENARIOS.Length];
            _scenarioLabelText.text = GameData.L("scenario." + scenario);
            _run.StartDock(scenario);
        }

        /// <summary>마우스가 도크 경기장 위에 있고 편집 모달이 열려 있지 않은지 반환한다.</summary>
        private bool IsPointerInDock()
        {
            return _arena != null && (_spellEditor == null || !_spellEditor.IsModalOpen)
                && Mouse.current != null && _arena.TryGetPointer(Mouse.current.position.ReadValue(), out _);
        }

        /// <summary>포인터 조준, 좌클릭·자동 시전, R 리셋 입력을 도크 고정 스텝으로 전달한다.</summary>
        private void StepWithInput()
        {
            Mouse mouse = Mouse.current;
            SimVector aim = new SimVector(1, 0);
            if (mouse != null && _arena.TryGetPointer(mouse.position.ReadValue(), out SimVector point)) aim = point - _run.Dock.Player.Position;
            bool isTyping = UiFactory.IsTyping();
            bool fire = _run.AutoFire || (!isTyping && IsPointerInDock() && mouse != null && mouse.leftButton.isPressed);
            if (!isTyping && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetDock();
            _run.Step(aim, fire);
        }

        /// <summary>도크 누적 피해·DPS·에너지·동시 개체 지표를 갱신한다.</summary>
        private void RefreshMetrics()
        {
            _metrics.text = GameData.L("ui.damage") + " " + _run.Dock.TotalDamage.ToString("0") + "   " + GameData.L("ui.dps") + " " + _run.Dock.RollingDps.ToString("0.0") +
                "   " + GameData.L("ui.energy") + " " + _run.Dock.EnergySpent.ToString("0") + "   " + GameData.L("ui.peak") + " " + _run.Dock.PeakSpellEntities;
        }

        /// <summary>자동 시전·배속·적응 상태와 적응 학습 수치를 한 문구로 갱신한다.</summary>
        private void RefreshAdaptationText()
        {
            _adaptationText.text = GameData.L("ui.autofire") + " " + GameData.L(_run.AutoFire ? "ui.on" : "ui.off") + "  ·  " + _run.Speed.ToString("0.#") + "×  ·  " +
                GameData.L("ui.adaptation") + " " + GameData.L(_run.Dock.Adaptation.Enabled ? "ui.on" : "ui.off") + "\n" + AdaptationText.Format(_run.Dock, false);
        }
    }
}
