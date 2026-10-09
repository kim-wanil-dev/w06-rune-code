using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RuneCode
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RuneGraphCanvas : MaskableGraphic,
        IPointerDownHandler, IPointerUpHandler, IPointerMoveHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        private const float NODE_WIDTH = 160f;
        private const float NODE_BASE_HEIGHT = 39f;
        private const float NODE_BORDER = 2f;
        private const float ACCENT_BAR_HEIGHT = 4f;
        private const float ICON_OFFSET_X = 5f;
        private const float ICON_OFFSET_Y = 17f;
        private const float ICON_RADIUS = 3f;

        private const float PORT_SPACING = 23f;
        private const float PORT_TOP_OFFSET = 40f;
        private const float PORT_RADIUS = 5f;
        private const float PORT_HIT_RADIUS = 9f;
        private const float MIN_PORT_HIT_RADIUS = 8f;

        private const float LABEL_FONT_SIZE = 12f;
        private const float LABEL_PADDING_X = 14f;
        private const float LABEL_PADDING_TOP = 10f;
        private const float LABEL_PADDING_BOTTOM = 4f;

        private const float GRID_STEP = 28f;
        private const float GRID_DOT_SIZE = 1.5f;

        private const float EDGE_WIDTH = 2.4f;
        private const float EDGE_ARROW_SIZE = 4.5f;
        private const float EDGE_ARROW_POSITION = 0.68f;
        private const float EDGE_HIT_DISTANCE = 8f;
        private const float SELECTION_LINE_WIDTH = 1f;

        private const float MIN_ZOOM = 0.45f;
        private const float MAX_ZOOM = 1.5f;
        private const float MAX_FIT_ZOOM = 1f;
        private const float ZOOM_IN_FACTOR = 1.1f;
        private const float ZOOM_OUT_FACTOR = 0.9f;
        private const float FIT_MARGIN = 24f;

        private const float AUTO_CONNECT_DISTANCE = 175f;
        private const float AUTO_CHAIN_DISTANCE = 320f;
        private const float CHAIN_PLACEMENT_GAP = 20f;
        private const float PLACEMENT_MARGIN = 12f;
        private const float PLACEMENT_BOTTOM_MARGIN = 90f;

        private const float HIGHLIGHT_DURATION = 0.2f;
        private const float NODE_DRAG_THRESHOLD = 1f;

        private static readonly Vector2 MODIFIER_PLACEMENT_OFFSET = new Vector2(-140f, 90f);

        private static readonly Color BACKGROUND_COLOR = new Color(0.035f, 0.064f, 0.10f);
        private static readonly Color GRID_COLOR = new Color(0.16f, 0.24f, 0.31f);
        private static readonly Color CHAIN_COLOR = new Color(0.98f, 0.74f, 0.36f);
        private static readonly Color MODIFIER_EDGE_COLOR = new Color(0.70f, 0.47f, 0.98f);
        private static readonly Color EXEC_EDGE_COLOR = new Color(0.2f, 0.70f, 0.82f);
        private static readonly Color EDGE_ARROW_COLOR = new Color(0.65f, 0.85f, 0.94f);
        private static readonly Color NODE_BORDER_COLOR = new Color(0.18f, 0.26f, 0.34f);
        private static readonly Color NODE_FILL_COLOR = new Color(0.075f, 0.12f, 0.18f);
        private static readonly Color EXECUTED_BORDER_COLOR = new Color(1f, 0.95f, 0.55f);
        private static readonly Color EXECUTED_FILL_COLOR = new Color(0.18f, 0.22f, 0.18f);
        private static readonly Color MODIFIER_PORT_COLOR = new Color(0.76f, 0.52f, 1f);
        private static readonly Color EXEC_PORT_COLOR = new Color(0.32f, 0.88f, 0.98f);
        private static readonly Color CONNECTABLE_PORT_COLOR = new Color(0.28f, 1f, 0.57f);
        private static readonly Color BLOCKED_PORT_COLOR = new Color(1f, 0.26f, 0.29f);
        private static readonly Color SELECTION_FILL_COLOR = new Color(0.24f, 0.80f, 1f, 0.12f);

        private ISpellEditor _editor;
        private TMP_FontAsset _font;
        private Action<GraphNode> _selectionChanged;
        private Action<GraphNode> _hoverChanged;
        private Action<Vector2> _quickPlacement;

        private Vector2 _pan = new Vector2(30, -35);
        private float _zoom = 1;

        private readonly HashSet<string> _selected = new HashSet<string>();
        public IReadOnlyCollection<string> SelectedNodes => _selected;

        private readonly Dictionary<string, TextMeshProUGUI> _labels = new Dictionary<string, TextMeshProUGUI>();
        private readonly HashSet<string> _existingNodes = new HashSet<string>();
        private readonly List<string> _staleIds = new List<string>();
        private readonly StringBuilder _labelBuilder = new StringBuilder();

        private readonly Dictionary<string, Vector2> _dragOrigins = new Dictionary<string, Vector2>();
        private Vector2 _pointer;
        private Vector2 _dragStart;
        private Vector2 _panStart;
        private bool _isPanning;
        private bool _isBoxSelecting;
        private bool _isNodeDragging;

        private readonly Dictionary<string, bool[]> _connectablePorts = new Dictionary<string, bool[]>();
        private string _sourceNode;
        private string _sourcePort;
        private bool _isSourceOutput;

        private GraphNode _hoveredNode;
        private bool _isHoverValid;

        private readonly Dictionary<string, GraphNode> _nodeLookup = new Dictionary<string, GraphNode>();
        private readonly Dictionary<string, float> _executedUntil = new Dictionary<string, float>();

        private bool HasGraph => _editor != null && _editor.Graph != null;
        private Vector2 TopLeft => new Vector2(rectTransform.rect.xMin, rectTransform.rect.yMax);

        /// <summary>편집 세션, 글꼴과 선택 콜백을 연결하고 그래프를 표시한다.</summary>
        public void Initialize(ISpellEditor editor, TMP_FontAsset font, Action<GraphNode> selectionChanged,
            Action<GraphNode> hoverChanged, Action<Vector2> quickPlacement)
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

        /// <summary>현재 그래프의 노드 라벨 텍스트·배치와 메시를 갱신하고 사라진 노드의 선택을 해제한다.</summary>
        public void RefreshGraph()
        {
            if (!HasGraph)
            {
                return;
            }

            _existingNodes.Clear();
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                _existingNodes.Add(node.Id);
                TextMeshProUGUI label = GetOrCreateLabel(node.Id);
                string text = BuildLabelText(node, label);
                if (label.text != text)
                {
                    label.text = text;
                }
            }

            RemoveStaleLabels();
            _selected.RemoveWhere(id => !_existingNodes.Contains(id));
            _isHoverValid = false;
            LayoutLabels();
        }

        /// <summary>지정 노드를 실행 하이라이트 상태로 일정 시간 표시한다.</summary>
        public void Highlight(string nodeId)
        {
            _executedUntil[nodeId] = Time.unscaledTime + HIGHLIGHT_DURATION;
            SetVerticesDirty();
        }

        /// <summary>지정 노드가 중앙에 보이도록 화면 위치를 조정하고 선택한다.</summary>
        public void FocusNode(string nodeId)
        {
            GraphNode node = FindNode(nodeId);
            if (node == null)
            {
                return;
            }

            float centerX = rectTransform.rect.width / 2 - (node.X + NODE_WIDTH / 2) * _zoom;
            float centerY = -rectTransform.rect.height / 2 + node.Y * _zoom;
            _pan = new Vector2(centerX, centerY);

            _selected.Clear();
            _selected.Add(nodeId);
            _selectionChanged(node);
            RefreshGraph();
        }

        /// <summary>현재 선택의 삭제 불가능한 시전 노드를 제외하고 노드와 연결을 삭제한다.</summary>
        public void DeleteSelection()
        {
            foreach (string id in new List<string>(_selected))
            {
                _editor.RemoveNode(id);
            }

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

        /// <summary>현재 보이는 캔버스 안에서 새 룬을 놓을 좌표를 반환한다. 효과는 Behavior 아래, Behavior는 Shape의 열린 체인 끝 오른쪽을 우선한다.</summary>
        public Vector2 SuggestPlacement(string runeId)
        {
            RuneDefinition rune = GameData.Runes.Get(runeId);
            Vector2 position = GraphPoint(rectTransform.rect.center);

            // 문법상 기준점(효과를 받을 노드, 열린 체인 끝) 중 마지막 노드 옆을 제안한다.
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                if (!SpellAutoConnect.IsAnchor(_editor.Graph, GameData.Runes, rune, node))
                {
                    continue;
                }
                position = SpellAutoConnect.IsModifier(rune)
                    ? new Vector2(node.X, node.Y) + MODIFIER_PLACEMENT_OFFSET
                    : new Vector2(node.X + NODE_WIDTH + CHAIN_PLACEMENT_GAP, node.Y);
            }

            // 제안 위치가 화면 밖이면 보이는 영역 안쪽으로 당긴다.
            Rect bounds = rectTransform.rect;
            Vector2 local = ToLocal(position);
            local.x = Mathf.Clamp(local.x, bounds.xMin + PLACEMENT_MARGIN, bounds.xMax - (NODE_WIDTH + PLACEMENT_MARGIN) * _zoom);
            local.y = Mathf.Clamp(local.y, bounds.yMin + PLACEMENT_BOTTOM_MARGIN * _zoom, bounds.yMax - PLACEMENT_MARGIN);
            return GraphPoint(local);
        }

        /// <summary>
        /// 지정 좌표에 룬을 배치한다. 효과는 가까운 Behavior·프리셋 호출의 효과 입력에,
        /// Behavior 블록(Apply 제외)은 가까운 Shape의 열린 체인 끝에 자동으로 연결한다.
        /// </summary>
        public void PlaceRune(string runeId, Vector2 position)
        {
            int oldCount = _editor.Graph.Nodes.Count;
            _editor.AddRune(runeId, position.x, position.y);
            if (_editor.Graph.Nodes.Count == oldCount)
            {
                return;
            }

            RuneDefinition rune = GameData.Runes.Get(runeId);
            GraphNode placed = _editor.Graph.Nodes[_editor.Graph.Nodes.Count - 1];
            if (SpellAutoConnect.IsModifier(rune))
            {
                AutoConnect(placed, position, AUTO_CONNECT_DISTANCE);
            }
            else if (SpellAutoConnect.IsChainLink(rune))
            {
                AutoConnect(placed, position, AUTO_CHAIN_DISTANCE);
            }
        }

        void Update()
        {
            if (_executedUntil.Count == 0)
            {
                return;
            }

            _staleIds.Clear();
            foreach (var pair in _executedUntil)
            {
                if (pair.Value <= Time.unscaledTime)
                {
                    _staleIds.Add(pair.Key);
                }
            }

            if (_staleIds.Count == 0)
            {
                return;
            }

            foreach (string id in _staleIds)
            {
                _executedUntil.Remove(id);
            }
            SetVerticesDirty();
        }

        /// <summary>노드 좌표를 화면 좌표로 변환해 배경, 연결선, 노드, 드래그 표시 순서로 그래프 메시를 구성한다.</summary>
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            DrawBackground(mesh);
            if (!HasGraph)
            {
                return;
            }

            RebuildNodeLookup();
            DrawEdges(mesh);
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                DrawNode(mesh, node);
            }
            DrawConnectionPreview(mesh);
            DrawBoxSelection(mesh);
        }

        /// <summary>포인터 위치에서 연결, 노드 선택, 화면 이동 또는 영역 선택을 시작한다.</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            _pointer = Local(eventData);
            _dragStart = _pointer;
            _panStart = _pan;

            if (eventData.button == PointerEventData.InputButton.Middle)
            {
                _isPanning = true;
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (TryRemoveEdgeAt(_pointer))
                {
                    RefreshGraph();
                    return;
                }
                _isPanning = true;
                return;
            }

            if (FindPort(_pointer, out GraphNode portNode, out PortDefinition port))
            {
                BeginConnection(portNode, port);
                SetVerticesDirty();
                return;
            }

            bool isShift = IsShiftPressed();
            GraphNode clicked = FindNodeAt(_pointer);
            if (clicked != null)
            {
                BeginNodeDrag(clicked, isShift);
            }
            else
            {
                if (eventData.clickCount >= 2)
                {
                    _quickPlacement(GraphPoint(_pointer));
                    return;
                }
                BeginBoxSelection(isShift);
            }
            SetVerticesDirty();
        }

        /// <summary>포인터 이동에 맞춰 포트 선, 선택 노드 또는 화면 위치를 변경한다.</summary>
        public void OnDrag(PointerEventData eventData)
        {
            _pointer = Local(eventData);

            if (_isPanning)
            {
                _pan = _panStart + _pointer - _dragStart;
            }
            if (_isNodeDragging)
            {
                MoveDraggedNodes();
            }

            if (_isPanning || _isNodeDragging)
            {
                LayoutLabels();
            }
            else
            {
                SetVerticesDirty();
            }
        }

        /// <summary>연결 시도와 영역 선택을 확정하고 그래프 변경을 애플리케이션에 알린다.</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            _pointer = Local(eventData);

            TryCompleteConnection();
            if (_isBoxSelecting)
            {
                SelectNodesInBox();
            }
            if (_isNodeDragging && Vector2.Distance(_pointer, _dragStart) > NODE_DRAG_THRESHOLD)
            {
                _editor.MarkChanged();
            }

            ResetPointerState();
            RefreshGraph();
        }

        /// <summary>그래프 드래그를 수신하도록 Unity 이벤트 시스템에 알린다.</summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        /// <summary>포인터 해제와 동일한 그래프 드래그 종료 절차를 수행한다.</summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            OnPointerUp(eventData);
        }

        /// <summary>마우스 위치를 중심으로 그래프 확대 배율을 변경한다.</summary>
        public void OnScroll(PointerEventData eventData)
        {
            Vector2 point = Local(eventData) - TopLeft;
            float factor = eventData.scrollDelta.y > 0 ? ZOOM_IN_FACTOR : ZOOM_OUT_FACTOR;
            float nextZoom = Mathf.Clamp(_zoom * factor, MIN_ZOOM, MAX_ZOOM);

            // 포인터 아래 그래프 지점이 확대 전후 같은 화면 위치에 남도록 화면 이동을 보정한다.
            _pan = point - (point - _pan) * (nextZoom / _zoom);
            _zoom = nextZoom;
            LayoutLabels();
        }

        /// <summary>포인터 아래 노드가 바뀐 경우에만 툴팁에 사용할 노드 정보를 전달한다.</summary>
        public void OnPointerMove(PointerEventData eventData)
        {
            GraphNode hovered = FindNodeAt(Local(eventData));
            if (_isHoverValid && hovered == _hoveredNode)
            {
                return;
            }

            _hoveredNode = hovered;
            _isHoverValid = true;
            _hoverChanged(hovered);
        }

        /// <summary>첫 표시 때 모든 노드가 편집 영역 안에 보이도록 화면 위치와 배율을 맞춘다.</summary>
        private void FitGraph()
        {
            if (!HasGraph || _editor.Graph.Nodes.Count == 0)
            {
                return;
            }

            float minimumX = float.MaxValue;
            float minimumY = float.MaxValue;
            float maximumX = float.MinValue;
            float maximumY = float.MinValue;
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                float height = NodeHeight(GameData.Runes.Get(node.RuneId));
                minimumX = Mathf.Min(minimumX, node.X);
                minimumY = Mathf.Min(minimumY, node.Y);
                maximumX = Mathf.Max(maximumX, node.X + NODE_WIDTH);
                maximumY = Mathf.Max(maximumY, node.Y + height);
            }

            float zoomX = (rectTransform.rect.width - FIT_MARGIN * 2) / Mathf.Max(1, maximumX - minimumX);
            float zoomY = (rectTransform.rect.height - FIT_MARGIN * 2) / Mathf.Max(1, maximumY - minimumY);
            _zoom = Mathf.Clamp(Mathf.Min(zoomX, zoomY), MIN_ZOOM, MAX_FIT_ZOOM);
            _pan = new Vector2(FIT_MARGIN - minimumX * _zoom, -FIT_MARGIN + minimumY * _zoom);
        }

        /// <summary>노드 ID에 해당하는 라벨을 반환하며 없으면 고정 글꼴 크기와 좌상단 기준점으로 새로 만든다.</summary>
        private TextMeshProUGUI GetOrCreateLabel(string nodeId)
        {
            if (_labels.TryGetValue(nodeId, out TextMeshProUGUI label))
            {
                return label;
            }

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
            textRect.anchorMin = new Vector2(0, 1);
            textRect.anchorMax = new Vector2(0, 1);
            textRect.pivot = new Vector2(0, 1);

            _labels.Add(nodeId, label);
            return label;
        }

        /// <summary>
        /// 룬 이름·RAM, 핵심 설정 요약, 입력·출력 포트 이름 행으로 구성한 노드 라벨 rich text를 반환한다.
        /// 출력 포트 이름은 지정 라벨의 글꼴로 너비를 재어 노드 오른쪽 끝에 맞춘다.
        /// </summary>
        private string BuildLabelText(GraphNode node, TextMeshProUGUI label)
        {
            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            _labelBuilder.Clear();
            _labelBuilder.Append("<b>").Append(rune.Name).Append("</b>  <color=#8EACC6>").Append(GameData.Runes.NodeRam(node)).Append(" RAM</color>\n");

            string summary = NodeSummary(node);
            if (!string.IsNullOrEmpty(summary))
            {
                _labelBuilder.Append("<size=10><color=#A8C9D8>").Append(summary).Append("</color></size>\n");
            }

            int rows = PortRowCount(rune);
            float labelWidth = NODE_WIDTH - LABEL_PADDING_X * 2;
            for (int row = 0; row < rows; row++)
            {
                PortDefinition input = PortAt(rune, SpellGrammar.DIRECTION_IN, row);
                PortDefinition output = PortAt(rune, SpellGrammar.DIRECTION_OUT, row);
                _labelBuilder.Append("<size=10>").Append(input?.Id);
                if (output != null)
                {
                    // 출력 포트 이름이 길어도 노드 밖으로 넘치지 않도록 오른쪽 끝 기준으로 시작 위치를 정한다.
                    float outputWidth = label.GetPreferredValues("<size=10>" + output.Id).x;
                    float outputStart = Mathf.Max(0, labelWidth - outputWidth);
                    _labelBuilder.Append("<pos=").Append(outputStart.ToString("0.#", CultureInfo.InvariantCulture)).Append('>').Append(output.Id);
                }
                _labelBuilder.Append("</size>\n");
            }
            return _labelBuilder.ToString();
        }

        /// <summary>
        /// 그래프에서 사라진 노드의 라벨 오브젝트를 파괴하고 라벨 목록에서 제거한다.
        /// </summary>
        private void RemoveStaleLabels()
        {
            _staleIds.Clear();
            foreach (var pair in _labels)
            {
                if (!_existingNodes.Contains(pair.Key))
                {
                    _staleIds.Add(pair.Key);
                }
            }

            foreach (string id in _staleIds)
            {
                Destroy(_labels[id].gameObject);
                _labels.Remove(id);
            }
        }

        /// <summary>
        /// 화면 이동·확대 배율에 맞춰 라벨 위치와 스케일만 갱신하고 메시 재구성을 예약한다.
        /// 글꼴 크기는 고정하고 Transform 스케일로 확대해 rich text 크기·위치 태그가 노드와 같은 비율을 유지한다.
        /// </summary>
        private void LayoutLabels()
        {
            if (!HasGraph)
            {
                return;
            }

            Vector3 scale = new Vector3(_zoom, _zoom, 1);
            float labelWidth = NODE_WIDTH - LABEL_PADDING_X * 2;
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                if (!_labels.TryGetValue(node.Id, out TextMeshProUGUI label))
                {
                    continue;
                }

                RuneDefinition rune = GameData.Runes.Get(node.RuneId);
                Rect rect = NodeRect(node, rune);
                float labelHeight = NodeHeight(rune) - LABEL_PADDING_TOP - LABEL_PADDING_BOTTOM;

                RectTransform textRect = label.rectTransform;
                textRect.anchoredPosition = new Vector2(rect.x + LABEL_PADDING_X * _zoom,
                    rect.yMax - rectTransform.rect.yMax - LABEL_PADDING_TOP * _zoom);
                textRect.sizeDelta = new Vector2(labelWidth, labelHeight);
                textRect.localScale = scale;
            }
            SetVerticesDirty();
        }

        /// <summary>Trigger·Shape·Apply·Spell Call의 핵심 값(시작 조건, 속성, 호출 대상)을 노드 라벨용 문자열로 반환하며 해당 없으면 null을 반환한다.</summary>
        private string NodeSummary(GraphNode node)
        {
            if (node.RuneId == SpellGrammar.CORE_RUNE)
            {
                string trigger = node.GetText(SpellGrammar.TRIGGER_PARAM, SpellGrammar.TRIGGER_ON_ATTACK);
                return LocalizedValue("trigger." + trigger, trigger);
            }

            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            if (rune.Category == SpellGrammar.CATEGORY_SHAPE || rune.Id == SpellGrammar.APPLY_RUNE)
            {
                string fallback = rune.Id == SpellGrammar.APPLY_RUNE ? "healing" : SpellGrammar.ELEMENT_NEUTRAL;
                string element = node.GetText(SpellGrammar.ELEMENT_PARAM, fallback);
                return LocalizedValue("element." + element, element);
            }

            if (node.RuneId == SpellGrammar.CALL_RUNE)
            {
                string spellId = node.GetText("spellId");
                foreach (SpellGraph graph in _editor.Library)
                {
                    if (graph.Id == spellId)
                    {
                        return graph.Name;
                    }
                }
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

        /// <summary>배경과 화면 이동·배율에 맞춘 격자 점을 메시에 추가한다.</summary>
        private void DrawBackground(VertexHelper mesh)
        {
            Rect bounds = rectTransform.rect;
            RuneMesh.Rect(mesh, bounds, BACKGROUND_COLOR);

            float step = GRID_STEP * _zoom;
            for (float x = bounds.xMin + _pan.x % step; x < bounds.xMax; x += step)
            {
                for (float y = bounds.yMax + _pan.y % step; y > bounds.yMin; y -= step)
                {
                    RuneMesh.Rect(mesh, new Rect(x, y, GRID_DOT_SIZE, GRID_DOT_SIZE), GRID_COLOR);
                }
            }
        }

        /// <summary>모든 연결선과 방향 화살표를 메시에 추가한다. RebuildNodeLookup 이후 호출한다.</summary>
        private void DrawEdges(VertexHelper mesh)
        {
            foreach (GraphEdge edge in _editor.Graph.Edges)
            {
                Vector2 from = LookupPortPosition(edge.FromNode, edge.FromPort);
                Vector2 to = LookupPortPosition(edge.ToNode, edge.ToPort);
                RuneMesh.Line(mesh, from, to, EDGE_WIDTH, EdgeColor(edge));

                Vector2 direction = (to - from).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x);
                RuneMesh.Polygon(mesh, Vector2.Lerp(from, to, EDGE_ARROW_POSITION), EDGE_ARROW_SIZE, EDGE_ARROW_COLOR, 3, angle);
            }
        }

        /// <summary>노드 테두리·본체·카테고리 강조 막대·아이콘과 포트를 메시에 추가한다.</summary>
        private void DrawNode(VertexHelper mesh, GraphNode node)
        {
            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            Color accent = RuneMesh.CategoryColor(rune.Category);
            Rect rect = NodeRect(node, rune);
            bool isExecuted = _executedUntil.TryGetValue(node.Id, out float until) && until > Time.unscaledTime;

            Rect border = new Rect(rect.x - NODE_BORDER, rect.y - NODE_BORDER, rect.width + NODE_BORDER * 2, rect.height + NODE_BORDER * 2);
            RuneMesh.Rect(mesh, border, NodeBorderColor(node, accent, isExecuted));
            RuneMesh.Rect(mesh, rect, isExecuted ? EXECUTED_FILL_COLOR : NODE_FILL_COLOR);

            Rect accentBar = new Rect(rect.x, rect.yMax - ACCENT_BAR_HEIGHT * _zoom, rect.width, ACCENT_BAR_HEIGHT * _zoom);
            RuneMesh.Rect(mesh, accentBar, accent);

            Vector2 iconCenter = new Vector2(rect.x + ICON_OFFSET_X * _zoom, rect.yMax - ICON_OFFSET_Y * _zoom);
            RuneMesh.Polygon(mesh, iconCenter, ICON_RADIUS * _zoom, accent, CategoryIconSides(rune.Category));

            DrawPorts(mesh, node, rune, rect);
        }

        /// <summary>노드의 포트를 종류별 모양으로 그리며 연결 드래그 중에는 연결 가능 여부 색으로 표시한다.</summary>
        private void DrawPorts(VertexHelper mesh, GraphNode node, RuneDefinition rune, Rect rect)
        {
            _connectablePorts.TryGetValue(node.Id, out bool[] connectable);
            for (int i = 0; i < rune.Ports.Count; i++)
            {
                PortDefinition port = rune.Ports[i];
                Color color = PortBaseColor(port.Kind);
                if (_sourceNode != null)
                {
                    bool canConnect = connectable != null && connectable[i];
                    color = canConnect ? CONNECTABLE_PORT_COLOR : BLOCKED_PORT_COLOR;
                }

                Vector2 point = PortPosition(rect, rune, port.Id);
                RuneMesh.Polygon(mesh, point, PORT_RADIUS * _zoom, color, PortSides(port.Kind));
            }
        }

        /// <summary>연결 드래그 중이면 시작 포트에서 포인터까지 미리보기 선을 메시에 추가한다.</summary>
        private void DrawConnectionPreview(VertexHelper mesh)
        {
            if (_sourceNode == null)
            {
                return;
            }

            RuneMesh.Line(mesh, LookupPortPosition(_sourceNode, _sourcePort), _pointer, EDGE_WIDTH, Color.white);
        }

        /// <summary>영역 선택 중이면 선택 사각형과 위·아래 경계선을 메시에 추가한다.</summary>
        private void DrawBoxSelection(VertexHelper mesh)
        {
            if (!_isBoxSelecting)
            {
                return;
            }

            Rect selection = MakeRect(_dragStart, _pointer);
            RuneMesh.Rect(mesh, selection, SELECTION_FILL_COLOR);
            RuneMesh.Line(mesh, new Vector2(selection.xMin, selection.yMin), new Vector2(selection.xMax, selection.yMin), SELECTION_LINE_WIDTH, Color.cyan);
            RuneMesh.Line(mesh, new Vector2(selection.xMin, selection.yMax), new Vector2(selection.xMax, selection.yMax), SELECTION_LINE_WIDTH, Color.cyan);
        }

        /// <summary>실행 하이라이트, 선택 여부 순으로 노드 테두리 색을 반환한다.</summary>
        private Color NodeBorderColor(GraphNode node, Color accent, bool isExecuted)
        {
            if (isExecuted)
            {
                return EXECUTED_BORDER_COLOR;
            }
            return _selected.Contains(node.Id) ? accent : NODE_BORDER_COLOR;
        }

        /// <summary>효과 연결, 체인 연결, 실행 연결 순으로 연결선 색을 반환한다.</summary>
        private static Color EdgeColor(GraphEdge edge)
        {
            if (edge.FromPort == SpellGrammar.MODIFIER_PORT)
            {
                return MODIFIER_EDGE_COLOR;
            }
            if (edge.ToPort == SpellGrammar.CHAIN_IN)
            {
                return CHAIN_COLOR;
            }
            return EXEC_EDGE_COLOR;
        }

        /// <summary>룬 카테고리별 노드 아이콘 다각형의 변 개수를 반환한다.</summary>
        private static int CategoryIconSides(string category)
        {
            return category switch
            {
                SpellGrammar.CATEGORY_BEHAVIOR => 4,
                SpellGrammar.CATEGORY_ELEMENT => 3,
                SpellGrammar.CATEGORY_SHAPE => 5,
                SpellGrammar.CATEGORY_MODIFIER => 6,
                _ => 20,
            };
        }

        /// <summary>포트 종류별 기본 색을 반환한다.</summary>
        private static Color PortBaseColor(string kind)
        {
            return kind switch
            {
                SpellGrammar.MODIFIER_KIND => MODIFIER_PORT_COLOR,
                SpellGrammar.CHAIN_KIND => CHAIN_COLOR,
                _ => EXEC_PORT_COLOR,
            };
        }

        /// <summary>포트 종류별 다각형 변 개수를 반환한다.</summary>
        private static int PortSides(string kind)
        {
            return kind switch
            {
                SpellGrammar.MODIFIER_KIND => 4,
                SpellGrammar.CHAIN_KIND => 6,
                _ => 16,
            };
        }

        /// <summary>포인터에서 가까운 연결선을 찾아 삭제하고 삭제 여부를 반환한다.</summary>
        private bool TryRemoveEdgeAt(Vector2 point)
        {
            RebuildNodeLookup();
            foreach (GraphEdge edge in _editor.Graph.Edges)
            {
                Vector2 from = LookupPortPosition(edge.FromNode, edge.FromPort);
                Vector2 to = LookupPortPosition(edge.ToNode, edge.ToPort);

                // 포인터를 선분에 투영한 가장 가까운 점과의 거리로 클릭 여부를 판단한다.
                Vector2 span = to - from;
                float t = Mathf.Clamp01(Vector2.Dot(point - from, span) / Mathf.Max(1, span.sqrMagnitude));
                if (Vector2.Distance(point, from + span * t) < EDGE_HIT_DISTANCE)
                {
                    _editor.RemoveEdge(edge.Id);
                    return true;
                }
            }
            return false;
        }

        /// <summary>지정 포트를 연결 시작점으로 저장하고 다른 포트의 연결 가능 여부를 캐시한다.</summary>
        private void BeginConnection(GraphNode node, PortDefinition port)
        {
            _sourceNode = node.Id;
            _sourcePort = port.Id;
            _isSourceOutput = port.Direction == SpellGrammar.DIRECTION_OUT;
            CacheConnectablePorts();
        }

        /// <summary>
        /// 연결 시작 포트 기준으로 모든 노드 포트의 연결 가능 여부를 계산해 노드 ID별 포트 순번 배열로 저장한다.
        /// 연결 드래그 동안 그래프가 바뀌지 않으므로 메시 재구성마다 규칙 검사를 반복하지 않는다.
        /// </summary>
        private void CacheConnectablePorts()
        {
            _connectablePorts.Clear();
            string oppositeDirection = _isSourceOutput ? SpellGrammar.DIRECTION_IN : SpellGrammar.DIRECTION_OUT;
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                IReadOnlyList<PortDefinition> ports = GameData.Runes.Get(node.RuneId).Ports;
                var connectable = new bool[ports.Count];
                for (int i = 0; i < ports.Count; i++)
                {
                    PortDefinition port = ports[i];
                    if (port.Direction != oppositeDirection)
                    {
                        continue;
                    }

                    connectable[i] = _isSourceOutput
                        ? _editor.CanConnectPorts(_sourceNode, _sourcePort, node.Id, port.Id)
                        : _editor.CanConnectPorts(node.Id, port.Id, _sourceNode, _sourcePort);
                }
                _connectablePorts[node.Id] = connectable;
            }
        }

        /// <summary>포인터 위치의 포트가 시작 노드가 아닌 다른 노드에 있으면 방향에 맞춰 연결한다.</summary>
        private void TryCompleteConnection()
        {
            if (_sourceNode == null)
            {
                return;
            }
            if (!FindPort(_pointer, out GraphNode node, out PortDefinition port) || node.Id == _sourceNode)
            {
                return;
            }

            if (_isSourceOutput)
            {
                _editor.Connect(_sourceNode, _sourcePort, node.Id, port.Id);
            }
            else
            {
                _editor.Connect(node.Id, port.Id, _sourceNode, _sourcePort);
            }
        }

        /// <summary>클릭한 노드로 선택을 갱신하고 선택된 노드들의 드래그 시작 좌표를 저장한다.</summary>
        private void BeginNodeDrag(GraphNode clicked, bool isShift)
        {
            // Shift 없이 선택 밖 노드를 누르면 단일 선택, Shift를 누르면 선택을 토글한다.
            if (!isShift && !_selected.Contains(clicked.Id))
            {
                _selected.Clear();
            }
            if (isShift && _selected.Contains(clicked.Id))
            {
                _selected.Remove(clicked.Id);
            }
            else
            {
                _selected.Add(clicked.Id);
            }
            _selectionChanged(clicked);

            _isNodeDragging = true;
            _dragOrigins.Clear();
            foreach (string id in _selected)
            {
                GraphNode node = FindNode(id);
                if (node != null)
                {
                    _dragOrigins[id] = new Vector2(node.X, node.Y);
                }
            }
            _editor.BeginEdit();
        }

        /// <summary>드래그 시작 이후 포인터 이동량만큼 선택된 노드들을 그래프 좌표에서 이동한다.</summary>
        private void MoveDraggedNodes()
        {
            Vector2 difference = (_pointer - _dragStart) / _zoom;
            foreach (var pair in _dragOrigins)
            {
                FindNode(pair.Key)?.Move(pair.Value.x + difference.x, pair.Value.y - difference.y);
            }
        }

        /// <summary>Shift가 없으면 기존 선택을 해제하고 영역 선택을 시작한다.</summary>
        private void BeginBoxSelection(bool isShift)
        {
            if (!isShift)
            {
                _selected.Clear();
            }
            _isBoxSelecting = true;
            _selectionChanged(null);
        }

        /// <summary>영역 선택 사각형과 겹치는 노드를 선택에 추가한다.</summary>
        private void SelectNodesInBox()
        {
            Rect area = MakeRect(_dragStart, _pointer);
            foreach (GraphNode candidate in _editor.Graph.Nodes)
            {
                if (area.Overlaps(NodeRect(candidate, GameData.Runes.Get(candidate.RuneId))))
                {
                    _selected.Add(candidate.Id);
                }
            }
        }

        /// <summary>연결·화면 이동·노드 드래그·영역 선택 상태와 연결 가능 캐시를 초기화한다.</summary>
        private void ResetPointerState()
        {
            _sourceNode = null;
            _isPanning = false;
            _isNodeDragging = false;
            _isBoxSelecting = false;
            _connectablePorts.Clear();
        }

        /// <summary>왼쪽 또는 오른쪽 Shift 키가 눌려 있는지 반환한다.</summary>
        private static bool IsShiftPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        /// <summary>
        /// 배치한 노드와 문법상 자동 연결할 수 있는 노드 중 거리 안의 가장 가까운 노드에 연결한다.
        /// 연결 규칙은 SpellAutoConnect, 편집 가능 여부·제한은 편집 세션의 연결 검사를 따른다.
        /// </summary>
        private void AutoConnect(GraphNode placed, Vector2 position, float maxDistance)
        {
            GraphNode target = FindClosest(position, maxDistance, node => CanAutoConnect(placed, node, out _));
            if (target != null && CanAutoConnect(placed, target, out AutoConnection connection))
            {
                _editor.Connect(connection.FromNode, connection.FromPort, connection.ToNode, connection.ToPort);
            }
        }

        /// <summary>후보 노드와의 문법상 자동 연결을 구하고 편집 세션에서 실제로 연결 가능한지 반환한다.</summary>
        private bool CanAutoConnect(GraphNode placed, GraphNode candidate, out AutoConnection connection)
        {
            return SpellAutoConnect.TryGetConnection(_editor.Graph, GameData.Runes, placed, candidate, out connection)
                && _editor.CanConnectPorts(connection.FromNode, connection.FromPort, connection.ToNode, connection.ToPort);
        }

        /// <summary>조건을 만족하는 노드 중 기준 좌표에서 최대 거리 안의 가장 가까운 노드를 반환하며 없으면 null을 반환한다.</summary>
        private GraphNode FindClosest(Vector2 position, float maxDistance, Func<GraphNode, bool> predicate)
        {
            GraphNode closest = null;
            float distance = maxDistance;
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                if (!predicate(node))
                {
                    continue;
                }

                float next = Vector2.Distance(position, new Vector2(node.X, node.Y));
                if (next < distance)
                {
                    distance = next;
                    closest = node;
                }
            }
            return closest;
        }

        /// <summary>화면 포인터를 그래프 RectTransform의 로컬 좌표로 변환한다.</summary>
        private Vector2 Local(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 position);
            return position;
        }

        /// <summary>그래프 로컬 좌표에서 확대와 화면 이동을 제거한 노드 좌표를 반환한다.</summary>
        private Vector2 GraphPoint(Vector2 local)
        {
            Vector2 position = (local - TopLeft - _pan) / _zoom;
            return new Vector2(position.x, -position.y);
        }

        /// <summary>노드 좌표에 확대와 화면 이동을 적용한 그래프 로컬 좌표를 반환한다. GraphPoint의 역변환이다.</summary>
        private Vector2 ToLocal(Vector2 graphPoint)
        {
            return TopLeft + _pan + new Vector2(graphPoint.x, -graphPoint.y) * _zoom;
        }

        /// <summary>그래프 노드 ID와 일치하는 노드를 반환한다.</summary>
        private GraphNode FindNode(string id)
        {
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                if (node.Id == id)
                {
                    return node;
                }
            }
            return null;
        }

        /// <summary>그래프 화면 위치에 포함된 가장 앞쪽 노드를 반환한다.</summary>
        private GraphNode FindNodeAt(Vector2 point)
        {
            for (int i = _editor.Graph.Nodes.Count - 1; i >= 0; i--)
            {
                GraphNode node = _editor.Graph.Nodes[i];
                if (NodeRect(node, GameData.Runes.Get(node.RuneId)).Contains(point))
                {
                    return node;
                }
            }
            return null;
        }

        /// <summary>화면 위치에서 판정 반경 안의 첫 연결 포트를 찾아 노드와 포트 정의를 반환한다.</summary>
        private bool FindPort(Vector2 point, out GraphNode foundNode, out PortDefinition foundPort)
        {
            float radius = Mathf.Max(MIN_PORT_HIT_RADIUS, PORT_HIT_RADIUS * _zoom);
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                RuneDefinition rune = GameData.Runes.Get(node.RuneId);
                Rect rect = NodeRect(node, rune);
                foreach (PortDefinition port in rune.Ports)
                {
                    if (Vector2.Distance(point, PortPosition(rect, rune, port.Id)) < radius)
                    {
                        foundNode = node;
                        foundPort = port;
                        return true;
                    }
                }
            }

            foundNode = null;
            foundPort = null;
            return false;
        }

        /// <summary>현재 그래프 노드를 ID로 찾는 조회 테이블을 다시 채운다.</summary>
        private void RebuildNodeLookup()
        {
            _nodeLookup.Clear();
            foreach (GraphNode node in _editor.Graph.Nodes)
            {
                _nodeLookup[node.Id] = node;
            }
        }

        /// <summary>조회 테이블에서 노드를 찾아 포트 중심의 화면 좌표를 반환하며 노드가 없으면 원점을 반환한다. RebuildNodeLookup 이후 호출한다.</summary>
        private Vector2 LookupPortPosition(string nodeId, string portId)
        {
            if (!_nodeLookup.TryGetValue(nodeId, out GraphNode node))
            {
                return Vector2.zero;
            }

            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            return PortPosition(NodeRect(node, rune), rune, portId);
        }

        /// <summary>룬 포트 수로 계산한 배율 적용 전 노드 높이를 반환한다.</summary>
        private static float NodeHeight(RuneDefinition rune)
        {
            return NODE_BASE_HEIGHT + PortRowCount(rune) * PORT_SPACING;
        }

        /// <summary>입력·출력 포트 중 많은 쪽의 개수를 노드의 포트 행 수로 반환한다.</summary>
        private static int PortRowCount(RuneDefinition rune)
        {
            return Math.Max(CountPorts(rune, SpellGrammar.DIRECTION_IN), CountPorts(rune, SpellGrammar.DIRECTION_OUT));
        }

        /// <summary>룬 포트 수와 배율을 반영한 화면상의 노드 사각형을 반환한다.</summary>
        private Rect NodeRect(GraphNode node, RuneDefinition rune)
        {
            float height = NodeHeight(rune) * _zoom;
            Vector2 topLeft = ToLocal(new Vector2(node.X, node.Y));
            return new Rect(topLeft.x, topLeft.y - height, NODE_WIDTH * _zoom, height);
        }

        /// <summary>노드 화면 사각형에서 포트 ID에 대응하는 포트 중심의 화면 좌표를 반환하며 포트가 없으면 노드 중심을 반환한다.</summary>
        private Vector2 PortPosition(Rect rect, RuneDefinition rune, string portId)
        {
            int index = IndexOfPort(rune, portId);
            if (index < 0)
            {
                return rect.center;
            }

            // 같은 방향 포트 중 몇 번째인지가 세로 행 위치가 된다.
            PortDefinition port = rune.Ports[index];
            int row = 0;
            for (int i = 0; i < index; i++)
            {
                if (rune.Ports[i].Direction == port.Direction)
                {
                    row++;
                }
            }

            float x = port.Direction == SpellGrammar.DIRECTION_IN ? rect.xMin : rect.xMax;
            float y = rect.yMax - (PORT_TOP_OFFSET + row * PORT_SPACING) * _zoom;
            return new Vector2(x, y);
        }

        /// <summary>포트 ID와 일치하는 첫 포트의 순번을 반환하며 없으면 -1을 반환한다.</summary>
        private static int IndexOfPort(RuneDefinition rune, string portId)
        {
            for (int i = 0; i < rune.Ports.Count; i++)
            {
                if (rune.Ports[i].Id == portId)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>지정 방향에 속한 룬 포트 개수를 반환한다.</summary>
        private static int CountPorts(RuneDefinition rune, string direction)
        {
            int count = 0;
            foreach (PortDefinition port in rune.Ports)
            {
                if (port.Direction == direction)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>지정 방향의 순번에 해당하는 포트를 반환하며 없으면 null을 반환한다.</summary>
        private static PortDefinition PortAt(RuneDefinition rune, string direction, int row)
        {
            int current = 0;
            foreach (PortDefinition port in rune.Ports)
            {
                if (port.Direction != direction)
                {
                    continue;
                }
                if (current == row)
                {
                    return port;
                }
                current++;
            }
            return null;
        }

        /// <summary>두 점을 포함하는 선택 영역 사각형을 반환한다.</summary>
        private static Rect MakeRect(Vector2 first, Vector2 second)
        {
            return Rect.MinMaxRect(
                Mathf.Min(first.x, second.x),
                Mathf.Min(first.y, second.y),
                Mathf.Max(first.x, second.x),
                Mathf.Max(first.y, second.y));
        }
    }
}
