using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RuneCode
{
    public sealed class SpellProgramIssue
    {
        private readonly string _code;
        private readonly string _nodeId;
        public string Code => _code;
        public string NodeId => _nodeId;

        /// <summary>검증 코드와 관련 노드 ID(없으면 null)로 이슈를 생성한다.</summary>
        public SpellProgramIssue(string code, string nodeId = null)
        {
            _code = code;
            _nodeId = nodeId;
        }
    }

    /// <summary>편집 그래프를 0..N-1 색인으로 바꾼 읽기 전용 실행 그래프다. 검증 결과, 도달성, 파라미터 값, 서명을 함께 가진다.</summary>
    public sealed class SpellProgram
    {
        private const int MAX_PORTS = 2;
        private const int NO_INDEX = -1;
        private const double PERCENT_SCALE = 100.0;
        private const ulong FNV_OFFSET_BASIS = 14695981039346656037UL;
        private const ulong FNV_PRIME = 1099511628211UL;
        private const string NUMBER_FORMAT = "R";
        private const string HASH_FORMAT = "x16";

        private readonly Dictionary<string, int> _indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<SpellProgramIssue> _errors = new List<SpellProgramIssue>();
        private readonly List<SpellProgramIssue> _warnings = new List<SpellProgramIssue>();
        private readonly string[] _nodeIds;
        private readonly SpellNodeKind[] _kinds;
        private readonly bool[] _isValidNode;
        private readonly double[] _dwells;
        private readonly double[] _numbers;
        private readonly AimMode[] _aims;
        private readonly BranchCondition[] _conditions;
        private readonly bool[] _isGreaterOrEqual;
        private readonly bool[] _isReachable;
        private readonly int[] _nextTargets;
        private readonly int[] _nextInPorts;
        private readonly double _defaultLoad;
        private readonly double _defaultRatio;
        private readonly double _defaultShare;
        private readonly double _defaultThreshold;
        private string _signature = string.Empty;
        private int _castNode = NO_INDEX;
        private int _onHitNode = NO_INDEX;

        public bool IsValid => _errors.Count == 0;
        public IReadOnlyList<SpellProgramIssue> Errors => _errors;
        public IReadOnlyList<SpellProgramIssue> Warnings => _warnings;
        public int NodeCount => _nodeIds.Length;
        public int CastNode => _castNode;
        public int OnHitNode => _onHitNode;
        public string Signature => _signature;

        /// <summary>노드 수에 맞춰 색인 배열을 할당하고 설정에서 파라미터 기본값을 읽어 둔다. 다음 링크는 모두 NO_INDEX로 채운다.</summary>
        private SpellProgram(SpellSettings settings, int nodeCount)
        {
            _nodeIds = new string[nodeCount];
            _kinds = new SpellNodeKind[nodeCount];
            _isValidNode = new bool[nodeCount];
            _dwells = new double[nodeCount];
            _numbers = new double[nodeCount];
            _aims = new AimMode[nodeCount];
            _conditions = new BranchCondition[nodeCount];
            _isGreaterOrEqual = new bool[nodeCount];
            _isReachable = new bool[nodeCount];
            _nextTargets = new int[nodeCount * MAX_PORTS];
            _nextInPorts = new int[nodeCount * MAX_PORTS];
            for (int slot = 0; slot < _nextTargets.Length; slot++)
            {
                _nextTargets[slot] = NO_INDEX;
                _nextInPorts[slot] = NO_INDEX;
            }
            _defaultLoad = settings.LoadDefault;
            _defaultRatio = settings.AmplifyDefault;
            _defaultShare = settings.ForkShareDefault / PERCENT_SCALE;
            _defaultThreshold = settings.BranchThresholdDefault;
        }

        /// <summary>편집 그래프와 마법 설정으로 실행 그래프를 만든다. 그래프가 null이면 노드 0개와 G1 오류를 가진 객체를 반환하며, 구조 오류가 있어도 객체를 반환한다.</summary>
        public static SpellProgram Build(SpellGraph graph, SpellSettings settings)
        {
            IReadOnlyList<GraphNode> nodes = graph?.Nodes ?? Array.Empty<GraphNode>();
            IReadOnlyList<GraphEdge> edges = graph?.Edges ?? Array.Empty<GraphEdge>();
            SpellProgram program = new SpellProgram(settings, nodes.Count);
            program.ReadNodes(nodes, settings);
            program.CheckAnchors();
            program.LinkEdges(edges);
            program.MarkReachability();
            program.CheckProjectileReach();
            program._signature = graph == null ? string.Empty : ComputeSignature(nodes, edges);
            return program;
        }

        /// <summary>노드 색인에 해당하는 노드 ID를 반환한다. 잘못된 노드는 null일 수 있다.</summary>
        public string GetNodeId(int node) => _nodeIds[node];

        /// <summary>노드 ID의 색인을 찾아 반환하며 ID가 없거나 null이면 -1을 반환한다.</summary>
        public int IndexOf(string nodeId)
        {
            if (nodeId == null) return NO_INDEX;
            return _indexById.TryGetValue(nodeId, out int index) ? index : NO_INDEX;
        }

        /// <summary>노드의 종류를 반환한다. 잘못된 노드는 Add로 기록된다.</summary>
        public SpellNodeKind GetKind(int node) => _kinds[node];

        /// <summary>노드 종류의 입력 포트 수를 반환하며 잘못된 노드는 0을 반환한다.</summary>
        public int GetInputCount(int node) => _isValidNode[node] ? SpellNodes.GetInputCount(_kinds[node]) : 0;

        /// <summary>노드 종류의 출력 포트 수를 반환하며 잘못된 노드는 0을 반환한다.</summary>
        public int GetOutputCount(int node) => _isValidNode[node] ? SpellNodes.GetOutputCount(_kinds[node]) : 0;

        /// <summary>노드가 토큰을 머무르게 하는 시간(초)을 반환한다.</summary>
        public double GetDwell(int node) => _dwells[node];

        /// <summary>출력 포트에 연결된 다음 노드와 도착 입력 포트를 반환하며 엣지가 있으면 true를 반환한다. 포트 번호가 범위를 벗어나면 false를 반환한다.</summary>
        public bool TryGetNext(int node, int outPort, out int target, out int inPort)
        {
            if (outPort < 0 || outPort >= MAX_PORTS)
            {
                target = NO_INDEX;
                inPort = NO_INDEX;
                return false;
            }
            int slot = node * MAX_PORTS + outPort;
            target = _nextTargets[slot];
            inPort = _nextInPorts[slot];
            return target != NO_INDEX;
        }

        /// <summary>시전 노드나 적중 노드에서 출력 엣지를 따라 도달할 수 있는 노드인지 반환한다.</summary>
        public bool IsReachable(int node) => _isReachable[node];

        /// <summary>시전 노드의 조준 모드를 반환하며 시전이 아니면 커서 조준을 반환한다.</summary>
        public AimMode GetAim(int node) => _kinds[node] == SpellNodeKind.Cast ? _aims[node] : AimMode.Cursor;

        /// <summary>시전 노드의 적재량을 반환하며 시전이 아니면 설정 기본 적재량을 반환한다.</summary>
        public double GetLoad(int node) => _kinds[node] == SpellNodeKind.Cast ? _numbers[node] : _defaultLoad;

        /// <summary>증폭 노드의 배율을 반환하며 증폭이 아니면 설정 기본 배율을 반환한다.</summary>
        public double GetRatio(int node) => _kinds[node] == SpellNodeKind.Amplify ? _numbers[node] : _defaultRatio;

        /// <summary>분배 노드의 비율을 0에서 1 사이 값으로 반환하며 분배가 아니면 설정 기본 비율을 반환한다.</summary>
        public double GetShare(int node) => _kinds[node] == SpellNodeKind.Fork ? _numbers[node] : _defaultShare;

        /// <summary>분기 노드의 조건을 반환하며 분기가 아니면 위력 조건을 반환한다.</summary>
        public BranchCondition GetCondition(int node) => _kinds[node] == SpellNodeKind.Branch ? _conditions[node] : BranchCondition.Power;

        /// <summary>분기 노드의 비교가 이상(≥)인지 반환하며 분기가 아니면 true를 반환한다.</summary>
        public bool IsGreaterOrEqual(int node) => _kinds[node] != SpellNodeKind.Branch || _isGreaterOrEqual[node];

        /// <summary>분기 노드의 기준값을 반환하며 분기가 아니면 설정 기본 기준값을 반환한다.</summary>
        public double GetThreshold(int node) => _kinds[node] == SpellNodeKind.Branch ? _numbers[node] : _defaultThreshold;

        /// <summary>노드를 그래프 순서대로 색인하고 ID, 종류, 파라미터를 채운다. 중복 ID, 알 수 없는 종류, null 노드는 G5로 기록하고 종류를 Add로 둔다.</summary>
        private void ReadNodes(IReadOnlyList<GraphNode> nodes, SpellSettings settings)
        {
            for (int index = 0; index < nodes.Count; index++)
            {
                GraphNode node = nodes[index];
                string nodeId = node?.Id;
                bool hasId = !string.IsNullOrEmpty(nodeId);
                bool isUniqueId = hasId && !_indexById.ContainsKey(nodeId);
                SpellNodeKind kind = SpellNodeKind.Add;
                bool isKnownKind = node != null && SpellNodes.TryParse(node.RuneId, out kind);
                _nodeIds[index] = nodeId;
                if (isUniqueId) _indexById.Add(nodeId, index);
                _isValidNode[index] = isUniqueId && isKnownKind;
                if (!_isValidNode[index])
                {
                    _errors.Add(new SpellProgramIssue("G5", nodeId));
                    kind = SpellNodeKind.Add;
                }
                _kinds[index] = kind;
                _dwells[index] = SpellNodes.GetDwell(kind, settings);
                if (_isValidNode[index]) ReadParameters(index, node, settings);
            }
        }

        /// <summary>유효한 노드의 파라미터를 검사해 키·범위·옵션이 어긋나면 G6을 노드당 한 번 기록하고, 값을 풀어 배열에 채운다. 목록에 null 항목이 있으면 값을 읽지 않고 기본값을 쓴다.</summary>
        private void ReadParameters(int index, GraphNode node, SpellSettings settings)
        {
            SpellNodeKind kind = _kinds[index];
            IReadOnlyList<NodeParameter> parameters = node.Params;
            bool isReadable = parameters != null && !ContainsNullParameter(parameters);
            if (!isReadable || !AreParametersValid(kind, parameters, settings))
                _errors.Add(new SpellProgramIssue("G6", node.Id));
            SetDefaults(index, kind);
            if (isReadable) ReadValues(index, node, kind, settings);
        }

        /// <summary>노드 종류에 맞는 숫자 기본값과 열거 기본값을 배열에 채운다.</summary>
        private void SetDefaults(int index, SpellNodeKind kind)
        {
            switch (kind)
            {
                case SpellNodeKind.Cast:
                    _numbers[index] = _defaultLoad;
                    break;
                case SpellNodeKind.Amplify:
                    _numbers[index] = _defaultRatio;
                    break;
                case SpellNodeKind.Fork:
                    _numbers[index] = _defaultShare;
                    break;
                case SpellNodeKind.Branch:
                    _numbers[index] = _defaultThreshold;
                    break;
                default:
                    _numbers[index] = 0.0;
                    break;
            }
            _aims[index] = AimMode.Cursor;
            _conditions[index] = BranchCondition.Power;
            _isGreaterOrEqual[index] = true;
        }

        /// <summary>노드 종류가 쓰는 파라미터를 설정 기본값과 함께 읽어 숫자와 열거 배열에 채운다. 분배 비율은 퍼센트를 0에서 1 사이 값으로 바꾼다.</summary>
        private void ReadValues(int index, GraphNode node, SpellNodeKind kind, SpellSettings settings)
        {
            switch (kind)
            {
                case SpellNodeKind.Cast:
                    _numbers[index] = node.GetNumber(SpellNodes.PARAM_LOAD, settings.LoadDefault);
                    SpellNodes.TryParseAim(node.GetText(SpellNodes.PARAM_AIM, SpellNodes.GetDefaultText(SpellNodes.PARAM_AIM)), out AimMode aim);
                    _aims[index] = aim;
                    break;
                case SpellNodeKind.Amplify:
                    _numbers[index] = node.GetNumber(SpellNodes.PARAM_RATIO, settings.AmplifyDefault);
                    break;
                case SpellNodeKind.Fork:
                    _numbers[index] = node.GetNumber(SpellNodes.PARAM_SHARE, settings.ForkShareDefault) / PERCENT_SCALE;
                    break;
                case SpellNodeKind.Branch:
                    _numbers[index] = node.GetNumber(SpellNodes.PARAM_THRESHOLD, settings.BranchThresholdDefault);
                    SpellNodes.TryParseCondition(node.GetText(SpellNodes.PARAM_CONDITION, SpellNodes.GetDefaultText(SpellNodes.PARAM_CONDITION)), out BranchCondition condition);
                    _conditions[index] = condition;
                    _isGreaterOrEqual[index] = node.GetText(SpellNodes.PARAM_COMPARE, SpellNodes.GetDefaultText(SpellNodes.PARAM_COMPARE)) != SpellNodes.COMPARE_LE;
                    break;
            }
        }

        /// <summary>파라미터 목록의 키가 종류에 허용되고 숫자는 범위 안, 텍스트는 옵션 안인지 검사하여 모두 맞으면 true를 반환한다.</summary>
        private static bool AreParametersValid(SpellNodeKind kind, IReadOnlyList<NodeParameter> parameters, SpellSettings settings)
        {
            for (int index = 0; index < parameters.Count; index++)
            {
                NodeParameter param = parameters[index];
                if (!IsKeyAllowed(kind, param.Key)) return false;
                if (SpellNodes.IsNumberParameter(param.Key))
                {
                    if (!IsNumberInRange(param.Key, param.Number, settings)) return false;
                }
                else if (!IsTextOption(param.Key, param.Text))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>파라미터 목록에 null 항목이 있는지 반환한다.</summary>
        private static bool ContainsNullParameter(IReadOnlyList<NodeParameter> parameters)
        {
            for (int index = 0; index < parameters.Count; index++)
                if (parameters[index] == null) return true;
            return false;
        }

        /// <summary>파라미터 키가 해당 노드 종류의 파라미터 키 목록에 있는지 반환한다.</summary>
        private static bool IsKeyAllowed(SpellNodeKind kind, string key)
        {
            IReadOnlyList<string> keys = SpellNodes.GetParameterKeys(kind);
            for (int index = 0; index < keys.Count; index++)
                if (keys[index] == key) return true;
            return false;
        }

        /// <summary>숫자 값이 유한하고 키의 최소·최대 범위 안이면 true를 반환한다.</summary>
        private static bool IsNumberInRange(string key, float value, SpellSettings settings)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            SpellNodes.TryGetNumberRange(key, settings, out float min, out float max, out _, out _);
            return value >= min && value <= max;
        }

        /// <summary>텍스트 값이 키의 선택 옵션 목록에 있으면 true를 반환한다.</summary>
        private static bool IsTextOption(string key, string text)
        {
            IReadOnlyList<string> options = SpellNodes.GetTextOptions(key);
            for (int index = 0; index < options.Count; index++)
                if (options[index] == text) return true;
            return false;
        }

        /// <summary>시전 노드와 적중 노드의 개수를 세어 시전이 정확히 1개가 아니면 G1을, 두 번째 이후 적중 노드에는 G2를 기록하고 첫 번째 색인을 저장한다.</summary>
        private void CheckAnchors()
        {
            int castCount = 0;
            for (int index = 0; index < _kinds.Length; index++)
            {
                if (!_isValidNode[index]) continue;
                if (_kinds[index] == SpellNodeKind.Cast)
                {
                    if (castCount == 0) _castNode = index;
                    castCount++;
                }
                else if (_kinds[index] == SpellNodeKind.OnHit)
                {
                    if (_onHitNode == NO_INDEX) _onHitNode = index;
                    else _errors.Add(new SpellProgramIssue("G2", _nodeIds[index]));
                }
            }
            if (castCount != 1) _errors.Add(new SpellProgramIssue("G1"));
        }

        /// <summary>엣지를 그래프 순서대로 출력 포트 배열에 연결한다. 끊긴 엣지는 G5, 시전·적중으로 들어오는 엣지는 G3, 같은 출력 포트의 두 번째 엣지는 G4로 기록하고 건너뛴다.</summary>
        private void LinkEdges(IReadOnlyList<GraphEdge> edges)
        {
            for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                GraphEdge edge = edges[edgeIndex];
                int fromIndex = edge == null ? NO_INDEX : IndexOf(edge.FromNode);
                int toIndex = edge == null ? NO_INDEX : IndexOf(edge.ToNode);
                int outPort = NO_INDEX;
                int inPort = NO_INDEX;
                bool isPortValid = edge != null && TryParsePort(edge.FromPort, out outPort) && TryParsePort(edge.ToPort, out inPort);
                if (fromIndex == NO_INDEX || toIndex == NO_INDEX || !isPortValid)
                {
                    _errors.Add(new SpellProgramIssue("G5", edge?.ToNode));
                    continue;
                }
                SpellNodeKind toKind = _kinds[toIndex];
                if (toKind == SpellNodeKind.Cast || toKind == SpellNodeKind.OnHit)
                {
                    _errors.Add(new SpellProgramIssue("G3", edge.ToNode));
                    continue;
                }
                if (outPort >= GetOutputCount(fromIndex) || inPort >= GetInputCount(toIndex))
                {
                    _errors.Add(new SpellProgramIssue("G5", edge.ToNode));
                    continue;
                }
                int slot = fromIndex * MAX_PORTS + outPort;
                if (_nextTargets[slot] != NO_INDEX)
                {
                    _errors.Add(new SpellProgramIssue("G4", edge.FromNode));
                    continue;
                }
                _nextTargets[slot] = toIndex;
                _nextInPorts[slot] = inPort;
            }
        }

        /// <summary>십진 정수 문자열만 포트 번호로 받아들이며 성공하면 true를 반환한다.</summary>
        private static bool TryParsePort(string text, out int port)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port);
        }

        /// <summary>시전 노드와 적중 노드에서 출력 엣지를 따라 도달 가능한 노드를 표시한다.</summary>
        private void MarkReachability()
        {
            MarkFrom(_castNode, _isReachable);
            MarkFrom(_onHitNode, _isReachable);
        }

        /// <summary>시작 노드에서 출력 엣지를 따라 너비 우선으로 방문 표시를 채운다. 시작 노드가 없거나 이미 표시되었으면 아무것도 하지 않는다.</summary>
        private void MarkFrom(int start, bool[] visited)
        {
            if (start == NO_INDEX || visited[start]) return;
            Queue<int> queue = new Queue<int>();
            visited[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                for (int port = 0; port < MAX_PORTS; port++)
                {
                    int target = _nextTargets[node * MAX_PORTS + port];
                    if (target == NO_INDEX || visited[target]) continue;
                    visited[target] = true;
                    queue.Enqueue(target);
                }
            }
        }

        /// <summary>시전 노드에서만 시작한 탐색에서 투사체 노드를 만나지 못하면 W1 경고를 기록한다. 시전 노드가 없으면 경고를 추가하지 않는다.</summary>
        private void CheckProjectileReach()
        {
            if (_castNode == NO_INDEX) return;
            bool[] fromCast = new bool[_kinds.Length];
            MarkFrom(_castNode, fromCast);
            for (int index = 0; index < _kinds.Length; index++)
                if (fromCast[index] && _kinds[index] == SpellNodeKind.Projectile) return;
            _warnings.Add(new SpellProgramIssue("W1", _nodeIds[_castNode]));
        }

        /// <summary>노드 ID, 종류, 파라미터, 엣지를 ID 순서의 정규 문자열로 만든 뒤 FNV-1a 64비트 해시를 16진수 문자열로 반환한다. 좌표와 배열 순서는 제외한다.</summary>
        private static string ComputeSignature(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges)
        {
            StringBuilder canonical = new StringBuilder();
            List<int> nodeOrder = new List<int>(nodes.Count);
            for (int index = 0; index < nodes.Count; index++)
                if (nodes[index] != null) nodeOrder.Add(index);
            nodeOrder.Sort((left, right) =>
            {
                int result = string.CompareOrdinal(nodes[left].Id, nodes[right].Id);
                return result != 0 ? result : left.CompareTo(right);
            });
            foreach (int nodeIndex in nodeOrder)
            {
                GraphNode node = nodes[nodeIndex];
                canonical.Append(node.Id).Append(':').Append(node.RuneId).Append(';');
                IReadOnlyList<NodeParameter> parameters = node.Params;
                if (parameters == null) continue;
                List<int> paramOrder = new List<int>(parameters.Count);
                for (int index = 0; index < parameters.Count; index++)
                    if (parameters[index] != null) paramOrder.Add(index);
                paramOrder.Sort((left, right) =>
                {
                    int result = string.CompareOrdinal(parameters[left].Key, parameters[right].Key);
                    return result != 0 ? result : left.CompareTo(right);
                });
                foreach (int paramIndex in paramOrder)
                {
                    NodeParameter param = parameters[paramIndex];
                    canonical.Append(param.Key).Append('=').Append(param.Number.ToString(NUMBER_FORMAT, CultureInfo.InvariantCulture))
                        .Append('/').Append(param.Text).Append(';');
                }
            }
            string[] edgeKeys = new string[edges.Count];
            List<int> edgeOrder = new List<int>(edges.Count);
            for (int index = 0; index < edges.Count; index++)
            {
                GraphEdge edge = edges[index];
                if (edge == null) continue;
                edgeKeys[index] = edge.FromNode + ":" + edge.FromPort + ":" + edge.ToNode + ":" + edge.ToPort;
                edgeOrder.Add(index);
            }
            edgeOrder.Sort((left, right) =>
            {
                int result = string.CompareOrdinal(edgeKeys[left], edgeKeys[right]);
                return result != 0 ? result : left.CompareTo(right);
            });
            foreach (int edgeIndex in edgeOrder)
            {
                GraphEdge edge = edges[edgeIndex];
                canonical.Append(edge.FromNode).Append('.').Append(edge.FromPort).Append('>')
                    .Append(edge.ToNode).Append('.').Append(edge.ToPort).Append(';');
            }
            ulong hash = FNV_OFFSET_BASIS;
            foreach (byte value in Encoding.UTF8.GetBytes(canonical.ToString()))
            {
                hash ^= value;
                hash = unchecked(hash * FNV_PRIME);
            }
            return hash.ToString(HASH_FORMAT, CultureInfo.InvariantCulture);
        }
    }
}
