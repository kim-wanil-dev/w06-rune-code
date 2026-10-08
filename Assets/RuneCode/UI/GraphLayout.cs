using UnityEngine;

namespace RuneCode
{
    public static class GraphLayout
    {
        public const float NODE_WIDTH = 160f;
        public const float HEADER_HEIGHT = 58f;
        public const float PORT_SPACING = 23f;
        public const float PORT_TOP = 12f;

        private const float CURVE_MIN_HANDLE = 60f;
        private const float CURVE_HANDLE_RATIO = 0.5f;
        private const float CURVE_LOOP_MARGIN = 40f;

        /// <summary>노드 종류의 포트 행 수를 입력과 출력 중 많은 쪽으로 정하며 최소 1행을 반환한다.</summary>
        public static int GetRowCount(SpellNodeKind kind)
        {
            return Mathf.Max(1, Mathf.Max(SpellNodes.GetInputCount(kind), SpellNodes.GetOutputCount(kind)));
        }

        /// <summary>노드 종류의 그래프 단위 크기(너비, 높이)를 헤더와 포트 행 수로 계산해 반환한다.</summary>
        public static Vector2 GetNodeSize(SpellNodeKind kind)
        {
            return new Vector2(NODE_WIDTH, HEADER_HEIGHT + GetRowCount(kind) * PORT_SPACING);
        }

        /// <summary>노드 왼쪽 위를 기준으로 한 포트 중심의 그래프 단위 오프셋을 반환한다. 출력 포트는 오른쪽 가장자리, 입력 포트는 왼쪽 가장자리에 있다.</summary>
        public static Vector2 GetPortOffset(SpellNodeKind kind, bool isOutput, int index)
        {
            return new Vector2(isOutput ? NODE_WIDTH : 0f, HEADER_HEIGHT + PORT_TOP + index * PORT_SPACING);
        }

        /// <summary>노드의 룬 ID를 노드 종류로 바꾼다. 알 수 없는 ID나 null 노드이면 false를 반환한다.</summary>
        public static bool TryGetKind(GraphNode node, out SpellNodeKind kind)
        {
            if (node == null)
            {
                kind = SpellNodeKind.Cast;
                return false;
            }
            return SpellNodes.TryParse(node.RuneId, out kind);
        }

        /// <summary>그래프 좌표(y가 아래로 증가)를 그래프 원점의 로컬 좌표와 배율로 바꿔 화면 좌표(y가 위로 증가)를 반환한다.</summary>
        public static Vector2 ToView(Vector2 origin, float zoom, float graphX, float graphY)
        {
            return origin + new Vector2(graphX * zoom, -graphY * zoom);
        }

        /// <summary>출발 노드 종류의 되돌아가는 곡선을 위로 올리는 높이를 그래프 단위로 반환한다. 노드 높이의 절반에 여백을 더한 값이며 배율은 곱하지 않는다.</summary>
        public static float GetEdgeLift(SpellNodeKind fromKind)
        {
            return GetNodeSize(fromKind).y * 0.5f + CURVE_LOOP_MARGIN;
        }

        /// <summary>출발과 도착의 화면 좌표, 같은 노드 여부로 되돌아가는 엣지인지 판정해 반환한다. 도착이 출발보다 왼쪽이거나 같은 노드이면 true이다.</summary>
        public static bool IsLoopingEdge(Vector2 from, Vector2 to, bool isSameNode)
        {
            return to.x < from.x || isSameNode;
        }

        /// <summary>출발과 도착의 화면 좌표로 3차 베지어 곡선을 만들고 t 위치의 화면 좌표를 반환한다. 되돌아가는 곡선이면 lift에 배율을 곱한 만큼 두 제어점을 위로 올린다.</summary>
        public static Vector2 GetEdgePoint(Vector2 from, Vector2 to, bool isLooping, float lift, float zoom, float t)
        {
            GetControlPoints(from, to, isLooping, lift, zoom, out Vector2 first, out Vector2 second);
            return GetBezierPoint(from, first, second, to, t);
        }

        /// <summary>출발과 도착의 화면 좌표로 만든 3차 베지어 곡선의 t 위치 접선 벡터를 반환한다. 제어점 규칙은 GetEdgePoint와 같다.</summary>
        public static Vector2 GetEdgeTangent(Vector2 from, Vector2 to, bool isLooping, float lift, float zoom, float t)
        {
            GetControlPoints(from, to, isLooping, lift, zoom, out Vector2 first, out Vector2 second);
            return GetBezierTangent(from, first, second, to, t);
        }

        /// <summary>출발과 도착 사이의 가로 거리에 맞춰 두 제어점을 계산하며, 되돌아가는 곡선이면 두 제어점을 위로 올린다.</summary>
        private static void GetControlPoints(Vector2 from, Vector2 to, bool isLooping, float lift, float zoom, out Vector2 first, out Vector2 second)
        {
            float handle = Mathf.Max(CURVE_MIN_HANDLE * zoom, Mathf.Abs(to.x - from.x) * CURVE_HANDLE_RATIO);
            Vector2 up = new Vector2(0f, isLooping ? lift * zoom : 0f);
            first = from + new Vector2(handle, 0f) + up;
            second = to - new Vector2(handle, 0f) + up;
        }

        /// <summary>네 제어점으로 이루어진 3차 베지어 곡선의 t 위치 점을 반환한다.</summary>
        private static Vector2 GetBezierPoint(Vector2 from, Vector2 first, Vector2 second, Vector2 to, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * inverse * from + 3f * inverse * inverse * t * first
                + 3f * inverse * t * t * second + t * t * t * to;
        }

        /// <summary>네 제어점으로 이루어진 3차 베지어 곡선의 t 위치에서 접선 벡터를 반환한다.</summary>
        private static Vector2 GetBezierTangent(Vector2 from, Vector2 first, Vector2 second, Vector2 to, float t)
        {
            float inverse = 1f - t;
            return 3f * inverse * inverse * (first - from) + 6f * inverse * t * (second - first) + 3f * t * t * (to - second);
        }
    }
}
