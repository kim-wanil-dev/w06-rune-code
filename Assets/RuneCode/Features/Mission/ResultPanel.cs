using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 미션 결과 패널이다. 정산 문구와 스테이지·처치·진행 정보를 표시하고
    /// 마법 개선은 작업실 전환, 재도전은 같은 씬의 새 미션 시작으로 이어진다.
    /// </summary>
    public sealed class ResultPanel : MonoBehaviour
    {
        [Header("문구")]
        [SerializeField] private TextMeshProUGUI _summaryLabel;
        [SerializeField] private TextMeshProUGUI _stageLabel;
        [SerializeField] private TextMeshProUGUI _progressLabel;

        [Header("버튼")]
        [SerializeField] private Button _improveButton;
        [SerializeField] private Button _retryButton;

        private RuneCodeSession _session;
        private MissionScreen _screen;

        /// <summary>세션과 화면을 받아 버튼 리스너를 등록하고 패널을 닫아 둔다.</summary>
        public void Initialize(RuneCodeSession session, MissionScreen screen)
        {
            _session = session;
            _screen = screen;
            _improveButton.onClick.AddListener(() => _session.RequestScreen(AppScreen.Workshop));
            _retryButton.onClick.AddListener(_screen.RetryStage);
            gameObject.SetActive(false);
        }

        /// <summary>정산된 미션의 결과 문구와 스테이지·처치·최고 기록 정보를 쓰고 패널을 연다.</summary>
        public void Show(MissionRun run)
        {
            _summaryLabel.text = run.LastResult;
            _stageLabel.text = GameData.L("ui.stage") + " " + run.Simulation.StageNumber + "  ·  " + GameData.L("ui.kills") + " " + run.Simulation.KillCount;
            _progressLabel.text = GameData.L("ui.highestStage") + " " + _session.HighestClearedStage + "  ·  " + GameData.L("ui.nextStage") + " " + (_session.HighestClearedStage + 1);
            gameObject.SetActive(true);
        }

        /// <summary>패널을 닫는다.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
