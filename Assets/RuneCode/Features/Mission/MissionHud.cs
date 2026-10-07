using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 미션 화면의 HUD다. 체력·에너지 막대, 스테이지, 처치·조각, 남은 시간, 마법 줄과 조각 토스트를 갱신하고
    /// 일시정지 버튼과 디버그 전용 버튼·모달을 운영한다.
    /// </summary>
    public sealed class MissionHud : MonoBehaviour
    {
        private const float BAR_WIDTH = 238f;
        private const float BAR_HEIGHT = 9f;
        private const float TIMER_WARNING_SECONDS = 5f;
        private const float TOAST_DURATION = 0.9f;

        private static readonly Color TimerColor = new Color(0.98f, 0.82f, 0.45f);
        private static readonly Color TimerWarningColor = new Color(1f, 0.43f, 0.36f);

        [Header("막대")]
        [SerializeField] private TextMeshProUGUI _hpCaption;
        [SerializeField] private TextMeshProUGUI _energyCaption;
        [SerializeField] private Image _hpFill;
        [SerializeField] private Image _energyFill;

        [Header("전투 정보")]
        [SerializeField] private TextMeshProUGUI _stageLabel;
        [SerializeField] private TextMeshProUGUI _statsLabel;
        [SerializeField] private TextMeshProUGUI _timerLabel;
        [SerializeField] private TextMeshProUGUI _spellLabel;
        [SerializeField] private TextMeshProUGUI _fragmentToast;

        [Header("버튼")]
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _debugButton;
        [SerializeField] private GameObject _debugModal;
        [SerializeField] private Button _debugCloseButton;
        [SerializeField] private Button _debugGrantButton;
        [SerializeField] private Button _debugUnlockButton;
        [SerializeField] private Button _debugInvulnerableButton;
        [SerializeField] private Button _debugSpawnButton;

        private RuneCodeSession _session;
        private MissionScreen _screen;
        private MissionRun _run;
        private int _lastFragments;
        private float _fragmentToastUntil;

        /// <summary>디버그 모달이 열려 있는지 반환한다. 포인터 조준 차단에 쓰인다.</summary>
        public bool IsDebugModalOpen => _debugModal != null && _debugModal.activeSelf;

        /// <summary>세션과 화면을 받아 버튼 리스너를 등록하고 디버그 버튼 표시를 디버그 실행 여부에 맞춘다.</summary>
        public void Initialize(RuneCodeSession session, MissionScreen screen)
        {
            _session = session;
            _screen = screen;
            _pauseButton.onClick.AddListener(_screen.TogglePause);
            _debugButton.onClick.AddListener(OpenDebugModal);
            _debugCloseButton.onClick.AddListener(CloseDebugModal);
            _debugGrantButton.onClick.AddListener(() => _session.DebugGrant());
            _debugUnlockButton.onClick.AddListener(() => _session.DebugUnlock());
            _debugInvulnerableButton.onClick.AddListener(() => { if (_run != null) _run.DebugInvulnerable(); });
            _debugSpawnButton.onClick.AddListener(() => { if (_run != null) _run.DebugSpawn(); });
            _debugButton.gameObject.SetActive(_session.IsDebugEnabled);
            CloseDebugModal();
        }

        /// <summary>새 미션을 받아 스테이지 문구와 조각 토스트 상태를 시작 값으로 되돌리고 HUD를 즉시 갱신한다.</summary>
        public void BeginRun(MissionRun run)
        {
            _run = run;
            _lastFragments = run.Simulation.EarnedFragments;
            _fragmentToastUntil = 0f;
            _fragmentToast.text = "";
            _stageLabel.text = GameData.L("ui.stage") + " " + run.Simulation.StageNumber;
            UpdateHud();
        }

        /// <summary>현재 시뮬레이션 값으로 HUD 전체를 갱신한다. 진행 중인 미션이 없으면 무시한다.</summary>
        public void UpdateHud()
        {
            if (_run == null) return;
            RuneSimulation sim = _run.Simulation;
            _hpFill.rectTransform.sizeDelta = new Vector2(BAR_WIDTH * (float)(sim.Player.Hp / sim.Player.MaxHp), BAR_HEIGHT);
            _energyFill.rectTransform.sizeDelta = new Vector2(BAR_WIDTH * (float)(sim.Player.Energy / sim.Player.MaxEnergy), BAR_HEIGHT);
            _hpCaption.text = GameData.L("ui.hp") + "  " + sim.Player.Hp.ToString("0") + "/" + sim.Player.MaxHp.ToString("0");
            _energyCaption.text = GameData.L("ui.energy") + "  " + sim.Player.Energy.ToString("0") + "/" + sim.Player.MaxEnergy.ToString("0");
            _statsLabel.text = GameData.L("ui.kills") + " " + sim.KillCount + "  ·  " + GameData.L("ui.fragments") + " " + sim.EarnedFragments;
            _timerLabel.text = GameData.L("ui.remaining") + " " + sim.RemainingTime.ToString("0.0") + "s";
            _timerLabel.color = sim.RemainingTime <= TIMER_WARNING_SECONDS ? TimerWarningColor : TimerColor;
            _spellLabel.text = GameData.L("ui.singleSpell") + "  " + _run.SpellName + "  ·  " + GameData.L("ui.cooldown") + " " + sim.Player.Cooldowns[0].ToString("0.0") +
                "s  ·  " + GameData.L("ui.cost") + " " + _run.SpellCost.ToString("0.#") + " EN";
            if (sim.EarnedFragments > _lastFragments)
            {
                _fragmentToast.text = "+" + (sim.EarnedFragments - _lastFragments) + " " + GameData.L("ui.fragments");
                _fragmentToastUntil = Time.unscaledTime + TOAST_DURATION;
            }
            _lastFragments = sim.EarnedFragments;
            if (Time.unscaledTime >= _fragmentToastUntil) _fragmentToast.text = "";
        }

        /// <summary>디버그 조작 모달을 연다.</summary>
        public void OpenDebugModal()
        {
            _debugModal.SetActive(true);
        }

        /// <summary>디버그 조작 모달을 닫는다.</summary>
        public void CloseDebugModal()
        {
            if (_debugModal != null) _debugModal.SetActive(false);
        }
    }
}
