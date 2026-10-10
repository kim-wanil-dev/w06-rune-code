using System;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 작업실 화면 View다. 헤더·탭·상태줄과 탭별 패널 View를 묶고 탭 클릭을 알린다.
    /// 진행은 WorkshopPresenter가 하며, UIManager가 재사용하므로 초기화는 한 번, 진입 갱신은 표시할 때마다 한다.
    /// </summary>
    public sealed class WorkshopScreen : UiView
    {
        public const string TAB_EDITOR = "editor";
        public const string TAB_BENCH = "bench";
        public const string TAB_TREE = "tree";
        public const string TAB_DEPLOY = "deploy";
        public const string TAB_PUZZLE = "puzzle";
        public const string TAB_SETTINGS = "settings";

        private static readonly string[] TABS = { TAB_EDITOR, TAB_BENCH, TAB_TREE, TAB_DEPLOY, TAB_PUZZLE, TAB_SETTINGS };

        [Header("탭 구성")]
        [SerializeField] private RectTransform _editorRoot;
        [SerializeField] private RectTransform _benchRoot;
        [SerializeField] private RectTransform _treeRoot;
        [SerializeField] private RectTransform _deployRoot;
        [SerializeField] private RectTransform _settingsRoot;
        [SerializeField] private Button[] _tabButtons;
        [SerializeField] private TextMeshProUGUI[] _tabLabels;

        [Header("패널")]
        [SerializeField] private SpellEditorPanel _spellEditor;
        [SerializeField] private DockPanel _dockPanel;
        [SerializeField] private BenchPanel _benchPanel;
        [SerializeField] private UpgradeTreePanel _upgradeTreePanel;
        [SerializeField] private DeployPanel _deployPanel;
        [SerializeField] private SettingsPanel _settingsPanel;

        [Header("표시")]
        [SerializeField] private TextMeshProUGUI _headerStats;
        [SerializeField] private TextMeshProUGUI _status;

        private WorkshopPresenter _presenter;

        public SpellEditorPanel SpellEditor => _spellEditor;
        public DockPanel Dock => _dockPanel;
        public BenchPanel Bench => _benchPanel;
        public UpgradeTreePanel UpgradeTree => _upgradeTreePanel;
        public DeployPanel Deploy => _deployPanel;
        public SettingsPanel Settings => _settingsPanel;

        /// <summary>탭 버튼을 눌렀을 때 탭 ID와 함께 알린다.</summary>
        public event Action<string> TabClicked;

        /// <summary>탭 버튼을 이벤트로 연결하고 세션·UI 관리자로 Presenter를 만든다. 이미 초기화했으면 무시한다.</summary>
        public void Initialize(RuneCodeSession session, UIManager ui)
        {
            if (_presenter != null) return;
            for (int i = 0; i < TABS.Length; i++)
            {
                string tab = TABS[i];
                _tabButtons[i].onClick.AddListener(() => TabClicked?.Invoke(tab));
            }
            _presenter = new WorkshopPresenter(session, ui, this);
        }

        /// <summary>지정 탭의 패널만 활성화하고 탭 버튼을 선택 색으로 표시한다. 편집 패널 표시는 Presenter가 정한다.</summary>
        public void ShowTab(string tab)
        {
            _editorRoot.gameObject.SetActive(tab == TAB_EDITOR);
            _benchRoot.gameObject.SetActive(tab == TAB_BENCH);
            _treeRoot.gameObject.SetActive(tab == TAB_TREE);
            _deployRoot.gameObject.SetActive(tab == TAB_DEPLOY || tab == TAB_PUZZLE);
            _settingsRoot.gameObject.SetActive(tab == TAB_SETTINGS);
            for (int i = 0; i < TABS.Length; i++)
            {
                Color accent = TABS[i] == tab ? UiTheme.Cyan : UiTheme.Muted;
                _tabLabels[i].color = accent;
                _tabButtons[i].image.color = new Color(accent.r * 0.19f + 0.03f, accent.g * 0.19f + 0.05f, accent.b * 0.19f + 0.07f);
            }
        }

        /// <summary>헤더 수치 문구를 표시한다.</summary>
        public void SetHeader(string text) => _headerStats.text = text;

        /// <summary>하단 상태 문구를 표시한다.</summary>
        public void SetStatus(string text) => _status.text = text;

        /// <summary>작업실에 들어올 때마다 Presenter에 진입을 알린다.</summary>
        protected override void OnShown() => _presenter.Enter();

        /// <summary>작업실을 떠날 때 Presenter에 퇴장을 알린다.</summary>
        protected override void OnHidden() => _presenter.Exit();

        void Update()
        {
            _presenter?.Tick();
        }

        void OnDestroy()
        {
            _presenter?.Dispose();
        }
    }
}
