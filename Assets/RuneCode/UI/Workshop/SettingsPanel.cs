using System;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>설정 탭 View다. 피드백 설정 문구를 표시하고 설정 전환·저장 초기화·디버그 클릭을 알린다.</summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private Button _screenShakeButton;
        [SerializeField] private TextMeshProUGUI _screenShakeLabel;
        [SerializeField] private Button _hitStopButton;
        [SerializeField] private TextMeshProUGUI _hitStopLabel;
        [SerializeField] private Button _resetSaveButton;

        [Header("디버그")]
        [SerializeField] private GameObject _debugGroup;
        [SerializeField] private Button _debugGrantButton;
        [SerializeField] private Button _debugUnlockButton;
        [SerializeField] private Button _debugResetTreeButton;

        /// <summary>화면 흔들림 버튼을 눌렀을 때 알린다.</summary>
        public event Action ScreenShakeClicked;

        /// <summary>히트스톱 버튼을 눌렀을 때 알린다.</summary>
        public event Action HitStopClicked;

        /// <summary>저장 초기화 버튼을 눌렀을 때 알린다.</summary>
        public event Action ResetSaveClicked;

        /// <summary>디버그 조각 지급을 눌렀을 때 알린다.</summary>
        public event Action DebugGrantClicked;

        /// <summary>디버그 전체 해금을 눌렀을 때 알린다.</summary>
        public event Action DebugUnlockClicked;

        /// <summary>디버그 강화 트리 초기화를 눌렀을 때 알린다.</summary>
        public event Action DebugTreeResetClicked;

        /// <summary>버튼 클릭을 이벤트로 연결하고 디버그 도구 표시 여부를 정한다. Presenter가 한 번만 호출한다.</summary>
        public void Bind(bool isDebugVisible)
        {
            _resetSaveButton.GetComponentInChildren<TextMeshProUGUI>().text =
                GameData.L(isDebugVisible ? "ui.resetDebugSession" : "ui.resetSave");
            _screenShakeButton.onClick.AddListener(() => ScreenShakeClicked?.Invoke());
            _hitStopButton.onClick.AddListener(() => HitStopClicked?.Invoke());
            _resetSaveButton.onClick.AddListener(() => ResetSaveClicked?.Invoke());
            _debugGrantButton.onClick.AddListener(() => DebugGrantClicked?.Invoke());
            _debugUnlockButton.onClick.AddListener(() => DebugUnlockClicked?.Invoke());
            if (_debugResetTreeButton != null) _debugResetTreeButton.onClick.AddListener(() => DebugTreeResetClicked?.Invoke());
            _debugGroup.SetActive(isDebugVisible);
        }

        /// <summary>화면 흔들림·히트스톱 설정의 켜짐 여부를 버튼 문구에 표시한다.</summary>
        public void SetFeedback(bool isScreenShake, bool isHitStop)
        {
            _screenShakeLabel.text = GameData.L("ui.shake") + "  " + GameData.L(isScreenShake ? "ui.on" : "ui.off");
            _hitStopLabel.text = GameData.L("ui.hitstop") + "  " + GameData.L(isHitStop ? "ui.on" : "ui.off");
        }
    }
}
