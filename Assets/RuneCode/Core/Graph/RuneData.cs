using System;
using System.Collections.Generic;

using RuneCode.Tables;

namespace RuneCode
{
    public sealed class PortDefinition
    {
        private readonly string _id;
        private readonly string _kind;
        private readonly string _direction;
        private readonly int _max;
        private readonly string _status;

        public string Id => _id;
        public string Kind => _kind;
        public string Direction => _direction;
        public int Max => _max;
        public string Status => _status;

        /// <summary>지원 중단 포트인지 반환한다. 새로 연결할 수 없고 기존 연결은 보존하되 실행하지 않는다.</summary>
        public bool IsDeprecated => _status == SpellGrammar.PORT_DEPRECATED;

        /// <summary>rune_ports 테이블 행의 포트 ID·종류·방향·최대 연결 수·상태(빈 칸이면 active)로 포트를 만든다.</summary>
        internal PortDefinition(TableRow row)
        {
            _id = row.GetString("portId");
            _kind = row.GetString("kind");
            _direction = row.GetString("direction");
            _max = row.GetInt("max");
            _status = row.GetOptionalString("status", SpellGrammar.PORT_ACTIVE);
        }
    }

    public sealed class ParameterDefinition
    {
        private readonly string _id;
        private readonly string _kind;
        private readonly float _min;
        private readonly float _max;
        private readonly float _step;
        private readonly float _defaultNumber;
        private readonly string _defaultText;
        private readonly IReadOnlyList<string> _options;

        public string Id => _id;
        public string Kind => _kind;
        public float Min => _min;
        public float Max => _max;
        public float Step => _step;
        public float DefaultNumber => _defaultNumber;
        public string DefaultText => _defaultText;
        public IReadOnlyList<string> Options => _options;

        /// <summary>rune_params 테이블 행과 rune_param_options의 선택지 목록으로 파라미터를 만든다. 빈 숫자 칸은 0, 빈 문자 칸은 빈 문자열이다.</summary>
        internal ParameterDefinition(TableRow row, IReadOnlyList<string> options)
        {
            _id = row.GetString("paramId");
            _kind = row.GetString("kind");
            _min = row.GetOptionalFloat("min", 0f);
            _max = row.GetOptionalFloat("max", 0f);
            _step = row.GetOptionalFloat("step", 0f);
            _defaultNumber = row.GetOptionalFloat("defaultNumber", 0f);
            _defaultText = row.GetOptionalString("defaultText", "");
            _options = options;
        }
    }

    public sealed class RuneStats
    {
        private readonly float _damage;
        private readonly float _speed;
        private readonly float _radius;
        private readonly float _lifetime;
        private readonly float _offset;
        private readonly int _count;
        private readonly int _orbitCount;
        private readonly float _orbitRadius;
        private readonly float _angularSpeed;
        private readonly float _hitInterval;
        private readonly float _tickInterval;
        private readonly float _damageMultiplier;
        private readonly float _radiusMultiplier;
        private readonly int _pierce;
        private readonly float _pierceLoss;
        private readonly float _homingTurn;
        private readonly float _homingRange;
        private readonly float _arcRange;
        private readonly int _arcTargets;
        private readonly float _arcMultiplier;
        private readonly float _learningMultiplier;
        private readonly float _spreadAngle;
        private readonly float _shieldAmount;
        private readonly float _shieldSeconds;

        public float Damage => _damage;
        public float Speed => _speed;
        public float Radius => _radius;
        public float Lifetime => _lifetime;
        public float Offset => _offset;
        public int Count => _count;
        public int OrbitCount => _orbitCount;
        public float OrbitRadius => _orbitRadius;
        public float AngularSpeed => _angularSpeed;
        public float HitInterval => _hitInterval;
        public float TickInterval => _tickInterval;
        public float DamageMultiplier => _damageMultiplier;
        public float RadiusMultiplier => _radiusMultiplier;
        public int Pierce => _pierce;
        public float PierceLoss => _pierceLoss;
        public float HomingTurn => _homingTurn;
        public float HomingRange => _homingRange;
        public float ArcRange => _arcRange;
        public int ArcTargets => _arcTargets;
        public float ArcMultiplier => _arcMultiplier;
        public float LearningMultiplier => _learningMultiplier;
        public float SpreadAngle => _spreadAngle;
        public float ShieldAmount => _shieldAmount;
        public float ShieldSeconds => _shieldSeconds;

        /// <summary>rune_stats 테이블 행으로 효과 수치를 만든다. 빈 칸은 배율 열이면 1, 그 외는 0이다.</summary>
        internal RuneStats(TableRow row)
        {
            _damage = row.GetOptionalFloat("damage", 0f);
            _speed = row.GetOptionalFloat("speed", 0f);
            _radius = row.GetOptionalFloat("radius", 0f);
            _lifetime = row.GetOptionalFloat("lifetime", 0f);
            _offset = row.GetOptionalFloat("offset", 0f);
            _count = row.GetOptionalInt("count", 0);
            _orbitCount = row.GetOptionalInt("orbitCount", 0);
            _orbitRadius = row.GetOptionalFloat("orbitRadius", 0f);
            _angularSpeed = row.GetOptionalFloat("angularSpeed", 0f);
            _hitInterval = row.GetOptionalFloat("hitInterval", 0f);
            _tickInterval = row.GetOptionalFloat("tickInterval", 0f);
            _damageMultiplier = row.GetOptionalFloat("damageMultiplier", 1f);
            _radiusMultiplier = row.GetOptionalFloat("radiusMultiplier", 1f);
            _pierce = row.GetOptionalInt("pierce", 0);
            _pierceLoss = row.GetOptionalFloat("pierceLoss", 0f);
            _homingTurn = row.GetOptionalFloat("homingTurn", 0f);
            _homingRange = row.GetOptionalFloat("homingRange", 0f);
            _arcRange = row.GetOptionalFloat("arcRange", 0f);
            _arcTargets = row.GetOptionalInt("arcTargets", 0);
            _arcMultiplier = row.GetOptionalFloat("arcMultiplier", 0f);
            _learningMultiplier = row.GetOptionalFloat("learningMultiplier", 1f);
            _spreadAngle = row.GetOptionalFloat("spreadAngle", 0f);
            _shieldAmount = row.GetOptionalFloat("shieldAmount", 0f);
            _shieldSeconds = row.GetOptionalFloat("shieldSeconds", 0f);
        }
    }

    public sealed class RuneDefinition
    {
        private readonly string _id;
        private readonly string _name;
        private readonly string _category;
        private readonly int _ram;
        private readonly float _energy;
        private readonly float _energyMult;
        private readonly string _unlockType;
        private readonly int _unlockCost;
        private readonly IReadOnlyList<string> _tags;
        private readonly IReadOnlyList<PortDefinition> _ports;
        private readonly IReadOnlyList<ParameterDefinition> _params;
        private readonly RuneStats _stats;

        public string Id => _id;
        public string Name => _name;
        public string Category => _category;
        public int Ram => _ram;
        public float Energy => _energy;
        public float EnergyMult => _energyMult;
        public string UnlockType => _unlockType;
        public int UnlockCost => _unlockCost;
        public IReadOnlyList<string> Tags => _tags;
        public IReadOnlyList<PortDefinition> Ports => _ports;
        public IReadOnlyList<ParameterDefinition> Params => _params;
        public RuneStats Stats => _stats;

        /// <summary>runes 테이블 행과 자식 테이블에서 모은 태그·포트·파라미터·효과 수치로 룬 정의를 만든다.</summary>
        internal RuneDefinition(TableRow row, IReadOnlyList<string> tags, IReadOnlyList<PortDefinition> ports,
            IReadOnlyList<ParameterDefinition> parameters, RuneStats stats)
        {
            _id = row.GetString("id");
            _name = row.GetString("name");
            _category = row.GetString("category");
            _ram = row.GetInt("ram");
            _energy = row.GetFloat("energy");
            _energyMult = row.GetFloat("energyMult");
            _unlockType = row.GetString("unlockType");
            _unlockCost = row.GetInt("unlockCost");
            _tags = tags;
            _ports = ports;
            _params = parameters;
            _stats = stats;
        }

        /// <summary>포트 ID와 방향으로 해당 룬의 포트 정의를 찾거나 null을 반환한다.</summary>
        public PortDefinition FindPort(string id, string direction)
        {
            foreach (PortDefinition port in Ports)
            {
                if (port.Id == id && port.Direction == direction) return port;
            }
            return null;
        }
    }

    /// <summary>
    /// 속성(Element) 정의다. 속성은 그래프 노드가 아니라 Shape·Apply 노드의 드롭다운 값이며,
    /// RAM·해금은 같은 ID의 속성 룬(runes 테이블의 element 카테고리 행)이 가진다.
    /// </summary>
    public sealed class ElementDefinition
    {
        private readonly string _id;
        private readonly string _runeId;
        private readonly string _category;
        private readonly string _internalValue;
        private readonly string _runtimeTag;

        public string Id => _id;
        public string RuneId => _runeId;
        public string Category => _category;
        public bool IsFunctional => _category == SpellGrammar.ELEMENT_CATEGORY_FUNCTIONAL;
        public string InternalValue => _internalValue;
        public string RuntimeTag => _runtimeTag;

        /// <summary>elements 테이블 행의 식별자·속성 룬 ID·Elemental/Functional 분류·컴파일 내부 값·런타임 태그로 속성을 만든다.</summary>
        internal ElementDefinition(TableRow row)
        {
            _id = row.GetString("id");
            _runeId = row.GetString("runeId");
            _category = row.GetString("category");
            _internalValue = row.GetString("internalValue");
            _runtimeTag = row.GetOptionalString("runtimeTag", "");
        }
    }

    /// <summary>
    /// 룬 정의 레지스트리다. 정규화된 룬 테이블 6종(runes, rune_stats, rune_ports, rune_params, rune_param_options, rune_tags)과
    /// 속성 정의 테이블(elements)을 룬 ID로 합쳐 만들며, 테이블 기반 데이터 로딩의 참조 구현이다. 규칙은 docs/DATA_TABLES.md를 따른다.
    /// </summary>
    public sealed class RuneCatalog
    {
        public const string RUNES_TABLE = "runes";
        public const string STATS_TABLE = "rune_stats";
        public const string PORTS_TABLE = "rune_ports";
        public const string PARAMS_TABLE = "rune_params";
        public const string OPTIONS_TABLE = "rune_param_options";
        public const string TAGS_TABLE = "rune_tags";
        public const string ELEMENTS_TABLE = "elements";

        private static readonly HashSet<string> CATEGORIES = new HashSet<string>(StringComparer.Ordinal)
        {
            "core", "form", "element", "modifier", "flow", "action", "magic", "shape", "behavior", "method", "internal"
        };
        private static readonly HashSet<string> ELEMENT_CATEGORIES = new HashSet<string>(StringComparer.Ordinal)
        {
            SpellGrammar.ELEMENT_CATEGORY_ELEMENTAL, SpellGrammar.ELEMENT_CATEGORY_FUNCTIONAL
        };
        private static readonly HashSet<string> UNLOCK_TYPES = new HashSet<string>(StringComparer.Ordinal) { "start", "bench", "reward" };

        private readonly IReadOnlyList<RuneDefinition> _runes;
        private readonly Dictionary<string, RuneDefinition> _byId = new Dictionary<string, RuneDefinition>(StringComparer.Ordinal);
        private readonly List<string> _startRunes = new List<string>();
        private readonly IReadOnlyList<ElementDefinition> _elements;
        private readonly Dictionary<string, ElementDefinition> _elementsById = new Dictionary<string, ElementDefinition>(StringComparer.Ordinal);

        public IReadOnlyList<RuneDefinition> All => _runes;
        public IReadOnlyList<string> StartRunes => _startRunes;
        public IReadOnlyList<ElementDefinition> Elements => _elements;

        /// <summary>검증된 룬·속성 목록으로 ID 조회표와 시작 해금 목록을 만든다.</summary>
        private RuneCatalog(IReadOnlyList<RuneDefinition> runes, IReadOnlyList<ElementDefinition> elements)
        {
            _runes = runes;
            _elements = elements;
            foreach (ElementDefinition element in elements)
            {
                _elementsById.Add(element.Id, element);
            }
            foreach (RuneDefinition rune in runes)
            {
                _byId.Add(rune.Id, rune);
                if (rune.UnlockType == "start") _startRunes.Add(rune.Id);
            }
        }

        /// <summary>
        /// 테이블 소스에서 룬 테이블 6종을 읽어 룬 ID로 합치고, 형식·키·참조·문법 규칙을 검증한 레지스트리를 반환한다.
        /// 오류는 모두 모은 뒤 TableLoadException(FormatException)으로 한 번에 보고한다.
        /// </summary>
        public static RuneCatalog FromTables(ITableSource source)
        {
            var log = new TableErrorLog();
            DataTable runes = DataTable.Load(source, RUNES_TABLE, log);
            DataTable stats = DataTable.Load(source, STATS_TABLE, log);
            DataTable ports = DataTable.Load(source, PORTS_TABLE, log);
            DataTable parameters = DataTable.Load(source, PARAMS_TABLE, log);
            DataTable options = DataTable.Load(source, OPTIONS_TABLE, log);
            DataTable tags = DataTable.Load(source, TAGS_TABLE, log);
            DataTable elements = DataTable.Load(source, ELEMENTS_TABLE, log);
            bool hasColumns = runes.RequireColumns("id", "name", "category", "ram", "energy", "energyMult", "unlockType", "unlockCost")
                & stats.RequireColumns("runeId")
                & ports.RequireColumns("runeId", "order", "portId", "kind", "direction", "max", "status")
                & parameters.RequireColumns("runeId", "order", "paramId", "kind", "min", "max", "step", "defaultNumber", "defaultText")
                & options.RequireColumns("runeId", "paramId", "order", "option")
                & tags.RequireColumns("runeId", "order", "tag")
                & elements.RequireColumns("id", "runeId", "category", "internalValue", "runtimeTag");
            if (!hasColumns)
            {
                log.ThrowIfAny();
            }

            // 키 유일성과 부모 참조를 먼저 검증한다. 자식 테이블의 runeId는 반드시 runes에 있어야 한다.
            HashSet<string> runeIds = runes.RequireUniqueKeys("id");
            HashSet<string> statRuneIds = stats.RequireUniqueKeys("runeId");
            ports.RequireUniqueKeys("runeId", "direction", "portId");
            ports.RequireUniqueKeys("runeId", "order");
            HashSet<string> paramKeys = parameters.RequireUniqueKeys("runeId", "paramId");
            parameters.RequireUniqueKeys("runeId", "order");
            options.RequireUniqueKeys("runeId", "paramId", "order");
            tags.RequireUniqueKeys("runeId", "order");
            stats.RequireReferences(runeIds, RUNES_TABLE, "runeId");
            ports.RequireReferences(runeIds, RUNES_TABLE, "runeId");
            parameters.RequireReferences(runeIds, RUNES_TABLE, "runeId");
            options.RequireReferences(paramKeys, PARAMS_TABLE, "runeId", "paramId");
            tags.RequireReferences(runeIds, RUNES_TABLE, "runeId");
            elements.RequireUniqueKeys("id");
            elements.RequireUniqueKeys("runeId");
            elements.RequireReferences(runeIds, RUNES_TABLE, "runeId");

            Dictionary<string, List<TableRow>> statRows = stats.GroupBy(null, "runeId");
            Dictionary<string, List<TableRow>> portRows = ports.GroupBy("order", "runeId");
            Dictionary<string, List<TableRow>> paramRows = parameters.GroupBy("order", "runeId");
            Dictionary<string, List<TableRow>> optionRows = options.GroupBy("order", "runeId", "paramId");
            Dictionary<string, List<TableRow>> tagRows = tags.GroupBy("order", "runeId");

            var definitions = new List<RuneDefinition>();
            foreach (TableRow row in runes.Rows)
            {
                string id = row.GetString("id");
                if (!statRuneIds.Contains(id))
                {
                    row.ReportError("id", STATS_TABLE + "에 이 룬의 행이 없습니다.");
                    continue;
                }

                RuneStats runeStats = new RuneStats(statRows[id][0]);
                List<PortDefinition> runePorts = BuildPorts(portRows, id);
                List<ParameterDefinition> runeParams = BuildParameters(paramRows, optionRows, id);
                var runeTags = new List<string>();
                if (tagRows.TryGetValue(id, out List<TableRow> tagGroup))
                {
                    foreach (TableRow tagRow in tagGroup) runeTags.Add(tagRow.GetString("tag"));
                }

                var rune = new RuneDefinition(row, runeTags, runePorts, runeParams, runeStats);
                ValidateRune(rune, row);
                ValidateStats(rune, statRows[id][0]);
                definitions.Add(rune);
            }

            if (runes.Rows.Count == 0)
            {
                log.Add(RUNES_TABLE, 0, null, "룬 정의가 비어 있습니다.");
            }
            List<ElementDefinition> elementDefinitions = BuildElements(elements, definitions);
            log.ThrowIfAny();
            return new RuneCatalog(definitions, elementDefinitions);
        }

        /// <summary>룬 ID로 정의를 반환하며 알 수 없는 ID이면 예외를 발생시킨다.</summary>
        public RuneDefinition Get(string id) => _byId[id];

        /// <summary>룬 ID의 존재 여부와 정의를 반환한다.</summary>
        public bool TryGet(string id, out RuneDefinition rune)
        {
            rune = null;
            return !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out rune);
        }

        /// <summary>
        /// 그래프 노드 하나가 차지하는 RAM을 반환한다. 룬 자체 RAM에, 속성 드롭다운을 가진 노드(Shape·Apply)는 선택한 속성 룬의 RAM을 더한다.
        /// 알 수 없는 룬이면 0을 반환한다.
        /// </summary>
        public int NodeRam(GraphNode node)
        {
            if (!TryGet(node.RuneId, out RuneDefinition rune)) return 0;
            int ram = rune.Ram;
            if (TryGetNodeElement(node, out ElementDefinition element) && TryGet(element.RuneId, out RuneDefinition elementRune)) ram += elementRune.Ram;
            return ram;
        }

        /// <summary>노드의 element 파라미터(없으면 룬 기본값)에 해당하는 속성을 반환한다. 속성 드롭다운이 없는 노드이거나 값이 없으면 false를 반환한다.</summary>
        public bool TryGetNodeElement(GraphNode node, out ElementDefinition element)
        {
            element = null;
            if (!TryGet(node.RuneId, out RuneDefinition rune)) return false;
            foreach (ParameterDefinition param in rune.Params)
            {
                if (param.Id == SpellGrammar.ELEMENT_PARAM) return TryGetElement(node.GetText(SpellGrammar.ELEMENT_PARAM, param.DefaultText), out element);
            }
            return false;
        }

        /// <summary>속성 식별자(neutral, fire 등)의 존재 여부와 정의를 반환한다.</summary>
        public bool TryGetElement(string id, out ElementDefinition element)
        {
            element = null;
            return !string.IsNullOrEmpty(id) && _elementsById.TryGetValue(id, out element);
        }

        /// <summary>
        /// elements 테이블 행으로 속성 정의를 만들고 분류값과, 참조한 룬이 element 카테고리인지 검증한다.
        /// 위반은 해당 행 위치로 오류 로그에 기록한다.
        /// </summary>
        private static List<ElementDefinition> BuildElements(DataTable elements, List<RuneDefinition> runes)
        {
            var result = new List<ElementDefinition>();
            foreach (TableRow row in elements.Rows)
            {
                var element = new ElementDefinition(row);
                if (!ELEMENT_CATEGORIES.Contains(element.Category))
                {
                    row.ReportError("category", "elemental 또는 functional이어야 합니다: " + element.Category);
                }
                RuneDefinition rune = runes.Find(candidate => candidate.Id == element.RuneId);
                if (rune != null && rune.Category != SpellGrammar.CATEGORY_ELEMENT)
                {
                    row.ReportError("runeId", "element 카테고리 룬이어야 합니다: " + element.RuneId);
                }
                result.Add(element);
            }
            return result;
        }

        /// <summary>룬의 포트 행을 순서대로 포트 정의로 만들고 종류·방향·최대 연결 수·상태 규칙을 검증한다. 지원 중단은 실행 출력에만 허용한다.</summary>
        private static List<PortDefinition> BuildPorts(Dictionary<string, List<TableRow>> portRows, string runeId)
        {
            var result = new List<PortDefinition>();
            if (!portRows.TryGetValue(runeId, out List<TableRow> rows))
            {
                return result;
            }
            foreach (TableRow row in rows)
            {
                var port = new PortDefinition(row);
                if ((port.Kind != "exec" && port.Kind != "mod" && port.Kind != "chain")
                    || (port.Direction != "in" && port.Direction != "out") || port.Max < 0
                    || (port.Kind == "chain" && port.Max != 1)
                    || (port.Direction == "in" && port.Kind == "exec" && port.Max != 1))
                {
                    row.ReportError(null, "유효하지 않은 룬 포트입니다 (종류 exec/mod/chain, 방향 in/out, chain과 exec 입력은 최대 1).");
                }
                if ((port.Status != SpellGrammar.PORT_ACTIVE && !port.IsDeprecated)
                    || (port.IsDeprecated && (port.Kind != "exec" || port.Direction != "out")))
                {
                    row.ReportError("status", "active(빈 칸) 또는 deprecated여야 하며, deprecated는 exec 출력 포트에만 쓸 수 있습니다.");
                }
                result.Add(port);
            }
            return result;
        }

        /// <summary>룬의 파라미터 행과 선택지 행을 순서대로 파라미터 정의로 만들고 종류별 값 규칙을 검증한다.</summary>
        private static List<ParameterDefinition> BuildParameters(Dictionary<string, List<TableRow>> paramRows,
            Dictionary<string, List<TableRow>> optionRows, string runeId)
        {
            var result = new List<ParameterDefinition>();
            if (!paramRows.TryGetValue(runeId, out List<TableRow> rows))
            {
                return result;
            }
            foreach (TableRow row in rows)
            {
                var optionValues = new List<string>();
                if (optionRows.TryGetValue(DataTable.Key(runeId, row.GetString("paramId")), out List<TableRow> optionGroup))
                {
                    foreach (TableRow optionRow in optionGroup) optionValues.Add(optionRow.GetString("option"));
                }

                var param = new ParameterDefinition(row, optionValues);
                bool isInvalid = (param.Kind != "number" && param.Kind != "enum" && param.Kind != "text")
                    || (param.Kind == "number" && (param.Min > param.Max || param.DefaultNumber < param.Min
                        || param.DefaultNumber > param.Max || param.Step <= 0f))
                    || (param.Kind == "enum" && !optionValues.Contains(param.DefaultText))
                    || (param.Kind == "text" && param.DefaultText.Length > 80);
                if (isInvalid)
                {
                    row.ReportError(null, "유효하지 않은 룬 파라미터입니다 (number는 min≤기본값≤max·step>0, enum은 기본값이 선택지에 있어야 함).");
                }
                result.Add(param);
            }
            return result;
        }

        /// <summary>룬의 카테고리·해금 방식·RAM·에너지 값 규칙을 검증하고 위반을 runes 테이블 행에 기록한다.</summary>
        private static void ValidateRune(RuneDefinition rune, TableRow row)
        {
            if (!CATEGORIES.Contains(rune.Category)) row.ReportError("category", "알 수 없는 카테고리입니다: " + rune.Category);
            if (!UNLOCK_TYPES.Contains(rune.UnlockType)) row.ReportError("unlockType", "알 수 없는 해금 방식입니다: " + rune.UnlockType);
            if (rune.Ram < 0) row.ReportError("ram", "음수일 수 없습니다.");
            if (rune.Energy < 0f) row.ReportError("energy", "음수일 수 없습니다.");
            if (rune.EnergyMult <= 0f) row.ReportError("energyMult", "0보다 커야 합니다.");
            if (rune.UnlockCost < 0 || (rune.UnlockType == "bench" && rune.UnlockCost == 0))
                row.ReportError("unlockCost", "음수일 수 없으며 bench 해금은 비용이 있어야 합니다.");
        }

        /// <summary>룬 효과 수치가 음수가 아니고 이전 형태 룬에 필수 수치가 있는지 검증하고 위반을 rune_stats 행에 기록한다.</summary>
        private static void ValidateStats(RuneDefinition rune, TableRow row)
        {
            RuneStats stats = rune.Stats;
            float[] numbers = { stats.Damage, stats.Speed, stats.Radius, stats.Lifetime, stats.Offset, stats.OrbitRadius,
                stats.AngularSpeed, stats.HitInterval, stats.TickInterval, stats.DamageMultiplier, stats.RadiusMultiplier,
                stats.PierceLoss, stats.HomingTurn, stats.HomingRange, stats.ArcRange, stats.ArcMultiplier,
                stats.LearningMultiplier, stats.SpreadAngle, stats.ShieldAmount, stats.ShieldSeconds };
            foreach (float value in numbers)
            {
                if (value < 0f)
                {
                    row.ReportError(null, "효과 수치는 음수일 수 없습니다.");
                    break;
                }
            }
            if (stats.Count < 0 || stats.Pierce < 0 || stats.ArcTargets < 0 || stats.OrbitCount < 0
                || (rune.Category == "form" && (stats.Count <= 0 || stats.Damage <= 0f || stats.Radius <= 0f)))
                row.ReportError(null, "유효하지 않은 형태 룬 수치입니다 (form은 count·damage·radius가 0보다 커야 함).");
        }
    }
}
