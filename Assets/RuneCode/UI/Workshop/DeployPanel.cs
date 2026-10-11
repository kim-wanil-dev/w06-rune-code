using System;
using System.Collections.Generic;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>출격 탭 View다. 스테이지 정보·출현 적·마법 문구를 표시하고 스테이지 이동·출격 클릭을 알린다.</summary>
    public sealed class DeployPanel : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField] private TextMeshProUGUI _highestText;
        [SerializeField] private TextMeshProUGUI _stageText;
        [SerializeField] private TextMeshProUGUI _durationText;
        [SerializeField] private TextMeshProUGUI _spellText;

        [Header("출현 적 미리보기")]
        [SerializeField] private GameObject _enemyPreviewRoot;
        [SerializeField] private GameObject _bossStageBanner;
        [SerializeField] private EnemyPreview[] _enemyPreviews;

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

        /// <summary>1~10스테이지의 실제 출현 적 카드만 표시하고 보스 스테이지 배너를 전환한다.</summary>
        public void SetEnemyPreview(int stage, bool isBoss, ISet<string> enemyIds)
        {
            bool isVisible = stage >= 1 && stage <= 10;
            _enemyPreviewRoot.SetActive(isVisible);
            _bossStageBanner.SetActive(isVisible && isBoss);
            foreach (EnemyPreview preview in _enemyPreviews)
                preview.Root.SetActive(isVisible && enemyIds.Contains(preview.EnemyId));
        }

        [Serializable]
        private sealed class EnemyPreview
        {
            [Header("적 카드 연결")]
            [SerializeField] private string _enemyId;
            [SerializeField] private GameObject _root;

            public string EnemyId => _enemyId;
            public GameObject Root => _root;
        }
    }
}
