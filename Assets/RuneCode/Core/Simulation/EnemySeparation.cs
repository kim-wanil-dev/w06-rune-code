using System;
using System.Collections.Generic;

namespace RuneCode
{
    /// <summary>
    /// 적이 한 점으로 뭉치지 않게 매 틱 공간 격자로 겹친 쌍을 찾아 서로 밀어내는 분리 처리기다.
    /// 격자 배열과 적별 밀림 버퍼를 소유하며 전투 시작에 Resize로 크기를 정하고 틱마다 새로 할당하지 않는다.
    /// 밀림 양을 모두 누적한 뒤 한꺼번에 적용하는 야코비 방식이라 적 목록 순서에 좌우되지 않고 상태를 남기지 않는다.
    /// </summary>
    internal sealed class EnemySeparation
    {
        private const double NEAR_ZERO_DISTANCE = 0.000001;
        private const double FULL_CIRCLE_RADIANS = Math.PI * 2;
        private const int DETERMINISTIC_ANGLE_STEPS = 64;
        private int[] _head = Array.Empty<int>();
        private int[] _next = Array.Empty<int>();
        private SimVector[] _push = Array.Empty<SimVector>();
        private int _columns;
        private int _rows;
        private double _cellSize;

        /// <summary>맵 픽셀 크기, 적 상한, 격자 칸 크기로 격자와 밀림 버퍼를 준비한다. 전투 시작에 한 번 호출하며 배열이 이미 충분하면 다시 할당하지 않는다.</summary>
        public void Resize(int mapWidth, int mapHeight, int enemyLimit, double cellSize)
        {
            _cellSize = cellSize > 0 ? cellSize : 1;
            _columns = Math.Max(1, (int)Math.Ceiling(mapWidth / _cellSize));
            _rows = Math.Max(1, (int)Math.Ceiling(mapHeight / _cellSize));
            int cellCount = _columns * _rows;
            if (_head.Length != cellCount) _head = new int[cellCount];
            if (_next.Length < enemyLimit) _next = new int[enemyLimit];
            if (_push.Length < enemyLimit) _push = new SimVector[enemyLimit];
        }

        /// <summary>
        /// 살아 있고 더미가 아닌 적을 격자에 넣고, 허용 거리(반경 합 × 허용 비율)보다 가까운 쌍의 겹친 양을 질량 비율로 나눠 누적한 뒤 한꺼번에 적용한다.
        /// 질량은 반경이며 isFixed가 true인 적은 밀리지 않고 상대를 전부 민다. 각 적의 누적 밀림은 최대 이동량으로 잘라 벽을 검사하는 맵 이동으로 옮기고 바라보는 방향은 유지한다.
        /// </summary>
        public void Apply(IReadOnlyList<SimulationEnemy> enemies, MissionMap map, CombatBalance combat, Func<SimulationEnemy, bool> isFixed)
        {
            BuildGrid(enemies);
            AccumulatePushes(enemies, combat, isFixed);
            ApplyPushes(enemies, map, combat.SeparationMaxStep);
        }

        /// <summary>틱마다 격자를 비우고 대상 적의 밀림 버퍼를 초기화한 뒤 목록 순서대로 자기 칸 연결 목록 맨 앞에 넣는다.</summary>
        private void BuildGrid(IReadOnlyList<SimulationEnemy> enemies)
        {
            for (int i = 0; i < _head.Length; i++) _head[i] = -1;
            int count = Math.Min(enemies.Count, _next.Length);
            for (int i = 0; i < count; i++)
            {
                SimulationEnemy enemy = enemies[i];
                if (!enemy.IsAlive || enemy.IsDummy) continue;
                _push[i] = SimVector.Zero;
                int cell = GetCellIndex(enemy.Position);
                _next[i] = _head[cell];
                _head[cell] = i;
            }
        }

        /// <summary>적마다 자기 칸과 주변 8칸의 이웃 중 번호가 뒤인 적만 검사해 겹친 양을 두 적의 밀림 버퍼에 나눠 누적한다. 둘 다 고정이면 건너뛴다.</summary>
        private void AccumulatePushes(IReadOnlyList<SimulationEnemy> enemies, CombatBalance combat, Func<SimulationEnemy, bool> isFixed)
        {
            double allowance = combat.SeparationAllowance;
            double strength = combat.SeparationStrength;
            int count = Math.Min(enemies.Count, _next.Length);
            for (int i = 0; i < count; i++)
            {
                SimulationEnemy enemy = enemies[i];
                if (!enemy.IsAlive || enemy.IsDummy) continue;
                bool isEnemyFixed = isFixed(enemy);
                GetCellXY(enemy.Position, out int x, out int y);
                for (int row = y - 1; row <= y + 1; row++)
                {
                    if (row < 0 || row >= _rows) continue;
                    for (int column = x - 1; column <= x + 1; column++)
                    {
                        if (column < 0 || column >= _columns) continue;
                        for (int j = _head[row * _columns + column]; j >= 0; j = _next[j])
                        {
                            if (j <= i) continue;
                            SimulationEnemy other = enemies[j];
                            bool isOtherFixed = isFixed(other);
                            if (isEnemyFixed && isOtherFixed) continue;
                            double allowedDistance = (enemy.Radius + other.Radius) * allowance;
                            SimVector offset = enemy.Position - other.Position;
                            double distance = offset.Length;
                            if (distance >= allowedDistance) continue;
                            SimVector direction = distance < NEAR_ZERO_DISTANCE ? GetDeterministicDirection(enemy.Id, other.Id) : offset / distance;
                            double enemyWeight;
                            double otherWeight;
                            if (isEnemyFixed) { enemyWeight = 0; otherWeight = 1; }
                            else if (isOtherFixed) { enemyWeight = 1; otherWeight = 0; }
                            else { enemyWeight = other.Radius / (enemy.Radius + other.Radius); otherWeight = enemy.Radius / (enemy.Radius + other.Radius); }
                            SimVector push = direction * ((allowedDistance - distance) * strength);
                            _push[i] += push * enemyWeight;
                            _push[j] -= push * otherWeight;
                        }
                    }
                }
            }
        }

        /// <summary>누적한 밀림을 적 목록 순서대로 한꺼번에 적용한다. 길이를 틱당 최대 이동량으로 잘라 벽을 검사하는 맵 이동으로 옮기고 Facing은 그대로 둔다.</summary>
        private void ApplyPushes(IReadOnlyList<SimulationEnemy> enemies, MissionMap map, double maxStep)
        {
            int count = Math.Min(enemies.Count, _push.Length);
            for (int i = 0; i < count; i++)
            {
                SimulationEnemy enemy = enemies[i];
                if (!enemy.IsAlive || enemy.IsDummy) continue;
                SimVector push = _push[i];
                double length = push.Length;
                if (length < NEAR_ZERO_DISTANCE) continue;
                if (length > maxStep) push = push / length * maxStep;
                enemy.Move(map.Move(enemy.Position, push, enemy.Radius), enemy.Facing);
            }
        }

        /// <summary>월드 좌표를 격자 칸 번호로 바꾼다. 맵 경계 밖 위치는 가장자리 칸으로 몰아 넣는다.</summary>
        private int GetCellIndex(SimVector position)
        {
            GetCellXY(position, out int x, out int y);
            return y * _columns + x;
        }

        /// <summary>월드 좌표를 격자 칸 열·행 번호로 바꾼다. 맵 경계 밖 위치는 가장자리 칸으로 몰아 넣는다.</summary>
        private void GetCellXY(SimVector position, out int x, out int y)
        {
            x = Math.Clamp((int)(position.X / _cellSize), 0, _columns - 1);
            y = Math.Clamp((int)(position.Y / _cellSize), 0, _rows - 1);
        }

        /// <summary>두 적 ID로 항상 같은 각도의 단위 방향을 정한다. 거리가 0에 가까운 쌍(분열 자식, 같은 틱 스폰)의 밀어낼 방향을 정하기 위함이다.</summary>
        private static SimVector GetDeterministicDirection(int firstId, int secondId)
        {
            double angle = ((firstId * 31 + secondId * 17) & (DETERMINISTIC_ANGLE_STEPS - 1)) / (double)DETERMINISTIC_ANGLE_STEPS * FULL_CIRCLE_RADIANS;
            return new SimVector(Math.Cos(angle), Math.Sin(angle));
        }
    }
}
