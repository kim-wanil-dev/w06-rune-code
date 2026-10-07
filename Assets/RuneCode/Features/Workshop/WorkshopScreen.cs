using System;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 작업실 화면의 헤더·탭·상태줄을 운영하고 편집 패널의 ISpellEditorHost 역할을 맡는다.
    /// 에디터 탭에서는 SpellEditorPanel과 도크를 함께 표시한다.
    /// </summary>
    public sealed class WorkshopScreen : MonoBehaviour, ISpellEditorHost
    {
        private static readonly string[] TABS = { "editor", "bench", "deploy", "settings" };

        [Header("탭 구성")]
        [SerializeField] private RectTransform _editorRoot;
        [SerializeField] private RectTransform _benchRoot;
        [SerializeField] private RectTransform _deployRoot;
        [SerializeField] private RectTransform _settingsRoot;
        [SerializeField] private Button[] _tabButtons;
        [SerializeField] private TextMeshProUGUI[] _tabLabels;

        [Header("패널")]
        [SerializeField] private SpellEditorPanel _spellEditor;
        [SerializeField] private DockPanel _dockPanel;
        [SerializeField] private BenchPanel _benchPanel;
        [SerializeField] private DeployPanel _deployPanel;
        [SerializeField] private SettingsPanel _settingsPanel;

        [Header("표시")]
        [SerializeField] private TextMeshProUGUI _headerStats;
        [SerializeField] private TextMeshProUGUI _status;

        private RuneCodeSession _session;
        private DockRun _dockRun;
        private string _selectedTab = "editor";
        private RuneSimulation _observedDock;
        private int _lastDockEventCount;

        /// <summary>세션과 도크 구동을 연결하고 패널 초기화와 탭 리스너 등록 후 에디터 탭으로 진입한다.</summary>
        public void Initialize(RuneCodeSession session)
        {
            _session = session;
            _dockRun = new DockRun(session);
            for (int i = 0; i < TABS.Length; i++)
            {
                int index = i;
                _tabButtons[i].onClick.AddListener(() => SetTab(TABS[index]));
            }
            _dockPanel.Initialize(session, _dockRun, _spellEditor);
            _spellEditor.Initialize(session.Spells, this);
            _benchPanel.Initialize(session, _dockRun, this);
            _deployPanel.Initialize(session);
            _settingsPanel.Initialize(session, this);
            _session.Spells.Compiled += OnSpellCompiled;
            SetTab("editor");
        }

        /// <summary>탭 패널의 활성 상태와 버튼 색을 바꾸고 진입 패널과 헤더를 갱신한다.</summary>
        public void SetTab(string tab)
        {
            _selectedTab = tab;
            _editorRoot.gameObject.SetActive(tab == "editor");
            _benchRoot.gameObject.SetActive(tab == "bench");
            _deployRoot.gameObject.SetActive(tab == "deploy");
            _settingsRoot.gameObject.SetActive(tab == "settings");
            if (tab == "editor") _spellEditor.Show();
            else _spellEditor.Hide();
            for (int i = 0; i < TABS.Length; i++)
            {
                Color accent = TABS[i] == tab ? UiTheme.Cyan : UiTheme.Muted;
                _tabLabels[i].color = accent;
                _tabButtons[i].image.color = new Color(accent.r * 0.19f + 0.03f, accent.g * 0.19f + 0.05f, accent.b * 0.19f + 0.07f);
            }
            if (tab == "bench") _benchPanel.Refresh();
            if (tab == "deploy") _deployPanel.Refresh();
            if (tab == "settings") _settingsPanel.Refresh();
            RefreshHeader();
        }

        /// <summary>도크를 다시 시작한 뒤 에디터 탭으로 전환하고 전체 패널을 갱신한다.</summary>
        public void ShowEditorTab()
        {
            _dockRun.ResetDock();
            SetTab("editor");
            _benchPanel.Refresh();
            _deployPanel.Refresh();
            RefreshHeader();
        }

        /// <summary>보유 조각과 RAM 사용량·용량의 헤더 수치를 갱신한다.</summary>
        public void RefreshHeader()
        {
            _headerStats.text = HeaderStatsText();
        }

        void Update()
        {
            if (_session == null) return;
            _session.Spells.Tick(Time.unscaledTime);
            _status.text = _session.StatusMessage;
            UpdateDockHighlight();
        }

        void OnDestroy()
        {
            if (_session != null) _session.Spells.Compiled -= OnSpellCompiled;
            if (_dockRun != null) _dockRun.Dispose();
        }

        int ISpellEditorHost.EquippedRam => _session.EquippedRam;

        int ISpellEditorHost.Capacity => _session.Capacity;

        int ISpellEditorHost.TestExecutionCount => _dockRun != null && _dockRun.Dock != null ? _dockRun.Dock.NodeExecutionCount : 0;

        void ISpellEditorHost.FireTest() => _dockRun.FireDock();

        void ISpellEditorHost.CompleteTutorial() => _session.AdvanceTutorial(3);

        /// <summary>컴파일 결과가 갱신되면 헤더 수치를 다시 표시한다.</summary>
        private void OnSpellCompiled()
        {
            RefreshHeader();
        }

        /// <summary>현재 보유 조각과 단일 마법의 RAM 사용량·용량을 작업실 머리글 문자열로 반환한다.</summary>
        private string HeaderStatsText()
        {
            return GameData.L("ui.fragments") + "  " + _session.Save.Currency + "   /   " + GameData.L("ui.capacity") + " " + _session.EquippedRam + "/" + _session.Capacity;
        }

        /// <summary>최근 도크 노드 실행을 12틱 이내 조건으로 편집 그래프 하이라이트로 전달한다.</summary>
        private void UpdateDockHighlight()
        {
            if (_selectedTab != "editor") return;
            RuneSimulation dock = _dockRun.Dock;
            if (dock == null) return;
            if (_observedDock != dock) { _observedDock = dock; _lastDockEventCount = 0; }
            int newEvents = dock.NodeExecutionCount - _lastDockEventCount;
            for (int index = Math.Max(0, dock.NodeEvents.Count - newEvents); index < dock.NodeEvents.Count; index++)
            {
                NodeExecutionEvent entry = dock.NodeEvents[index];
                if (dock.Tick - entry.Tick <= 12) _spellEditor.HighlightNode(entry.NodeId);
            }
            _lastDockEventCount = dock.NodeExecutionCount;
        }
    }
}
