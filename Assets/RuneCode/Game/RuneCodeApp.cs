using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using UnityEngine;

using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RuneCode
{
    public enum AppScreen { Title, Workshop, Mission, Result }

    public sealed class RuneCodeApp : MonoBehaviour
    {
        private const int HISTORY_LIMIT = 50;
        private const double FIXED_STEP = 1.0 / RuneSimulation.TICK_RATE;
        private const float COMPILE_DEBOUNCE = 0.2f;
        private const float MIN_DOCK_SPEED = 0.5f;
        private const float MAX_DOCK_SPEED = 2f;

        [Header("화면 구성")]
        [SerializeField] private TMP_FontAsset _font;

        private RuneCodeView _view;
        private PlayerSave _save;
        private SpellGraph _editingGraph;
        private SpellGraph _previousGraph;
        private SpellProgram _program;
        private readonly List<SpellGraph> _undo = new List<SpellGraph>();
        private readonly List<SpellGraph> _redo = new List<SpellGraph>();
        private readonly List<GraphNode> _copiedNodes = new List<GraphNode>();
        private readonly List<GraphEdge> _copiedEdges = new List<GraphEdge>();
        private RuneSimulation _mission;
        private RuneSimulation _dock;
        private IncrementalDefinition _incremental;
        private AppScreen _screen;
        private string _tab = "editor";
        private string _statusMessage;
        private string _lastResult;
        private int _activeSlot;
        private int _nextId;
        private int _missionTelemetryCount;
        private int _dockTelemetryCount;
        private bool _isTerminalEditor;
        private bool _isPaused;
        private bool _graphDirty;
        private bool _debugEnabled;
        private bool _settled;
        private bool _hasPendingMissionDash;
        private bool _hasPendingMissionCast;
        private bool _hasPendingDockCast;
        private bool _isGraphOverlayVisible = true;
        private float _dockSpeed = 1;
        private float _compileAt;
        private double _missionAccumulator;
        private double _dockAccumulator;
        private string _dockScenario = "dummy_single";

        public AppScreen Screen => _screen;
        public PlayerSave Save => _save;
        public SpellGraph EditingGraph => _editingGraph;
        public SpellGraph CurrentGraph => _editingGraph;
        public SpellProgram Program => _program;
        public RuneSimulation Mission => _mission;
        public RuneSimulation Dock => _dock;
        public bool IsMission => _screen == AppScreen.Mission;
        public bool IsTerminal => _mission != null && _mission.Stage == MissionStage.Terminal;
        public bool IsTerminalEditor => _isTerminalEditor;
        public bool IsPaused => _isPaused;
        public bool IsGraphOverlayVisible => _isGraphOverlayVisible;
        public int ActiveSlot => _activeSlot;
        public int CurrentSlot => _activeSlot;
        public string Tab => _tab;
        public string StatusMessage => _statusMessage;
        public string LastResult => _lastResult;
        public string ResultSummary => _lastResult;
        public bool Completed => _mission != null && _mission.Stage == MissionStage.Cleared;
        public int TutorialStep => _save.TutorialStep;
        public bool DebugEnabled => _debugEnabled;
        public bool IsDebugEnabled => _debugEnabled;
        public float DockSpeed => _dockSpeed;
        public float MaxEnergy => GameData.Balance.Spell.ManaMax + _save.EnergyLevel * GameData.Balance.Economy.StatStep;
        public float MaxHp => GameData.Balance.Player.MaxHp + _save.HpLevel * GameData.Balance.Economy.StatStep;
        public int SelectedStage => _save.SelectedStage;
        public int HighestClearedStage => _save.HighestClearedStage;
        public float BattleDuration => (float)_incremental.BaseDuration + _save.DurationLevel * GameData.Balance.Economy.DurationStep;
        public float EnergyRegen => GameData.Balance.Spell.ManaRegen + _save.EnergyLevel * GameData.Balance.Economy.EnergyRegenStep;
        public bool RunIsAutomatic => false;

        void Awake()
        {
            GameData.Load();
            if (SimulationCli.TryRunCommandLine()) { enabled = false; return; }
            Application.targetFrameRate = GameData.Balance.Sim.TickRate;
            _save = SaveStore.Load();
            _incremental = RuneSimulation.LoadIncrementalDefinition();
            _statusMessage = SaveStore.LastWarning;
            _debugEnabled = Environment.GetCommandLineArgs().Contains("-debug") || Application.absoluteURL.Contains("debug=1");
            _nextId = _save.Library.Count + 1;
            _editingGraph = _save.FindGraph(_save.ActiveSpellId).Clone();
            _previousGraph = _editingGraph.Clone();
            _screen = AppScreen.Title;
            _view = gameObject.AddComponent<RuneCodeView>();
            if (_font == null) _font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 36);
            _view.Font = _font;
            RebuildProgram();
            StartDock(_dockScenario);
            _view.Initialize(this);
            LocalTelemetry.Record(0, "session", "start");
        }

        void Update()
        {
            if (_graphDirty && Time.unscaledTime >= _compileAt)
            {
                _graphDirty = false;
                RebuildProgram();
                SaveGraph();
                _view.RefreshGraph();
            }
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var isTyping = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
            if (!isTyping && keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (_isTerminalEditor) CloseTerminal();
                    else if (IsMission) TogglePause();
                }
                if ((keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) && keyboard.zKey.wasPressedThisFrame) Undo();
                if ((keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) && keyboard.yKey.wasPressedThisFrame) Redo();
                if (keyboard.f8Key.wasPressedThisFrame) LocalTelemetry.DumpToConsole();
                if (keyboard.gKey.wasPressedThisFrame) ToggleGraphOverlay();
            }
            if (IsMission && !_isPaused && !_isTerminalEditor)
            {
                var previousStage = _mission.Stage;
                var movement = SimVector.Zero;
                if (!isTyping && keyboard != null)
                {
                    movement = new SimVector((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                        (keyboard.sKey.isPressed ? 1 : 0) - (keyboard.wKey.isPressed ? 1 : 0));
                    _hasPendingMissionDash |= keyboard.spaceKey.wasPressedThisFrame;
                }
                var pointer = SimVector.Zero;
                var hasPointer = !isTyping && _view.IsPointerInMission && _view.TryGetMissionPointer(out pointer);
                if (!isTyping && mouse != null && _view.IsPointerInMission && mouse.leftButton.wasPressedThisFrame) _hasPendingMissionCast = true;
                var maxSteps = GameData.Balance.Limits.MaxFrameSteps;
                _missionAccumulator = Math.Min(_missionAccumulator + Time.unscaledDeltaTime, FIXED_STEP * maxSteps);
                var steps = 0;
                while (_missionAccumulator >= FIXED_STEP && steps++ < maxSteps)
                {
                    var aim = hasPointer ? pointer - _mission.Player.Position : _mission.Player.AimDirection;
                    _mission.Step(new SimulationInput(movement, aim, _hasPendingMissionCast, false, false, _hasPendingMissionDash));
                    _hasPendingMissionDash = false;
                    _hasPendingMissionCast = false;
                    _missionAccumulator -= FIXED_STEP;
                }
                if (_mission.Stage != previousStage) _view.Refresh();
                CaptureNodeTelemetry(_mission, "mission", ref _missionTelemetryCount);
                if (_mission.Stage == MissionStage.Dead || _mission.Stage == MissionStage.Cleared) FinishMission();
            }
            else { _missionAccumulator = 0; _hasPendingMissionDash = false; _hasPendingMissionCast = false; }
            if ((_screen == AppScreen.Workshop && _tab == "editor") || _isTerminalEditor)
            {
                var aim = new SimVector(1, 0);
                if (_view.TryGetDockPointer(out var point)) aim = point - _dock.Player.Position;
                if (!isTyping && mouse != null && _view.IsPointerInDock && mouse.leftButton.wasPressedThisFrame) _hasPendingDockCast = true;
                if (!isTyping && keyboard != null && keyboard.rKey.wasPressedThisFrame) ResetDock();
                var maxSteps = GameData.Balance.Limits.MaxFrameSteps;
                _dockAccumulator = Math.Min(_dockAccumulator + Time.unscaledDeltaTime * _dockSpeed, FIXED_STEP * maxSteps);
                var steps = 0;
                while (_dockAccumulator >= FIXED_STEP && steps++ < maxSteps)
                {
                    _dock.Step(new SimulationInput(SimVector.Zero, aim, _hasPendingDockCast));
                    _hasPendingDockCast = false;
                    _dockAccumulator -= FIXED_STEP;
                }
                CaptureNodeTelemetry(_dock, "dock", ref _dockTelemetryCount);
            }
            else { _dockAccumulator = 0; _hasPendingDockCast = false; }
        }

        void OnApplicationQuit() { SaveGraph(); }

        /// <summary>편집 그래프로 실행 그래프를 다시 만든다. 서명이 바뀌었을 때만 도크에 새 프로그램을 넘겨 토큰과 투사체 등 실행 상태를 지운다.</summary>
        private void RebuildProgram()
        {
            var previous = _program;
            _program = SpellProgram.Build(_editingGraph, GameData.Balance.Spell);
            if (_dock != null && (previous == null || previous.Signature != _program.Signature)) _dock.SetProgram(_program);
        }

        /// <summary>새로 실행된 노드만 로컬 링 버퍼에 기록하여 동일 tick 실행도 보존한다.</summary>
        private void CaptureNodeTelemetry(RuneSimulation simulation, string source, ref int recordedCount)
        {
            var count = simulation.NodeExecutionCount - recordedCount;
            var start = Math.Max(0, simulation.NodeEvents.Count - count);
            for (var index = start; index < simulation.NodeEvents.Count; index++)
            {
                var entry = simulation.NodeEvents[index];
                LocalTelemetry.Record(entry.Tick, source + ".node", entry.NodeId);
            }
            recordedCount = simulation.NodeExecutionCount;
        }

        /// <summary>진행을 저장하고 작업실의 현재 탭을 표시한다.</summary>
        public void StartWorkshop()
        {
            if (IsMission && !_settled) return;
            _screen = AppScreen.Workshop;
            _isPaused = false;
            _isTerminalEditor = false;
            _view.Refresh();
        }

        /// <summary>작업실에서 에디터·벤치·출격·설정 중 전달된 탭을 선택한다.</summary>
        public void SetTab(string tab) { if (_screen != AppScreen.Workshop) return; _tab = tab; _view.Refresh(); }

        /// <summary>보관함의 마법을 편집 사본으로 열고 연결된 슬롯 또는 보관함 선택 상태를 갱신한다.</summary>
        public void SelectSpell(string id)
        {
            if (!CanEdit() || id != _save.ActiveSpellId) return;
            SaveGraph();
            var graph = _save.FindGraph(id);
            if (graph == null) return;
            _editingGraph = graph.Clone();
            _activeSlot = -1;
            for (var i = 0; i < _save.SlotCount; i++) if (_save.Loadout[i] == id) _activeSlot = i;
            _undo.Clear();
            _redo.Clear();
            _previousGraph = _editingGraph.Clone();
            RebuildProgram();
            _view.Refresh();
        }

        /// <summary>보관함 마법 ID를 선택한다.</summary>
        public void SelectLibrary(string id) { SelectSpell(id); }

        /// <summary>해금된 슬롯의 장착 마법을 열며 빈 슬롯이면 장착할 보관함 마법 선택을 안내한다.</summary>
        public void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= _save.SlotCount) return;
            if (!string.IsNullOrEmpty(_save.Loadout[slot])) SelectSpell(_save.Loadout[slot]);
            else { _activeSlot = slot; SetStatus("editor.emptySlot"); }
        }

        /// <summary>현재 단일 마법의 편집 사본을 반영하고 버전 3 파일에 저장한다.</summary>
        public void SaveGraph()
        {
            if (_save == null || _editingGraph == null) return;
            _save.StoreGraph(_editingGraph);
            if (!SaveStore.Write(_save)) _statusMessage = SaveStore.LastWarning;
        }

        /// <summary>추가 마법 생성 요청에 단일 마법 성장 규칙을 안내한다.</summary>
        public void NewSpell()
        {
            SetStatus("editor.singleSpell");
        }

        /// <summary>추가 마법 생성 요청에 단일 마법 성장 규칙을 안내한다.</summary>
        public void NewGraph() { NewSpell(); }

        /// <summary>마법 복제 요청에 단일 마법 성장 규칙을 안내한다.</summary>
        public void DuplicateSpell()
        {
            SetStatus("editor.singleSpell");
        }

        /// <summary>마법 복제 요청에 단일 마법 성장 규칙을 안내한다.</summary>
        public void DuplicateGraph() { DuplicateSpell(); }

        /// <summary>현재 단일 마법 삭제를 거부하고 성장 규칙을 안내한다.</summary>
        public void DeleteSpell()
        {
            SetStatus("editor.singleSpell");
        }

        /// <summary>현재 단일 마법 삭제를 거부하고 성장 규칙을 안내한다.</summary>
        public void DeleteGraph() { DeleteSpell(); }

        /// <summary>공백이 아닌 이름을 제한된 길이로 현재 마법에 반영한다.</summary>
        public void RenameSpell(string name)
        {
            if (!CanEdit() || string.IsNullOrWhiteSpace(name)) return;
            _editingGraph.Rename(name.Trim().Substring(0, Math.Min(40, name.Trim().Length)));
            ChangedGraph(); _view.Refresh();
        }

        /// <summary>현재 마법의 표시 이름을 변경한다.</summary>
        public void RenameGraph(string name) { RenameSpell(name); }

        /// <summary>실행 그래프 검증을 통과한 현재 마법을 지정 슬롯에 장착한다. 슬롯은 첫 번째만 사용한다.</summary>
        public void Equip(int slot)
        {
            if (!CanEdit() || slot != 0) return;
            RebuildProgram();
            if (!_program.IsValid) { SetStatus("editor.invalidEquip"); return; }
            SaveGraph(); _save.SetLoadout(slot, _editingGraph.Id); _activeSlot = slot;
            SaveStore.Write(_save); _view.Refresh();
        }

        /// <summary>현재 마법을 지정 슬롯에 장착한다.</summary>
        public void EquipCurrent(int slot) { Equip(slot); }

        /// <summary>단일 마법의 장착 해제 요청을 거부하고 성장 규칙을 안내한다.</summary>
        public void Unequip(int slot) { SetStatus("editor.singleSpell"); }

        /// <summary>노드 종류 ID의 노드를 지정 좌표에 추가하고 그래프를 변경한다. 시전 노드는 추가할 수 없고, 적중 노드는 그래프에 하나만 허용하며, 노드 기술 상한을 넘으면 추가하지 않는다.</summary>
        public void AddRune(string runeId, float x = 300, float y = 150)
        {
            if (!CanEdit()) return;
            if (!SpellNodes.TryParse(runeId, out var kind) || kind == SpellNodeKind.Cast) return;
            if (kind == SpellNodeKind.OnHit && HasNodeOfKind(_editingGraph, SpellNodeKind.OnHit)) { SetStatus("editor.onHitLimit"); return; }
            if (_editingGraph.Nodes.Count >= GameData.Balance.Limits.MaxGraphNodes) { SetStatus("editor.graphLimit"); return; }
            _editingGraph.AddNode(new GraphNode(NewId("node"), runeId, x, y));
            ChangedGraph(); _view.RefreshGraph();
        }

        /// <summary>출력 포트에서 입력 포트로 엣지를 잇는다. 같은 출력 포트의 기존 엣지는 교체하고, 시전·적중 입력과 포트 범위 위반은 거부하며, 상태 변경 시 true를 반환한다.</summary>
        public bool Connect(string fromNode, string fromPort, string toNode, string toPort)
        {
            if (!CanEdit()) return false;
            var source = _editingGraph.FindNode(fromNode);
            var target = _editingGraph.FindNode(toNode);
            if (source == null || target == null || !SpellNodes.TryParse(source.RuneId, out var fromKind)
                || !SpellNodes.TryParse(target.RuneId, out var toKind) || !TryParsePort(fromPort, out var outPort) || !TryParsePort(toPort, out var inPort))
            { SetStatus("editor.invalidConnection"); return false; }
            if (toKind == SpellNodeKind.Cast || toKind == SpellNodeKind.OnHit) { SetStatus("editor.noInput"); return false; }
            if (outPort >= SpellNodes.GetOutputCount(fromKind) || inPort >= SpellNodes.GetInputCount(toKind))
            { SetStatus("editor.invalidConnection"); return false; }
            var existing = FindOutgoingEdge(_editingGraph, fromNode, fromPort);
            if (existing != null && existing.ToNode == toNode && existing.ToPort == toPort) return true;
            if (existing != null) _editingGraph.RemoveEdge(existing.Id);
            else if (_editingGraph.Edges.Count >= GameData.Balance.Limits.MaxGraphEdges)
            { SetStatus("editor.graphLimit"); return false; }
            _editingGraph.AddEdge(new GraphEdge(NewId("edge"), fromNode, fromPort, toNode, toPort));
            ChangedGraph(); _view.RefreshGraph();
            return true;
        }

        /// <summary>시전 노드를 제외한 지정 노드와 관련 엣지를 제거하고 그래프를 변경한다.</summary>
        public void RemoveNode(string id) { if (!CanEdit()) return; _editingGraph.RemoveNode(id); ChangedGraph(); _view.RefreshGraph(); }

        /// <summary>지정한 엣지를 삭제하고 그래프를 변경한다.</summary>
        public void RemoveEdge(string id) { if (!CanEdit()) return; _editingGraph.RemoveEdge(id); ChangedGraph(); _view.RefreshGraph(); }

        /// <summary>숫자 파라미터를 선택 노드에 저장하고 그래프 변경을 기록한다.</summary>
        public void SetNodeNumber(string id, string key, float value)
        { if (!CanEdit()) return; var node = _editingGraph.Nodes.FirstOrDefault(item => item.Id == id); if (node == null) return; node.SetNumber(key, value); ChangedGraph(); }

        /// <summary>열거 파라미터를 선택 노드에 저장하고 그래프 변경을 기록한다.</summary>
        public void SetNodeText(string id, string key, string value)
        { if (!CanEdit()) return; var node = _editingGraph.Nodes.FirstOrDefault(item => item.Id == id); if (node == null) return; node.SetText(key, value); ChangedGraph(); }

        /// <summary>그래프 변경을 undo 기록에 남기고, 도크의 실행 상태를 지우고, 실행 그래프 재생성을 디바운스로 예약한다.</summary>
        public void ChangedGraph()
        {
            if (_previousGraph != null && ShareCodec.Serialize(_previousGraph) != ShareCodec.Serialize(_editingGraph))
            {
                _undo.Add(_previousGraph.Clone());
                if (_undo.Count > HISTORY_LIMIT) _undo.RemoveAt(0);
                _redo.Clear(); _previousGraph = _editingGraph.Clone();
            }
            _dock?.ClearSpellState();
            _graphDirty = true;
            _compileAt = Time.unscaledTime + COMPILE_DEBOUNCE;
        }

        /// <summary>드래그 시작 시 마지막 확정 그래프를 되돌리기용 사본으로 보관한다.</summary>
        public void BeginGraphEdit() { _previousGraph = _editingGraph.Clone(); }

        /// <summary>이전 그래프 스냅샷으로 되돌리고 현재 상태를 다시 실행 목록에 기록한다.</summary>
        public void Undo()
        {
            if (_undo.Count == 0 || !CanEdit()) return;
            _redo.Add(_editingGraph.Clone()); _editingGraph = _undo[_undo.Count - 1]; _undo.RemoveAt(_undo.Count - 1);
            _dock?.ClearSpellState();
            _previousGraph = _editingGraph.Clone(); RebuildProgram(); SaveGraph(); _view.RefreshGraph();
        }

        /// <summary>되돌린 그래프 스냅샷을 복구한다.</summary>
        public void Redo()
        {
            if (_redo.Count == 0 || !CanEdit()) return;
            _undo.Add(_editingGraph.Clone()); _editingGraph = _redo[_redo.Count - 1]; _redo.RemoveAt(_redo.Count - 1);
            _dock?.ClearSpellState();
            _previousGraph = _editingGraph.Clone(); RebuildProgram(); SaveGraph(); _view.RefreshGraph();
        }

        /// <summary>선택된 노드와 내부 연결을 별도 사본에 기록한다. 시전 노드는 복사 대상에서 제외한다.</summary>
        public void CopyNodes(IEnumerable<string> ids)
        {
            var selected = new HashSet<string>(ids);
            var snapshot = _editingGraph.Clone();
            _copiedNodes.Clear(); _copiedEdges.Clear();
            _copiedNodes.AddRange(snapshot.Nodes.Where(node => selected.Contains(node.Id) && node.RuneId != SpellNodes.CAST_ID));
            _copiedEdges.AddRange(snapshot.Edges.Where(edge => _copiedNodes.Any(node => node.Id == edge.FromNode) && _copiedNodes.Any(node => node.Id == edge.ToNode)));
        }

        /// <summary>복사 노드에 새 ID와 좌표 오프셋을 부여해 붙여넣는다. 그래프에 적중 노드가 이미 있으면 복사본의 적중 노드와 그 엣지를 건너뛰고, 같은 출력 포트의 중복 엣지도 건너뛴다.</summary>
        public void PasteNodes()
        {
            if (_copiedNodes.Count == 0 || !CanEdit()) return;
            if (_editingGraph.Nodes.Count + _copiedNodes.Count > GameData.Balance.Limits.MaxGraphNodes ||
                _editingGraph.Edges.Count + _copiedEdges.Count > GameData.Balance.Limits.MaxGraphEdges)
            { SetStatus("editor.graphLimit"); return; }
            var candidate = _editingGraph.Clone();
            var map = new Dictionary<string, string>();
            var hasOnHit = HasNodeOfKind(candidate, SpellNodeKind.OnHit);
            foreach (var node in _copiedNodes)
            {
                if (SpellNodes.TryParse(node.RuneId, out var kind) && kind == SpellNodeKind.OnHit)
                {
                    if (hasOnHit) continue;
                    hasOnHit = true;
                }
                var clone = node.Clone(NewId("node")); clone.Move(node.X + 30, node.Y + 30); map[node.Id] = clone.Id; candidate.AddNode(clone);
            }
            foreach (var edge in _copiedEdges)
            {
                if (!map.ContainsKey(edge.FromNode) || !map.ContainsKey(edge.ToNode)) continue;
                if (FindOutgoingEdge(candidate, map[edge.FromNode], edge.FromPort) != null) continue;
                candidate.AddEdge(new GraphEdge(NewId("edge"), map[edge.FromNode], edge.FromPort, map[edge.ToNode], edge.ToPort));
            }
            _editingGraph = candidate; ChangedGraph(); _view.RefreshGraph();
        }

        /// <summary>그래프의 노드 순서대로 5열 격자에 좌표를 배치하고 그래프를 변경한다.</summary>
        public void AutoArrange()
        {
            if (!CanEdit()) return;
            for (var i = 0; i < _editingGraph.Nodes.Count; i++) _editingGraph.Nodes[i].Move(35 + (i % 5) * 190, 55 + (i / 5) * 130);
            ChangedGraph(); _view.RefreshGraph();
        }

        /// <summary>선택 시나리오의 독립 시뮬레이션을 만들고 현재 실행 그래프를 연결한다. 실행 그래프가 없으면 먼저 만든다.</summary>
        public void StartDock(string scenario)
        {
            _dockScenario = scenario;
            if (_program == null) RebuildProgram();
            _dock = new RuneSimulation(1, false, MaxHp, MaxEnergy, energyRegen: EnergyRegen);
            _dock.ResetBench(scenario); _dock.SetProgram(_program);
            _dockAccumulator = 0;
            _dockTelemetryCount = 0;
        }

        /// <summary>도크를 현재 또는 전달된 시나리오의 초기 상태로 복구한다.</summary>
        public void ResetDock(string scenario = null) { StartDock(scenario ?? _dockScenario); _view.Refresh(); }

        /// <summary>도크의 시간 배율을 최소 0.5에서 최대 2 사이로 제한해 저장한다.</summary>
        public void SetDockSpeed(float speed) { _dockSpeed = Math.Max(MIN_DOCK_SPEED, Math.Min(MAX_DOCK_SPEED, speed)); }

        /// <summary>전투 그래프 오버레이의 표시 여부를 뒤집는다.</summary>
        public void ToggleGraphOverlay() { _isGraphOverlayVisible = !_isGraphOverlayVisible; }

        /// <summary>실행 그래프를 갱신한 뒤 도크에 시전을 한 번 요청한다. 성공하면 튜토리얼 시험 단계를 저장하고, 프로그램 오류나 마나·과부하 실패면 상태 메시지를 남긴다.</summary>
        public void FireDock()
        {
            RebuildProgram();
            if (!_program.IsValid) { SetStatus("editor.invalidCast"); return; }
            var result = _dock.TryCast();
            if (result == CastResult.NoMana) { SetStatus("cast.noMana"); return; }
            if (result == CastResult.Overload) { SetStatus("cast.overload"); return; }
            if (result != CastResult.Success) return;
            _save.SetTutorialStep(3); SaveStore.Write(_save);
            LocalTelemetry.Record(_dock.Tick, "bench.cast", _program.Signature); _view.RefreshGraph();
        }

        /// <summary>일반 전투 중 편집 불가 규칙에 따라 현재 화면의 편집 허용 상태를 반환한다.</summary>
        private bool CanEdit() { return _screen == AppScreen.Workshop && _tab == "editor"; }

        /// <summary>현재 단일 마법을 저장하고 실행 그래프를 검증한 뒤 선택 스테이지의 제한시간·영구 능력치 전투를 시작한다. 프로그램이 유효하지 않으면 시작하지 않는다.</summary>
        public void Deploy()
        {
            if (IsMission && !_settled) return;
            SaveGraph(); RebuildProgram();
            if (!_program.IsValid) { SetStatus("editor.invalidEquip"); return; }
            _mission = new RuneSimulation(1, true, MaxHp, MaxEnergy, SelectedStage, BattleDuration, EnergyRegen);
            _mission.SetProgram(_program);
            _screen = AppScreen.Mission; _isPaused = false; _isTerminalEditor = false; _settled = false;
            _missionAccumulator = 0; _missionTelemetryCount = 0; _hasPendingMissionDash = false; _hasPendingMissionCast = false;
            LocalTelemetry.Record(0, "mission.start", SelectedStage + ":" + _program.Signature);
            _view.Refresh();
        }

        /// <summary>현재 단일 마법으로 선택 스테이지를 시작한다.</summary>
        public void StartMission() { Deploy(); }

        /// <summary>해금된 범위 안에서 출격 스테이지를 선택하고 저장한다.</summary>
        public void SelectStage(int stage)
        {
            if (IsMission || stage < 1 || stage > HighestClearedStage + 1) return;
            _save.SelectStage(stage); SaveStore.Write(_save); _view.Refresh();
        }

        /// <summary>정산된 결과의 스테이지를 다시 선택하여 같은 마법으로 재도전한다.</summary>
        public void RetryStage()
        {
            if (_screen != AppScreen.Result || _mission == null || !_settled) return;
            _save.SelectStage(_mission.StageNumber); Deploy();
        }

        /// <summary>시간제 전투의 터미널 편집 요청을 거부한다.</summary>
        public void InteractTerminal() { }

        /// <summary>시간제 전투의 터미널 편집 요청을 거부한다.</summary>
        public void OpenTerminalEditor() { }

        /// <summary>시간제 전투에는 터미널이 없으므로 편집 종료 요청을 무시한다.</summary>
        public void CloseTerminal() { }

        /// <summary>시간제 전투에는 터미널이 없으므로 편집 종료 요청을 무시한다.</summary>
        public void CloseTerminalEditor() { }

        /// <summary>시간제 전투에는 방 전환이 없으므로 다음 방 요청을 무시한다.</summary>
        public void ContinueMission() { }

        /// <summary>미션의 고정 시뮬레이션 갱신을 일시정지하거나 재개한다.</summary>
        public void TogglePause() { _isPaused = !_isPaused; _view.Refresh(); }

        /// <summary>시간 종료 또는 사망 보상을 한 번 정산하고 성공 시 다음 스테이지를 해금한다.</summary>
        private void FinishMission()
        {
            if (_settled) return;
            _settled = true;
            var cleared = _mission.Stage == MissionStage.Cleared;
            var fragments = _mission.SettlementFragments;
            if (cleared) _save.RecordStageClear(_mission.StageNumber);
            _save.Settle(fragments, cleared);
            foreach (var pair in _mission.KillCounts) _save.RecordKills(pair.Key, pair.Value);
            SaveStore.Write(_save);
            _lastResult = GameData.L(cleared ? "result.timed" : "result.dead") + "\n" + GameData.L("result.fragments") + " " + fragments;
            _screen = AppScreen.Result; _isPaused = false; _isTerminalEditor = false;
            LocalTelemetry.Record(_mission.Tick, "mission.result", (cleared ? "clear" : "death") + ":" + fragments);
            _view.Refresh();
        }

        /// <summary>이미 결과가 정산된 미션 또는 작업실 화면에서 작업실로 돌아간다.</summary>
        public void ReturnWorkshop()
        {
            if (IsMission && !_settled) { SetStatus("mission.resumeRequired"); return; }
            _screen = AppScreen.Workshop; _tab = "editor"; _isPaused = false; _isTerminalEditor = false;
            StartDock(_dockScenario); _view.Refresh();
        }

        /// <summary>전투를 중단하여 사망과 같은 70% 비율로 현재 획득 조각을 정산한다.</summary>
        public void AbandonMission()
        {
            if (_mission == null || _settled) return;
            _settled = true;
            var fragments = (int)Math.Round(_mission.EarnedFragments * GameData.Balance.Economy.DeathRetention, MidpointRounding.AwayFromZero);
            _save.Settle(fragments, false); foreach (var pair in _mission.KillCounts) _save.RecordKills(pair.Key, pair.Value);
            SaveStore.Write(_save); _lastResult = GameData.L("result.retreat") + "\n" + GameData.L("result.fragments") + " " + fragments;
            _screen = AppScreen.Result; _isPaused = false; _isTerminalEditor = false; _view.Refresh();
        }

        /// <summary>성장 항목(energy, duration)의 현재 단계 다음 구매 비용을 반환하며, 그 밖의 항목이거나 상한에 도달했으면 -1을 반환한다.</summary>
        public int GetUpgradeCost(string kind)
        {
            var economy = GameData.Balance.Economy;
            switch (kind)
            {
                case "energy": return economy.GetGrowthCost(kind, _save.EnergyLevel);
                case "duration": return economy.GetGrowthCost(kind, _save.DurationLevel);
                default: return -1;
            }
        }

        /// <summary>성장 단계와 비용을 확인한 후 선택 성장 항목을 구매하고 실행 그래프와 도크를 갱신한다.</summary>
        public void BuyUpgrade(string kind)
        {
            if (_screen != AppScreen.Workshop) return;
            var cost = GetUpgradeCost(kind);
            if (cost < 0) { SetStatus("bench.maxed"); return; }
            if (!_save.Spend(cost)) { SetStatus("bench.insufficient"); return; }
            _save.Upgrade(kind);
            SaveStore.Write(_save); RebuildProgram(); StartDock(_dockScenario);
            LocalTelemetry.Record(0, "bench.purchase", kind + ":" + cost); _view.Refresh();
        }

        /// <summary>선택한 성장 항목을 구매한다.</summary>
        public void Upgrade(string kind) { BuyUpgrade(kind); }

        /// <summary>사용자 설정을 갱신하고 저장한다.</summary>
        public void SetFeedback(bool screenShake, bool hitStop) { _save.SetFeedback(screenShake, hitStop); SaveStore.Write(_save); }

        /// <summary>설정 ID에 해당하는 화면 흔들림 또는 히트스톱 값을 저장한다.</summary>
        public void SetSetting(string key, bool enabled)
        { SetFeedback(key == "screenShake" ? enabled : _save.ScreenShake, key == "hitStop" ? enabled : _save.HitStop); }

        /// <summary>설정 화면에서 확인된 진행 초기화를 수행하고 새 시작 마법을 연다.</summary>
        public void ResetSave()
        {
            if (_screen != AppScreen.Workshop) return;
            _save = PlayerSave.CreateNew(); _editingGraph = _save.Library[0].Clone(); _previousGraph = _editingGraph.Clone();
            _undo.Clear(); _redo.Clear(); _activeSlot = 0; _tab = "editor"; RebuildProgram(); StartDock(_dockScenario); SaveStore.Write(_save); _view.Refresh();
        }

        /// <summary>디버그 실행에서만 조각을 지급한다.</summary>
        public void DebugGrant() { if (!_debugEnabled) return; _save.Settle(500, false); SaveStore.Write(_save); _view.Refresh(); }

        /// <summary>디버그 실행에서만 미션 플레이어 무적을 토글한다.</summary>
        public void DebugInvulnerable() { if (_debugEnabled && _mission != null) _mission.SetDebugInvulnerable(!_mission.DebugInvulnerable); }

        /// <summary>디버그 실행에서만 지정한 적을 미션에 생성한다.</summary>
        public void DebugSpawn(string kind = "enemy.scout") { if (_debugEnabled && _mission != null) _mission.DebugSpawn(kind); }

        /// <summary>순번과 GUID를 결합한 보관함·노드·엣지의 영구 고유 ID를 반환한다.</summary>
        private string NewId(string prefix) { return prefix + "-" + _nextId++ + "-" + Guid.NewGuid().ToString("N").Substring(0, 8); }

        /// <summary>그래프에서 지정한 출력 포트를 출발하는 첫 엣지를 찾아 반환하며 없으면 null을 반환한다.</summary>
        private static GraphEdge FindOutgoingEdge(SpellGraph graph, string fromNode, string fromPort)
        {
            foreach (GraphEdge edge in graph.Edges)
                if (edge.FromNode == fromNode && edge.FromPort == fromPort) return edge;
            return null;
        }

        /// <summary>그래프에 지정 종류의 노드가 하나라도 있으면 true를 반환한다.</summary>
        private static bool HasNodeOfKind(SpellGraph graph, SpellNodeKind kind)
        {
            foreach (GraphNode node in graph.Nodes)
                if (SpellNodes.TryParse(node.RuneId, out var nodeKind) && nodeKind == kind) return true;
            return false;
        }

        /// <summary>십진 정수 포트 번호 문자열을 정수로 바꾸며, 성공하면 true를 반환한다.</summary>
        private static bool TryParsePort(string text, out int port)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port);
        }

        /// <summary>번역된 상태 메시지를 저장하고 표시를 갱신한다.</summary>
        private void SetStatus(string key) { _statusMessage = GameData.L(key); _view.RefreshGraph(); }

        /// <summary>요청된 튜토리얼 완료 단계를 저장한다.</summary>
        public void AdvanceTutorial(int step) { _save.SetTutorialStep(step); SaveStore.Write(_save); _view.RefreshGraph(); }
    }
}
