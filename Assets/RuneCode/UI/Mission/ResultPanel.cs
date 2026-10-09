using System;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 미션 결과 View다. 정산 문구와 스테이지·처치·진행 정보를 표시하고 마법 개선·재도전 클릭을 알린다.
    /// 미션 화면 안의 전체 화면 패널로, 결과가 나오면 MissionPresenter가 표시한다.
    /// </summary>
    public sealed class ResultPanel : UiView
    {
        [Header("문구")]
        [SerializeField] private TextMeshProUGUI _summaryLabel;
        [SerializeField] private TextMeshProUGUI _stageLabel;
        [SerializeField] private TextMeshProUGUI _progressLabel;

        [Header("버튼")]
        [SerializeField] private Button _improveButton;
        [SerializeField] private Button _retryButton;

        private bool _isBound;

        /// <summary>마법 개선(작업실로 이동)을 눌렀을 때 알린다.</summary>
        public event Action ImproveClicked;

        /// <summary>같은 스테이지 재도전을 눌렀을 때 알린다.</summary>
        public event Action RetryClicked;

        /// <summary>버튼 클릭을 이벤트로 처음 한 번만 연결한다.</summary>
        public void Bind()
        {
            if (_isBound) return;
            _improveButton.onClick.AddListener(() => ImproveClicked?.Invoke());
            _retryButton.onClick.AddListener(() => RetryClicked?.Invoke());
            _isBound = true;
        }

        /// <summary>결과 문구와 스테이지·진행 문구를 채운다. 표시는 Show로 한다.</summary>
        public void SetContent(string summary, string stage, string progress)
        {
            _summaryLabel.text = summary;
            _stageLabel.text = stage;
            _progressLabel.text = progress;
        }
    }
}
