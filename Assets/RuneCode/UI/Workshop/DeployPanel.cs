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

        [Header("조작")]
        [SerializeField] private Button _stageDownButton;
        [SerializeField] private Button _stageUpButton;
        [SerializeField] private Button _launchButton;

        /// <summary>스테이지 이동 버튼을 눌렀을 때 방향(-1, +1)과 함께 알린다.</summary>
        public event Action<int> StageStepClicked;

        /// <summary>출격 버튼을 눌렀을 때 알린다.</summary>
        public event Action LaunchClicked;

        /// <summary>버튼 클릭을 이벤트로 연결한다. Presenter가 한 번만 호출한다.</summary>
        public void Bind()
        {
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
    }
}
