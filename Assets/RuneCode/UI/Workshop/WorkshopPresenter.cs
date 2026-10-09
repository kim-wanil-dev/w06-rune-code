using System;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 작업실 화면 Presenter다. 탭 Presenter(도크·벤치·강화 트리·출격·설정)를 조립하고 탭 전환, 헤더·상태줄, 도크 실행 하이라이트를 다룬다.
    /// 편집 패널(SpellEditorPanel)의 ISpellEditorHost 역할을 맡는다.
    /// </summary>
    public sealed class WorkshopPresenter : ISpellEditorHost, IDisposable
    {
        private const int HIGHLIGHT_TICKS = 12;

        private readonly RuneCodeSession _session;
        private readonly UIManager _ui;
        private readonly WorkshopScreen _view;
        private readonly DockRun _dockRun;
        private readonly DockPresenter _dock;
        private readonly BenchPresenter _bench;
        private readonly UpgradeTreePresenter _upgradeTree;
        private readonly DeployPresenter _deploy;
        private readonly SettingsPresenter _settings;

        private string _selectedTab = WorkshopScreen.TAB_EDITOR;
        private RuneSimulation _observedDock;
        private int _lastDockEventCount;

        /// <summary>세션·UI 관리자·작업실 View로 도크 구동과 탭 Presenter를 만들고 편집 패널과 탭 이벤트를 연결한다.</summary>
        public WorkshopPresenter(RuneCodeSession session, UIManager ui, WorkshopScreen view)
        {
            _session = session;
            _ui = ui;
            _view = view;
            _dockRun = new DockRun(session);
            _dock = new DockPresenter(session, _dockRun, view.Dock, IsEditorInputBlocked);
            view.SpellEditor.Initialize(session.Spells, this, ui);
            _bench = new BenchPresenter(session, view.Bench, RefreshProgression);
            _upgradeTree = new UpgradeTreePresenter(session, view.UpgradeTree, RefreshProgression);
            _deploy = new DeployPresenter(session, view.Deploy);
            _settings = new SettingsPresenter(session, ui, view.Settings, ShowEditorTab, RefreshHeader);
            _view.TabClicked += SetTab;
            _session.Spells.Compiled += RefreshHeader;
        }

        /// <summary>작업실에 들어올 때 도크를 다시 시작하고 에디터 탭으로 전체를 갱신한다.</summary>
        public void Enter()
        {
            ShowEditorTab();
        }

        /// <summary>작업실을 떠날 때 편집 패널을 닫고 도크 누적 시간을 버린다.</summary>
        public void Exit()
        {
            _view.SpellEditor.Hide();
            _dock.Suspend();
        }

        /// <summary>프레임마다 편집 세션 시간 처리, 상태줄 표시, 에디터 탭의 도크 진행과 실행 하이라이트를 한다.</summary>
        public void Tick()
        {
            _session.Spells.Tick(Time.unscaledTime);
            _view.SetStatus(_session.StatusMessage);
            if (_selectedTab != WorkshopScreen.TAB_EDITOR) return;
            _dock.Tick();
            ForwardDockHighlights();
        }

        /// <summary>컴파일 이벤트 구독을 풀고 도크 구동을 정리한다.</summary>
        public void Dispose()
        {
            _session.Spells.Compiled -= RefreshHeader;
            _dockRun.Dispose();
        }

        int ISpellEditorHost.EquippedRam => _session.EquippedRam;

        int ISpellEditorHost.Capacity => _session.Capacity;

        int ISpellEditorHost.TestExecutionCount => _dockRun.Dock != null ? _dockRun.Dock.NodeExecutionCount : 0;

        void ISpellEditorHost.FireTest() => _dockRun.FireDock();

        void ISpellEditorHost.CompleteTutorial() => _session.AdvanceTutorial(3);

        /// <summary>탭을 바꾸고 진입한 탭의 내용과 헤더를 갱신한다. 에디터 탭을 떠나면 편집 패널을 닫고 도크를 멈춘다.</summary>
        private void SetTab(string tab)
        {
            _selectedTab = tab;
            _view.ShowTab(tab);
            if (tab == WorkshopScreen.TAB_EDITOR) _view.SpellEditor.Show();
            else
            {
                _view.SpellEditor.Hide();
                _dock.Suspend();
            }
            if (tab == WorkshopScreen.TAB_BENCH) _bench.Refresh();
            if (tab == WorkshopScreen.TAB_TREE) _upgradeTree.Refresh();
            if (tab == WorkshopScreen.TAB_DEPLOY) _deploy.Refresh();
            if (tab == WorkshopScreen.TAB_SETTINGS) _settings.Refresh();
            RefreshHeader();
        }

        /// <summary>도크를 다시 시작한 뒤 에디터 탭으로 전환하고 벤치·출격·헤더를 갱신한다.</summary>
        private void ShowEditorTab()
        {
            _dockRun.ResetDock();
            SetTab(WorkshopScreen.TAB_EDITOR);
            _bench.Refresh();
            _deploy.Refresh();
        }

        /// <summary>구매 등으로 진행이 바뀌면 도크를 다시 시작하고 벤치·트리·출격·헤더를 갱신한다.</summary>
        private void RefreshProgression()
        {
            _dockRun.ResetDock();
            _bench.Refresh();
            _upgradeTree.Refresh();
            _deploy.Refresh();
            RefreshHeader();
        }

        /// <summary>보유 조각과 단일 마법의 RAM 사용량·용량을 헤더에 표시한다.</summary>
        private void RefreshHeader()
        {
            _view.SetHeader(GameData.L("ui.fragments") + "  " + _session.Save.Currency + "   /   " + GameData.L("ui.capacity") + " "
                + _session.EquippedRam + "/" + _session.Capacity);
        }

        /// <summary>팝업이 열려 있어 도크 클릭 시전을 막아야 하는지 반환한다.</summary>
        private bool IsEditorInputBlocked()
        {
            return _ui.IsPopupOpen;
        }

        /// <summary>최근 도크 노드 실행 중 하이라이트 유지 틱 이내의 것을 편집 그래프 하이라이트로 전달한다.</summary>
        private void ForwardDockHighlights()
        {
            RuneSimulation dock = _dockRun.Dock;
            if (dock == null) return;
            if (_observedDock != dock)
            {
                _observedDock = dock;
                _lastDockEventCount = 0;
            }
            int newEvents = dock.NodeExecutionCount - _lastDockEventCount;
            for (int index = Math.Max(0, dock.NodeEvents.Count - newEvents); index < dock.NodeEvents.Count; index++)
            {
                NodeExecutionEvent entry = dock.NodeEvents[index];
                if (dock.Tick - entry.Tick <= HIGHLIGHT_TICKS) _view.SpellEditor.HighlightNode(entry.NodeId);
            }
            _lastDockEventCount = dock.NodeExecutionCount;
        }
    }
}
