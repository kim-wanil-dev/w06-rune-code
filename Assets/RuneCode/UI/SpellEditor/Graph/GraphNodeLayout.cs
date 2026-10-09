using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 그래프 노드의 화면 크기·포트 위치 계산과 화면 좌표의 노드·포트·연결선 찾기를 맡는다.
    /// 지원 중단 포트는 기존 연결이 있을 때만 표시 포트로 친다.
    /// </summary>
    internal sealed class GraphNodeLayout
    {
        public const float NODE_WIDTH = 160f;
        public const float NODE_BASE_HEIGHT = 39f;

        private const float PORT_SPACING = 23f;
        private const float PORT_TOP_OFFSET = 40f;
        private const float PORT_HIT_RADIUS = 9f;
        private const float MIN_PORT_HIT_RADIUS = 8f;
        private const float EDGE_HIT_DISTANCE = 8f;

        private readonly GraphViewport _viewport;
        private readonly Func<SpellGraph> _graph;
        private readonly Dictionary<string, GraphNode> _nodeLookup = new Dictionary<string, GraphNode>();

        /// <summary>뷰포트와 현재 그래프 조회 함수로 배치 계산기를 만든다.</summary>
        public GraphNodeLayout(GraphViewport viewport, Func<SpellGraph> graph)
        {
            _viewport = viewport;
            _graph = graph;
        }

        /// <summary>현재 그래프 노드를 ID로 찾는 조회 테이블을 다시 채운다. 연결선 위치 조회 전에 호출한다.</summary>
        public void RebuildLookup()
        {
            _nodeLookup.Clear();
            foreach (GraphNode node in _graph().Nodes) _nodeLookup[node.Id] = node;
        }

        /// <summary>노드의 표시 포트 수로 계산한 배율 적용 전 노드 높이를 반환한다.</summary>
        public float NodeHeight(GraphNode node, RuneDefinition rune)
        {
            return NODE_BASE_HEIGHT + PortRowCount(node, rune) * PORT_SPACING;
        }

        /// <summary>입력·출력 표시 포트 중 많은 쪽의 개수를 노드의 포트 행 수로 반환한다.</summary>
        public int PortRowCount(GraphNode node, RuneDefinition rune)
        {
            return Math.Max(CountPorts(node, rune, SpellGrammar.DIRECTION_IN), CountPorts(node, rune, SpellGrammar.DIRECTION_OUT));
        }

        /// <summary>표시 포트 수와 배율을 반영한 캔버스 로컬 노드 사각형을 반환한다.</summary>
        public Rect NodeRect(GraphNode node, RuneDefinition rune)
        {
            float height = NodeHeight(node, rune) * _viewport.Zoom;
            Vector2 topLeft = _viewport.ToLocal(new Vector2(node.X, node.Y));
            return new Rect(topLeft.x, topLeft.y - height, NODE_WIDTH * _viewport.Zoom, height);
        }

        /// <summary>그래프 좌표 기준 모든 노드의 최소·최대 범위를 구한다. 노드가 없으면 false를 반환한다.</summary>
        public bool TryGetGraphBounds(out Vector2 minimum, out Vector2 maximum)
        {
            minimum = new Vector2(float.MaxValue, float.MaxValue);
            maximum = new Vector2(float.MinValue, float.MinValue);
            IReadOnlyList<GraphNode> nodes = _graph().Nodes;
            foreach (GraphNode node in nodes)
            {
                float height = NodeHeight(node, GameData.Runes.Get(node.RuneId));
                minimum = Vector2.Min(minimum, new Vector2(node.X, node.Y));
                maximum = Vector2.Max(maximum, new Vector2(node.X + NODE_WIDTH, node.Y + height));
            }
            return nodes.Count > 0;
        }

        /// <summary>노드 사각형에서 포트 ID에 해당하는 포트 중심 좌표를 반환하며 포트가 없으면 노드 중심을 반환한다.</summary>
        public Vector2 PortPosition(Rect rect, GraphNode node, RuneDefinition rune, string portId)
        {
            int index = IndexOfPort(rune, portId);
            if (index < 0) return rect.center;

            // 같은 방향의 표시 포트 중 몇 번째인지가 세로 행 위치가 된다.
            PortDefinition port = rune.Ports[index];
            int row = 0;
            for (int i = 0; i < index; i++)
            {
                if (rune.Ports[i].Direction == port.Direction && IsPortShown(node, rune.Ports[i])) row++;
            }
            float x = port.Direction == SpellGrammar.DIRECTION_IN ? rect.xMin : rect.xMax;
            float y = rect.yMax - (PORT_TOP_OFFSET + row * PORT_SPACING) * _viewport.Zoom;
            return new Vector2(x, y);
        }

        /// <summary>조회 테이블에서 노드를 찾아 포트 중심 좌표를 반환하며 노드가 없으면 원점을 반환한다. RebuildLookup 이후 호출한다.</summary>
        public Vector2 LookupPortPosition(string nodeId, string portId)
        {
            if (!_nodeLookup.TryGetValue(nodeId, out GraphNode node)) return Vector2.zero;
            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            return PortPosition(NodeRect(node, rune), node, rune, portId);
        }

        /// <summary>노드에 표시할 포트인지 반환한다. 지원 중단 포트는 기존 연결이 남아 있을 때만 표시한다.</summary>
        public bool IsPortShown(GraphNode node, PortDefinition port)
        {
            if (!port.IsDeprecated) return true;
            foreach (GraphEdge edge in _graph().Edges)
            {
                if (edge.FromNode == node.Id && edge.FromPort == port.Id) return true;
            }
            return false;
        }

        /// <summary>연결의 출발 포트가 지원 중단 포트(실행되지 않는 기존 연결)인지 반환한다. RebuildLookup 이후 호출한다.</summary>
        public bool IsDeprecatedEdge(GraphEdge edge)
        {
            return _nodeLookup.TryGetValue(edge.FromNode, out GraphNode node)
                && GameData.Runes.Get(node.RuneId).FindPort(edge.FromPort, SpellGrammar.DIRECTION_OUT)?.IsDeprecated == true;
        }

        /// <summary>지정 방향의 표시 포트 중 순번에 해당하는 포트를 반환하며 없으면 null을 반환한다.</summary>
        public PortDefinition PortAt(GraphNode node, RuneDefinition rune, string direction, int row)
        {
            int current = 0;
            foreach (PortDefinition port in rune.Ports)
            {
                if (port.Direction != direction || !IsPortShown(node, port)) continue;
                if (current == row) return port;
                current++;
            }
            return null;
        }

        /// <summary>그래프 노드 ID와 일치하는 노드를 반환하며 없으면 null을 반환한다.</summary>
        public GraphNode FindNode(string id)
        {
            foreach (GraphNode node in _graph().Nodes)
            {
                if (node.Id == id) return node;
            }
            return null;
        }

        /// <summary>캔버스 로컬 위치에 있는 가장 앞쪽(나중에 그린) 노드를 반환하며 없으면 null을 반환한다.</summary>
        public GraphNode FindNodeAt(Vector2 point)
        {
            IReadOnlyList<GraphNode> nodes = _graph().Nodes;
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                if (NodeRect(nodes[i], GameData.Runes.Get(nodes[i].RuneId)).Contains(point)) return nodes[i];
            }
            return null;
        }

        /// <summary>캔버스 로컬 위치에서 판정 반경 안의 첫 표시 포트를 찾아 노드와 포트 정의를 반환한다.</summary>
        public bool FindPort(Vector2 point, out GraphNode foundNode, out PortDefinition foundPort)
        {
            float radius = Mathf.Max(MIN_PORT_HIT_RADIUS, PORT_HIT_RADIUS * _viewport.Zoom);
            foreach (GraphNode node in _graph().Nodes)
            {
                RuneDefinition rune = GameData.Runes.Get(node.RuneId);
                Rect rect = NodeRect(node, rune);
                foreach (PortDefinition port in rune.Ports)
                {
                    if (IsPortShown(node, port) && Vector2.Distance(point, PortPosition(rect, node, rune, port.Id)) < radius)
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

        /// <summary>캔버스 로컬 위치에서 판정 거리 안의 첫 연결선을 반환하며 없으면 null을 반환한다.</summary>
        public GraphEdge FindEdgeAt(Vector2 point)
        {
            RebuildLookup();
            foreach (GraphEdge edge in _graph().Edges)
            {
                Vector2 from = LookupPortPosition(edge.FromNode, edge.FromPort);
                Vector2 to = LookupPortPosition(edge.ToNode, edge.ToPort);

                // 포인터를 선분에 투영한 가장 가까운 점과의 거리로 판정한다.
                Vector2 span = to - from;
                float t = Mathf.Clamp01(Vector2.Dot(point - from, span) / Mathf.Max(1, span.sqrMagnitude));
                if (Vector2.Distance(point, from + span * t) < EDGE_HIT_DISTANCE) return edge;
            }
            return null;
        }

        /// <summary>포트 ID와 일치하는 첫 포트의 순번을 반환하며 없으면 -1을 반환한다.</summary>
        private static int IndexOfPort(RuneDefinition rune, string portId)
        {
            for (int i = 0; i < rune.Ports.Count; i++)
            {
                if (rune.Ports[i].Id == portId) return i;
            }
            return -1;
        }

        /// <summary>지정 방향에 속한 노드의 표시 포트 개수를 반환한다.</summary>
        private int CountPorts(GraphNode node, RuneDefinition rune, string direction)
        {
            int count = 0;
            foreach (PortDefinition port in rune.Ports)
            {
                if (port.Direction == direction && IsPortShown(node, port)) count++;
            }
            return count;
        }
    }
}
