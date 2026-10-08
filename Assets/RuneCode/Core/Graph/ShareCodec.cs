using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    public static class ShareCodec
    {
        private const int MAX_JSON_LENGTH = 131072;
        private const int MAX_NODE_COUNT = 256;
        private const int MAX_EDGE_COUNT = 1024;

        /// <summary>마법 그래프를 저장 또는 복제에 사용할 JSON 문자열로 직렬화한다.</summary>
        public static string Serialize(SpellGraph graph, bool pretty = false) => JsonUtility.ToJson(graph, pretty);

        /// <summary>크기·필수 필드·ID·숫자를 검증한 JSON 그래프를 반환한다.</summary>
        public static SpellGraph Deserialize(string json)
        {
            string trimmed = json?.TrimStart();
            if (string.IsNullOrEmpty(trimmed) || json.Length > MAX_JSON_LENGTH || trimmed[0] != '{')
                throw new FormatException(GameData.L("share.invalid"));
            SpellGraph graph = JsonUtility.FromJson<SpellGraph>(json);
            if (graph == null || graph.Version != 1 || !IsIdentifier(graph.Id) || string.IsNullOrWhiteSpace(graph.Name)
                || graph.Name.Length > 80 || graph.Nodes == null || graph.Edges == null
                || graph.Nodes.Count == 0 || graph.Nodes.Count > MAX_NODE_COUNT || graph.Edges.Count > MAX_EDGE_COUNT)
                throw new FormatException(GameData.L("share.invalid"));
            HashSet<string> nodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GraphNode node in graph.Nodes)
            {
                if (node == null || !IsIdentifier(node.Id) || !IsIdentifier(node.RuneId) || !nodeIds.Add(node.Id)
                    || !IsFinite(node.X) || !IsFinite(node.Y) || node.Params == null || node.Params.Count > 16)
                    throw new FormatException(GameData.L("share.invalid"));
                HashSet<string> paramKeys = new HashSet<string>();
                foreach (NodeParameter param in node.Params)
                {
                    if (param == null || !IsIdentifier(param.Key) || !paramKeys.Add(param.Key)
                        || !IsFinite(param.Number) || (param.Text != null && param.Text.Length > 80))
                        throw new FormatException(GameData.L("share.invalid"));
                }
            }
            HashSet<string> edgeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GraphEdge edge in graph.Edges)
            {
                if (edge == null || !IsIdentifier(edge.Id) || !edgeIds.Add(edge.Id)
                    || !nodeIds.Contains(edge.FromNode) || !nodeIds.Contains(edge.ToNode)
                    || !IsIdentifier(edge.FromPort) || !IsIdentifier(edge.ToPort))
                    throw new FormatException(GameData.L("share.invalid"));
            }
            return graph;
        }

        /// <summary>직렬화 식별자의 길이와 허용 문자를 검증한다.</summary>
        private static bool IsIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 80) return false;
            foreach (char character in value)
                if (!((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z')
                    || (character >= '0' && character <= '9')) && character != '.' && character != '_' && character != '-') return false;
            return true;
        }

        /// <summary>공유 데이터 숫자가 유한한 값인지 반환한다.</summary>
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
