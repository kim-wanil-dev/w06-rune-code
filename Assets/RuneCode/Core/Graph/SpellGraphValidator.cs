using System;
using System.Collections.Generic;

namespace RuneCode
{
    /// <summary>
    /// 외부에서 들어온 마법 그래프(공유 코드·저장 파일·템플릿)의 구조를 검증한다.
    /// 크기 한도, 필수 값, ID 허용 문자, 중복 ID, 존재하지 않는 노드 참조, 유한한 숫자를 확인한다.
    /// 문법상 연결 가능 여부는 GraphCompiler가 따로 검사한다.
    /// </summary>
    public static class SpellGraphValidator
    {
        public const int MAX_NODE_COUNT = 256;
        public const int MAX_EDGE_COUNT = 1024;
        public const int MAX_PARAMETER_COUNT = 16;
        public const int MAX_TEXT_LENGTH = 80;

        /// <summary>그래프가 구조 규칙을 모두 지키면 true를 반환한다.</summary>
        public static bool IsValid(SpellGraph graph)
        {
            if (graph == null || graph.Version < 1 || graph.Version > SpellGraph.CURRENT_VERSION || !IsIdentifier(graph.Id)
                || string.IsNullOrWhiteSpace(graph.Name) || graph.Name.Length > MAX_TEXT_LENGTH
                || graph.Nodes == null || graph.Edges == null
                || graph.Nodes.Count == 0 || graph.Nodes.Count > MAX_NODE_COUNT || graph.Edges.Count > MAX_EDGE_COUNT)
            {
                return false;
            }

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GraphNode node in graph.Nodes)
            {
                if (node == null || !IsIdentifier(node.Id) || !IsIdentifier(node.RuneId) || !nodeIds.Add(node.Id)
                    || !IsFinite(node.X) || !IsFinite(node.Y) || node.Params == null || node.Params.Count > MAX_PARAMETER_COUNT)
                {
                    return false;
                }

                var paramKeys = new HashSet<string>();
                foreach (NodeParameter param in node.Params)
                {
                    if (param == null || !IsIdentifier(param.Key) || !paramKeys.Add(param.Key)
                        || !IsFinite(param.Number) || (param.Text != null && param.Text.Length > MAX_TEXT_LENGTH))
                    {
                        return false;
                    }
                }
            }

            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GraphEdge edge in graph.Edges)
            {
                if (edge == null || !IsIdentifier(edge.Id) || !edgeIds.Add(edge.Id)
                    || !nodeIds.Contains(edge.FromNode) || !nodeIds.Contains(edge.ToNode)
                    || !IsIdentifier(edge.FromPort) || !IsIdentifier(edge.ToPort) || edge.Order < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>직렬화 식별자가 비어 있지 않고 80자 이하이며 영문·숫자·'.'·'_'·'-'만 포함하는지 반환한다.</summary>
        public static bool IsIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MAX_TEXT_LENGTH) return false;
            foreach (char character in value)
            {
                bool isAllowed = (character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z')
                    || (character >= '0' && character <= '9') || character == '.' || character == '_' || character == '-';
                if (!isAllowed) return false;
            }
            return true;
        }

        /// <summary>숫자가 NaN이나 무한대가 아닌지 반환한다.</summary>
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
