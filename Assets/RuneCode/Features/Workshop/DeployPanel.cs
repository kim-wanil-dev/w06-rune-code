using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 출격 탭의 스테이지 선택과 시간제 전투 시작을 담당한다.
    /// 출격은 세션의 준비 검증을 통과했을 때만 미션 화면 전환을 요청한다.
    /// </summary>
    public sealed class DeployPanel : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField] private TextMeshProUGUI _highestText;
        [SerializeField] private TextMeshProUGUI _stageText;
        [SerializeField] private TextMeshProUGUI _durationText;
        [SerializeField] private TextMeshProUGUI _spellText;

        [Header("조작")]
        [SerializeField] private Button _stageDownButton;
        [SerializeField] private Button _stageUpButton;
        [SerializeField] private Button _launchButton;

        private RuneCodeSession _session;

        /// <summary>세션을 받아 스테이지 이동과 출격 버튼 리스너를 등록하고 현재 값을 채운다.</summary>
        public void Initialize(RuneCodeSession session)
        {
            _session = session;
            _stageDownButton.onClick.AddListener(() => ChangeStage(-1));
            _stageUpButton.onClick.AddListener(() => ChangeStage(1));
            _launchButton.onClick.AddListener(Launch);
            Refresh();
        }

        /// <summary>해금 최고 단계, 선택 스테이지, 제한시간과 현재 마법 문구를 다시 표시한다.</summary>
        public void Refresh()
        {
            _highestText.text = GameData.L("ui.highestStage") + "  " + _session.HighestClearedStage;
            _stageText.text = GameData.L("ui.stage") + " " + _session.SelectedStage;
            _durationText.text = GameData.L("ui.duration") + "  " + _session.BattleDuration.ToString("0") + "s";
            _spellText.text = CurrentSpellText();
        }

        /// <summary>해금 범위 안에서 선택 스테이지를 이동하고 표시를 갱신한다.</summary>
        private void ChangeStage(int direction)
        {
            _session.SelectStage(_session.SelectedStage + direction);
            Refresh();
        }

        /// <summary>마법을 저장·검증해 준비가 되면 미션 화면 전환을 요청한다.</summary>
        private void Launch()
        {
            if (_session.TryPrepareMission(out _)) _session.RequestScreen(AppScreen.Mission);
        }

        /// <summary>현재 단일 마법의 이름과 노드 용량 사용량을 반환한다.</summary>
        private string CurrentSpellText()
        {
            return GameData.L("ui.singleSpell") + "  " + _session.Spells.SpellName + "  ·  " + GameData.L("ui.capacity") + " " + _session.EquippedRam + "/" + _session.Capacity;
        }
    }
}
