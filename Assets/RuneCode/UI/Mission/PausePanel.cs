using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 미션 일시정지 모달이다. 계속하기, 화면 흔들림·히트스톱 설정, 후퇴 정산을 제공하며
    /// 레이아웃이 비활성 패널로 베이크하고 런타임은 SetActive로 열고 닫는다.
    /// </summary>
    public sealed class PausePanel : MonoBehaviour
    {
        [Header("버튼")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _shakeButton;
        [SerializeField] private TextMeshProUGUI _shakeLabel;
        [SerializeField] private Button _hitStopButton;
        [SerializeField] private TextMeshProUGUI _hitStopLabel;
        [SerializeField] private Button _retreatButton;
        [SerializeField] private Button _closeButton;

        private RuneCodeSession _session;
        private MissionScreen _screen;

        /// <summary>일시정지 패널이 열려 있는지 반환한다. 포인터 조준 차단에 쓰인다.</summary>
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>세션과 화면을 받아 버튼 리스너를 등록하고 패널을 닫아 둔다.</summary>
        public void Initialize(RuneCodeSession session, MissionScreen screen)
        {
            _session = session;
            _screen = screen;
            _resumeButton.onClick.AddListener(_screen.TogglePause);
            _shakeButton.onClick.AddListener(ToggleScreenShake);
            _hitStopButton.onClick.AddListener(ToggleHitStop);
            _retreatButton.onClick.AddListener(_screen.AbandonMission);
            _closeButton.onClick.AddListener(_screen.TogglePause);
            gameObject.SetActive(false);
        }

        /// <summary>현재 피드백 설정 문구를 새로 쓰고 패널을 연다.</summary>
        public void Open()
        {
            RefreshLabels();
            gameObject.SetActive(true);
        }

        /// <summary>패널을 닫는다.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>화면 흔들림 설정을 반대 값으로 저장하고 문구를 갱신한다.</summary>
        private void ToggleScreenShake()
        {
            _session.SetSetting("screenShake", !_session.Save.ScreenShake);
            RefreshLabels();
        }

        /// <summary>히트스톱 설정을 반대 값으로 저장하고 문구를 갱신한다.</summary>
        private void ToggleHitStop()
        {
            _session.SetSetting("hitStop", !_session.Save.HitStop);
            RefreshLabels();
        }

        /// <summary>세이브의 현재 설정 값으로 화면 흔들림·히트스톱 버튼 문구를 다시 쓴다.</summary>
        private void RefreshLabels()
        {
            _shakeLabel.text = GameData.L("ui.shake") + " " + GameData.L(_session.Save.ScreenShake ? "ui.on" : "ui.off");
            _hitStopLabel.text = GameData.L("ui.hitstop") + " " + GameData.L(_session.Save.HitStop ? "ui.on" : "ui.off");
        }
    }
}
