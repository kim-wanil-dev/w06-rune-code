using System;
using System.Collections.Generic;
using System.Linq;

namespace RuneCode
{
    /// <summary>
    /// 문법 블록 [Shape(Element)] → [Behavior]와 [Apply(Element)]를 컴파일 내부 표현인 Inline Magic 노드 하나로 합친다.
    /// Behavior·Apply 노드의 ID를 그대로 쓰므로 거기에 연결된 효과와 이벤트 분기는 그대로 유지된다.
    /// Inline Magic의 magicType·element·form 값은 이전 문법의 내부 값을 그대로 사용해 컴파일·실행 결과가 바뀌지 않는다.
    /// </summary>
    public static class SpellChainNormalizer
    {
        /// <summary>
        /// 문법 블록을 Inline Magic으로 합친 컴파일용 그래프를 반환한다. 문법 블록이 없으면 원본을 그대로 반환한다.
        /// Shape·속성·Behavior·Apply의 RAM 합계를 chainRam으로, 해금·연결·속성 값 오류와 미사용 경고를 각 목록에 추가한다.
        /// </summary>
        public static SpellGraph Normalize(SpellGraph graph, RuneCatalog runes, HashSet<string> unlocked,
            List<CompileIssue> errors, List<CompileIssue> warnings, out int chainRam)
        {
            chainRam = 0;
            if (graph?.Nodes == null || graph.Edges == null) return graph;
            var definitions = new Dictionary<string, RuneDefinition>(StringComparer.Ordinal);
            var elements = new Dictionary<string, ElementDefinition>(StringComparer.Ordinal);
            bool hasGrammar = false;
            int errorCount = errors.Count;
            foreach (GraphNode node in graph.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Id) || definitions.ContainsKey(node.Id)) continue;
                if (!runes.TryGet(node.RuneId, out RuneDefinition rune)) continue;
                definitions[node.Id] = rune;
                if (!IsGrammarBlock(rune)) continue;
                hasGrammar = true;
                chainRam += rune.Ram;
                if (!unlocked.Contains(rune.Id)) errors.Add(new CompileIssue("E10", node.Id));
                if (HasElement(rune) && TryResolveElement(node, rune, runes, errors, out ElementDefinition element, out RuneDefinition elementRune))
                {
                    elements[node.Id] = element;
                    chainRam += elementRune.Ram;
                    if (!unlocked.Contains(elementRune.Id)) errors.Add(new CompileIssue("E10", node.Id));
                }
            }
            if (!hasGrammar) return graph;

            var chainEdges = new List<GraphEdge>();
            foreach (GraphEdge edge in graph.Edges)
            {
                if (edge != null && IsChainEdge(edge, definitions)) chainEdges.Add(edge);
            }

            var consumed = new HashSet<string>(StringComparer.Ordinal);
            var shapeToBehavior = new Dictionary<string, string>(StringComparer.Ordinal);
            var synthetic = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
            foreach (GraphNode node in graph.Nodes)
            {
                if (node == null || !definitions.TryGetValue(node.Id, out RuneDefinition rune)) continue;
                if (rune.Id == SpellGrammar.APPLY_RUNE)
                {
                    if (elements.TryGetValue(node.Id, out ElementDefinition applied)) synthetic[node.Id] = CreateApplyNode(node, applied);
                    continue;
                }
                if (rune.Category != SpellGrammar.CATEGORY_BEHAVIOR || IsIsolated(node.Id, graph.Edges)) continue;

                GraphNode shape = SingleSource(node.Id, chainEdges, graph);
                if (shape == null || !definitions.TryGetValue(shape.Id, out RuneDefinition shapeRune) || shapeRune.Category != SpellGrammar.CATEGORY_SHAPE)
                {
                    errors.Add(new CompileIssue("E18", node.Id));
                    continue;
                }
                if (CountOutgoing(shape.Id, chainEdges) != 1)
                {
                    errors.Add(new CompileIssue("E18", shape.Id));
                    continue;
                }
                consumed.Add(shape.Id);
                shapeToBehavior[shape.Id] = node.Id;
                if (elements.TryGetValue(shape.Id, out ElementDefinition element)) synthetic[node.Id] = CreateInlineNode(node, shape, element);
            }

            var removed = new HashSet<string>(consumed, StringComparer.Ordinal);
            foreach (GraphNode node in graph.Nodes)
            {
                if (node == null || !definitions.TryGetValue(node.Id, out RuneDefinition rune) || !IsGrammarBlock(rune)) continue;
                if (consumed.Contains(node.Id) || synthetic.ContainsKey(node.Id)) continue;
                if (IsIsolated(node.Id, graph.Edges))
                {
                    warnings.Add(new CompileIssue("W1", node.Id));
                    removed.Add(node.Id);
                }
                else if (rune.Category != SpellGrammar.CATEGORY_BEHAVIOR) errors.Add(new CompileIssue("E18", node.Id));
            }
            if (errors.Count > errorCount) return graph;

            var nodes = new List<GraphNode>();
            foreach (GraphNode node in graph.Nodes)
            {
                if (node != null && removed.Contains(node.Id)) continue;
                nodes.Add(node != null && synthetic.TryGetValue(node.Id, out GraphNode inline) ? inline : node);
            }
            var edges = new List<GraphEdge>();
            foreach (GraphEdge edge in graph.Edges)
            {
                if (edge != null && (chainEdges.Contains(edge) || removed.Contains(edge.FromNode))) continue;
                if (edge != null && shapeToBehavior.TryGetValue(edge.ToNode, out string behaviorId))
                {
                    // Shape로 들어오던 실행 연결은 합쳐진 Inline Magic(Behavior 노드 ID)의 실행 입력으로 옮긴다.
                    edges.Add(new GraphEdge(edge.Id, edge.FromNode, edge.FromPort, behaviorId, SpellGrammar.EXEC_PORT, edge.Order));
                    continue;
                }
                if (edge != null && removed.Contains(edge.ToNode)) continue;
                edges.Add(edge);
            }
            return graph.CopyWith(nodes, edges);
        }

        /// <summary>Shape·Behavior·속성 블록인지 반환한다. 속성 블록은 배치 대상이 아니며 남아 있으면 오류·경고 대상이다.</summary>
        public static bool IsGrammarBlock(RuneDefinition rune)
        {
            return rune.Category == SpellGrammar.CATEGORY_SHAPE || rune.Category == SpellGrammar.CATEGORY_BEHAVIOR
                || rune.Category == SpellGrammar.CATEGORY_ELEMENT;
        }

        /// <summary>속성 드롭다운을 가진 블록(Shape, Apply)인지 반환한다.</summary>
        private static bool HasElement(RuneDefinition rune)
        {
            return rune.Category == SpellGrammar.CATEGORY_SHAPE || rune.Id == SpellGrammar.APPLY_RUNE;
        }

        /// <summary>
        /// 노드의 element 파라미터(없으면 룬 기본값)를 속성 정의와 속성 룬으로 찾는다.
        /// 룬이 허용하지 않는 값이면 E9를 추가하고 false를 반환한다.
        /// </summary>
        private static bool TryResolveElement(GraphNode node, RuneDefinition rune, RuneCatalog runes, List<CompileIssue> errors,
            out ElementDefinition element, out RuneDefinition elementRune)
        {
            element = null;
            elementRune = null;
            ParameterDefinition parameter = null;
            foreach (ParameterDefinition candidate in rune.Params)
            {
                if (candidate.Id == SpellGrammar.ELEMENT_PARAM) parameter = candidate;
            }
            string value = node.GetText(SpellGrammar.ELEMENT_PARAM, parameter?.DefaultText ?? SpellGrammar.ELEMENT_NEUTRAL);
            bool isAllowed = parameter == null || parameter.Options.Contains(value);
            if (!isAllowed || !runes.TryGetElement(value, out element) || !runes.TryGet(element.RuneId, out elementRune))
            {
                errors.Add(new CompileIssue("E9", node.Id));
                return false;
            }
            return true;
        }

        /// <summary>Behavior 노드 ID를 유지하고 Shape·속성·Behavior 값과 Shape의 효과량 파라미터를 담은 Inline Magic 노드를 만든다.</summary>
        private static GraphNode CreateInlineNode(GraphNode behavior, GraphNode shape, ElementDefinition element)
        {
            var inline = new GraphNode(behavior.Id, SpellGrammar.INLINE_RUNE, behavior.X, behavior.Y);
            inline.SetText("magicType", SpellGrammar.MagicTypeOf(shape.RuneId));
            inline.SetText("element", element.InternalValue);
            inline.SetText("form", SpellGrammar.FormOf(behavior.RuneId));
            CopyEffectParameters(shape, inline);
            return inline;
        }

        /// <summary>Apply 노드 ID를 유지하고 자기 적용(버프)·속성 값과 효과량 파라미터를 담은 Inline Magic 노드를 만든다.</summary>
        private static GraphNode CreateApplyNode(GraphNode apply, ElementDefinition element)
        {
            var inline = new GraphNode(apply.Id, SpellGrammar.INLINE_RUNE, apply.X, apply.Y);
            inline.SetText("magicType", SpellGrammar.MAGIC_TYPE_BUFF);
            inline.SetText("element", element.InternalValue);
            inline.SetText("form", SpellGrammar.FormOf(apply.RuneId));
            CopyEffectParameters(apply, inline);
            return inline;
        }

        /// <summary>원본 노드에 명시적으로 저장된 효과량(power)·보호막 지속시간(buffDuration) 값만 저장 순서대로 복사한다.</summary>
        private static void CopyEffectParameters(GraphNode source, GraphNode target)
        {
            foreach (NodeParameter param in source.Params)
            {
                if (param.Key == SpellGrammar.POWER_PARAM || param.Key == SpellGrammar.BUFF_DURATION_PARAM) target.SetNumber(param.Key, param.Number);
            }
        }

        /// <summary>출력 또는 입력 포트 중 하나가 체인 종류인 엣지인지 반환한다.</summary>
        private static bool IsChainEdge(GraphEdge edge, Dictionary<string, RuneDefinition> definitions)
        {
            if (definitions.TryGetValue(edge.FromNode, out RuneDefinition from)
                && from.FindPort(edge.FromPort, SpellGrammar.DIRECTION_OUT)?.Kind == SpellGrammar.CHAIN_KIND) return true;
            return definitions.TryGetValue(edge.ToNode, out RuneDefinition to)
                && to.FindPort(edge.ToPort, SpellGrammar.DIRECTION_IN)?.Kind == SpellGrammar.CHAIN_KIND;
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
            foreach (GraphEdge edge in chainEdges)
            {
                if (edge.FromNode == nodeId) count++;
            }
            return count;
        }

        /// <summary>노드에 연결된 엣지가 하나도 없는지 반환한다.</summary>
        private static bool IsIsolated(string nodeId, IReadOnlyList<GraphEdge> edges)
        {
            foreach (GraphEdge edge in edges)
            {
                if (edge != null && (edge.FromNode == nodeId || edge.ToNode == nodeId)) return false;
            }
            return true;
        }
    }
}
