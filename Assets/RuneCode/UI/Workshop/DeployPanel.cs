using System;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>출격 탭 View다. 최고 단계·선택 스테이지·제한시간·마법 문구를 표시하고 스테이지 이동·출격 클릭을 알린다.</summary>
    public sealed class DeployPanel : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField] private TextMeshProUGUI _highestText;
        [SerializeField] private TextMeshProUGUI _stageText;
        [SerializeField] private TextMeshProUGUI _durationText;
        [SerializeField] private TextMeshProUGUI _spellText;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _battleModeText;

        [Header("조작")]
        [SerializeField] private Button _stageDownButton;
        [SerializeField] private Button _stageUpButton;
        [SerializeField] private Button _launchButton;

        private float _stageFontSize;
        private float _durationFontSize;
        private Vector2 _stageSize;
        private Vector2 _durationSize;
        private TextMeshProUGUI _launchLabel;
        private string _launchCaption;

        /// <summary>스테이지 이동 버튼을 눌렀을 때 방향(-1, +1)과 함께 알린다.</summary>
        public event Action<int> StageStepClicked;

        /// <summary>출격 버튼을 눌렀을 때 알린다.</summary>
        public event Action LaunchClicked;

        /// <summary>버튼 클릭을 이벤트로 연결한다. Presenter가 한 번만 호출한다.</summary>
        public void Bind()
        {
            _stageFontSize = _stageText.fontSize;
            _durationFontSize = _durationText.fontSize;
            _stageSize = _stageText.rectTransform.sizeDelta;
            _durationSize = _durationText.rectTransform.sizeDelta;
            _launchLabel = _launchButton.GetComponentInChildren<TextMeshProUGUI>();
            _launchCaption = _launchLabel.text;
            _stageDownButton.onClick.AddListener(() => StageStepClicked?.Invoke(-1));
            _stageUpButton.onClick.AddListener(() => StageStepClicked?.Invoke(1));
            _launchButton.onClick.AddListener(() => LaunchClicked?.Invoke());
        }

        /// <summary>최고 단계·선택 스테이지·제한시간·마법 문구를 표시한다.</summary>
        public void SetContent(string highest, string stage, string duration, string spell)
        {
            _highestText.text = highest;
            _stageText.text = stage;
            _durationText.text = duration;
            _spellText.text = spell;
        }

        /// <summary>공유 출격 패널의 제목과 조건 표시 크기를 현재 전투 종류에 맞춘다.</summary>
        public void SetPuzzleMode(bool isPuzzle)
        {
            _titleText.text = GameData.L(isPuzzle ? "ui.puzzle" : "ui.deploy");
            _descriptionText.text = GameData.L(isPuzzle ? "puzzle.description" : "ui.battleDescription");
            _battleModeText.gameObject.SetActive(!isPuzzle);
            _launchLabel.text = isPuzzle ? GameData.L("puzzle.launch") : _launchCaption;
            _stageText.fontSize = isPuzzle ? 19 : _stageFontSize;
            _stageText.rectTransform.sizeDelta = isPuzzle ? new Vector2(_stageSize.x, 48) : _stageSize;
            _durationText.fontSize = isPuzzle ? 18 : _durationFontSize;
            _durationText.rectTransform.sizeDelta = isPuzzle ? new Vector2(_durationSize.x, 75) : _durationSize;
        }
    }
}
