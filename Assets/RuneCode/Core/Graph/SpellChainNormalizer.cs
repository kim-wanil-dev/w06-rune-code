using System;
using System.Collections.Generic;

namespace RuneCode
{
    /// <summary>
    /// 문법 블록 체인 [마법 타입] → [속성 부여] → [형태]를 컴파일 내부 표현인 Inline Magic 노드 하나로 합친다.
    /// 형태 블록의 노드 ID를 그대로 쓰므로 형태에 연결된 효과와 피격·만료·완료 분기는 그대로 유지된다.
    /// </summary>
    public static class SpellChainNormalizer
    {
        /// <summary>
        /// 체인 블록을 Inline Magic으로 합친 컴파일용 그래프를 반환한다. 체인이 없으면 원본을 그대로 반환한다.
        /// 체인 블록의 RAM 합계를 chainRam으로, 해금·순서 오류와 미사용 경고를 각 목록에 추가한다.
        /// </summary>
        public static SpellGraph Normalize(SpellGraph graph, RuneCatalog runes, HashSet<string> unlocked,
            List<CompileIssue> errors, List<CompileIssue> warnings, out int chainRam)
        {
            chainRam = 0;
            if (graph?.Nodes == null || graph.Edges == null) return graph;
            Dictionary<string, RuneDefinition> definitions = new Dictionary<string, RuneDefinition>(StringComparer.Ordinal);
            bool hasChain = false;
            foreach (GraphNode node in graph.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Id) || definitions.ContainsKey(node.Id)) continue;
                if (!runes.TryGet(node.RuneId, out RuneDefinition rune)) continue;
                definitions[node.Id] = rune;
                if (!IsChainBlock(rune)) continue;
                hasChain = true;
                chainRam += rune.Ram;
                if (!unlocked.Contains(rune.Id)) errors.Add(new CompileIssue("E10", node.Id));
            }
            if (!hasChain) return graph;

            List<GraphEdge> chainEdges = new List<GraphEdge>();
            foreach (GraphEdge edge in graph.Edges)
                if (edge != null && IsChainEdge(edge, definitions)) chainEdges.Add(edge);

            HashSet<string> consumed = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> typeToShape = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, GraphNode> synthetic = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
            int errorCount = errors.Count;
            foreach (GraphNode shape in graph.Nodes)
            {
                if (shape == null || !definitions.TryGetValue(shape.Id, out RuneDefinition shapeRune)
                    || shapeRune.Category != SpellGrammar.CATEGORY_SHAPE) continue;
                if (IsIsolated(shape.Id, graph.Edges)) continue;
                GraphNode element = null;
                GraphNode type = null;
                GraphNode previous = SingleSource(shape.Id, chainEdges, graph);
                if (previous != null && definitions[previous.Id].Category == SpellGrammar.CATEGORY_ELEMENT)
                {
                    element = previous;
                    previous = SingleSource(element.Id, chainEdges, graph);
                }
                if (previous != null && definitions[previous.Id].Category == SpellGrammar.CATEGORY_MAGIC_TYPE) type = previous;
                if (type == null)
                {
                    errors.Add(new CompileIssue("E18", (element ?? shape).Id));
                    continue;
                }
                if (CountOutgoing(type.Id, chainEdges) != 1 || (element != null && CountOutgoing(element.Id, chainEdges) != 1))
                {
                    errors.Add(new CompileIssue("E18", type.Id));
                    continue;
                }
                synthetic[shape.Id] = CreateInlineNode(shape, type, element);
                consumed.Add(type.Id);
                if (element != null) consumed.Add(element.Id);
                typeToShape[type.Id] = shape.Id;
            }

            HashSet<string> removed = new HashSet<string>(consumed, StringComparer.Ordinal);
            foreach (GraphNode node in graph.Nodes)
            {
                if (node == null || !definitions.TryGetValue(node.Id, out RuneDefinition rune) || !IsChainBlock(rune)) continue;
                if (consumed.Contains(node.Id) || synthetic.ContainsKey(node.Id)) continue;
                if (IsIsolated(node.Id, graph.Edges))
                {
                    warnings.Add(new CompileIssue("W1", node.Id));
                    removed.Add(node.Id);
                }
                else if (rune.Category != SpellGrammar.CATEGORY_SHAPE) errors.Add(new CompileIssue("E18", node.Id));
            }
            if (errors.Count > errorCount) return graph;

            List<GraphNode> nodes = new List<GraphNode>();
            foreach (GraphNode node in graph.Nodes)
            {
                if (node != null && removed.Contains(node.Id)) continue;
                nodes.Add(node != null && synthetic.TryGetValue(node.Id, out GraphNode inline) ? inline : node);
            }
            List<GraphEdge> edges = new List<GraphEdge>();
            foreach (GraphEdge edge in graph.Edges)
            {
                if (edge != null && (chainEdges.Contains(edge) || removed.Contains(edge.FromNode))) continue;
                if (edge != null && typeToShape.TryGetValue(edge.ToNode, out string shapeId))
                {
                    edges.Add(new GraphEdge(edge.Id, edge.FromNode, edge.FromPort, shapeId, "exec", edge.Order));
                    continue;
                }
                if (edge != null && removed.Contains(edge.ToNode)) continue;
                edges.Add(edge);
            }
            return graph.CopyWith(nodes, edges);
        }

        /// <summary>마법 타입·속성 부여·형태 블록인지 반환한다.</summary>
        public static bool IsChainBlock(RuneDefinition rune)
        {
            return rune.Category == SpellGrammar.CATEGORY_MAGIC_TYPE || rune.Category == SpellGrammar.CATEGORY_ELEMENT
                || rune.Category == SpellGrammar.CATEGORY_SHAPE;
        }

        /// <summary>형태 노드 ID를 유지하고 타입·속성·형태 값과 버프 파라미터를 담은 Inline Magic 노드를 만든다.</summary>
        private static GraphNode CreateInlineNode(GraphNode shape, GraphNode type, GraphNode element)
        {
            GraphNode inline = new GraphNode(shape.Id, SpellGrammar.INLINE_RUNE, shape.X, shape.Y);
            inline.SetText("magicType", SpellGrammar.MagicTypeOf(type.RuneId));
            inline.SetText("element", element == null ? "normal" : SpellGrammar.ElementOf(element.RuneId));
            inline.SetText("form", SpellGrammar.ShapeOf(shape.RuneId));
            if (element == null) return inline;
            foreach (NodeParameter param in element.Params)
                if (param.Key == "power" || param.Key == "buffDuration") inline.SetNumber(param.Key, param.Number);
            return inline;
        }

        /// <summary>출력 또는 입력 포트 중 하나가 체인 종류인 엣지인지 반환한다.</summary>
        private static bool IsChainEdge(GraphEdge edge, Dictionary<string, RuneDefinition> definitions)
        {
            if (definitions.TryGetValue(edge.FromNode, out RuneDefinition from)
                && from.FindPort(edge.FromPort, "out")?.Kind == SpellGrammar.CHAIN_KIND) return true;
            return definitions.TryGetValue(edge.ToNode, out RuneDefinition to)
                && to.FindPort(edge.ToPort, "in")?.Kind == SpellGrammar.CHAIN_KIND;
        }

        /// <summary>노드의 체인 입력이 정확히 하나일 때 그 출발 노드를, 아니면 null을 반환한다.</summary>
        private static GraphNode SingleSource(string nodeId, List<GraphEdge> chainEdges, SpellGraph graph)
        {
            GraphEdge found = null;
            foreach (GraphEdge edge in chainEdges)
            {
                if (edge.ToNode != nodeId) continue;
                if (found != null) return null;
                found = edge;
            }
            return found == null ? null : graph.FindNode(found.FromNode);
        }

        /// <summary>노드에서 나가는 체인 엣지 수를 반환한다.</summary>
        private static int CountOutgoing(string nodeId, List<GraphEdge> chainEdges)
        {
            int count = 0;
            foreach (GraphEdge edge in chainEdges) if (edge.FromNode == nodeId) count++;
            return count;
        }

        /// <summary>노드에 연결된 엣지가 하나도 없는지 반환한다.</summary>
        private static bool IsIsolated(string nodeId, IReadOnlyList<GraphEdge> edges)
        {
            foreach (GraphEdge edge in edges)
                if (edge != null && (edge.FromNode == nodeId || edge.ToNode == nodeId)) return false;
            return true;
        }
    }
}
