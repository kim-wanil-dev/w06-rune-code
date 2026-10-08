using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 비주얼 스크립팅 편집 패널이다. 마법 제목 줄, 메트릭, 룬 팔레트, 노드 그래프, 인스펙터, 편집 도구줄,
    /// 튜토리얼과 패널 내부 모달을 표시하고 모든 편집 명령을 ISpellEditor로 전달한다.
    /// 레이아웃과 직렬화 참조는 UI/Editor/Layouts/SpellEditorLayout이 만든다.
    /// </summary>
    public sealed class SpellEditorPanel : MonoBehaviour
    {
        public static readonly string[] CATEGORY_IDS = { "all", SpellGrammar.CATEGORY_MAGIC_TYPE, SpellGrammar.CATEGORY_ELEMENT, SpellGrammar.CATEGORY_SHAPE, SpellGrammar.CATEGORY_MODIFIER, SpellGrammar.CATEGORY_FLOW, SpellGrammar.CATEGORY_METHOD };
        private static readonly Color LOCKED_TINT = new Color(0.35f, 0.42f, 0.50f);
        private static readonly Color ISSUE_ERROR_TINT = new Color(1f, 0.41f, 0.44f);
        private static readonly Color ISSUE_WARNING_TINT = new Color(1f, 0.76f, 0.38f);
        private static readonly Color DELETE_TINT = new Color(0.97f, 0.43f, 0.45f);

        [Header("마법 제목 줄")]
        [SerializeField] private TextMeshProUGUI _spellTitle;
        [SerializeField] private Button _libraryButton;
        [SerializeField] private Button _renameButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _shareButton;

        [Header("지표와 팔레트")]
        [SerializeField] private TextMeshProUGUI _metricsText;
        [SerializeField] private TMP_InputField _searchField;
        [SerializeField] private Button[] _categoryButtons;
        [SerializeField] private RectTransform _runeList;
        [SerializeField] private UiRow _runeRowPrefab;

        [Header("그래프와 인스펙터")]
        [SerializeField] private RuneGraphCanvas _graphCanvas;
        [SerializeField] private RectTransform _inspectorPanel;
        [SerializeField] private SpellParameterRow _parameterRowPrefab;

        [Header("편집 도구줄")]
        [SerializeField] private Button _undoButton;
        [SerializeField] private Button _redoButton;
        [SerializeField] private Button _arrangeButton;
        [SerializeField] private Button _copyButton;
        [SerializeField] private Button _pasteButton;
        [SerializeField] private TextMeshProUGUI _tooltipText;

        [Header("튜토리얼")]
        [SerializeField] private TextMeshProUGUI _tutorialText;
        [SerializeField] private Button _tutorialButton;

        [Header("빠른 팔레트 모달")]
        [SerializeField] private RectTransform _quickPaletteModal;
        [SerializeField] private Button _quickPaletteCloseButton;
        [SerializeField] private TMP_InputField _quickPaletteSearch;
        [SerializeField] private RectTransform _quickPaletteList;
        [SerializeField] private UiRow _quickPaletteRowPrefab;
        [SerializeField] private UiRow _issueRowPrefab;

        [Header("보관함 모달")]
        [SerializeField] private RectTransform _libraryModal;
        [SerializeField] private Button _libraryCloseButton;
        [SerializeField] private RectTransform _libraryList;
        [SerializeField] private Button _newSpellButton;
        [SerializeField] private Button _duplicateSpellButton;
        [SerializeField] private UiRow _libraryRowPrefab;

        [Header("Spell Call 대상 모달")]
        [SerializeField] private RectTransform _spellPickerModal;
        [SerializeField] private Button _spellPickerCloseButton;
        [SerializeField] private RectTransform _spellPickerList;

        [Header("튜토리얼 안내 모달")]
        [SerializeField] private RectTransform _tutorialModal;
        [SerializeField] private Button _tutorialCloseButton;

        [Header("이름 변경 모달")]
        [SerializeField] private RectTransform _renameModal;
        [SerializeField] private Button _renameCloseButton;
        [SerializeField] private TMP_InputField _renameField;
        [SerializeField] private Button _renameSaveButton;

        [Header("공유 모달")]
        [SerializeField] private RectTransform _shareModal;
        [SerializeField] private Button _shareCloseButton;
        [SerializeField] private TMP_InputField _shareField;
        [SerializeField] private Button _shareExportButton;
        [SerializeField] private Button _sharePasteButton;
        [SerializeField] private Button _shareImportButton;

        [Header("공용")]
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private GameObject _panelPrefab;
        [SerializeField] private GameObject _buttonPrefab;

        private ISpellEditor _editor;
        private ISpellEditorHost _host;
        private UiFactory _ui;
        private RectTransform _openModal;
        private GraphNode _selected;
        private Vector2 _quickPalettePosition;
        private string _category = "all";
        private bool _isModalOpen;
        private bool _isTutorialActive;
        private int _tutorialStartExecutions;

        /// <summary>패널 모달이 열려 입력을 가로막고 있는지 반환한다.</summary>
        public bool IsModalOpen => _isModalOpen;

        /// <summary>편집 세션과 작업실 호스트를 받아 버튼 리스너와 갱신 이벤트를 연결하고 표시 값을 채운다.</summary>
        public void Initialize(ISpellEditor editor, ISpellEditorHost host)
        {
            _editor = editor;
            _host = host;
            if (_font == null) _font = TMP_Settings.defaultFontAsset;
            if (_ui == null) _ui = new UiFactory(_font, _panelPrefab, _buttonPrefab);
            _searchField.onValueChanged.AddListener(_ => RefreshPalette());
            for (int i = 0; i < _categoryButtons.Length; i++)
            {
                Button button = _categoryButtons[i];
                string category = CATEGORY_IDS[i];
                button.onClick.AddListener(() => { _category = category; RefreshPalette(); });
            }
            _libraryButton.onClick.AddListener(OpenSpellLibrary);
            _renameButton.onClick.AddListener(OpenRename);
            _saveButton.onClick.AddListener(() => _editor.Save());
            _shareButton.onClick.AddListener(OpenShare);
            _undoButton.onClick.AddListener(_editor.Undo);
            _redoButton.onClick.AddListener(_editor.Redo);
            _arrangeButton.onClick.AddListener(_editor.AutoArrange);
            _copyButton.onClick.AddListener(() => _editor.CopyNodes(_graphCanvas.SelectedNodes));
            _pasteButton.onClick.AddListener(_editor.PasteNodes);
            _tutorialButton.onClick.AddListener(() =>
            {
                _isTutorialActive = true;
                _tutorialStartExecutions = _host.TestExecutionCount;
                OpenTutorialHelp();
            });
            _quickPaletteCloseButton.onClick.AddListener(CloseModal);
            _quickPaletteSearch.onValueChanged.AddListener(PopulateQuickPalette);
            _libraryCloseButton.onClick.AddListener(CloseModal);
            _newSpellButton.onClick.AddListener(() => { CloseModal(); _editor.NewSpell(); });
            _duplicateSpellButton.onClick.AddListener(() => { CloseModal(); _editor.DuplicateSpell(); });
            _spellPickerCloseButton.onClick.AddListener(CloseModal);
            _tutorialCloseButton.onClick.AddListener(CloseModal);
            _renameCloseButton.onClick.AddListener(CloseModal);
            _renameSaveButton.onClick.AddListener(() => { _editor.Rename(_renameField.text); CloseModal(); });
            _shareCloseButton.onClick.AddListener(CloseModal);
            _shareExportButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = _shareField.text);
            _sharePasteButton.onClick.AddListener(() => _shareField.text = GUIUtility.systemCopyBuffer);
            _shareImportButton.onClick.AddListener(() => { _editor.Import(_shareField.text); CloseModal(); });
            _graphCanvas.Initialize(_editor, _font, node => { _selected = node; BuildInspector(node); }, UpdateTooltip, OpenQuickPalette);
            _editor.GraphChanged += OnGraphChanged;
            _editor.SpellSwitched += OnSpellSwitched;
            RefreshAll();
        }

        /// <summary>패널을 표시하고 편집을 허용한 뒤 전체 표시 값을 갱신한다.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            _editor.SetEditable(true);
            RefreshAll();
        }

        /// <summary>열린 모달을 닫고 편집을 금지한 뒤 패널을 숨긴다.</summary>
        public void Hide()
        {
            CloseModal();
            _editor.SetEditable(false);
            gameObject.SetActive(false);
        }

        /// <summary>도크 실행 하이라이트 요청을 그래프 캔버스에 전달한다.</summary>
        public void HighlightNode(string nodeId)
        {
            if (_graphCanvas != null) _graphCanvas.Highlight(nodeId);
        }

        void Update()
        {
            if (_editor == null) return;
            UpdateTutorial();
            if (_graphCanvas == null || _isModalOpen || UiFactory.IsTyping()) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            bool isControl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) _graphCanvas.DeleteSelection();
            if (isControl && keyboard.cKey.wasPressedThisFrame) _editor.CopyNodes(_graphCanvas.SelectedNodes);
            if (isControl && keyboard.vKey.wasPressedThisFrame) _editor.PasteNodes();
            if (isControl && keyboard.dKey.wasPressedThisFrame) { _editor.CopyNodes(_graphCanvas.SelectedNodes); _editor.PasteNodes(); }
            if (isControl && keyboard.sKey.wasPressedThisFrame) _editor.Save();
            if (keyboard.digit1Key.wasPressedThisFrame) _host.FireTest();
            if (isControl && keyboard.zKey.wasPressedThisFrame) _editor.Undo();
            if (isControl && keyboard.yKey.wasPressedThisFrame) _editor.Redo();
        }

        void OnDestroy()
        {
            if (_editor != null)
            {
                _editor.GraphChanged -= OnGraphChanged;
                _editor.SpellSwitched -= OnSpellSwitched;
            }
        }

        /// <summary>그래프·제목·메트릭·인스펙터를 현재 편집 상태로 갱신한다. GraphChanged 이벤트와 Show에서 사용한다.</summary>
        private void RefreshAll()
        {
            _spellTitle.text = _editor.Graph.Name;
            RefreshPalette();
            RefreshMetrics();
            _graphCanvas.ResetView();
            if (_selected != null) _selected = _editor.Graph.FindNode(_selected.Id);
            BuildInspector(_selected);
        }

        /// <summary>그래프 표시와 제목, 메트릭, 선택 노드 인스펙터를 다시 그린다. GraphChanged 구독자다.</summary>
        private void OnGraphChanged()
        {
            _graphCanvas.RefreshGraph();
            _spellTitle.text = _editor.Graph.Name;
            RefreshMetrics();
            if (_selected != null) _selected = _editor.Graph.FindNode(_selected.Id);
            BuildInspector(_selected);
        }

        /// <summary>모달과 선택을 해제하고 패널 전체 값을 새 마법 기준으로 갱신한다. SpellSwitched 구독자다.</summary>
        private void OnSpellSwitched()
        {
            CloseModal();
            _selected = null;
            RefreshAll();
        }

        /// <summary>선택 계열과 검색 문자열에 해당하는 룬 목록을 다시 표시한다.</summary>
        private void RefreshPalette()
        {
            UiFactory.ClearChildren(_runeList);
            string query = _searchField.text.Trim();
            foreach (RuneDefinition rune in GameData.Runes.All)
            {
                if (!IsPaletteRune(rune) || (_category != "all" && rune.Category != _category)) continue;
                if (!string.IsNullOrEmpty(query) && rune.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 && rune.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool unlocked = _editor.IsRuneUnlocked(rune.Id);
                RuneDefinition selected = rune;
                string label = rune.Name + "   " + rune.Ram + " RAM";
                if (!unlocked) label += "\n" + L("ui.locked") + " · " + (rune.UnlockType == "reward" ? L("ui.reward") : rune.UnlockCost + " " + L("ui.fragments"));
                UiRow row = Instantiate(_runeRowPrefab, _runeList);
                row.Configure(label, unlocked ? RuneMesh.CategoryColor(rune.Category) : LOCKED_TINT, unlocked ? 40 : 53,
                    () => { if (unlocked) _graphCanvas.PlaceRune(selected.Id, _graphCanvas.SuggestPlacement(selected.Id)); }, 12);
                row.gameObject.AddComponent<RunePaletteDrag>().Initialize(_graphCanvas, rune.Id, unlocked);
            }
        }

        /// <summary>시작 조건(항상 존재)과 컴파일 내부 정의를 제외한, 팔레트에 배치 가능한 룬인지 반환한다.</summary>
        private static bool IsPaletteRune(RuneDefinition rune)
        {
            return rune.Category != SpellGrammar.CATEGORY_CORE && rune.Category != SpellGrammar.CATEGORY_INTERNAL;
        }

        /// <summary>빈 편집 영역의 빠른 검색 입력과 검색된 룬의 현재 위치 배치를 제공한다.</summary>
        private void OpenQuickPalette(Vector2 position)
        {
            _quickPalettePosition = position;
            OpenModal(_quickPaletteModal);
            _quickPaletteSearch.text = string.Empty;
            PopulateQuickPalette(string.Empty);
            _quickPaletteSearch.ActivateInputField();
        }

        /// <summary>해금된 룬 가운데 검색 문자열과 일치하는 항목으로 빠른 팔레트 목록을 채운다.</summary>
        private void PopulateQuickPalette(string query)
        {
            UiFactory.ClearChildren(_quickPaletteList);
            foreach (RuneDefinition rune in GameData.Runes.All)
            {
                if (!IsPaletteRune(rune) || !_editor.IsRuneUnlocked(rune.Id)) continue;
                if (!string.IsNullOrEmpty(query) && rune.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 && rune.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string id = rune.Id;
                UiRow row = Instantiate(_quickPaletteRowPrefab, _quickPaletteList);
                row.Configure(rune.Name + " · " + rune.Ram + " RAM", RuneMesh.CategoryColor(rune.Category), 36,
                    () => { CloseModal(); _graphCanvas.PlaceRune(id, _quickPalettePosition); }, 14);
            }
        }

        /// <summary>선택한 노드의 조정 가능한 파라미터와 그래프 검증 메시지를 표시한다.</summary>
        private void BuildInspector(GraphNode node)
        {
            UiFactory.ClearChildren(_inspectorPanel);
            _ui.Text(_inspectorPanel, 14, 12, 222, 27, L("ui.inspector"), 17, Color.white, FontStyles.Bold);
            float top = 50;
            if (node == null)
            {
                _ui.Text(_inspectorPanel, 14, top, 224, 62, L("ui.selectNode"), 13, UiTheme.Muted);
                top += 72;
            }
            else
            {
                RuneDefinition rune = GameData.Runes.Get(node.RuneId);
                _ui.Text(_inspectorPanel, 14, top, 222, 28, rune.Name + " / " + rune.Ram + " RAM", 17, RuneMesh.CategoryColor(rune.Category));
                top += 37;
                foreach (ParameterDefinition param in rune.Params)
                {
                    ParameterDefinition definition = param;
                    string nodeId = node.Id;
                    SpellParameterRow row = Instantiate(_parameterRowPrefab, _inspectorPanel);
                    ((RectTransform)row.transform).anchoredPosition = new Vector2(14, -top);
                    if (param.Kind == "number")
                    {
                        bool isModifierScale = rune.Category == SpellGrammar.CATEGORY_MODIFIER;
                        row.ConfigureNumber(L("param." + param.Id), node.GetNumber(param.Id, param.DefaultNumber).ToString("0.###"), isModifierScale);
                        row.Input.contentType = TMP_InputField.ContentType.DecimalNumber;
                        row.Input.onEndEdit.AddListener(value => { if (float.TryParse(value, out float amount)) _editor.SetNodeNumber(nodeId, definition.Id, Mathf.Clamp(amount, definition.Min, definition.Max)); });
                        if (isModifierScale)
                        {
                            row.MinusButton.onClick.AddListener(() => ChangeModifierNumber(nodeId, definition, -1));
                            row.PlusButton.onClick.AddListener(() => ChangeModifierNumber(nodeId, definition, 1));
                        }
                    }
                    else if (param.Kind == "text" && rune.Id == "spell.call" && param.Id == "spellId")
                    {
                        string spellId = node.GetText(param.Id, param.DefaultText);
                        SpellGraph called = _editor.Library.FirstOrDefault(graph => graph.Id == spellId);
                        string label = called == null
                            ? (string.IsNullOrEmpty(spellId) ? L("ui.selectSpell") : spellId + " · " + L("ui.missingSpell"))
                            : called.Name + "\n" + called.Id;
                        row.ConfigureSpell(L("param." + param.Id), label);
                        row.SpellButton.onClick.AddListener(() => OpenSpellPicker(nodeId));
                    }
                    else if (param.Kind == "text")
                    {
                        row.ConfigureText(L("param." + param.Id), node.GetText(param.Id, param.DefaultText));
                        row.Input.onEndEdit.AddListener(value => _editor.SetNodeText(nodeId, definition.Id, value));
                    }
                    else
                    {
                        string current = node.GetText(param.Id, param.DefaultText);
                        row.ConfigureEnum(L("param." + param.Id), OptionLabel(param.Id, current));
                        row.OptionButton.onClick.AddListener(() =>
                        {
                            int index = 0;
                            for (int i = 0; i < definition.Options.Count; i++) if (definition.Options[i] == current) index = i;
                            _editor.SetNodeText(nodeId, definition.Id, definition.Options[(index + 1) % definition.Options.Count]);
                        });
                    }
                    top += param.Kind == "text" && rune.Id == "spell.call" && param.Id == "spellId" ? 76 : 64;
                }
                if (rune.Category == SpellGrammar.CATEGORY_MODIFIER && rune.Params.Count > 0)
                {
                    ParameterDefinition parameter = rune.Params[0];
                    _ui.Text(_inspectorPanel, 14, top, 222, 26, L("ui.scaleBounds") + " " + parameter.Min.ToString("0.#") + "–" + parameter.Max.ToString("0.#") + "×", 12, UiTheme.Cyan);
                    top += 30;
                    _ui.Text(_inspectorPanel, 14, top, 222, 72, L("ui." + rune.Id + "Hint"), 12, UiTheme.Muted);
                    top += 78;
                }
                if (rune.Params.Count == 0)
                {
                    _ui.Text(_inspectorPanel, 14, top, 222, 34, L("ui.energy") + " " + rune.Energy.ToString("0.#") + "  /  " + L("ui.damage") + " " + rune.Stats.Damage.ToString("0.#"), 12, UiTheme.Muted);
                    top += 42;
                }
                if (rune.Category == SpellGrammar.CATEGORY_SHAPE)
                {
                    SpellAction action = _editor.CompileResult.Spell?.FindAction(node.Id);
                    if (action != null && action.Stats != null)
                    {
                        SpellStats stats = action.Stats;
                        float reach = action.Form == SpellGrammar.FORM_BOLT ? stats.Speed * stats.Lifetime : action.Form == SpellGrammar.FORM_ORBIT ? stats.OrbitRadius : stats.Offset;
                        _ui.Text(_inspectorPanel, 14, top, 222, 70,
                            L("ui.spellRange") + " " + reach.ToString("0.#") + "px\n" +
                            L("ui.spellRadius") + " " + stats.Radius.ToString("0.#") + "px\n" +
                            L("ui.spellSpeed") + " " + (action.Form == SpellGrammar.FORM_ORBIT ? stats.AngularSpeed.ToString("0.#") + "°/s" : stats.Speed.ToString("0.#") + "px/s"), 12, UiTheme.Cyan);
                        top += 76;
                    }
                }
                if (rune.Category != SpellGrammar.CATEGORY_CORE)
                {
                    _ui.Button(_inspectorPanel, 14, top, 222, 29, L("ui.delete"), () => _editor.RemoveNode(node.Id), DELETE_TINT, 12);
                    top += 42;
                }
            }
            _ui.Text(_inspectorPanel, 14, top, 222, 25, L("ui.validation"), 15, Color.white, FontStyles.Bold);
            top += 31;
            RectTransform errors = UiFactory.ScrollList(_inspectorPanel, "Validation", 12, top, 228, Mathf.Max(80, 510 - top));
            CompileResult result = _editor.CompileResult;
            if (result.Ok) AddIssueLabel(errors, L("ui.valid"), UiTheme.Cyan, null);
            foreach (CompileIssue issue in result.Errors) AddIssueLabel(errors, issue.Code + " · " + CompileIssueText.Format(issue), ISSUE_ERROR_TINT, issue.NodeId);
            foreach (CompileIssue issue in result.Warnings) AddIssueLabel(errors, issue.Code + " · " + CompileIssueText.Format(issue), ISSUE_WARNING_TINT, issue.NodeId);
            AddIssueLabel(errors, L("ui.controls"), UiTheme.Muted, null);
        }

        /// <summary>Spell Library 항목을 표시하고 선택 또는 새 Spell Function 생성을 제공한다.</summary>
        private void OpenSpellLibrary()
        {
            OpenModal(_libraryModal);
            UiFactory.ClearChildren(_libraryList);
            foreach (SpellGraph graph in _editor.Library)
            {
                SpellGraph selected = graph;
                UiRow row = Instantiate(_libraryRowPrefab, _libraryList);
                row.Configure(graph.Name + "\n" + graph.Id, UiTheme.Cyan, 50, () => { _editor.SelectSpell(selected.Id); CloseModal(); }, 12);
            }
        }

        /// <summary>Spell Call 대상 목록에서 표시 이름을 보여 주고 안정된 ID를 노드에 저장한다.</summary>
        private void OpenSpellPicker(string nodeId)
        {
            OpenModal(_spellPickerModal);
            UiFactory.ClearChildren(_spellPickerList);
            foreach (SpellGraph graph in _editor.Library)
            {
                SpellGraph selected = graph;
                UiRow row = Instantiate(_libraryRowPrefab, _spellPickerList);
                row.Configure(graph.Name + "\n" + graph.Id, UiTheme.Cyan, 50, () =>
                {
                    CloseModal();
                    _editor.SetNodeText(nodeId, "spellId", selected.Id);
                }, 12);
            }
        }

        /// <summary>Trigger·Magic enum 값을 한국어 라벨로 반환하고 번역이 없으면 원문 값을 표시한다.</summary>
        private static string OptionLabel(string parameter, string value)
        {
            string prefix = parameter == "trigger" ? "trigger." : parameter == "magicType" ? "magicType."
                : parameter == "element" ? "element." : parameter == "form" ? "magicForm."
                : parameter == "status" ? "status." : "condition.";
            string localized = L(prefix + value);
            return localized == prefix + value ? value : localized;
        }

        /// <summary>선택 수식의 숫자 배율을 데이터의 한 단계만큼 조절하고 범위 안으로 제한하여 표시를 갱신한다.</summary>
        private void ChangeModifierNumber(string nodeId, ParameterDefinition parameter, int direction)
        {
            GraphNode node = _editor.Graph.FindNode(nodeId);
            float value = node.GetNumber(parameter.Id, parameter.DefaultNumber) + parameter.Step * direction;
            _editor.SetNodeNumber(nodeId, parameter.Id, Mathf.Clamp(value, parameter.Min, parameter.Max));
            OnGraphChanged();
        }

        /// <summary>검증 메시지를 선택 노드 포커스 기능과 함께 목록에 추가한다.</summary>
        private void AddIssueLabel(RectTransform list, string label, Color tint, string nodeId)
        {
            UiRow row = Instantiate(_issueRowPrefab, list);
            row.Configure(label, tint, 56, () => { if (nodeId != null) _graphCanvas.FocusNode(nodeId); }, 11);
        }

        /// <summary>공유 RAM, 마법 비용, 쿨다운과 예상 동시 개체 수를 표시한다.</summary>
        private void RefreshMetrics()
        {
            CompiledSpell spell = _editor.CompileResult.Spell;
            int ram = 0;
            foreach (GraphNode node in _editor.Graph.Nodes) ram += GameData.Runes.Get(node.RuneId).Ram;
            _metricsText.text = L("ui.ram") + " " + _host.EquippedRam + "/" + _host.Capacity + "  ·  " + ram + " RAM" +
                (spell == null ? "  ·  " + L("ui.warning") : "  ·  " + L("ui.energy") + " " + spell.EnergyCost.ToString("0.#") + "  ·  " + L("ui.cooldown") + " " + spell.Cooldown.ToString("0.00") + "s  ·  " + L("ui.peak") + " " + spell.WorstCaseEntities);
        }

        /// <summary>마우스가 가리키는 룬 효과 또는 출력 분기의 실행 순서를 표시한다.</summary>
        private void UpdateTooltip(GraphNode node)
        {
            if (node == null) { _tooltipText.text = L("ui.executionOrder"); return; }
            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            SpellAction action = _editor.CompileResult.Spell?.FindAction(node.Id);
            _tooltipText.text = rune.Name + "  ·  " + L("ui.damage") + " " + (action?.Stats?.Damage ?? rune.Stats.Damage).ToString("0.#") +
                "  ·  " + L("ui.energy") + " " + (action?.OwnEnergy ?? rune.Energy).ToString("0.#");
        }

        /// <summary>타입→발사 체인 연결, 화염 속성 삽입, 실제 시전 순서의 초보자 안내를 진행 상태에 맞춰 표시한다.</summary>
        private void UpdateTutorial()
        {
            bool hasCastType = false;
            bool hasLaunch = false;
            bool hasFire = false;
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                bool isType = SpellGrammar.MagicTypeOf(node.RuneId) != null;
                foreach (GraphEdge edge in _editor.Graph.Edges)
                {
                    if (isType && edge.ToNode == node.Id && edge.ToPort == SpellGrammar.EXEC_PORT) hasCastType = true;
                    if (node.RuneId == "shape.launch" && edge.ToNode == node.Id && edge.ToPort == SpellGrammar.CHAIN_IN) hasLaunch = true;
                    if (node.RuneId == "element.fire" && edge.FromNode == node.Id) hasFire = true;
                }
            }
            bool hasChain = hasCastType && hasLaunch;
            bool hasTested = _host.TestExecutionCount > (_isTutorialActive ? _tutorialStartExecutions : 0);
            _tutorialText.text = L(!hasChain ? "ui.tutorial1" : !hasFire ? "ui.tutorial2" : !hasTested ? "ui.tutorial3" : "ui.complete");
            if (_isTutorialActive && hasChain && hasFire && hasTested) { _isTutorialActive = false; _host.CompleteTutorial(); }
        }

        /// <summary>현재 마법을 보존하면서 노드 배치, 연결과 시험 안내를 표시한다.</summary>
        private void OpenTutorialHelp()
        {
            OpenModal(_tutorialModal);
        }

        /// <summary>계속 강화하는 현재 마법의 이름 입력을 표시하고 이름을 저장한다.</summary>
        private void OpenRename()
        {
            OpenModal(_renameModal);
            _renameField.text = _editor.Graph.Name;
        }

        /// <summary>RC1 공유 코드의 클립보드 복사와 검증된 가져오기 입력을 표시한다.</summary>
        private void OpenShare()
        {
            OpenModal(_shareModal);
            _shareField.text = _editor.Export();
        }

        /// <summary>입력 뒤의 화면을 가리는 모달을 열고 입력 차단을 시작한다.</summary>
        private void OpenModal(RectTransform modal)
        {
            CloseModal();
            _openModal = modal;
            _isModalOpen = true;
            _openModal.gameObject.SetActive(true);
        }

        /// <summary>열린 모달의 입력 차단을 해제하고 모달을 숨긴다.</summary>
        private void CloseModal()
        {
            if (_openModal != null) _openModal.gameObject.SetActive(false);
            _openModal = null;
            _isModalOpen = false;
        }

        /// <summary>사용자 문자열 키에 해당하는 한국어 데이터 값을 반환한다.</summary>
        private static string L(string key) => GameData.L(key);
    }
}
