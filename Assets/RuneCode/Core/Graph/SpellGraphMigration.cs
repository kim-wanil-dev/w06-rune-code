using System;
using System.Collections.Generic;

namespace RuneCode
{
    /// <summary>
    /// 이전 룬(단독 형태·속성, 직접 마법, 방벽, 점멸, 노이즈, 사거리)으로 만든 그래프를
    /// 마법 비주얼 스크립팅 문법 블록 체인으로 변환한다. 여러 번 호출해도 결과가 같다.
    /// </summary>
    public static class SpellGraphMigration
    {
        private const float SHIELD_POWER = 20f;
        private const float SHIELD_SECONDS = 3f;
        private const float BLOCK_SPACING = 150f;
        private const float BLOCK_DROP = 110f;

        private static readonly Dictionary<string, string> LEGACY_SHAPES = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "form.bolt", "launch" }, { "form.burst", "explosion" }, { "form.orbit", "orbit" }, { "form.zone", "remain" }
        };

        private static readonly Dictionary<string, string> LEGACY_ELEMENTS = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "elem.fire", "fire" }, { "elem.ice", "ice" }, { "elem.arc", "electric" }
        };

        /// <summary>
        /// 이전 해금 룬 ID를 문법 블록 해금 ID로 바꾼 목록을 반환한다.
        /// 대응 블록이 없는 ID(점멸, 노이즈)는 그대로 두며 호출부가 카탈로그 기준으로 정리한다.
        /// </summary>
        public static IEnumerable<string> MapUnlockedRune(string runeId)
        {
            if (LEGACY_SHAPES.TryGetValue(runeId, out string shape)) return new[] { SpellGrammar.ShapeRune(shape) };
            if (LEGACY_ELEMENTS.TryGetValue(runeId, out string element)) return new[] { SpellGrammar.ElementRune(element) };
            if (runeId == "mod.range") return new[] { "mod.duration" };
            if (runeId == "act.shield")
                return new[] { SpellGrammar.TypeRune("buff"), SpellGrammar.ElementRune("protection"), SpellGrammar.ShapeRune("remain") };
            return new[] { runeId };
        }

        /// <summary>그래프의 이전 룬 노드를 문법 블록으로 바꾸고 변경 여부를 반환한다.</summary>
        public static bool Migrate(SpellGraph graph)
        {
            if (graph?.Nodes == null || graph.Edges == null || !HasLegacyNode(graph)) return false;
            foreach (GraphNode node in new List<GraphNode>(graph.Nodes))
            {
                if (node == null) continue;
                if (LEGACY_SHAPES.TryGetValue(node.RuneId, out string shape))
                    ConvertToChain(graph, node, "sphere", AttachedLegacyElement(graph, node), shape, null);
                else if (node.RuneId == SpellGrammar.INLINE_RUNE)
                    ConvertToChain(graph, node, node.GetText("magicType", "sphere"), node.GetText("element", "normal"),
                        node.GetText("form", "launch"), node);
                else if (node.RuneId == "act.shield") ConvertShield(graph, node);
                else if (node.RuneId == "act.blink") Bypass(graph, node);
                else if (node.RuneId == "mod.range") ConvertRange(graph, node);
                else if (node.RuneId == "mod.noise") graph.RemoveNode(node.Id);
            }
            foreach (GraphNode node in new List<GraphNode>(graph.Nodes))
                if (node != null && LEGACY_ELEMENTS.ContainsKey(node.RuneId)) graph.RemoveNode(node.Id);
            return true;
        }

        /// <summary>그래프에 변환 대상 이전 룬이 있는지 반환한다.</summary>
        private static bool HasLegacyNode(SpellGraph graph)
        {
            foreach (GraphNode node in graph.Nodes)
            {
                if (node == null) continue;
                if (LEGACY_SHAPES.ContainsKey(node.RuneId) || LEGACY_ELEMENTS.ContainsKey(node.RuneId) || node.RuneId == SpellGrammar.INLINE_RUNE
                    || node.RuneId == "act.shield" || node.RuneId == "act.blink" || node.RuneId == "mod.range" || node.RuneId == "mod.noise")
                    return true;
            }
            return false;
        }

        /// <summary>형태 노드에 mod로 연결된 이전 속성 룬의 문법 속성 값을 반환하고 없으면 normal을 반환한다.</summary>
        private static string AttachedLegacyElement(SpellGraph graph, GraphNode form)
        {
            foreach (GraphEdge edge in graph.Edges)
            {
                if (edge.ToNode != form.Id || edge.ToPort != "mod") continue;
                GraphNode source = graph.FindNode(edge.FromNode);
                if (source != null && LEGACY_ELEMENTS.TryGetValue(source.RuneId, out string element)) return element;
            }
            return "normal";
        }

        /// <summary>
        /// 노드를 같은 ID의 형태 블록으로 바꾸고 앞에 마법 타입과(일반이 아니면) 속성 부여 블록을 체인으로 붙인다.
        /// 들어오던 실행 엣지는 타입 블록으로 옮기고 이전 속성 룬 연결은 제거한다.
        /// </summary>
        private static void ConvertToChain(SpellGraph graph, GraphNode node, string magicType, string element, string shape, GraphNode buffSource)
        {
            string typeId = UniqueNodeId(graph, node.Id + "-type");
            graph.AddNode(new GraphNode(typeId, SpellGrammar.TypeRune(magicType), node.X - BLOCK_SPACING * 2, node.Y + BLOCK_DROP));
            string chainTail = typeId;
            if (element != "normal")
            {
                string elementId = UniqueNodeId(graph, node.Id + "-attr");
                GraphNode elementNode = new GraphNode(elementId, SpellGrammar.ElementRune(element), node.X - BLOCK_SPACING, node.Y + BLOCK_DROP);
                if (buffSource != null)
                {
                    foreach (NodeParameter param in buffSource.Params)
                        if (param.Key == "power" || param.Key == "buffDuration") elementNode.SetNumber(param.Key, param.Number);
                }
                graph.AddNode(elementNode);
                graph.AddEdge(new GraphEdge(UniqueEdgeId(graph, node.Id + "-chain"), typeId, SpellGrammar.CHAIN_OUT, elementId, SpellGrammar.CHAIN_IN));
                chainTail = elementId;
            }
            graph.ReplaceNode(new GraphNode(node.Id, SpellGrammar.ShapeRune(shape), node.X, node.Y));
            foreach (GraphEdge edge in new List<GraphEdge>(graph.Edges))
            {
                if (edge.ToNode != node.Id) continue;
                if (edge.ToPort == "exec")
                {
                    graph.RemoveEdge(edge.Id);
                    graph.AddEdge(new GraphEdge(edge.Id, edge.FromNode, edge.FromPort, typeId, "exec", edge.Order));
                }
                else if (edge.ToPort == "mod" && LEGACY_ELEMENTS.ContainsKey(graph.FindNode(edge.FromNode)?.RuneId ?? "")) graph.RemoveEdge(edge.Id);
            }
            graph.AddEdge(new GraphEdge(UniqueEdgeId(graph, node.Id + "-chain"), chainTail, SpellGrammar.CHAIN_OUT, node.Id, SpellGrammar.CHAIN_IN));
        }

        /// <summary>방벽 행동을 버프·보호·잔류 체인으로 바꾸고 기존 다음 실행 분기를 완료 분기로 옮긴다.</summary>
        private static void ConvertShield(SpellGraph graph, GraphNode node)
        {
            GraphNode source = new GraphNode(node.Id, node.RuneId, node.X, node.Y);
            source.SetNumber("power", SHIELD_POWER);
            source.SetNumber("buffDuration", SHIELD_SECONDS);
            ConvertToChain(graph, node, "buff", "protection", "remain", source);
            foreach (GraphEdge edge in new List<GraphEdge>(graph.Edges))
            {
                if (edge.FromNode != node.Id || edge.FromPort != "next") continue;
                graph.RemoveEdge(edge.Id);
                graph.AddEdge(new GraphEdge(edge.Id, edge.FromNode, "onComplete", edge.ToNode, edge.ToPort, edge.Order));
            }
        }

        /// <summary>문법에 없는 점멸 노드를 제거하고 앞 실행 출력을 뒤 실행 대상에 직접 연결한다.</summary>
        private static void Bypass(SpellGraph graph, GraphNode node)
        {
            List<GraphEdge> incoming = new List<GraphEdge>();
            List<GraphEdge> outgoing = new List<GraphEdge>();
            foreach (GraphEdge edge in graph.Edges)
            {
                if (edge.ToNode == node.Id && edge.ToPort == "exec") incoming.Add(edge);
                if (edge.FromNode == node.Id && edge.FromPort == "next") outgoing.Add(edge);
            }
            graph.RemoveNode(node.Id);
            foreach (GraphEdge input in incoming)
                foreach (GraphEdge output in outgoing)
                    graph.AddEdge(new GraphEdge(UniqueEdgeId(graph, input.Id + "-bypass"), input.FromNode, input.FromPort,
                        output.ToNode, output.ToPort, graph.NextEdgeOrder(input.FromNode, input.FromPort)));
        }

        /// <summary>사거리 수식을 같은 배율의 지속시간 효과로 바꾼다.</summary>
        private static void ConvertRange(SpellGraph graph, GraphNode node)
        {
            GraphNode duration = new GraphNode(node.Id, "mod.duration", node.X, node.Y);
            duration.SetNumber("durationScale", node.GetNumber("rangeScale", 1.5f));
            graph.ReplaceNode(duration);
        }

        /// <summary>그래프에 없는 노드 ID를 기준 ID에 번호를 붙여 반환한다.</summary>
        private static string UniqueNodeId(SpellGraph graph, string baseId)
        {
            string id = baseId;
            for (int index = 1; graph.FindNode(id) != null; index++) id = baseId + index;
            return id;
        }

        /// <summary>그래프에 없는 엣지 ID를 기준 ID에 번호를 붙여 반환한다.</summary>
        private static string UniqueEdgeId(SpellGraph graph, string baseId)
        {
            string id = baseId;
            for (int index = 1; ContainsEdge(graph, id); index++) id = baseId + index;
            return id;
        }

        /// <summary>엣지 ID가 그래프에 있는지 반환한다.</summary>
        private static bool ContainsEdge(SpellGraph graph, string id)
        {
            foreach (GraphEdge edge in graph.Edges) if (edge.Id == id) return true;
            return false;
        }
    }
}
