using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 미션 시뮬레이션을 월드 오브젝트로 표시한다. 전투 시작 시 맵(벽·격자선)을 한 번 만들고,
    /// 매 프레임 플레이어·적·마법·적 탄·오브·피해 숫자 뷰를 시뮬레이션 목록 순서대로 재사용해 갱신한다.
    /// 피해가 생기면 카메라 흔들림과 짧은 갱신 정지(히트 스톱)를 설정에 따라 적용한다.
    /// </summary>
    public sealed class MissionWorldView : MonoBehaviour
    {
        private const float SHAKE_SECONDS = 0.08f;
        private const float HIT_STOP_SECONDS = 0.025f;
        // 히트 스톱 시작 후 이 시간 동안은 다시 시작하지 않는다. 폭발 확장처럼 피해가 연속될 때 화면이 계속 멈추지 않게 한다.
        private const float HIT_STOP_COOLDOWN_SECONDS = 0.15f;
        private const double DAMAGE_EPSILON = 0.000001;
        private const int MAX_DAMAGE_NUMBERS = 64;
        private const int DAMAGE_NUMBER_TICKS = 40;
        private const float WALL_EDGE_WIDTH = 1f;
        private const float GRID_LINE_WIDTH = 1f;
        private const int GRID_ORDER = 0;
        private const int WALL_ORDER = 1;
        private const int WALL_EDGE_ORDER = 2;

        private static readonly Color WallColor = new Color(0.13f, 0.20f, 0.27f);
        private static readonly Color WallEdgeColor = new Color(0.23f, 0.40f, 0.47f);
        private static readonly Color GridColor = new Color(0.15f, 0.22f, 0.29f);

        [Header("연결")]
        [SerializeField] private MissionCamera _camera;

        [Header("Prefab")]
        [SerializeField] private PlayerView _playerPrefab;
        [SerializeField] private EnemyView _enemyPrefab;
        [SerializeField] private SpellEntityView _spellPrefab;
        [SerializeField] private HostileProjectileView _projectilePrefab;
        [SerializeField] private OrbView _orbPrefab;
        [SerializeField] private ItemDropView _itemDropPrefab;
        [SerializeField] private DamageNumberView _damageNumberPrefab;

        [Header("맵")]
        [SerializeField] private Sprite _squareSprite;
        [SerializeField] private Material _spriteMaterial;
        [SerializeField] private float _gridSpacing = 64f;

        private readonly List<EnemyView> _enemyViews = new List<EnemyView>();
        private readonly Dictionary<string, List<EnemyView>> _additionalEnemyViews = new Dictionary<string, List<EnemyView>>();
        private readonly Dictionary<string, EnemyView> _additionalEnemyPrefabs = new Dictionary<string, EnemyView>();
        private readonly Dictionary<string, int> _additionalEnemyCounts = new Dictionary<string, int>();
        private readonly List<SpellEntityView> _spellViews = new List<SpellEntityView>();
        private readonly List<HostileProjectileView> _projectileViews = new List<HostileProjectileView>();
        private readonly List<OrbView> _orbViews = new List<OrbView>();
        private readonly List<ItemDropView> _itemDropViews = new List<ItemDropView>();
        private readonly List<DamageNumberView> _damageNumberViews = new List<DamageNumberView>();
        private Func<RuneSimulation> _simulation;
        private Func<bool> _isShakeEnabled;
        private Func<bool> _isHitStopEnabled;
        private RuneSimulation _observedSimulation;
        private Transform _mapRoot;
        private PlayerView _playerView;
        private double _lastDamage;
        private double _lastStatusDamage;
        private double _lastPersistDamage;
        private double _lastHp;
        private float _hitStopUntil;
        private float _hitStopReadyAt;

        /// <summary>월드를 따라가는 미션 카메라다. 화면 View가 조준 좌표 변환에 쓴다.</summary>
        public MissionCamera Camera => _camera;

        /// <summary>표시할 시뮬레이션 조회 함수와 화면 흔들림·히트 스톱 설정 조회 함수를 연결한다.</summary>
        public void Initialize(Func<RuneSimulation> simulation, Func<bool> isShakeEnabled, Func<bool> isHitStopEnabled)
        {
            _simulation = simulation;
            _isShakeEnabled = isShakeEnabled;
            _isHitStopEnabled = isHitStopEnabled;
        }

        void LateUpdate()
        {
            RuneSimulation sim = _simulation?.Invoke();
            if (sim == null) return;
            if (_observedSimulation != sim) BeginSimulation(sim);
            DetectImpact(sim);
            if (_hitStopUntil > Time.unscaledTime && (_isHitStopEnabled?.Invoke() ?? false)) return;
            SimVector origin = sim.MapCenter;
            if (_playerView == null) _playerView = Instantiate(_playerPrefab, transform);
            _playerView.Apply(sim.Player, origin);
            int count = 0;
            foreach (string id in _additionalEnemyPrefabs.Keys) _additionalEnemyCounts[id] = 0;
            foreach (SimulationEnemy enemy in sim.Enemies)
            {
                if (_additionalEnemyPrefabs.TryGetValue(enemy.Kind, out EnemyView prefab))
                {
                    int index = _additionalEnemyCounts[enemy.Kind]++;
                    GetView(_additionalEnemyViews[enemy.Kind], prefab, index).Apply(enemy, sim, origin);
                }
                else GetView(_enemyViews, _enemyPrefab, count++).Apply(enemy, sim, origin);
            }
            HideFrom(_enemyViews, count);
            foreach (string id in _additionalEnemyPrefabs.Keys) HideFrom(_additionalEnemyViews[id], _additionalEnemyCounts[id]);
            count = 0;
            foreach (SimulationSpellEntity spell in sim.SpellEntities) GetView(_spellViews, _spellPrefab, count++).Apply(spell, origin);
            HideFrom(_spellViews, count);
            count = 0;
            foreach (SimulationProjectile projectile in sim.EnemyProjectiles) GetView(_projectileViews, _projectilePrefab, count++).Apply(projectile, origin);
            HideFrom(_projectileViews, count);
            count = 0;
            foreach (FragmentOrb orb in sim.Orbs) GetView(_orbViews, _orbPrefab, count++).Apply(orb, origin);
            HideFrom(_orbViews, count);
            count = 0;
            foreach (SimulationItemDrop drop in sim.ItemDrops) GetView(_itemDropViews, _itemDropPrefab, count++).Apply(drop, origin);
            HideFrom(_itemDropViews, count);
            count = 0;
            foreach (DamageNumber number in sim.DamageNumbers)
            {
                int elapsed = sim.Tick - number.Tick;
                if (elapsed > DAMAGE_NUMBER_TICKS || count >= MAX_DAMAGE_NUMBERS) continue;
                GetView(_damageNumberViews, _damageNumberPrefab, count++).Apply(number, elapsed, origin);
            }
            HideFrom(_damageNumberViews, count);
        }

        /// <summary>새 시뮬레이션의 맵을 다시 만들고 피해 감지 기준값을 현재 값으로 맞춘다.</summary>
        private void BeginSimulation(RuneSimulation sim)
        {
            _observedSimulation = sim;
            if (_additionalEnemyPrefabs.Count == 0)
            foreach (EnemyView prefab in Resources.LoadAll<EnemyView>("RuneCode/enemies"))
                { _additionalEnemyPrefabs.Add(prefab.EnemyId, prefab); _additionalEnemyViews.Add(prefab.EnemyId, new List<EnemyView>()); _additionalEnemyCounts.Add(prefab.EnemyId, 0); }
            _lastDamage = sim.TotalDamage;
            _lastStatusDamage = sim.StatusDamage;
            _lastPersistDamage = sim.PersistDamage;
            _lastHp = sim.Player.Hp;
            BuildMap(sim.Map, sim.MapCenter);
        }

        /// <summary>
        /// 적에게 준 직접 피해나 플레이어 체력 감소가 있으면 설정에 따라 카메라를 흔들고 히트 스톱을 시작한다.
        /// 상태 이상 피해(화염)나 잔류(Persist) 피해만으로는 카메라가 흔들리지 않고 히트 스톱도 시작하지 않는다.
        /// 히트 스톱은 시작 후 재발동 간격 동안 다시 시작하지 않는다.
        /// </summary>
        private void DetectImpact(RuneSimulation sim)
        {
            bool isHpLost = sim.Player.Hp < _lastHp;
            double directDelta = (sim.TotalDamage - _lastDamage) - (sim.StatusDamage - _lastStatusDamage) - (sim.PersistDamage - _lastPersistDamage);
            bool isImpact = directDelta > DAMAGE_EPSILON || isHpLost;
            if (isImpact && (_isShakeEnabled?.Invoke() ?? false)) _camera.Shake(SHAKE_SECONDS);
            if (isImpact && Time.unscaledTime >= _hitStopReadyAt)
            {
                _hitStopUntil = Time.unscaledTime + HIT_STOP_SECONDS;
                _hitStopReadyAt = Time.unscaledTime + HIT_STOP_COOLDOWN_SECONDS;
            }
            _lastDamage = sim.TotalDamage;
            _lastStatusDamage = sim.StatusDamage;
            _lastPersistDamage = sim.PersistDamage;
            _lastHp = sim.Player.Hp;
        }

        /// <summary>
        /// 기존 맵 표시를 지우고 벽 타일을 줄 단위로 묶어 사각형과 위쪽 경계선으로, 바닥 전체에 일정 간격의 격자선을 만든다.
        /// 맵은 전투 중 바뀌지 않으므로 시뮬레이션마다 한 번만 만든다.
        /// </summary>
        private void BuildMap(MissionMap map, SimVector origin)
        {
            if (_mapRoot != null) Destroy(_mapRoot.gameObject);
            _mapRoot = new GameObject("Map").transform;
            _mapRoot.SetParent(transform, false);
            int size = map.TileSize;
            for (float x = _gridSpacing; x < map.Width; x += _gridSpacing)
                AddLine(origin, new SimVector(x, 0), new SimVector(x, map.Height), GRID_LINE_WIDTH, GridColor, GRID_ORDER);
            for (float y = _gridSpacing; y < map.Height; y += _gridSpacing)
                AddLine(origin, new SimVector(0, y), new SimVector(map.Width, y), GRID_LINE_WIDTH, GridColor, GRID_ORDER);
            for (int row = 0; row < map.Tiles.Length; row++)
            {
                string tiles = map.Tiles[row];
                for (int start = 0; start < tiles.Length; start++)
                {
                    if (tiles[start] != '#') continue;
                    int end = start;
                    while (end + 1 < tiles.Length && tiles[end + 1] == '#') end++;
                    double left = start * size, right = (end + 1) * size, top = row * size;
                    AddLine(origin, new SimVector(left, top + size * 0.5), new SimVector(right, top + size * 0.5), size, WallColor, WALL_ORDER);
                    AddLine(origin, new SimVector(left, top), new SimVector(right, top), WALL_EDGE_WIDTH, WallEdgeColor, WALL_EDGE_ORDER);
                    start = end;
                }
            }
        }

        /// <summary>맵 표시용 정사각 스프라이트를 시뮬레이션 두 점 사이의 지정 두께(px) 선분으로 만들어 맵 루트에 둔다.</summary>
        private void AddLine(SimVector origin, SimVector from, SimVector to, float widthPixels, Color color, int order)
        {
            var line = new GameObject("Line", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            line.transform.SetParent(_mapRoot, false);
            line.sprite = _squareSprite;
            line.sharedMaterial = _spriteMaterial;
            line.sortingOrder = order;
            MissionWorldSpace.PlaceLine(line, MissionWorldSpace.ToWorld(from, origin) * MissionWorldSpace.PIXELS_PER_UNIT,
                MissionWorldSpace.ToWorld(to, origin) * MissionWorldSpace.PIXELS_PER_UNIT, widthPixels, color);
        }

        /// <summary>목록의 index번째 뷰를 반환하며 없으면 Prefab에서 만들고, 숨겨져 있으면 다시 표시한다.</summary>
        private T GetView<T>(List<T> views, T prefab, int index) where T : Component
        {
            if (index == views.Count) views.Add(Instantiate(prefab, transform));
            T view = views[index];
            MissionWorldSpace.SetVisible(view, true);
            return view;
        }

        /// <summary>목록에서 count번째 이후 뷰를 숨긴다.</summary>
        private static void HideFrom<T>(List<T> views, int count) where T : Component
        {
            for (int i = count; i < views.Count; i++) MissionWorldSpace.SetVisible(views[i], false);
        }
    }
}
