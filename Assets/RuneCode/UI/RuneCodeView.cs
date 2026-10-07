using System;
using System.Collections.Generic;

using UnityEngine;

using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace RuneCode
{
    public sealed class RuneCodeView : MonoBehaviour
    {
        private static readonly Color PANEL = new Color(0.065f, 0.10f, 0.155f);
        private static readonly Color MUTED = new Color(0.54f, 0.67f, 0.77f);
        private static readonly Color CYAN = new Color(0.24f, 0.84f, 0.94f);

        private RuneCodeApp _app;
        private RectTransform _page;
        private RectTransform _logicalRoot;
        private RectTransform _inspector;
        private RectTransform _palette;
        private RectTransform _modal;
        private RuneGraphCanvas _graph;
        private RuneArenaGraphic _dockArena;
        private RuneArenaGraphic _missionArena;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _metrics;
        private TextMeshProUGUI _headerStats;
        private TextMeshProUGUI _spellTitle;
        private TextMeshProUGUI _dockMetrics;
        private TextMeshProUGUI _tutorial;
        private TextMeshProUGUI _tooltip;
        private TextMeshProUGUI _missionStats;
        private TextMeshProUGUI _hpCaption;
        private TextMeshProUGUI _energyCaption;
        private TextMeshProUGUI _missionSpell;
        private TextMeshProUGUI _missionTimer;
        private TextMeshProUGUI _adaptation;
        private TextMeshProUGUI _fragmentToast;
        private UnityEngine.UI.Image _hpFill;
        private UnityEngine.UI.Image _energyFill;
        private string _missionSpellName;
        private float _missionSpellCost;
        private int _lastFragments;
        private float _fragmentToastUntil;
        private bool _isTutorialActive;
        private int _tutorialStartExecutions;
        private TMP_InputField _search;
        private GraphNode _selected;
        private string _category = "all";
        private string _scenario = "dummy_single";
        private int _lastDockEventCount;
        private RuneSimulation _observedDock;
        private bool _isModalOpen;
        public TMP_FontAsset Font { get; set; }
        public bool IsPointerInDock => _dockArena != null && !_isModalOpen && Mouse.current != null && _dockArena.TryGetPointer(Mouse.current.position.ReadValue(), out _);
        public bool IsPointerInMission => _missionArena != null && !_isModalOpen && Mouse.current != null && _missionArena.TryGetPointer(Mouse.current.position.ReadValue(), out _);

        /// <summary>애플리케이션 상태를 표시할1280×720 uGUI 캔버스와 입력 경로를 초기화한다.</summary>
        public void Initialize(RuneCodeApp app)
        {
            _app = app;
            if (Font == null) Font = TMP_Settings.defaultFontAsset;
            var canvasTarget = new GameObject("RuneCodeCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasTarget.transform.SetParent(transform, false);
            canvasTarget.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.UI.CanvasScaler scaler = canvasTarget.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            if (EventSystem.current == null)
            {
                var events = new GameObject("RuneCodeEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            _logicalRoot = Rect(canvasTarget.transform, "LogicalScreen", 0, 0, 1280, 720);
            _logicalRoot.anchorMin = _logicalRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _logicalRoot.pivot = new Vector2(0.5f, 0.5f);
            _logicalRoot.anchoredPosition = Vector2.zero;
            Refresh();
        }

        /// <summary>현재 화면과 탭에 맞는 페이지를 생성하고 선택 및 실시간 표시 참조를 재연결한다.</summary>
        public void Refresh()
        {
            if (_logicalRoot == null) return;
            if (_page != null) { _page.gameObject.SetActive(false); Destroy(_page.gameObject); }
            _modal = null; _isModalOpen = false; _graph = null; _dockArena = null; _missionArena = null;
            _missionStats = null; _missionSpell = null; _missionTimer = null; _adaptation = null;
            _metrics = null; _headerStats = null; _spellTitle = null; _dockMetrics = null; _tutorial = null; _tooltip = null; _selected = null;
            _inspector = null; _palette = null; _status = null;
            _page = Panel(_logicalRoot, 0, 0, 1280, 720, new Color(0.025f, 0.045f, 0.075f));
            if (_app.Screen == AppScreen.Title) { BuildTitle(); return; }
            if (_app.Screen == AppScreen.Result) { BuildResult(); return; }
            if (_app.Screen == AppScreen.Mission) { BuildMission(); return; }
            BuildHeader();
            if (_app.Tab == "bench") BuildBench();
            else if (_app.Tab == "deploy") BuildDeploy();
            else if (_app.Tab == "settings") BuildSettings();
            else BuildEditor();
            _status = Text(_page, 18, 686, 1240, 22, "", 12, MUTED);
        }

        /// <summary>페이지를 재생성하지 않고 노드, 컴파일 지표와 선택 파라미터를 갱신한다.</summary>
        public void RefreshGraph()
        {
            _graph?.RefreshGraph();
            if (_headerStats != null) _headerStats.text = HeaderStatsText();
            if (_spellTitle != null) _spellTitle.text = _app.EditingGraph.Name;
            RefreshMetrics();
            if (_selected != null) _selected = _app.EditingGraph.FindNode(_selected.Id);
            if (_inspector != null) BuildInspector(_selected);
        }

        /// <summary>시험 도크 위의 포인터를 시뮬레이션 좌표로 반환한다.</summary>
        public bool TryGetDockPointer(out SimVector point)
        {
            point = SimVector.Zero;
            return _dockArena != null && Mouse.current != null && _dockArena.TryGetPointer(Mouse.current.position.ReadValue(), out point);
        }

        /// <summary>미션 경기장 위의 포인터를 시뮬레이션 좌표로 반환한다.</summary>
        public bool TryGetMissionPointer(out SimVector point)
        {
            point = SimVector.Zero;
            return _missionArena != null && Mouse.current != null && _missionArena.TryGetPointer(Mouse.current.position.ReadValue(), out point);
        }

        /// <summary>도크 위 포인터를 애플리케이션 입력에 사용할 두 좌표로 반환한다.</summary>
        public bool TryGetDockPointer(out float x, out float y)
        {
            bool isInside = TryGetDockPointer(out SimVector point);
            x = (float)point.X; y = (float)point.Y;
            return isInside;
        }

        /// <summary>미션 위 포인터를 애플리케이션 입력에 사용할 두 좌표로 반환한다.</summary>
        public bool TryGetMissionPointer(out float x, out float y)
        {
            bool isInside = TryGetMissionPointer(out SimVector point);
            x = (float)point.X; y = (float)point.Y;
            return isInside;
        }

        void Update()
        {
            if (_app == null) return;
            if (_status != null) _status.text = _app.StatusMessage;
            if (_dockMetrics != null && _app.Dock != null)
            {
                if (_observedDock != _app.Dock)
                { _observedDock = _app.Dock; _lastDockEventCount = 0; if (_isTutorialActive) _tutorialStartExecutions = 0; }
                _dockMetrics.text = L("ui.damage") + " " + _app.Dock.TotalDamage.ToString("0") + "   " + L("ui.dps") + " " + _app.Dock.RollingDps.ToString("0.0") +
                    "   " + L("ui.energy") + " " + _app.Dock.EnergySpent.ToString("0") + "   " + L("ui.peak") + " " + _app.Dock.PeakSpellEntities;
                int newEvents = _app.Dock.NodeExecutionCount - _lastDockEventCount;
                for (int index = Math.Max(0, _app.Dock.NodeEvents.Count - newEvents); index < _app.Dock.NodeEvents.Count; index++)
                {
                    NodeExecutionEvent entry = _app.Dock.NodeEvents[index];
                    if (_app.Dock.Tick - entry.Tick <= 12) _graph?.Highlight(entry.NodeId);
                }
                _lastDockEventCount = _app.Dock.NodeExecutionCount;
            }
            if (_tutorial != null) UpdateTutorial();
            if (_missionStats != null && _app.Mission != null) UpdateMissionHud();
            if (_graph == null || _isModalOpen || IsTyping()) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            bool isControl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) _graph.DeleteSelection();
            if (isControl && keyboard.cKey.wasPressedThisFrame) _app.CopyNodes(_graph.SelectedNodes);
            if (isControl && keyboard.vKey.wasPressedThisFrame) _app.PasteNodes();
            if (isControl && keyboard.dKey.wasPressedThisFrame) { _app.CopyNodes(_graph.SelectedNodes); _app.PasteNodes(); }
            if (isControl && keyboard.sKey.wasPressedThisFrame) _app.SaveGraph();
            if (keyboard.digit1Key.wasPressedThisFrame) _app.FireDock();
        }

        /// <summary>첫 화면의 제품 소개와 작업실 진입 버튼을 배치한다.</summary>
        private void BuildTitle()
        {
            Panel(_page, 72, 72, 1136, 576, PANEL);
            Panel(_page, 110, 116, 6, 430, CYAN);
            Text(_page, 150, 132, 960, 30, L("ui.introCode"), 16, CYAN);
            Text(_page, 146, 190, 980, 92, L("ui.title"), 76, Color.white, FontStyles.Bold);
            Text(_page, 152, 310, 850, 60, L("ui.subtitle"), 24, MUTED);
            Text(_page, 152, 400, 920, 34, L("ui.help"), 25, CYAN);
            Button(_page, 154, 494, 274, 60, L("ui.start"), _app.StartWorkshop, CYAN, 21);
            Text(_page, 640, 518, 440, 60, L("ui.incrementalLoop"), 14, MUTED);
        }

        /// <summary>작업실 내비게이션과 저장된 성장 상태를 표시한다.</summary>
        private void BuildHeader()
        {
            Panel(_page, 0, 0, 1280, 70, PANEL);
            Text(_page, 18, 18, 200, 36, L("ui.title"), 27, Color.white, FontStyles.Bold);
            string[] tabs = { "editor", "bench", "deploy", "settings" };
            for (int i = 0; i < tabs.Length; i++)
            {
                string tab = tabs[i];
                Button(_page, 242 + i * 128, 18, 116, 34, L("ui." + tab), () => _app.SetTab(tab), _app.Tab == tab ? CYAN : MUTED);
            }
            _headerStats = Text(_page, 816, 23, 440, 30, HeaderStatsText(), 16, CYAN);
        }

        /// <summary>현재 보유 RAM과 단일 마법의 사용량·용량을 작업실 머리글 문자열로 반환한다.</summary>
        private string HeaderStatsText()
        {
            return L("ui.fragments") + "  " + _app.Save.Currency + "   /   " + L("ui.capacity") + " " + _app.EquippedRam + "/" + _app.Capacity;
        }

        /// <summary>룬 팔레트, 편집 화면, 인스펙터 및 도킹된 실험 공간을 생성한다.</summary>
        private void BuildEditor()
        {
            Text(_page, 18, 91, 190, 30, L("ui.singleSpell"), 15, CYAN);
            _spellTitle = Text(_page, 222, 89, 390, 33, _app.EditingGraph.Name, 22, Color.white, FontStyles.Bold);
            Button(_page, 628, 86, 116, 34, L("ui.rename"), OpenRename, MUTED, 13);
            Button(_page, 756, 86, 92, 34, L("ui.save"), _app.SaveGraph, CYAN, 13);
            Button(_page, 862, 86, 132, 34, L("ui.share"), OpenShare, MUTED, 13);
            _metrics = Text(_page, 224, 129, 766, 23, "", 12, CYAN);
            _palette = Panel(_page, 16, 136, 190, 526, PANEL);
            Text(_palette, 12, 12, 166, 26, L("ui.palette"), 17, Color.white, FontStyles.Bold);
            _search = Input(_palette, 10, 47, 170, 32, "", L("ui.search"));
            _search.onValueChanged.AddListener(_ => RefreshPalette());
            string[] categories = { "all", "form", "element", "modifier", "flow", "action" };
            for (int i = 0; i < categories.Length; i++)
            {
                string category = categories[i];
                Button(_palette, 10 + i % 3 * 58, 87 + i / 3 * 29, 54, 25,
                    category == "all" ? L("ui.all") : L("category." + category), () => { _category = category; RefreshPalette(); }, MUTED, 10);
            }
            RectTransform graphRect = Rect(_page, "GraphViewport", 222, 162, 772, 306);
            graphRect.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            RectTransform graphSurface = Rect(graphRect, "GraphCanvas", 0, 0, 772, 306);
            _graph = graphSurface.gameObject.AddComponent<RuneGraphCanvas>();
            _graph.Initialize(_app, Font, node => { _selected = node; BuildInspector(node); }, UpdateTooltip, OpenQuickPalette);
            _inspector = Panel(_page, 1010, 136, 254, 526, PANEL);
            BuildInspector(null);
            Button(_page, 222, 476, 72, 27, L("ui.undo"), _app.Undo, MUTED, 11);
            Button(_page, 300, 476, 72, 27, L("ui.redo"), _app.Redo, MUTED, 11);
            Button(_page, 378, 476, 84, 27, L("ui.arrange"), _app.AutoArrange, MUTED, 11);
            Button(_page, 468, 476, 58, 27, L("ui.copy"), () => _app.CopyNodes(_graph.SelectedNodes), MUTED, 11);
            Button(_page, 532, 476, 78, 27, L("ui.paste"), _app.PasteNodes, MUTED, 11);
            _tooltip = Text(_page, 619, 476, 372, 28, "", 11, MUTED);
            BuildDock();
            _tutorial = Text(_page, 222, 662, 1026, 22, "", 12, CYAN);
            Button(_page, 16, 666, 190, 19, L("ui.tutorial"), () =>
            {
                _isTutorialActive = true;
                _tutorialStartExecutions = _app.Dock.NodeExecutionCount;
                OpenTutorialHelp();
            }, CYAN, 11);
            RefreshPalette();
            RefreshMetrics();
        }

        /// <summary>선택 계열과 검색 문자열에 해당하는 룬 목록을 다시 표시한다.</summary>
        private void RefreshPalette()
        {
            Transform existing = _palette.Find("RuneList");
            if (existing != null) { existing.gameObject.SetActive(false); Destroy(existing.gameObject); }
            RectTransform list = ScrollList(_palette, "RuneList", 8, 153, 174, 362);
            string query = _search.text.Trim();
            foreach (RuneDefinition rune in GameData.Runes.All)
            {
                if (rune.Category == "core" || (_category != "all" && rune.Category != _category)) continue;
                if (!string.IsNullOrEmpty(query) && rune.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 && rune.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool unlocked = Contains(_app.Save.UnlockedRunes, rune.Id);
                RuneDefinition selected = rune;
                string label = rune.Name + "   " + rune.Ram + " RAM";
                if (!unlocked) label += "\n" + L("ui.locked") + " · " + (rune.UnlockType == "reward" ? L("ui.reward") : rune.UnlockCost + " " + L("ui.fragments"));
                UnityEngine.UI.Button button = Button(list, 0, 0, 164, unlocked ? 40 : 53, label,
                    () => { if (unlocked) _graph.PlaceRune(selected.Id, _graph.SuggestPlacement(selected.Id)); },
                    unlocked ? RuneMesh.CategoryColor(rune.Category) : new Color(0.35f, 0.42f, 0.50f), 12);
                Layout(button.gameObject, unlocked ? 40 : 53);
                button.gameObject.AddComponent<RunePaletteDrag>().Initialize(_graph, rune.Id, unlocked);
            }
        }

        /// <summary>빈 편집 영역의 빠른 검색 입력과 검색된 룬의 현재 위치 배치를 제공한다.</summary>
        private void OpenQuickPalette(Vector2 position)
        {
            RectTransform card = OpenModal(L("ui.search"), 550, 470);
            TMP_InputField search = Input(card, 24, 80, 502, 38, "", L("ui.search"));
            RectTransform list = ScrollList(card, "QuickPalette", 24, 138, 502, 300);
            Action<string> populate = query =>
            {
                ClearChildren(list);
                foreach (RuneDefinition rune in GameData.Runes.All)
                {
                    if (rune.Category == "core" || !Contains(_app.Save.UnlockedRunes, rune.Id)) continue;
                    if (!string.IsNullOrEmpty(query) && rune.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 && rune.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string id = rune.Id;
                    UnityEngine.UI.Button button = Button(list, 0, 0, 480, 36, rune.Name + " · " + rune.Ram + " RAM", () => { CloseModal(); _graph.PlaceRune(id, position); }, RuneMesh.CategoryColor(rune.Category));
                    Layout(button.gameObject, 36);
                }
            };
            search.onValueChanged.AddListener(value => populate(value));
            populate(""); search.ActivateInputField();
        }

        /// <summary>선택한 노드의 조정 가능한 파라미터와 그래프 검증 메시지를 표시한다.</summary>
        private void BuildInspector(GraphNode node)
        {
            if (_inspector == null) return;
            ClearChildren(_inspector);
            Text(_inspector, 14, 12, 222, 27, L("ui.inspector"), 17, Color.white, FontStyles.Bold);
            float top = 50;
            if (node == null) { Text(_inspector, 14, top, 224, 62, L("ui.selectNode"), 13, MUTED); top += 72; }
            else
            {
                RuneDefinition rune = GameData.Runes.Get(node.RuneId);
                Text(_inspector, 14, top, 222, 28, rune.Name + " / " + rune.Ram + " RAM", 17, RuneMesh.CategoryColor(rune.Category)); top += 37;
                foreach (ParameterDefinition param in rune.Params)
                {
                    ParameterDefinition definition = param;
                    string nodeId = node.Id;
                    Text(_inspector, 14, top, 224, 20, L("param." + param.Id), 12, MUTED); top += 24;
                    if (param.Kind == "number")
                    {
                        bool isModifierScale = rune.Category == "modifier";
                        TMP_InputField field = Input(_inspector, isModifierScale ? 56 : 14, top, isModifierScale ? 132 : 222, 30,
                            node.GetNumber(param.Id, param.DefaultNumber).ToString("0.###"), "");
                        field.contentType = TMP_InputField.ContentType.DecimalNumber;
                        field.onEndEdit.AddListener(value => { if (float.TryParse(value, out float amount)) _app.SetNodeNumber(nodeId, definition.Id, Mathf.Clamp(amount, definition.Min, definition.Max)); });
                        if (isModifierScale)
                        {
                            Button(_inspector, 14, top, 34, 30, "−", () => ChangeModifierNumber(nodeId, definition, -1), MUTED, 18);
                            Button(_inspector, 196, top, 40, 30, "+", () => ChangeModifierNumber(nodeId, definition, 1), CYAN, 18);
                        }
                    }
                    else
                    {
                        string current = node.GetText(param.Id, param.DefaultText);
                        Button(_inspector, 14, top, 222, 30, L((param.Id == "status" ? "status." : "condition.") + current), () =>
                        {
                            int index = 0;
                            for (int i = 0; i < definition.Options.Count; i++) if (definition.Options[i] == current) index = i;
                            _app.SetNodeText(nodeId, definition.Id, definition.Options[(index + 1) % definition.Options.Count]);
                        }, MUTED, 12);
                    }
                    top += 40;
                }
                if (rune.Category == "modifier" && rune.Params.Count > 0)
                {
                    ParameterDefinition parameter = rune.Params[0];
                    Text(_inspector, 14, top, 222, 26, L("ui.scaleBounds") + " " + parameter.Min.ToString("0.#") + "–" + parameter.Max.ToString("0.#") + "×", 12, CYAN);
                    top += 30;
                    Text(_inspector, 14, top, 222, 72, L("ui." + rune.Id + "Hint"), 12, MUTED); top += 78;
                }
                if (rune.Params.Count == 0)
                {
                    Text(_inspector, 14, top, 222, 34, L("ui.energy") + " " + rune.Energy.ToString("0.#") + "  /  " + L("ui.damage") + " " + rune.Stats.Damage.ToString("0.#"), 12, MUTED); top += 42;
                }
                if (rune.Category == "form")
                {
                    SpellAction action = _app.CompileResult.Spell == null ? null : FindAction(_app.CompileResult.Spell.Root, node.Id);
                    if (action != null)
                    {
                        SpellStats stats = action.Stats;
                        float reach = action.Form == "bolt" ? stats.Speed * stats.Lifetime : action.Form == "orbit" ? stats.OrbitRadius : stats.Offset;
                        Text(_inspector, 14, top, 222, 70,
                            L("ui.spellRange") + " " + reach.ToString("0.#") + "px\n" +
                            L("ui.spellRadius") + " " + stats.Radius.ToString("0.#") + "px\n" +
                            L("ui.spellSpeed") + " " + (action.Form == "orbit" ? stats.AngularSpeed.ToString("0.#") + "°/s" : stats.Speed.ToString("0.#") + "px/s"), 12, CYAN);
                        top += 76;
                    }
                }
                if (rune.Category != "core") { Button(_inspector, 14, top, 222, 29, L("ui.delete"), () => _app.RemoveNode(node.Id), new Color(0.97f, 0.43f, 0.45f), 12); top += 42; }
            }
            Text(_inspector, 14, top, 222, 25, L("ui.validation"), 15, Color.white, FontStyles.Bold); top += 31;
            RectTransform errors = ScrollList(_inspector, "Validation", 12, top, 228, Mathf.Max(80, 510 - top));
            CompileResult result = _app.CompileResult;
            if (result.Ok) AddIssueLabel(errors, L("ui.valid"), CYAN, null);
            foreach (CompileIssue issue in result.Errors) AddIssueLabel(errors, issue.Code + " · " + issue.Message, new Color(1, 0.41f, 0.44f), issue.NodeId);
            foreach (CompileIssue issue in result.Warnings) AddIssueLabel(errors, issue.Code + " · " + issue.Message, new Color(1, 0.76f, 0.38f), issue.NodeId);
            AddIssueLabel(errors, L("ui.controls"), MUTED, null);
        }

        /// <summary>선택 수식의 숫자 배율을 데이터의 한 단계만큼 조절하고 범위 안으로 제한하여 표시를 갱신한다.</summary>
        private void ChangeModifierNumber(string nodeId, ParameterDefinition parameter, int direction)
        {
            GraphNode node = _app.EditingGraph.FindNode(nodeId);
            float value = node.GetNumber(parameter.Id, parameter.DefaultNumber) + parameter.Step * direction;
            _app.SetNodeNumber(nodeId, parameter.Id, Mathf.Clamp(value, parameter.Min, parameter.Max));
            RefreshGraph();
        }

        /// <summary>검증 메시지를 선택 노드 포커스 기능과 함께 목록에 추가한다.</summary>
        private void AddIssueLabel(RectTransform list, string label, Color tint, string nodeId)
        {
            UnityEngine.UI.Button button = Button(list, 0, 0, 216, 56, label, () => { if (nodeId != null) _graph.FocusNode(nodeId); }, tint, 11);
            Layout(button.gameObject, 56);
        }

        /// <summary>시험 도크의 시나리오, 자동 발사, 적응, 속도와 리셋 조작을 생성한다.</summary>
        private void BuildDock()
        {
            Panel(_page, 222, 512, 772, 143, PANEL);
            RectTransform arena = Rect(_page, "DockArena", 222, 541, 292, 110);
            _dockArena = arena.gameObject.AddComponent<RuneArenaGraphic>();
            _dockArena.Initialize(() => _app.Dock, Font, () => _app.Save.ScreenShake, () => _app.Save.HitStop);
            Button(_page, 226, 515, 126, 22, L("scenario." + _scenario), CycleScenario, MUTED, 10);
            Button(_page, 360, 515, 50, 22, L("ui.test"), _app.FireDock, CYAN, 10);
            Button(_page, 418, 515, 86, 22, L("ui.reset"), () => { _app.ResetDock(); _lastDockEventCount = 0; }, MUTED, 10);
            Button(_page, 528, 521, 128, 27, L("ui.autofire"), () => _app.SetDockOptions(!_app.DockAutoFire, _app.DockAdaptation, _app.DockSpeed), CYAN, 11);
            Button(_page, 666, 521, 124, 27, L("ui.adaptation"), () => _app.SetDockAdaptation(!_app.Dock.Adaptation.Enabled), CYAN, 11);
            Button(_page, 800, 521, 76, 27, "0.5 / 1 / 2×", () => _app.SetDockOptions(_app.DockAutoFire, _app.DockAdaptation, _app.DockSpeed == 1 ? 2 : _app.DockSpeed == 2 ? 0.5f : 1), MUTED, 10);
            _dockMetrics = Text(_page, 528, 562, 455, 46, "", 12, Color.white);
            _adaptation = Text(_page, 528, 605, 452, 45, "", 10, MUTED);
            if (_app.Dock == null) _app.StartDock(_scenario);
        }

        /// <summary>시험 시나리오를 다음 항목으로 전환하고 독립 시뮬레이션을 다시 시작한다.</summary>
        private void CycleScenario()
        {
            string[] scenarios = { "dummy_single", "dummy_line", "dummy_swarm", "aegis", "adapt_loop" };
            int index = Array.IndexOf(scenarios, _scenario);
            _scenario = scenarios[(index + 1) % scenarios.Length];
            _app.StartDock(_scenario); _lastDockEventCount = 0;
            Refresh();
        }

        /// <summary>공유 RAM, 마법 비용, 쿨다운과 예상 동시 개체 수를 표시한다.</summary>
        private void RefreshMetrics()
        {
            if (_metrics == null) return;
            CompiledSpell spell = _app.CompileResult.Spell;
            int ram = 0;
            foreach (GraphNode node in _app.EditingGraph.Nodes) ram += GameData.Runes.Get(node.RuneId).Ram;
            _metrics.text = L("ui.ram") + " " + _app.EquippedRam + "/" + _app.Capacity + "  ·  " + ram + " RAM" +
                (spell == null ? "  ·  " + L("ui.warning") : "  ·  " + L("ui.energy") + " " + spell.EnergyCost.ToString("0.#") + "  ·  " + L("ui.cooldown") + " " + spell.Cooldown.ToString("0.00") + "s  ·  " + L("ui.peak") + " " + spell.WorstCaseEntities);
        }

        /// <summary>마우스가 가리키는 룬의 기본 수치와 연결 시 계산되는 효과를 표시한다.</summary>
        private void UpdateTooltip(GraphNode node)
        {
            if (_tooltip == null) return;
            if (node == null) { _tooltip.text = ""; return; }
            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            SpellAction action = _app.CompileResult.Spell == null ? null : FindAction(_app.CompileResult.Spell.Root, node.Id);
            _tooltip.text = rune.Name + "  ·  " + L("ui.damage") + " " + (action?.Stats?.Damage ?? rune.Stats.Damage).ToString("0.#") +
                "  ·  " + L("ui.energy") + " " + (action?.OwnEnergy ?? rune.Energy).ToString("0.#");
        }

        /// <summary>실행 트리를 탐색하여 선택 노드의 컴파일된 명령을 반환한다.</summary>
        private static SpellAction FindAction(IReadOnlyList<SpellAction> actions, string id)
        {
            foreach (SpellAction action in actions)
            {
                if (action.NodeId == id) return action;
                SpellAction found = FindAction(action.OnHit, id) ?? FindAction(action.OnExpire, id) ?? FindAction(action.Then, id) ??
                    FindAction(action.Else, id) ?? FindAction(action.Body, id) ?? FindAction(action.Next, id);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>볼트 배치, 화염 연결, 실제 시전 순서의 초보자 안내를 진행 상태에 맞춰 표시한다.</summary>
        private void UpdateTutorial()
        {
            bool hasBolt = false;
            bool hasFire = false;
            foreach (GraphNode node in _app.EditingGraph.Nodes)
            {
                if (node.RuneId == "form.bolt")
                    foreach (GraphEdge edge in _app.EditingGraph.Edges)
                        if (edge.ToNode == node.Id && edge.ToPort == "exec") hasBolt = true;
                if (node.RuneId != "elem.fire") continue;
                foreach (GraphEdge edge in _app.EditingGraph.Edges) if (edge.FromNode == node.Id) hasFire = true;
            }
            bool hasTested = _app.Dock != null && _app.Dock.NodeExecutionCount > (_isTutorialActive ? _tutorialStartExecutions : 0);
            _tutorial.text = L(!hasBolt ? "ui.tutorial1" : !hasFire ? "ui.tutorial2" : !hasTested ? "ui.tutorial3" : "ui.complete");
            if (_isTutorialActive && hasBolt && hasFire && hasTested) { _isTutorialActive = false; _app.AdvanceTutorial(3); }
            if (_adaptation != null && _app.Dock != null)
            {
                _adaptation.text = L("ui.autofire") + " " + L(_app.DockAutoFire ? "ui.on" : "ui.off") + "  ·  " + _app.DockSpeed.ToString("0.#") + "×  ·  " + L("ui.adaptation") + " " + L(_app.Dock.Adaptation.Enabled ? "ui.on" : "ui.off") + "\n" + AdaptationText(_app.Dock, false);
            }
        }

        /// <summary>현재 마법을 보존하면서 노드 배치, 연결과 시험 안내를 표시한다.</summary>
        private void OpenTutorialHelp()
        {
            RectTransform card = OpenModal(L("ui.tutorial"), 660, 380);
            Text(card, 30, 92, 600, 68, L("ui.tutorial1"), 18, Color.white);
            Text(card, 30, 172, 600, 68, L("ui.tutorial2"), 18, Color.white);
            Text(card, 30, 252, 600, 68, L("ui.tutorial3"), 18, CYAN);
        }

        /// <summary>계속 강화하는 현재 마법의 이름 입력을 표시하고 이름을 저장한다.</summary>
        private void OpenRename()
        {
            RectTransform card = OpenModal(L("ui.rename"), 560, 250);
            TMP_InputField name = Input(card, 28, 88, 504, 42, _app.EditingGraph.Name, "");
            Button(card, 352, 164, 180, 40, L("ui.save"), () => { _app.RenameSpell(name.text); CloseModal(); }, CYAN);
        }

        /// <summary>RC1 공유 코드의 클립보드 복사와 검증된 가져오기 입력을 표시한다.</summary>
        private void OpenShare()
        {
            RectTransform card = OpenModal(L("ui.share"), 770, 400);
            Text(card, 26, 74, 718, 40, L("ui.importCurrent"), 12, MUTED);
            TMP_InputField field = Input(card, 26, 122, 718, 168, _app.ExportSpell(), "RC1.");
            field.lineType = TMP_InputField.LineType.MultiLineNewline;
            Button(card, 26, 322, 180, 42, L("ui.export"), () => GUIUtility.systemCopyBuffer = field.text, CYAN);
            Button(card, 220, 322, 180, 42, L("ui.paste"), () => field.text = GUIUtility.systemCopyBuffer);
            Button(card, 564, 322, 180, 42, L("ui.import"), () => { _app.ImportSpell(field.text); CloseModal(); }, CYAN);
        }

        /// <summary>RAM을 사용하여 노드 용량, 최대 에너지, 전투 시간을 각각 강화하고 룬을 해금한다.</summary>
        private void BuildBench()
        {
            Text(_page, 30, 98, 850, 40, L("ui.bench"), 29, Color.white, FontStyles.Bold);
            Text(_page, 30, 144, 800, 34, L("ui.balance") + "  " + _app.Save.Currency + " " + L("ui.fragments"), 20, CYAN);
            string[] types = { "capacity", "energy", "duration" };
            string[] values = { _app.Capacity.ToString(), _app.MaxEnergy.ToString("0"), _app.BattleDuration.ToString("0") + "s" };
            for (int i = 0; i < types.Length; i++)
            {
                string type = types[i];
                RectTransform card = Panel(_page, 30 + i * 406, 196, 386, 184, PANEL);
                Text(card, 18, 14, 350, 31, L("ui." + type), 21, Color.white);
                Text(card, 18, 52, 350, 34, values[i], 28, CYAN, FontStyles.Bold);
                string description = L("ui." + type + "Description");
                if (type == "energy") description += "  " + L("ui.regen") + " " + _app.EnergyRegen.ToString("0.#") + "/s";
                Text(card, 18, 93, 350, 38, description, 12, MUTED);
                int cost = _app.GetUpgradeCost(type);
                Button(card, 16, 142, 354, 28, cost < 0 ? L("ui.maxed") : L("ui.upgrade") + "  ·  " + cost + " " + L("ui.fragments"), () => _app.BuyUpgrade(type), cost >= 0 && _app.Save.Currency >= cost ? CYAN : MUTED, 12);
            }
            Text(_page, 30, 393, 1200, 24, L("ui.runeUnlocks"), 15, Color.white, FontStyles.Bold);
            RectTransform runes = ScrollList(_page, "UnlockList", 28, 426, 1224, 228);
            foreach (RuneDefinition rune in GameData.Runes.All)
            {
                if (rune.UnlockType == "start") continue;
                RuneDefinition current = rune;
                bool unlocked = Contains(_app.Save.UnlockedRunes, rune.Id);
                string cost = rune.UnlockType == "reward" ? L("ui.reward") : rune.UnlockCost + " " + L("ui.fragments");
                UnityEngine.UI.Button button = Button(runes, 0, 0, 1198, 44, rune.Name + "  /  " + L("category." + rune.Category) + "  /  " + rune.Ram + " RAM  ·  " + (unlocked ? L("ui.complete") : cost),
                    () => { if (!unlocked) _app.BuyUpgrade(current.Id); }, unlocked ? MUTED : RuneMesh.CategoryColor(rune.Category), 15);
                Layout(button.gameObject, 44);
            }
        }

        /// <summary>열린 단계와 제한시간을 확인하고 현재 마법으로 시간제 전투를 시작한다.</summary>
        private void BuildDeploy()
        {
            RectTransform card = Panel(_page, 64, 122, 1152, 498, PANEL);
            Text(card, 38, 32, 1070, 46, L("ui.timedBattle"), 32, Color.white, FontStyles.Bold);
            Text(card, 38, 98, 1010, 72, L("ui.battleDescription"), 20, MUTED);
            Text(card, 38, 202, 480, 30, L("ui.highestStage") + "  " + _app.HighestClearedStage, 16, MUTED);
            Text(card, 38, 246, 238, 36, L("ui.stage") + " " + _app.SelectedStage, 28, CYAN, FontStyles.Bold);
            Button(card, 292, 240, 52, 44, "−", () => _app.SelectStage(_app.SelectedStage - 1), MUTED, 22);
            Button(card, 358, 240, 52, 44, "+", () => _app.SelectStage(_app.SelectedStage + 1), CYAN, 22);
            Text(card, 510, 202, 560, 40, L("ui.duration") + "  " + _app.BattleDuration.ToString("0") + "s", 27, Color.white);
            Text(card, 510, 253, 560, 35, L(_app.RunIsAutomatic ? "ui.autoBattle" : "ui.manualBattle"), 18, CYAN);
            Text(card, 38, 310, 1032, 52, CurrentSpellText(), 18, Color.white);
            Button(card, 38, 386, 320, 60, L("ui.launch"), _app.Deploy, CYAN, 22);
            Text(card, 406, 390, 680, 56, L(_app.RunIsAutomatic ? "ui.autoBattleControls" : "ui.manualBattleControls"), 14, MUTED);
        }

        /// <summary>피드백 설정과 저장 초기화 및 요청된 디버그 도구를 표시한다.</summary>
        private void BuildSettings()
        {
            Text(_page, 42, 112, 800, 42, L("ui.settings"), 30, Color.white, FontStyles.Bold);
            Button(_page, 42, 194, 446, 48, L("ui.shake") + "  " + L(_app.Save.ScreenShake ? "ui.on" : "ui.off"), () => { _app.SetSetting("screenShake", !_app.Save.ScreenShake); Refresh(); }, CYAN, 19);
            Button(_page, 42, 264, 446, 48, L("ui.hitstop") + "  " + L(_app.Save.HitStop ? "ui.on" : "ui.off"), () => { _app.SetSetting("hitStop", !_app.Save.HitStop); Refresh(); }, CYAN, 19);
            Button(_page, 42, 420, 446, 48, L("ui.resetSave"), () =>
            {
                RectTransform card = OpenModal(L("ui.resetSave"), 540, 240);
                Button(card, 28, 105, 232, 60, L("ui.resetSave"), () => { CloseModal(); _app.ResetSave(); }, new Color(1, 0.43f, 0.43f));
                Button(card, 280, 105, 232, 60, L("ui.close"), CloseModal);
            }, new Color(1, 0.43f, 0.43f), 18);
            if (_app.IsDebugEnabled) BuildDebug(_page, 560, 190);
        }

        /// <summary>시간제 전투의 경기장, 남은 시간, 처치 보상과 한 마법의 시전 상태를 표시한다.</summary>
        private void BuildMission()
        {
            RectTransform arena = Rect(_page, "MissionArena", 12, 74, 1256, 572);
            _missionArena = arena.gameObject.AddComponent<RuneArenaGraphic>();
            _missionArena.Initialize(() => _app.Mission, Font, () => _app.Save.ScreenShake, () => _app.Save.HitStop);
            Panel(_page, 0, 0, 1280, 74, PANEL);
            _hpCaption = Text(_page, 18, 11, 238, 24, L("ui.hp"), 12, MUTED);
            Panel(_page, 18, 40, 238, 9, new Color(0.20f, 0.15f, 0.20f));
            _hpFill = Panel(_page, 18, 40, 238, 9, new Color(1, 0.38f, 0.4f)).GetComponent<UnityEngine.UI.Image>();
            _energyCaption = Text(_page, 282, 11, 238, 24, L("ui.energy"), 12, MUTED);
            Panel(_page, 282, 40, 238, 9, new Color(0.1f, 0.2f, 0.28f));
            _energyFill = Panel(_page, 282, 40, 238, 9, CYAN).GetComponent<UnityEngine.UI.Image>();
            Text(_page, 542, 10, 280, 23, L("ui.stage") + " " + _app.Mission.StageNumber, 16, Color.white, FontStyles.Bold);
            _missionStats = Text(_page, 542, 37, 280, 26, "", 15, CYAN);
            _missionTimer = Text(_page, 836, 14, 274, 50, "", 29, new Color(0.98f, 0.82f, 0.45f), FontStyles.Bold);
            Button(_page, 1142, 18, 114, 34, L("ui.pause"), _app.TogglePause);
            Panel(_page, 0, 650, 1280, 70, PANEL);
            _missionSpell = Text(_page, 270, 661, 968, 30, "", 17, CYAN);
            Text(_page, 20, 698, 1220, 19, L(_app.RunIsAutomatic ? "ui.autoBattleControls" : "ui.manualBattleControls"), 11, MUTED);
            Text(_page, 1036, 96, 214, 32, L("ui.incoming"), 14, new Color(0.97f, 0.57f, 0.45f));
            _fragmentToast = Text(_page, 46, 98, 300, 35, "", 18, new Color(0.4f, 0.97f, 0.77f));
            _lastFragments = _app.Mission.EarnedFragments;
            _missionSpellName = _app.EditingGraph.Name;
            _missionSpellCost = _app.CompileResult.Spell?.EnergyCost ?? 0;
            UpdateMissionHud();
            if (_app.IsDebugEnabled) Button(_page, 20, 654, 116, 36, L("ui.debug"), () => { RectTransform card = OpenModal(L("ui.debug"), 510, 420); BuildDebug(card, 24, 82); }, MUTED, 11);
            if (_app.IsPaused) OpenPause();
        }

        /// <summary>전투의 남은 시간, 처치 수, RAM과 현재 마법의 체력·에너지·쿨다운을 갱신한다.</summary>
        private void UpdateMissionHud()
        {
            RuneSimulation sim = _app.Mission;
            _hpFill.rectTransform.sizeDelta = new Vector2(238 * (float)(sim.Player.Hp / sim.Player.MaxHp), 9);
            _energyFill.rectTransform.sizeDelta = new Vector2(238 * (float)(sim.Player.Energy / sim.Player.MaxEnergy), 9);
            _hpCaption.text = L("ui.hp") + "  " + sim.Player.Hp.ToString("0") + "/" + sim.Player.MaxHp.ToString("0");
            _energyCaption.text = L("ui.energy") + "  " + sim.Player.Energy.ToString("0") + "/" + sim.Player.MaxEnergy.ToString("0");
            _missionStats.text = L("ui.kills") + " " + sim.KillCount + "  ·  " + L("ui.fragments") + " " + sim.EarnedFragments;
            _missionTimer.text = L("ui.remaining") + " " + sim.RemainingTime.ToString("0.0") + "s";
            _missionTimer.color = sim.RemainingTime <= 5 ? new Color(1, 0.43f, 0.36f) : new Color(0.98f, 0.82f, 0.45f);
            _missionSpell.text = L("ui.singleSpell") + "  " + _missionSpellName + "  ·  " + L("ui.cooldown") + " " + sim.Player.Cooldowns[0].ToString("0.0") +
                "s  ·  " + L("ui.cost") + " " + _missionSpellCost.ToString("0.#") + " EN";
            if (sim.EarnedFragments > _lastFragments)
            {
                _fragmentToast.text = "+" + (sim.EarnedFragments - _lastFragments) + " " + L("ui.fragments");
                _fragmentToastUntil = Time.unscaledTime + 0.9f;
            }
            _lastFragments = sim.EarnedFragments;
            if (Time.unscaledTime >= _fragmentToastUntil) _fragmentToast.text = "";
        }

        /// <summary>일시정지 메뉴에서 계속하기와 설정 변경 및 작업실 복귀를 제공한다.</summary>
        private void OpenPause()
        {
            RectTransform card = OpenModal(L("ui.pause"), 500, 384, _app.TogglePause);
            Button(card, 28, 90, 444, 48, L("ui.resume"), _app.TogglePause, CYAN, 19);
            Button(card, 28, 155, 444, 42, L("ui.shake") + " " + L(_app.Save.ScreenShake ? "ui.on" : "ui.off"), () => { _app.SetSetting("screenShake", !_app.Save.ScreenShake); OpenPause(); }, MUTED, 16);
            Button(card, 28, 214, 444, 42, L("ui.hitstop") + " " + L(_app.Save.HitStop ? "ui.on" : "ui.off"), () => { _app.SetSetting("hitStop", !_app.Save.HitStop); OpenPause(); }, MUTED, 16);
            Button(card, 28, 292, 444, 48, L("ui.retreat"), _app.AbandonMission, MUTED, 18);
        }

        /// <summary>시간 종료 또는 사망에 따른 정산과 단계 진행을 표시하고 노드 강화나 재도전을 제공한다.</summary>
        private void BuildResult()
        {
            RectTransform card = Panel(_page, 92, 72, 1096, 570, PANEL);
            Text(card, 40, 32, 1016, 48, L("ui.result"), 34, Color.white, FontStyles.Bold);
            Text(card, 40, 108, 1016, 108, _app.LastResult, 26, CYAN);
            if (_app.Mission != null)
            {
                Text(card, 40, 250, 1016, 35, L("ui.stage") + " " + _app.Mission.StageNumber + "  ·  " + L("ui.kills") + " " + _app.Mission.KillCount, 23, Color.white);
                Text(card, 40, 308, 1016, 35, L("ui.highestStage") + " " + _app.HighestClearedStage + "  ·  " + L("ui.nextStage") + " " + (_app.HighestClearedStage + 1), 22, MUTED);
            }
            Text(card, 40, 366, 1016, 64, L("ui.improveHint"), 20, MUTED);
            Button(card, 40, 464, 344, 58, L("ui.improveSpell"), () => { _app.ReturnWorkshop(); _app.SetTab("editor"); }, CYAN, 20);
            Button(card, 412, 464, 344, 58, L("ui.retryStage"), _app.RetryStage, MUTED, 20);
        }

        /// <summary>활성화된 디버그 전용 조각 지급, 해금, 무적 및 적 소환 조작을 표시한다.</summary>
        private void BuildDebug(Transform parent, float left, float top)
        {
            Button(parent, left, top, 444, 48, L("ui.grant"), _app.DebugGrant, CYAN);
            Button(parent, left, top + 65, 444, 48, L("ui.unlockAll"), _app.DebugUnlock);
            Button(parent, left, top + 130, 444, 48, L("ui.invulnerable"), _app.DebugInvulnerable);
            Button(parent, left, top + 195, 444, 48, L("ui.spawn"), () => _app.DebugSpawn());
        }

        /// <summary>HUD는 전체 태그의 학습값과 고정 상태를 한 줄 막대로, 도크는 사용된 태그의 수치를 표시한다.</summary>
        private string AdaptationText(RuneSimulation sim, bool bars)
        {
            string text = bars ? L("ui.adaptation") + "\n" : "";
            foreach (string tag in sim.Adaptation.Tags)
            {
                double value = sim.Adaptation.GetValue(tag);
                if (bars)
                {
                    bool isElement = tag == "fire" || tag == "ice" || tag == "arc" || tag == "raw";
                    float cap = isElement ? GameData.Balance.Adaptation.ElementCap : GameData.Balance.Adaptation.FormCap;
                    int filled = Mathf.Clamp(Mathf.RoundToInt((float)value / Mathf.Max(0.001f, cap) * 8), 0, 8);
                    string locked = sim.Adaptation.IsLocked(tag) ? "<color=#FFC96B>*</color>" : "";
                    text += "<color=#DDEBFA>" + L("tag." + tag) + "<pos=62>" + (value * 100).ToString("0") + "%</color>" + locked +
                        "<pos=112><color=#344455>" + new string('■', 8) + "</color><pos=112><color=#BFB2FF>" + new string('■', filled) + "</color>\n";
                }
                else if (value > 0) text += L("tag." + tag) + " " + (value * 100).ToString("0") + "%  ";
            }
            return text;
        }

        /// <summary>현재 단일 마법의 이름과 노드 용량 사용량을 반환한다.</summary>
        private string CurrentSpellText()
        {
            return L("ui.singleSpell") + "  " + _app.EditingGraph.Name + "  ·  " + L("ui.capacity") + " " + _app.EquippedRam + "/" + _app.Capacity;
        }

        /// <summary>입력 뒤의 화면을 가리는 모달을 표시하고 조작을 받는 카드 영역을 반환한다.</summary>
        private RectTransform OpenModal(string title, float width, float height, Action close = null)
        {
            CloseModal();
            _isModalOpen = true;
            _modal = Panel(_page, 0, 0, 1280, 720, new Color(0, 0.015f, 0.035f, 0.88f));
            _modal.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            RectTransform card = Panel(_modal, (1280 - width) / 2, (720 - height) / 2, width, height, PANEL);
            Text(card, 24, 21, width - 90, 35, title, 24, Color.white, FontStyles.Bold);
            Button(card, width - 60, 20, 36, 32, "×", () => { if (close == null) CloseModal(); else close(); }, MUTED, 23);
            return card;
        }

        /// <summary>열린 모달의 입력 차단을 해제하고 모달 객체를 제거한다.</summary>
        private void CloseModal()
        {
            if (_modal != null) { _modal.gameObject.SetActive(false); Destroy(_modal.gameObject); }
            _modal = null; _isModalOpen = false;
        }

        /// <summary>주어진 위치와 크기의 좌상단 기준 RectTransform을 생성한다.</summary>
        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var target = new GameObject(name, typeof(RectTransform));
            target.transform.SetParent(parent, false);
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        /// <summary>지정 색의 패널 이미지를 생성하며 기본적으로 포인터를 차단하지 않는다.</summary>
        private static RectTransform Panel(Transform parent, float x, float y, float width, float height, Color tint)
        {
            RectTransform rect = Rect(parent, "Panel", x, y, width, height);
            UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = tint; image.raycastTarget = false;
            return rect;
        }

        /// <summary>지정 위치에 글꼴, 크기와 색상을 설정한 TMP 텍스트를 생성한다.</summary>
        private TextMeshProUGUI Text(Transform parent, float x, float y, float width, float height, string value, float size, Color tint, FontStyles style = FontStyles.Normal)
        {
            RectTransform rect = Rect(parent, "Label", x, y, width, height);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font; text.fontSize = size; text.color = tint; text.fontStyle = style;
            text.text = value; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        /// <summary>포인터 입력이 가능한 버튼을 생성하고 실행 콜백을 연결한다.</summary>
        private UnityEngine.UI.Button Button(Transform parent, float x, float y, float width, float height, string label, Action action, Color? tint = null, float size = 14)
        {
            Color accent = tint ?? MUTED;
            RectTransform rect = Panel(parent, x, y, width, height, new Color(accent.r * 0.19f + 0.03f, accent.g * 0.19f + 0.05f, accent.b * 0.19f + 0.07f));
            UnityEngine.UI.Image image = rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f); colors.pressedColor = new Color(0.65f, 0.85f, 1);
            button.colors = colors;
            button.onClick.AddListener(() => action());
            TextMeshProUGUI text = Text(rect, 6, 0, width - 12, height, label, size, accent);
            text.alignment = TextAlignmentOptions.Midline;
            return button;
        }

        /// <summary>문자 입력 필드를 TMP 뷰포트, 표시 문자와 안내 문구에 연결한다.</summary>
        private TMP_InputField Input(Transform parent, float x, float y, float width, float height, string value, string placeholder)
        {
            RectTransform rect = Panel(parent, x, y, width, height, new Color(0.027f, 0.052f, 0.09f));
            rect.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            TMP_InputField field = rect.gameObject.AddComponent<TMP_InputField>();
            RectTransform viewport = Rect(rect, "TextViewport", 8, 4, width - 16, height - 8);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            TextMeshProUGUI text = Text(viewport, 0, 0, width - 16, height - 8, "", 13, Color.white);
            TextMeshProUGUI hint = Text(viewport, 0, 0, width - 16, height - 8, placeholder, 13, MUTED);
            field.textViewport = viewport; field.textComponent = text; field.placeholder = hint; field.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            field.text = value; field.fontAsset = Font;
            return field;
        }

        /// <summary>마스킹된 스크롤 영역과 자동 높이의 세로 목록을 생성하고 콘텐츠를 반환한다.</summary>
        private static RectTransform ScrollList(Transform parent, string name, float x, float y, float width, float height)
        {
            RectTransform root = Rect(parent, name, x, y, width, height);
            UnityEngine.UI.ScrollRect scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            RectTransform viewport = Panel(root, 0, 0, width, height, new Color(1, 1, 1, 0.005f));
            viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            UnityEngine.UI.Mask mask = viewport.gameObject.AddComponent<UnityEngine.UI.Mask>(); mask.showMaskGraphic = false;
            RectTransform content = Rect(viewport, "Content", 0, 0, width - 8, height);
            UnityEngine.UI.VerticalLayoutGroup layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 5; layout.childControlWidth = true; layout.childControlHeight = false; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            UnityEngine.UI.ContentSizeFitter fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            return content;
        }

        /// <summary>세로 목록 요소의 최소 높이와 원하는 높이를 고정한다.</summary>
        private static void Layout(GameObject target, float height)
        {
            UnityEngine.UI.LayoutElement layout = target.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredHeight = height; layout.minHeight = height;
        }

        /// <summary>현재 선택 입력이 TMP 필드이면 단축키 처리를 제한한다.</summary>
        private static bool IsTyping() => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;

        /// <summary>특정 값이 읽기 전용 문자열 목록에 포함되어 있는지 반환한다.</summary>
        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            foreach (string entry in values) if (entry == value) return true;
            return false;
        }

        /// <summary>패널의 기존 표시 요소를 비활성화한 뒤 제거한다.</summary>
        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) { parent.GetChild(i).gameObject.SetActive(false); Destroy(parent.GetChild(i).gameObject); }
        }

        /// <summary>사용자 문자열 키에 해당하는 한국어 데이터 값을 반환한다.</summary>
        private static string L(string key) => GameData.L(key);
    }
}
