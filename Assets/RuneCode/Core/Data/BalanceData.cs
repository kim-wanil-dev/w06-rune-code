using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    [Serializable]
    public sealed class PlayerBalance
    {
        [Header("플레이어")]
        [SerializeField] private float _maxHp;
        [SerializeField] private float _moveSpeed;
        [SerializeField] private float _maxEnergy;
        [SerializeField] private float _energyRegen;
        [SerializeField] private float _dashDistance;
        [SerializeField] private float _dashDuration;
        [SerializeField] private float _dashCooldown;
        [SerializeField] private float _hitInvulnerability;
        public float MaxHp => _maxHp;
        public float MoveSpeed => _moveSpeed;
        public float MaxEnergy => _maxEnergy;
        public float EnergyRegen => _energyRegen;
        public float DashDistance => _dashDistance;
        public float DashDuration => _dashDuration;
        public float DashCooldown => _dashCooldown;
        public float HitInvulnerability => _hitInvulnerability;
        public float Hp => _maxHp;
        public float Speed => _moveSpeed;
        public float Energy => _maxEnergy;
        public float DashSeconds => _dashDuration;
        public float HurtInvulnerability => _hitInvulnerability;
    }

    [Serializable]
    public sealed class LimitBalance
    {
        [Header("제한")]
        [SerializeField] private int _maxLiveSpellEntities;
        [SerializeField] private int _maxFrameSteps;
        [SerializeField] private int _maxGraphNodes;
        [SerializeField] private int _maxGraphEdges;
        [SerializeField] private int _maxEnemies = 300;
        [SerializeField] private int _maxCompiledActions = 2048;
        [SerializeField] private int _maxScheduledExecutions = 2048;
        [SerializeField] private int _maxActionsPerTick = 512;
        public int MaxLiveSpellEntities => _maxLiveSpellEntities;
        public int MaxFrameSteps => _maxFrameSteps;
        public int MaxGraphNodes => _maxGraphNodes;
        public int MaxGraphEdges => _maxGraphEdges;
        public int MaxEnemies => _maxEnemies;
        public int MaxCompiledActions => _maxCompiledActions;
        public int MaxScheduledExecutions => _maxScheduledExecutions;
        public int MaxActionsPerTick => _maxActionsPerTick;
    }

    [Serializable]
    public sealed class AdaptationBalance
    {
        [Header("적응")]
        [SerializeField] private float _elementLearning;
        [SerializeField] private float _formLearning;
        [SerializeField] private float _elementCap;
        [SerializeField] private float _formCap;
        [SerializeField] private float _dotWeight;
        [SerializeField] private float _noiseMultiplier;
        [SerializeField] private float _relayMultiplier;
        [SerializeField] private float _decayDelay;
        [SerializeField] private float _decayPerSecond;
        [SerializeField] private float _resistanceThreshold;
        public float ElementLearning => _elementLearning;
        public float FormLearning => _formLearning;
        public float ElementCap => _elementCap;
        public float FormCap => _formCap;
        public float DotWeight => _dotWeight;
        public float NoiseMultiplier => _noiseMultiplier;
        public float RelayMultiplier => _relayMultiplier;
        public float DecayDelay => _decayDelay;
        public float DecayPerSecond => _decayPerSecond;
        public float ResistanceThreshold => _resistanceThreshold;
    }

    [Serializable]
    public sealed class CombatBalance
    {
        [Header("상태와 방어")]
        [SerializeField] private float _burnDps;
        [SerializeField] private float _burnInterval;
        [SerializeField] private float _burnSeconds;
        [SerializeField] private float _chillSlow;
        [SerializeField] private float _chillSeconds;
        [SerializeField] private int _chillMaxStacks;
        [SerializeField] private int _chillImmuneMaxStacks;
        [SerializeField] private float _freezeSeconds;
        [SerializeField] private float _freezeImmunity;
        [SerializeField] private float _empSeconds;
        [SerializeField] private float _aegisAngle;
        [SerializeField] private float _aegisReduction;
        [SerializeField] private float _relayRadius;
        [SerializeField] private float _relayReduction;
        [SerializeField] private float _separationAllowance;
        [SerializeField] private float _separationStrength;
        [SerializeField] private float _separationMaxStep;
        public float BurnDps => _burnDps;
        public float BurnInterval => _burnInterval;
        public float BurnSeconds => _burnSeconds;
        public float ChillSlow => _chillSlow;
        public float ChillSeconds => _chillSeconds;
        public int ChillMaxStacks => _chillMaxStacks;

        /// <summary>빙결 면역 중 쌓을 수 있는 냉기 스택 상한이다. 빙결을 일으키는 최대 스택보다 작아야 한다.</summary>
        public int ChillImmuneMaxStacks => _chillImmuneMaxStacks;
        public float FreezeSeconds => _freezeSeconds;
        public float FreezeImmunity => _freezeImmunity;
        public float EmpSeconds => _empSeconds;
        public float AegisAngle => _aegisAngle;
        public float AegisReduction => _aegisReduction;
        public float RelayRadius => _relayRadius;
        public float RelayReduction => _relayReduction;
        public float SeparationAllowance => _separationAllowance;
        public float SeparationStrength => _separationStrength;
        public float SeparationMaxStep => _separationMaxStep;
    }

    [Serializable]
    public sealed class EconomyBalance
    {
        [Header("성장")]
        [SerializeField] private int[] _capacityCosts;
        [SerializeField] private int[] _statCosts;
        [SerializeField] private int _slotCost;
        [SerializeField] private float _deathRetention;
        [SerializeField] private int _baseCapacity;
        [SerializeField] private int _capacityStep;
        [SerializeField] private int _maxLibrary;
        [SerializeField] private int _modifierStartStock;
        [SerializeField] private int _statStep;
        [SerializeField] private float _orbAbsorbRadius;
        [SerializeField] private float _terminalHeal;

        [Header("인크리멘탈 성장")]
        [SerializeField] private int _capacityBaseCost = 12;
        [SerializeField] private int _energyBaseCost = 15;
        [SerializeField] private int _durationBaseCost = 18;
        [SerializeField] private float _growthCostMultiplier = 1.22f;
        [SerializeField] private int _maxGrowthLevel = 100;
        [SerializeField] private int _maxDurationLevel = 18;
        [SerializeField] private float _durationStep = 5;
        [SerializeField] private float _energyRegenStep = 1;
        public IReadOnlyList<int> CapacityCosts => _capacityCosts;
        public IReadOnlyList<int> StatCosts => _statCosts;
        public int SlotCost => _slotCost;
        public float DeathRetention => _deathRetention;
        public int BaseCapacity => _baseCapacity;
        public int CapacityStep => _capacityStep;
        public int MaxLibrary => _maxLibrary;

        /// <summary>새 세이브(또는 소지량 기록이 없는 세이브)에서 Modifier 종류마다 주는 시작 소지량이다.</summary>
        public int ModifierStartStock => _modifierStartStock;
        public int StatStep => _statStep;
        public float OrbAbsorbRadius => _orbAbsorbRadius;
        public float TerminalHeal => _terminalHeal;
        public int MaxGrowthLevel => _maxGrowthLevel;
        public int MaxDurationLevel => _maxDurationLevel;
        public float DurationStep => _durationStep;
        public float EnergyRegenStep => _energyRegenStep;
        public float GrowthCostMultiplier => _growthCostMultiplier;

        /// <summary>성장 종류와 현재 단계에서 다음 강화 가격을 계산하고 상한 도달이면 -1을 반환한다.</summary>
        public int GetGrowthCost(string kind, int level)
        {
            int baseCost = kind == "capacity" ? _capacityBaseCost : kind == "energy" ? _energyBaseCost : kind == "duration" ? _durationBaseCost : 0;
            int maxLevel = kind == "duration" ? _maxDurationLevel : _maxGrowthLevel;
            if (baseCost == 0 || level < 0 || level >= maxLevel) return -1;
            return (int)Math.Min(int.MaxValue, Math.Ceiling(baseCost * Math.Pow(_growthCostMultiplier, level)));
        }
    }

    [Serializable]
    public sealed class RamBalance
    {
        [Header("RAM")]
        [SerializeField] private string _mode;
        [SerializeField] private float _cooldownBase;
        [SerializeField] private float _cooldownPerRam;
        public string Mode => _mode;
        public float CooldownBase => _cooldownBase;
        public float CooldownPerRam => _cooldownPerRam;
    }

    [Serializable]
    public sealed class SimulationBalance
    {
        [Header("시뮬레이션")]
        [SerializeField] private int _tickRate;
        [SerializeField] private float _multiOffset;
        [SerializeField] private float _telemetryHighlightSeconds;
        [SerializeField] private float _benchPlayerX;
        [SerializeField] private float _benchPlayerY;
        [SerializeField] private float _benchDummyX;
        [SerializeField] private float _benchDummyY;
        [SerializeField] private float _benchHp;
        [SerializeField] private float _benchLineStartX;
        [SerializeField] private float _benchLineGap;
        [SerializeField] private int _benchLineCount;
        [SerializeField] private float _benchSwarmX;
        [SerializeField] private float _benchSwarmY;
        [SerializeField] private int _benchSwarmColumns;
        [SerializeField] private int _benchSwarmRows;
        [SerializeField] private float _benchSwarmGapX;
        [SerializeField] private float _benchSwarmGapY;
        [SerializeField] private float _spellVisualSeconds;
        [SerializeField] private float _hitFlashSeconds;
        [SerializeField] private float _damageNumberSeconds;
        [SerializeField] private float _wandOffset;
        [SerializeField] private float _itemPickupRadius = 24f;
        [SerializeField] private float _executionTimeScale = 1f;
        public int TickRate => _tickRate;
        public float MultiOffset => _multiOffset;
        public float TelemetryHighlightSeconds => _telemetryHighlightSeconds;
        public float BenchPlayerX => _benchPlayerX;
        public float BenchPlayerY => _benchPlayerY;
        public float BenchDummyX => _benchDummyX;
        public float BenchDummyY => _benchDummyY;
        public float BenchHp => _benchHp;
        public float BenchLineStartX => _benchLineStartX;
        public float BenchLineGap => _benchLineGap;
        public int BenchLineCount => _benchLineCount;
        public float BenchSwarmX => _benchSwarmX;
        public float BenchSwarmY => _benchSwarmY;
        public int BenchSwarmColumns => _benchSwarmColumns;
        public int BenchSwarmRows => _benchSwarmRows;
        public float BenchSwarmGapX => _benchSwarmGapX;
        public float BenchSwarmGapY => _benchSwarmGapY;
        public float SpellVisualSeconds => _spellVisualSeconds;
        public float HitFlashSeconds => _hitFlashSeconds;
        public float DamageNumberSeconds => _damageNumberSeconds;
        public float WandOffset => _wandOffset;

        /// <summary>바닥에 떨어진 Modifier 드롭을 줍는 반경이다.</summary>
        public float ItemPickupRadius => _itemPickupRadius;

        /// <summary>룬 실행 시간 기여분의 전역 배율이며 0이면 Shape·Behavior 노드 시간 기여를 끈다.</summary>
        public float ExecutionTimeScale => _executionTimeScale;
    }

    [Serializable]
    public sealed class BalanceData
    {
        [Header("공용 밸런스")]
        [SerializeField] private PlayerBalance _player;
        [SerializeField] private LimitBalance _limits;
        [SerializeField] private AdaptationBalance _adaptation;
        [SerializeField] private CombatBalance _combat;
        [SerializeField] private EconomyBalance _economy;
        [SerializeField] private RamBalance _ram;
        [SerializeField] private SimulationBalance _sim;
        public PlayerBalance Player => _player;
        public LimitBalance Limits => _limits;
        public AdaptationBalance Adaptation => _adaptation;
        public CombatBalance Combat => _combat;
        public EconomyBalance Economy => _economy;
        public RamBalance Ram => _ram;
        public SimulationBalance Sim => _sim;

        private GrammarLimits _grammar;

        /// <summary>문법 엔진(컴파일러)에 전달할 한도·기본값을 이 밸런스 값으로 만들어 반환한다. 처음 요청할 때 한 번 만든다.</summary>
        public GrammarLimits Grammar => _grammar ??= new GrammarLimits(_limits.MaxGraphNodes, _limits.MaxGraphEdges,
            _limits.MaxCompiledActions, _limits.MaxLiveSpellEntities, _economy.BaseCapacity,
            _player.MaxEnergy, _ram.CooldownBase, _ram.CooldownPerRam);

        /// <summary>밸런스 JSON을 읽고 필수 설정과 양수 제한값을 검증하여 반환한다.</summary>
        public static BalanceData FromJson(string json)
        {
            BalanceData data = JsonUtility.FromJson<BalanceData>(json);
            if (data == null || data.Player == null || data.Limits == null || data.Adaptation == null
                || data.Combat == null || data.Economy == null || data.Ram == null || data.Sim == null
                || data.Player.MaxHp <= 0f || data.Player.MaxEnergy <= 0f || data.Sim.TickRate != 60
                || data.Limits.MaxLiveSpellEntities <= 0
                || data.Economy.BaseCapacity <= 0 || data.Economy.CapacityCosts == null || data.Economy.StatCosts == null
                || (data.Ram.Mode != "shared" && data.Ram.Mode != "perSpell"))
                throw new FormatException("유효하지 않은 밸런스 데이터입니다.");
            float[] values = { data.Player.MaxHp, data.Player.MoveSpeed, data.Player.MaxEnergy, data.Player.EnergyRegen,
                data.Player.DashDistance, data.Player.DashDuration, data.Player.DashCooldown, data.Player.HitInvulnerability,
                data.Adaptation.ElementLearning, data.Adaptation.FormLearning, data.Adaptation.ElementCap, data.Adaptation.FormCap,
                data.Adaptation.DotWeight, data.Adaptation.NoiseMultiplier, data.Adaptation.RelayMultiplier, data.Adaptation.DecayDelay,
                data.Adaptation.DecayPerSecond, data.Adaptation.ResistanceThreshold, data.Combat.BurnDps, data.Combat.BurnInterval,
                data.Combat.BurnSeconds, data.Combat.ChillSlow, data.Combat.ChillSeconds, data.Combat.FreezeSeconds,
                data.Combat.FreezeImmunity, data.Combat.EmpSeconds, data.Combat.AegisAngle, data.Combat.AegisReduction,
                data.Combat.RelayRadius, data.Combat.RelayReduction, data.Economy.DeathRetention, data.Economy.OrbAbsorbRadius,
                data.Economy.TerminalHeal, data.Ram.CooldownBase, data.Ram.CooldownPerRam, data.Sim.MultiOffset,
                data.Sim.TelemetryHighlightSeconds, data.Sim.BenchPlayerX, data.Sim.BenchPlayerY, data.Sim.BenchDummyX,
                data.Sim.BenchDummyY, data.Sim.BenchHp, data.Sim.BenchLineStartX, data.Sim.BenchLineGap, data.Sim.BenchSwarmX,
                data.Sim.BenchSwarmY, data.Sim.BenchSwarmGapX, data.Sim.BenchSwarmGapY, data.Sim.SpellVisualSeconds,
                data.Sim.HitFlashSeconds, data.Sim.DamageNumberSeconds, data.Sim.WandOffset, data.Sim.ItemPickupRadius,
                data.Sim.ExecutionTimeScale,
                data.Economy.DurationStep, data.Economy.EnergyRegenStep, data.Economy.GrowthCostMultiplier,
                data.Combat.SeparationAllowance, data.Combat.SeparationStrength, data.Combat.SeparationMaxStep };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                    throw new FormatException("밸런스 값은 유한한 0 이상이어야 합니다.");
            if (data.Player.MoveSpeed <= 0f || data.Player.DashDuration <= 0f || data.Combat.BurnInterval <= 0f
                || data.Combat.ChillMaxStacks <= 0 || data.Combat.ChillImmuneMaxStacks <= 0
                || data.Combat.ChillImmuneMaxStacks >= data.Combat.ChillMaxStacks
                || data.Adaptation.ElementCap > 1f || data.Adaptation.FormCap > 1f
                || data.Combat.AegisReduction > 1f || data.Combat.RelayReduction > 1f || data.Combat.ChillSlow > 1f
                || data.Combat.SeparationAllowance <= 0f || data.Combat.SeparationAllowance > 1f
                || data.Combat.SeparationStrength <= 0f || data.Combat.SeparationStrength > 1f
                || data.Combat.SeparationMaxStep <= 0f
                || data.Economy.DeathRetention > 1f || data.Economy.TerminalHeal > 1f || data.Economy.CapacityStep <= 0
                || data.Economy.StatStep <= 0 || data.Economy.MaxLibrary <= 0 || data.Economy.ModifierStartStock < 0 || data.Economy.SlotCost <= 0
                || data.Limits.MaxFrameSteps <= 0 || data.Limits.MaxGraphNodes <= 0 || data.Limits.MaxGraphEdges <= 0
                || data.Economy.CapacityCosts.Count == 0 || data.Economy.StatCosts.Count == 0 || data.Sim.BenchHp <= 0f
                || data.Sim.BenchLineCount <= 0 || data.Sim.BenchSwarmColumns <= 0 || data.Sim.BenchSwarmRows <= 0
                || data.Sim.SpellVisualSeconds <= 0f || data.Sim.DamageNumberSeconds <= 0f || data.Sim.ItemPickupRadius <= 0f
                || data.Limits.MaxEnemies <= 0 || data.Limits.MaxCompiledActions <= 0
                || data.Limits.MaxScheduledExecutions <= 0 || data.Limits.MaxActionsPerTick <= 0
                || data.Economy.MaxGrowthLevel <= 0 || data.Economy.MaxGrowthLevel > 255
                || data.Economy.MaxDurationLevel <= 0 || data.Economy.MaxDurationLevel > 100
                || data.Economy.DurationStep <= 0 || data.Economy.EnergyRegenStep < 0
                || data.Economy.GrowthCostMultiplier < 1 || data.Economy.GrowthCostMultiplier > 2
                || data.Economy.GetGrowthCost("capacity", 0) <= 0 || data.Economy.GetGrowthCost("energy", 0) <= 0
                || data.Economy.GetGrowthCost("duration", 0) <= 0)
                throw new FormatException("밸런스 범위 또는 비용 목록이 유효하지 않습니다.");
            foreach (int cost in data.Economy.CapacityCosts)
                if (cost <= 0) throw new FormatException("RAM 용량 비용은 양수여야 합니다.");
            foreach (int cost in data.Economy.StatCosts)
                if (cost <= 0) throw new FormatException("능력치 비용은 양수여야 합니다.");
            return data;
        }
    }
}
