using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

using UnityEngine;

namespace RuneCode
{
    public static class SimulationCli
    {
        private const string DEFAULT_SPELL = "default";
        private const int DEFAULT_CAST_INTERVAL = 30;

        /// <summary>명령행의 --sim 모드에서 지정 마법·시나리오를 실행하여 JSON 파일을 기록하고 종료한다.</summary>
        public static bool TryRunCommandLine()
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Contains("--sim")) return false;
            var output = Argument(arguments, "--output", Path.Combine(Application.persistentDataPath, "runecode.sim.json"));
            try
            {
                var spell = Argument(arguments, "--spell", DEFAULT_SPELL);
                var scenario = Argument(arguments, "--scenario", "dummy_line");
                var ticks = int.Parse(Argument(arguments, "--ticks", "600"), CultureInfo.InvariantCulture);
                var seed = int.Parse(Argument(arguments, "--seed", "1"), CultureInfo.InvariantCulture);
                var stage = int.Parse(Argument(arguments, "--stage", "1"), CultureInfo.InvariantCulture);
                var duration = double.Parse(Argument(arguments, "--duration", "0"), CultureInfo.InvariantCulture);
                var energyLevel = int.Parse(Argument(arguments, "--energy-level", "0"), CultureInfo.InvariantCulture);
                var castInterval = int.Parse(Argument(arguments, "--cast-interval", "30"), CultureInfo.InvariantCulture);
                var report = Run(spell, scenario, ticks, seed, stage, duration, energyLevel, castInterval);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                File.WriteAllText(output, report);
                UnityEngine.Debug.Log(report);
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError(exception.Message);
                Application.Quit(1);
            }
            return true;
        }

        /// <summary>파일 경로의 저장 그래프 또는 기본 그래프로 실행 그래프를 만들고, 시드와 castInterval 틱마다 시전 입력을 넣어 고정 tick 시뮬레이션을 실행한 뒤 피해·DPS·실행 횟수 JSON을 반환한다. 구조 오류가 있는 그래프는 예외를 던진다.</summary>
        public static string Run(string spellPath, string scenario, int ticks, int seed, int stage = 1, double duration = 0, int energyLevel = 0, int castInterval = DEFAULT_CAST_INTERVAL)
        {
            GameData.Load();
            if (ticks < 1 || ticks > 1000000) throw new ArgumentOutOfRangeException(nameof(ticks));
            if (castInterval < 1) throw new ArgumentOutOfRangeException(nameof(castInterval));
            SpellGraph graph = File.Exists(spellPath)
                ? ShareCodec.Deserialize(File.ReadAllText(spellPath))
                : SpellGraph.Create("cli-default", "default");
            var economy = GameData.Balance.Economy;
            if (stage < 1 || stage > 1000000 || double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0
                || energyLevel < 0 || energyLevel > economy.MaxGrowthLevel)
                throw new ArgumentOutOfRangeException(nameof(stage), "스테이지, 제한시간 또는 성장 단계가 유효하지 않습니다.");
            var program = SpellProgram.Build(graph, GameData.Balance.Spell);
            if (!program.IsValid) throw new ArgumentException(string.Join("\n", program.Errors.Select(issue => GameData.L("graph." + issue.Code))));
            var isTimedBattle = scenario == "incremental";
            var spell = GameData.Balance.Spell;
            var maxEnergy = spell.ManaMax + energyLevel * economy.StatStep;
            var regen = spell.ManaRegen + energyLevel * economy.EnergyRegenStep;
            var simulation = new RuneSimulation(seed, isTimedBattle, GameData.Balance.Player.MaxHp, maxEnergy, stage, duration, regen);
            if (!isTimedBattle) simulation.ResetBench(scenario);
            simulation.SetProgram(program);
            var castRange = GetAutomaticCastRange(simulation.Map.TileSize);
            var timer = Stopwatch.StartNew();
            for (var tick = 0; tick < ticks && !simulation.Completed && !simulation.IsDead; tick++)
            {
                var canCast = tick % castInterval == 0;
                simulation.Step(isTimedBattle ? CreateAutomaticInput(simulation, castRange, canCast) : new SimulationInput(SimVector.Zero, new SimVector(1, 0), canCast));
            }
            timer.Stop();
            return JsonUtility.ToJson(new SimulationReport(simulation, simulation.Tick, seed, scenario, program, timer.Elapsed.TotalMilliseconds), true);
        }

        /// <summary>살아 있는 가장 가까운 적을 조준하고, canCast가 true이면서 사거리 안에 있을 때만 시전하는 자동 전투 입력을 반환한다.</summary>
        public static SimulationInput CreateAutomaticInput(RuneSimulation simulation, double castRange, bool canCast)
        {
            SimulationEnemy nearest = null;
            var distanceSquared = double.MaxValue;
            foreach (var enemy in simulation.Enemies)
            {
                if (!enemy.IsAlive) continue;
                var candidateDistance = (enemy.Position - simulation.Player.Position).LengthSquared;
                if (candidateDistance >= distanceSquared) continue;
                nearest = enemy; distanceSquared = candidateDistance;
            }
            var aim = nearest == null ? simulation.Player.AimDirection : nearest.Position - simulation.Player.Position;
            var reach = castRange + (nearest == null ? 0 : nearest.Radius);
            return new SimulationInput(SimVector.Zero, aim, canCast && nearest != null && distanceSquared <= reach * reach);
        }

        /// <summary>설정의 투사체 사거리(타일 단위)에 타일 크기를 곱해 자동 시전 거리(px)를 반환한다.</summary>
        public static double GetAutomaticCastRange(double tileSize)
        {
            return GameData.Balance.Spell.ProjectileRangeTiles * tileSize;
        }

        /// <summary>옵션 이름 다음의 인수를 반환하며 없으면 기본값을 반환한다.</summary>
        private static string Argument(string[] arguments, string name, string fallback)
        {
            var index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : fallback;
        }
    }

    [Serializable]
    public sealed class SimulationReport
    {
        [Header("시뮬레이션 내보내기")]
        [SerializeField] private int _ticks;
        [SerializeField] private int _seed;
        [SerializeField] private string _scenario;
        [SerializeField] private string _signature;
        [SerializeField] private double _maxEnergy;
        [SerializeField] private double _totalDamage;
        [SerializeField] private double _dps;
        [SerializeField] private double _energySpent;
        [SerializeField] private int _peakProjectiles;
        [SerializeField] private int _nodeExecutionCount;
        [SerializeField] private double _elapsedMilliseconds;
        [SerializeField] private string _stateHash;
        [SerializeField] private int _stage;
        [SerializeField] private double _duration;
        [SerializeField] private double _remainingTime;
        [SerializeField] private string _result;
        [SerializeField] private int _kills;
        [SerializeField] private int _earnedRam;
        [SerializeField] private int _settledRam;
        [SerializeField] private int _spawnedEnemies;
        [SerializeField] private double _energyRegen;

        /// <summary>실행한 시뮬레이션과 실행 그래프 서명, 설정값에서 공유 가능한 메트릭 스냅샷을 만든다.</summary>
        public SimulationReport(RuneSimulation simulation, int ticks, int seed, string scenario, SpellProgram program, double elapsedMilliseconds)
        {
            _ticks = ticks; _seed = seed; _scenario = scenario; _signature = program.Signature;
            _maxEnergy = simulation.Player.MaxEnergy;
            _totalDamage = simulation.TotalDamage; _dps = simulation.TotalDamage / (ticks / (double)GameData.Balance.Sim.TickRate);
            _energySpent = simulation.EnergySpent; _peakProjectiles = simulation.PeakProjectiles;
            _nodeExecutionCount = simulation.NodeExecutionCount; _elapsedMilliseconds = elapsedMilliseconds;
            _stateHash = simulation.StateHash();
            _stage = simulation.StageNumber; _duration = simulation.BattleDuration; _remainingTime = simulation.RemainingTime;
            _result = simulation.Stage.ToString(); _kills = simulation.KillCount; _earnedRam = simulation.EarnedFragments;
            _settledRam = simulation.Completed || simulation.IsDead ? simulation.SettlementFragments : 0;
            _spawnedEnemies = simulation.SpawnedEnemies; _energyRegen = simulation.EnergyRegen;
        }
    }
}
