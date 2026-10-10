using System;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 새 룬의 배치 위치 제안과 배치 후 자동 연결을 맡는다. 문법상 연결 규칙은 SpellAutoConnect,
    /// 실제 연결 가능 여부는 편집 세션의 연결 검사를 따르고, 이 클래스는 거리 정책만 정한다.
    /// </summary>
    internal sealed class GraphPlacement
    {
        private const float AUTO_CONNECT_DISTANCE = 175f;
        private const float AUTO_CHAIN_DISTANCE = 320f;
        private const float CHAIN_PLACEMENT_GAP = 20f;
        private const float PLACEMENT_MARGIN = 12f;
        private const float PLACEMENT_BOTTOM_MARGIN = 90f;

        private static readonly Vector2 MODIFIER_PLACEMENT_OFFSET = new Vector2(-140f, 90f);

        private readonly Func<ISpellEditor> _editor;
        private readonly GraphViewport _viewport;

        /// <summary>편집 세션 조회 함수와 뷰포트로 배치 도우미를 만든다.</summary>
        public GraphPlacement(Func<ISpellEditor> editor, GraphViewport viewport)
        {
            _editor = editor;
            _viewport = viewport;
        }

        /// <summary>현재 보이는 영역 안에서 새 룬을 놓을 그래프 좌표를 반환한다. 효과는 Behavior 아래, Behavior는 Shape의 열린 체인 끝 오른쪽을 우선한다.</summary>
        public Vector2 Suggest(string runeId)
        {
            ISpellEditor editor = _editor();
            RuneDefinition rune = GameData.Runes.Get(runeId);
            Vector2 position = _viewport.GraphPoint(_viewport.Bounds.center);

            // 문법상 기준점(효과를 받을 노드, 열린 체인 끝) 중 마지막 노드 옆을 제안한다.
            foreach (GraphNode node in editor.Graph.Nodes)
            {
                if (!SpellAutoConnect.IsAnchor(editor.Graph, GameData.Runes, rune, node)) continue;
                position = SpellAutoConnect.IsModifier(rune)
                    ? new Vector2(node.X, node.Y) + MODIFIER_PLACEMENT_OFFSET
                    : new Vector2(node.X + GraphNodeLayout.NODE_WIDTH + CHAIN_PLACEMENT_GAP, node.Y);
            }

            // 제안 위치가 화면 밖이면 보이는 영역 안쪽으로 당긴다.
            Rect bounds = _viewport.Bounds;
            float zoom = _viewport.Zoom;
            Vector2 local = _viewport.ToLocal(position);
            local.x = Mathf.Clamp(local.x, bounds.xMin + PLACEMENT_MARGIN, bounds.xMax - (GraphNodeLayout.NODE_WIDTH + PLACEMENT_MARGIN) * zoom);
            local.y = Mathf.Clamp(local.y, bounds.yMin + PLACEMENT_BOTTOM_MARGIN * zoom, bounds.yMax - PLACEMENT_MARGIN);
            return _viewport.GraphPoint(local);
        }

        /// <summary>그래프 좌표에 룬을 지정 등급으로 배치하고 효과·Behavior를 가까운 대상에 자동 연결한다.</summary>
        public void Place(string runeId, Vector2 position, string grade = null)
        {
            ISpellEditor editor = _editor();
            int oldCount = editor.Graph.Nodes.Count;
            editor.AddRune(runeId, position.x, position.y, grade);
            if (editor.Graph.Nodes.Count == oldCount) return;

            RuneDefinition rune = GameData.Runes.Get(runeId);
            GraphNode placed = editor.Graph.Nodes[editor.Graph.Nodes.Count - 1];
            if (SpellAutoConnect.IsModifier(rune)) AutoConnect(editor, placed, position, AUTO_CONNECT_DISTANCE);
            else if (SpellAutoConnect.IsChainLink(rune)) AutoConnect(editor, placed, position, AUTO_CHAIN_DISTANCE);
        }

        /// <summary>배치한 노드와 자동 연결할 수 있는 노드 중 거리 안의 가장 가까운 노드에 연결한다.</summary>
        private static void AutoConnect(ISpellEditor editor, GraphNode placed, Vector2 position, float maxDistance)
        {
            GraphNode closest = null;
            float distance = maxDistance;
            foreach (GraphNode node in editor.Graph.Nodes)
            {
                if (!CanAutoConnect(editor, placed, node, out _)) continue;
                float next = Vector2.Distance(position, new Vector2(node.X, node.Y));
                if (next >= distance) continue;
                distance = next;
                closest = node;
            }
            if (closest != null && CanAutoConnect(editor, placed, closest, out AutoConnection connection))
                editor.Connect(connection.FromNode, connection.FromPort, connection.ToNode, connection.ToPort);
        }

        /// <summary>후보 노드와의 문법상 자동 연결을 구하고 편집 세션에서 실제로 연결 가능한지 반환한다.</summary>
        private static bool CanAutoConnect(ISpellEditor editor, GraphNode placed, GraphNode candidate, out AutoConnection connection)
        {
            return SpellAutoConnect.TryGetConnection(editor.Graph, GameData.Runes, placed, candidate, out connection)
                && editor.CanConnectPorts(connection.FromNode, connection.FromPort, connection.ToNode, connection.ToPort);
        }
    }
}
