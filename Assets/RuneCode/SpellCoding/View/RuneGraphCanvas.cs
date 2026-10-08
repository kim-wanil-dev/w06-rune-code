using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RuneCode
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RuneGraphCanvas : UnityEngine.UI.MaskableGraphic, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler, IScrollHandler, IPointerMoveHandler
    {
        private const float NODE_WIDTH = 160f;
        private const float PORT_SPACING = 23f;
        private const float LABEL_FONT_SIZE = 12f;
        private const float AUTO_CONNECT_DISTANCE = 175f;
        private const float AUTO_CHAIN_DISTANCE = 320f;

        private static readonly Color CHAIN_COLOR = new Color(0.98f, 0.74f, 0.36f);

        private readonly HashSet<string> _selected = new HashSet<string>();
        private readonly Dictionary<string, TextMeshProUGUI> _labels = new Dictionary<string, TextMeshProUGUI>();
        private readonly Dictionary<string, Vector2> _dragOrigins = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, float> _executedUntil = new Dictionary<string, float>();
        private readonly Dictionary<string, GraphNode> _nodeLookup = new Dictionary<string, GraphNode>();
        private readonly Dictionary<string, bool[]> _connectablePorts = new Dictionary<string, bool[]>();
        private readonly HashSet<string> _existingNodes = new HashSet<string>();
        private readonly List<string> _staleIds = new List<string>();
        private ISpellEditor _editor;
        private TMP_FontAsset _font;
        private Vector2 _pan = new Vector2(30, -35);
        private Vector2 _pointer;
        private Vector2 _dragStart;
        private Vector2 _panStart;
        private float _zoom = 1;
        private bool _isPanning;
        private bool _isBoxSelecting;
        private bool _isNodeDragging;
        private string _sourceNode;
        private string _sourcePort;
        private string _sourceKind;
        private bool _isSourceOutput;
        private GraphNode _hoveredNode;
        private bool _isHoverValid;
        private Action<GraphNode> _selectionChanged;
        private Action<GraphNode> _hoverChanged;
        private Action<Vector2> _quickPlacement;
        public IReadOnlyCollection<string> SelectedNodes => _selected;

        /// <summary>편집 세션, 글꼴과 선택 콜백을 연결하고 그래프를 표시한다.</summary>
        public void Initialize(ISpellEditor editor, TMP_FontAsset font, Action<GraphNode> selectionChanged, Action<GraphNode> hoverChanged, Action<Vector2> quickPlacement)
        {
            _editor = editor;
            _font = font;
            _selectionChanged = selectionChanged;
            _hoverChanged = hoverChanged;
            _quickPlacement = quickPlacement;
            raycastTarget = true;
            ResetView();
        }

        /// <summary>그래프 전체가 보이도록 화면 위치와 배율을 초기 배치하고 표시를 갱신한다.</summary>
        public void ResetView()
        {
            FitGraph();
            RefreshGraph();
        }

        /// <summary>첫 표시 때 모든 노드가 편집 영역 안에 보이도록 화면 위치와 배율을 맞춘다.</summary>
        private void FitGraph()
        {
            if (_editor == null || _editor.Graph == null || _editor.Graph.Nodes.Count == 0) return;
            float minimumX = float.MaxValue;
            float minimumY = float.MaxValue;
            float maximumX = float.MinValue;
            float maximumY = float.MinValue;
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                float height = NodeHeight(GameData.Runes.Get(node.RuneId));
                minimumX = Mathf.Min(minimumX, node.X); minimumY = Mathf.Min(minimumY, node.Y);
                maximumX = Mathf.Max(maximumX, node.X + NODE_WIDTH); maximumY = Mathf.Max(maximumY, node.Y + height);
            }
            _zoom = Mathf.Clamp(Mathf.Min((rectTransform.rect.width - 48) / Mathf.Max(1, maximumX - minimumX),
                (rectTransform.rect.height - 48) / Mathf.Max(1, maximumY - minimumY)), 0.45f, 1);
            _pan = new Vector2(24 - minimumX * _zoom, -24 + minimumY * _zoom);
        }

        /// <summary>현재 그래프의 노드 라벨 텍스트·배치와 메시를 갱신하고 사라진 노드의 선택을 해제한다.</summary>
        public void RefreshGraph()
        {
            if (_editor == null || _editor.Graph == null) return;
            _existingNodes.Clear();
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                _existingNodes.Add(node.Id);
                if (!_labels.TryGetValue(node.Id, out TextMeshProUGUI label))
                {
                    var target = new GameObject("NodeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                    target.transform.SetParent(transform, false);
                    label = target.GetComponent<TextMeshProUGUI>();
                    label.font = _font;
                    label.fontSize = LABEL_FONT_SIZE;
                    label.color = Color.white;
                    label.raycastTarget = false;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.overflowMode = TextOverflowModes.Overflow;
                    RectTransform textRect = label.rectTransform;
                    textRect.anchorMin = textRect.anchorMax = new Vector2(0, 1);
                    textRect.pivot = new Vector2(0, 1);
                    _labels.Add(node.Id, label);
                }
                RuneDefinition rune = GameData.Runes.Get(node.RuneId);
                int rows = Math.Max(CountPorts(rune, "in"), CountPorts(rune, "out"));
                string text = "<b>" + rune.Name + "</b>  <color=#8EACC6>" + rune.Ram + " RAM</color>\n";
                string summary = NodeSummary(node);
                if (!string.IsNullOrEmpty(summary)) text += "<size=10><color=#A8C9D8>" + summary + "</color></size>\n";
                for (int row = 0; row < rows; row++)
                {
                    PortDefinition input = PortAt(rune, "in", row);
                    PortDefinition output = PortAt(rune, "out", row);
                    text += "<size=10>" + (input == null ? "" : input.Id) +
                        "<pos=75>" + (output == null ? "" : output.Id) + "</size>\n";
                }
                if (label.text != text) label.text = text;
            }
            _staleIds.Clear();
            foreach (var pair in _labels) if (!_existingNodes.Contains(pair.Key)) _staleIds.Add(pair.Key);
            foreach (string id in _staleIds) { Destroy(_labels[id].gameObject); _labels.Remove(id); }
            _selected.RemoveWhere(id => !_existingNodes.Contains(id));
            _isHoverValid = false;
            LayoutLabels();
        }

        /// <summary>
        /// 화면 이동·확대 배율에 맞춰 라벨 위치와 스케일만 갱신하고 메시 재구성을 예약한다.
        /// 글꼴 크기는 고정하고 Transform 스케일로 확대해 rich text 크기·위치 태그가 노드와 같은 비율을 유지한다.
        /// </summary>
        private void LayoutLabels()
        {
            if (_editor == null || _editor.Graph == null) return;
            Vector3 scale = new Vector3(_zoom, _zoom, 1);
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                if (!_labels.TryGetValue(node.Id, out TextMeshProUGUI label)) continue;
                RuneDefinition rune = GameData.Runes.Get(node.RuneId);
                Rect rect = NodeRect(node, rune);
                RectTransform textRect = label.rectTransform;
                textRect.anchoredPosition = new Vector2(rect.x + 14 * _zoom, rect.yMax - rectTransform.rect.yMax - 10 * _zoom);
                textRect.sizeDelta = new Vector2(NODE_WIDTH - 28, NodeHeight(rune) - 14);
                textRect.localScale = scale;
            }
            SetVerticesDirty();
        }

        /// <summary>Trigger·Inline Magic·Spell Call의 핵심 값을 노드 라벨에 표시한다.</summary>
        private string NodeSummary(GraphNode node)
        {
            if (node.RuneId == "core.cast") return LocalizedValue("trigger." + node.GetText("trigger", "attack"), node.GetText("trigger", "attack"));
            if (node.RuneId == "magic.inline")
            {
                string type = node.GetText("magicType", "sphere");
                string element = node.GetText("element", "normal");
                string form = node.GetText("form", "launch");
                return LocalizedValue("magicType." + type, type) + " / "
                    + LocalizedValue("element." + element, element) + " / "
                    + LocalizedValue("magicForm." + form, form);
            }
            if (node.RuneId == "spell.call")
            {
                string spellId = node.GetText("spellId");
                foreach (SpellGraph graph in _editor.Library)
                    if (graph.Id == spellId) return graph.Name;
                return string.IsNullOrEmpty(spellId) ? GameData.L("ui.selectSpell") : GameData.L("ui.missingSpell");
            }
            return null;
        }

        /// <summary>현지화 값을 찾고 번역이 없으면 지정 원문을 반환한다.</summary>
        private static string LocalizedValue(string key, string fallback)
        {
            string value = GameData.L(key);
            return value == key ? fallback : value;
        }

        /// <summary>지정 노드를 실행 하이라이트 상태로 200밀리초 표시한다.</summary>
        public void Highlight(string nodeId)
        {
            _executedUntil[nodeId] = Time.unscaledTime + 0.2f;
            SetVerticesDirty();
        }

        /// <summary>지정 노드가 중앙에 보이도록 화면 위치를 조정하고 선택한다.</summary>
        public void FocusNode(string nodeId)
        {
            GraphNode node = FindNode(nodeId);
            if (node == null) return;
            _pan = new Vector2(rectTransform.rect.width / 2 - (node.X + NODE_WIDTH / 2) * _zoom,
                -rectTransform.rect.height / 2 + node.Y * _zoom);
            _selected.Clear();
            _selected.Add(nodeId);
            _selectionChanged(node);
            RefreshGraph();
        }

        /// <summary>현재 선택의 삭제 불가능한 시전 노드를 제외하고 노드와 연결을 삭제한다.</summary>
        public void DeleteSelection()
        {
            foreach (string id in new List<string>(_selected)) _editor.RemoveNode(id);
            _selected.Clear();
            _selectionChanged(null);
            RefreshGraph();
        }

        /// <summary>화면 좌표가 편집 영역 안에 있을 경우 배치할 그래프 좌표를 반환한다.</summary>
        public bool TryGetGraphPoint(Vector2 screen, out Vector2 graphPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, null, out Vector2 point);
            graphPoint = GraphPoint(point);
            return rectTransform.rect.Contains(point);
        }

        /// <summary>현재 보이는 캔버스 안에서 새 룬을 놓을 좌표를 반환한다. 효과는 형태 아래, 속성·형태는 열린 체인 끝 오른쪽을 우선한다.</summary>
        public Vector2 SuggestPlacement(string runeId)
        {
            RuneDefinition rune = GameData.Runes.Get(runeId);
            Vector2 position = GraphPoint(rectTransform.rect.center);
            if (rune.Category == "modifier")
            {
                foreach (GraphNode node in _editor.Graph.Nodes)
                    if (CanReceiveModifier(GameData.Runes.Get(node.RuneId))) position = new Vector2(node.X - 140, node.Y + 90);
            }
            else if (rune.Category == SpellGrammar.CATEGORY_ELEMENT || rune.Category == SpellGrammar.CATEGORY_SHAPE)
            {
                foreach (GraphNode node in _editor.Graph.Nodes)
                    if (HasOpenChainOutput(node)) position = new Vector2(node.X + NODE_WIDTH + 20, node.Y);
            }
            Vector2 local = new Vector2(rectTransform.rect.xMin, rectTransform.rect.yMax) + _pan + new Vector2(position.x, -position.y) * _zoom;
            local.x = Mathf.Clamp(local.x, rectTransform.rect.xMin + 12, rectTransform.rect.xMax - (NODE_WIDTH + 12) * _zoom);
            local.y = Mathf.Clamp(local.y, rectTransform.rect.yMin + 90 * _zoom, rectTransform.rect.yMax - 12);
            return GraphPoint(local);
        }

        /// <summary>
        /// 지정 좌표에 룬을 배치한다. 효과는 가까운 형태·마법 메소드의 효과 입력에,
        /// 속성 부여·형태 블록은 가까운 열린 체인 끝(마법 타입·속성 부여)에 자동으로 연결한다.
        /// </summary>
        public void PlaceRune(string runeId, Vector2 position)
        {
            int oldCount = _editor.Graph.Nodes.Count;
            _editor.AddRune(runeId, position.x, position.y);
            if (_editor.Graph.Nodes.Count == oldCount) return;
            RuneDefinition rune = GameData.Runes.Get(runeId);
            GraphNode placed = _editor.Graph.Nodes[_editor.Graph.Nodes.Count - 1];
            if (rune.Category == "modifier")
            {
                GraphNode target = FindClosest(position, AUTO_CONNECT_DISTANCE,
                    node => CanReceiveModifier(GameData.Runes.Get(node.RuneId)) && _editor.CanConnectPorts(placed.Id, "mod", node.Id, "mod"));
                if (target != null) _editor.Connect(placed.Id, "mod", target.Id, "mod");
            }
            else if (rune.Category == SpellGrammar.CATEGORY_ELEMENT || rune.Category == SpellGrammar.CATEGORY_SHAPE)
            {
                GraphNode source = FindClosest(position, AUTO_CHAIN_DISTANCE, node => node.Id != placed.Id && HasOpenChainOutput(node)
                    && _editor.CanConnectPorts(node.Id, SpellGrammar.CHAIN_OUT, placed.Id, SpellGrammar.CHAIN_IN));
                if (source != null) _editor.Connect(source.Id, SpellGrammar.CHAIN_OUT, placed.Id, SpellGrammar.CHAIN_IN);
            }
        }

        /// <summary>효과를 받을 수 있는 형태 블록 또는 마법 메소드인지 반환한다.</summary>
        private static bool CanReceiveModifier(RuneDefinition rune)
        {
            return rune.Category == SpellGrammar.CATEGORY_SHAPE || rune.Id == SpellGrammar.CALL_RUNE;
        }

        /// <summary>체인 출력 포트가 있고 아직 연결되지 않은 노드인지 반환한다.</summary>
        private bool HasOpenChainOutput(GraphNode node)
        {
            if (GameData.Runes.Get(node.RuneId).FindPort(SpellGrammar.CHAIN_OUT, "out")?.Kind != SpellGrammar.CHAIN_KIND) return false;
            foreach (GraphEdge edge in _editor.Graph.Edges)
                if (edge.FromNode == node.Id && edge.FromPort == SpellGrammar.CHAIN_OUT) return false;
            return true;
        }

        /// <summary>조건을 만족하는 노드 중 기준 좌표에서 최대 거리 안의 가장 가까운 노드를 반환하며 없으면 null을 반환한다.</summary>
        private GraphNode FindClosest(Vector2 position, float maxDistance, Func<GraphNode, bool> predicate)
        {
            GraphNode closest = null;
            float distance = maxDistance;
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                if (!predicate(node)) continue;
                float next = Vector2.Distance(position, new Vector2(node.X, node.Y));
                if (next < distance) { distance = next; closest = node; }
            }
            return closest;
        }

        /// <summary>노드 좌표를 화면 좌표로 변환해 그래프 메시를 구성한다.</summary>
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            RuneMesh.Rect(mesh, bounds, new Color(0.035f, 0.064f, 0.10f));
            float step = 28 * _zoom;
            for (float x = bounds.xMin + _pan.x % step; x < bounds.xMax; x += step)
                for (float y = bounds.yMax + _pan.y % step; y > bounds.yMin; y -= step)
                    RuneMesh.Rect(mesh, new Rect(x, y, 1.5f, 1.5f), new Color(0.16f, 0.24f, 0.31f));
            if (_editor == null || _editor.Graph == null) return;
            RebuildNodeLookup();
            foreach (GraphEdge edge in _editor.Graph.Edges)
            {
                Vector2 from = LookupPortPosition(edge.FromNode, edge.FromPort);
                Vector2 to = LookupPortPosition(edge.ToNode, edge.ToPort);
                Color edgeColor = edge.FromPort == "mod" ? new Color(0.70f, 0.47f, 0.98f)
                    : edge.ToPort == SpellGrammar.CHAIN_IN ? CHAIN_COLOR : new Color(0.2f, 0.70f, 0.82f);
                RuneMesh.Line(mesh, from, to, 2.4f, edgeColor);
                Vector2 direction = (to - from).normalized;
                RuneMesh.Polygon(mesh, Vector2.Lerp(from, to, 0.68f), 4.5f, new Color(0.65f, 0.85f, 0.94f), 3, Mathf.Atan2(direction.y, direction.x));
            }
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                RuneDefinition rune = GameData.Runes.Get(node.RuneId);
                Color accent = RuneMesh.CategoryColor(rune.Category);
                Rect rect = NodeRect(node, rune);
                bool isExecuted = _executedUntil.TryGetValue(node.Id, out float until) && until > Time.unscaledTime;
                RuneMesh.Rect(mesh, new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4),
                    isExecuted ? new Color(1, 0.95f, 0.55f) : _selected.Contains(node.Id) ? accent : new Color(0.18f, 0.26f, 0.34f));
                RuneMesh.Rect(mesh, rect, isExecuted ? new Color(0.18f, 0.22f, 0.18f) : new Color(0.075f, 0.12f, 0.18f));
                RuneMesh.Rect(mesh, new Rect(rect.x, rect.yMax - 4 * _zoom, rect.width, 4 * _zoom), accent);
                int shape = rune.Category == SpellGrammar.CATEGORY_SHAPE ? 4 : rune.Category == SpellGrammar.CATEGORY_ELEMENT ? 3
                    : rune.Category == SpellGrammar.CATEGORY_MAGIC_TYPE ? 5 : rune.Category == "modifier" ? 6 : 20;
                RuneMesh.Polygon(mesh, new Vector2(rect.x + 5 * _zoom, rect.yMax - 17 * _zoom), 3 * _zoom, accent, shape);
                _connectablePorts.TryGetValue(node.Id, out bool[] connectable);
                for (int i = 0; i < rune.Ports.Count; i++)
                {
                    PortDefinition port = rune.Ports[i];
                    Vector2 point = PortPosition(rect, rune, port.Id);
                    Color portColor = port.Kind == "mod" ? new Color(0.76f, 0.52f, 1f)
                        : port.Kind == SpellGrammar.CHAIN_KIND ? CHAIN_COLOR : new Color(0.32f, 0.88f, 0.98f);
                    if (_sourceNode != null)
                        portColor = connectable != null && connectable[i] ? new Color(0.28f, 1, 0.57f) : new Color(1, 0.26f, 0.29f);
                    RuneMesh.Polygon(mesh, point, 5 * _zoom, portColor, port.Kind == "mod" ? 4 : port.Kind == SpellGrammar.CHAIN_KIND ? 6 : 16);
                }
            }
            if (_sourceNode != null) RuneMesh.Line(mesh, LookupPortPosition(_sourceNode, _sourcePort), _pointer, 2.4f, Color.white);
            if (_isBoxSelecting)
            {
                Rect selection = MakeRect(_dragStart, _pointer);
                RuneMesh.Rect(mesh, selection, new Color(0.24f, 0.80f, 1f, 0.12f));
                RuneMesh.Line(mesh, new Vector2(selection.xMin, selection.yMin), new Vector2(selection.xMax, selection.yMin), 1, Color.cyan);
                RuneMesh.Line(mesh, new Vector2(selection.xMin, selection.yMax), new Vector2(selection.xMax, selection.yMax), 1, Color.cyan);
            }
        }

        void Update()
        {
            if (_executedUntil.Count == 0) return;
            _staleIds.Clear();
            foreach (var pair in _executedUntil) if (pair.Value <= Time.unscaledTime) _staleIds.Add(pair.Key);
            if (_staleIds.Count == 0) return;
            foreach (string id in _staleIds) _executedUntil.Remove(id);
            SetVerticesDirty();
        }

        /// <summary>포인터 위치에서 연결, 노드 선택, 화면 이동 또는 영역 선택을 시작한다.</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            _pointer = Local(eventData);
            _dragStart = _pointer;
            _panStart = _pan;
            bool isShift = UnityEngine.InputSystem.Keyboard.current != null &&
                (UnityEngine.InputSystem.Keyboard.current.leftShiftKey.isPressed || UnityEngine.InputSystem.Keyboard.current.rightShiftKey.isPressed);
            if (eventData.button == PointerEventData.InputButton.Middle) { _isPanning = true; return; }
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                RebuildNodeLookup();
                foreach (GraphEdge edge in _editor.Graph.Edges)
                {
                    Vector2 from = LookupPortPosition(edge.FromNode, edge.FromPort);
                    Vector2 to = LookupPortPosition(edge.ToNode, edge.ToPort);
                    Vector2 span = to - from;
                    float t = Mathf.Clamp01(Vector2.Dot(_pointer - from, span) / Mathf.Max(1, span.sqrMagnitude));
                    if (Vector2.Distance(_pointer, from + span * t) < 8) { _editor.RemoveEdge(edge.Id); RefreshGraph(); return; }
                }
                _isPanning = true;
                return;
            }
            if (FindPort(_pointer, out GraphNode portNode, out PortDefinition port))
            {
                _sourceNode = portNode.Id; _sourcePort = port.Id; _sourceKind = port.Kind; _isSourceOutput = port.Direction == "out";
                CacheConnectablePorts();
                SetVerticesDirty(); return;
            }
            GraphNode selected = FindNodeAt(_pointer);
            if (selected != null)
            {
                if (!isShift && !_selected.Contains(selected.Id)) _selected.Clear();
                if (isShift && _selected.Contains(selected.Id)) _selected.Remove(selected.Id);
                else _selected.Add(selected.Id);
                _selectionChanged(selected);
                _isNodeDragging = true;
                _dragOrigins.Clear();
                foreach (string id in _selected)
                {
                    GraphNode node = FindNode(id);
                    if (node != null) _dragOrigins[id] = new Vector2(node.X, node.Y);
                }
                _editor.BeginEdit();
            }
            else
            {
                if (eventData.clickCount >= 2) { _quickPlacement(GraphPoint(_pointer)); return; }
                if (!isShift) _selected.Clear();
                _isBoxSelecting = true;
                _selectionChanged(null);
            }
            SetVerticesDirty();
        }

        /// <summary>포인터 이동에 맞춰 포트 선, 선택 노드 또는 화면 위치를 변경한다.</summary>
        public void OnDrag(PointerEventData eventData)
        {
            _pointer = Local(eventData);
            if (_isPanning) _pan = _panStart + _pointer - _dragStart;
            if (_isNodeDragging)
            {
                Vector2 difference = (_pointer - _dragStart) / _zoom;
                foreach (var pair in _dragOrigins) FindNode(pair.Key)?.Move(pair.Value.x + difference.x, pair.Value.y - difference.y);
            }
            if (_isPanning || _isNodeDragging) LayoutLabels();
            else SetVerticesDirty();
        }

        /// <summary>연결 시도와 영역 선택을 확정하고 그래프 변경을 애플리케이션에 알린다.</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            _pointer = Local(eventData);
            if (_sourceNode != null && FindPort(_pointer, out GraphNode node, out PortDefinition port) && node.Id != _sourceNode)
            {
                if (_isSourceOutput) _editor.Connect(_sourceNode, _sourcePort, node.Id, port.Id);
                else _editor.Connect(node.Id, port.Id, _sourceNode, _sourcePort);
            }
            if (_isBoxSelecting)
            {
                Rect area = MakeRect(_dragStart, _pointer);
                foreach (GraphNode candidate in _editor.Graph.Nodes)
                    if (area.Overlaps(NodeRect(candidate, GameData.Runes.Get(candidate.RuneId)))) _selected.Add(candidate.Id);
            }
            if (_isNodeDragging && Vector2.Distance(_pointer, _dragStart) > 1) _editor.MarkChanged();
            _sourceNode = null; _isPanning = false; _isNodeDragging = false; _isBoxSelecting = false;
            _connectablePorts.Clear();
            RefreshGraph();
        }

        /// <summary>그래프 드래그를 수신하도록 Unity 이벤트 시스템에 알린다.</summary>
        public void OnBeginDrag(PointerEventData eventData) { }

        /// <summary>포인터 해제와 동일한 그래프 드래그 종료 절차를 수행한다.</summary>
        public void OnEndDrag(PointerEventData eventData) { OnPointerUp(eventData); }

        /// <summary>마우스 위치를 중심으로 그래프 확대 배율을 변경한다.</summary>
        public void OnScroll(PointerEventData eventData)
        {
            Vector2 point = Local(eventData) - new Vector2(rectTransform.rect.xMin, rectTransform.rect.yMax);
            float next = Mathf.Clamp(_zoom * (eventData.scrollDelta.y > 0 ? 1.1f : 0.9f), 0.45f, 1.5f);
            _pan = point - (point - _pan) * (next / _zoom);
            _zoom = next;
            LayoutLabels();
        }

        /// <summary>포인터 아래 노드가 바뀐 경우에만 툴팁에 사용할 노드 정보를 전달한다.</summary>
        public void OnPointerMove(PointerEventData eventData)
        {
            GraphNode hovered = FindNodeAt(Local(eventData));
            if (_isHoverValid && hovered == _hoveredNode) return;
            _hoveredNode = hovered;
            _isHoverValid = true;
            _hoverChanged(hovered);
        }

        /// <summary>화면 포인터를 그래프 RectTransform의 로컬 좌표로 변환한다.</summary>
        private Vector2 Local(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 position);
            return position;
        }

        /// <summary>그래프 로컬 포인터에서 확대와 화면 이동을 제거한 노드 좌표를 반환한다.</summary>
        private Vector2 GraphPoint(Vector2 point)
        {
            Vector2 position = (point - new Vector2(rectTransform.rect.xMin, rectTransform.rect.yMax) - _pan) / _zoom;
            return new Vector2(position.x, -position.y);
        }

        /// <summary>그래프 노드 ID와 일치하는 노드를 반환한다.</summary>
        private GraphNode FindNode(string id)
        {
            foreach (GraphNode node in _editor.Graph.Nodes) if (node.Id == id) return node;
            return null;
        }

        /// <summary>그래프 화면 위치에 포함된 가장 앞쪽 노드를 반환한다.</summary>
        private GraphNode FindNodeAt(Vector2 point)
        {
            for (int i = _editor.Graph.Nodes.Count - 1; i >= 0; i--)
            {
                GraphNode node = _editor.Graph.Nodes[i];
                if (NodeRect(node, GameData.Runes.Get(node.RuneId)).Contains(point)) return node;
            }
            return null;
        }

        /// <summary>화면 위치에 가장 가까운 연결 포트를 찾아 노드와 포트 정의를 반환한다.</summary>
        private bool FindPort(Vector2 point, out GraphNode foundNode, out PortDefinition foundPort)
        {
            float radius = Mathf.Max(8, 9 * _zoom);
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                RuneDefinition rune = GameData.Runes.Get(node.RuneId);
                Rect rect = NodeRect(node, rune);
                foreach (PortDefinition port in rune.Ports)
                    if (Vector2.Distance(point, PortPosition(rect, rune, port.Id)) < radius)
                    {
                        foundNode = node; foundPort = port;
                        return true;
                    }
            }
            foundNode = null; foundPort = null; return false;
        }

        /// <summary>
        /// 연결 시작 포트 기준으로 모든 노드 포트의 연결 가능 여부를 계산해 노드 ID별 포트 순번 배열로 저장한다.
        /// 연결 드래그 동안 그래프가 바뀌지 않으므로 메시 재구성마다 규칙 검사를 반복하지 않는다.
        /// </summary>
        private void CacheConnectablePorts()
        {
            _connectablePorts.Clear();
            string oppositeDirection = _isSourceOutput ? "in" : "out";
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                IReadOnlyList<PortDefinition> ports = GameData.Runes.Get(node.RuneId).Ports;
                var connectable = new bool[ports.Count];
                for (int i = 0; i < ports.Count; i++)
                {
                    PortDefinition port = ports[i];
                    if (port.Direction != oppositeDirection) continue;
                    connectable[i] = _isSourceOutput
                        ? _editor.CanConnectPorts(_sourceNode, _sourcePort, node.Id, port.Id)
                        : _editor.CanConnectPorts(node.Id, port.Id, _sourceNode, _sourcePort);
                }
                _connectablePorts[node.Id] = connectable;
            }
        }

        /// <summary>현재 그래프 노드를 ID로 찾는 조회 테이블을 다시 채운다.</summary>
        private void RebuildNodeLookup()
        {
            _nodeLookup.Clear();
            foreach (GraphNode node in _editor.Graph.Nodes) _nodeLookup[node.Id] = node;
        }

        /// <summary>조회 테이블에서 노드를 찾아 포트 중심의 화면 좌표를 반환하며 노드가 없으면 원점을 반환한다. RebuildNodeLookup 이후 호출한다.</summary>
        private Vector2 LookupPortPosition(string nodeId, string portId)
        {
            if (!_nodeLookup.TryGetValue(nodeId, out GraphNode node)) return Vector2.zero;
            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            return PortPosition(NodeRect(node, rune), rune, portId);
        }

        /// <summary>룬 포트 수로 계산한 배율 적용 전 노드 높이를 반환한다.</summary>
        private static float NodeHeight(RuneDefinition rune)
        {
            return 39 + Math.Max(CountPorts(rune, "in"), CountPorts(rune, "out")) * PORT_SPACING;
        }

        /// <summary>룬 포트 수와 배율을 반영한 화면상의 노드 사각형을 반환한다.</summary>
        private Rect NodeRect(GraphNode node, RuneDefinition rune)
        {
            float height = NodeHeight(rune);
            float x = rectTransform.rect.xMin + _pan.x + node.X * _zoom;
            float y = rectTransform.rect.yMax + _pan.y - node.Y * _zoom;
            return new Rect(x, y - height * _zoom, NODE_WIDTH * _zoom, height * _zoom);
        }

        /// <summary>노드 화면 사각형과 룬 정의에서 포트 ID에 대응하는 포트 중심의 화면 좌표를 반환한다.</summary>
        private Vector2 PortPosition(Rect rect, RuneDefinition rune, string portId)
        {
            foreach (PortDefinition port in rune.Ports)
                if (port.Id == portId)
                {
                    int row = 0;
                    foreach (PortDefinition other in rune.Ports)
                    {
                        if (other.Id == portId) break;
                        if (other.Direction == port.Direction) row++;
                    }
                    return new Vector2(port.Direction == "in" ? rect.xMin : rect.xMax, rect.yMax - (40 + row * PORT_SPACING) * _zoom);
                }
            return rect.center;
        }

        /// <summary>지정 방향에 속한 룬 포트 개수를 반환한다.</summary>
        private static int CountPorts(RuneDefinition rune, string direction)
        {
            int count = 0;
            foreach (PortDefinition port in rune.Ports) if (port.Direction == direction) count++;
            return count;
        }

        /// <summary>지정 방향의 순번에 해당하는 포트를 반환한다.</summary>
        private static PortDefinition PortAt(RuneDefinition rune, string direction, int row)
        {
            foreach (PortDefinition port in rune.Ports) if (port.Direction == direction && row-- == 0) return port;
            return null;
        }

        /// <summary>두 점을 포함하는 선택 영역 사각형을 반환한다.</summary>
        private static Rect MakeRect(Vector2 first, Vector2 second)
        { return Rect.MinMaxRect(Mathf.Min(first.x, second.x), Mathf.Min(first.y, second.y), Mathf.Max(first.x, second.x), Mathf.Max(first.y, second.y)); }
    }
}
