using UnityEngine;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 그래프 편집 화면의 메시를 그린다. 배경 격자, 연결선·화살표, 노드 본체·포트, 연결 미리보기선, 영역 선택 사각형 순서다.
    /// 선택·하이라이트·연결 드래그 상태는 GraphPointerState에서 읽는다.
    /// </summary>
    internal sealed class GraphMeshPainter
    {
        private const float NODE_BORDER = 2f;
        private const float ACCENT_BAR_HEIGHT = 4f;
        private const float ICON_OFFSET_X = 5f;
        private const float ICON_OFFSET_Y = 17f;
        private const float ICON_RADIUS = 3f;
        private const float PORT_RADIUS = 5f;
        private const float GRID_STEP = 28f;
        private const float GRID_DOT_SIZE = 1.5f;
        private const float EDGE_WIDTH = 2.4f;
        private const float EDGE_ARROW_SIZE = 4.5f;
        private const float EDGE_ARROW_POSITION = 0.68f;
        private const float SELECTION_LINE_WIDTH = 1f;

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
        private static readonly Color DEPRECATED_COLOR = new Color(0.45f, 0.47f, 0.50f);
        private static readonly Color SELECTION_FILL_COLOR = new Color(0.24f, 0.80f, 1f, 0.12f);

        private readonly GraphViewport _viewport;
        private readonly GraphNodeLayout _layout;
        private readonly GraphPointerState _state;

        /// <summary>뷰포트·노드 배치·편집 상태로 그리기 도구를 만든다.</summary>
        public GraphMeshPainter(GraphViewport viewport, GraphNodeLayout layout, GraphPointerState state)
        {
            _viewport = viewport;
            _layout = layout;
            _state = state;
        }

        /// <summary>배경과, 그래프가 있으면 연결선·노드·연결 미리보기·영역 선택을 메시에 그린다.</summary>
        public void Paint(VertexHelper mesh, SpellGraph graph)
        {
            mesh.Clear();
            DrawBackground(mesh);
            if (graph == null) return;
            _layout.RebuildLookup();
            DrawEdges(mesh, graph);
            foreach (GraphNode node in graph.Nodes) DrawNode(mesh, node);
            DrawConnectionPreview(mesh);
            DrawBoxSelection(mesh);
        }

        /// <summary>배경과 화면 이동·배율에 맞춘 격자 점을 그린다.</summary>
        private void DrawBackground(VertexHelper mesh)
        {
            Rect bounds = _viewport.Bounds;
            RuneMesh.Rect(mesh, bounds, BACKGROUND_COLOR);
            float step = GRID_STEP * _viewport.Zoom;
            for (float x = bounds.xMin + _viewport.Pan.x % step; x < bounds.xMax; x += step)
            {
                for (float y = bounds.yMax + _viewport.Pan.y % step; y > bounds.yMin; y -= step)
                    RuneMesh.Rect(mesh, new Rect(x, y, GRID_DOT_SIZE, GRID_DOT_SIZE), GRID_COLOR);
            }
        }

        /// <summary>모든 연결선과 방향 화살표를 그린다. 지원 중단 연결은 회색이다.</summary>
        private void DrawEdges(VertexHelper mesh, SpellGraph graph)
        {
            foreach (GraphEdge edge in graph.Edges)
            {
                Vector2 from = _layout.LookupPortPosition(edge.FromNode, edge.FromPort);
                Vector2 to = _layout.LookupPortPosition(edge.ToNode, edge.ToPort);
                RuneMesh.Line(mesh, from, to, EDGE_WIDTH, _layout.IsDeprecatedEdge(edge) ? DEPRECATED_COLOR : EdgeColor(edge));
                Vector2 direction = (to - from).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x);
                RuneMesh.Polygon(mesh, Vector2.Lerp(from, to, EDGE_ARROW_POSITION), EDGE_ARROW_SIZE, EDGE_ARROW_COLOR, 3, angle);
            }
        }

        /// <summary>노드 테두리·본체·카테고리 강조 막대·아이콘과 포트를 그린다.</summary>
        private void DrawNode(VertexHelper mesh, GraphNode node)
        {
            float zoom = _viewport.Zoom;
            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            Color accent = RuneMesh.CategoryColor(rune.Category);
            Rect rect = _layout.NodeRect(node, rune);
            bool isExecuted = _state.ExecutedUntil.TryGetValue(node.Id, out float until) && until > Time.unscaledTime;

            Rect border = new Rect(rect.x - NODE_BORDER, rect.y - NODE_BORDER, rect.width + NODE_BORDER * 2, rect.height + NODE_BORDER * 2);
            RuneMesh.Rect(mesh, border, NodeBorderColor(node, accent, isExecuted));
            RuneMesh.Rect(mesh, rect, isExecuted ? EXECUTED_FILL_COLOR : NODE_FILL_COLOR);
            RuneMesh.Rect(mesh, new Rect(rect.x, rect.yMax - ACCENT_BAR_HEIGHT * zoom, rect.width, ACCENT_BAR_HEIGHT * zoom), accent);
            Vector2 iconCenter = new Vector2(rect.x + ICON_OFFSET_X * zoom, rect.yMax - ICON_OFFSET_Y * zoom);
            RuneMesh.Polygon(mesh, iconCenter, ICON_RADIUS * zoom, accent, CategoryIconSides(rune.Category));
            DrawPorts(mesh, node, rune, rect);
        }

        /// <summary>노드의 표시 포트를 종류별 모양으로 그리며 연결 드래그 중에는 연결 가능 여부 색으로 표시한다. 지원 중단 포트는 회색이다.</summary>
        private void DrawPorts(VertexHelper mesh, GraphNode node, RuneDefinition rune, Rect rect)
        {
            _state.ConnectablePorts.TryGetValue(node.Id, out bool[] connectable);
            for (int i = 0; i < rune.Ports.Count; i++)
            {
                PortDefinition port = rune.Ports[i];
                if (!_layout.IsPortShown(node, port)) continue;
                Color color = port.IsDeprecated ? DEPRECATED_COLOR : PortBaseColor(port.Kind);
                if (_state.IsConnecting) color = connectable != null && connectable[i] ? CONNECTABLE_PORT_COLOR : BLOCKED_PORT_COLOR;
                Vector2 point = _layout.PortPosition(rect, node, rune, port.Id);
                RuneMesh.Polygon(mesh, point, PORT_RADIUS * _viewport.Zoom, color, PortSides(port.Kind));
            }
        }

        /// <summary>연결 드래그 중이면 시작 포트에서 포인터까지 미리보기 선을 그린다.</summary>
        private void DrawConnectionPreview(VertexHelper mesh)
        {
            if (!_state.IsConnecting) return;
            RuneMesh.Line(mesh, _layout.LookupPortPosition(_state.SourceNode, _state.SourcePort), _state.Pointer, EDGE_WIDTH, Color.white);
        }

        /// <summary>영역 선택 중이면 선택 사각형과 위·아래 경계선을 그린다.</summary>
        private void DrawBoxSelection(VertexHelper mesh)
        {
            if (!_state.IsBoxSelecting) return;
            Rect selection = _state.SelectionRect;
            RuneMesh.Rect(mesh, selection, SELECTION_FILL_COLOR);
            RuneMesh.Line(mesh, new Vector2(selection.xMin, selection.yMin), new Vector2(selection.xMax, selection.yMin), SELECTION_LINE_WIDTH, Color.cyan);
            RuneMesh.Line(mesh, new Vector2(selection.xMin, selection.yMax), new Vector2(selection.xMax, selection.yMax), SELECTION_LINE_WIDTH, Color.cyan);
        }

        /// <summary>실행 하이라이트, 선택 여부 순으로 노드 테두리 색을 반환한다.</summary>
        private Color NodeBorderColor(GraphNode node, Color accent, bool isExecuted)
        {
            if (isExecuted) return EXECUTED_BORDER_COLOR;
            return _state.Selected.Contains(node.Id) ? accent : NODE_BORDER_COLOR;
        }

        /// <summary>효과 연결, 체인 연결, 실행 연결 순으로 연결선 색을 반환한다.</summary>
        private static Color EdgeColor(GraphEdge edge)
        {
            if (edge.FromPort == SpellGrammar.MODIFIER_PORT) return MODIFIER_EDGE_COLOR;
            if (edge.ToPort == SpellGrammar.CHAIN_IN) return CHAIN_COLOR;
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
    }
}
