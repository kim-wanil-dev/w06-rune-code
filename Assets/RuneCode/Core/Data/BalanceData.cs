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
        [SerializeField] private int _maxFrameSteps;
        [SerializeField] private int _maxGraphNodes;
        [SerializeField] private int _maxGraphEdges;
        [SerializeField] private int _maxEnemies = 300;
        public int MaxFrameSteps => _maxFrameSteps;
        public int MaxGraphNodes => _maxGraphNodes;
        public int MaxGraphEdges => _maxGraphEdges;
        public int MaxEnemies => _maxEnemies;
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
        [SerializeField] private float _freezeSeconds;
        [SerializeField] private float _freezeImmunity;
        [SerializeField] private float _empSeconds;
        [SerializeField] private float _aegisAngle;
        [SerializeField] private float _aegisReduction;
        [SerializeField] private float _relayRadius;
        [SerializeField] private float _relayReduction;
        public float BurnDps => _burnDps;
        public float BurnInterval => _burnInterval;
        public float BurnSeconds => _burnSeconds;
        public float ChillSlow => _chillSlow;
        public float ChillSeconds => _chillSeconds;
        public int ChillMaxStacks => _chillMaxStacks;
        public float FreezeSeconds => _freezeSeconds;
        public float FreezeImmunity => _freezeImmunity;
        public float EmpSeconds => _empSeconds;
        public float AegisAngle => _aegisAngle;
        public float AegisReduction => _aegisReduction;
        public float RelayRadius => _relayRadius;
        public float RelayReduction => _relayReduction;
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
    }

    [Serializable]
    public sealed class SpellSettings
    {
        [Header("마법 그래프")]
        [SerializeField] private float _basePower;
        [SerializeField] private float _tokenLifetime;
        [SerializeField] private int _maxNodesPerFrame;
        [SerializeField] private float _manaMax;
        [SerializeField] private float _manaRegen;
        [SerializeField] private float _loadMin;
        [SerializeField] private float _loadMax;
        [SerializeField] private float _loadDefault;
        [SerializeField] private float _loadStep;
        [SerializeField] private float _costFloor;
        [SerializeField] private float _projectileCostPerShot;
        [SerializeField] private float _amplifyCostFactor;
        [SerializeField] private float _addCost;
        [SerializeField] private float _addPower;
        [SerializeField] private float _splitCostFactor;
        [SerializeField] private float _splitPowerFactor;
        [SerializeField] private float _forkCost;
        [SerializeField] private float _joinCost;
        [SerializeField] private float _branchCost;
        [SerializeField] private float _amplifyMin;
        [SerializeField] private float _amplifyMax;
        [SerializeField] private float _amplifyDefault;
        [SerializeField] private float _amplifyStep;
        [SerializeField] private float _forkShareMin;
        [SerializeField] private float _forkShareMax;
        [SerializeField] private float _forkShareDefault;
        [SerializeField] private float _forkShareStep;
        [SerializeField] private float _branchThresholdMin;
        [SerializeField] private float _branchThresholdMax;
        [SerializeField] private float _branchThresholdDefault;
        [SerializeField] private float _branchThresholdStep;
        [SerializeField] private float _projectileDwell;
        [SerializeField] private float _defaultDwell;
        [SerializeField] private float _onHitDwell;
        [SerializeField] private float _minDwell;
        [SerializeField] private float _joinMaxWait;
        [SerializeField] private float _projectileSpeedTiles;
        [SerializeField] private float _projectileRangeTiles;
        [SerializeField] private float _projectileRadius;
        [SerializeField] private float _fanStepDegrees;
        [SerializeField] private float _fanMaxDegrees;
        [SerializeField] private float _batchPositionTolerance;
        [SerializeField] private float _batchAngleToleranceDegrees;
        [SerializeField] private int _maxTokens;
        [SerializeField] private int _maxProjectiles;
        [SerializeField] private float _endEventSeconds;
        public float BasePower => _basePower;
        public float TokenLifetime => _tokenLifetime;
        public int MaxNodesPerFrame => _maxNodesPerFrame;
        public float ManaMax => _manaMax;
        public float ManaRegen => _manaRegen;
        public float LoadMin => _loadMin;
        public float LoadMax => _loadMax;
        public float LoadDefault => _loadDefault;
        public float LoadStep => _loadStep;
        public float CostFloor => _costFloor;
        public float ProjectileCostPerShot => _projectileCostPerShot;
        public float AmplifyCostFactor => _amplifyCostFactor;
        public float AddCost => _addCost;
        public float AddPower => _addPower;
        public float SplitCostFactor => _splitCostFactor;
        public float SplitPowerFactor => _splitPowerFactor;
        public float ForkCost => _forkCost;
        public float JoinCost => _joinCost;
        public float BranchCost => _branchCost;
        public float AmplifyMin => _amplifyMin;
        public float AmplifyMax => _amplifyMax;
        public float AmplifyDefault => _amplifyDefault;
        public float AmplifyStep => _amplifyStep;
        public float ForkShareMin => _forkShareMin;
        public float ForkShareMax => _forkShareMax;
        public float ForkShareDefault => _forkShareDefault;
        public float ForkShareStep => _forkShareStep;
        public float BranchThresholdMin => _branchThresholdMin;
        public float BranchThresholdMax => _branchThresholdMax;
        public float BranchThresholdDefault => _branchThresholdDefault;
        public float BranchThresholdStep => _branchThresholdStep;
        public float ProjectileDwell => _projectileDwell;
        public float DefaultDwell => _defaultDwell;
        public float OnHitDwell => _onHitDwell;
        public float MinDwell => _minDwell;
        public float JoinMaxWait => _joinMaxWait;
        public float ProjectileSpeedTiles => _projectileSpeedTiles;
        public float ProjectileRangeTiles => _projectileRangeTiles;
        public float ProjectileRadius => _projectileRadius;
        public float FanStepDegrees => _fanStepDegrees;
        public float FanMaxDegrees => _fanMaxDegrees;
        public float BatchPositionTolerance => _batchPositionTolerance;
        public float BatchAngleToleranceDegrees => _batchAngleToleranceDegrees;
        public int MaxTokens => _maxTokens;
        public int MaxProjectiles => _maxProjectiles;
        public float EndEventSeconds => _endEventSeconds;
    }

    [Serializable]
    public sealed class BalanceData
    {
        [Header("공용 밸런스")]
        [SerializeField] private PlayerBalance _player;
        [SerializeField] private LimitBalance _limits;
        [SerializeField] private CombatBalance _combat;
        [SerializeField] private EconomyBalance _economy;
        [SerializeField] private SimulationBalance _sim;
        [SerializeField] private SpellSettings _spell;
        public PlayerBalance Player => _player;
        public LimitBalance Limits => _limits;
        public CombatBalance Combat => _combat;
        public EconomyBalance Economy => _economy;
        public SimulationBalance Sim => _sim;
        public SpellSettings Spell => _spell;

        /// <summary>밸런스 JSON을 읽고 필수 설정과 양수 제한값을 검증하여 반환한다.</summary>
        public static BalanceData FromJson(string json)
        {
            BalanceData data = JsonUtility.FromJson<BalanceData>(json);
            if (data == null || data.Player == null || data.Limits == null
                || data.Combat == null || data.Economy == null || data.Sim == null
                || data.Player.MaxHp <= 0f || data.Player.MaxEnergy <= 0f || data.Sim.TickRate != 60
                || data.Economy.BaseCapacity <= 0 || data.Economy.CapacityCosts == null || data.Economy.StatCosts == null)
                throw new FormatException("유효하지 않은 밸런스 데이터입니다.");
            float[] values = { data.Player.MaxHp, data.Player.MoveSpeed, data.Player.MaxEnergy, data.Player.EnergyRegen,
                data.Player.DashDistance, data.Player.DashDuration, data.Player.DashCooldown, data.Player.HitInvulnerability,
                data.Combat.BurnDps, data.Combat.BurnInterval,
                data.Combat.BurnSeconds, data.Combat.ChillSlow, data.Combat.ChillSeconds, data.Combat.FreezeSeconds,
                data.Combat.FreezeImmunity, data.Combat.EmpSeconds, data.Combat.AegisAngle, data.Combat.AegisReduction,
                data.Combat.RelayRadius, data.Combat.RelayReduction, data.Economy.DeathRetention, data.Economy.OrbAbsorbRadius,
                data.Economy.TerminalHeal, data.Sim.MultiOffset,
                data.Sim.TelemetryHighlightSeconds, data.Sim.BenchPlayerX, data.Sim.BenchPlayerY, data.Sim.BenchDummyX,
                data.Sim.BenchDummyY, data.Sim.BenchHp, data.Sim.BenchLineStartX, data.Sim.BenchLineGap, data.Sim.BenchSwarmX,
                data.Sim.BenchSwarmY, data.Sim.BenchSwarmGapX, data.Sim.BenchSwarmGapY, data.Sim.SpellVisualSeconds,
                data.Sim.HitFlashSeconds, data.Sim.DamageNumberSeconds, data.Sim.WandOffset,
                data.Economy.DurationStep, data.Economy.EnergyRegenStep, data.Economy.GrowthCostMultiplier };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                    throw new FormatException("밸런스 값은 유한한 0 이상이어야 합니다.");
            if (data.Player.MoveSpeed <= 0f || data.Player.DashDuration <= 0f || data.Combat.BurnInterval <= 0f
                || data.Combat.ChillMaxStacks <= 0
                || data.Combat.AegisReduction > 1f || data.Combat.RelayReduction > 1f || data.Combat.ChillSlow > 1f
                || data.Economy.DeathRetention > 1f || data.Economy.TerminalHeal > 1f || data.Economy.CapacityStep <= 0
                || data.Economy.StatStep <= 0 || data.Economy.MaxLibrary <= 0 || data.Economy.SlotCost <= 0
                || data.Limits.MaxFrameSteps <= 0 || data.Limits.MaxGraphNodes <= 0 || data.Limits.MaxGraphEdges <= 0
                || data.Economy.CapacityCosts.Count == 0 || data.Economy.StatCosts.Count == 0 || data.Sim.BenchHp <= 0f
                || data.Sim.BenchLineCount <= 0 || data.Sim.BenchSwarmColumns <= 0 || data.Sim.BenchSwarmRows <= 0
                || data.Sim.SpellVisualSeconds <= 0f || data.Sim.DamageNumberSeconds <= 0f
                || data.Limits.MaxEnemies <= 0
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
            ValidateSpell(data.Spell);
            return data;
        }

        /// <summary>마법 그래프 설정의 존재, 유한한 값, 최소값과 범위 관계를 검증하고 위반 시 예외를 던진다.</summary>
        private static void ValidateSpell(SpellSettings spell)
        {
            if (spell == null)
                throw new FormatException("유효하지 않은 마법 그래프 설정입니다.");
            float[] values = {
                spell.BasePower, spell.TokenLifetime, spell.ManaMax, spell.ManaRegen, spell.LoadMin,
                spell.LoadMax, spell.LoadDefault, spell.LoadStep, spell.CostFloor, spell.ProjectileCostPerShot,
                spell.AmplifyCostFactor, spell.AddCost, spell.AddPower, spell.SplitCostFactor, spell.SplitPowerFactor,
                spell.ForkCost, spell.JoinCost, spell.BranchCost, spell.AmplifyMin, spell.AmplifyMax,
                spell.AmplifyDefault, spell.AmplifyStep, spell.ForkShareMin, spell.ForkShareMax, spell.ForkShareDefault,
                spell.ForkShareStep, spell.BranchThresholdMin, spell.BranchThresholdMax, spell.BranchThresholdDefault, spell.BranchThresholdStep,
                spell.ProjectileDwell, spell.DefaultDwell, spell.OnHitDwell, spell.MinDwell, spell.JoinMaxWait,
                spell.ProjectileSpeedTiles, spell.ProjectileRangeTiles, spell.ProjectileRadius, spell.FanStepDegrees, spell.FanMaxDegrees,
                spell.BatchPositionTolerance, spell.BatchAngleToleranceDegrees, spell.EndEventSeconds
            };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                    throw new FormatException("유효하지 않은 마법 그래프 설정입니다.");
            if (spell.CostFloor < 1f
                || spell.BasePower <= 0f
                || spell.TokenLifetime <= 0f
                || spell.MaxNodesPerFrame <= 0
                || spell.MaxTokens <= 0
                || spell.MaxProjectiles <= 0
                || spell.ManaMax <= 0f
                || spell.LoadMin <= 0f
                || spell.LoadMin > spell.LoadDefault
                || spell.LoadDefault > spell.LoadMax
                || spell.LoadStep <= 0f
                || spell.AmplifyMin <= 1f
                || spell.AmplifyMin > spell.AmplifyDefault
                || spell.AmplifyDefault > spell.AmplifyMax
                || spell.AmplifyStep <= 0f
                || spell.ForkShareMin <= 0f
                || spell.ForkShareMin > spell.ForkShareDefault
                || spell.ForkShareDefault > spell.ForkShareMax
                || spell.ForkShareMax >= 100f
                || spell.ForkShareStep <= 0f
                || spell.BranchThresholdMin > spell.BranchThresholdDefault
                || spell.BranchThresholdDefault > spell.BranchThresholdMax
                || spell.BranchThresholdStep <= 0f
                || spell.MinDwell <= 0f
                || spell.ProjectileDwell < spell.MinDwell
                || spell.DefaultDwell < spell.MinDwell
                || spell.JoinMaxWait <= 0f
                || spell.ProjectileSpeedTiles <= 0f
                || spell.ProjectileRangeTiles <= 0f
                || spell.ProjectileRadius <= 0f
                || spell.FanStepDegrees <= 0f
                || spell.FanMaxDegrees <= 0f
                || spell.EndEventSeconds <= 0f)
                throw new FormatException("유효하지 않은 마법 그래프 설정입니다.");
        }
    }
}
