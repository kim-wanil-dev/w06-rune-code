using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

namespace RuneCode
{
    public static class ShareCodec
    {
        private const int MAX_JSON_LENGTH = 131072;
        private const int MAX_NODE_COUNT = 256;
        private const int MAX_EDGE_COUNT = 1024;
        private const string PREFIX = "RC1.";

        /// <summary>마법 그래프를 저장 또는 복제에 사용할 JSON 문자열로 직렬화한다.</summary>
        public static string Serialize(SpellGraph graph, bool pretty = false) => JsonUtility.ToJson(graph, pretty);

        /// <summary>크기·필수 필드·ID·숫자를 검증한 JSON 그래프를 반환한다.</summary>
        public static SpellGraph Deserialize(string json)
        {
            string trimmed = json?.TrimStart();
            if (string.IsNullOrEmpty(trimmed) || json.Length > MAX_JSON_LENGTH || trimmed[0] != '{')
                throw new FormatException(GameData.L("share.invalid"));
            SpellGraph graph = JsonUtility.FromJson<SpellGraph>(json);
            if (graph == null || (graph.Version != 1 && graph.Version != 2) || !IsIdentifier(graph.Id) || string.IsNullOrWhiteSpace(graph.Name)
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

        /// <summary>그래프 버전에 맞게 RC1 또는 RC2 접두사의 URL 안전 Base64 공유 코드로 인코딩한다.</summary>
        public static string Encode(SpellGraph graph)
        {
            string json = Serialize(graph);
            if (json.Length > MAX_JSON_LENGTH) throw new FormatException(GameData.L("share.tooLarge"));
            return (graph.Version == 2 ? "RC2." : PREFIX) + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>공유 코드의 형식·크기·그래프 연결을 검증하고 실패 시 사용자 오류를 반환한다.</summary>
        public static bool TryDecode(string code, out SpellGraph graph, out string error)
        {
            graph = null;
            error = null;
            try
            {
                code = code?.Trim();
                if (code == null || (!code.StartsWith(PREFIX, StringComparison.Ordinal) && !code.StartsWith("RC2.", StringComparison.Ordinal)) || code.Length > MAX_JSON_LENGTH * 2)
                    throw new FormatException(GameData.L("share.invalid"));
                string payload = code.Substring(PREFIX.Length);
                foreach (char value in payload)
                    if (!((value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z')
                        || (value >= '0' && value <= '9')) && value != '-' && value != '_')
                        throw new FormatException(GameData.L("share.invalid"));
                string base64 = payload.Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
                string json = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(base64));
                graph = Deserialize(json);
                if ((graph.Version == 2) != code.StartsWith("RC2.", StringComparison.Ordinal))
                    throw new FormatException(GameData.L("share.invalid"));
                if (GameData.IsLoaded)
                {
                    List<string> allRunes = new List<string>();
                    foreach (RuneDefinition rune in GameData.Runes.All) allRunes.Add(rune.Id);
                    CompileResult result = GraphCompiler.Compile(graph, GameData.Runes, GameData.Balance, allRunes, int.MaxValue, float.MaxValue);
                    if (!result.Ok) throw new FormatException(result.Errors[0].Message);
                }
                return true;
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException || exception is OverflowException)
            {
                graph = null;
                error = exception is FormatException ? exception.Message : GameData.L("share.invalid");
                return false;
            }
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
