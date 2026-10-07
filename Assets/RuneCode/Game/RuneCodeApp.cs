using System;
using System.Collections.Generic;
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

        [Header("화면 구성")]
        [SerializeField] private TMP_FontAsset _font;

        private RuneCodeView _view;
        private PlayerSave _save;
        private SpellGraph _editingGraph;
        private SpellGraph _previousGraph;
        private CompileResult _compileResult;
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
        private bool _dockAutoFire;
        private bool _dockAdaptation;
        private bool _debugEnabled;
        private bool _settled;
        private bool _hasPendingMissionDash;
        private float _dockSpeed = 1;
        private float _compileAt;
        private double _missionAccumulator;
        private double _dockAccumulator;
        private string _dockScenario = "dummy_single";

        public AppScreen Screen => _screen;
        public PlayerSave Save => _save;
        public SpellGraph EditingGraph => _editingGraph;
        public SpellGraph CurrentGraph => _editingGraph;
        public CompileResult CompileResult => _compileResult;
        public RuneSimulation Mission => _mission;
        public RuneSimulation Dock => _dock;
        public bool IsMission => _screen == AppScreen.Mission;
        public bool IsTerminal => _mission != null && _mission.Stage == MissionStage.Terminal;
        public bool IsTerminalEditor => _isTerminalEditor;
        public bool IsPaused => _isPaused;
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
        public bool DockAutoFire => _dockAutoFire;
        public bool DockAdaptation => _dockAdaptation;
        public float DockSpeed => _dockSpeed;
        public int Capacity => GameData.Balance.Economy.BaseCapacity + _save.CapacityLevel * GameData.Balance.Economy.CapacityStep;
        public float MaxEnergy => GameData.Balance.Player.MaxEnergy + _save.EnergyLevel * GameData.Balance.Economy.StatStep;
        public float MaxHp => GameData.Balance.Player.MaxHp + _save.HpLevel * GameData.Balance.Economy.StatStep;
        public int EquippedRam => CalculateEquippedRam(_editingGraph);
        public int SelectedStage => _save.SelectedStage;
        public int HighestClearedStage => _save.HighestClearedStage;
        public float BattleDuration => (float)_incremental.BaseDuration + _save.DurationLevel * GameData.Balance.Economy.DurationStep;
        public float EnergyRegen => GameData.Balance.Player.EnergyRegen + _save.EnergyLevel * GameData.Balance.Economy.EnergyRegenStep;
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
            Recompile();
            StartDock(_dockScenario);
            _view.Initialize(this);
            LocalTelemetry.Record(0, "session", "start");
        }

        void Update()
        {
            if (_graphDirty && Time.unscaledTime >= _compileAt)
            {
                _graphDirty = false;
                Recompile();
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
                var maxSteps = GameData.Balance.Limits.MaxFrameSteps;
                _missionAccumulator = Math.Min(_missionAccumulator + Time.unscaledDeltaTime, FIXED_STEP * maxSteps);
                var steps = 0;
                while (_missionAccumulator >= FIXED_STEP && steps++ < maxSteps)
                {
                    var aim = hasPointer ? pointer - _mission.Player.Position : _mission.Player.AimDirection;
                    _mission.Step(new SimulationInput(movement, aim, hasPointer, false, false, _hasPendingMissionDash));
                    _hasPendingMissionDash = false;
                    _missionAccumulator -= FIXED_STEP;
                }
                if (_mission.Stage != previousStage) _view.Refresh();
                CaptureNodeTelemetry(_mission, "mission", ref _missionTelemetryCount);
                if (_mission.Stage == MissionStage.Dead || _mission.Stage == MissionStage.Cleared) FinishMission();
            }
            else { _missionAccumulator = 0; _hasPendingMissionDash = false; }
            if ((_screen == AppScreen.Workshop && _tab == "editor") || _isTerminalEditor)
            {
                var aim = new SimVector(1, 0);
                if (_view.TryGetDockPointer(out var point)) aim = point - _dock.Player.Position;
                var fire = _dockAutoFire || (!isTyping && _view.IsPointerInDock && mouse != null && mouse.leftButton.isPressed);
                if (!isTyping && keyboard != null && keyboard.rKey.wasPressedThisFrame) ResetDock();
                var maxSteps = GameData.Balance.Limits.MaxFrameSteps;
                _dockAccumulator = Math.Min(_dockAccumulator + Time.unscaledDeltaTime * _dockSpeed, FIXED_STEP * maxSteps);
                var steps = 0;
                while (_dockAccumulator >= FIXED_STEP && steps++ < maxSteps)
                {
                    _dock.Step(new SimulationInput(SimVector.Zero, aim, fire));
                    _dockAccumulator -= FIXED_STEP;
                }
                CaptureNodeTelemetry(_dock, "dock", ref _dockTelemetryCount);
            }
            else _dockAccumulator = 0;
        }

        void OnApplicationQuit() { SaveGraph(); }

        /// <summary>편집기의 선택 마법을 검증하고 도크의 시전 프로그램을 갱신한다.</summary>
        private void Recompile()
        {
            _compileResult = GraphCompiler.Compile(_editingGraph, GameData.Runes, GameData.Balance, _save.UnlockedRunes, Capacity, MaxEnergy);
            if (_dock != null) _dock.SetLoadout(new[] { _compileResult.Ok ? _compileResult.Spell : null });
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
            Recompile();
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

        /// <summary>현재 단일 마법의 편집 사본을 반영하고 버전 2 파일에 저장한다.</summary>
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

        /// <summary>컴파일 및 공유 RAM 한도를 만족하는 현재 마법을 지정 슬롯에 장착한다.</summary>
        public void Equip(int slot)
        {
            if (!CanEdit() || slot != 0) return;
            Recompile();
            if (!_compileResult.Ok) { SetStatus("editor.invalidEquip"); return; }
            var ram = 0;
            for (var i = 0; i < _save.SlotCount; i++)
                ram += i == slot ? _compileResult.Spell.RamUsed : GetGraphRam(_save.Loadout[i] == _editingGraph.Id ? _editingGraph : _save.FindGraph(_save.Loadout[i]));
            if (GameData.Balance.Ram.Mode == "shared" && ram > Capacity) { SetStatus("editor.ramBlocked"); return; }
            SaveGraph(); _save.SetLoadout(slot, _editingGraph.Id); _activeSlot = slot;
            SaveStore.Write(_save); _view.Refresh();
        }

        /// <summary>현재 마법을 지정 슬롯에 장착한다.</summary>
        public void EquipCurrent(int slot) { Equip(slot); }

        /// <summary>단일 마법의 장착 해제 요청을 거부하고 성장 규칙을 안내한다.</summary>
        public void Unequip(int slot) { SetStatus("editor.singleSpell"); }

        /// <summary>현재 마법을 RC1 공유 문자열로 반환한다.</summary>
        public string ExportSpell() { return ShareCodec.Encode(_editingGraph); }

        /// <summary>현재 마법의 공유 문자열을 반환한다.</summary>
        public string ExportGraph() { return ExportSpell(); }

        /// <summary>공유 데이터를 현재 단일 마법에 반영하며 실패 이유를 상태 표시로 전달한다.</summary>
        public void ImportSpell(string code) { var error = ImportGraph(code); if (!string.IsNullOrEmpty(error)) _statusMessage = error; }

        /// <summary>RC1 구조를 검증하여 현재 마법의 ID를 유지한 채 가져오고 실패하면 설명을 반환한다.</summary>
        public string ImportGraph(string code)
        {
            if (!CanEdit()) return GameData.L("editor.combatLocked");
            if (!ShareCodec.TryDecode(code, out var graph, out var error)) return error;
            ApplyGraph(graph);
            Recompile(); SaveGraph();
            return null;
        }

        /// <summary>전달된 그래프를 현재 ID의 편집 상태에 반영하고 검증을 예약한다.</summary>
        public void ApplyGraph(SpellGraph graph)
        {
            if (!CanEdit()) return;
            if (graph.Nodes.Count > GameData.Balance.Limits.MaxGraphNodes || graph.Edges.Count > GameData.Balance.Limits.MaxGraphEdges)
            { SetStatus("editor.graphLimit"); return; }
            _editingGraph = graph.Clone(_editingGraph.Id);
            ChangedGraph(); _view.RefreshGraph();
        }

        /// <summary>Core 중복과 공유 RAM 초과를 막고 지정 좌표에 룬을 배치한다.</summary>
        public void AddRune(string runeId, float x = 300, float y = 150)
        {
            if (!CanEdit() || !_save.UnlockedRunes.Contains(runeId)) return;
            var rune = GameData.Runes.Get(runeId);
            if (rune == null || runeId == "core.cast") return;
            if (_editingGraph.Nodes.Count >= GameData.Balance.Limits.MaxGraphNodes)
            { SetStatus("editor.graphLimit"); return; }
            var extraCopies = _save.Loadout.Take(_save.SlotCount).Count(id => id == _editingGraph.Id);
            if (extraCopies > 0 && GameData.Balance.Ram.Mode == "shared" && EquippedRam + rune.Ram * extraCopies > Capacity)
            { SetStatus("editor.ramBlocked"); return; }
            if (extraCopies > 0 && GameData.Balance.Ram.Mode != "shared" && GetGraphRam(_editingGraph) + rune.Ram > Capacity)
            { SetStatus("editor.ramBlocked"); return; }
            _editingGraph.AddNode(new GraphNode(NewId("node"), runeId, x, y));
            if (runeId == "form.bolt") _save.SetTutorialStep(1);
            ChangedGraph(); _view.RefreshGraph();
        }

        /// <summary>후보 연결을 컴파일러로 검사하여 입력 수·종류·슬롯·순환 위반을 차단한다.</summary>
        public bool Connect(string fromNode, string fromPort, string toNode, string toPort)
        {
            if (!CanEdit()) return false;
            if (_editingGraph.Edges.Count >= GameData.Balance.Limits.MaxGraphEdges)
            { SetStatus("editor.graphLimit"); return false; }
            var candidate = _editingGraph.Clone();
            var edge = new GraphEdge(NewId("edge"), fromNode, fromPort, toNode, toPort);
            if (!GraphCompiler.CanConnect(_editingGraph, edge, GameData.Runes, out _))
            { SetStatus("editor.invalidConnection"); return false; }
            candidate.AddEdge(edge);
            var result = GraphCompiler.Compile(candidate, GameData.Runes, GameData.Balance, _save.UnlockedRunes, Capacity, MaxEnergy);
            if (result.Errors.Any(issue => issue.Code == "E2" || issue.Code == "E3" || issue.Code == "E4" || issue.Code == "E5" || issue.Code == "E6"))
            { SetStatus("editor.invalidConnection"); return false; }
            _editingGraph.AddEdge(edge);
            var source = _editingGraph.Nodes.FirstOrDefault(node => node.Id == fromNode);
            if (source != null && source.RuneId == "elem.fire") _save.SetTutorialStep(2);
            ChangedGraph(); _view.RefreshGraph();
            return true;
        }

        /// <summary>Core 이외의 지정 노드와 관련 엣지를 제거한다.</summary>
        public void RemoveNode(string id) { if (!CanEdit()) return; _editingGraph.RemoveNode(id); ChangedGraph(); _view.RefreshGraph(); }

        /// <summary>지정한 엣지를 삭제하고 검증을 갱신한다.</summary>
        public void RemoveEdge(string id) { if (!CanEdit()) return; _editingGraph.RemoveEdge(id); ChangedGraph(); _view.RefreshGraph(); }

        /// <summary>숫자 파라미터를 선택 노드에 저장하고 검증을 예약한다.</summary>
        public void SetNodeNumber(string id, string key, float value)
        { if (!CanEdit()) return; var node = _editingGraph.Nodes.FirstOrDefault(item => item.Id == id); if (node == null) return; node.SetNumber(key, value); ChangedGraph(); }

        /// <summary>열거 파라미터를 선택 노드에 저장하고 검증을 예약한다.</summary>
        public void SetNodeText(string id, string key, string value)
        { if (!CanEdit()) return; var node = _editingGraph.Nodes.FirstOrDefault(item => item.Id == id); if (node == null) return; node.SetText(key, value); ChangedGraph(); }

        /// <summary>이전 편집 상태를 최대 50단계 기록하고 자동 컴파일을 예약한다.</summary>
        public void ChangedGraph()
        {
            if (_previousGraph != null && ShareCodec.Serialize(_previousGraph) != ShareCodec.Serialize(_editingGraph))
            {
                _undo.Add(_previousGraph.Clone());
                if (_undo.Count > HISTORY_LIMIT) _undo.RemoveAt(0);
                _redo.Clear(); _previousGraph = _editingGraph.Clone();
            }
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
            _previousGraph = _editingGraph.Clone(); Recompile(); SaveGraph(); _view.RefreshGraph();
        }

        /// <summary>되돌린 그래프 스냅샷을 복구한다.</summary>
        public void Redo()
        {
            if (_redo.Count == 0 || !CanEdit()) return;
            _undo.Add(_editingGraph.Clone()); _editingGraph = _redo[_redo.Count - 1]; _redo.RemoveAt(_redo.Count - 1);
            _previousGraph = _editingGraph.Clone(); Recompile(); SaveGraph(); _view.RefreshGraph();
        }

        /// <summary>선택된 노드와 내부 연결을 별도 사본에 기록한다.</summary>
        public void CopyNodes(IEnumerable<string> ids)
        {
            var selected = new HashSet<string>(ids);
            var snapshot = _editingGraph.Clone();
            _copiedNodes.Clear(); _copiedEdges.Clear();
            _copiedNodes.AddRange(snapshot.Nodes.Where(node => selected.Contains(node.Id) && node.RuneId != "core.cast"));
            _copiedEdges.AddRange(snapshot.Edges.Where(edge => _copiedNodes.Any(node => node.Id == edge.FromNode) && _copiedNodes.Any(node => node.Id == edge.ToNode)));
        }

        /// <summary>복사 노드에 새 ID와 좌표 오프셋을 부여하고 장착 RAM 검증 후 붙여넣는다.</summary>
        public void PasteNodes()
        {
            if (_copiedNodes.Count == 0 || !CanEdit()) return;
            if (_editingGraph.Nodes.Count + _copiedNodes.Count > GameData.Balance.Limits.MaxGraphNodes ||
                _editingGraph.Edges.Count + _copiedEdges.Count > GameData.Balance.Limits.MaxGraphEdges)
            { SetStatus("editor.graphLimit"); return; }
            var candidate = _editingGraph.Clone();
            var map = new Dictionary<string, string>();
            foreach (var node in _copiedNodes)
            {
                var clone = node.Clone(NewId("node")); clone.Move(node.X + 30, node.Y + 30); map[node.Id] = clone.Id; candidate.AddNode(clone);
            }
            foreach (var edge in _copiedEdges) candidate.AddEdge(new GraphEdge(NewId("edge"), map[edge.FromNode], edge.FromPort, map[edge.ToNode], edge.ToPort));
            if (IsEditingEquipped() && CalculateEquippedRam(candidate) > Capacity && GameData.Balance.Ram.Mode == "shared")
            { SetStatus("editor.ramBlocked"); return; }
            if (IsEditingEquipped() && GetGraphRam(candidate) > Capacity && GameData.Balance.Ram.Mode != "shared")
            { SetStatus("editor.ramBlocked"); return; }
            _editingGraph = candidate; ChangedGraph(); _view.RefreshGraph();
        }

        /// <summary>실행 노드와 속성·수식 노드를 역할별 행에 정렬한다.</summary>
        public void AutoArrange()
        {
            if (!CanEdit()) return;
            var execution = _editingGraph.Nodes.Where(node => { var category = GameData.Runes.Get(node.RuneId).Category; return category != "element" && category != "modifier"; }).ToList();
            var modifiers = _editingGraph.Nodes.Except(execution).ToList();
            for (var i = 0; i < execution.Count; i++) execution[i].Move(35 + (i % 4) * 170, 55 + (i / 4) * 130);
            for (var i = 0; i < modifiers.Count; i++) modifiers[i].Move(80 + (i % 4) * 170, 250 + (i / 4) * 100);
            ChangedGraph(); _view.RefreshGraph();
        }

        /// <summary>선택 시나리오의 독립 시뮬레이션을 만들고 현재 마법을 연결한다.</summary>
        public void StartDock(string scenario)
        {
            _dockScenario = scenario; _dock = new RuneSimulation(1, false, MaxHp, MaxEnergy, energyRegen: EnergyRegen);
            _dock.ResetBench(scenario); _dock.SetAdaptationEnabled(_dockAdaptation || scenario == "adapt_loop");
            _dock.SetUnlockedElements(GetUnlockedElements()); Recompile(); _dockAccumulator = 0;
            _dockTelemetryCount = 0;
        }

        /// <summary>도크를 현재 또는 전달된 시나리오의 초기 상태로 복구한다.</summary>
        public void ResetDock(string scenario = null) { StartDock(scenario ?? _dockScenario); _view.Refresh(); }

        /// <summary>유효한 편집 마법을 도크에 한 번 시전하고 튜토리얼 시험 단계를 저장한다.</summary>
        public void FireDock()
        {
            Recompile();
            if (!_compileResult.Ok) { SetStatus("editor.invalidCast"); return; }
            if (!_dock.TryCast(_compileResult.Spell)) return;
            _save.SetTutorialStep(3); SaveStore.Write(_save);
            LocalTelemetry.Record(_dock.Tick, "bench.cast", _compileResult.Spell.Signature); _view.RefreshGraph();
        }

        /// <summary>도크의 적응 활성 상태를 변경한다.</summary>
        public void SetDockAdaptation(bool enabled) { _dockAdaptation = enabled; _dock.SetAdaptationEnabled(enabled); }

        /// <summary>도크 자동 시전·적응·시간 배율을 설정한다.</summary>
        public void SetDockOptions(bool autoplay, bool adaptation, float speed)
        { _dockAutoFire = autoplay; _dockSpeed = Math.Max(0.5f, Math.Min(2, speed)); SetDockAdaptation(adaptation); }

        /// <summary>일반 전투 중 편집 불가 규칙에 따라 현재 화면의 편집 허용 상태를 반환한다.</summary>
        private bool CanEdit() { return _screen == AppScreen.Workshop && _tab == "editor"; }

        /// <summary>현재 편집 마법이 하나 이상의 슬롯에 장착되어 있는지 반환한다.</summary>
        private bool IsEditingEquipped() { return _save.Loadout.Take(_save.SlotCount).Contains(_editingGraph.Id); }

        /// <summary>현재 단일 마법을 검증하고 선택 스테이지의 제한시간·영구 능력치로 직접 이동·조준하는 전투를 시작한다.</summary>
        public void Deploy()
        {
            if (IsMission && !_settled) return;
            SaveGraph(); Recompile();
            if (!_compileResult.Ok || EquippedRam > Capacity) { SetStatus("editor.invalidEquip"); return; }
            var spell = _compileResult.Spell;
            _mission = new RuneSimulation(1, true, MaxHp, MaxEnergy, SelectedStage, BattleDuration, EnergyRegen);
            _mission.SetLoadout(new[] { spell });
            _mission.SetUnlockedElements(GetUnlockedElements());
            _screen = AppScreen.Mission; _isPaused = false; _isTerminalEditor = false; _settled = false;
            _missionAccumulator = 0; _missionTelemetryCount = 0; _hasPendingMissionDash = false;
            LocalTelemetry.Record(0, "mission.start", SelectedStage + ":" + spell.Signature);
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

        /// <summary>지정 성장 항목의 현재 단계 또는 미해금 룬의 구매 비용을 반환하며 구매 불가이면 -1을 반환한다.</summary>
        public int GetUpgradeCost(string kind)
        {
            var economy = GameData.Balance.Economy;
            switch (kind)
            {
                case "capacity": return economy.GetGrowthCost(kind, _save.CapacityLevel);
                case "energy": return economy.GetGrowthCost(kind, _save.EnergyLevel);
                case "duration": return economy.GetGrowthCost(kind, _save.DurationLevel);
                default:
                    return GameData.Runes.TryGet(kind, out var rune) && rune.UnlockType == "bench" && !_save.UnlockedRunes.Contains(kind) ? rune.UnlockCost : -1;
            }
        }

        /// <summary>성장 단계와 비용을 확인한 후 선택 업그레이드 또는 룬 해금을 구매한다.</summary>
        public void BuyUpgrade(string kind)
        {
            if (_screen != AppScreen.Workshop) return;
            var cost = GetUpgradeCost(kind);
            if (cost < 0) { SetStatus("bench.maxed"); return; }
            if (!_save.Spend(cost)) { SetStatus("bench.insufficient"); return; }
            if (kind.Contains(".")) _save.Unlock(kind); else _save.Upgrade(kind);
            SaveStore.Write(_save); Recompile(); StartDock(_dockScenario);
            LocalTelemetry.Record(0, "bench.purchase", kind + ":" + cost); _view.Refresh();
        }

        /// <summary>선택한 성장 항목을 구매한다.</summary>
        public void Upgrade(string kind) { BuyUpgrade(kind); }

        /// <summary>벤치에서 선택한 룬을 구매한다.</summary>
        public void UnlockRune(string id) { BuyUpgrade(id); }

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
            _undo.Clear(); _redo.Clear(); _activeSlot = 0; _tab = "editor"; Recompile(); StartDock(_dockScenario); SaveStore.Write(_save); _view.Refresh();
        }

        /// <summary>디버그 실행에서만 조각을 지급한다.</summary>
        public void DebugGrant() { if (!_debugEnabled) return; _save.Settle(500, false); SaveStore.Write(_save); _view.Refresh(); }

        /// <summary>디버그 실행에서만 모든 룬을 해금한다.</summary>
        public void DebugUnlock() { if (!_debugEnabled) return; foreach (var rune in GameData.Runes.All) _save.Unlock(rune.Id); SaveStore.Write(_save); Recompile(); _view.Refresh(); }

        /// <summary>디버그 실행에서만 미션 플레이어 무적을 토글한다.</summary>
        public void DebugInvulnerable() { if (_debugEnabled && _mission != null) _mission.SetDebugInvulnerable(!_mission.DebugInvulnerable); }

        /// <summary>디버그 실행에서만 지정한 적을 미션에 생성한다.</summary>
        public void DebugSpawn(string kind = "enemy.scout") { if (_debugEnabled && _mission != null) _mission.DebugSpawn(kind); }

        /// <summary>순번과 GUID를 결합한 보관함·노드·엣지의 영구 고유 ID를 반환한다.</summary>
        private string NewId(string prefix) { return prefix + "-" + _nextId++ + "-" + Guid.NewGuid().ToString("N").Substring(0, 8); }

        /// <summary>배치된 모든 룬의 RAM을 합산한다.</summary>
        private int GetGraphRam(SpellGraph graph)
        { if (graph == null) return 0; return graph.Nodes.Sum(node => GameData.Runes.TryGet(node.RuneId, out var rune) ? rune.Ram : 0); }

        /// <summary>편집 사본을 반영한 장착 마법 전체의 RAM 사용량을 반환한다.</summary>
        private int CalculateEquippedRam(SpellGraph edited)
        { var ram = 0; foreach (var id in _save.Loadout.Take(_save.SlotCount)) ram += GetGraphRam(edited != null && id == edited.Id ? edited : _save.FindGraph(id)); return ram; }

        /// <summary>노이즈의 선택 후보로 사용할 해금 속성 태그와 기본 raw 태그를 반환한다.</summary>
        private IReadOnlyList<string> GetUnlockedElements()
        { var elements = new List<string> { "raw" }; foreach (var id in _save.UnlockedRunes) if (id.StartsWith("elem.", StringComparison.Ordinal)) elements.Add(id.Substring(5)); return elements; }

        /// <summary>번역된 상태 메시지를 저장하고 표시를 갱신한다.</summary>
        private void SetStatus(string key) { _statusMessage = GameData.L(key); _view.RefreshGraph(); }

        /// <summary>요청된 튜토리얼 완료 단계를 저장한다.</summary>
        public void AdvanceTutorial(int step) { _save.SetTutorialStep(step); SaveStore.Write(_save); _view.RefreshGraph(); }
    }
}
