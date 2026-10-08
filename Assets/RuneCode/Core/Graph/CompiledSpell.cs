using System.Collections.Generic;

namespace RuneCode
{
    public sealed class CompileIssue
    {
        private readonly string _code;
        private readonly string _nodeId;
        private readonly string _message;
        public string Code => _code;
        public string NodeId => _nodeId;
        public string Message => _message;

        /// <summary>검증 코드와 관련 노드 및 사용자 메시지를 저장한다.</summary>
        public CompileIssue(string code, string nodeId = null, string detail = null)
        {
            _code = code;
            _nodeId = nodeId;
            _message = detail == null ? GameData.L("issue." + code) : GameData.L("issue." + code) + " " + detail;
        }
    }

    public sealed class SpellStats
    {
        private readonly float _damage;
        private readonly float _speed;
        private readonly float _radius;
        private readonly float _lifetime;
        private readonly float _offset;
        private readonly float _orbitRadius;
        private readonly float _angularSpeed;
        private readonly float _hitInterval;
        private readonly float _tickInterval;
        private readonly int _pierce;
        private readonly float _pierceLoss;
        private readonly float _homingTurn;
        private readonly float _homingRange;
        private readonly float _arcRange;
        private readonly int _arcTargets;
        private readonly float _arcMultiplier;
        private readonly float _learningMultiplier;
        private readonly float _spreadAngle;
        public float Damage => _damage;
        public float Speed => _speed;
        public float Radius => _radius;
        public float Lifetime => _lifetime;
        public float Offset => _offset;
        public float OrbitRadius => _orbitRadius;
        public float AngularSpeed => _angularSpeed;
        public float HitInterval => _hitInterval;
        public float TickInterval => _tickInterval;
        public int Pierce => _pierce;
        public float PierceLoss => _pierceLoss;
        public float HomingTurn => _homingTurn;
        public float HomingRange => _homingRange;
        public float ArcRange => _arcRange;
        public int ArcTargets => _arcTargets;
        public float ArcMultiplier => _arcMultiplier;
        public float LearningMultiplier => _learningMultiplier;
        public float SpreadAngle => _spreadAngle;

        /// <summary>형태·속성·수식 정의와 개별 수식 노드의 배율을 결합하여 읽기 전용 효과 수치를 만든다.</summary>
        public SpellStats(RuneStats form, RuneStats element, IReadOnlyList<RuneDefinition> mods, string formKind, IReadOnlyList<GraphNode> modifierNodes = null)
        {
            float damageMult = 1f;
            float radiusMult = 1f;
            float rangeMult = 1f;
            float speedMult = 1f;
            _learningMultiplier = 1f;
            for (int index = 0; index < mods.Count; index++)
            {
                RuneDefinition mod = mods[index];
                GraphNode modifierNode = modifierNodes == null ? null : modifierNodes[index];
                damageMult *= mod.Stats.DamageMultiplier;
                radiusMult *= mod.Id == "mod.expand" ? ReadModifierNumber(mod, modifierNode, "sizeScale") : mod.Stats.RadiusMultiplier;
                if (mod.Id == "mod.range") rangeMult *= ReadModifierNumber(mod, modifierNode, "rangeScale");
                if (mod.Id == "mod.speed") speedMult *= ReadModifierNumber(mod, modifierNode, "speedScale");
                if (formKind == "bolt" || formKind == "vector")
                {
                    _pierce += mod.Stats.Pierce;
                    if (mod.Stats.PierceLoss > 0f) _pierceLoss = mod.Stats.PierceLoss;
                    if (mod.Stats.HomingTurn > 0f) _homingTurn = mod.Stats.HomingTurn;
                    if (mod.Stats.HomingRange > 0f) _homingRange = mod.Stats.HomingRange;
                }
                _learningMultiplier *= mod.Stats.LearningMultiplier;
                if (mod.Stats.SpreadAngle > 0f) _spreadAngle = mod.Stats.SpreadAngle;
            }
            _damage = form.Damage * damageMult;
            _speed = form.Speed * speedMult;
            _radius = form.Radius * radiusMult;
            _lifetime = (formKind == "bolt" || formKind == "vector") ? form.Lifetime * rangeMult / speedMult : form.Lifetime;
            _offset = form.Offset * rangeMult;
            _orbitRadius = form.OrbitRadius * radiusMult * rangeMult;
            _angularSpeed = form.AngularSpeed * speedMult;
            _hitInterval = form.HitInterval;
            _tickInterval = form.TickInterval;
            if (element != null)
            {
                _arcRange = element.ArcRange;
                _arcTargets = element.ArcTargets;
                _arcMultiplier = element.ArcMultiplier;
            }
        }

        /// <summary>수식 노드에 저장한 숫자 배율을 반환하며 이전 설계에는 해당 파라미터의 데이터 기본값을 사용한다.</summary>
        private static float ReadModifierNumber(RuneDefinition rune, GraphNode node, string parameter)
        {
            foreach (ParameterDefinition definition in rune.Params)
                if (definition.Id == parameter) return node == null ? definition.DefaultNumber : node.GetNumber(parameter, definition.DefaultNumber);
            return 1f;
        }
    }

    public sealed class SpellCondition
    {
        private readonly string _type;
        private readonly float _pct;
        private readonly string _status;
        private readonly float _px;
        public string Type => _type;
        public float Pct => _pct;
        public string Status => _status;
        public float Px => _px;

        /// <summary>조건 노드의 기본값과 편집 값을 읽어 런타임 조건으로 변환한다.</summary>
        public SpellCondition(GraphNode node, RuneDefinition rune)
        {
            foreach (ParameterDefinition param in rune.Params)
            {
                switch (param.Id)
                {
                    case "type": _type = node.GetText("type", param.DefaultText); break;
                    case "pct": _pct = node.GetNumber("pct", param.DefaultNumber); break;
                    case "status": _status = node.GetText("status", param.DefaultText); break;
                    case "px": _px = node.GetNumber("px", param.DefaultNumber); break;
                }
            }
        }
    }

    public sealed class SpellAction
    {
        private readonly GraphNode _parameters;
        private readonly RuneDefinition _definition;
        private readonly string _kind;
        private readonly string _nodeId;
        private readonly string _form;
        private readonly string _element;
        private readonly bool _isNoise;
        private readonly int _count;
        private readonly SpellStats _stats;
        private readonly IReadOnlyList<string> _mods;
        private readonly float _seconds;
        private readonly int _times;
        private readonly float _interval;
        private readonly SpellCondition _condition;
        private readonly float _distance;
        private readonly float _shieldAmount;
        private readonly float _shieldSeconds;
        private readonly float _ownEnergy;

        private IReadOnlyList<SpellAction> _events = new List<SpellAction>();
        private IReadOnlyList<SpellAction> _onHit = new List<SpellAction>();
        private IReadOnlyList<SpellAction> _onExpire = new List<SpellAction>();
        private IReadOnlyList<SpellAction> _then = new List<SpellAction>();
        private IReadOnlyList<SpellAction> _else = new List<SpellAction>();
        private IReadOnlyList<SpellAction> _body = new List<SpellAction>();
        private IReadOnlyList<SpellAction> _next = new List<SpellAction>();
        private IReadOnlyList<string> _attachedNodeIds = new List<string>();
        public string Kind => _kind;
        public IReadOnlyList<SpellAction> Events => _events;
        public string NodeId => _nodeId;
        public string Form => _form;
        public string Element => _element;
        public bool Noise => _isNoise;
        public int Count => _count;
        public SpellStats Stats => _stats;
        public IReadOnlyList<string> Mods => _mods;
        public float Seconds => _seconds;
        public int Times => _times;
        public float Interval => _interval;
        public SpellCondition Condition => _condition;
        public float Distance => _distance;
        public float ShieldAmount => _shieldAmount;
        public float ShieldSeconds => _shieldSeconds;
        public float OwnEnergy => _ownEnergy;
        public IReadOnlyList<SpellAction> OnHit => _onHit;
        public IReadOnlyList<SpellAction> OnExpire => _onExpire;
        public IReadOnlyList<SpellAction> Then => _then;
        public IReadOnlyList<SpellAction> Else => _else;
        public IReadOnlyList<SpellAction> Body => _body;
        public IReadOnlyList<SpellAction> Next => _next;
        public IReadOnlyList<string> AttachedNodeIds => _attachedNodeIds;

        /// <summary>형태 또는 흐름 노드 정의와 연결 수식으로 실행 명령을 초기화한다.</summary>
        public SpellAction(GraphNode node, RuneDefinition rune, RuneDefinition element, IReadOnlyList<RuneDefinition> mods, IReadOnlyList<GraphNode> modifierNodes = null)
        {
            _parameters = node.Clone(node.Id);
            _definition = rune;
            _nodeId = node.Id;
            _kind = rune.Category == "form" ? "spawn" : rune.Id.Substring(rune.Id.IndexOf('.') + 1);
            _form = rune.Category == "form" ? rune.Id.Substring(5) : "";
            _element = element == null ? "raw" : element.Id.Substring(5);
            _count = rune.Category == "form" ? rune.Stats.Count : 0;
            _ownEnergy = rune.Energy;
            List<string> ids = new List<string>();
            foreach (RuneDefinition mod in mods)
            {
                ids.Add(mod.Id);
                _ownEnergy *= mod.EnergyMult;
                if (mod.Id == "mod.multi") _count = _form == "orbit" ? mod.Stats.OrbitCount : mod.Stats.Count;
                if (mod.Id == "mod.noise") _isNoise = true;
            }
            _mods = ids;
            if (element != null) _ownEnergy += element.Energy;
            if (_kind == "spawn") _stats = new SpellStats(rune.Stats, element?.Stats, mods, _form, modifierNodes);
            _seconds = node.GetNumber("seconds", DefaultNumber(rune, "seconds"));
            _times = (int)node.GetNumber("times", DefaultNumber(rune, "times"));
            _interval = node.GetNumber("interval", DefaultNumber(rune, "interval"));
            _distance = node.GetNumber("distance", DefaultNumber(rune, "distance"));
            _shieldAmount = rune.Stats.ShieldAmount;
            _shieldSeconds = rune.Stats.ShieldSeconds;
            if (_kind == "if") _condition = new SpellCondition(node, rune);
        }

        /// <summary>복사한 노드에서 숫자 설정을 읽고 생략된 설정은 해당 룬의 기본값을 반환한다.</summary>
        public float Number(string key) => _parameters.GetNumber(key, DefaultNumber(_definition, key));

        /// <summary>복사한 노드에서 열거 설정을 읽고 생략된 설정은 해당 룬의 기본값을 반환한다.</summary>
        public string Text(string key)
        {
            foreach (ParameterDefinition parameter in _definition.Params)
                if (parameter.Id == key) return _parameters.GetText(key, parameter.DefaultText);
            return "";
        }

        /// <summary>룬 파라미터 목록에서 숫자 기본값을 찾고 없으면 0을 반환한다.</summary>
        private static float DefaultNumber(RuneDefinition rune, string key)
        {
            foreach (ParameterDefinition param in rune.Params)
                if (param.Id == key) return param.DefaultNumber;
            return 0f;
        }

        /// <summary>포트 이름에 맞는 실행 후속 명령 목록을 연결한다.</summary>
        public void SetBranch(string port, IReadOnlyList<SpellAction> actions)
        {
            switch (port)
            {
                case "event": _events = actions; break;
                case "onHit": _onHit = actions; break;
                case "onExpire": _onExpire = actions; break;
                case "then": _then = actions; break;
                case "else": _else = actions; break;
                case "body": _body = actions; break;
                case "next": _next = actions; break;
            }
        }

        /// <summary>형태 실행 시 함께 강조할 속성·수식 원본 노드 ID를 저장한다.</summary>
        public void SetAttachedNodes(IReadOnlyList<string> nodeIds) => _attachedNodeIds = nodeIds;
    }

    public sealed class CompiledSpell
    {
        private readonly int _version;
        private readonly SpellAction _input;
        private readonly string _name;
        private readonly string _signature;
        private readonly string _coreNodeId;
        private readonly int _ramUsed;
        private readonly float _energyCost;
        private readonly float _cooldown;
        private readonly int _worstCaseEntities;
        private readonly IReadOnlyList<string> _tags;
        private readonly IReadOnlyList<SpellAction> _root;
        public string Name => _name;
        public bool IsModular => _version == 2;
        public SpellAction Input => _input;
        public string Signature => _signature;
        public string CoreNodeId => _coreNodeId;
        public int RamUsed => _ramUsed;
        public float EnergyCost => _energyCost;
        public float Cooldown => _cooldown;
        public int WorstCaseEntities => _worstCaseEntities;
        public IReadOnlyList<string> Tags => _tags;
        public IReadOnlyList<SpellAction> Root => _root;

        /// <summary>검증된 그래프의 비용·실행 루트·적응 태그를 읽기 전용 마법으로 저장한다.</summary>
        public CompiledSpell(string name, string signature, string coreNodeId, int ramUsed, float energyCost,
            float cooldown, int worstCaseEntities, IReadOnlyList<string> tags, IReadOnlyList<SpellAction> root,
            int version = 1, SpellAction input = null)
        {
            _version = version;
            _input = input;
            _name = name;
            _signature = signature;
            _coreNodeId = coreNodeId;
            _ramUsed = ramUsed;
            _energyCost = energyCost;
            _cooldown = cooldown;
            _worstCaseEntities = worstCaseEntities;
            _tags = tags;
            _root = root;
        }
    }

    public sealed class CompileResult
    {
        private readonly IReadOnlyList<CompileIssue> _errors;
        private readonly IReadOnlyList<CompileIssue> _warnings;
        private readonly CompiledSpell _spell;
        public bool Ok => _errors.Count == 0 && _spell != null;
        public IReadOnlyList<CompileIssue> Errors => _errors;
        public IReadOnlyList<CompileIssue> Warnings => _warnings;
        public CompiledSpell Spell => _spell;

        /// <summary>컴파일의 오류·경고와 성공한 마법을 반환 객체에 저장한다.</summary>
        public CompileResult(IReadOnlyList<CompileIssue> errors, IReadOnlyList<CompileIssue> warnings, CompiledSpell spell)
        {
            _errors = errors;
            _warnings = warnings;
            _spell = spell;
        }
    }
}
