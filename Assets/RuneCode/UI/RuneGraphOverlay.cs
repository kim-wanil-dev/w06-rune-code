using System;
using System.Globalization;

using UnityEngine;

using TMPro;

namespace RuneCode
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RuneGraphOverlay : UnityEngine.UI.MaskableGraphic
    {
        private const int CURVE_SEGMENTS = 12;
        private const float LAYOUT_PADDING = 10f;
        private const float MIN_ZOOM = 0.15f;
        private const float MAX_ZOOM = 0.8f;
        private const float FRAME_WIDTH = 1f;
        private const float EDGE_MIN_WIDTH = 1f;
        private const float EDGE_WIDTH_SCALE = 1.5f;
        private const float ACCENT_HEIGHT = 3f;

        private static readonly Color PANEL_COLOR = new Color(0.03f, 0.06f, 0.10f, 0.82f);
        private static readonly Color FRAME_COLOR = new Color(0.25f, 0.45f, 0.55f);
        private static readonly Color EDGE_COLOR = new Color(0.2f, 0.7f, 0.82f, 0.8f);
        private static readonly Color NODE_BODY_COLOR = new Color(0.075f, 0.12f, 0.18f);
        private static readonly Color UNREACHABLE_ACCENT_COLOR = new Color(0.32f, 0.36f, 0.40f);

        private readonly Vector2[] _curvePoints = new Vector2[CURVE_SEGMENTS + 1];
        private Func<SpellGraph> _graph;
        private Func<SpellProgram> _program;
        private SpellFlowLayer _flowLayer;
        private SpellGraph _layoutGraph;
        private int _layoutNodeCount;
        private float _layoutCoordinateSum;
        private Vector2 _origin;
        private float _zoom;

        /// <summary>패널 바탕을 포인터 차단 없이 설정하고, 토큰 흐름을 그릴 SpellFlowLayer 자식을 부모 크기로 만들어 연결한다.</summary>
        public void Initialize(TMP_FontAsset font, Func<RuneSimulation> simulation, Func<SpellGraph> graph, Func<SpellProgram> program)
        {
            raycastTarget = false;
            _graph = graph;
            _program = program;
            var layerObject = new GameObject("SpellFlowLayer", typeof(RectTransform));
            layerObject.transform.SetParent(transform, false);
            RectTransform layerRect = layerObject.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = Vector2.zero;
            layerRect.offsetMax = Vector2.zero;
            layerRect.pivot = rectTransform.pivot;
            _flowLayer = layerObject.AddComponent<SpellFlowLayer>();
            _flowLayer.Initialize(font, simulation, graph);
            _flowLayer.raycastTarget = false;
        }

        /// <summary>그래프 인스턴스, 노드 수 또는 노드 좌표 합이 바뀌었을 때만 배치를 다시 계산하고 바탕 메시를 갱신한다.</summary>
        void Update()
        {
            SpellGraph graph = _graph();
            if (!HasGraphChanged(graph)) return;
            RecalculateLayout(graph);
            SetVerticesDirty();
        }

        /// <summary>패널 배경과 테두리, 엣지 곡선, 노드 사각형을 바탕 메시로 구성한다. 배치가 아직 없으면 빈 메시를 만든다.</summary>
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (_layoutGraph == null) return;
            Rect bounds = rectTransform.rect;
            RuneMesh.Rect(mesh, bounds, PANEL_COLOR);
            DrawFrame(mesh, bounds);
            SpellProgram program = _program();
            for (int index = 0; index < _layoutGraph.Edges.Count; index++) DrawEdge(mesh, _layoutGraph.Edges[index]);
            for (int index = 0; index < _layoutGraph.Nodes.Count; index++) DrawNode(mesh, _layoutGraph.Nodes[index], program);
        }

        /// <summary>그래프 인스턴스, 노드 수 또는 노드 좌표 합이 마지막 배치 계산과 다르면 true를 반환한다.</summary>
        private bool HasGraphChanged(SpellGraph graph)
        {
            return graph != _layoutGraph || graph.Nodes.Count != _layoutNodeCount || ComputeCoordinateSum(graph) != _layoutCoordinateSum;
        }

        /// <summary>종류를 알 수 있는 노드 전체가 패널 안에 들어가도록 배율과 원점을 계산하고, 결과를 흐름 레이어에 넘긴다.</summary>
        private void RecalculateLayout(SpellGraph graph)
        {
            float minimumX = float.MaxValue;
            float minimumY = float.MaxValue;
            float maximumX = float.MinValue;
            float maximumY = float.MinValue;
            bool hasNode = false;
            for (int index = 0; index < graph.Nodes.Count; index++)
            {
                GraphNode node = graph.Nodes[index];
                if (!GraphLayout.TryGetKind(node, out SpellNodeKind kind)) continue;
                Vector2 size = GraphLayout.GetNodeSize(kind);
                hasNode = true;
                minimumX = Mathf.Min(minimumX, node.X);
                minimumY = Mathf.Min(minimumY, node.Y);
                maximumX = Mathf.Max(maximumX, node.X + size.x);
                maximumY = Mathf.Max(maximumY, node.Y + size.y);
            }
            if (!hasNode)
            {
                minimumX = 0f; minimumY = 0f; maximumX = 0f; maximumY = 0f;
            }
            float width = Mathf.Max(1f, maximumX - minimumX);
            float height = Mathf.Max(1f, maximumY - minimumY);
            Rect bounds = rectTransform.rect;
            _zoom = Mathf.Clamp(Mathf.Min((bounds.width - 2f * LAYOUT_PADDING) / width, (bounds.height - 2f * LAYOUT_PADDING) / height), MIN_ZOOM, MAX_ZOOM);
            _origin = new Vector2(bounds.xMin + LAYOUT_PADDING - minimumX * _zoom, bounds.yMax - LAYOUT_PADDING + minimumY * _zoom);
            _layoutGraph = graph;
            _layoutNodeCount = graph.Nodes.Count;
            _layoutCoordinateSum = ComputeCoordinateSum(graph);
            _flowLayer.SetView(_origin, _zoom);
        }

        /// <summary>그래프의 모든 노드 X와 Y 좌표를 더한 값을 반환한다. 노드 이동이나 추가를 감지하는 데 쓴다.</summary>
        private static float ComputeCoordinateSum(SpellGraph graph)
        {
            float sum = 0f;
            for (int index = 0; index < graph.Nodes.Count; index++) sum += graph.Nodes[index].X + graph.Nodes[index].Y;
            return sum;
        }

        /// <summary>바탕 사각형의 네 변을 FRAME_WIDTH 두께의 테두리 색으로 그린다.</summary>
        private static void DrawFrame(UnityEngine.UI.VertexHelper mesh, Rect bounds)
        {
            RuneMesh.Rect(mesh, new Rect(bounds.xMin, bounds.yMax - FRAME_WIDTH, bounds.width, FRAME_WIDTH), FRAME_COLOR);
            RuneMesh.Rect(mesh, new Rect(bounds.xMin, bounds.yMin, bounds.width, FRAME_WIDTH), FRAME_COLOR);
            RuneMesh.Rect(mesh, new Rect(bounds.xMin, bounds.yMin, FRAME_WIDTH, bounds.height), FRAME_COLOR);
            RuneMesh.Rect(mesh, new Rect(bounds.xMax - FRAME_WIDTH, bounds.yMin, FRAME_WIDTH, bounds.height), FRAME_COLOR);
        }

        /// <summary>엣지의 출력 포트에서 입력 포트까지를 CURVE_SEGMENTS 개의 선분 곡선으로 그린다. 포트 번호를 읽지 못하거나 범위를 벗어나거나 노드 종류를 알 수 없으면 그리지 않는다.</summary>
        private void DrawEdge(UnityEngine.UI.VertexHelper mesh, GraphEdge edge)
        {
            if (!TryParsePort(edge.FromPort, out int outputIndex) || !TryParsePort(edge.ToPort, out int inputIndex)) return;
            GraphNode fromNode = _layoutGraph.FindNode(edge.FromNode);
            GraphNode toNode = _layoutGraph.FindNode(edge.ToNode);
            if (!GraphLayout.TryGetKind(fromNode, out SpellNodeKind fromKind) || !GraphLayout.TryGetKind(toNode, out SpellNodeKind toKind)) return;
            if (outputIndex >= SpellNodes.GetOutputCount(fromKind) || inputIndex >= SpellNodes.GetInputCount(toKind)) return;
            Vector2 fromOffset = GraphLayout.GetPortOffset(fromKind, true, outputIndex);
            Vector2 toOffset = GraphLayout.GetPortOffset(toKind, false, inputIndex);
            Vector2 from = GraphLayout.ToView(_origin, _zoom, fromNode.X + fromOffset.x, fromNode.Y + fromOffset.y);
            Vector2 to = GraphLayout.ToView(_origin, _zoom, toNode.X + toOffset.x, toNode.Y + toOffset.y);
            bool isLooping = GraphLayout.IsLoopingEdge(from, to, fromNode == toNode);
            float lift = GraphLayout.GetEdgeLift(fromKind);
            for (int index = 0; index <= CURVE_SEGMENTS; index++)
                _curvePoints[index] = GraphLayout.GetEdgePoint(from, to, isLooping, lift, _zoom, (float)index / CURVE_SEGMENTS);
            float width = Mathf.Max(EDGE_MIN_WIDTH, EDGE_WIDTH_SCALE * _zoom);
            for (int index = 0; index < CURVE_SEGMENTS; index++)
                RuneMesh.Line(mesh, _curvePoints[index], _curvePoints[index + 1], width, EDGE_COLOR);
        }

        /// <summary>노드의 본문 사각형과 위쪽 종류 띠를 그린다. 도달할 수 없는 노드의 띠는 회색이다.</summary>
        private void DrawNode(UnityEngine.UI.VertexHelper mesh, GraphNode node, SpellProgram program)
        {
            if (!GraphLayout.TryGetKind(node, out SpellNodeKind kind)) return;
            Vector2 size = GraphLayout.GetNodeSize(kind);
            Vector2 topLeft = GraphLayout.ToView(_origin, _zoom, node.X, node.Y);
            Rect rect = new Rect(topLeft.x, topLeft.y - size.y * _zoom, size.x * _zoom, size.y * _zoom);
            RuneMesh.Rect(mesh, rect, NODE_BODY_COLOR);
            Color accent = IsUnreachable(program, node) ? UNREACHABLE_ACCENT_COLOR : RuneMesh.NodeColor(kind);
            RuneMesh.Rect(mesh, new Rect(rect.xMin, rect.yMax - ACCENT_HEIGHT * _zoom, rect.width, ACCENT_HEIGHT * _zoom), accent);
        }

        /// <summary>프로그램에 색인된 노드가 시전과 적중에서 도달할 수 없으면 true를 반환한다. 프로그램이 없거나 색인에 없으면 false를 반환한다.</summary>
        private static bool IsUnreachable(SpellProgram program, GraphNode node)
        {
            if (program == null) return false;
            int index = program.IndexOf(node.Id);
            return index >= 0 && !program.IsReachable(index);
        }

        /// <summary>포트 번호 문자열을 십진 정수로 읽으며 성공하면 true를 반환한다.</summary>
        private static bool TryParsePort(string text, out int port)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port);
        }
    }
}
