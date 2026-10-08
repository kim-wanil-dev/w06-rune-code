using System;
using System.Collections.Generic;
using System.Globalization;

using UnityEngine;

using TMPro;

namespace RuneCode
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpellFlowLayer : UnityEngine.UI.MaskableGraphic
    {
        private const int PAID_FLASH_TICKS = 18;
        private const int END_BLINK_PERIOD_TICKS = 6;
        private const int ARC_SEGMENTS = 24;
        private const int CIRCLE_SIDES = 16;
        private const int NO_END_EVENT = -1;
        private const int MULTI_TOKEN_COUNT = 2;
        private const float TOKEN_RADIUS = 5f;
        private const float TOKEN_MIN_RADIUS = 3f;
        private const float MULTI_TOKEN_SCALE = 1.3f;
        private const float TOKEN_RING_WIDTH = 1f;
        private const float JOIN_RING_GAP = 4f;
        private const float JOIN_RING_WIDTH = 1.5f;
        private const float COUNT_DOT_RADIUS = 3f;
        private const float COUNT_DOT_MIN_RADIUS = 2f;
        private const float NODE_BORDER_WIDTH = 2f;
        private const float NODE_BORDER_GAP = 2f;
        private const float LABEL_FONT_SIZE = 11f;
        private const float LABEL_MIN_FONT_SIZE = 8f;
        private const float LABEL_WIDTH = 140f;
        private const float LABEL_HEIGHT = 16f;
        private const float LABEL_GAP = 10f;
        private const string COST_PREFIX = "−";
        private const string BADGE_PREFIX = "×";
        private const string NUMBER_FORMAT = "0.0";
        private const string NEED_SEPARATOR = " / ";
        private const string KEY_NEED = "token.need";
        private const string KEY_HAVE = "token.have";
        private const string KEY_EXPIRED = "token.expired";

        private static readonly Color TOKEN_RING_COLOR = Color.white;
        private static readonly Color JOIN_RING_COLOR = new Color(0.55f, 0.9f, 1f);
        private static readonly Color COUNT_COLOR = new Color(0.8f, 0.95f, 1f);
        private static readonly Color PAID_FLASH_COLOR = new Color(1f, 0.93f, 0.35f);
        private static readonly Color PAID_IDLE_COLOR = new Color(0.55f, 0.66f, 0.78f);
        private static readonly Color UNPAYABLE_COLOR = new Color(1f, 0.26f, 0.29f);
        private static readonly Color EXPIRED_COLOR = new Color(0.6f, 0.6f, 0.6f);

        private readonly List<TextMeshProUGUI> _labels = new List<TextMeshProUGUI>();
        private readonly Dictionary<string, GraphNode> _nodesById = new Dictionary<string, GraphNode>();
        private Func<RuneSimulation> _simulation;
        private Func<SpellGraph> _graph;
        private TMP_FontAsset _font;
        private SpellGraph _indexedGraph;
        private int _indexedNodeCount = -1;
        private int[] _tokenCounts = new int[0];
        private int[] _latestEndIndices = new int[0];
        private int _usedLabels;
        private Vector2 _origin;
        private float _zoom = 1f;

        /// <summary>글꼴, 시험 도크 시뮬레이션 조회 함수, 편집 그래프 조회 함수를 저장하고 포인터 입력을 받지 않도록 설정한다.</summary>
        public void Initialize(TMP_FontAsset font, Func<RuneSimulation> simulation, Func<SpellGraph> graph)
        {
            _font = font;
            _simulation = simulation;
            _graph = graph;
            raycastTarget = false;
        }

        /// <summary>그래프 원점의 로컬 좌표와 화면 배율을 저장하고 다음 프레임에 메시를 다시 그리도록 표시한다.</summary>
        public void SetView(Vector2 origin, float zoom)
        {
            _origin = origin;
            _zoom = zoom;
            SetVerticesDirty();
        }

        /// <summary>매 프레임 메시를 다시 그리고, 노드별 토큰 수와 지불·소멸 기록에 맞춰 라벨을 갱신한다. 실행 상태가 없으면 라벨을 모두 숨긴다.</summary>
        void Update()
        {
            SetVerticesDirty();
            _usedLabels = 0;
            if (TryGetFlowSource(out RuneSimulation sim, out _))
            {
                RefreshNodeStates(sim.Spell);
                UpdateLabels(sim);
            }
            HideUnusedLabels();
        }

        /// <summary>토큰 점, 합류 진행 링, 노드별 토큰 수 점, 지불과 소멸 테두리를 메시로 구성한다. 실행 상태가 없으면 아무것도 그리지 않는다.</summary>
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (!TryGetFlowSource(out RuneSimulation sim, out _)) return;
            RefreshNodeStates(sim.Spell);
            DrawNodeMarkers(mesh, sim);
            DrawTokens(mesh, sim);
        }

        /// <summary>시험 도크 시뮬레이션과 편집 그래프, 실행 그래프가 모두 있으면 true를 반환하고 노드 ID 색인을 필요할 때 다시 만든다.</summary>
        private bool TryGetFlowSource(out RuneSimulation sim, out SpellGraph graph)
        {
            sim = _simulation?.Invoke();
            graph = _graph?.Invoke();
            if (sim == null || graph == null || sim.Spell.Program == null) return false;
            EnsureNodeIndex(graph);
            return true;
        }

        /// <summary>그래프 인스턴스나 노드 수가 바뀐 경우에만 노드 ID와 노드 객체의 사전을 다시 만든다. ID가 없거나 중복된 노드는 건너뛴다.</summary>
        private void EnsureNodeIndex(SpellGraph graph)
        {
            if (graph == _indexedGraph && graph.Nodes.Count == _indexedNodeCount) return;
            _indexedGraph = graph;
            _indexedNodeCount = graph.Nodes.Count;
            _nodesById.Clear();
            for (int index = 0; index < graph.Nodes.Count; index++)
            {
                GraphNode node = graph.Nodes[index];
                if (node == null || string.IsNullOrEmpty(node.Id) || _nodesById.ContainsKey(node.Id)) continue;
                _nodesById.Add(node.Id, node);
            }
        }

        /// <summary>실행 그래프의 노드 색인을 편집 그래프의 노드와 종류로 바꾼다. ID가 현재 그래프에 없거나 종류를 알 수 없으면 false를 반환한다.</summary>
        private bool TryResolveNode(SpellRuntime runtime, int programIndex, out GraphNode node, out SpellNodeKind kind)
        {
            node = null;
            kind = SpellNodeKind.Cast;
            string nodeId = runtime.Program.GetNodeId(programIndex);
            if (nodeId == null || !_nodesById.TryGetValue(nodeId, out node)) return false;
            return GraphLayout.TryGetKind(node, out kind);
        }

        /// <summary>노드별로 지금 있는 토큰 수와 가장 최근 소멸 기록의 색인을 센다. 배열은 노드 수에 맞춰 재사용하며 매번 새로 할당하지 않는다.</summary>
        private void RefreshNodeStates(SpellRuntime runtime)
        {
            int nodeCount = runtime.Program.NodeCount;
            EnsureNodeStateCapacity(nodeCount);
            for (int index = 0; index < nodeCount; index++)
            {
                _tokenCounts[index] = 0;
                _latestEndIndices[index] = NO_END_EVENT;
            }
            IReadOnlyList<SpellToken> tokens = runtime.Tokens;
            for (int index = 0; index < tokens.Count; index++)
            {
                SpellToken token = tokens[index];
                if (!token.IsEnded) _tokenCounts[token.Node]++;
            }
            IReadOnlyList<TokenEndEvent> endEvents = runtime.EndEvents;
            for (int index = 0; index < endEvents.Count; index++)
                _latestEndIndices[endEvents[index].Node] = index;
        }

        /// <summary>노드 수보다 작은 노드별 배열을 새 크기로 다시 만든다. 이미 충분하면 아무것도 하지 않는다.</summary>
        private void EnsureNodeStateCapacity(int nodeCount)
        {
            if (_tokenCounts.Length >= nodeCount) return;
            _tokenCounts = new int[nodeCount];
            _latestEndIndices = new int[nodeCount];
        }

        /// <summary>노드별 지불 직후 테두리와 소멸 깜빡임 테두리, 토큰 수 점을 메시에 그린다.</summary>
        private void DrawNodeMarkers(UnityEngine.UI.VertexHelper mesh, RuneSimulation sim)
        {
            SpellRuntime runtime = sim.Spell;
            int nodeCount = runtime.Program.NodeCount;
            bool isBlinkOn = sim.Tick % END_BLINK_PERIOD_TICKS < END_BLINK_PERIOD_TICKS / 2;
            for (int index = 0; index < nodeCount; index++)
            {
                if (!TryResolveNode(runtime, index, out GraphNode node, out SpellNodeKind kind)) continue;
                Rect rect = GetNodeRect(node, kind);
                int paidTick = runtime.GetLastPaidTick(index);
                if (paidTick >= 0 && sim.Tick - paidTick <= PAID_FLASH_TICKS) DrawRectOutline(mesh, rect, PAID_FLASH_COLOR);
                int endIndex = _latestEndIndices[index];
                if (endIndex != NO_END_EVENT && isBlinkOn)
                {
                    TokenEndReason reason = runtime.EndEvents[endIndex].Reason;
                    DrawRectOutline(mesh, rect, reason == TokenEndReason.Unpayable ? UNPAYABLE_COLOR : EXPIRED_COLOR);
                }
                if (_tokenCounts[index] == 1)
                    RuneMesh.Polygon(mesh, new Vector2(rect.xMax, rect.yMax), Mathf.Max(COUNT_DOT_MIN_RADIUS, COUNT_DOT_RADIUS * _zoom), COUNT_COLOR, CIRCLE_SIDES);
            }
        }

        /// <summary>현재 실행 중인 토큰을 생성 순서대로 그린다. 합류 대기 토큰은 입력 포트에, 나머지는 나갈 엣지 위에 그린다.</summary>
        private void DrawTokens(UnityEngine.UI.VertexHelper mesh, RuneSimulation sim)
        {
            SpellRuntime runtime = sim.Spell;
            IReadOnlyList<SpellToken> tokens = runtime.Tokens;
            float joinMaxWait = GameData.Balance.Spell.JoinMaxWait;
            for (int index = 0; index < tokens.Count; index++)
            {
                SpellToken token = tokens[index];
                if (token.IsEnded) continue;
                if (token.IsJoinWaiting)
                {
                    DrawJoinWaitingToken(mesh, runtime, token, joinMaxWait);
                    continue;
                }
                if (!TryGetTokenPoint(runtime, token, out Vector2 point)) continue;
                DrawTokenDot(mesh, point, token);
            }
        }

        /// <summary>합류 대기 토큰을 대기 중인 입력 포트에 그리고, 남은 대기 비율만큼 줄어드는 링을 그린다.</summary>
        private void DrawJoinWaitingToken(UnityEngine.UI.VertexHelper mesh, SpellRuntime runtime, SpellToken token, float joinMaxWait)
        {
            if (!TryResolveNode(runtime, token.Node, out GraphNode node, out SpellNodeKind kind)) return;
            Vector2 port = GetPortPoint(node, kind, false, token.JoinPort);
            DrawTokenDot(mesh, port, token);
            float remaining = 1f - Mathf.Clamp01((float)(token.JoinWait / joinMaxWait));
            DrawArc(mesh, port, GetTokenRadius(token) + JOIN_RING_GAP * _zoom, JOIN_RING_WIDTH, JOIN_RING_COLOR, remaining);
        }

        /// <summary>토큰 위치에 세대 색 점과 흰 테두리를 그린다. 개수가 두 개 이상이면 점을 조금 키운다.</summary>
        private void DrawTokenDot(UnityEngine.UI.VertexHelper mesh, Vector2 point, SpellToken token)
        {
            float radius = GetTokenRadius(token);
            RuneMesh.Polygon(mesh, point, radius, RuneArenaGraphic.GenerationColor(token.Generation), CIRCLE_SIDES);
            RuneMesh.Ring(mesh, point, radius, TOKEN_RING_WIDTH, TOKEN_RING_COLOR, CIRCLE_SIDES);
        }

        /// <summary>토큰이 지금 있는 위치를 화면 좌표로 구한다. 나갈 엣지가 있으면 머무는 진행률만큼 엣지 곡선 위에, 없으면 노드 오른쪽 가운데에 둔다. 노드를 찾지 못하면 false를 반환한다.</summary>
        private bool TryGetTokenPoint(SpellRuntime runtime, SpellToken token, out Vector2 point)
        {
            point = Vector2.zero;
            if (!TryResolveNode(runtime, token.Node, out GraphNode node, out SpellNodeKind kind)) return false;
            if (!runtime.Program.TryGetNext(token.Node, token.NextPort, out int target, out int inPort))
            {
                Rect rect = GetNodeRect(node, kind);
                point = new Vector2(rect.xMax, rect.center.y);
                return true;
            }
            if (!TryResolveNode(runtime, target, out GraphNode targetNode, out SpellNodeKind targetKind)) return false;
            Vector2 from = GetPortPoint(node, kind, true, token.NextPort);
            Vector2 to = GetPortPoint(targetNode, targetKind, false, inPort);
            bool isLooping = GraphLayout.IsLoopingEdge(from, to, target == token.Node);
            float progress = GetDwellProgress(runtime.Program.GetDwell(token.Node), token.RemainingWait);
            point = GraphLayout.GetEdgePoint(from, to, isLooping, GraphLayout.GetEdgeLift(kind), _zoom, progress);
            return true;
        }

        /// <summary>남은 대기 시간으로 머무는 진행률을 0에서 1 사이로 구한다. 머무는 시간이 0 이하이면 1을 반환한다.</summary>
        private static float GetDwellProgress(double dwell, double remainingWait)
        {
            if (dwell <= 0.0) return 1f;
            return Mathf.Clamp01((float)(1.0 - remainingWait / dwell));
        }

        /// <summary>토큰 점의 화면 반지름을 반환한다. 개수가 두 개 이상이면 기본 반지름에 배율을 곱한다.</summary>
        private float GetTokenRadius(SpellToken token)
        {
            float radius = Mathf.Max(TOKEN_MIN_RADIUS, TOKEN_RADIUS * _zoom);
            return token.Count >= MULTI_TOKEN_COUNT ? radius * MULTI_TOKEN_SCALE : radius;
        }

        /// <summary>노드 그래프 좌표로 화면 사각형을 구한다. 위쪽 왼쪽 점은 캔버스와 같은 규칙으로 정하며 높이는 노드 종류의 크기이다.</summary>
        private Rect GetNodeRect(GraphNode node, SpellNodeKind kind)
        {
            Vector2 topLeft = GraphLayout.ToView(_origin, _zoom, node.X, node.Y);
            Vector2 size = GraphLayout.GetNodeSize(kind) * _zoom;
            return new Rect(topLeft.x, topLeft.y - size.y, size.x, size.y);
        }

        /// <summary>노드의 입력 또는 출력 포트 중심을 화면 좌표로 구한다.</summary>
        private Vector2 GetPortPoint(GraphNode node, SpellNodeKind kind, bool isOutput, int index)
        {
            Vector2 offset = GraphLayout.GetPortOffset(kind, isOutput, index);
            return GraphLayout.ToView(_origin, _zoom, node.X + offset.x, node.Y + offset.y);
        }

        /// <summary>노드 사각형 바깥 가장자리에 지정 색의 얇은 테두리를 네 변으로 그린다.</summary>
        private static void DrawRectOutline(UnityEngine.UI.VertexHelper mesh, Rect rect, Color color)
        {
            Rect outer = Rect.MinMaxRect(rect.xMin - NODE_BORDER_GAP, rect.yMin - NODE_BORDER_GAP,
                rect.xMax + NODE_BORDER_GAP, rect.yMax + NODE_BORDER_GAP);
            Vector2 bottomLeft = new Vector2(outer.xMin, outer.yMin);
            Vector2 topLeft = new Vector2(outer.xMin, outer.yMax);
            Vector2 topRight = new Vector2(outer.xMax, outer.yMax);
            Vector2 bottomRight = new Vector2(outer.xMax, outer.yMin);
            RuneMesh.Line(mesh, bottomLeft, topLeft, NODE_BORDER_WIDTH, color);
            RuneMesh.Line(mesh, topLeft, topRight, NODE_BORDER_WIDTH, color);
            RuneMesh.Line(mesh, topRight, bottomRight, NODE_BORDER_WIDTH, color);
            RuneMesh.Line(mesh, bottomRight, bottomLeft, NODE_BORDER_WIDTH, color);
        }

        /// <summary>중심과 반경으로 위쪽에서 시계 방향으로 fraction 비율만큼 호를 세그먼트 선으로 그린다.</summary>
        private static void DrawArc(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius, float width, Color color, float fraction)
        {
            int count = Mathf.RoundToInt(ARC_SEGMENTS * fraction);
            float step = Mathf.PI * 2f / ARC_SEGMENTS;
            for (int index = 0; index < count; index++)
            {
                float first = Mathf.PI * 0.5f - index * step;
                float second = first - step;
                RuneMesh.Line(mesh, center + new Vector2(Mathf.Cos(first), Mathf.Sin(first)) * radius,
                    center + new Vector2(Mathf.Cos(second), Mathf.Sin(second)) * radius, width, color);
            }
        }

        /// <summary>편집 그래프와 실행 상태를 읽어 노드별 라벨(토큰 수 배지, 마지막 지불 비용, 소멸 문구)을 표시한다.</summary>
        private void UpdateLabels(RuneSimulation sim)
        {
            SpellRuntime runtime = sim.Spell;
            int nodeCount = runtime.Program.NodeCount;
            for (int index = 0; index < nodeCount; index++)
            {
                if (!TryResolveNode(runtime, index, out GraphNode node, out SpellNodeKind kind)) continue;
                Rect rect = GetNodeRect(node, kind);
                if (_tokenCounts[index] >= MULTI_TOKEN_COUNT)
                    ShowLabel(new Vector2(rect.xMax, rect.yMax), BADGE_PREFIX + _tokenCounts[index].ToString(CultureInfo.InvariantCulture), COUNT_COLOR);
                int paidTick = runtime.GetLastPaidTick(index);
                if (paidTick >= 0)
                {
                    bool isFresh = sim.Tick - paidTick <= PAID_FLASH_TICKS;
                    string cost = COST_PREFIX + runtime.GetLastPaidCost(index).ToString(NUMBER_FORMAT, CultureInfo.InvariantCulture);
                    ShowLabel(new Vector2(rect.center.x, rect.yMin - LABEL_GAP * _zoom), cost, isFresh ? PAID_FLASH_COLOR : PAID_IDLE_COLOR);
                }
                int endIndex = _latestEndIndices[index];
                if (endIndex != NO_END_EVENT) ShowEndLabel(runtime.EndEvents[endIndex], rect);
            }
        }

        /// <summary>소멸 기록을 노드 위 문구로 표시한다. 지불 불가는 필요와 보유 마나, 수명 초과는 만료 문구이다.</summary>
        private void ShowEndLabel(TokenEndEvent endEvent, Rect rect)
        {
            Vector2 center = new Vector2(rect.center.x, rect.yMax + LABEL_GAP * _zoom);
            if (endEvent.Reason != TokenEndReason.Unpayable)
            {
                ShowLabel(center, GameData.L(KEY_EXPIRED), EXPIRED_COLOR);
                return;
            }
            string text = GameData.L(KEY_NEED) + " " + endEvent.Need.ToString(NUMBER_FORMAT, CultureInfo.InvariantCulture)
                + NEED_SEPARATOR + GameData.L(KEY_HAVE) + " " + endEvent.Have.ToString(NUMBER_FORMAT, CultureInfo.InvariantCulture);
            ShowLabel(center, text, UNPAYABLE_COLOR);
        }

        /// <summary>라벨 풀에서 다음 라벨을 꺼내 가운데 정렬 위치와 글자, 색, 크기를 정하고 켠다.</summary>
        private void ShowLabel(Vector2 center, string text, Color color)
        {
            TextMeshProUGUI label = GetLabel(_usedLabels);
            _usedLabels++;
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchoredPosition = ToTopLeft(center);
            labelRect.sizeDelta = new Vector2(LABEL_WIDTH * _zoom, LABEL_HEIGHT * _zoom);
            label.fontSize = Mathf.Max(LABEL_MIN_FONT_SIZE, LABEL_FONT_SIZE * _zoom);
            label.color = color;
            label.text = text;
        }

        /// <summary>풀의 index번째 라벨을 반환하고, 없으면 레이어의 자식으로 새로 만든다.</summary>
        private TextMeshProUGUI GetLabel(int index)
        {
            if (index < _labels.Count) return _labels[index];
            var target = new GameObject("FlowLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            target.transform.SetParent(transform, false);
            TextMeshProUGUI label = target.GetComponent<TextMeshProUGUI>();
            label.font = _font;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.Center;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0, 1);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            _labels.Add(label);
            return label;
        }

        /// <summary>이번 프레임에 쓰지 않은 풀의 라벨을 끈다.</summary>
        private void HideUnusedLabels()
        {
            for (int index = _usedLabels; index < _labels.Count; index++)
                if (_labels[index].gameObject.activeSelf) _labels[index].gameObject.SetActive(false);
        }

        /// <summary>레이어 로컬 좌표를 왼쪽 위를 기준으로 한 anchoredPosition 값으로 바꿔 반환한다.</summary>
        private Vector2 ToTopLeft(Vector2 local)
        {
            return new Vector2(local.x - rectTransform.rect.xMin, local.y - rectTransform.rect.yMax);
        }
    }
}
