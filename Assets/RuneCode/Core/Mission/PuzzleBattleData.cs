using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    [Serializable]
    public sealed class PuzzlePosition
    {
        [Header("시작 위치 기준 좌표")]
        [SerializeField] private double _x;
        [SerializeField] private double _y;

        public SimVector Offset => new SimVector(_x, _y);
    }

    [Serializable]
    public sealed class PuzzleWave
    {
        [Header("유한 스폰")]
        [SerializeField] private double _at;
        [SerializeField] private PuzzlePosition[] _positions;

        public double At => _at;
        public IReadOnlyList<PuzzlePosition> Positions => _positions;
    }

    [Serializable]
    public sealed class PuzzleBattleDefinition
    {
        [Header("퍼즐 조건")]
        [SerializeField] private string _id;
        [SerializeField] private string _nameKey;
        [SerializeField] private double _maxHp;
        [SerializeField] private double _maxEnergy;
        [SerializeField] private double _energyRegen;
        [SerializeField] private int _capacity;
        [SerializeField] private double _timeLimit;
        [SerializeField] private int _killTarget;
        [SerializeField] private string _enemyId;
        [SerializeField] private PuzzleWave[] _waves;

        public string Id => _id;
        public string Name => GameData.L(_nameKey);
        public double MaxHp => _maxHp;
        public double MaxEnergy => _maxEnergy;
        public double EnergyRegen => _energyRegen;
        public int Capacity => _capacity;
        public double TimeLimit => _timeLimit;
        public int KillTarget => _killTarget;
        public string EnemyId => _enemyId;
        public IReadOnlyList<PuzzleWave> Waves => _waves;
        public int EnemyCount
        {
            get { int count = 0; foreach (PuzzleWave wave in _waves) count += wave.Positions.Count; return count; }
        }
    }

    [Serializable]
    public sealed class PuzzleBattleCatalog
    {
        [Header("퍼즐 목록")]
        [SerializeField] private PuzzleBattleDefinition[] _puzzles;

        public int Count => _puzzles.Length;

        /// <summary>1부터 시작하는 번호의 퍼즐을 반환하며 범위를 벗어나면 예외를 반환한다.</summary>
        public PuzzleBattleDefinition Get(int number)
        {
            if (number < 1 || number > Count) throw new ArgumentOutOfRangeException(nameof(number));
            return _puzzles[number - 1];
        }

        /// <summary>JSON의 퍼즐별 능력치·유한 스폰·목표·시간 설정을 읽고 잘못된 값을 거부한다.</summary>
        public static PuzzleBattleCatalog FromJson(string json)
        {
            PuzzleBattleCatalog catalog = JsonUtility.FromJson<PuzzleBattleCatalog>(json);
            if (catalog?._puzzles == null || catalog.Count == 0) throw new FormatException("퍼즐 목록이 비어 있습니다.");
            var ids = new HashSet<string>();
            foreach (PuzzleBattleDefinition puzzle in catalog._puzzles)
            {
                if (puzzle == null || string.IsNullOrEmpty(puzzle.Id) || !ids.Add(puzzle.Id)
                    || string.IsNullOrEmpty(puzzle.EnemyId) || puzzle.Waves == null || puzzle.Waves.Count == 0
                    || !IsFinite(puzzle.MaxHp) || puzzle.MaxHp <= 0 || !IsFinite(puzzle.MaxEnergy) || puzzle.MaxEnergy <= 0
                    || !IsFinite(puzzle.EnergyRegen) || puzzle.EnergyRegen < 0 || puzzle.Capacity < 1
                    || !IsFinite(puzzle.TimeLimit) || puzzle.TimeLimit < 0 || puzzle.KillTarget < 1)
                    throw new FormatException("퍼즐 조건이 잘못되었습니다.");
                double previousTime = -1;
                foreach (PuzzleWave wave in puzzle.Waves)
                {
                    if (wave == null || !IsFinite(wave.At) || wave.At < 0 || wave.At < previousTime
                        || (puzzle.TimeLimit > 0 && wave.At >= puzzle.TimeLimit) || wave.Positions == null || wave.Positions.Count == 0)
                        throw new FormatException("퍼즐 스폰 일정이 잘못되었습니다: " + puzzle.Id);
                    previousTime = wave.At;
                    foreach (PuzzlePosition position in wave.Positions)
                        if (position == null || !IsFinite(position.Offset.X) || !IsFinite(position.Offset.Y))
                            throw new FormatException("퍼즐 스폰 좌표가 잘못되었습니다: " + puzzle.Id);
                }
                if (puzzle.KillTarget > puzzle.EnemyCount) throw new FormatException("퍼즐 목표가 적 수보다 큽니다: " + puzzle.Id);
            }
            return catalog;
        }

        /// <summary>설정값이 NaN이나 무한대가 아닌지 반환한다.</summary>
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
