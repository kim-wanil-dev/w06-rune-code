using System;
using System.Collections.Generic;

using UnityEngine;

using TMPro;
using UnityEngine.EventSystems;

namespace RuneCode
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RuneGraphCanvas : UnityEngine.UI.MaskableGraphic, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IBeginDragHandler, IEndDragHandler, IScrollHandler, IPointerMoveHandler
    {
        private const float NODE_WIDTH = 160f;
        private const float PORT_SPACING = 23f;

        private readonly HashSet<string> _selected = new HashSet<string>();
        private readonly Dictionary<string, TextMeshProUGUI> _labels = new Dictionary<string, TextMeshProUGUI>();
        private readonly Dictionary<string, Vector2> _dragOrigins = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, float> _executedUntil = new Dictionary<string, float>();
        private RuneCodeApp _app;
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
        private Action<GraphNode> _selectionChanged;
        private Action<GraphNode> _hoverChanged;
        private Action<Vector2> _quickPlacement;
        public IReadOnlyCollection<string> SelectedNodes => _selected;

        /// <summary>애플리케이션, 글꼴과 선택 콜백을 연결하고 그래프를 표시한다.</summary>
        public void Initialize(RuneCodeApp app, TMP_FontAsset font, Action<GraphNode> selectionChanged, Action<GraphNode> hoverChanged, Action<Vector2> quickPlacement)
        {
            _app = app;
            _font = font;
            _selectionChanged = selectionChanged;
            _hoverChanged = hoverChanged;
            _quickPlacement = quickPlacement;
            raycastTarget = true;
            FitGraph();
            RefreshGraph();
        }

        /// <summary>첫 표시 때 모든 노드가 편집 영역 안에 보이도록 화면 위치와 배율을 맞춘다.</summary>
        private void FitGraph()
        {
            if (_app.EditingGraph == null || _app.EditingGraph.Nodes.Count == 0) return;
            float minimumX = float.MaxValue;
            float minimumY = float.MaxValue;
            float maximumX = float.MinValue;
            float maximumY = float.MinValue;
            foreach (GraphNode node in _app.EditingGraph.Nodes)
            {
                RuneDefinition rune = GameData.Runes.Get(node.RuneId);
                float height = 39 + Math.Max(CountPorts(rune, "in"), CountPorts(rune, "out")) * PORT_SPACING;
                minimumX = Mathf.Min(minimumX, node.X); minimumY = Mathf.Min(minimumY, node.Y);
                maximumX = Mathf.Max(maximumX, node.X + NODE_WIDTH); maximumY = Mathf.Max(maximumY, node.Y + height);
            }
            _zoom = Mathf.Clamp(Mathf.Min((rectTransform.rect.width - 48) / Mathf.Max(1, maximumX - minimumX),
                (rectTransform.rect.height - 48) / Mathf.Max(1, maximumY - minimumY)), 0.45f, 1);
            _pan = new Vector2(24 - minimumX * _zoom, -24 + minimumY * _zoom);
        }

        /// <summary>현재 그래프의 노드 라벨과 메시를 갱신하고 사라진 노드의 선택을 해제한다.</summary>
        public void RefreshGraph()
        {
            if (_app == null || _app.EditingGraph == null) return;
            var existing = new HashSet<string>();
            foreach (GraphNode node in _app.EditingGraph.Nodes)
            {
                existing.Add(node.Id);
                if (!_labels.TryGetValue(node.Id, out TextMeshProUGUI label))
                {
                    var target = new GameObject("NodeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                    target.transform.SetParent(transform, false);
                    label = target.GetComponent<TextMeshProUGUI>();
                    label.font = _font;
                    label.fontSize = 13;
                    label.raycastTarget = false;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.overflowMode = TextOverflowModes.Overflow;
                    _labels.Add(node.Id, label);
                }
                RuneDefinition rune = GameData.Runes.Get(node.RuneId);
                int rows = Math.Max(CountPorts(rune, "in"), CountPorts(rune, "out"));
                Rect rect = NodeRect(node, rune);
                RectTransform textRect = label.rectTransform;
                textRect.anchorMin = textRect.anchorMax = new Vector2(0, 1);
                textRect.pivot = new Vector2(0, 1);
                textRect.anchoredPosition = new Vector2(rect.x + 14 * _zoom, rect.yMax - rectTransform.rect.yMax - 10 * _zoom);
                textRect.sizeDelta = new Vector2((NODE_WIDTH - 28) * _zoom, rect.height - 14 * _zoom);
                label.fontSize = 12 * _zoom;
                string text = "<b>" + rune.Name + "</b>  <color=#8EACC6>" + rune.Ram + " RAM</color>\n";
                for (int row = 0; row < rows; row++)
                {
                    PortDefinition input = PortAt(rune, "in", row);
                    PortDefinition output = PortAt(rune, "out", row);
                    text += "<size=10>" + (input == null ? "" : input.Id) +
                        "<pos=75>" + (output == null ? "" : output.Id) + "</size>\n";
                }
                label.text = text;
                label.color = Color.white;
            }
            var remove = new List<string>();
            foreach (var pair in _labels) if (!existing.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (string id in remove) { Destroy(_labels[id].gameObject); _labels.Remove(id); }
            _selected.RemoveWhere(id => !existing.Contains(id));
            SetVerticesDirty();
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
            foreach (string id in new List<string>(_selected)) _app.RemoveNode(id);
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

        /// <summary>현재 보이는 캔버스 안에서 새 룬을 놓을 좌표를 반환하며 수식은 형태 근처를 우선한다.</summary>
        public Vector2 SuggestPlacement(string runeId)
        {
            RuneDefinition rune = GameData.Runes.Get(runeId);
            Vector2 position = GraphPoint(rectTransform.rect.center);
            if (rune.Category == "modifier" || rune.Category == "element")
            {
                foreach (GraphNode node in _app.EditingGraph.Nodes)
                    if (GameData.Runes.Get(node.RuneId).Category == "form") position = new Vector2(node.X - 140, node.Y + 90);
            }
            Vector2 local = new Vector2(rectTransform.rect.xMin, rectTransform.rect.yMax) + _pan + new Vector2(position.x, -position.y) * _zoom;
            local.x = Mathf.Clamp(local.x, rectTransform.rect.xMin + 12, rectTransform.rect.xMax - (NODE_WIDTH + 12) * _zoom);
            local.y = Mathf.Clamp(local.y, rectTransform.rect.yMin + 90 * _zoom, rectTransform.rect.yMax - 12);
            return GraphPoint(local);
        }

        /// <summary>지정 좌표에 룬을 배치하고 가까운 형태에 속성이나 수식을 자동으로 연결한다.</summary>
        public void PlaceRune(string runeId, Vector2 position)
        {
            int oldCount = _app.EditingGraph.Nodes.Count;
            _app.AddRune(runeId, position.x, position.y);
            if (_app.EditingGraph.Nodes.Count == oldCount) return;
            RuneDefinition rune = GameData.Runes.Get(runeId);
            if (rune.Category != "element" && rune.Category != "modifier") return;
            GraphNode source = _app.EditingGraph.Nodes[_app.EditingGraph.Nodes.Count - 1];
            GraphNode closest = null;
            float distance = 175;
            foreach (GraphNode target in _app.EditingGraph.Nodes)
            {
                if (GameData.Runes.Get(target.RuneId).Category != "form") continue;
                float next = Vector2.Distance(position, new Vector2(target.X, target.Y));
                if (next < distance) { distance = next; closest = target; }
            }
            if (closest != null) _app.Connect(source.Id, "mod", closest.Id, "mod");
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
            if (_app == null || _app.EditingGraph == null) return;
            foreach (GraphEdge edge in _app.EditingGraph.Edges)
            {
                Vector2 from = PortPosition(edge.FromNode, edge.FromPort);
                Vector2 to = PortPosition(edge.ToNode, edge.ToPort);
                RuneMesh.Line(mesh, from, to, 2.4f, edge.FromPort == "mod" ? new Color(0.70f, 0.47f, 0.98f) : new Color(0.2f, 0.70f, 0.82f));
                Vector2 direction = (to - from).normalized;
                RuneMesh.Polygon(mesh, Vector2.Lerp(from, to, 0.68f), 4.5f, new Color(0.65f, 0.85f, 0.94f), 3, Mathf.Atan2(direction.y, direction.x));
            }
            foreach (GraphNode node in _app.EditingGraph.Nodes)
            {
                RuneDefinition rune = GameData.Runes.Get(node.RuneId);
                Color accent = RuneMesh.CategoryColor(rune.Category);
                Rect rect = NodeRect(node, rune);
                bool isExecuted = _executedUntil.TryGetValue(node.Id, out float until) && until > Time.unscaledTime;
                RuneMesh.Rect(mesh, new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4),
                    isExecuted ? new Color(1, 0.95f, 0.55f) : _selected.Contains(node.Id) ? accent : new Color(0.18f, 0.26f, 0.34f));
                RuneMesh.Rect(mesh, rect, isExecuted ? new Color(0.18f, 0.22f, 0.18f) : new Color(0.075f, 0.12f, 0.18f));
                RuneMesh.Rect(mesh, new Rect(rect.x, rect.yMax - 4 * _zoom, rect.width, 4 * _zoom), accent);
                int shape = rune.Category == "form" ? 4 : rune.Category == "element" ? 3 : rune.Category == "modifier" ? 6 : 20;
                RuneMesh.Polygon(mesh, new Vector2(rect.x + 5 * _zoom, rect.yMax - 17 * _zoom), 3 * _zoom, accent, shape);
                foreach (PortDefinition port in rune.Ports)
                {
                    Vector2 point = PortPosition(node.Id, port.Id);
                    Color portColor = port.Kind == "mod" ? new Color(0.76f, 0.52f, 1f) : new Color(0.32f, 0.88f, 0.98f);
                    if (_sourceNode != null && port.Direction != (_isSourceOutput ? "out" : "in"))
                        portColor = port.Kind == _sourceKind ? new Color(0.28f, 1, 0.57f) : new Color(1, 0.26f, 0.29f);
                    RuneMesh.Polygon(mesh, point, 5 * _zoom, portColor, port.Kind == "mod" ? 4 : 16);
                }
            }
            if (_sourceNode != null) RuneMesh.Line(mesh, PortPosition(_sourceNode, _sourcePort), _pointer, 2.4f, Color.white);
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
            if (_executedUntil.Count > 0) SetVerticesDirty();
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
                foreach (GraphEdge edge in _app.EditingGraph.Edges)
                {
                    Vector2 from = PortPosition(edge.FromNode, edge.FromPort);
                    Vector2 to = PortPosition(edge.ToNode, edge.ToPort);
                    Vector2 span = to - from;
                    float t = Mathf.Clamp01(Vector2.Dot(_pointer - from, span) / Mathf.Max(1, span.sqrMagnitude));
                    if (Vector2.Distance(_pointer, from + span * t) < 8) { _app.RemoveEdge(edge.Id); RefreshGraph(); return; }
                }
                _isPanning = true;
                return;
            }
            if (FindPort(_pointer, out GraphNode portNode, out PortDefinition port))
            {
                _sourceNode = portNode.Id; _sourcePort = port.Id; _sourceKind = port.Kind; _isSourceOutput = port.Direction == "out";
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
                _app.BeginGraphEdit();
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
            if (_isPanning) { _pan = _panStart + _pointer - _dragStart; RefreshGraph(); }
            if (_isNodeDragging)
            {
                Vector2 difference = (_pointer - _dragStart) / _zoom;
                foreach (var pair in _dragOrigins) FindNode(pair.Key)?.Move(pair.Value.x + difference.x, pair.Value.y - difference.y);
                RefreshGraph();
            }
            SetVerticesDirty();
        }

        /// <summary>연결 시도와 영역 선택을 확정하고 그래프 변경을 애플리케이션에 알린다.</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            _pointer = Local(eventData);
            if (_sourceNode != null && FindPort(_pointer, out GraphNode node, out PortDefinition port) && node.Id != _sourceNode)
            {
                if (_isSourceOutput) _app.Connect(_sourceNode, _sourcePort, node.Id, port.Id);
                else _app.Connect(node.Id, port.Id, _sourceNode, _sourcePort);
            }
            if (_isBoxSelecting)
            {
                Rect area = MakeRect(_dragStart, _pointer);
                foreach (GraphNode candidate in _app.EditingGraph.Nodes)
                    if (area.Overlaps(NodeRect(candidate, GameData.Runes.Get(candidate.RuneId)))) _selected.Add(candidate.Id);
            }
            if (_isNodeDragging && Vector2.Distance(_pointer, _dragStart) > 1) _app.ChangedGraph();
            _sourceNode = null; _isPanning = false; _isNodeDragging = false; _isBoxSelecting = false;
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
            RefreshGraph();
        }

        /// <summary>포인터 아래 노드의 툴팁에 사용할 선택 정보를 전달한다.</summary>
        public void OnPointerMove(PointerEventData eventData) { _hoverChanged(FindNodeAt(Local(eventData))); }

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
            foreach (GraphNode node in _app.EditingGraph.Nodes) if (node.Id == id) return node;
            return null;
        }

        /// <summary>그래프 화면 위치에 포함된 가장 앞쪽 노드를 반환한다.</summary>
        private GraphNode FindNodeAt(Vector2 point)
        {
            for (int i = _app.EditingGraph.Nodes.Count - 1; i >= 0; i--)
            {
                GraphNode node = _app.EditingGraph.Nodes[i];
                if (NodeRect(node, GameData.Runes.Get(node.RuneId)).Contains(point)) return node;
            }
            return null;
        }

        /// <summary>화면 위치에 가장 가까운 연결 포트를 찾아 노드와 포트 정의를 반환한다.</summary>
        private bool FindPort(Vector2 point, out GraphNode foundNode, out PortDefinition foundPort)
        {
            foreach (GraphNode node in _app.EditingGraph.Nodes)
                foreach (PortDefinition port in GameData.Runes.Get(node.RuneId).Ports)
                    if (Vector2.Distance(point, PortPosition(node.Id, port.Id)) < Mathf.Max(8, 9 * _zoom))
                    { foundNode = node; foundPort = port; return true; }
            foundNode = null; foundPort = null; return false;
        }

        /// <summary>룬 포트 수와 배율을 반영한 화면상의 노드 사각형을 반환한다.</summary>
        private Rect NodeRect(GraphNode node, RuneDefinition rune)
        {
            float height = 39 + Math.Max(CountPorts(rune, "in"), CountPorts(rune, "out")) * PORT_SPACING;
            float x = rectTransform.rect.xMin + _pan.x + node.X * _zoom;
            float y = rectTransform.rect.yMax + _pan.y - node.Y * _zoom;
            return new Rect(x, y - height * _zoom, NODE_WIDTH * _zoom, height * _zoom);
        }

        /// <summary>노드 ID와 포트 ID에 대응하는 포트 중심의 화면 좌표를 반환한다.</summary>
        private Vector2 PortPosition(string nodeId, string portId)
        {
            GraphNode node = FindNode(nodeId);
            if (node == null) return Vector2.zero;
            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            Rect rect = NodeRect(node, rune);
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
