using System;
using System.Collections.Generic;

using UnityEngine;

using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 미션 결과 View다. 완료·스테이지·플레이 시간과 획득 보상을 표시하고 마법 개선·재도전·클리어 Modifier 선택을 알린다.
    /// 미션 화면 안의 전체 화면 패널로, 결과가 나오면 MissionPresenter가 표시한다.
    /// </summary>
    public sealed class ResultPanel : UiView
    {
        [Header("문구")]
        [SerializeField] private TextMeshProUGUI _summaryLabel;
        [SerializeField] private TextMeshProUGUI _stageLabel;
        [SerializeField] private TextMeshProUGUI _progressLabel;

        [Header("획득 보상")]
        [SerializeField] private RectTransform _rewardItemsRoot;
        [SerializeField] private ResultRewardItemView _rewardItemPrefab;
        [SerializeField] private GameObject _rewardTooltip;
        [SerializeField] private TextMeshProUGUI _rewardTooltipTitle;
        [SerializeField] private TextMeshProUGUI _rewardTooltipDescription;

        [Header("버튼")]
        [SerializeField] private GameObject _winOnlyButtonRoot;
        [SerializeField] private Button _nextStageButton;
        [SerializeField] private Button _improveButton;
        [SerializeField] private Button _retryButton;

        [Header("클리어 보상 팝업")]
        [SerializeField] private GameObject _clearChoicePopup;
        [SerializeField] private TextMeshProUGUI _clearChoiceTitleLabel;
        [SerializeField] private TextMeshProUGUI _rewardTitleLabel;

        [Header("보상 카드")]
        [SerializeField] private GameObject[] _clearChoiceCards;
        [SerializeField] private Button[] _clearChoiceButtons;
        [SerializeField] private TextMeshProUGUI[] _clearChoiceGradeLabels;
        [SerializeField] private TextMeshProUGUI[] _clearChoiceNameLabels;
        [SerializeField] private TextMeshProUGUI[] _clearChoiceDescriptionLabels;

        private bool _isBound;
        private ResultRewardItemView _hoveredRewardItem;
        private readonly Vector3[] _tooltipCorners = new Vector3[4];

        /// <summary>마법 개선(작업실로 이동)을 눌렀을 때 알린다.</summary>
        public event Action ImproveClicked;

        /// <summary>승리 후 다음 스테이지 준비를 선택했을 때 알린다.</summary>
        public event Action NextStageClicked;

        /// <summary>같은 스테이지 재도전을 눌렀을 때 알린다.</summary>
        public event Action RetryClicked;

        /// <summary>클리어 Modifier 선택 후보 버튼을 눌렀을 때 후보 순번으로 알린다.</summary>
        public event Action<int> ClearModifierChoiceClicked;

        /// <summary>버튼 클릭을 이벤트로 처음 한 번만 연결한다.</summary>
        public void Bind()
        {
            if (_isBound) return;
            _nextStageButton.onClick.AddListener(() => NextStageClicked?.Invoke());
            _improveButton.onClick.AddListener(() => ImproveClicked?.Invoke());
            _retryButton.onClick.AddListener(() => RetryClicked?.Invoke());
            for (int i = 0; i < _clearChoiceButtons.Length; i++)
            {
                int index = i;
                _clearChoiceButtons[i].onClick.AddListener(() => ClearModifierChoiceClicked?.Invoke(index));
            }
            _isBound = true;
        }

        /// <summary>결과 문구와 스테이지·진행 문구를 채우고 승리 결과일 때만 승리 전용 버튼을 표시한다.</summary>
        public void SetContent(string summary, string stage, string progress, bool isVictory)
        {
            _summaryLabel.text = summary;
            _stageLabel.text = stage;
            _progressLabel.text = progress;
            _winOnlyButtonRoot.SetActive(isVictory);
        }

        /// <summary>이번 미션에서 얻은 조각·Modifier를 중앙 아이콘 목록으로 갱신한다.</summary>
        public void SetRewards(IReadOnlyList<ResultRewardContent> rewards)
        {
            HideRewardTooltip();
            UiFactory.ClearChildren(_rewardItemsRoot);
            if (rewards == null) return;
            foreach (ResultRewardContent reward in rewards)
            {
                ResultRewardItemView item = Instantiate(_rewardItemPrefab, _rewardItemsRoot, false);
                item.SetContent(reward, HandleRewardHover);
            }
        }

        /// <summary>보상 아이콘에 포인터가 올라오면 이름·설명을 보여 주고 벗어나면 닫는다.</summary>
        private void HandleRewardHover(ResultRewardItemView item, bool isHovered)
        {
            if (!isHovered)
            {
                if (_hoveredRewardItem == item) HideRewardTooltip();
                return;
            }
            _hoveredRewardItem = item;
            _rewardTooltipTitle.text = item.Title;
            _rewardTooltipDescription.text = item.Description;
            _rewardTooltip.SetActive(true);
            PositionRewardTooltip(item);
        }

        /// <summary>현재 보상 정보 툴팁을 닫는다.</summary>
        private void HideRewardTooltip()
        {
            _hoveredRewardItem = null;
            _rewardTooltip.SetActive(false);
        }

        /// <summary>포인터가 올라온 아이콘 옆에 툴팁을 배치하고 화면 패널 경계 안으로 맞춘다.</summary>
        private void PositionRewardTooltip(ResultRewardItemView item)
        {
            RectTransform tooltipRect = (RectTransform)_rewardTooltip.transform;
            RectTransform parent = (RectTransform)tooltipRect.parent;
            ((RectTransform)item.transform).GetWorldCorners(_tooltipCorners);
            Vector2 itemTopRight = parent.InverseTransformPoint(_tooltipCorners[2]);
            Vector2 itemTopLeft = parent.InverseTransformPoint(_tooltipCorners[1]);
            float width = tooltipRect.rect.width;
            float height = tooltipRect.rect.height;
            float x = itemTopRight.x + 10f;
            if (x + width > parent.rect.xMax - 12f) x = itemTopLeft.x - width - 10f;
            float y = itemTopRight.y;
            if (y - height < parent.rect.yMin + 12f) y = itemTopRight.y + height + 10f;
            x = Mathf.Clamp(x, parent.rect.xMin + 12f, parent.rect.xMax - width - 12f);
            y = Mathf.Clamp(y, parent.rect.yMin + height + 12f, parent.rect.yMax - 12f);
            tooltipRect.anchoredPosition = new Vector2(x - parent.rect.xMin, y - parent.rect.yMax);
        }

        /// <summary>클리어 Modifier 후보 이름·효과·등급을 팝업 카드에 표시하고 선택 입력을 받는다.</summary>
        public void ShowClearModifierChoices(string title, IReadOnlyList<string> names, IReadOnlyList<string> descriptions, string grade)
        {
            HideRewardTooltip();
            _clearChoicePopup.SetActive(true);
            _clearChoiceTitleLabel.text = title;
            _rewardTitleLabel.gameObject.SetActive(false);
            _nextStageButton.interactable = false;
            _improveButton.interactable = false;
            _retryButton.interactable = false;
            GameObject firstChoice = null;
            for (int i = 0; i < _clearChoiceButtons.Length; i++)
            {
                bool hasChoice = names != null && i < names.Count;
                _clearChoiceCards[i].SetActive(hasChoice);
                if (!hasChoice) continue;
                if (firstChoice == null) firstChoice = _clearChoiceButtons[i].gameObject;
                _clearChoiceButtons[i].interactable = true;
                Color gradeColor = GetClearModifierGradeColor(grade);
                _clearChoiceGradeLabels[i].text = grade + "급";
                _clearChoiceGradeLabels[i].color = gradeColor;
                _clearChoiceNameLabels[i].text = names[i];
                _clearChoiceDescriptionLabels[i].text = descriptions != null && i < descriptions.Count ? descriptions[i] : string.Empty;
            }
            if (EventSystem.current != null && firstChoice != null)
                EventSystem.current.SetSelectedGameObject(firstChoice);
        }

        /// <summary>클리어 Modifier 선택 팝업을 닫고 결과 버튼을 다시 활성화한다.</summary>
        public void HideClearModifierChoices()
        {
            _clearChoicePopup.SetActive(false);
            _rewardTitleLabel.gameObject.SetActive(true);
            for (int i = 0; i < _clearChoiceButtons.Length; i++)
            {
                _clearChoiceCards[i].SetActive(false);
                _clearChoiceButtons[i].interactable = true;
            }
            _nextStageButton.interactable = true;
            _improveButton.interactable = true;
            _retryButton.interactable = true;
            if (EventSystem.current != null && gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(_improveButton.gameObject);
        }

        /// <summary>grade에 대응하는 보상 카드 등급 표시 색상을 반환한다.</summary>
        private static Color GetClearModifierGradeColor(string grade)
        {
            switch (grade)
            {
                case "C": return new Color(0.75f, 0.78f, 0.80f, 1f);
                case "B": return new Color(0.24f, 0.84f, 0.94f, 1f);
                case "A": return new Color(0.66f, 0.40f, 1f, 1f);
                case "S": return new Color(1f, 0.765f, 0.365f, 1f);
                default: return Color.white;
            }
        }
    }
}
