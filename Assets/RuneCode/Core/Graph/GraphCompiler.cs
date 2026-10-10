using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RuneCode
{
    public static class GraphCompiler
    {
        /// <summary>
        /// 그래프와 해금·RAM·에너지 조건을 검증하고 최악 비용의 실행 트리를 컴파일한다.
        /// modifierStock이 있으면 Modifier 소지량(보관함 전체 공유)을 넘는 배치를 E21 오류로 낸다. null이면 검사하지 않는다.
        /// </summary>
        public static CompileResult Compile(SpellGraph graph, RuneCatalog runes, GrammarLimits limits,
            IEnumerable<string> unlocked, int capacity = -1, float maxEnergy = -1f, IEnumerable<SpellGraph> library = null,
            IReadOnlyDictionary<string, int> modifierStock = null, IReadOnlyDictionary<string, float> maxResourceCosts = null,
            bool legacyBoxWorldAligned = true)
        {
            CompileContext context = new CompileContext(unlocked ?? runes.StartRunes, library, graph, capacity, maxEnergy,
                maxResourceCosts, legacyBoxWorldAligned);
            List<CompileIssue> dependencyErrors = new List<CompileIssue>();
            ValidateSpellDependencies(graph, context.Library, new HashSet<string>(StringComparer.Ordinal),
                new List<string>(), dependencyErrors);
            if (dependencyErrors.Count > 0) return new CompileResult(dependencyErrors, new List<CompileIssue>(), null);
            CompileResult result = CompileGraph(graph, runes, limits, context);
            List<CompileIssue> stockErrors = modifierStock == null ? null : FindModifierStockIssues(graph, runes, library, modifierStock);
            if (stockErrors == null || stockErrors.Count == 0) return result;
            return new CompileResult(result.Errors.Concat(stockErrors).ToList(), result.Warnings, result.Spell);
        }

        /// <summary>그래프에 배치된 Modifier 노드 수를 룬 ID와 등급별로 세어 반환한다.</summary>
        public static Dictionary<string, int> CountModifiers(SpellGraph graph, RuneCatalog runes)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (graph?.Nodes == null) return counts;
            foreach (GraphNode node in graph.Nodes)
            {
                if (node == null || !runes.TryGet(node.RuneId, out RuneDefinition rune) || rune.Category != SpellGrammar.CATEGORY_MODIFIER) continue;
                string grade = SpellGrammar.GetModifierGrade(rune, node, runes.ModifierGrades);
                string key = SpellGrammar.ModifierStockKey(node.RuneId, grade);
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
            }
            return counts;
        }

        /// <summary>
        /// 보관함 전체에서 Modifier 종류·등급별 배치 수를 세어 소지량을 넘는 등급마다 이 그래프의 첫 초과 노드에 E21 오류를 만든다.
        /// 보관함의 같은 ID 그래프는 이 그래프로 대체해 센다. 소지량에 없는 Modifier의 소지량은 0이다.
        /// </summary>
        private static List<CompileIssue> FindModifierStockIssues(SpellGraph graph, RuneCatalog runes, IEnumerable<SpellGraph> library,
            IReadOnlyDictionary<string, int> modifierStock)
        {
            Dictionary<string, int> used = CountModifiers(graph, runes);
            if (library != null)
            {
                foreach (SpellGraph other in library)
                {
                    if (other == null || (!string.IsNullOrEmpty(graph.Id) && other.Id == graph.Id)) continue;
                    foreach (KeyValuePair<string, int> pair in CountModifiers(other, runes))
                    {
                        used.TryGetValue(pair.Key, out int count);
                        used[pair.Key] = count + pair.Value;
                    }
                }
            }
            var issues = new List<CompileIssue>();
            foreach (KeyValuePair<string, int> pair in used)
            {
                modifierStock.TryGetValue(pair.Key, out int stock);
                if (pair.Value <= stock) continue;
                int separator = pair.Key.LastIndexOf('#');
                string runeId = pair.Key.Substring(0, separator);
                string grade = pair.Key.Substring(separator + 1);
                GraphNode node = graph.Nodes.FirstOrDefault(item => item != null && item.RuneId == runeId
                    && runes.TryGet(item.RuneId, out RuneDefinition rune)
                    && SpellGrammar.GetModifierGrade(rune, item, runes.ModifierGrades) == grade);
                if (node != null) issues.Add(new CompileIssue("E21", node.Id, runes.Get(runeId).Name + " " + grade + " " + pair.Value + "/" + stock));
            }
            return issues;
        }

        /// <summary>유효한 종속성을 가진 그래프를 순환 검사 스택에 넣고 한 번 컴파일한다.</summary>
        private static CompileResult CompileGraph(SpellGraph graph, RuneCatalog runes, GrammarLimits limits, CompileContext context)
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
            SpellGraph active = RemoveDeprecatedEdges(graph, runes, chainWarnings);
            SpellGraph normalized = SpellChainNormalizer.Normalize(active, runes, context.UnlockedIds, chainErrors, chainWarnings, out int chainRam);
            CompileResult result = chainErrors.Any(issue => issue.Code != "E10")
                ? new CompileResult(chainErrors, chainWarnings, null)
                : CompileGraphBody(normalized, runes, limits, context, chainErrors, chainWarnings, chainRam);
            if (!string.IsNullOrEmpty(graph.Id)) context.ActiveSpellIds.Remove(graph.Id);
            if (result.Ok && !string.IsNullOrEmpty(graph.Id)) context.Compiled[graph.Id] = result.Spell;
            return result;
        }

        /// <summary>
        /// 지원 중단 포트에서 나가는 연결을 W5 경고와 함께 뺀 컴파일용 그래프를 반환한다. 원본 그래프의 연결은 그대로 보존된다.
        /// 연결 수 상한이 있는 효과 출력(효과 룬은 대상 하나)은 순서상 앞선 연결만 남기고 나머지는 W9 경고와 함께 뺀다.
        /// 남는 연결 중 이전 이벤트 의미로 만든 연결(IsLegacyEvent)에는 W6 경고를 추가한다. 뺄 연결이 없으면 원본을 반환한다.
        /// </summary>
        private static SpellGraph RemoveDeprecatedEdges(SpellGraph graph, RuneCatalog runes, List<CompileIssue> warnings)
        {
            if (graph.Nodes == null || graph.Edges == null) return graph;
            HashSet<GraphEdge> extraModifierEdges = FindExtraModifierEdges(graph, runes);
            var kept = new List<GraphEdge>(graph.Edges.Count);
            foreach (GraphEdge edge in graph.Edges)
            {
                GraphNode from = edge == null ? null : graph.FindNode(edge.FromNode);
                PortDefinition port = from != null && runes.TryGet(from.RuneId, out RuneDefinition rune)
                    ? rune.FindPort(edge.FromPort, SpellGrammar.DIRECTION_OUT) : null;
                if (port != null && port.IsDeprecated)
                {
                    warnings.Add(new CompileIssue("W5", edge.FromNode, edge.FromPort));
                    continue;
                }
                if (edge != null && extraModifierEdges.Contains(edge))
                {
                    warnings.Add(new CompileIssue("W9", edge.FromNode));
                    continue;
                }
                if (port != null && edge.IsLegacyEvent) warnings.Add(new CompileIssue("W6", edge.FromNode, edge.FromPort));
                kept.Add(edge);
            }
            return kept.Count == graph.Edges.Count ? graph : graph.CopyWith(graph.Nodes, kept);
        }

        /// <summary>
        /// 연결 수 상한이 있는 효과 출력마다 순서(Order, 같으면 저장 순)상 상한을 넘는 연결을 찾아 반환한다.
        /// 효과 룬은 대상 하나에만 적용하므로(백서 v3) 기존 그래프의 추가 연결은 보존하되 컴파일에서 뺀다.
        /// </summary>
        private static HashSet<GraphEdge> FindExtraModifierEdges(SpellGraph graph, RuneCatalog runes)
        {
            var extra = new HashSet<GraphEdge>();
            var uses = new Dictionary<(string node, string port), int>();
            foreach (GraphEdge edge in graph.Edges.Where(edge => edge != null).OrderBy(edge => edge.Order))
            {
                GraphNode from = graph.FindNode(edge.FromNode);
                PortDefinition port = from != null && runes.TryGet(from.RuneId, out RuneDefinition rune)
                    ? rune.FindPort(edge.FromPort, SpellGrammar.DIRECTION_OUT) : null;
                if (port == null || port.Kind != "mod" || port.Max <= 0) continue;
                uses.TryGetValue((edge.FromNode, edge.FromPort), out int count);
                uses[(edge.FromNode, edge.FromPort)] = count + 1;
                if (count >= port.Max) extra.Add(edge);
            }
            return extra;
        }

        /// <summary>
        /// 체인 정규화를 마친 그래프의 해금·RAM·에너지 조건을 검사하고 실행 트리 및 비용 상한을 계산한다.
        /// 정규화 단계의 오류·경고 목록에 이어서 기록하며 chainRam은 합쳐진 문법 블록의 RAM이다.
        /// </summary>
        private static CompileResult CompileGraphBody(SpellGraph graph, RuneCatalog runes, GrammarLimits limits, CompileContext context,
            List<CompileIssue> errors, List<CompileIssue> warnings, int chainRam)
        {
            if (graph.Version < 1 || graph.Version > SpellGraph.CURRENT_VERSION || graph.Nodes == null || graph.Edges == null
                || graph.Nodes.Count > limits.MaxGraphNodes || graph.Edges.Count > limits.MaxGraphEdges)
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
                ValidateParameters(node, rune, runes.ModifierGrades, errors);
                ValidateMagicNode(node, rune, graph.Edges, errors);
            }
            if (cores.Count != 1) errors.Add(new CompileIssue("E1"));
            ValidateEdges(graph, definitions, runes, errors, warnings);
            if (!errors.Any(issue => issue.Code == "E3") && HasCycle(graph, nodes)) errors.Add(new CompileIssue("E4"));
            int limit = context.Capacity < 0 ? limits.BaseCapacity : context.Capacity;
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
            List<SpellAction> root = BuildBranch(graph, cores[0].Id, "exec", nodes, definitions, warnings, errors, runes, limits, context);
            if (errors.Any(issue => issue.Code == "E11" || issue.Code == "E12" || issue.Code == "E14" || issue.Code == "E15"))
                return new CompileResult(errors, warnings, null);
            ResourceCostSet resourceCosts = Cost(root, limits.HitTriggerCap);
            int entities = EntityCount(root, limits.HitTriggerCap);
            long actionCeiling = limits.MaxCompiledActions;
            long expandedActions = AddBounded(1L, ActionCount(root, limits.HitTriggerCap, actionCeiling + 1L), actionCeiling + 1L);
            // 최대 예상 비용은 시전을 막지 않는 정보다(백서 7.2). 실행 수 상한만 안전 제약으로 오류 처리한다.
            if (expandedActions > actionCeiling) errors.Add(new CompileIssue("E8"));
            foreach (ResourceAmount cost in resourceCosts.Amounts)
            {
                if (double.IsInfinity(cost.Amount) || double.IsNaN(cost.Amount))
                {
                    warnings.Add(new CompileIssue("W8", null, cost.Resource));
                }
                else if (context.TryGetMaxResource(cost.Resource, limits, out float maximum) && cost.Amount > maximum)
                {
                    warnings.Add(new CompileIssue("W7", null, cost.Resource + "="
                        + cost.Amount.ToString("0.#", CultureInfo.InvariantCulture) + "/" + maximum.ToString("0.#", CultureInfo.InvariantCulture)));
                }
            }
            if (entities > limits.MaxLiveSpellEntities) errors.Add(new CompileIssue("E17"));
            SortedSet<string> tags = new SortedSet<string>(StringComparer.Ordinal);
            GatherTags(root, tags);
            string trigger = cores[0].GetText(SpellGrammar.TRIGGER_PARAM, SpellGrammar.TRIGGER_ON_ATTACK);
            CompiledSpell compiled = new CompiledSpell(graph.Name, Signature(graph, root), cores[0].Id, ramUsed, resourceCosts,
                limits.CooldownBase + limits.CooldownPerRam * ramUsed, entities, tags.ToList(), root, graph.Id, trigger);
            return new CompileResult(errors, warnings, compiled);
        }

        /// <summary>연결 후보가 입력 수·포트 종류·지원 중단 포트·순환·수식 슬롯 규칙을 지키는지 반환한다.</summary>
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
            else if (output.IsDeprecated)
                issue = new CompileIssue("E19", edge.FromNode, edge.FromPort);
            else if (output.Kind == SpellGrammar.CHAIN_KIND && (!SpellGrammar.IsValidChainLink(fromRune, toRune)
                || graph.Edges.Any(current => current.FromNode == edge.FromNode && current.FromPort == edge.FromPort)
                || graph.Edges.Any(current => current.ToNode == edge.ToNode && current.ToPort == edge.ToPort)))
                issue = new CompileIssue("E18", edge.ToNode);
            else if (input.Kind == "mod" && !CanAttachModifier(fromRune, toRune))
                issue = new CompileIssue("E13", edge.ToNode);
            else if (fromRune.Id == "magic.inline" && from.GetText("magicType", "sphere") == "buff"
                && (edge.FromPort == SpellGrammar.ON_HIT_PORT || edge.FromPort == SpellGrammar.ON_EXPIRE_PORT))
                issue = new CompileIssue("E16", edge.FromNode);
            else if (graph.Version >= 2 && output.Kind == "exec" && graph.Edges.Any(current => current.FromNode == edge.FromNode
                && current.FromPort == edge.FromPort && current.Order == edge.Order))
                issue = new CompileIssue("E9", edge.ToNode);
            else if (graph.Edges.Any(current => current.FromNode == edge.FromNode && current.FromPort == edge.FromPort
                && current.ToNode == edge.ToNode && current.ToPort == edge.ToPort))
                issue = new CompileIssue("E9", edge.ToNode);
            else if (input.Kind == "exec" && graph.Edges.Any(current => current.ToNode == edge.ToNode && current.ToPort == edge.ToPort))
                issue = new CompileIssue("E2", edge.ToNode);
            else if (input.Kind == "mod" && input.Max > 0 && graph.Edges.Count(current => current.ToNode == edge.ToNode && current.ToPort == edge.ToPort) >= input.Max)
                issue = new CompileIssue("E5", edge.ToNode);
            else if (output.Kind == "mod" && output.Max > 0 && graph.Edges.Count(current => current.FromNode == edge.FromNode && current.FromPort == edge.FromPort) >= output.Max)
                issue = new CompileIssue("E20", edge.FromNode);
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

        /// <summary>효과 입력에는 Behavior 블록·프리셋 호출(및 내부 Inline Magic)에 효과 룬만 연결할 수 있는지 반환한다.</summary>
        internal static bool CanAttachModifier(RuneDefinition source, RuneDefinition target)
        {
            if (target.Category == SpellGrammar.CATEGORY_BEHAVIOR || target.Id == SpellGrammar.CALL_RUNE || target.Id == SpellGrammar.INLINE_RUNE)
                return source.Category == "modifier";
            return target.Category == "form" && (source.Category == "modifier" || source.Category == "element");
        }

        /// <summary>
        /// Inline Magic의 종류·속성·형태 조합과 버프의 이벤트 출력을 검사한다. 해금은 문법 블록 단계에서 검사한다.
        /// Apply는 회복·보호·화염, 회복 Shape는 Burst·Persist, 보호 Shape는 Burst와 구형 Orbit(방벽)만 허용한다.
        /// </summary>
        private static void ValidateMagicNode(GraphNode node, RuneDefinition rune, IReadOnlyList<GraphEdge> edges, List<CompileIssue> errors)
        {
            if (rune.Id != "magic.inline") return;
            string magicType = node.GetText("magicType", "sphere");
            string element = node.GetText("element", "normal");
            string form = node.GetText("form", "launch");
            bool isBuffElement = element == "heal" || element == "protection" || element == "fire";
            bool isProtectionOrbit = magicType == "sphere" && element == "protection" && form == "orbit";
            bool isValid;
            if (magicType == "buff") isValid = isBuffElement && form == "remain";
            else if (element == "heal") isValid = form == "explosion" || form == "remain" || form == SpellGrammar.FORM_BEAM;
            else if (element == "protection") isValid = form == "explosion" || isProtectionOrbit || form == SpellGrammar.FORM_BEAM;
            else isValid = true;
            // 부채꼴은 실행 위치에서 방향으로 펼친 범위(Burst·Persist)와 진행 방향으로 펼친 부채꼴 발사체(Launch)에만 정의되어 있다.
            if (magicType == SpellGrammar.MAGIC_TYPE_CONE && form != "explosion" && form != "remain" && form != "launch") isValid = false;
            // Beam은 Box Shape와만 조합할 수 있다(백서 v5). 다른 Shape면 E14다.
            if (form == SpellGrammar.FORM_BEAM && magicType != SpellGrammar.MAGIC_TYPE_BOX) isValid = false;
            if (!isValid) errors.Add(new CompileIssue("E14", node.Id));
            if (magicType == "buff" && edges.Any(edge => edge.FromNode == node.Id
                && (edge.FromPort == SpellGrammar.ON_HIT_PORT || edge.FromPort == SpellGrammar.ON_EXPIRE_PORT)))
                errors.Add(new CompileIssue("E16", node.Id));
        }

        /// <summary>
        /// 효과 룬이 대상 노드에 효과를 내는지 modifier_compat 테이블로 판정한다. 효과 룬이 아니거나 대상이 이전 형태 룬이면 true다.
        /// 효과가 없는 효과 룬은 부착은 유지하되 실행 수치·비용 배율에서 제외한다.
        /// </summary>
        private static bool IsModifierEffective(RuneDefinition modifier, GraphNode target, RuneDefinition targetRune, RuneCatalog runes)
        {
            if (modifier.Category != SpellGrammar.CATEGORY_MODIFIER) return true;
            string targetId = ModifierTargetId(target, targetRune);
            return targetId == null || runes.IsModifierCompatible(modifier.Id, targetId);
        }

        /// <summary>
        /// 효과 호환성 판정에 쓸 대상 룬 ID를 반환한다. Inline Magic은 합쳐지기 전 Behavior ID(버프는 Apply)로 되돌린다.
        /// 판정 대상이 아니면 null을 반환한다.
        /// </summary>
        private static string ModifierTargetId(GraphNode node, RuneDefinition rune)
        {
            if (rune.Category == SpellGrammar.CATEGORY_BEHAVIOR || rune.Id == SpellGrammar.CALL_RUNE) return rune.Id;
            if (rune.Id != SpellGrammar.INLINE_RUNE) return null;
            if (node.GetText("magicType", SpellGrammar.MAGIC_TYPE_SPHERE) == SpellGrammar.MAGIC_TYPE_BUFF) return SpellGrammar.APPLY_RUNE;
            switch (node.GetText("form", "launch"))
            {
                case "launch": return "behavior.launch";
                case "explosion": return "behavior.burst";
                case "orbit": return "behavior.orbit";
                case "remain": return "behavior.persist";
                case SpellGrammar.FORM_BEAM: return "behavior.beam";
                default: return null;
            }
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
                case SpellGrammar.FORM_BEAM: return "form.beam";
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

        /// <summary>각 노드의 숫자·열거 파라미터, Modifier 등급 범위와 중복 키를 검증한다.</summary>
        private static void ValidateParameters(GraphNode node, RuneDefinition rune, ModifierGradeTable modifierGrades, List<CompileIssue> errors)
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
                bool isModifierGrade = value.Key == SpellGrammar.MODIFIER_GRADE_PARAM && rune.Category == SpellGrammar.CATEGORY_MODIFIER;
                bool isInvalid = !seen.Add(value.Key) || (!isModifierGrade && definition == null);
                if (!isInvalid && isModifierGrade)
                {
                    isInvalid = value.Text == null || modifierGrades == null || !modifierGrades.IsGradeAvailable(rune.Id, value.Text);
                }
                else if (!isInvalid && definition.Kind == "number")
                {
                    isInvalid = float.IsNaN(value.Number) || float.IsInfinity(value.Number)
                        || value.Number < definition.Min || value.Number > definition.Max;
                    if (value.Key == "times" && value.Number != (int)value.Number) isInvalid = true;
                }
                if (!isInvalid && !isModifierGrade && definition.Kind == "enum") isInvalid = !definition.Options.Contains(value.Text);
                if (!isInvalid && !isModifierGrade && definition.Kind == "text") isInvalid = value.Text != null && value.Text.Length > 80;
                if (isInvalid) errors.Add(new CompileIssue("E9", node.Id, value.Key));
            }
        }

        /// <summary>연결의 포트 종류·입력 수·실행 순번·속성 수·수식 중복 오류와, 대상에 효과가 없는 수식 경고(W2)를 추가한다.</summary>
        private static void ValidateEdges(SpellGraph graph, Dictionary<string, RuneDefinition> definitions, RuneCatalog runes,
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
                    && (edge.FromPort == SpellGrammar.ON_HIT_PORT || edge.FromPort == SpellGrammar.ON_EXPIRE_PORT))
                {
                    errors.Add(new CompileIssue("E16", edge.FromNode));
                    continue;
                }
                if (input.Kind == "mod" && !CanAttachModifier(from, to))
                {
                    errors.Add(new CompileIssue("E13", edge.ToNode));
                    continue;
                }
                if (graph.Version >= 2 && output.Kind == "exec")
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
                if (!IsModifierEffective(from, graph.FindNode(edge.ToNode), to, runes))
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
                // onFirstHitOrExpire는 소멸 시에도 실행되어 대상이 없을 수 있으므로 onExpire처럼 대상 없음으로 본다.
                bool nextHasTarget = edge.FromPort == SpellGrammar.ON_HIT_PORT
                    || (hasTarget && edge.FromPort != SpellGrammar.ON_EXPIRE_PORT && edge.FromPort != SpellGrammar.ON_FIRST_HIT_OR_EXPIRE_PORT);
                FindReachable(graph, edge.ToNode, nextHasTarget, reachable, definitions, warnings);
            }
        }

        /// <summary>출력 포트에 연결된 노드를 안정된 순서의 실행 명령으로 변환한다.</summary>
        private static List<SpellAction> BuildBranch(SpellGraph graph, string nodeId, string port,
            Dictionary<string, GraphNode> nodes, Dictionary<string, RuneDefinition> definitions, List<CompileIssue> warnings,
            List<CompileIssue> errors, RuneCatalog runes, GrammarLimits limits, CompileContext context)
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
                    // 효과가 없는 효과 룬은 실행 수치와 비용에서 빼고 편집기 안내(W2)만 남긴다.
                    if (!IsModifierEffective(attached, node, rune, runes)) continue;
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
                        CompileResult targetResult = CompileGraph(target, runes, limits, context);
                        if (targetResult.Ok) calledSpell = targetResult.Spell;
                        else errors.Add(new CompileIssue("E15", node.Id, target.Name, targetResult.Errors[0]));
                    }
                }
                RuneDefinition shape = null;
                if (rune.Id == SpellGrammar.INLINE_RUNE && node.GetText("magicType", SpellGrammar.MAGIC_TYPE_SPHERE) != SpellGrammar.MAGIC_TYPE_BUFF)
                    runes.TryGet(SpellGrammar.ShapeRune(node.GetText("magicType", SpellGrammar.MAGIC_TYPE_SPHERE)), out shape);
                float coneAngle = shape?.Stats.ConeAngle ?? 0f;
                SpellAction action = new SpellAction(node, rune, effectForm, element, mods, modifierNodes, calledSpell,
                    shape, coneAngle, runes.ModifierGrades, context.LegacyBoxWorldAligned);
                action.SetAttachedNodes(attachedNodeIds);
                foreach (PortDefinition output in rune.Ports)
                {
                    if (output.Direction == "out" && output.Kind == "exec")
                        action.SetBranch(output.Id, BuildBranch(graph, node.Id, output.Id, nodes, definitions, warnings, errors, runes, limits, context));
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

        /// <summary>
        /// 개체 하나가 이벤트를 낼 수 있는 최대 OnHit 횟수를 반환한다. 발사체는 관통 허용 수 + 1(마지막은 적 또는 벽),
        /// 공전·Burst·Beam은 대상마다 내므로 개체당 이벤트 안전 상한을 쓴다. Burst·Beam의 마나 비용은 Cost에서 산정 불가로 따로 다룬다.
        /// </summary>
        private static int MaxHitEvents(SpellAction action, int hitCap)
        {
            return action.Form == SpellGrammar.FORM_BOLT ? Math.Min(1 + action.Stats.Pierce, hitCap) : hitCap;
        }

        /// <summary>
        /// 최악 분기와 이벤트 상한을 포함한 에너지 비용을 합산한다. 발사체는 명중 경로(OnHit)와 미명중 소멸 경로(OnExpire)가
        /// 서로 배타적이라 큰 쪽만 더하고, OnFirstHitOrExpire는 개체당 한 번 더한다.
        /// </summary>
        private static ResourceCostSet Cost(IReadOnlyList<SpellAction> actions, int hitCap,
            SpellModifierValues inheritedModifiers = null, double costMultiplier = 1d)
        {
            ResourceCostSet sum = ResourceCostSet.Empty;
            inheritedModifiers = inheritedModifiers ?? SpellModifierValues.None;
            foreach (SpellAction action in actions)
            {
                ResourceCostSet ownCosts = action.GetNodeCosts(inheritedModifiers).Multiply(costMultiplier);
                switch (action.Kind)
                {
                    case "spawn":
                        ResourceCostSet hitChainCost = Cost(action.OnHit, hitCap, inheritedModifiers, costMultiplier);
                        // Burst·Beam.OnHit은 개체당 상한이 없어(대상마다 1회) 후속 비용이 있으면 최대 비용을 산정할 수 없다(W8).
                        ResourceCostSet hitCost = action.Form == SpellGrammar.FORM_BURST || action.Form == SpellGrammar.FORM_BEAM
                            ? (HasCosts(hitChainCost) ? hitChainCost.MarkUnbounded() : ResourceCostSet.Empty)
                            : hitChainCost.Multiply(MaxHitEvents(action, hitCap));
                        ResourceCostSet expireCost = Cost(action.OnExpire, hitCap, inheritedModifiers, costMultiplier);
                        ResourceCostSet eventCost = action.Form == SpellGrammar.FORM_BOLT
                            ? ResourceCostSet.Max(hitCost, expireCost) : hitCost.Add(expireCost);
                        sum = sum.Add(ownCosts).Add(eventCost.Add(Cost(action.OnFirstHitOrExpire, hitCap,
                            inheritedModifiers, costMultiplier)).Multiply(action.Count));
                        break;
                    case "buff": sum = sum.Add(ownCosts).Add(Cost(action.OnFirstHitOrExpire, hitCap,
                        inheritedModifiers, costMultiplier).Multiply(action.Count)); break;
                    case "call":
                        long eventCount = (long)action.Count * action.CalledSpell.WorstCaseEntities;
                        SpellModifierValues callModifiers = inheritedModifiers.Combine(action.ModifierValues);
                        double callCostMultiplier = costMultiplier * action.EnergyMultiplier;
                        ResourceCostSet calledCosts = Cost(action.CalledSpell.Root, hitCap, callModifiers, callCostMultiplier)
                            .Multiply(action.Count);
                        ResourceCostSet eventCosts = Cost(action.OnHit, hitCap, inheritedModifiers, costMultiplier).Multiply(hitCap)
                            .Add(Cost(action.OnExpire, hitCap, inheritedModifiers, costMultiplier))
                            .Add(Cost(action.OnFirstHitOrExpire, hitCap, inheritedModifiers, costMultiplier));
                        sum = sum.Add(ownCosts).Add(calledCosts).Add(eventCosts.Multiply(eventCount));
                        break;
                    case "delay": sum = sum.Add(Cost(action.Then, hitCap, inheritedModifiers, costMultiplier)); break;
                    case "repeat": sum = sum.Add(Cost(action.Body, hitCap, inheritedModifiers, costMultiplier).Multiply(action.Times))
                        .Add(Cost(action.OnComplete, hitCap, inheritedModifiers, costMultiplier)); break;
                    case "if": sum = sum.Add(ResourceCostSet.Max(Cost(action.Then, hitCap, inheritedModifiers, costMultiplier),
                        Cost(action.Else, hitCap, inheritedModifiers, costMultiplier))); break;
                    default: sum = sum.Add(ownCosts).Add(Cost(action.Next, hitCap, inheritedModifiers, costMultiplier)); break;
                }
            }
            return sum;
        }

        /// <summary>자원별 비용 묶음에 양수 비용이 하나라도 있는지 반환한다.</summary>
        private static bool HasCosts(ResourceCostSet costs)
        {
            foreach (ResourceAmount amount in costs.Amounts)
                if (amount.Amount > 0 || double.IsInfinity(amount.Amount)) return true;
            return false;
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
                        long hitWork = MultiplyBounded(MaxHitEvents(action, hitCap), EntityCount(action.OnHit, hitCap), limit);
                        long expireWork = EntityCount(action.OnExpire, hitCap);
                        long pathWork = action.Form == SpellGrammar.FORM_BOLT ? Math.Max(hitWork, expireWork) : AddBounded(hitWork, expireWork, limit);
                        long eventWork = AddBounded(pathWork, EntityCount(action.OnFirstHitOrExpire, hitCap), limit);
                        long perEntityWork = AddBounded(1L, eventWork, limit);
                        count = MultiplyBounded(action.Count, perEntityWork, limit);
                        break;
                    case "buff": count = MultiplyBounded(action.Count, EntityCount(action.OnFirstHitOrExpire, hitCap), limit); break;
                    case "call":
                        long callEntities = MultiplyBounded(action.Count, action.CalledSpell.WorstCaseEntities, limit);
                        long callEvents = AddBounded(MultiplyBounded(hitCap, EntityCount(action.OnHit, hitCap), limit),
                            AddBounded(EntityCount(action.OnExpire, hitCap), EntityCount(action.OnFirstHitOrExpire, hitCap), limit), limit);
                        count = AddBounded(callEntities, MultiplyBounded(callEntities, callEvents, limit), limit);
                        break;
                    case "delay": count = EntityCount(action.Then, hitCap); break;
                    case "repeat":
                        count = AddBounded(MultiplyBounded(action.Times, EntityCount(action.Body, hitCap), limit),
                            EntityCount(action.OnComplete, hitCap), limit);
                        break;
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
                        long hitWork = MultiplyBounded(MaxHitEvents(action, hitCap), ActionCount(action.OnHit, hitCap, saturation), saturation);
                        long expireWork = ActionCount(action.OnExpire, hitCap, saturation);
                        long pathWork = action.Form == SpellGrammar.FORM_BOLT ? Math.Max(hitWork, expireWork) : AddBounded(hitWork, expireWork, saturation);
                        long eventWork = AddBounded(pathWork, ActionCount(action.OnFirstHitOrExpire, hitCap, saturation), saturation);
                        count = AddBounded(count, MultiplyBounded(action.Count, eventWork, saturation), saturation);
                        break;
                    case "buff": count = AddBounded(count, MultiplyBounded(action.Count,
                        ActionCount(action.OnFirstHitOrExpire, hitCap, saturation), saturation), saturation); break;
                    case "call":
                        long calleeActions = ActionCount(action.CalledSpell.Root, hitCap, saturation);
                        long invocationWork = MultiplyBounded(action.Count, calleeActions, saturation);
                        long callHitWork = MultiplyBounded(hitCap, ActionCount(action.OnHit, hitCap, saturation), saturation);
                        long callExpireWork = AddBounded(ActionCount(action.OnExpire, hitCap, saturation),
                            ActionCount(action.OnFirstHitOrExpire, hitCap, saturation), saturation);
                        long callEvents = MultiplyBounded((long)action.Count * action.CalledSpell.WorstCaseEntities,
                            AddBounded(callHitWork, callExpireWork, saturation), saturation);
                        count = AddBounded(count, AddBounded(invocationWork, callEvents, saturation), saturation);
                        break;
                    case "delay": count = AddBounded(count, ActionCount(action.Then, hitCap, saturation), saturation); break;
                    case "repeat":
                        long repeatWork = AddBounded(MultiplyBounded(action.Times, ActionCount(action.Body, hitCap, saturation), saturation),
                            ActionCount(action.OnComplete, hitCap, saturation), saturation);
                        count = AddBounded(count, repeatWork, saturation);
                        break;
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
                GatherTags(action.OnFirstHitOrExpire, tags);
                GatherTags(action.Then, tags);
                GatherTags(action.Else, tags);
                GatherTags(action.Body, tags);
                GatherTags(action.OnComplete, tags);
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
                AppendCallSignatures(action.OnFirstHitOrExpire, canonical);
                AppendCallSignatures(action.Then, canonical);
                AppendCallSignatures(action.Else, canonical);
                AppendCallSignatures(action.Body, canonical);
                AppendCallSignatures(action.OnComplete, canonical);
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
            private readonly IReadOnlyDictionary<string, float> _maxResourceCosts;
            private readonly bool _legacyBoxWorldAligned;
            public HashSet<string> UnlockedIds => _unlockedIds;
            public Dictionary<string, SpellGraph> Library => _library;
            public Dictionary<string, CompiledSpell> Compiled => _compiled;
            public HashSet<string> ActiveSpellIds => _activeSpellIds;
            public int Capacity => _capacity;
            public float MaxEnergy => _maxEnergy;
            public bool LegacyBoxWorldAligned => _legacyBoxWorldAligned;

            /// <summary>라이브러리·해금·컴파일 캐시·자원 한도와 레거시 Box 기본 방향을 컴파일 작업에 보관한다.</summary>
            public CompileContext(IEnumerable<string> unlocked, IEnumerable<SpellGraph> library, SpellGraph current, int capacity,
                float maxEnergy, IReadOnlyDictionary<string, float> maxResourceCosts, bool legacyBoxWorldAligned)
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
                _maxResourceCosts = maxResourceCosts;
                _legacyBoxWorldAligned = legacyBoxWorldAligned;
            }

            /// <summary>자원별 최대 비용 한도를 반환하고 등록되지 않은 자원에는 false를 반환한다.</summary>
            public bool TryGetMaxResource(string resource, GrammarLimits limits, out float maximum)
            {
                if (_maxResourceCosts != null && _maxResourceCosts.TryGetValue(resource, out maximum)) return true;
                if (resource == "mana")
                {
                    maximum = _maxEnergy < 0f ? limits.MaxEnergy : _maxEnergy;
                    return true;
                }
                maximum = 0f;
                return false;
            }
        }
    }
}
