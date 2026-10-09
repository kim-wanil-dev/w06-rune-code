using System;

using UnityEngine;

using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>미션 디버그 팝업 View다. 조각 지급·전체 해금·무적·적 소환 클릭을 알린다. 디버그 실행에서만 연다.</summary>
    public sealed class MissionDebugPopup : UiPopup
    {
        [Header("버튼")]
        [SerializeField] private Button _grantButton;
        [SerializeField] private Button _unlockButton;
        [SerializeField] private Button _invulnerableButton;
        [SerializeField] private Button _spawnButton;

        private bool _isBound;

        /// <summary>조각 지급을 눌렀을 때 알린다.</summary>
        public event Action GrantClicked;

        /// <summary>전체 해금을 눌렀을 때 알린다.</summary>
        public event Action UnlockClicked;

        /// <summary>무적 전환을 눌렀을 때 알린다.</summary>
        public event Action InvulnerableClicked;

        /// <summary>적 소환을 눌렀을 때 알린다.</summary>
        public event Action SpawnClicked;

        /// <summary>버튼 클릭을 이벤트로 처음 한 번만 연결한다.</summary>
        public void Bind()
        {
            if (_isBound) return;
            _grantButton.onClick.AddListener(() => GrantClicked?.Invoke());
            _unlockButton.onClick.AddListener(() => UnlockClicked?.Invoke());
            _invulnerableButton.onClick.AddListener(() => InvulnerableClicked?.Invoke());
            _spawnButton.onClick.AddListener(() => SpawnClicked?.Invoke());
            _isBound = true;
        }
    }
}
