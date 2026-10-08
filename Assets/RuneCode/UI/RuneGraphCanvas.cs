using System;
using System.Collections.Generic;
using System.Globalization;

using UnityEngine;

using TMPro;
using UnityEngine.EventSystems;

namespace RuneCode
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RuneGraphCanvas : UnityEngine.UI.MaskableGraphic, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IBeginDragHandler, IEndDragHandler, IScrollHandler, IPointerMoveHandler
    {
        private const int CURVE_SEGMENTS = 16;
        private const int FALSE_BRANCH_PORT = 1;
        private const float CURVE_ARROW_T = 0.68f;
        private const float EDGE_WIDTH = 2.4f;
        private const float EDGE_HIT_DISTANCE = 8f;
        private const float ARROW_RADIUS = 4.5f;
        private const float PORT_RADIUS = 5f;
        private const float PORT_HIT_MIN = 8f;
        private const float PORT_HIT_SCALE = 9f;
        private const float GRID_STEP = 28f;
        private const float NODE_PADDING = 12f;
        private const float NODE_TOP_PADDING = 6f;
        private const float CAPTION_GAP = 10f;
        private const float CAPTION_WIDTH = 60f;
        private const float CAPTION_HEIGHT = 16f;
        private const float HEADER_FONT_SIZE = 12f;
        private const float SUB_FONT_SIZE = 10f;
        private const float PERCENT_TOTAL = 100f;
        private const float HIGHLIGHT_SECONDS = 0.2f;
        private const float MIN_ZOOM = 0.45f;
        private const float MAX_ZOOM = 1.5f;
        private const float FIT_MARGIN = 48f;
        private const float DRAG_LINE_WIDTH = 2.4f;
        private const string PORT_LABEL_SUFFIX = "/out";
        private const string COST_COLOR_HEX = "#8EACC6";
        private const string NUMBER_FORMAT_ONE_DECIMAL = "0.#";
        private const string NUMBER_FORMAT_RATIO = "0.0#";
        private const string NUMBER_FORMAT_INTEGER = "0";
        private const string PORT_TRUE_KEY = "port.true";
        private const string PORT_FALSE_KEY = "port.false";
        private const string GREATER_OR_EQUAL = "≥";
        private const string LESS_OR_EQUAL = "≤";

        private static readonly Color FLOW_EDGE_COLOR = new Color(0.2f, 0.70f, 0.82f);
        private static readonly Color FALSE_EDGE_COLOR = new Color(1f, 0.62f, 0.3f);
        private static readonly Color ARROW_COLOR = new Color(0.65f, 0.85f, 0.94f);
        private static readonly Color PORT_COLOR = new Color(0.32f, 0.88f, 0.98f);
        private static readonly Color PORT_LINK_COLOR = new Color(0.28f, 1f, 0.57f);
        private static readonly Color PORT_BLOCK_COLOR = new Color(1f, 0.26f, 0.29f);
        private static readonly Color NODE_BODY_COLOR = new Color(0.075f, 0.12f, 0.18f);
        private static readonly Color UNREACHABLE_BODY_COLOR = new Color(0.04f, 0.05f, 0.065f);
        private static readonly Color UNREACHABLE_ACCENT_COLOR = new Color(0.32f, 0.36f, 0.40f);
        private static readonly Color UNREACHABLE_LABEL_COLOR = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color CAPTION_COLOR = new Color(0.82f, 0.88f, 0.94f);

        private readonly HashSet<string> _selected = new HashSet<string>();
        private readonly Dictionary<string, TextMeshProUGUI> _labels = new Dictionary<string, TextMeshProUGUI>();
        private readonly Dictionary<string, Vector2> _dragOrigins = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, float> _executedUntil = new Dictionary<string, float>();
        private readonly Vector2[] _curvePoints = new Vector2[CURVE_SEGMENTS + 1];
        private RuneCodeApp _app;
        private TMP_FontAsset _font;
        private SpellFlowLayer _flowLayer;
        private Vector2 _pan = new Vector2(30, -35);
        private Vector2 _pointer;
        private Vector2 _dragStart;
        private Vector2 _panStart;
        private float _zoom = 1;
        private bool _isPanning;
        private bool _isBoxSelecting;
        private bool _isNodeDragging;
        private string _sourceNode;
        private int _sourceIndex;
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
            CreateFlowLayer();
            FitGraph();
            RefreshGraph();
        }

        /// <summary>부모와 같은 크기의 SpellFlowLayer 자식을 만들어 시험 도크 실행과 편집 그래프를 연결한다. 레이어는 입력을 받지 않는다.</summary>
        private void CreateFlowLayer()
        {
            var target = new GameObject("SpellFlowLayer", typeof(RectTransform));
            target.transform.SetParent(transform, false);
            _flowLayer = target.AddComponent<SpellFlowLayer>();
            RectTransform layerRect = _flowLayer.rectTransform;
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = Vector2.zero;
            layerRect.offsetMax = Vector2.zero;
            layerRect.pivot = rectTransform.pivot;
            _flowLayer.Initialize(_font, () => _app.Dock, () => _app.EditingGraph);
        }

        /// <summary>흐름 레이어에 그래프 원점의 캔버스 로컬 좌표와 배율을 전달한다. 레이어가 아직 없으면 아무것도 하지 않는다.</summary>
        private void SyncFlowLayer()
        {
            if (_flowLayer == null) return;
            _flowLayer.SetView(new Vector2(rectTransform.rect.xMin + _pan.x, rectTransform.rect.yMax + _pan.y), _zoom);
        }

        /// <summary>종류를 알 수 있는 노드가 모두 편집 영역 안에 보이도록 화면 위치와 배율을 맞춘다.</summary>
        private void FitGraph()
        {
            if (_app.EditingGraph == null || _app.EditingGraph.Nodes.Count == 0) return;
            float minimumX = float.MaxValue;
            float minimumY = float.MaxValue;
            float maximumX = float.MinValue;
            float maximumY = float.MinValue;
            bool hasNode = false;
            foreach (GraphNode node in _app.EditingGraph.Nodes)
            {
                if (!GraphLayout.TryGetKind(node, out SpellNodeKind kind)) continue;
                Vector2 size = GraphLayout.GetNodeSize(kind);
                hasNode = true;
                minimumX = Mathf.Min(minimumX, node.X); minimumY = Mathf.Min(minimumY, node.Y);
                maximumX = Mathf.Max(maximumX, node.X + size.x); maximumY = Mathf.Max(maximumY, node.Y + size.y);
            }
            if (!hasNode) return;
            _zoom = Mathf.Clamp(Mathf.Min((rectTransform.rect.width - FIT_MARGIN) / Mathf.Max(1, maximumX - minimumX),
                (rectTransform.rect.height - FIT_MARGIN) / Mathf.Max(1, maximumY - minimumY)), MIN_ZOOM, 1);
            _pan = new Vector2(24 - minimumX * _zoom, -24 + minimumY * _zoom);
            SyncFlowLayer();
        }

        /// <summary>현재 그래프의 노드 라벨과 메시를 갱신하고 사라진 노드의 라벨과 선택을 정리한다. 종류를 알 수 없는 노드는 그리지 않는다.</summary>
        public void RefreshGraph()
        {
            if (_app == null || _app.EditingGraph == null) return;
            var existingNodes = new HashSet<string>();
            var activeKeys = new HashSet<string>();
            foreach (GraphNode node in _app.EditingGraph.Nodes)
            {
                if (!GraphLayout.TryGetKind(node, out SpellNodeKind kind)) continue;
                existingNodes.Add(node.Id);
                UpdateNodeLabels(node, kind, NodeRect(node, kind), activeKeys);
            }
            var remove = new List<string>();
            foreach (var pair in _labels) if (!activeKeys.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (string key in remove) { Destroy(_labels[key].gameObject); _labels.Remove(key); }
            _selected.RemoveWhere(id => !existingNodes.Contains(id));
            SyncFlowLayer();
            _flowLayer.transform.SetAsLastSibling();
            SetVerticesDirty();
        }

        /// <summary>지정 노드를 실행 하이라이트 상태로 200밀리초 표시한다.</summary>
        public void Highlight(string nodeId)
        {
            _executedUntil[nodeId] = Time.unscaledTime + HIGHLIGHT_SECONDS;
            SetVerticesDirty();
        }

        /// <summary>지정 노드가 중앙에 보이도록 화면 위치를 조정하고 선택한다.</summary>
        public void FocusNode(string nodeId)
        {
            GraphNode node = FindNode(nodeId);
            if (node == null) return;
            _pan = new Vector2(rectTransform.rect.width / 2 - (node.X + GraphLayout.NODE_WIDTH / 2) * _zoom,
                -rectTransform.rect.height / 2 + node.Y * _zoom);
            _selected.Clear();
            _selected.Add(nodeId);
            _selectionChanged(node);
            RefreshGraph();
        }

        /// <summary>현재 선택의 노드와 연결을 삭제한다. 시전 노드는 애플리케이션이 삭제를 거부한다.</summary>
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

        /// <summary>현재 보이는 캔버스 중앙을 기준으로 새 노드를 놓을 그래프 좌표를 반환하며 캔버스 안에 머물도록 클램프한다. 룬 ID는 쓰지 않는다.</summary>
        public Vector2 SuggestPlacement(string runeId)
        {
            Vector2 position = GraphPoint(rectTransform.rect.center);
            Vector2 local = new Vector2(rectTransform.rect.xMin, rectTransform.rect.yMax) + _pan + new Vector2(position.x, -position.y) * _zoom;
            local.x = Mathf.Clamp(local.x, rectTransform.rect.xMin + 12, rectTransform.rect.xMax - (GraphLayout.NODE_WIDTH + 12) * _zoom);
            local.y = Mathf.Clamp(local.y, rectTransform.rect.yMin + 90 * _zoom, rectTransform.rect.yMax - 12);
            return GraphPoint(local);
        }

        /// <summary>지정 그래프 좌표에 해당 종류의 노드를 추가한다. 연결이나 자동 부착은 하지 않는다.</summary>
        public void PlaceRune(string runeId, Vector2 position)
        {
            _app.AddRune(runeId, position.x, position.y);
        }

        /// <summary>노드 사각형, 포트, 엣지 곡선을 캔버스 메시로 구성한다.</summary>
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            SyncFlowLayer();
            Rect bounds = rectTransform.rect;
            RuneMesh.Rect(mesh, bounds, new Color(0.035f, 0.064f, 0.10f));
            float step = GRID_STEP * _zoom;
            for (float x = bounds.xMin + _pan.x % step; x < bounds.xMax; x += step)
                for (float y = bounds.yMax + _pan.y % step; y > bounds.yMin; y -= step)
                    RuneMesh.Rect(mesh, new Rect(x, y, 1.5f, 1.5f), new Color(0.16f, 0.24f, 0.31f));
            if (_app == null || _app.EditingGraph == null) return;
            foreach (GraphEdge edge in _app.EditingGraph.Edges) DrawEdge(mesh, edge);
            foreach (GraphNode node in _app.EditingGraph.Nodes) DrawNode(mesh, node);
            if (_sourceNode != null && TryGetPortPosition(_sourceNode, _isSourceOutput, _sourceIndex, out Vector2 start, out _))
                RuneMesh.Line(mesh, start, _pointer, DRAG_LINE_WIDTH, Color.white);
            if (_isBoxSelecting)
            {
                Rect selection = MakeRect(_dragStart, _pointer);
                RuneMesh.Rect(mesh, selection, new Color(0.24f, 0.80f, 1f, 0.12f));
                RuneMesh.Line(mesh, new Vector2(selection.xMin, selection.yMin), new Vector2(selection.xMax, selection.yMin), 1, Color.cyan);
                RuneMesh.Line(mesh, new Vector2(selection.xMin, selection.yMax), new Vector2(selection.xMax, selection.yMax), 1, Color.cyan);
            }
        }

        /// <summary>실행 하이라이트가 남아 있는 동안 매 프레임 메시를 다시 그린다.</summary>
        void Update()
        {
            if (_executedUntil.Count > 0) SetVerticesDirty();
        }

        /// <summary>포인터 위치에서 포트 연결 시작, 엣지 삭제, 노드 선택, 화면 이동 또는 영역 선택을 시작한다.</summary>
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
                    if (!TryGetCurveEnds(edge, out Vector2 from, out Vector2 to, out bool isLoop, out float lift)) continue;
                    FillCurvePoints(_curvePoints, from, to, isLoop, lift);
                    for (int index = 0; index < CURVE_SEGMENTS; index++)
                    {
                        if (GetSegmentDistance(_pointer, _curvePoints[index], _curvePoints[index + 1]) < EDGE_HIT_DISTANCE)
                        {
                            _app.RemoveEdge(edge.Id);
                            RefreshGraph();
                            return;
                        }
                    }
                }
                _isPanning = true;
                return;
            }
            if (FindPort(_pointer, out GraphNode portNode, out bool isOutput, out int portIndex))
            {
                _sourceNode = portNode.Id; _sourceIndex = portIndex; _isSourceOutput = isOutput;
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
            if (_sourceNode != null && FindPort(_pointer, out GraphNode targetNode, out bool targetIsOutput, out int targetIndex)
                && targetIsOutput != _isSourceOutput)
                ConnectDraggedPort(targetNode.Id, targetIndex);
            if (_isBoxSelecting)
            {
                Rect area = MakeRect(_dragStart, _pointer);
                foreach (GraphNode candidate in _app.EditingGraph.Nodes)
                    if (GraphLayout.TryGetKind(candidate, out SpellNodeKind kind) && area.Overlaps(NodeRect(candidate, kind))) _selected.Add(candidate.Id);
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
            float next = Mathf.Clamp(_zoom * (eventData.scrollDelta.y > 0 ? 1.1f : 0.9f), MIN_ZOOM, MAX_ZOOM);
            _pan = point - (point - _pan) * (next / _zoom);
            _zoom = next;
            RefreshGraph();
        }

        /// <summary>포인터 아래 노드의 툴팁에 사용할 선택 정보를 전달한다.</summary>
        public void OnPointerMove(PointerEventData eventData) { _hoverChanged(FindNodeAt(Local(eventData))); }

        /// <summary>엣지 곡선 위 비율 t(0에서 1 사이)의 캔버스 로컬 좌표를 구한다. 엣지의 포트를 찾지 못하면 false를 반환한다.</summary>
        public bool TryGetEdgePoint(GraphEdge edge, float t, out Vector2 point)
        {
            point = Vector2.zero;
            if (edge == null || !TryGetCurveEnds(edge, out Vector2 from, out Vector2 to, out bool isLoop, out float lift)) return false;
            point = GraphLayout.GetEdgePoint(from, to, isLoop, lift, _zoom, Mathf.Clamp01(t));
            return true;
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
            foreach (GraphNode node in _app.EditingGraph.Nodes) if (node.Id == id) return node;
            return null;
        }

        /// <summary>그래프 화면 위치에 포함된 가장 앞쪽 노드를 반환한다. 종류를 알 수 없는 노드는 건너뛴다.</summary>
        private GraphNode FindNodeAt(Vector2 point)
        {
            for (int i = _app.EditingGraph.Nodes.Count - 1; i >= 0; i--)
            {
                GraphNode node = _app.EditingGraph.Nodes[i];
                if (GraphLayout.TryGetKind(node, out SpellNodeKind kind) && NodeRect(node, kind).Contains(point)) return node;
            }
            return null;
        }

        /// <summary>화면 위치에 가까운 포트를 찾아 노드, 출력 여부, 포트 번호를 반환한다. 종류를 알 수 없는 노드는 건너뛴다.</summary>
        private bool FindPort(Vector2 point, out GraphNode node, out bool isOutput, out int index)
        {
            float radius = Mathf.Max(PORT_HIT_MIN, PORT_HIT_SCALE * _zoom);
            foreach (GraphNode candidate in _app.EditingGraph.Nodes)
            {
                if (!GraphLayout.TryGetKind(candidate, out SpellNodeKind kind)) continue;
                Rect rect = NodeRect(candidate, kind);
                for (int side = 0; side < 2; side++)
                {
                    bool isOutputSide = side == 1;
                    int count = isOutputSide ? SpellNodes.GetOutputCount(kind) : SpellNodes.GetInputCount(kind);
                    for (int slot = 0; slot < count; slot++)
                    {
                        if (Vector2.Distance(point, PortLocal(rect, kind, isOutputSide, slot)) < radius)
                        {
                            node = candidate;
                            isOutput = isOutputSide;
                            index = slot;
                            return true;
                        }
                    }
                }
            }
            node = null; isOutput = false; index = 0;
            return false;
        }

        /// <summary>드래그 출처 포트와 도착 포트를 출력 쪽이 앞이 되도록 정규화해 애플리케이션 연결 명령을 호출한다. 같은 노드끼리의 연결도 허용한다.</summary>
        private void ConnectDraggedPort(string targetNode, int targetIndex)
        {
            string outputNode = _isSourceOutput ? _sourceNode : targetNode;
            int outputIndex = _isSourceOutput ? _sourceIndex : targetIndex;
            string inputNode = _isSourceOutput ? targetNode : _sourceNode;
            int inputIndex = _isSourceOutput ? targetIndex : _sourceIndex;
            _app.Connect(outputNode, outputIndex.ToString(CultureInfo.InvariantCulture),
                inputNode, inputIndex.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>노드 종류의 그래프 단위 크기와 화면 배율로 노드의 화면 사각형을 반환한다.</summary>
        private Rect NodeRect(GraphNode node, SpellNodeKind kind)
        {
            Vector2 size = GraphLayout.GetNodeSize(kind);
            float x = rectTransform.rect.xMin + _pan.x + node.X * _zoom;
            float y = rectTransform.rect.yMax + _pan.y - node.Y * _zoom;
            return new Rect(x, y - size.y * _zoom, size.x * _zoom, size.y * _zoom);
        }

        /// <summary>노드 화면 사각형과 포트 오프셋으로 포트 중심의 캔버스 로컬 좌표를 반환한다.</summary>
        private Vector2 PortLocal(Rect nodeRect, SpellNodeKind kind, bool isOutput, int index)
        {
            Vector2 offset = GraphLayout.GetPortOffset(kind, isOutput, index);
            return new Vector2(nodeRect.xMin + offset.x * _zoom, nodeRect.yMax - offset.y * _zoom);
        }

        /// <summary>노드 ID, 출력 여부, 포트 번호로 포트 중심의 캔버스 로컬 좌표와 노드 종류를 구한다. 노드가 없거나 번호가 범위를 벗어나면 false를 반환한다.</summary>
        private bool TryGetPortPosition(string nodeId, bool isOutput, int index, out Vector2 position, out SpellNodeKind kind)
        {
            position = Vector2.zero;
            kind = SpellNodeKind.Cast;
            GraphNode node = FindNode(nodeId);
            if (node == null || !GraphLayout.TryGetKind(node, out kind)) return false;
            int count = isOutput ? SpellNodes.GetOutputCount(kind) : SpellNodes.GetInputCount(kind);
            if (index < 0 || index >= count) return false;
            position = PortLocal(NodeRect(node, kind), kind, isOutput, index);
            return true;
        }

        /// <summary>엣지의 출력 포트와 입력 포트의 캔버스 로컬 좌표, 되돌아가는 곡선 여부와 올림 높이를 구한다. 포트를 찾지 못하면 false를 반환한다.</summary>
        private bool TryGetCurveEnds(GraphEdge edge, out Vector2 from, out Vector2 to, out bool isLoop, out float lift)
        {
            from = Vector2.zero; to = Vector2.zero; isLoop = false; lift = 0f;
            if (!TryParsePort(edge.FromPort, out int outputIndex) || !TryParsePort(edge.ToPort, out int inputIndex)) return false;
            if (!TryGetPortPosition(edge.FromNode, true, outputIndex, out from, out SpellNodeKind fromKind)) return false;
            if (!TryGetPortPosition(edge.ToNode, false, inputIndex, out to, out _)) return false;
            isLoop = GraphLayout.IsLoopingEdge(from, to, edge.FromNode == edge.ToNode);
            lift = GraphLayout.GetEdgeLift(fromKind);
            return true;
        }

        /// <summary>엣지 곡선을 CURVE_SEGMENTS 구간으로 나눈 점 목록을 points에 채운다. 곡선 규칙은 GraphLayout을 따른다.</summary>
        private void FillCurvePoints(Vector2[] points, Vector2 from, Vector2 to, bool isLoop, float lift)
        {
            for (int index = 0; index <= CURVE_SEGMENTS; index++)
                points[index] = GraphLayout.GetEdgePoint(from, to, isLoop, lift, _zoom, (float)index / CURVE_SEGMENTS);
        }

        /// <summary>점에서 선분 시작과 끝 사이의 가장 가까운 거리를 반환한다.</summary>
        private static float GetSegmentDistance(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 span = end - start;
            float lengthSquared = span.sqrMagnitude;
            float t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point - start, span) / lengthSquared) : 0f;
            return Vector2.Distance(point, start + span * t);
        }

        /// <summary>엣지를 곡선 선분으로 그리고 t = 0.68 지점에 접선 방향 화살표를 그린다. 분기의 거짓 출력에서 나가는 엣지는 주황색이다.</summary>
        private void DrawEdge(UnityEngine.UI.VertexHelper mesh, GraphEdge edge)
        {
            if (!TryGetCurveEnds(edge, out Vector2 from, out Vector2 to, out bool isLoop, out float lift)) return;
            FillCurvePoints(_curvePoints, from, to, isLoop, lift);
            Color color = IsFalseBranchEdge(edge) ? FALSE_EDGE_COLOR : FLOW_EDGE_COLOR;
            for (int index = 0; index < CURVE_SEGMENTS; index++)
                RuneMesh.Line(mesh, _curvePoints[index], _curvePoints[index + 1], EDGE_WIDTH, color);
            Vector2 arrow = GraphLayout.GetEdgePoint(from, to, isLoop, lift, _zoom, CURVE_ARROW_T);
            Vector2 direction = GraphLayout.GetEdgeTangent(from, to, isLoop, lift, _zoom, CURVE_ARROW_T).normalized;
            RuneMesh.Polygon(mesh, arrow, ARROW_RADIUS, ARROW_COLOR, 3, Mathf.Atan2(direction.y, direction.x));
        }

        /// <summary>엣지가 분기 노드의 거짓 출력(출력 1)에서 나가는지 반환한다.</summary>
        private bool IsFalseBranchEdge(GraphEdge edge)
        {
            GraphNode source = FindNode(edge.FromNode);
            if (source == null) return false;
            if (!GraphLayout.TryGetKind(source, out SpellNodeKind kind) || kind != SpellNodeKind.Branch) return false;
            return TryParsePort(edge.FromPort, out int port) && port == FALSE_BRANCH_PORT;
        }

        /// <summary>노드의 사각형, 헤더 띠, 아이콘과 포트를 메시로 그린다. 도달할 수 없는 노드는 회색으로 그린다.</summary>
        private void DrawNode(UnityEngine.UI.VertexHelper mesh, GraphNode node)
        {
            if (!GraphLayout.TryGetKind(node, out SpellNodeKind kind)) return;
            bool isUnreachable = IsUnreachable(node);
            Color accent = isUnreachable ? UNREACHABLE_ACCENT_COLOR : RuneMesh.NodeColor(kind);
            Rect rect = NodeRect(node, kind);
            bool isExecuted = _executedUntil.TryGetValue(node.Id, out float until) && until > Time.unscaledTime;
            RuneMesh.Rect(mesh, new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4),
                isExecuted ? new Color(1, 0.95f, 0.55f) : _selected.Contains(node.Id) ? accent : new Color(0.18f, 0.26f, 0.34f));
            Color body = isExecuted ? new Color(0.18f, 0.22f, 0.18f) : isUnreachable ? UNREACHABLE_BODY_COLOR : NODE_BODY_COLOR;
            RuneMesh.Rect(mesh, rect, body);
            RuneMesh.Rect(mesh, new Rect(rect.x, rect.yMax - 4 * _zoom, rect.width, 4 * _zoom), accent);
            RuneMesh.Polygon(mesh, new Vector2(rect.x + 5 * _zoom, rect.yMax - 17 * _zoom), 3 * _zoom, accent, 20);
            int inputCount = SpellNodes.GetInputCount(kind);
            for (int index = 0; index < inputCount; index++)
                DrawPort(mesh, PortLocal(rect, kind, false, index), false);
            int outputCount = SpellNodes.GetOutputCount(kind);
            for (int index = 0; index < outputCount; index++)
                DrawPort(mesh, PortLocal(rect, kind, true, index), true);
        }

        /// <summary>포트 원을 그린다. 연결 드래그 중이면 반대 방향 포트는 초록, 같은 방향 포트는 빨강이다.</summary>
        private void DrawPort(UnityEngine.UI.VertexHelper mesh, Vector2 point, bool isOutput)
        {
            Color color = PORT_COLOR;
            if (_sourceNode != null) color = isOutput != _isSourceOutput ? PORT_LINK_COLOR : PORT_BLOCK_COLOR;
            RuneMesh.Polygon(mesh, point, PORT_RADIUS * _zoom, color, 16);
        }

        /// <summary>노드가 시전 또는 적중 노드에서 도달할 수 없는지 반환한다. 실행 그래프가 없거나 색인에 없는 노드는 false를 반환한다.</summary>
        private bool IsUnreachable(GraphNode node)
        {
            if (_app.Program == null) return false;
            int index = _app.Program.IndexOf(node.Id);
            return index >= 0 && !_app.Program.IsReachable(index);
        }

        /// <summary>노드의 헤더 라벨과 출력 포트 설명 라벨을 갱신하고, 사용 중인 라벨 키를 activeKeys에 기록한다.</summary>
        private void UpdateNodeLabels(GraphNode node, SpellNodeKind kind, Rect rect, HashSet<string> activeKeys)
        {
            SpellSettings settings = GameData.Balance.Spell;
            bool isUnreachable = IsUnreachable(node);
            activeKeys.Add(node.Id);
            TextMeshProUGUI header = GetOrCreateLabel(node.Id);
            RectTransform headerRect = header.rectTransform;
            headerRect.anchorMin = headerRect.anchorMax = new Vector2(0, 1);
            headerRect.pivot = new Vector2(0, 1);
            headerRect.anchoredPosition = ToTopLeft(new Vector2(rect.xMin + NODE_PADDING * _zoom, rect.yMax - NODE_TOP_PADDING * _zoom));
            headerRect.sizeDelta = new Vector2((GraphLayout.NODE_WIDTH - NODE_PADDING * 2f) * _zoom,
                (GraphLayout.HEADER_HEIGHT - NODE_TOP_PADDING) * _zoom);
            header.fontSize = HEADER_FONT_SIZE * _zoom;
            header.text = BuildHeaderText(node, kind, settings);
            header.color = isUnreachable ? UNREACHABLE_LABEL_COLOR : Color.white;
            for (int index = 0; index < SpellNodes.GetOutputCount(kind); index++)
            {
                string caption = GetOutputCaption(node, kind, index, settings);
                if (caption == null) continue;
                string key = GetCaptionKey(node.Id, index);
                activeKeys.Add(key);
                TextMeshProUGUI label = GetOrCreateLabel(key);
                RectTransform captionRect = label.rectTransform;
                captionRect.anchorMin = captionRect.anchorMax = new Vector2(0, 1);
                captionRect.pivot = new Vector2(1, 0.5f);
                Vector2 port = PortLocal(rect, kind, true, index);
                captionRect.anchoredPosition = ToTopLeft(port - new Vector2(CAPTION_GAP * _zoom, 0f));
                captionRect.sizeDelta = new Vector2(CAPTION_WIDTH * _zoom, CAPTION_HEIGHT * _zoom);
                label.alignment = TextAlignmentOptions.MidlineRight;
                label.fontSize = SUB_FONT_SIZE * _zoom;
                label.text = caption;
                label.color = isUnreachable ? UNREACHABLE_LABEL_COLOR : CAPTION_COLOR;
            }
        }

        /// <summary>라벨 키에 해당하는 TMP 라벨을 반환하고, 없으면 캔버스의 자식으로 새로 만든다.</summary>
        private TextMeshProUGUI GetOrCreateLabel(string key)
        {
            if (_labels.TryGetValue(key, out TextMeshProUGUI label)) return label;
            var target = new GameObject("NodeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            target.transform.SetParent(transform, false);
            label = target.GetComponent<TextMeshProUGUI>();
            label.font = _font;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            _labels.Add(key, label);
            return label;
        }

        /// <summary>노드 이름, 비용 공식, 파라미터 요약을 세 줄의 TMP 문자열로 만들어 반환한다.</summary>
        private static string BuildHeaderText(GraphNode node, SpellNodeKind kind, SpellSettings settings)
        {
            float ratio = node.GetNumber(SpellNodes.PARAM_RATIO, settings.AmplifyDefault);
            string title = "<b>" + GameData.L("node." + SpellNodes.GetId(kind)) + "</b>";
            string formula = "<size=10><color=" + COST_COLOR_HEX + ">" + SpellNodes.GetCostFormula(kind, ratio, settings) + "</color></size>";
            string summary = "<size=10>" + BuildParameterSummary(node, kind, settings) + "</size>";
            return title + "\n" + formula + "\n" + summary;
        }

        /// <summary>노드 종류가 쓰는 파라미터를 한 줄의 요약 문자열로 만들어 반환한다. 파라미터가 없는 종류는 빈 문자열을 반환한다.</summary>
        private static string BuildParameterSummary(GraphNode node, SpellNodeKind kind, SpellSettings settings)
        {
            switch (kind)
            {
                case SpellNodeKind.Cast:
                    float load = node.GetNumber(SpellNodes.PARAM_LOAD, settings.LoadDefault);
                    string aim = node.GetText(SpellNodes.PARAM_AIM, SpellNodes.GetDefaultText(SpellNodes.PARAM_AIM));
                    return GameData.L("param.load") + " " + FormatNumber(load, NUMBER_FORMAT_ONE_DECIMAL)
                        + " · " + GameData.L("option.aim." + aim);
                case SpellNodeKind.Amplify:
                    float ratio = node.GetNumber(SpellNodes.PARAM_RATIO, settings.AmplifyDefault);
                    return "×" + FormatNumber(ratio, NUMBER_FORMAT_RATIO);
                case SpellNodeKind.Fork:
                    float share = node.GetNumber(SpellNodes.PARAM_SHARE, settings.ForkShareDefault);
                    return FormatNumber(share, NUMBER_FORMAT_INTEGER) + "% / "
                        + FormatNumber(PERCENT_TOTAL - share, NUMBER_FORMAT_INTEGER) + "%";
                case SpellNodeKind.Branch:
                    string condition = node.GetText(SpellNodes.PARAM_CONDITION, SpellNodes.GetDefaultText(SpellNodes.PARAM_CONDITION));
                    string compare = node.GetText(SpellNodes.PARAM_COMPARE, SpellNodes.GetDefaultText(SpellNodes.PARAM_COMPARE)) == SpellNodes.COMPARE_LE
                        ? LESS_OR_EQUAL : GREATER_OR_EQUAL;
                    float threshold = node.GetNumber(SpellNodes.PARAM_THRESHOLD, settings.BranchThresholdDefault);
                    return GameData.L("option.condition." + condition) + " " + compare + " "
                        + FormatNumber(threshold, NUMBER_FORMAT_ONE_DECIMAL);
                default:
                    return string.Empty;
            }
        }

        /// <summary>출력 포트 옆에 표시할 글자를 반환한다. 분기는 참과 거짓, 분배는 앞과 뒤 몫의 퍼센트이며 그 밖의 종류는 null이다.</summary>
        private static string GetOutputCaption(GraphNode node, SpellNodeKind kind, int index, SpellSettings settings)
        {
            switch (kind)
            {
                case SpellNodeKind.Fork:
                    float share = node.GetNumber(SpellNodes.PARAM_SHARE, settings.ForkShareDefault);
                    float portShare = index == 0 ? share : PERCENT_TOTAL - share;
                    return FormatNumber(portShare, NUMBER_FORMAT_INTEGER) + "%";
                case SpellNodeKind.Branch:
                    return GameData.L(index == 0 ? PORT_TRUE_KEY : PORT_FALSE_KEY);
                default:
                    return null;
            }
        }

        /// <summary>노드 ID와 출력 포트 번호로 출력 포트 라벨의 키를 만들어 반환한다.</summary>
        private static string GetCaptionKey(string nodeId, int index)
        {
            return nodeId + PORT_LABEL_SUFFIX + index.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>숫자를 지정 형식의 불변 문화권 문자열로 바꿔 반환한다.</summary>
        private static string FormatNumber(float value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>포트 번호 문자열을 십진 정수로 읽으며 성공하면 true를 반환한다.</summary>
        private static bool TryParsePort(string text, out int port)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port);
        }

        /// <summary>캔버스 로컬 좌표를 캔버스 왼쪽 위를 기준으로 한 anchoredPosition 값으로 바꿔 반환한다.</summary>
        private Vector2 ToTopLeft(Vector2 local)
        {
            return new Vector2(local.x - rectTransform.rect.xMin, local.y - rectTransform.rect.yMax);
        }

        /// <summary>두 점을 포함하는 선택 영역 사각형을 반환한다.</summary>
        private static Rect MakeRect(Vector2 first, Vector2 second)
        { return Rect.MinMaxRect(Mathf.Min(first.x, second.x), Mathf.Min(first.y, second.y), Mathf.Max(first.x, second.x), Mathf.Max(first.y, second.y)); }
    }
}
