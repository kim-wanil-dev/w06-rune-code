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
            IEnumerable<string> unlocked, int capacity = -1, float maxEnergy = -1f, IEnumerable<SpellGraph> library = null)
        {
            CompileContext context = new CompileContext(unlocked ?? runes.StartRunes, library, graph, capacity, maxEnergy);
            List<CompileIssue> dependencyErrors = new List<CompileIssue>();
            ValidateSpellDependencies(graph, context.Library, new HashSet<string>(StringComparer.Ordinal),
                new List<string>(), dependencyErrors);
            if (dependencyErrors.Count > 0) return new CompileResult(dependencyErrors, new List<CompileIssue>(), null);
            return CompileGraph(graph, runes, balance, context);
        }

        /// <summary>유효한 종속성을 가진 그래프를 순환 검사 스택에 넣고 한 번 컴파일한다.</summary>
        private static CompileResult CompileGraph(SpellGraph graph, RuneCatalog runes, BalanceData balance, CompileContext context)
        {
            if (graph == null)
            {
                return new CompileResult(new List<CompileIssue> { new CompileIssue("E1") }, new List<CompileIssue>(), null);
            }
            if (!string.IsNullOrEmpty(graph.Id) && context.Compiled.TryGetValue(graph.Id, out CompiledSpell cached))
                return new CompileResult(new List<CompileIssue>(), new List<CompileIssue>(), cached);
            if (!string.IsNullOrEmpty(graph.Id) && context.ActiveSpellIds.Contains(graph.Id))
                return new CompileResult(new List<CompileIssue> { new CompileIssue("E12", null, graph.Id) }, new List<CompileIssue>(), null);
            if (!string.IsNullOrEmpty(graph.Id)) context.ActiveSpellIds.Add(graph.Id);
            List<CompileIssue> chainErrors = new List<CompileIssue>();
            List<CompileIssue> chainWarnings = new List<CompileIssue>();
            SpellGraph normalized = SpellChainNormalizer.Normalize(graph, runes, context.UnlockedIds, chainErrors, chainWarnings, out int chainRam);
            CompileResult result = chainErrors.Any(issue => issue.Code != "E10")
                ? new CompileResult(chainErrors, chainWarnings, null)
                : CompileGraphBody(normalized, runes, balance, context, chainErrors, chainWarnings, chainRam);
            if (!string.IsNullOrEmpty(graph.Id)) context.ActiveSpellIds.Remove(graph.Id);
            if (result.Ok && !string.IsNullOrEmpty(graph.Id)) context.Compiled[graph.Id] = result.Spell;
            return result;
        }

        /// <summary>
        /// 체인 정규화를 마친 그래프의 해금·RAM·에너지 조건을 검사하고 실행 트리 및 비용 상한을 계산한다.
        /// 정규화 단계의 오류·경고 목록에 이어서 기록하며 chainRam은 합쳐진 문법 블록의 RAM이다.
        /// </summary>
        private static CompileResult CompileGraphBody(SpellGraph graph, RuneCatalog runes, BalanceData balance, CompileContext context,
            List<CompileIssue> errors, List<CompileIssue> warnings, int chainRam)
        {
            if ((graph.Version != 1 && graph.Version != 2) || graph.Nodes == null || graph.Edges == null
                || graph.Nodes.Count > balance.Limits.MaxGraphNodes || graph.Edges.Count > balance.Limits.MaxGraphEdges)
            {
                errors.Add(new CompileIssue("E9"));
                return new CompileResult(errors, warnings, null);
            }
            Dictionary<string, GraphNode> nodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
            Dictionary<string, RuneDefinition> definitions = new Dictionary<string, RuneDefinition>(StringComparer.Ordinal);
            List<GraphNode> cores = new List<GraphNode>();
            int ramUsed = chainRam;
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
                if (!context.UnlockedIds.Contains(rune.Id)) errors.Add(new CompileIssue("E10", node.Id));
                ValidateParameters(node, rune, errors);
                ValidateMagicNode(node, rune, graph.Edges, errors);
            }
            if (cores.Count != 1) errors.Add(new CompileIssue("E1"));
            ValidateEdges(graph, definitions, errors, warnings);
            if (!errors.Any(issue => issue.Code == "E3") && HasCycle(graph, nodes)) errors.Add(new CompileIssue("E4"));
            int limit = context.Capacity < 0 ? balance.Economy.BaseCapacity : context.Capacity;
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
            List<SpellAction> root = BuildBranch(graph, cores[0].Id, "exec", nodes, definitions, warnings, errors, runes, balance, context);
            if (errors.Any(issue => issue.Code == "E11" || issue.Code == "E12" || issue.Code == "E14" || issue.Code == "E15"))
                return new CompileResult(errors, warnings, null);
            float energy = Cost(root, balance.Limits.HitTriggerCap);
            int entities = EntityCount(root, balance.Limits.HitTriggerCap);
            float energyLimit = context.MaxEnergy < 0f ? balance.Player.MaxEnergy : context.MaxEnergy;
            long actionCeiling = balance.Limits.MaxCompiledActions;
            long expandedActions = AddBounded(1L, ActionCount(root, balance.Limits.HitTriggerCap, actionCeiling + 1L), actionCeiling + 1L);
            if (energy > energyLimit || expandedActions > actionCeiling) errors.Add(new CompileIssue("E8"));
            if (entities > balance.Limits.MaxLiveSpellEntities) errors.Add(new CompileIssue("E17"));
            SortedSet<string> tags = new SortedSet<string>(StringComparer.Ordinal);
            GatherTags(root, tags);
            string trigger = cores[0].GetText("trigger", "attack");
            CompiledSpell compiled = new CompiledSpell(graph.Name, Signature(graph, root), cores[0].Id, ramUsed, energy,
                balance.Ram.CooldownBase + balance.Ram.CooldownPerRam * ramUsed, entities, tags.ToList(), root, graph.Id, trigger);
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
            else if (output.Kind == SpellGrammar.CHAIN_KIND && (!SpellGrammar.IsValidChainLink(fromRune, toRune)
                || graph.Edges.Any(current => current.FromNode == edge.FromNode && current.FromPort == edge.FromPort)
                || graph.Edges.Any(current => current.ToNode == edge.ToNode && current.ToPort == edge.ToPort)))
                issue = new CompileIssue("E18", edge.ToNode);
            else if (input.Kind == "mod" && !CanAttachModifier(fromRune, toRune))
                issue = new CompileIssue("E13", edge.ToNode);
            else if (fromRune.Id == "magic.inline" && from.GetText("magicType", "sphere") == "buff"
                && (edge.FromPort == "onHit" || edge.FromPort == "onExpire"))
                issue = new CompileIssue("E16", edge.FromNode);
            else if (graph.Version == 2 && output.Kind == "exec" && graph.Edges.Any(current => current.FromNode == edge.FromNode
                && current.FromPort == edge.FromPort && current.Order == edge.Order))
                issue = new CompileIssue("E9", edge.ToNode);
            else if (graph.Edges.Any(current => current.FromNode == edge.FromNode && current.FromPort == edge.FromPort
                && current.ToNode == edge.ToNode && current.ToPort == edge.ToPort))
                issue = new CompileIssue("E9", edge.ToNode);
            else if (input.Kind == "exec" && graph.Edges.Any(current => current.ToNode == edge.ToNode && current.ToPort == edge.ToPort))
                issue = new CompileIssue("E2", edge.ToNode);
            else if (input.Kind == "mod" && input.Max > 0 && graph.Edges.Count(current => current.ToNode == edge.ToNode && current.ToPort == edge.ToPort) >= input.Max)
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

        /// <summary>효과 입력에는 형태 블록·마법 메소드(및 내부 Inline Magic)에 효과 룬만 연결할 수 있는지 반환한다.</summary>
        private static bool CanAttachModifier(RuneDefinition source, RuneDefinition target)
        {
            if (target.Category == SpellGrammar.CATEGORY_SHAPE || target.Id == SpellGrammar.CALL_RUNE || target.Id == SpellGrammar.INLINE_RUNE)
                return source.Category == "modifier";
            return target.Category == "form" && (source.Category == "modifier" || source.Category == "element");
        }

        /// <summary>Inline Magic의 종류·속성·형태 조합과 버프의 이벤트 출력을 검사한다. 해금은 문법 블록 단계에서 검사한다.</summary>
        private static void ValidateMagicNode(GraphNode node, RuneDefinition rune, IReadOnlyList<GraphEdge> edges, List<CompileIssue> errors)
        {
            if (rune.Id != "magic.inline") return;
            string magicType = node.GetText("magicType", "sphere");
            string element = node.GetText("element", "normal");
            string form = node.GetText("form", "launch");
            bool isBuffElement = element == "heal" || element == "protection";
            bool isProtectionOrbit = magicType == "sphere" && element == "protection" && form == "orbit";
            if ((magicType == "buff" && (!isBuffElement || form != "remain"))
                || (element == "heal" && magicType != "buff")
                || (element == "protection" && magicType != "buff" && !isProtectionOrbit))
                errors.Add(new CompileIssue("E14", node.Id));
            if (magicType == "buff" && edges.Any(edge => edge.FromNode == node.Id
                && (edge.FromPort == "onHit" || edge.FromPort == "onExpire")))
                errors.Add(new CompileIssue("E16", node.Id));
        }

        /// <summary>Inline Magic의 기존 런타임 Form 룬 ID를 반환한다.</summary>
        private static string EffectiveFormId(GraphNode node, RuneDefinition rune)
        {
            if (rune.Id != "magic.inline") return rune.Category == "form" ? rune.Id : "";
            switch (node.GetText("form", "launch"))
            {
                case "launch": return "form.bolt";
                case "explosion": return "form.burst";
                case "orbit": return "form.orbit";
                case "remain": return "form.zone";
                default: return "";
            }
        }

        /// <summary>라이브러리 참조를 따라가며 없는 Spell ID와 직접·간접 재귀를 검사한다.</summary>
        private static void ValidateSpellDependencies(SpellGraph graph,
            Dictionary<string, SpellGraph> library, HashSet<string> visited, List<string> active, List<CompileIssue> errors)
        {
            if (graph == null) return;
            if (!string.IsNullOrEmpty(graph.Id))
            {
                if (visited.Contains(graph.Id)) return;
                active.Add(graph.Id);
            }
            if (graph.Nodes != null)
            {
                foreach (GraphNode node in graph.Nodes)
                {
                    if (node == null || node.RuneId != "spell.call") continue;
                    string spellId = node.GetText("spellId").Trim();
                    if (!IsSpellId(spellId) || !library.TryGetValue(spellId, out SpellGraph target))
                    {
                        errors.Add(new CompileIssue("E11", node.Id, spellId));
                        continue;
                    }
                    if (active.Contains(spellId))
                    {
                        errors.Add(new CompileIssue("E12", node.Id, spellId));
                        continue;
                    }
                    ValidateSpellDependencies(target, library, visited, active, errors);
                }
            }
            if (!string.IsNullOrEmpty(graph.Id))
            {
                active.RemoveAt(active.Count - 1);
                visited.Add(graph.Id);
            }
        }

        /// <summary>Spell ID가 JSON 및 라이브러리 참조에서 허용되는 문자만 포함하는지 반환한다.</summary>
        private static bool IsSpellId(string spellId)
        {
            if (string.IsNullOrWhiteSpace(spellId) || spellId.Length > 80) return false;
            foreach (char character in spellId)
                if (!((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z')
                    || (character >= '0' && character <= '9')) && character != '.' && character != '_' && character != '-') return false;
            return true;
        }

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
                if (!isInvalid && definition.Kind == "text") isInvalid = value.Text != null && value.Text.Length > 80;
                if (isInvalid) errors.Add(new CompileIssue("E9", node.Id, value.Key));
            }
        }

        /// <summary>연결의 포트 종류·입력 수·실행 순번·속성 수·수식 중복 오류를 추가한다.</summary>
        private static void ValidateEdges(SpellGraph graph, Dictionary<string, RuneDefinition> definitions,
            List<CompileIssue> errors, List<CompileIssue> warnings)
        {
            HashSet<string> ids = new HashSet<string>();
            Dictionary<string, int> incoming = new Dictionary<string, int>();
            Dictionary<string, int> elements = new Dictionary<string, int>();
            Dictionary<string, Dictionary<string, HashSet<int>>> execOrders = new Dictionary<string, Dictionary<string, HashSet<int>>>(StringComparer.Ordinal);
            HashSet<string> mods = new HashSet<string>();
            foreach (GraphEdge edge in graph.Edges)
            {
                if (edge == null || string.IsNullOrEmpty(edge.Id) || string.IsNullOrEmpty(edge.FromNode) || string.IsNullOrEmpty(edge.ToNode)
                    || edge.Order < 0 || !ids.Add(edge.Id) || !definitions.TryGetValue(edge.FromNode, out RuneDefinition from)
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
                GraphNode fromNode = graph.FindNode(edge.FromNode);
                if (from.Id == "magic.inline" && fromNode.GetText("magicType", "sphere") == "buff"
                    && (edge.FromPort == "onHit" || edge.FromPort == "onExpire"))
                {
                    errors.Add(new CompileIssue("E16", edge.FromNode));
                    continue;
                }
                if (input.Kind == "mod" && !CanAttachModifier(from, to))
                {
                    errors.Add(new CompileIssue("E13", edge.ToNode));
                    continue;
                }
                if (graph.Version == 2 && output.Kind == "exec")
                {
                    if (!execOrders.TryGetValue(edge.FromNode, out Dictionary<string, HashSet<int>> ports))
                    {
                        ports = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
                        execOrders.Add(edge.FromNode, ports);
                    }
                    if (!ports.TryGetValue(edge.FromPort, out HashSet<int> orders))
                    {
                        orders = new HashSet<int>();
                        ports.Add(edge.FromPort, orders);
                    }
                    if (!orders.Add(edge.Order)) errors.Add(new CompileIssue("E9", edge.ToNode));
                }
                string portKey = edge.ToNode + ":" + edge.ToPort;
                int count = incoming.TryGetValue(portKey, out int previous) ? previous + 1 : 1;
                incoming[portKey] = count;
                if (input.Kind == "exec" && count > 1) errors.Add(new CompileIssue("E2", edge.ToNode));
                if (input.Kind != "mod") continue;
                if (input.Max > 0 && count > input.Max) errors.Add(new CompileIssue("E5", edge.ToNode));
                if (!mods.Add(edge.ToNode + ":" + from.Id)) errors.Add(new CompileIssue("E9", edge.ToNode));
                string effectiveForm = EffectiveFormId(graph.FindNode(edge.ToNode), to);
                if ((from.Id == "mod.pierce" || from.Id == "mod.homing") && effectiveForm != "form.bolt" && to.Id != "spell.call")
                    warnings.Add(new CompileIssue("W2", edge.FromNode));
                if (from.Id == "mod.speed" && effectiveForm != "form.bolt" && effectiveForm != "form.orbit" && to.Id != "spell.call")
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
            Dictionary<string, GraphNode> nodes, Dictionary<string, RuneDefinition> definitions, List<CompileIssue> warnings,
            List<CompileIssue> errors, RuneCatalog runes, BalanceData balance, CompileContext context)
        {
            List<SpellAction> actions = new List<SpellAction>();
            IEnumerable<GraphEdge> branchEdges = graph.Edges.Where(edge => edge.FromNode == nodeId && edge.FromPort == port);
            if (graph.Version == 1) branchEdges = branchEdges.OrderBy(edge => edge.ToNode, StringComparer.Ordinal);
            else branchEdges = graph.Edges.Select((edge, index) => new { edge, index })
                .Where(item => item.edge.FromNode == nodeId && item.edge.FromPort == port)
                .OrderBy(item => item.edge.Order).ThenBy(item => item.index).Select(item => item.edge);
            foreach (GraphEdge edge in branchEdges)
            {
                GraphNode node = nodes[edge.ToNode];
                RuneDefinition rune = definitions[node.Id];
                RuneDefinition element = null;
                RuneDefinition effectForm = rune.Category == "form" ? rune : null;
                CompiledSpell calledSpell = null;
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
                if (rune.Id == "magic.inline")
                {
                    string formId = EffectiveFormId(node, rune);
                    if (!runes.TryGet(formId, out effectForm)) errors.Add(new CompileIssue("E14", node.Id));
                    string elementId = InlineElementRuneId(node.GetText("element", "normal"));
                    if (elementId != null && !runes.TryGet(elementId, out element)) errors.Add(new CompileIssue("E14", node.Id));
                }
                if (rune.Id == "spell.call")
                {
                    string spellId = node.GetText("spellId").Trim();
                    if (context.Compiled.TryGetValue(spellId, out CompiledSpell cached)) calledSpell = cached;
                    else if (context.Library.TryGetValue(spellId, out SpellGraph target))
                    {
                        CompileResult targetResult = CompileGraph(target, runes, balance, context);
                        if (targetResult.Ok) calledSpell = targetResult.Spell;
                        else errors.Add(new CompileIssue("E15", node.Id, target.Name + ": " + targetResult.Errors[0].Message));
                    }
                }
                SpellAction action = new SpellAction(node, rune, effectForm, element, mods, modifierNodes, calledSpell);
                action.SetAttachedNodes(attachedNodeIds);
                foreach (PortDefinition output in rune.Ports)
                {
                    if (output.Direction == "out" && output.Kind == "exec")
                        action.SetBranch(output.Id, BuildBranch(graph, node.Id, output.Id, nodes, definitions, warnings, errors, runes, balance, context));
                }
                actions.Add(action);
            }
            return actions;
        }

        /// <summary>Inline Magic의 표준 원소를 기존 원소 룬 ID로 연결한다.</summary>
        private static string InlineElementRuneId(string element)
        {
            switch (element)
            {
                case "fire": return "elem.fire";
                case "electric": return "elem.arc";
                case "ice": return "elem.ice";
                default: return null;
            }
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
                        sum += action.OwnEnergy + action.Count * (Math.Min(maxHits, hitCap) * Cost(action.OnHit, hitCap)
                            + Cost(action.OnExpire, hitCap) + Cost(action.OnComplete, hitCap));
                        break;
                    case "buff": sum += action.OwnEnergy + action.Count * Cost(action.OnComplete, hitCap); break;
                    case "call":
                        long eventCount = (long)action.Count * action.CalledSpell.WorstCaseEntities;
                        sum += action.OwnEnergy
                            + eventCount * (hitCap * Cost(action.OnHit, hitCap) + Cost(action.OnExpire, hitCap))
                            + action.Count * Cost(action.OnComplete, hitCap);
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
            long sum = 0L;
            const long limit = int.MaxValue;
            foreach (SpellAction action in actions)
            {
                long count;
                switch (action.Kind)
                {
                    case "spawn":
                        int maxHits = action.Form == "bolt" ? 1 + action.Stats.Pierce : hitCap;
                        long hitWork = MultiplyBounded(Math.Min(maxHits, hitCap), EntityCount(action.OnHit, hitCap), limit);
                        long perEntityWork = AddBounded(AddBounded(1L, hitWork, limit), EntityCount(action.OnExpire, hitCap), limit);
                        count = AddBounded(MultiplyBounded(action.Count, perEntityWork, limit),
                            MultiplyBounded(action.Count, EntityCount(action.OnComplete, hitCap), limit), limit);
                        break;
                    case "buff": count = MultiplyBounded(action.Count, EntityCount(action.OnComplete, hitCap), limit); break;
                    case "call":
                        long callEntities = MultiplyBounded(action.Count, action.CalledSpell.WorstCaseEntities, limit);
                        long callEvents = AddBounded(MultiplyBounded(hitCap, EntityCount(action.OnHit, hitCap), limit),
                            EntityCount(action.OnExpire, hitCap), limit);
                        count = AddBounded(callEntities, MultiplyBounded(callEntities, callEvents, limit), limit);
                        count = AddBounded(count, MultiplyBounded(action.Count,
                            EntityCount(action.OnComplete, hitCap), limit), limit);
                        break;
                    case "delay": count = EntityCount(action.Then, hitCap); break;
                    case "repeat": count = MultiplyBounded(action.Times, EntityCount(action.Body, hitCap), limit); break;
                    case "if": count = Math.Max(EntityCount(action.Then, hitCap), EntityCount(action.Else, hitCap)); break;
                    default: count = EntityCount(action.Next, hitCap); break;
                }
                sum = AddBounded(sum, count, limit);
                if (sum == limit) return int.MaxValue;
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
                        count = AddBounded(count, MultiplyBounded(action.Count,
                            ActionCount(action.OnComplete, hitCap, saturation), saturation), saturation);
                        break;
                    case "buff": count = AddBounded(count, MultiplyBounded(action.Count,
                        ActionCount(action.OnComplete, hitCap, saturation), saturation), saturation); break;
                    case "call":
                        long calleeActions = ActionCount(action.CalledSpell.Root, hitCap, saturation);
                        long invocationWork = MultiplyBounded(action.Count, calleeActions, saturation);
                        long callEvents = MultiplyBounded((long)action.Count * action.CalledSpell.WorstCaseEntities,
                            AddBounded(MultiplyBounded(hitCap, ActionCount(action.OnHit, hitCap, saturation), saturation),
                                ActionCount(action.OnExpire, hitCap, saturation), saturation), saturation);
                        count = AddBounded(count, AddBounded(invocationWork, callEvents, saturation), saturation);
                        count = AddBounded(count, MultiplyBounded(action.Count,
                            ActionCount(action.OnComplete, hitCap, saturation), saturation), saturation);
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
                if (action.CalledSpell != null)
                    foreach (string tag in action.CalledSpell.Tags) tags.Add(tag);
                GatherTags(action.OnHit, tags);
                GatherTags(action.OnExpire, tags);
                GatherTags(action.OnComplete, tags);
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
            {
                canonical.Append(edge.FromNode).Append('.').Append(edge.FromPort).Append('>').Append(edge.ToNode).Append('.').Append(edge.ToPort);
                if (graph.Version >= 2) canonical.Append('#').Append(edge.Order);
                canonical.Append(';');
            }
            return HashSignature(canonical);
        }

        /// <summary>소스 그래프와 참조된 컴파일 Spell의 의미를 결합한 안정 시그니처를 반환한다.</summary>
        private static string Signature(SpellGraph graph, IReadOnlyList<SpellAction> root)
        {
            StringBuilder canonical = new StringBuilder(Signature(graph));
            AppendCallSignatures(root, canonical);
            return HashSignature(canonical);
        }

        /// <summary>실행 순서의 호출 참조 ID와 대상 컴파일 시그니처를 누적한다.</summary>
        private static void AppendCallSignatures(IReadOnlyList<SpellAction> actions, StringBuilder canonical)
        {
            foreach (SpellAction action in actions)
            {
                if (action.Kind == "call")
                    canonical.Append("call:").Append(action.CalledSpellId).Append('=').Append(action.CalledSpell.Signature).Append(';');
                AppendCallSignatures(action.OnHit, canonical);
                AppendCallSignatures(action.OnExpire, canonical);
                AppendCallSignatures(action.OnComplete, canonical);
                AppendCallSignatures(action.Then, canonical);
                AppendCallSignatures(action.Else, canonical);
                AppendCallSignatures(action.Body, canonical);
                AppendCallSignatures(action.Next, canonical);
            }
        }

        /// <summary>정규 그래프 표현을 FNV-1a 시그니처로 변환한다.</summary>
        private static string HashSignature(StringBuilder canonical)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte value in Encoding.UTF8.GetBytes(canonical.ToString()))
            {
                hash ^= value;
                hash = unchecked(hash * 1099511628211UL);
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private sealed class CompileContext
        {
            private readonly HashSet<string> _unlockedIds;
            private readonly Dictionary<string, SpellGraph> _library;
            private readonly Dictionary<string, CompiledSpell> _compiled = new Dictionary<string, CompiledSpell>(StringComparer.Ordinal);
            private readonly HashSet<string> _activeSpellIds = new HashSet<string>(StringComparer.Ordinal);
            private readonly int _capacity;
            private readonly float _maxEnergy;
            public HashSet<string> UnlockedIds => _unlockedIds;
            public Dictionary<string, SpellGraph> Library => _library;
            public Dictionary<string, CompiledSpell> Compiled => _compiled;
            public HashSet<string> ActiveSpellIds => _activeSpellIds;
            public int Capacity => _capacity;
            public float MaxEnergy => _maxEnergy;

            /// <summary>라이브러리 참조·해금 상태·컴파일 캐시와 실행 제한을 컴파일 작업에 보관한다.</summary>
            public CompileContext(IEnumerable<string> unlocked, IEnumerable<SpellGraph> library, SpellGraph current, int capacity, float maxEnergy)
            {
                _unlockedIds = new HashSet<string>(unlocked ?? Array.Empty<string>(), StringComparer.Ordinal);
                _unlockedIds.Add(SpellGrammar.INLINE_RUNE);
                _library = new Dictionary<string, SpellGraph>(StringComparer.Ordinal);
                if (library != null)
                    foreach (SpellGraph spell in library)
                        if (spell != null && !string.IsNullOrEmpty(spell.Id)) _library[spell.Id] = spell;
                if (current != null && !string.IsNullOrEmpty(current.Id)) _library[current.Id] = current;
                _capacity = capacity;
                _maxEnergy = maxEnergy;
            }
        }
    }
}
