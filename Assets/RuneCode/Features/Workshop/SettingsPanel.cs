using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 설정 탭의 피드백 설정, 초기화 확인 모달과 디버그 도구를 담당한다.
    /// 초기화 확인 시 에디터 탭으로 전환하고 도크를 다시 시작한다.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private Button _screenShakeButton;
        [SerializeField] private TextMeshProUGUI _screenShakeLabel;
        [SerializeField] private Button _hitStopButton;
        [SerializeField] private TextMeshProUGUI _hitStopLabel;
        [SerializeField] private Button _resetSaveButton;

        [Header("초기화 확인")]
        [SerializeField] private GameObject _resetModal;
        [SerializeField] private Button _modalCloseButton;
        [SerializeField] private Button _modalConfirmButton;
        [SerializeField] private Button _modalCancelButton;

        [Header("디버그")]
        [SerializeField] private GameObject _debugGroup;
        [SerializeField] private Button _debugGrantButton;
        [SerializeField] private Button _debugUnlockButton;

        private RuneCodeSession _session;
        private WorkshopScreen _screen;

        /// <summary>세션과 화면을 받아 설정·모달·디버그 리스너를 등록하고 초기 표시를 정한다.</summary>
        public void Initialize(RuneCodeSession session, WorkshopScreen screen)
        {
            _session = session;
            _screen = screen;
            _screenShakeButton.onClick.AddListener(ToggleScreenShake);
            _hitStopButton.onClick.AddListener(ToggleHitStop);
            _resetSaveButton.onClick.AddListener(OpenResetModal);
            _modalCloseButton.onClick.AddListener(CloseResetModal);
            _modalConfirmButton.onClick.AddListener(ConfirmReset);
            _modalCancelButton.onClick.AddListener(CloseResetModal);
            _debugGrantButton.onClick.AddListener(DebugGrant);
            _debugUnlockButton.onClick.AddListener(DebugUnlock);
            _debugGroup.SetActive(_session.IsDebugEnabled);
            _resetModal.SetActive(false);
            Refresh();
        }

        /// <summary>화면 흔들림과 히트스톱 설정의 현재 값을 버튼 문구에 반영한다.</summary>
        public void Refresh()
        {
            _screenShakeLabel.text = GameData.L("ui.shake") + "  " + GameData.L(_session.Save.ScreenShake ? "ui.on" : "ui.off");
            _hitStopLabel.text = GameData.L("ui.hitstop") + "  " + GameData.L(_session.Save.HitStop ? "ui.on" : "ui.off");
        }

        /// <summary>화면 흔들림 설정을 반전시켜 저장하고 문구를 갱신한다.</summary>
        private void ToggleScreenShake()
        {
            _session.SetSetting("screenShake", !_session.Save.ScreenShake);
            Refresh();
        }

        /// <summary>히트스톱 설정을 반전시켜 저장하고 문구를 갱신한다.</summary>
        private void ToggleHitStop()
        {
            _session.SetSetting("hitStop", !_session.Save.HitStop);
            Refresh();
        }

        /// <summary>초기화 확인 모달을 연다.</summary>
        private void OpenResetModal()
        {
            _resetModal.SetActive(true);
        }

        /// <summary>초기화 확인 모달을 닫는다.</summary>
        private void CloseResetModal()
        {
            _resetModal.SetActive(false);
        }

        /// <summary>모달을 닫고 진행을 초기화한 뒤 에디터 탭으로 전환해 전체를 갱신한다.</summary>
        private void ConfirmReset()
        {
            CloseResetModal();
            _session.ResetSave();
            _screen.ShowEditorTab();
        }

        /// <summary>디버그 실행에서만 조각을 지급하고 헤더 수치를 갱신한다.</summary>
        private void DebugGrant()
        {
            _session.DebugGrant();
            _screen.RefreshHeader();
        }

        /// <summary>디버그 실행에서만 모든 룬을 해금하고 헤더 수치를 갱신한다.</summary>
        private void DebugUnlock()
        {
            _session.DebugUnlock();
            _screen.RefreshHeader();
        }
    }
}
