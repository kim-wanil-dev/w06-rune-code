using System;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 미션 화면의 HUD View다. 체력·에너지 막대, 스테이지, 처치·조각, 남은 시간, 마법 줄과 조각 토스트를 표시하고
    /// 일시정지·디버그 버튼 클릭을 알린다. 표시 값 계산은 MissionPresenter가 한다.
    /// </summary>
    public sealed class MissionHud : MonoBehaviour
    {
        private const float BAR_WIDTH = 238f;
        private const float BAR_HEIGHT = 9f;

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

        private bool _isBound;

        /// <summary>일시정지 버튼을 눌렀을 때 알린다.</summary>
        public event Action PauseClicked;

        /// <summary>디버그 버튼을 눌렀을 때 알린다.</summary>
        public event Action DebugClicked;

        /// <summary>버튼 클릭을 이벤트로 처음 한 번만 연결하고 디버그 버튼 표시 여부를 정한다.</summary>
        public void Bind(bool isDebugVisible)
        {
            if (!_isBound)
            {
                _pauseButton.onClick.AddListener(() => PauseClicked?.Invoke());
                _debugButton.onClick.AddListener(() => DebugClicked?.Invoke());
                _isBound = true;
            }
            _debugButton.gameObject.SetActive(isDebugVisible);
        }

        /// <summary>스테이지 번호 문구를 표시한다.</summary>
        public void SetStage(string text) => _stageLabel.text = text;

        /// <summary>체력·에너지 막대 길이와 수치 문구를 표시한다.</summary>
        public void SetBars(double hp, double maxHp, double energy, double maxEnergy)
        {
            _hpFill.rectTransform.sizeDelta = new Vector2(BAR_WIDTH * (float)(hp / maxHp), BAR_HEIGHT);
            _energyFill.rectTransform.sizeDelta = new Vector2(BAR_WIDTH * (float)(energy / maxEnergy), BAR_HEIGHT);
            _hpCaption.text = GameData.L("ui.hp") + "  " + hp.ToString("0") + "/" + maxHp.ToString("0");
            _energyCaption.text = GameData.L("ui.energy") + "  " + energy.ToString("0") + "/" + maxEnergy.ToString("0");
        }

        /// <summary>처치·조각 문구를 표시한다.</summary>
        public void SetStats(string text) => _statsLabel.text = text;

        /// <summary>남은 시간 문구를 표시하고 임박하면 경고 색으로 바꾼다.</summary>
        public void SetTimer(string text, bool isWarning)
        {
            _timerLabel.text = text;
            _timerLabel.color = isWarning ? TimerWarningColor : TimerColor;
        }

        /// <summary>하단 마법 정보 줄을 표시한다.</summary>
        public void SetSpellLine(string text) => _spellLabel.text = text;

        /// <summary>조각 획득 토스트 문구를 표시한다. 빈 문자열이면 숨긴 것과 같다.</summary>
        public void SetToast(string text) => _fragmentToast.text = text;
    }
}
