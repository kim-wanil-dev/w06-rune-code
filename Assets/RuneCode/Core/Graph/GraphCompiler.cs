using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RuneCode
{
    public static class GraphCompiler
    {
        /// <summary>그래프와 해금·RAM·에너지 조건을 검증하고 최악 비용의 실행 트리를 컴파일한다.</summary>
        public static CompileResult Compile(SpellGraph graph, RuneCatalog runes, BalanceData balance,
            IEnumerable<string> unlocked, int capacity = -1, float maxEnergy = -1f)
        {
            List<CompileIssue> errors = new List<CompileIssue>();
            List<CompileIssue> warnings = new List<CompileIssue>();
            if (graph == null)
            {
                errors.Add(new CompileIssue("E1"));
                return new CompileResult(errors, warnings, null);
            }
            if (graph.Version != 1 || graph.Nodes == null || graph.Edges == null
                || graph.Nodes.Count > balance.Limits.MaxGraphNodes || graph.Edges.Count > balance.Limits.MaxGraphEdges)
            {
                errors.Add(new CompileIssue("E9"));
                return new CompileResult(errors, warnings, null);
            }
            Dictionary<string, GraphNode> nodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
            Dictionary<string, RuneDefinition> definitions = new Dictionary<string, RuneDefinition>(StringComparer.Ordinal);
            List<GraphNode> cores = new List<GraphNode>();
            HashSet<string> unlockedIds = new HashSet<string>(unlocked ?? runes.StartRunes, StringComparer.Ordinal);
            int ramUsed = 0;
            foreach (GraphNode node in graph.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Id) || !nodes.TryAdd(node.Id, node)
                    || !runes.TryGet(node.RuneId, out RuneDefinition rune))
                {
                    errors.Add(new CompileIssue("E9", node?.Id));
                    continue;
                }
                definitions[node.Id] = rune;
                ramUsed += rune.Ram;
                if (rune.Category == "core") cores.Add(node);
                if (!unlockedIds.Contains(rune.Id)) errors.Add(new CompileIssue("E10", node.Id));
                ValidateParameters(node, rune, errors);
            }
            if (cores.Count != 1) errors.Add(new CompileIssue("E1"));
            ValidateEdges(graph, definitions, errors, warnings);
            if (!errors.Any(issue => issue.Code == "E3") && HasCycle(graph, nodes)) errors.Add(new CompileIssue("E4"));
            int limit = capacity < 0 ? balance.Economy.BaseCapacity : capacity;
            if (ramUsed > limit) errors.Add(new CompileIssue("E7"));
            bool hasStructuralError = errors.Any(issue => issue.Code != "E7" && issue.Code != "E10");
            if (hasStructuralError) return new CompileResult(errors, warnings, null);

            HashSet<string> reachable = new HashSet<string>();
            FindReachable(graph, cores[0].Id, false, reachable, definitions, warnings);
            foreach (GraphNode node in graph.Nodes)
            {
                RuneDefinition rune = definitions[node.Id];
                if (!reachable.Contains(node.Id))
                {
                    warnings.Add(new CompileIssue("W1", node.Id));
                    if (rune.Id == "flow.if" && node.GetText("type", "targetHpBelow") != "selfHpBelow")
                        warnings.Add(new CompileIssue("W3", node.Id));
                }
                if ((rune.Category == "element" || rune.Category == "modifier")
                    && !graph.Edges.Any(edge => edge.FromNode == node.Id)) warnings.Add(new CompileIssue("W4", node.Id));
            }
            List<SpellAction> root = BuildBranch(graph, cores[0].Id, "exec", nodes, definitions, warnings);
            float energy = Cost(root, balance.Limits.HitTriggerCap);
            int entities = EntityCount(root, balance.Limits.HitTriggerCap);
            float energyLimit = maxEnergy < 0f ? balance.Player.MaxEnergy : maxEnergy;
            long actionCeiling = balance.Limits.MaxCompiledActions;
            long expandedActions = AddBounded(1L, ActionCount(root, balance.Limits.HitTriggerCap, actionCeiling + 1L), actionCeiling + 1L);
            if (energy > energyLimit || expandedActions > actionCeiling) errors.Add(new CompileIssue("E8"));
            SortedSet<string> tags = new SortedSet<string>(StringComparer.Ordinal);
            GatherTags(root, tags);
            CompiledSpell compiled = new CompiledSpell(graph.Name, Signature(graph), cores[0].Id, ramUsed, energy,
                balance.Ram.CooldownBase + balance.Ram.CooldownPerRam * ramUsed, entities, tags.ToList(), root);
            return new CompileResult(errors, warnings, compiled);
        }

        /// <summary>연결 후보가 입력 수·포트 종류·순환·수식 슬롯 규칙을 지키는지 반환한다.</summary>
        public static bool CanConnect(SpellGraph graph, GraphEdge edge, RuneCatalog runes, out CompileIssue issue)
        {
            issue = null;
            GraphNode from = graph.FindNode(edge.FromNode);
            GraphNode to = graph.FindNode(edge.ToNode);
            if (from == null || to == null || !runes.TryGet(from.RuneId, out RuneDefinition fromRune)
                || !runes.TryGet(to.RuneId, out RuneDefinition toRune))
            {
                issue = new CompileIssue("E3", edge.ToNode);
                return false;
            }
            PortDefinition output = fromRune.FindPort(edge.FromPort, "out");
            PortDefinition input = toRune.FindPort(edge.ToPort, "in");
            if (output == null || input == null || output.Kind != input.Kind)
                issue = new CompileIssue("E3", edge.ToNode);
            else if (graph.Edges.Any(current => current.FromNode == edge.FromNode && current.FromPort == edge.FromPort
                && current.ToNode == edge.ToNode && current.ToPort == edge.ToPort))
                issue = new CompileIssue("E9", edge.ToNode);
            else if (input.Kind == "exec" && graph.Edges.Any(current => current.ToNode == edge.ToNode && current.ToPort == edge.ToPort))
                issue = new CompileIssue("E2", edge.ToNode);
            else if (input.Kind == "mod" && graph.Edges.Count(current => current.ToNode == edge.ToNode && current.ToPort == edge.ToPort) >= input.Max)
                issue = new CompileIssue("E5", edge.ToNode);
            else if (input.Kind == "mod" && graph.Edges.Any(current => current.ToNode == edge.ToNode && current.ToPort == edge.ToPort
                && graph.FindNode(current.FromNode)?.RuneId == from.RuneId))
                issue = new CompileIssue("E9", edge.ToNode);
            else if (input.Kind == "mod" && fromRune.Category == "element" && graph.Edges.Any(current => current.ToNode == edge.ToNode
                && runes.Get(graph.FindNode(current.FromNode).RuneId).Category == "element"))
                issue = new CompileIssue("E6", edge.ToNode);
            else if (HasPath(graph, edge.ToNode, edge.FromNode, new HashSet<string>()))
                issue = new CompileIssue("E4", edge.ToNode);
            return issue == null;
        }

        /// <summary>접속 후보 검증을 수행하고 연결 불가 사유를 반환한다.</summary>
        public static bool ValidateConnection(SpellGraph graph, GraphEdge edge, RuneCatalog runes, out CompileIssue issue)
            => CanConnect(graph, edge, runes, out issue);

        /// <summary>각 노드의 숫자·열거 파라미터 및 중복 키가 정의 범위를 지키는지 검증한다.</summary>
        private static void ValidateParameters(GraphNode node, RuneDefinition rune, List<CompileIssue> errors)
        {
            if (node.Params == null)
            {
                errors.Add(new CompileIssue("E9", node.Id));
                return;
            }
            HashSet<string> seen = new HashSet<string>();
            foreach (NodeParameter value in node.Params)
            {
                if (value == null || string.IsNullOrEmpty(value.Key))
                {
                    errors.Add(new CompileIssue("E9", node.Id));
                    continue;
                }
                ParameterDefinition definition = rune.Params.FirstOrDefault(param => param.Id == value.Key);
                bool isInvalid = !seen.Add(value.Key) || definition == null;
                if (!isInvalid && definition.Kind == "number")
                {
                    isInvalid = float.IsNaN(value.Number) || float.IsInfinity(value.Number)
                        || value.Number < definition.Min || value.Number > definition.Max;
                    if (value.Key == "times" && value.Number != (int)value.Number) isInvalid = true;
                }
                if (!isInvalid && definition.Kind == "enum") isInvalid = !definition.Options.Contains(value.Text);
                if (isInvalid) errors.Add(new CompileIssue("E9", node.Id, value.Key));
            }
        }

        /// <summary>모든 연결의 포트 종류·입력 수·속성 수·동일 수식 중복 오류를 추가한다.</summary>
        private static void ValidateEdges(SpellGraph graph, Dictionary<string, RuneDefinition> definitions,
            List<CompileIssue> errors, List<CompileIssue> warnings)
        {
            HashSet<string> ids = new HashSet<string>();
            Dictionary<string, int> incoming = new Dictionary<string, int>();
            Dictionary<string, int> elements = new Dictionary<string, int>();
            HashSet<string> mods = new HashSet<string>();
            foreach (GraphEdge edge in graph.Edges)
            {
                if (edge == null || string.IsNullOrEmpty(edge.Id) || string.IsNullOrEmpty(edge.FromNode) || string.IsNullOrEmpty(edge.ToNode)
                    || !ids.Add(edge.Id) || !definitions.TryGetValue(edge.FromNode, out RuneDefinition from)
                    || !definitions.TryGetValue(edge.ToNode, out RuneDefinition to))
                {
                    errors.Add(new CompileIssue("E3", edge?.ToNode));
                    continue;
                }
                PortDefinition output = from.FindPort(edge.FromPort, "out");
                PortDefinition input = to.FindPort(edge.ToPort, "in");
                if (output == null || input == null || output.Kind != input.Kind)
                {
                    errors.Add(new CompileIssue("E3", edge.ToNode));
                    continue;
                }
                string portKey = edge.ToNode + ":" + edge.ToPort;
                int count = incoming.TryGetValue(portKey, out int previous) ? previous + 1 : 1;
                incoming[portKey] = count;
                if (input.Kind == "exec" && count > 1) errors.Add(new CompileIssue("E2", edge.ToNode));
                if (input.Kind != "mod") continue;
                if (count > input.Max) errors.Add(new CompileIssue("E5", edge.ToNode));
                if (!mods.Add(edge.ToNode + ":" + from.Id)) errors.Add(new CompileIssue("E9", edge.ToNode));
                if ((from.Id == "mod.pierce" || from.Id == "mod.homing") && to.Id != "form.bolt")
                    warnings.Add(new CompileIssue("W2", edge.FromNode));
                if (from.Id == "mod.speed" && to.Id != "form.bolt" && to.Id != "form.orbit")
                    warnings.Add(new CompileIssue("W2", edge.FromNode));
                if (from.Category != "element") continue;
                int elementCount = elements.TryGetValue(edge.ToNode, out int priorElements) ? priorElements + 1 : 1;
                elements[edge.ToNode] = elementCount;
                if (elementCount > 1) errors.Add(new CompileIssue("E6", edge.ToNode));
            }
        }

        /// <summary>각 노드의 연결을 깊이 우선으로 검사하여 순환 여부를 반환한다.</summary>
        private static bool HasCycle(SpellGraph graph, Dictionary<string, GraphNode> nodes)
        {
            Dictionary<string, int> colors = new Dictionary<string, int>();
            foreach (string id in nodes.Keys)
                if (VisitCycle(graph, id, colors)) return true;
            return false;
        }

        /// <summary>방문 중인 노드로 다시 진입하는 경로를 순환으로 검출한다.</summary>
        private static bool VisitCycle(SpellGraph graph, string id, Dictionary<string, int> colors)
        {
            if (colors.TryGetValue(id, out int color)) return color == 1;
            colors[id] = 1;
            foreach (GraphEdge edge in graph.Edges)
                if (edge.FromNode == id && VisitCycle(graph, edge.ToNode, colors)) return true;
            colors[id] = 2;
            return false;
        }

        /// <summary>연결 시작 노드에서 대상 노드에 도달할 수 있는지 반환한다.</summary>
        private static bool HasPath(SpellGraph graph, string from, string target, HashSet<string> visited)
        {
            if (from == target) return true;
            if (!visited.Add(from)) return false;
            foreach (GraphEdge edge in graph.Edges)
                if (edge.FromNode == from && HasPath(graph, edge.ToNode, target, visited)) return true;
            return false;
        }

        /// <summary>실행 경로와 참조 수식을 수집하고 대상이 없는 조건에 경고를 추가한다.</summary>
        private static void FindReachable(SpellGraph graph, string id, bool hasTarget, HashSet<string> reachable,
            Dictionary<string, RuneDefinition> definitions, List<CompileIssue> warnings)
        {
            if (!reachable.Add(id)) return;
            if (definitions[id].Id == "flow.if" && graph.FindNode(id).GetText("type", "targetHpBelow") != "selfHpBelow" && !hasTarget)
                warnings.Add(new CompileIssue("W3", id));
            foreach (GraphEdge edge in graph.Edges)
            {
                if (edge.ToNode == id && edge.ToPort == "mod") reachable.Add(edge.FromNode);
                if (edge.FromNode != id || edge.FromPort == "mod") continue;
                bool nextHasTarget = edge.FromPort == "onHit" || (hasTarget && edge.FromPort != "onExpire");
                FindReachable(graph, edge.ToNode, nextHasTarget, reachable, definitions, warnings);
            }
        }

        /// <summary>출력 포트에 연결된 노드를 안정된 순서의 실행 명령으로 변환한다.</summary>
        private static List<SpellAction> BuildBranch(SpellGraph graph, string nodeId, string port,
            Dictionary<string, GraphNode> nodes, Dictionary<string, RuneDefinition> definitions, List<CompileIssue> warnings)
        {
            List<SpellAction> actions = new List<SpellAction>();
            foreach (GraphEdge edge in graph.Edges.Where(edge => edge.FromNode == nodeId && edge.FromPort == port)
                .OrderBy(edge => edge.ToNode, StringComparer.Ordinal))
            {
                GraphNode node = nodes[edge.ToNode];
                RuneDefinition rune = definitions[node.Id];
                RuneDefinition element = null;
                List<RuneDefinition> mods = new List<RuneDefinition>();
                List<GraphNode> modifierNodes = new List<GraphNode>();
                List<string> attachedNodeIds = new List<string>();
                foreach (GraphEdge attachment in graph.Edges.Where(item => item.ToNode == node.Id && item.ToPort == "mod")
                    .OrderBy(item => item.FromNode, StringComparer.Ordinal))
                {
                    RuneDefinition attached = definitions[attachment.FromNode];
                    attachedNodeIds.Add(attachment.FromNode);
                    if (attached.Category == "element") element = attached;
                    else { mods.Add(attached); modifierNodes.Add(nodes[attachment.FromNode]); }
                }
                SpellAction action = new SpellAction(node, rune, element, mods, modifierNodes);
                action.SetAttachedNodes(attachedNodeIds);
                foreach (PortDefinition output in rune.Ports)
                {
                    if (output.Direction == "out" && output.Kind == "exec")
                        action.SetBranch(output.Id, BuildBranch(graph, node.Id, output.Id, nodes, definitions, warnings));
                }
                actions.Add(action);
            }
            return actions;
        }

        /// <summary>최악 분기와 이벤트 상한을 포함한 에너지 비용을 합산한다.</summary>
        private static float Cost(IReadOnlyList<SpellAction> actions, int hitCap)
        {
            float sum = 0f;
            foreach (SpellAction action in actions)
            {
                switch (action.Kind)
                {
                    case "spawn":
                        int maxHits = action.Form == "bolt" ? 1 + action.Stats.Pierce : hitCap;
                        sum += action.OwnEnergy + action.Count * (Math.Min(maxHits, hitCap) * Cost(action.OnHit, hitCap) + Cost(action.OnExpire, hitCap));
                        break;
                    case "delay": sum += Cost(action.Then, hitCap); break;
                    case "repeat": sum += action.Times * Cost(action.Body, hitCap); break;
                    case "if": sum += Math.Max(Cost(action.Then, hitCap), Cost(action.Else, hitCap)); break;
                    default: sum += action.OwnEnergy + Cost(action.Next, hitCap); break;
                }
            }
            return sum;
        }

        /// <summary>최악 분기의 생성 수를 이벤트 상한과 반복 횟수에 따라 합산한다.</summary>
        private static int EntityCount(IReadOnlyList<SpellAction> actions, int hitCap)
        {
            long sum = 0;
            foreach (SpellAction action in actions)
            {
                switch (action.Kind)
                {
                    case "spawn":
                        int maxHits = action.Form == "bolt" ? 1 + action.Stats.Pierce : hitCap;
                        sum += (long)action.Count * (1L + (long)Math.Min(maxHits, hitCap) * EntityCount(action.OnHit, hitCap) + EntityCount(action.OnExpire, hitCap));
                        break;
                    case "delay": sum += EntityCount(action.Then, hitCap); break;
                    case "repeat": sum += (long)action.Times * EntityCount(action.Body, hitCap); break;
                    case "if": sum += Math.Max(EntityCount(action.Then, hitCap), EntityCount(action.Else, hitCap)); break;
                    default: sum += EntityCount(action.Next, hitCap); break;
                }
                if (sum > int.MaxValue) return int.MaxValue;
            }
            return (int)sum;
        }

        /// <summary>반복·최악 분기·형태 이벤트를 펼친 실행 명령 수를 계산하고 지정 상한에서 포화한다.</summary>
        private static long ActionCount(IReadOnlyList<SpellAction> actions, int hitCap, long saturation)
        {
            long sum = 0L;
            foreach (SpellAction action in actions)
            {
                long count = 1L;
                switch (action.Kind)
                {
                    case "spawn":
                        int maxHits = action.Form == "bolt" ? 1 + action.Stats.Pierce : hitCap;
                        long hitWork = MultiplyBounded(Math.Min(maxHits, hitCap), ActionCount(action.OnHit, hitCap, saturation), saturation);
                        long eventWork = AddBounded(hitWork, ActionCount(action.OnExpire, hitCap, saturation), saturation);
                        count = AddBounded(count, MultiplyBounded(action.Count, eventWork, saturation), saturation);
                        break;
                    case "delay": count = AddBounded(count, ActionCount(action.Then, hitCap, saturation), saturation); break;
                    case "repeat": count = AddBounded(count, MultiplyBounded(action.Times, ActionCount(action.Body, hitCap, saturation), saturation), saturation); break;
                    case "if": count = AddBounded(count, Math.Max(ActionCount(action.Then, hitCap, saturation), ActionCount(action.Else, hitCap, saturation)), saturation); break;
                    default: count = AddBounded(count, ActionCount(action.Next, hitCap, saturation), saturation); break;
                }
                sum = AddBounded(sum, count, saturation);
                if (sum == saturation) return sum;
            }
            return sum;
        }

        /// <summary>음수가 아닌 두 실행 수를 더하며 덧셈 전 지정 상한 초과를 차단한다.</summary>
        private static long AddBounded(long left, long right, long saturation)
            => left >= saturation - right ? saturation : left + right;

        /// <summary>음수가 아닌 실행 수와 반복 수를 곱하며 곱셈 전 지정 상한 초과를 차단한다.</summary>
        private static long MultiplyBounded(long left, long right, long saturation)
        {
            if (left == 0L || right == 0L) return 0L;
            return left > saturation / right ? saturation : Math.Min(saturation, left * right);
        }

        /// <summary>실행되는 모든 형태와 속성의 적응 태그를 수집한다.</summary>
        private static void GatherTags(IReadOnlyList<SpellAction> actions, SortedSet<string> tags)
        {
            foreach (SpellAction action in actions)
            {
                if (action.Kind == "spawn")
                {
                    tags.Add(action.Form);
                    tags.Add(action.Element);
                }
                GatherTags(action.OnHit, tags);
                GatherTags(action.OnExpire, tags);
                GatherTags(action.Then, tags);
                GatherTags(action.Else, tags);
                GatherTags(action.Body, tags);
                GatherTags(action.Next, tags);
            }
        }

        /// <summary>표시 이름·좌표·배열 순서를 제외한 그래프 구성의 안정 해시를 반환한다.</summary>
        public static string Signature(SpellGraph graph)
        {
            StringBuilder canonical = new StringBuilder();
            foreach (GraphNode node in graph.Nodes.OrderBy(node => node.Id, StringComparer.Ordinal))
            {
                canonical.Append(node.Id).Append(':').Append(node.RuneId).Append(';');
                foreach (NodeParameter param in node.Params.OrderBy(param => param.Key, StringComparer.Ordinal))
                    canonical.Append(param.Key).Append('=').Append(param.Number.ToString("R", CultureInfo.InvariantCulture))
                        .Append('/').Append(param.Text).Append(';');
            }
            foreach (GraphEdge edge in graph.Edges.OrderBy(edge => edge.FromNode + ":" + edge.FromPort + ":" + edge.ToNode + ":" + edge.ToPort, StringComparer.Ordinal))
                canonical.Append(edge.FromNode).Append('.').Append(edge.FromPort).Append('>').Append(edge.ToNode).Append('.').Append(edge.ToPort).Append(';');
            ulong hash = 14695981039346656037UL;
            foreach (byte value in Encoding.UTF8.GetBytes(canonical.ToString()))
            {
                hash ^= value;
                hash = unchecked(hash * 1099511628211UL);
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
