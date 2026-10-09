using System;

using UnityEngine;

using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 에디터 탭 하단의 시험 도크 View다. 경기장을 표시하고 시나리오·시험·리셋·자동 발사·적응·배속 클릭을 알리며
    /// 지표·상태 문구를 표시한다. 도크 구동과 입력 해석은 DockPresenter가 한다.
    /// </summary>
    public sealed class DockPanel : MonoBehaviour
    {
        [Header("경기장")]
        [SerializeField] private RuneArenaGraphic _arena;
        [SerializeField] private TMP_FontAsset _font;

        [Header("조작")]
        [SerializeField] private Button _scenarioButton;
        [SerializeField] private Button _testButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _autoFireButton;
        [SerializeField] private Button _adaptationButton;
        [SerializeField] private Button _speedButton;

        [Header("표시")]
        [SerializeField] private TextMeshProUGUI _metrics;
        [SerializeField] private TextMeshProUGUI _adaptationText;

        private TextMeshProUGUI _scenarioLabel;

        /// <summary>시나리오 버튼을 눌렀을 때 알린다.</summary>
        public event Action ScenarioClicked;

        /// <summary>시험 버튼을 눌렀을 때 알린다.</summary>
        public event Action TestClicked;

        /// <summary>리셋 버튼을 눌렀을 때 알린다.</summary>
        public event Action ResetClicked;

        /// <summary>자동 발사 버튼을 눌렀을 때 알린다.</summary>
        public event Action AutoFireClicked;

        /// <summary>적응 버튼을 눌렀을 때 알린다.</summary>
        public event Action AdaptationClicked;

        /// <summary>배속 버튼을 눌렀을 때 알린다.</summary>
        public event Action SpeedClicked;

        /// <summary>경기장에 그릴 시뮬레이션과 피드백 설정 조회 함수를 연결하고 버튼 클릭을 이벤트로 연결한다. Presenter가 한 번만 호출한다.</summary>
        public void Bind(Func<RuneSimulation> simulation, Func<bool> screenShake, Func<bool> hitStop)
        {
            _arena.Initialize(simulation, _font, screenShake, hitStop);
            _scenarioLabel = _scenarioButton.GetComponentInChildren<TextMeshProUGUI>();
            _scenarioButton.onClick.AddListener(() => ScenarioClicked?.Invoke());
            _testButton.onClick.AddListener(() => TestClicked?.Invoke());
            _resetButton.onClick.AddListener(() => ResetClicked?.Invoke());
            _autoFireButton.onClick.AddListener(() => AutoFireClicked?.Invoke());
            _adaptationButton.onClick.AddListener(() => AdaptationClicked?.Invoke());
            _speedButton.onClick.AddListener(() => SpeedClicked?.Invoke());
        }

        /// <summary>시나리오 버튼 문구를 표시한다.</summary>
        public void SetScenario(string text) => _scenarioLabel.text = text;

        /// <summary>피해·DPS·에너지·동시 개체 지표 문구를 표시한다.</summary>
        public void SetMetrics(string text) => _metrics.text = text;

        /// <summary>자동 발사·배속·적응 상태와 적응 학습 수치 문구를 표시한다.</summary>
        public void SetStatus(string text) => _adaptationText.text = text;

        /// <summary>마우스가 경기장 안에 있으면 시뮬레이션 좌표의 조준점을 반환한다.</summary>
        public bool TryGetPointer(out SimVector pointer)
        {
            pointer = SimVector.Zero;
            Mouse mouse = Mouse.current;
            return mouse != null && _arena.TryGetPointer(mouse.position.ReadValue(), out pointer);
        }
    }
}
