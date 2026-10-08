using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    [Serializable]
    public sealed class PortDefinition
    {
        [Header("포트")]
        [SerializeField] private string _id;
        [SerializeField] private string _kind;
        [SerializeField] private string _direction;
        [SerializeField] private int _max;

        public string Id => _id;
        public string Kind => _kind;
        public string Direction => _direction;
        public int Max => _max;
    }

    [Serializable]
    public sealed class ParameterDefinition
    {
        [Header("파라미터")]
        [SerializeField] private string _id;
        [SerializeField] private string _kind;
        [SerializeField] private float _min;
        [SerializeField] private float _max;
        [SerializeField] private float _step;
        [SerializeField] private float _defaultNumber;
        [SerializeField] private string _defaultText;
        [SerializeField] private string[] _options;

        public string Id => _id;
        public string Kind => _kind;
        public float Min => _min;
        public float Max => _max;
        public float Step => _step;
        public float DefaultNumber => _defaultNumber;
        public string DefaultText => _defaultText;
        public IReadOnlyList<string> Options => _options ?? Array.Empty<string>();
    }

    [Serializable]
    public sealed class RuneStats
    {
        [Header("효과")]
        [SerializeField] private float _damage;
        [SerializeField] private float _speed;
        [SerializeField] private float _radius;
        [SerializeField] private float _lifetime;
        [SerializeField] private float _offset;
        [SerializeField] private int _count;
        [SerializeField] private int _orbitCount;
        [SerializeField] private float _orbitRadius;
        [SerializeField] private float _angularSpeed;
        [SerializeField] private float _hitInterval;
        [SerializeField] private float _tickInterval;
        [SerializeField] private float _damageMultiplier = 1f;
        [SerializeField] private float _radiusMultiplier = 1f;
        [SerializeField] private int _pierce;
        [SerializeField] private float _pierceLoss;
        [SerializeField] private float _homingTurn;
        [SerializeField] private float _homingRange;
        [SerializeField] private float _arcRange;
        [SerializeField] private int _arcTargets;
        [SerializeField] private float _arcMultiplier;
        [SerializeField] private float _learningMultiplier = 1f;
        [SerializeField] private float _spreadAngle;
        [SerializeField] private float _shieldAmount;
        [SerializeField] private float _shieldSeconds;

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
    }

    [Serializable]
    public sealed class RuneDefinition
    {
        [Header("룬 정의")]
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private string _category;
        [SerializeField] private int _ram = 10000;
        [SerializeField] private float _energy;
        [SerializeField] private float _energyMult = 1f;
        [SerializeField] private string _unlockType;
        [SerializeField] private int _unlockCost;
        [SerializeField] private string[] _tags;
        [SerializeField] private PortDefinition[] _ports;
        [SerializeField] private ParameterDefinition[] _params;
        [SerializeField] private RuneStats _stats;

        public string Id => _id;
        public string Name => _name;
        public string Category => _category;
        public int Ram => _ram;
        public float Energy => _energy;
        public float EnergyMult => _energyMult;
        public string UnlockType => _unlockType;
        public int UnlockCost => _unlockCost;
        public IReadOnlyList<string> Tags => _tags ?? Array.Empty<string>();
        public IReadOnlyList<PortDefinition> Ports => _ports ?? Array.Empty<PortDefinition>();
        public IReadOnlyList<ParameterDefinition> Params => _params ?? Array.Empty<ParameterDefinition>();
        public RuneStats Stats => _stats;

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

    [Serializable]
    public sealed class MagicRewardProfile
    {
        [Header("제약과 보상")]
        [SerializeField] private string _id;
        [SerializeField] private float _damageMultiplier = 1f;
        [SerializeField] private float _radiusMultiplier = 1f;
        [SerializeField] private float _energyFraction;
        [SerializeField] private float _hpThreshold;
        [SerializeField] private string _consumeStatus;
        public string Id => _id;
        public float DamageMultiplier => _damageMultiplier;
        public float RadiusMultiplier => _radiusMultiplier;
        public float EnergyFraction => _energyFraction;
        public float HpThreshold => _hpThreshold;
        public string ConsumeStatus => _consumeStatus;
    }

    [Serializable]
    public sealed class RuneCatalog
    {
        [Header("룬 목록")]
        [SerializeField] private RuneDefinition[] _runes;
        [SerializeField] private MagicRewardProfile[] _profiles;

        private Dictionary<string, RuneDefinition> _byId;
        private List<string> _startRunes;

        public IReadOnlyList<RuneDefinition> All => _runes ?? Array.Empty<RuneDefinition>();
        public IReadOnlyList<string> StartRunes => _startRunes;

        /// <summary>등록한 제약 ID의 고정 보상을 반환하며 없는 프로필은 null을 반환한다.</summary>
        public MagicRewardProfile FindProfile(string id)
        {
            foreach (MagicRewardProfile profile in _profiles ?? Array.Empty<MagicRewardProfile>())
                if (profile.Id == id) return profile;
            return null;
        }

        /// <summary>룬 JSON을 읽고 필수 정의와 중복 ID를 검증한 레지스트리를 반환한다.</summary>
        public static RuneCatalog FromJson(string json)
        {
            RuneCatalog catalog = JsonUtility.FromJson<RuneCatalog>(json);
            if (catalog == null || catalog._runes == null || catalog._runes.Length == 0)
                throw new FormatException("룬 정의가 비어 있습니다.");
            catalog._byId = new Dictionary<string, RuneDefinition>(StringComparer.Ordinal);
            catalog._startRunes = new List<string>();
            HashSet<string> profileIds = new HashSet<string>();
            foreach (MagicRewardProfile profile in catalog._profiles ?? Array.Empty<MagicRewardProfile>())
            {
                if (profile == null || string.IsNullOrEmpty(profile.Id) || !profileIds.Add(profile.Id)
                    || !IsFinite(profile.DamageMultiplier) || profile.DamageMultiplier < 1 || profile.DamageMultiplier > 3
                    || !IsFinite(profile.RadiusMultiplier) || profile.RadiusMultiplier < 1 || profile.RadiusMultiplier > 3
                    || !IsFinite(profile.EnergyFraction) || profile.EnergyFraction < 0 || profile.EnergyFraction > .5f
                    || !IsFinite(profile.HpThreshold) || profile.HpThreshold < 0 || profile.HpThreshold > .3f
                    || (profile.EnergyFraction == 0 && profile.HpThreshold == 0 && string.IsNullOrEmpty(profile.ConsumeStatus)))
                    throw new FormatException("유효하지 않은 제약 보상 프로필입니다.");
            }
            foreach (RuneDefinition rune in catalog._runes)
            {
                if (rune == null || string.IsNullOrEmpty(rune.Id) || string.IsNullOrEmpty(rune.Name)
                    || rune.Stats == null || rune.Ram < 0 || rune.Energy < 0f || !IsFinite(rune.Energy)
                    || rune.EnergyMult <= 0f || !IsFinite(rune.EnergyMult)
                    || (rune.Category != "core" && rune.Category != "form" && rune.Category != "element"
                        && rune.Category != "modifier" && rune.Category != "flow" && rune.Category != "action"
                        && rune.Category != "trigger" && rune.Category != "constraint")
                    || (rune.UnlockType != "start" && rune.UnlockType != "bench" && rune.UnlockType != "reward")
                    || rune.UnlockCost < 0 || (rune.UnlockType == "bench" && rune.UnlockCost == 0))
                    throw new FormatException("유효하지 않은 룬 정의입니다.");
                ValidateStats(rune);
                if (!catalog._byId.TryAdd(rune.Id, rune)) throw new FormatException("중복 룬 ID: " + rune.Id);
                if (rune.UnlockType == "start") catalog._startRunes.Add(rune.Id);
                HashSet<string> portIds = new HashSet<string>();
                foreach (PortDefinition port in rune.Ports)
                {
                    if (port == null || string.IsNullOrEmpty(port.Id) || !portIds.Add(port.Direction + ":" + port.Id)
                        || (port.Kind != "exec" && port.Kind != "mod" && port.Kind != "event")
                        || (port.Direction != "in" && port.Direction != "out") || port.Max < 0
                        || (port.Direction == "in" && port.Kind == "exec" && port.Max != 1)
                        || (port.Direction == "in" && port.Kind == "event" && port.Max != 1)
                        || (port.Direction == "in" && port.Kind == "mod" && port.Max != 3))
                        throw new FormatException("유효하지 않은 룬 포트: " + rune.Id);
                }
                HashSet<string> paramIds = new HashSet<string>();
                foreach (ParameterDefinition param in rune.Params)
                {
                    if (param == null || string.IsNullOrEmpty(param.Id) || !paramIds.Add(param.Id)
                        || (param.Kind != "number" && param.Kind != "enum")
                        || (param.Kind == "number" && (param.Min > param.Max || param.DefaultNumber < param.Min || param.DefaultNumber > param.Max
                            || !IsFinite(param.Min) || !IsFinite(param.Max) || !IsFinite(param.DefaultNumber) || param.Step <= 0f))
                        || (param.Kind == "enum" && !ContainsOption(param)))
                        throw new FormatException("유효하지 않은 룬 파라미터: " + rune.Id);
                }
            }
            return catalog;
        }

        /// <summary>룬 ID로 정의를 반환하며 알 수 없는 ID이면 예외를 발생시킨다.</summary>
        public RuneDefinition Get(string id) => _byId[id];

        /// <summary>룬 ID의 존재 여부와 정의를 반환한다.</summary>
        public bool TryGet(string id, out RuneDefinition rune)
        {
            rune = null;
            return !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out rune);
        }

        /// <summary>열거 파라미터의 옵션에 기본값이 실제로 존재하는지 반환한다.</summary>
        private static bool ContainsOption(ParameterDefinition param)
        {
            foreach (string option in param.Options)
                if (!string.IsNullOrEmpty(option) && option == param.DefaultText) return true;
            return false;
        }

        /// <summary>룬 효과 수치가 유한하고 음수가 아니며 형태에 필수 수치가 있는지 검증한다.</summary>
        private static void ValidateStats(RuneDefinition rune)
        {
            RuneStats stats = rune.Stats;
            float[] numbers = { stats.Damage, stats.Speed, stats.Radius, stats.Lifetime, stats.Offset, stats.OrbitRadius,
                stats.AngularSpeed, stats.HitInterval, stats.TickInterval, stats.DamageMultiplier, stats.RadiusMultiplier,
                stats.PierceLoss, stats.HomingTurn, stats.HomingRange, stats.ArcRange, stats.ArcMultiplier,
                stats.LearningMultiplier, stats.SpreadAngle, stats.ShieldAmount, stats.ShieldSeconds };
            foreach (float value in numbers)
                if (!IsFinite(value) || value < 0f) throw new FormatException("유효하지 않은 룬 효과 수치: " + rune.Id);
            if (stats.Count < 0 || stats.Pierce < 0 || stats.ArcTargets < 0 || stats.OrbitCount < 0
                || (rune.Category == "form" && (stats.Count <= 0 || (rune.Id != "form.infusion" && stats.Damage <= 0f) || stats.Radius <= 0f)))
                throw new FormatException("유효하지 않은 형태 룬 수치: " + rune.Id);
        }

        /// <summary>설정 수치가 NaN이나 무한대가 아닌지 반환한다.</summary>
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
