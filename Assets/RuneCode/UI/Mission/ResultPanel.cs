using System;
using System.Collections.Generic;

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
        private const int CLEAR_CHOICE_COUNT = 3;
        private const float CLEAR_CHOICE_WIDTH = 344f;
        private const float CLEAR_CHOICE_HEIGHT = 58f;
        private const float CLEAR_CHOICE_TOP_Y = 566f;
        private const float CLEAR_CHOICE_GAP = 16f;

        [Header("문구")]
        [SerializeField] private TextMeshProUGUI _summaryLabel;
        [SerializeField] private TextMeshProUGUI _stageLabel;
        [SerializeField] private TextMeshProUGUI _progressLabel;

        [Header("버튼")]
        [SerializeField] private Button _improveButton;
        [SerializeField] private Button _retryButton;

        private readonly List<Button> _clearChoiceButtons = new List<Button>();
        private bool _isBound;

        /// <summary>마법 개선(작업실로 이동)을 눌렀을 때 알린다.</summary>
        public event Action ImproveClicked;

        /// <summary>같은 스테이지 재도전을 눌렀을 때 알린다.</summary>
        public event Action RetryClicked;

        /// <summary>클리어 Modifier 선택 후보 버튼을 눌렀을 때 후보 순번으로 알린다.</summary>
        public event Action<int> ClearModifierChoiceClicked;

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

        /// <summary>클리어 Modifier 선택 후보를 버튼 문구로 표시하고 눌린 순번을 이벤트로 알린다.</summary>
        public void ShowClearModifierChoices(IReadOnlyList<string> labels)
        {
            EnsureClearChoiceButtons();
            for (int i = 0; i < _clearChoiceButtons.Count; i++)
            {
                bool hasChoice = labels != null && i < labels.Count;
                _clearChoiceButtons[i].gameObject.SetActive(hasChoice);
                if (!hasChoice) continue;
                _clearChoiceButtons[i].interactable = true;
                _clearChoiceButtons[i].GetComponentInChildren<TextMeshProUGUI>(true).text = labels[i];
            }
        }

        /// <summary>클리어 Modifier 선택이 확정됐으면 모든 후보 버튼을 누를 수 없게 바꾼다.</summary>
        public void MarkClearModifierChosen()
        {
            foreach (Button button in _clearChoiceButtons) button.interactable = false;
        }

        /// <summary>클리어 Modifier 선택 후보 버튼을 숨긴다.</summary>
        public void HideClearModifierChoices()
        {
            EnsureClearChoiceButtons();
            foreach (Button button in _clearChoiceButtons) button.gameObject.SetActive(false);
        }

        /// <summary>클리어 Modifier 선택 후보 버튼을 처음 필요할 때 개선 버튼의 복제로 만들어 배치한다. Prefab 수정 없이 런타임 생성이다.</summary>
        private void EnsureClearChoiceButtons()
        {
            if (_clearChoiceButtons.Count > 0) return;
            float totalWidth = CLEAR_CHOICE_COUNT * CLEAR_CHOICE_WIDTH + (CLEAR_CHOICE_COUNT - 1) * CLEAR_CHOICE_GAP;
            for (int i = 0; i < CLEAR_CHOICE_COUNT; i++)
            {
                Button button = Instantiate(_improveButton, transform);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ClearModifierChoiceClicked?.Invoke(_clearChoiceButtons.IndexOf(button)));
                RectTransform rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
                rect.pivot = new Vector2(0.5f, 1);
                float centerOffset = (i - (CLEAR_CHOICE_COUNT - 1) / 2f) * (CLEAR_CHOICE_WIDTH + CLEAR_CHOICE_GAP);
                rect.anchoredPosition = new Vector2(centerOffset, -CLEAR_CHOICE_TOP_Y);
                rect.sizeDelta = new Vector2(CLEAR_CHOICE_WIDTH, CLEAR_CHOICE_HEIGHT);
                button.gameObject.SetActive(false);
                _clearChoiceButtons.Add(button);
            }
        }
    }
}
