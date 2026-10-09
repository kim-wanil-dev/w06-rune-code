namespace RuneCode
{
    /// <summary>룬 배치 시 자동으로 만들 연결 하나의 양 끝 노드와 포트다.</summary>
    public readonly struct AutoConnection
    {
        public string FromNode { get; }
        public string FromPort { get; }
        public string ToNode { get; }
        public string ToPort { get; }

        /// <summary>출력 노드·포트와 입력 노드·포트로 자동 연결을 만든다.</summary>
        public AutoConnection(string fromNode, string fromPort, string toNode, string toPort)
        {
            FromNode = fromNode;
            FromPort = fromPort;
            ToNode = toNode;
            ToPort = toPort;
        }
    }

    /// <summary>
    /// 룬을 배치할 때 문법상 자동 연결할 수 있는 대상과 연결 포트를 판단한다.
    /// 효과는 효과를 받을 수 있는 노드의 효과 입력에, Behavior 블록(Apply 제외)은 Shape의 열린 체인 끝에 연결한다.
    /// 거리·화면 배치 같은 편집기 정책은 호출부가 정한다.
    /// </summary>
    public static class SpellAutoConnect
    {
        /// <summary>효과 룬인지 반환한다.</summary>
        public static bool IsModifier(RuneDefinition rune)
        {
            return rune.Category == SpellGrammar.CATEGORY_MODIFIER;
        }

        /// <summary>Shape의 열린 체인 출력에 이어 붙는 Behavior(Apply 제외)인지 반환한다.</summary>
        public static bool IsChainLink(RuneDefinition rune)
        {
            return rune.Category == SpellGrammar.CATEGORY_BEHAVIOR && rune.Id != SpellGrammar.APPLY_RUNE;
        }

        /// <summary>
        /// 배치할 룬 기준으로 그래프의 노드가 자동 연결·배치 기준점인지 반환한다.
        /// 효과는 효과를 받을 수 있는 노드, Behavior는 체인 출력이 비어 있는 Shape 노드가 기준점이다.
        /// </summary>
        public static bool IsAnchor(SpellGraph graph, RuneCatalog runes, RuneDefinition placed, GraphNode node)
        {
            RuneDefinition target = runes.Get(node.RuneId);
            if (IsModifier(placed))
            {
                return GraphCompiler.CanAttachModifier(placed, target);
            }
            if (IsChainLink(placed))
            {
                return HasOpenChainOutput(graph, target, node);
            }
            return false;
        }

        /// <summary>
        /// 배치한 노드와 후보 노드 사이에 문법상 만들 자동 연결을 반환한다. 후보가 기준점이 아니거나 자기 자신이면 false를 반환한다.
        /// 편집 가능 여부·연결 수 제한은 편집 세션의 연결 검사로 따로 확인해야 한다.
        /// </summary>
        public static bool TryGetConnection(SpellGraph graph, RuneCatalog runes, GraphNode placed, GraphNode candidate, out AutoConnection connection)
        {
            connection = default;
            RuneDefinition placedRune = runes.Get(placed.RuneId);
            if (candidate.Id == placed.Id || !IsAnchor(graph, runes, placedRune, candidate))
            {
                return false;
            }

            connection = IsModifier(placedRune)
                ? new AutoConnection(placed.Id, SpellGrammar.MODIFIER_PORT, candidate.Id, SpellGrammar.MODIFIER_PORT)
                : new AutoConnection(candidate.Id, SpellGrammar.CHAIN_OUT, placed.Id, SpellGrammar.CHAIN_IN);
            return true;
        }

        /// <summary>노드에 체인 출력 포트가 있고 아직 연결되지 않았는지 반환한다.</summary>
        private static bool HasOpenChainOutput(SpellGraph graph, RuneDefinition rune, GraphNode node)
        {
            PortDefinition chainOutput = rune.FindPort(SpellGrammar.CHAIN_OUT, SpellGrammar.DIRECTION_OUT);
            if (chainOutput?.Kind != SpellGrammar.CHAIN_KIND)
            {
                return false;
            }

            foreach (GraphEdge edge in graph.Edges)
            {
                if (edge.FromNode == node.Id && edge.FromPort == SpellGrammar.CHAIN_OUT)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
