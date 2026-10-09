using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    [Serializable]
    public sealed class EnemyDefinition
    {
        [Header("적 설정")]
        [SerializeField] private string _id;
        [SerializeField] private double _hp;
        [SerializeField] private double _speed;
        [SerializeField] private double _radius;
        [SerializeField] private double _damage;
        [SerializeField] private double _attackInterval;
        [SerializeField] private double _warningSeconds;
        [SerializeField] private double _attackRange;
        [SerializeField] private double _dashSpeed;
        [SerializeField] private double _dashSeconds;
        [SerializeField] private double _projectileSpeed;
        [SerializeField] private double _projectileRadius;
        [SerializeField] private double _projectileLifetime;
        [SerializeField] private double _spreadDegrees;
        [SerializeField] private double _burstInterval;
        [SerializeField] private int _reward;
        public string Id => _id;
        public double Hp => _hp;
        public double Speed => _speed;
        public double Radius => _radius;
        public double Damage => _damage;
        public double AttackInterval => _attackInterval;
        public double WarningSeconds => _warningSeconds;
        public double AttackRange => _attackRange;
        public double DashSpeed => _dashSpeed;
        public double DashSeconds => _dashSeconds;
        public double ProjectileSpeed => _projectileSpeed;
        public double ProjectileRadius => _projectileRadius;
        public double ProjectileLifetime => _projectileLifetime;
        public double SpreadDegrees => _spreadDegrees;
        public double BurstInterval => _burstInterval;
        public int Reward => _reward;
    }

    [Serializable]
    public sealed class EnemyCatalog
    {
        [Header("적 종류")]
        [SerializeField] private EnemyDefinition[] _enemies;
        public IReadOnlyList<EnemyDefinition> Enemies => _enemies;

        /// <summary>적 ID에 해당하는 설정을 반환하고 누락된 ID에는 데이터 오류를 발생시킨다.</summary>
        public EnemyDefinition Get(string id)
        { for (int i = 0; i < _enemies.Length; i++) if (_enemies[i].Id == id) return _enemies[i]; throw new FormatException("등록되지 않은 적: " + id); }

        /// <summary>JSON 적 설정을 읽고 고유 ID, 체력, 크기 및 보상 범위를 검증한다.</summary>
        public static EnemyCatalog FromJson(string json)
        {
            var catalog = JsonUtility.FromJson<EnemyCatalog>(json);
            if (catalog == null || catalog._enemies == null || catalog._enemies.Length != 6) throw new FormatException("적 설정은 6종이어야 합니다.");
            var ids = new HashSet<string>();
            foreach (EnemyDefinition enemy in catalog._enemies)
                if (!ids.Add(enemy.Id) || enemy.Hp <= 0 || enemy.Radius <= 0 || enemy.Reward < 0) throw new FormatException("적 설정 값이 유효하지 않습니다.");
            return catalog;
        }
    }

    [Serializable]
    public sealed class MissionSpawn
    {
        [Header("웨이브 배치")]
        [SerializeField] private string _enemyId;
        [SerializeField] private int _point;
        [SerializeField] private int _count;
        public string EnemyId => _enemyId;
        public int Point => _point;
        public int Count => _count;
    }

    [Serializable]
    public sealed class MissionWave
    {
        [Header("웨이브")]
        [SerializeField] private MissionSpawn[] _spawns;
        public IReadOnlyList<MissionSpawn> Spawns => _spawns;
    }

    [Serializable]
    public sealed class MissionRoom
    {
        [Header("방")]
        [SerializeField] private string _id;
        [SerializeField] private string _nameKey;
        [SerializeField] private string[] _tiles;
        [SerializeField] private MissionWave[] _waves;
        public string Id => _id;
        public string NameKey => _nameKey;
        public string[] Tiles => _tiles;
        public IReadOnlyList<MissionWave> Waves => _waves;
    }

    [Serializable]
    public sealed class SectorDefinition
    {
        [Header("섹터")]
        [SerializeField] private int _tileSize;
        [SerializeField] private double _playerRadius;
        [SerializeField] private double _spawnSpacing;
        [SerializeField] private MissionRoom[] _rooms;
        public int TileSize => _tileSize;
        public double PlayerRadius => _playerRadius;
        public double SpawnSpacing => _spawnSpacing;
        public IReadOnlyList<MissionRoom> Rooms => _rooms;

        /// <summary>방 데이터의 크기, 벽 경계, 시작점, 웨이브 및 스폰 참조를 검증하여 반환한다.</summary>
        public static SectorDefinition FromJson(string json, EnemyCatalog enemies)
        {
            var sector = JsonUtility.FromJson<SectorDefinition>(json);
            if (sector == null || sector._rooms == null || sector._rooms.Length != 5 || sector._tileSize <= 0 || sector._playerRadius <= 0) throw new FormatException("섹터 구성 오류입니다.");
            foreach (MissionRoom room in sector._rooms)
            {
                var map = new MissionMap(room.Tiles, sector.TileSize);
                if (room.Waves == null || room.Waves.Count == 0) throw new FormatException("방에 웨이브가 없습니다: " + room.Id);
                foreach (MissionWave wave in room.Waves)
                    foreach (MissionSpawn spawn in wave.Spawns)
                    { enemies.Get(spawn.EnemyId); if (spawn.Count < 1 || !map.HasPoint((char)('0' + spawn.Point))) throw new FormatException("스폰 참조 오류입니다."); }
            }
            return sector;
        }
    }

    [Serializable]
    public sealed class GovernorDefinition
    {
        [Header("거버너 패턴")]
        [SerializeField] private double _phaseTwoThreshold;
        [SerializeField] private double _phaseThreeThreshold;
        [SerializeField] private double _patchSeconds;
        [SerializeField] private double _radialInterval;
        [SerializeField] private int _radialCount;
        [SerializeField] private double _aimInterval;
        [SerializeField] private double _hazardInterval;
        [SerializeField] private double _hazardWarning;
        [SerializeField] private double _hazardLifetime;
        [SerializeField] private double _hazardRadius;
        [SerializeField] private double _hazardDamage;
        [SerializeField] private double _reinforcementInterval;
        [SerializeField] private int _reinforcementCount;
        [SerializeField] private double _phaseThreeMultiplier;
        [SerializeField] private double _preferredDistance;
        public double PhaseTwoThreshold => _phaseTwoThreshold;
        public double PhaseThreeThreshold => _phaseThreeThreshold;
        public double PatchSeconds => _patchSeconds;
        public double RadialInterval => _radialInterval;
        public int RadialCount => _radialCount;
        public double AimInterval => _aimInterval;
        public double HazardInterval => _hazardInterval;
        public double HazardWarning => _hazardWarning;
        public double HazardLifetime => _hazardLifetime;
        public double HazardRadius => _hazardRadius;
        public double HazardDamage => _hazardDamage;
        public double ReinforcementInterval => _reinforcementInterval;
        public int ReinforcementCount => _reinforcementCount;
        public double PhaseThreeMultiplier => _phaseThreeMultiplier;
        public double PreferredDistance => _preferredDistance;

        /// <summary>보스의 페이즈 문턱, 패치 및 공격 주기를 검증하여 JSON 설정을 반환한다.</summary>
        public static GovernorDefinition FromJson(string json)
        { var data = JsonUtility.FromJson<GovernorDefinition>(json); if (data == null || data.PhaseTwoThreshold <= data.PhaseThreeThreshold || data.PatchSeconds <= 0 || data.RadialCount < 1 || data.RadialInterval <= 0 || data.ReinforcementInterval <= 0) throw new FormatException("보스 패턴 설정 오류입니다."); return data; }
    }

    public sealed class MissionMap
    {
        private const int MIN_SIZE = 3;

        private readonly string[] _tiles;
        private readonly int _tileSize;
        private readonly Dictionary<char, SimVector> _points = new Dictionary<char, SimVector>();
        public string[] Tiles => _tiles;
        public int TileSize => _tileSize;
        public int Width => _tiles[0].Length * _tileSize;
        public int Height => _tiles.Length * _tileSize;
        public SimVector PlayerStart => _points['P'];

        /// <summary>직사각형 ASCII 맵의 벽과 명명된 시작, 문, 터미널 및 스폰 좌표를 읽는다. 외곽은 모두 벽이어야 한다.</summary>
        public MissionMap(string[] tiles, int tileSize)
        {
            if (tiles == null || tiles.Length < MIN_SIZE || tiles[0] == null || tiles[0].Length < MIN_SIZE) throw new FormatException("맵은 3×3 이상이어야 합니다.");
            _tiles = (string[])tiles.Clone(); _tileSize = tileSize;
            int width = _tiles[0].Length;
            for (int y = 0; y < _tiles.Length; y++)
            {
                if (_tiles[y] == null || _tiles[y].Length != width) throw new FormatException("맵의 모든 줄은 너비가 같아야 합니다.");
                for (int x = 0; x < _tiles[y].Length; x++)
                {
                    char cell = _tiles[y][x];
                    if ((x == 0 || x == width - 1 || y == 0 || y == _tiles.Length - 1) && cell != '#') throw new FormatException("맵 경계는 벽이어야 합니다.");
                    if (cell != '#' && cell != '.') _points[cell] = new SimVector((x + 0.5) * tileSize, (y + 0.5) * tileSize);
                }
            }
            if (!_points.ContainsKey('P')) throw new FormatException("플레이어 시작점이 없습니다.");
        }

        /// <summary>외곽에만 벽이 있는 빈 맵을 지정 칸 수로 만들고 가운데 칸을 플레이어 시작점으로 둔다.</summary>
        public static MissionMap CreateOpen(int columns, int rows, int tileSize)
        {
            var tiles = new string[rows];
            var wall = new string('#', columns);
            var floor = "#" + new string('.', columns - 2) + "#";
            for (int y = 0; y < rows; y++) tiles[y] = y == 0 || y == rows - 1 ? wall : floor;
            char[] center = tiles[rows / 2].ToCharArray(); center[columns / 2] = 'P'; tiles[rows / 2] = new string(center);
            return new MissionMap(tiles, tileSize);
        }

        /// <summary>문자 스폰이나 터미널이 방 안에 존재하는지 반환한다.</summary>
        public bool HasPoint(char point) => _points.ContainsKey(point);

        /// <summary>ASCII 문자에 연결된 중심 좌표를 반환한다.</summary>
        public SimVector GetPoint(char point) => _points[point];

        /// <summary>월드 좌표가 벽 또는 방 외부에 속하는지 반환한다.</summary>
        public bool IsWall(SimVector position)
        { int x = (int)Math.Floor(position.X / _tileSize); int y = (int)Math.Floor(position.Y / _tileSize); return x < 0 || y < 0 || y >= _tiles.Length || x >= _tiles[0].Length || _tiles[y][x] == '#'; }

        /// <summary>원형 개체가 벽 타일과 겹치는지 정확한 원과 사각형 충돌로 확인한다.</summary>
        public bool CanOccupy(SimVector position, double radius)
        {
            int left = (int)Math.Floor((position.X - radius) / _tileSize);
            int right = (int)Math.Floor((position.X + radius) / _tileSize);
            int top = (int)Math.Floor((position.Y - radius) / _tileSize);
            int bottom = (int)Math.Floor((position.Y + radius) / _tileSize);
            for (int y = top; y <= bottom; y++)
                for (int x = left; x <= right; x++)
                {
                    if (y < 0 || x < 0 || y >= _tiles.Length || x >= _tiles[0].Length) return false;
                    if (_tiles[y][x] != '#') continue;
                    double nearestX = Math.Max(x * _tileSize, Math.Min(position.X, (x + 1) * _tileSize));
                    double nearestY = Math.Max(y * _tileSize, Math.Min(position.Y, (y + 1) * _tileSize));
                    double dx = position.X - nearestX; double dy = position.Y - nearestY;
                    if (dx * dx + dy * dy < radius * radius) return false;
                }
            return true;
        }

        /// <summary>이동 경로를 작은 구간으로 확인하고 벽을 따라 미끄러진 최종 이동 가능 좌표를 반환한다.</summary>
        public SimVector Move(SimVector origin, SimVector displacement, double radius)
        {
            int pieces = Math.Max(1, (int)Math.Ceiling(displacement.Length / (_tileSize * 0.25)));
            SimVector result = origin; SimVector step = displacement / pieces;
            for (int i = 0; i < pieces; i++)
            {
                SimVector target = result + step;
                if (CanOccupy(target, radius)) { result = target; continue; }
                SimVector horizontal = new SimVector(target.X, result.Y);
                if (CanOccupy(horizontal, radius)) result = horizontal;
                SimVector vertical = new SimVector(result.X, target.Y);
                if (CanOccupy(vertical, radius)) result = vertical;
            }
            return result;
        }

        /// <summary>점멸 목적지가 벽이면 가장 가까운 이동 가능한 바닥 타일 중심을 반환한다.</summary>
        public SimVector NearestFree(SimVector target, double radius)
        {
            if (CanOccupy(target, radius)) return target;
            SimVector result = PlayerStart; double best = double.MaxValue;
            for (int y = 1; y < _tiles.Length - 1; y++)
                for (int x = 1; x < _tiles[y].Length - 1; x++)
                { if (_tiles[y][x] == '#') continue; var candidate = new SimVector((x + 0.5) * _tileSize, (y + 0.5) * _tileSize); double distance = (candidate - target).LengthSquared; if (distance < best && CanOccupy(candidate, radius)) { result = candidate; best = distance; } }
            return result;
        }
    }
}
