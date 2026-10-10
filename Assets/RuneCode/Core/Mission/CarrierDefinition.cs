using System;
using System.Collections.Generic;

namespace RuneCode
{
    /// <summary>캐리어 Prefab에서 읽은 생성 대상과 수치를 시뮬레이션에 전달하는 읽기 전용 설정이다.</summary>
    public sealed class CarrierDefinition
    {
        private readonly string[] _enemyIds;
        private readonly double _interval;
        private readonly int _count;
        private readonly double _distance;
        private readonly int _maxAlive;
        public IReadOnlyList<string> EnemyIds => _enemyIds;
        public double Interval => _interval;
        public int Count => _count;
        public double Distance => _distance;
        public int MaxAlive => _maxAlive;

        /// <summary>Prefab 생성 설정을 복사하고 유한한 주기·거리, 양수 생성 수와 생존 상한을 검증한다.</summary>
        public CarrierDefinition(string[] enemyIds, double interval, int count, double distance, int maxAlive)
        {
            if (enemyIds == null || enemyIds.Length == 0 || !IsPositive(interval) || count < 1 || !IsPositive(distance) || maxAlive < 1)
                throw new FormatException("캐리어 생성 설정이 유효하지 않습니다.");
            _enemyIds = (string[])enemyIds.Clone(); _interval = interval; _count = count; _distance = distance; _maxAlive = maxAlive;
        }

        /// <summary>생성 대상 ID를 카탈로그와 대조하고 보스·생성형 적의 재생성을 거부한다.</summary>
        public void Validate(EnemyCatalog catalog)
        {
            foreach (string id in _enemyIds)
                if (catalog.Get(id).HasTrait(EnemyDefinition.TRAIT_CARRIER) || id.StartsWith("boss.", StringComparison.Ordinal))
                    throw new FormatException("캐리어의 생성 대상은 생성형·보스가 아닌 일반 적이어야 합니다: " + id);
        }

        /// <summary>수치가 유한한 양수인지 반환한다.</summary>
        private static bool IsPositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }
}
