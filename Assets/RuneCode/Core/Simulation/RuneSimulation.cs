using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using UnityEngine;

namespace RuneCode
{
    public sealed class RuneSimulation
    {
        public const int TICK_RATE = 60;
        private const double STEP_SECONDS = 1.0 / TICK_RATE;
        private const double DEGREES_TO_RADIANS = Math.PI / 180;
        private readonly BalanceData _balance;
        private readonly EnemyCatalog _enemyCatalog;
        private readonly SectorDefinition _sector;
        private readonly GovernorDefinition _governor;
        private readonly IncrementalDefinition _incremental;
        private readonly SpellRuntime _spell;
        private readonly bool _isMission;
        private readonly int _stageNumber;
        private readonly double _battleDuration;
        private readonly double _maxHp;
        private readonly double _maxEnergy;
        private readonly double _energyRegen;
        private readonly List<SimulationEnemy> _enemies = new List<SimulationEnemy>();
        private readonly List<SimulationProjectile> _enemyProjectiles = new List<SimulationProjectile>();
        private readonly List<FragmentOrb> _orbs = new List<FragmentOrb>();
        private readonly List<DamageNumber> _damageNumbers = new List<DamageNumber>();
        private readonly List<NodeExecutionEvent> _nodeEvents = new List<NodeExecutionEvent>();
        private readonly List<PendingEnemyShot> _pendingEnemyShots = new List<PendingEnemyShot>();
        private readonly Queue<DamageSample> _damageWindow = new Queue<DamageSample>();
        private readonly Dictionary<string, int> _killCounts = new Dictionary<string, int>();
        private readonly int _initialSeed;
        private uint _rngState;
        private SimulationPlayer _player;
        private MissionMap _map;
        private MissionStage _stage;
        private int _tick;
        private int _nextEntityId;
        private int _nextSpawnTick;
        private int _spawnedEnemies;
        private int _killCount;
        private int _collectedFragments;
        private double _totalDamage;
        private double _rollingDamage;
        private double _energySpent;
        private int _peakProjectiles;
        private int _nodeExecutionCount;
        private string _scenario;
        private bool _debugInvulnerable;
        public int Tick => _tick;
        public double Time => _tick * STEP_SECONDS;
        public SimulationPlayer Player => _player;
        public SpellRuntime Spell => _spell;
        public IReadOnlyList<SimulationEnemy> Enemies => _enemies;
        public IReadOnlyList<SimulationProjectile> EnemyProjectiles => _enemyProjectiles;
        public IReadOnlyList<FragmentOrb> Orbs => _orbs;
        public IReadOnlyList<DamageNumber> DamageNumbers => _damageNumbers;
        public IReadOnlyList<NodeExecutionEvent> NodeEvents => _nodeEvents;
        public IReadOnlyDictionary<string, int> KillCounts => _killCounts;
        public MissionMap Map => _map;
        public MissionStage Stage => _stage;
        public bool Completed => _stage == MissionStage.Cleared;
        public bool IsDead => _stage == MissionStage.Dead;
        public bool IsTerminal => _stage == MissionStage.Terminal;
        public bool IsTimedBattle => _isMission;
        public int StageNumber => _stageNumber;
        public double BattleDuration => _battleDuration;
        public double RemainingTime => Math.Max(0, _battleDuration - Time);
        public double EnergyRegen => _energyRegen;
        public int SpawnedEnemies => _spawnedEnemies;
        public int KillCount => _killCount;
        public int EnemyLimit => _balance.Limits.MaxEnemies;
        public int RoomIndex => _stageNumber - 1;
        public string RoomName => _isMission ? GameData.L("incremental.stage") + " " + _stageNumber : GameData.L("scenario." + _scenario);
        public int CollectedFragments => _collectedFragments;
        public int Fragments => _collectedFragments;
        public int EarnedFragments
        {
            get { int total = _collectedFragments; foreach (FragmentOrb orb in _orbs) total += orb.Amount; return total; }
        }
        public int SettlementFragments => IsDead ? (int)Math.Round(_collectedFragments * _balance.Economy.DeathRetention, MidpointRounding.AwayFromZero) : _collectedFragments;
        public double TotalDamage => _totalDamage;
        public double RollingDps => _rollingDamage / Math.Min(5, Math.Max(STEP_SECONDS, Time));
        public double EnergySpent => _energySpent;
        public int PeakProjectiles => _peakProjectiles;
        public int NodeExecutionCount => _nodeExecutionCount;
        public bool DebugInvulnerable => _debugInvulnerable;

        /// <summary>명시적으로 열린 디버그 기능에서 플레이어 무적 상태를 변경한다.</summary>
        public void SetDebugInvulnerable(bool isInvulnerable) { _debugInvulnerable = isInvulnerable; }

        /// <summary>검증된 데이터, 시드, 시간제 스테이지와 영구 능력치로 독립된 전투 또는 시험 도크를 생성한다. 마나풀 값이 0 이하이면 설정 기본값을 쓴다.</summary>
        public RuneSimulation(int seed = 1, bool isMission = false, double maxHp = 0, double maxEnergy = 0, int stageNumber = 1, double battleDuration = 0, double energyRegen = 0)
        {
            GameData.Load();
            _balance = GameData.Balance;
            _enemyCatalog = EnemyCatalog.FromJson(LoadJson("enemies"));
            _sector = SectorDefinition.FromJson(LoadJson("sector1"), _enemyCatalog);
            _governor = GovernorDefinition.FromJson(LoadJson("governor"));
            _incremental = IncrementalDefinition.FromJson(LoadJson("incremental"), _enemyCatalog, _sector.TileSize);
            _spell = new SpellRuntime(_balance.Spell, _sector.TileSize, OnNodePaid, OnProjectileHit);
            _isMission = isMission; _initialSeed = seed; _rngState = (uint)seed; _maxHp = maxHp > 0 ? maxHp : _balance.Player.Hp; _maxEnergy = maxEnergy > 0 ? maxEnergy : _balance.Spell.ManaMax;
            _stageNumber = Math.Max(1, stageNumber); _battleDuration = isMission ? (battleDuration > 0 ? battleDuration : _incremental.BaseDuration) : 0;
            _energyRegen = energyRegen > 0 ? energyRegen : _balance.Spell.ManaRegen;
            if (isMission) BeginTimedBattle();
            else ResetBench("dummy_single");
        }

        /// <summary>시간제 스테이지의 공용 JSON 설정을 읽어 앱의 기본 제한시간과 스테이지 표시에 제공한다.</summary>
        public static IncrementalDefinition LoadIncrementalDefinition()
        {
            GameData.Load();
            EnemyCatalog enemies = EnemyCatalog.FromJson(LoadJson("enemies"));
            SectorDefinition sector = SectorDefinition.FromJson(LoadJson("sector1"), enemies);
            return IncrementalDefinition.FromJson(LoadJson("incremental"), enemies, sector.TileSize);
        }

        /// <summary>Resources에 포함된 JSON 텍스트를 읽고 누락된 필수 데이터 오류를 보고한다.</summary>
        private static string LoadJson(string name)
        { TextAsset asset = Resources.Load<TextAsset>("RuneCode/" + name); if (asset == null) throw new FormatException("누락된 룬 코드 데이터: " + name); return asset.text; }

        /// <summary>실행 그래프를 바꾸고 진행 중인 토큰, 투사체, 합류 대기를 모두 지운다. 마나풀 잔량은 바꾸지 않는다.</summary>
        public void SetProgram(SpellProgram program) { _spell.SetProgram(program); }

        /// <summary>토큰, 투사체, 합류 대기, 발사 요청, 소멸 기록을 모두 지운다. 편집 모드 진입 때 호출하며 마나는 돌려주지 않는다.</summary>
        public void ClearSpellState() { _spell.Clear(); }

        /// <summary>전투나 시험 도크 상태에서 현재 조준 방향으로 시전을 한 번 시도하고 결과를 반환한다. 성공하면 시전 노드 비용을 에너지 사용량에 더하고 노드 기록을 남긴다.</summary>
        public CastResult TryCast()
        {
            if (_stage != MissionStage.Bench && _stage != MissionStage.Combat) return CastResult.Invalid;
            CastResult result = _spell.TryCast(_player, _tick);
            if (result == CastResult.Success)
            {
                SpellProgram program = _spell.Program;
                _energySpent += program.GetLoad(program.CastNode);
                RecordNode(program.GetNodeId(program.CastNode));
            }
            return result;
        }

        /// <summary>입력 조준 방향을 플레이어에 반영한 뒤 시전을 한 번 시도하고 결과를 반환한다.</summary>
        public CastResult TryCast(SimVector aimDirection)
        {
            _player.Aim(aimDirection);
            return TryCast();
        }

        /// <summary>입력, 마나풀 회복, 유입, 시전, 적과 투사체, 토큰 처리, 발사 묶음, 제한시간 종료를 정확히 한 60Hz 틱만큼 갱신한다. 순서는 계획서 3.6을 따른다.</summary>
        public void Step(SimulationInput input)
        {
            if (Completed || IsDead) return;
            _player.Aim(input.AimDirection);
            _player.Advance(STEP_SECONDS, Time, _energyRegen);
            MovePlayer(input);
            if (_isMission) AdvanceEnemyStreaming();
            if (input.CastA) TryCast();
            AdvanceEnemies();
            RunEnemyShots();
            _spell.AdvanceProjectiles(_tick, STEP_SECONDS, _map, _enemies);
            _spell.ProcessTokens(_tick, STEP_SECONDS, _player.Position, _enemies);
            _spell.FlushFireRequests(_tick, _player.Position, _player.AimDirection, _enemies);
            _peakProjectiles = Math.Max(_peakProjectiles, _spell.Projectiles.Count);
            AdvanceHostileProjectiles();
            CleanupEnemies();
            CollectNearbyOrbs();
            if (_isMission && _player.Hp <= 0) { _stage = MissionStage.Dead; CollectAllOrbs(); CancelCombat(); }
            else if (!_isMission && _scenario == "adapt_loop" && _enemies.Count == 0) SpawnBenchDummy(new SimVector(_balance.Sim.BenchDummyX, _balance.Sim.BenchDummyY), _balance.Sim.BenchHp);
            _tick++;
            if (_isMission && !IsDead && Time + 0.000001 >= _battleDuration) CompleteTimedBattle();
            TrimFeedback();
        }

        /// <summary>시간제 전투에는 터미널 전환이 없으므로 진행 요청을 거부한다.</summary>
        public bool InteractTerminal() => false;

        /// <summary>터미널의 다음 방 진행 성공 여부를 반환한다.</summary>
        public bool TryContinue() => InteractTerminal();

        /// <summary>독립 시험 도크의 시드, 상태, 통계 및 선택 시나리오 적을 초기화한다.</summary>
        public void ResetBench(string scenario)
        {
            if (_isMission) throw new InvalidOperationException("미션은 시험 도크로 초기화할 수 없습니다.");
            if (scenario != "dummy_single" && scenario != "dummy_line" && scenario != "dummy_swarm" && scenario != "aegis" && scenario != "adapt_loop") throw new ArgumentException("알 수 없는 시험 시나리오입니다.", nameof(scenario));
            _scenario = scenario; _stage = MissionStage.Bench; _tick = 0; _nextEntityId = 0; _rngState = (uint)_initialSeed;
            _enemies.Clear(); _orbs.Clear(); _damageNumbers.Clear(); _nodeEvents.Clear(); _damageWindow.Clear(); _killCounts.Clear(); CancelCombat();
            _totalDamage = 0; _rollingDamage = 0; _energySpent = 0; _peakProjectiles = 0; _nodeExecutionCount = 0; _collectedFragments = 0; _killCount = 0; _spawnedEnemies = 0;
            SimulationBalance bench = _balance.Sim;
            _map = new MissionMap(_sector.Rooms[0].Tiles, _sector.TileSize); _player = new SimulationPlayer(_maxHp, _maxEnergy, new SimVector(bench.BenchPlayerX, bench.BenchPlayerY));
            if (scenario == "dummy_line") for (int i = 0; i < bench.BenchLineCount; i++) SpawnBenchDummy(new SimVector(bench.BenchLineStartX + i * bench.BenchLineGap, bench.BenchDummyY), bench.BenchHp);
            else if (scenario == "dummy_swarm")
                for (int i = 0; i < bench.BenchSwarmColumns * bench.BenchSwarmRows; i++) SpawnEnemy("enemy.scout", new SimVector(bench.BenchSwarmX + (i % bench.BenchSwarmColumns) * bench.BenchSwarmGapX, bench.BenchSwarmY + (i / bench.BenchSwarmColumns) * bench.BenchSwarmGapY), false, bench.BenchHp);
            else if (scenario == "aegis") SpawnEnemy("enemy.aegis", new SimVector(bench.BenchDummyX, bench.BenchDummyY), true, bench.BenchHp);
            else SpawnBenchDummy(new SimVector(bench.BenchDummyX, bench.BenchDummyY), bench.BenchHp);
        }

        /// <summary>디버그 기능에서 지정한 종류의 적을 이동 가능한 좌표에 추가한다.</summary>
        public SimulationEnemy DebugSpawn(string id) => SpawnEnemy(id, _map.NearestFree(_player.Position + _player.AimDirection * 260, 24), false, 0);

        /// <summary>디버그 기능에서 현재 적들을 처치하고 보상을 회수하되 제한시간과 다음 적 유입은 유지한다.</summary>
        public void DebugDefeatRoom()
        { if (_isMission && _stage != MissionStage.Combat) return; foreach (SimulationEnemy enemy in _enemies) enemy.Hurt(enemy.Hp, Time); CleanupEnemies(); if (_isMission) CollectAllOrbs(); }

        /// <summary>현재 시뮬레이션의 핵심 상태를 안정적으로 해시하여 동일 입력 실행의 결정성을 비교한다.</summary>
        public string StateHash()
        {
            var state = new StringBuilder();
            state.Append(_tick).Append('|').Append(_rngState).Append('|').Append(_nextEntityId).Append('|').Append(_stage).Append('|').Append(_stageNumber).Append('|').Append(_nextSpawnTick).Append('|').Append(_spawnedEnemies).Append('|').Append(_killCount).Append('|').Append(_scenario).Append('|').Append(_debugInvulnerable);
            AppendNumber(state, _battleDuration); AppendNumber(state, _energyRegen);
            _player.WriteState(state);
            foreach (SimulationEnemy enemy in _enemies) enemy.WriteState(state);
            _spell.WriteState(state);
            state.Append('|').Append(_spell.Program?.Signature);
            foreach (SimulationProjectile projectile in _enemyProjectiles) projectile.WriteState(state);
            foreach (FragmentOrb orb in _orbs) { AppendNumber(state, orb.Position.X); AppendNumber(state, orb.Position.Y); state.Append(orb.Amount); }
            foreach (PendingEnemyShot shot in _pendingEnemyShots) { state.Append(shot.Tick).Append('|').Append(shot.Owner.Id); AppendNumber(state, shot.Direction.X); AppendNumber(state, shot.Direction.Y); AppendNumber(state, shot.SpeedMultiplier); }
            AppendNumber(state, _totalDamage); state.Append(_collectedFragments).Append(_nodeExecutionCount);
            uint hash = 2166136261;
            foreach (char value in state.ToString()) { hash ^= value; hash *= 16777619; }
            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }

        /// <summary>불변 문화권 숫자 표기를 상태 해시 버퍼에 추가한다.</summary>
        private static void AppendNumber(StringBuilder state, double value) { state.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('|'); }

        /// <summary>대시와 일반 이동을 벽 충돌에 맞춰 처리한다.</summary>
        private void MovePlayer(SimulationInput input)
        {
            SimVector move = input.Movement.Length > 1 ? input.Movement.Normalized() : input.Movement;
            if (input.Dash && _stage != MissionStage.Terminal) _player.StartDash(move.LengthSquared > 0.000001 ? move : _player.AimDirection, _balance.Player.DashSeconds, _balance.Player.DashCooldown, Time);
            SimVector displacement = _player.DashRemaining > 0 ? _player.GetDashDirection() * (_balance.Player.DashDistance / _balance.Player.DashSeconds * Math.Min(STEP_SECONDS, _player.DashRemaining)) : move * (_balance.Player.Speed * STEP_SECONDS);
            _player.Move(_map.Move(_player.Position, displacement, _sector.PlayerRadius));
        }

        /// <summary>플레이어가 토큰 노드 비용을 지불했을 때 해당 노드의 실행 기록을 남긴다.</summary>
        private void OnNodePaid(int node) { RecordNode(_spell.Program.GetNodeId(node)); }

        /// <summary>투사체가 적에게 맞았을 때 경감 공식으로 실피해를 적용하고 피해 숫자와 DPS 기록을 남긴다. 패치 중인 보스는 무적이라 피해를 받지 않는다.</summary>
        private void OnProjectileHit(SimulationEnemy enemy, SpellProjectile projectile)
        {
            if (!enemy.IsAlive || enemy.IsPatching) return;
            double actual = enemy.TakeSpellDamage(projectile.Power, Time);
            _damageNumbers.Add(new DamageNumber(enemy.Position, actual, "raw", _tick));
            if (actual > 0) { _totalDamage += actual; _rollingDamage += actual; _damageWindow.Enqueue(new DamageSample(_tick, actual)); }
            if (enemy.Kind == "boss.governor" && enemy.IsAlive) CheckBossPatch(enemy);
        }

        /// <summary>적의 접근, 접촉, 예고 사격, 돌진, 방패 및 거버너 페이즈 패턴을 진행한다.</summary>
        private void AdvanceEnemies()
        {
            int initialCount = _enemies.Count;
            for (int i = 0; i < initialCount; i++)
            {
                SimulationEnemy enemy = _enemies[i]; if (!enemy.IsAlive) continue;
                enemy.Observe(Time);
                if (enemy.IsDummy || enemy.IsFrozen) continue;
                SimVector offset = _player.Position - enemy.Position; SimVector direction = offset.Normalized();
                if (enemy.Kind == "boss.governor") { AdvanceBoss(enemy, direction); continue; }
                EnemyDefinition definition = enemy.Definition;
                if (enemy.Kind == "enemy.relay") { if (_isMission) MoveEnemy(enemy, direction, _incremental.StationaryAdvanceSpeed); continue; }
                if (enemy.Kind == "enemy.sentry")
                {
                    if (_isMission) MoveEnemy(enemy, direction, _incremental.StationaryAdvanceSpeed); else enemy.Move(enemy.Position, direction);
                    if (enemy.WarningUntil > 0 && Time + 0.000001 >= enemy.WarningUntil)
                    { ShootBurst(enemy, enemy.AttackDirection, 1); enemy.WarningUntil = 0; enemy.AttackAt = Time + definition.AttackInterval - definition.WarningSeconds; }
                    else if (enemy.WarningUntil == 0 && Time + 0.000001 >= enemy.AttackAt)
                    { enemy.AttackDirection = direction; enemy.WarningUntil = Time + definition.WarningSeconds; }
                    continue;
                }
                if (enemy.Kind == "enemy.hunter")
                {
                    if (Time < enemy.DashUntil) { MoveEnemy(enemy, enemy.AttackDirection, definition.DashSpeed); HurtByContact(enemy); continue; }
                    if (enemy.WarningUntil > 0)
                    { if (Time + 0.000001 >= enemy.WarningUntil) { enemy.WarningUntil = 0; enemy.DashUntil = Time + definition.DashSeconds; enemy.AttackAt = Time + definition.AttackInterval; } continue; }
                    if (offset.Length < definition.AttackRange && Time >= enemy.AttackAt)
                    { enemy.WarningUntil = Time + definition.WarningSeconds; enemy.AttackDirection = direction; continue; }
                    MoveEnemy(enemy, direction, definition.Speed);
                    continue;
                }
                MoveEnemy(enemy, direction, definition.Speed);
                if (Time >= enemy.AttackAt && HurtByContact(enemy)) enemy.AttackAt = Time + definition.AttackInterval;
            }
        }

        /// <summary>냉기 감속을 반영하여 적을 벽 충돌 가능한 방향으로 이동시킨다.</summary>
        private void MoveEnemy(SimulationEnemy enemy, SimVector direction, double speed)
        { double slow = Math.Max(0, 1 - _balance.Combat.ChillSlow * enemy.ChillStacks); enemy.Move(_map.Move(enemy.Position, direction * (speed * slow * STEP_SECONDS), enemy.Radius), direction); }

        /// <summary>적과 플레이어가 겹치면 피해를 적용하고 접촉 여부를 반환한다.</summary>
        private bool HurtByContact(SimulationEnemy enemy)
        { if (!_isMission || SimVector.Distance(enemy.Position, _player.Position) > enemy.Radius + _sector.PlayerRadius) return false; HurtPlayer(enemy.Damage); return true; }

        /// <summary>플레이어 피해에 디버그 무적과 기본 피격 무적을 반영한다.</summary>
        private void HurtPlayer(double damage)
        { if (_debugInvulnerable || !_isMission) return; double actual = _player.Hurt(damage, Time, _balance.Player.HurtInvulnerability); if (actual > 0) _damageNumbers.Add(new DamageNumber(_player.Position, actual, "player", _tick)); }

        /// <summary>적 공격 설정의 탄환을 부채꼴로 발사한다.</summary>
        private void ShootFan(SimulationEnemy enemy, SimVector direction, int count, double speedMultiplier)
        {
            EnemyDefinition definition = enemy.Definition;
            for (int i = 0; i < count; i++)
            { double spread = count == 1 ? 0 : (i / (double)(count - 1) * 2 - 1) * definition.SpreadDegrees * DEGREES_TO_RADIANS; SimVector aim = direction.Rotated(spread); _enemyProjectiles.Add(new SimulationProjectile(enemy.Position + aim * (enemy.Radius + _balance.Sim.WandOffset), aim, definition.ProjectileSpeed * speedMultiplier, enemy.Damage, definition.ProjectileRadius, definition.ProjectileLifetime)); }
        }

        /// <summary>예고된 조준 방향에 첫 탄을 발사하고 데이터 간격에 맞춰 나머지 2발을 예약한다.</summary>
        private void ShootBurst(SimulationEnemy enemy, SimVector direction, double speedMultiplier)
        {
            ShootFan(enemy, direction, 1, speedMultiplier);
            for (int i = 1; i < 3; i++) _pendingEnemyShots.Add(new PendingEnemyShot(_tick + Math.Max(1, (int)Math.Round(i * enemy.Definition.BurstInterval * TICK_RATE)), enemy, direction, speedMultiplier));
        }

        /// <summary>예약 시점이 된 연사 탄환을 생존하며 행동 가능한 적의 현재 위치에서 발사한다.</summary>
        private void RunEnemyShots()
        {
            for (int i = 0; i < _pendingEnemyShots.Count;)
            {
                PendingEnemyShot shot = _pendingEnemyShots[i];
                if (!shot.Owner.IsAlive || shot.Owner.IsPatching || shot.Owner.IsFrozen) { _pendingEnemyShots.RemoveAt(i); continue; }
                if (shot.Tick > _tick) { i++; continue; }
                _pendingEnemyShots.RemoveAt(i); ShootFan(shot.Owner, shot.Direction, 1, shot.SpeedMultiplier);
            }
        }

        /// <summary>거버너의 이동, 방사 사격, 조준 사격, 예고 장판 및 증원 주기를 페이즈에 맞춰 처리한다.</summary>
        private void AdvanceBoss(SimulationEnemy boss, SimVector direction)
        {
            CheckBossPatch(boss); if (boss.IsPatching) return;
            double multiplier = boss.Phase == 3 ? _governor.PhaseThreeMultiplier : 1;
            if (SimVector.Distance(boss.Position, _player.Position) > _governor.PreferredDistance) MoveEnemy(boss, direction, boss.Definition.Speed * multiplier);
            else boss.Move(boss.Position, direction);
            if (Time + 0.000001 >= boss.AttackAt)
            {
                if (boss.Phase == 1 || boss.Phase == 3)
                    for (int i = 0; i < _governor.RadialCount; i++)
                    { double angle = i * 2 * Math.PI / _governor.RadialCount; ShootFan(boss, new SimVector(Math.Cos(angle), Math.Sin(angle)), 1, multiplier); }
                if (boss.Phase >= 2) ShootBurst(boss, direction, multiplier);
                boss.AttackAt = Time + (boss.Phase == 2 ? _governor.AimInterval : _governor.RadialInterval) / multiplier;
            }
            if (boss.Phase >= 2 && Time >= boss.HazardAt)
            { _enemyProjectiles.Add(new SimulationProjectile(_player.Position, SimVector.Zero, 0, _governor.HazardDamage * boss.DamageMultiplier, _governor.HazardRadius, _governor.HazardLifetime + _governor.HazardWarning, true, _governor.HazardWarning)); boss.HazardAt = Time + _governor.HazardInterval / multiplier; }
            if (Time >= boss.ReinforcementAt)
            {
                for (int i = 0; i < _governor.ReinforcementCount; i++)
                {
                    SimVector position = _isMission ? new SimVector(_incremental.SpawnX, _incremental.SpawnMinY + NextRandom() * (_incremental.SpawnMaxY - _incremental.SpawnMinY)) : _map.GetPoint((char)('1' + i * 2));
                    SpawnEnemy("enemy.scout", position, false, 0);
                }
                boss.ReinforcementAt = Time + _governor.ReinforcementInterval;
            }
            HurtByContact(boss);
        }

        /// <summary>거버너 체력 문턱을 넘으면 다음 페이즈로 올리고 패치 무적 시간과 공격 예약을 설정한다.</summary>
        private void CheckBossPatch(SimulationEnemy boss)
        {
            if (boss.IsPatching) return;
            int next = boss.Phase == 1 && boss.Hp / boss.MaxHp <= _governor.PhaseTwoThreshold ? 2 : boss.Phase == 2 && boss.Hp / boss.MaxHp <= _governor.PhaseThreeThreshold ? 3 : boss.Phase;
            if (next == boss.Phase) return;
            boss.Patch(next, Time, _governor.PatchSeconds); boss.Observe(Time);
            boss.AttackAt = Time + _governor.PatchSeconds + boss.Definition.WarningSeconds;
        }

        /// <summary>적 탄환과 위험 장판의 수명, 벽 충돌 및 플레이어 피격을 처리한다.</summary>
        private void AdvanceHostileProjectiles()
        {
            for (int i = _enemyProjectiles.Count - 1; i >= 0; i--)
            {
                SimulationProjectile projectile = _enemyProjectiles[i]; projectile.Advance(STEP_SECONDS);
                if (projectile.Age >= projectile.Lifetime || (!projectile.IsHazard && _map.IsWall(projectile.Position))) { _enemyProjectiles.RemoveAt(i); continue; }
                if (!projectile.IsWarning && SimVector.Distance(projectile.Position, _player.Position) <= projectile.Radius + _sector.PlayerRadius)
                { HurtPlayer(projectile.Damage); if (!projectile.IsHazard) _enemyProjectiles.RemoveAt(i); }
            }
        }

        /// <summary>처치된 적만 제거하고 시간제 전투의 처치 통계와 조각 오브를 생성한다.</summary>
        private void CleanupEnemies()
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                SimulationEnemy enemy = _enemies[i]; if (enemy.IsAlive) continue;
                if (_isMission)
                { _orbs.Add(new FragmentOrb(enemy.Position, enemy.Reward)); _killCounts.TryGetValue(enemy.Kind, out int count); _killCounts[enemy.Kind] = count + 1; _killCount++; }
                _enemies.RemoveAt(i);
            }
        }

        /// <summary>플레이어의 자동 흡수 반경 내 조각 오브를 회수한다.</summary>
        private void CollectNearbyOrbs()
        { for (int i = _orbs.Count - 1; i >= 0; i--) if (SimVector.Distance(_player.Position, _orbs[i].Position) <= _balance.Economy.OrbAbsorbRadius) { _collectedFragments += _orbs[i].Amount; _orbs.RemoveAt(i); } }

        /// <summary>전투 종료 시 남아 있는 모든 처치 조각 오브를 회수한다.</summary>
        private void CollectAllOrbs() { foreach (FragmentOrb orb in _orbs) _collectedFragments += orb.Amount; _orbs.Clear(); }

        /// <summary>제한시간이 끝나면 생존 적과 무관하게 처치 보상을 회수하고 전투 실행을 종료한다.</summary>
        private void CompleteTimedBattle()
        {
            if (_stage != MissionStage.Combat) return;
            CollectAllOrbs(); _stage = MissionStage.Cleared; CancelCombat();
        }

        /// <summary>왼쪽 플레이어와 열린 아레나를 초기화하고 첫 오른쪽 적 유입 및 다음 생성 틱을 설정한다.</summary>
        private void BeginTimedBattle()
        {
            _stage = MissionStage.Combat;
            _map = new MissionMap(_incremental.Tiles, _sector.TileSize);
            _player = new SimulationPlayer(_maxHp, _maxEnergy, _map.NearestFree(new SimVector(_incremental.PlayerX, _incremental.PlayerY), _sector.PlayerRadius));
            SpawnIncomingEnemy(); _nextSpawnTick = SpawnIntervalTicks();
        }

        /// <summary>스테이지 진행과 시간 경과에 따른 유입 주기를 유지하면서 오른쪽에 새 적을 생성한다.</summary>
        private void AdvanceEnemyStreaming()
        {
            if (_tick < _nextSpawnTick) return;
            if (_enemies.Count < EnemyLimit) SpawnIncomingEnemy();
            _nextSpawnTick = _tick + SpawnIntervalTicks();
        }

        /// <summary>스테이지 번호와 시간 비율로 유입 간격을 줄이되 JSON의 최소 간격을 지킨다.</summary>
        private int SpawnIntervalTicks()
        {
            double progress = Math.Max(0, Math.Min(1, Time / _battleDuration));
            double seconds = _incremental.BaseSpawnInterval / (1 + (_stageNumber - 1) * _incremental.IntervalStageScale) * (1 - progress * _incremental.IntraStageRamp);
            return Math.Max(1, (int)Math.Round(Math.Max(_incremental.MinSpawnInterval, seconds) * TICK_RATE));
        }

        /// <summary>현재 스테이지에서 해금된 적 편성을 가중 난수로 선택하고 오른쪽 가장자리에서 유입시킨다.</summary>
        private void SpawnIncomingEnemy()
        {
            double totalWeight = 0;
            foreach (IncrementalEnemyEntry entry in _incremental.Enemies) if (entry.FirstStage <= _stageNumber) totalWeight += entry.Weight;
            double choice = NextRandom() * totalWeight;
            foreach (IncrementalEnemyEntry entry in _incremental.Enemies)
            {
                if (entry.FirstStage > _stageNumber) continue;
                choice -= entry.Weight;
                if (choice > 0) continue;
                SimVector position = new SimVector(_incremental.SpawnX, _incremental.SpawnMinY + NextRandom() * (_incremental.SpawnMaxY - _incremental.SpawnMinY));
                SpawnEnemy(entry.EnemyId, position); return;
            }
        }

        /// <summary>무반격 더미 적을 지정한 위치와 체력으로 생성한다.</summary>
        private SimulationEnemy SpawnBenchDummy(SimVector position, double hp) => SpawnEnemy("enemy.scout", position, true, hp);

        /// <summary>설정 ID와 개별 위치, 더미 여부 및 체력으로 적을 생성하고 상대 시간 공격 일정을 설정한다.</summary>
        public SimulationEnemy SpawnEnemy(string id, SimVector position, bool isDummy = false, double hp = 0)
        {
            if (Completed || IsDead || _enemies.Count >= EnemyLimit) return null;
            EnemyDefinition definition = _enemyCatalog.Get(id);
            double hpMultiplier = _isMission ? 1 + (_stageNumber - 1) * _incremental.HpStageScale : 1;
            double damageMultiplier = _isMission ? 1 + (_stageNumber - 1) * _incremental.DamageStageScale : 1;
            int reward = definition.Reward;
            if (_isMission) foreach (IncrementalEnemyEntry entry in _incremental.Enemies) if (entry.EnemyId == id) { reward = entry.Reward; break; }
            var enemy = new SimulationEnemy(++_nextEntityId, definition, _map.NearestFree(position, definition.Radius), isDummy, hp, _balance.Sim.HitFlashSeconds, hpMultiplier, damageMultiplier, reward);
            enemy.AttackAt = Time + definition.AttackInterval;
            if (id == "boss.governor") { enemy.ReinforcementAt = Time + _governor.ReinforcementInterval; enemy.HazardAt = Time + _governor.HazardInterval; }
            enemy.Observe(Time); _enemies.Add(enemy); if (_isMission) _spawnedEnemies++; return enemy;
        }

        /// <summary>방 전환, 사망 및 클리어 때 적 탄환, 예고 연사, 마법 토큰과 투사체를 모두 지운다.</summary>
        private void CancelCombat() { _spell.Clear(); _enemyProjectiles.Clear(); _pendingEnemyShots.Clear(); }

        /// <summary>노드 실행 틱을 기록하고 누적 실행 수를 증가시킨다.</summary>
        private void RecordNode(string nodeId) { _nodeEvents.Add(new NodeExecutionEvent(nodeId, _tick)); _nodeExecutionCount++; }

        /// <summary>표시 수명이 지난 피해 숫자, 노드 이벤트, 5초 DPS 샘플과 소멸 기록을 제거한다.</summary>
        private void TrimFeedback()
        {
            for (int i = _damageNumbers.Count - 1; i >= 0; i--) if (_tick - _damageNumbers[i].Tick > _balance.Sim.DamageNumberSeconds * TICK_RATE) _damageNumbers.RemoveAt(i);
            for (int i = _nodeEvents.Count - 1; i >= 0; i--) if (_tick - _nodeEvents[i].Tick > TICK_RATE) _nodeEvents.RemoveAt(i);
            while (_damageWindow.Count > 0 && _tick - _damageWindow.Peek().Tick > 5 * TICK_RATE) _rollingDamage -= _damageWindow.Dequeue().Amount;
            _spell.TrimEvents(_tick);
        }

        /// <summary>시드 기반 mulberry32 연산으로 0 이상 1 미만의 결정적 난수를 반환한다.</summary>
        private double NextRandom()
        {
            unchecked { _rngState += 0x6D2B79F5; uint value = _rngState; value = (value ^ (value >> 15)) * (value | 1); value ^= value + ((value ^ (value >> 7)) * (value | 61)); return (value ^ (value >> 14)) / 4294967296.0; }
        }

        private readonly struct DamageSample
        {
            private readonly int _tick;
            private readonly double _amount;
            public int Tick => _tick;
            public double Amount => _amount;
            /// <summary>피해 틱과 실제 적용량을 5초 롤링 DPS 계산용으로 보관한다.</summary>
            public DamageSample(int tick, double amount) { _tick = tick; _amount = amount; }
        }

        private readonly struct PendingEnemyShot
        {
            private readonly int _tick;
            private readonly SimulationEnemy _owner;
            private readonly SimVector _direction;
            private readonly double _speedMultiplier;
            public int Tick => _tick;
            public SimulationEnemy Owner => _owner;
            public SimVector Direction => _direction;
            public double SpeedMultiplier => _speedMultiplier;

            /// <summary>연사 탄환의 발사 틱, 소유 적, 방향 및 속도 배율을 보관한다.</summary>
            public PendingEnemyShot(int tick, SimulationEnemy owner, SimVector direction, double speedMultiplier) { _tick = tick; _owner = owner; _direction = direction; _speedMultiplier = speedMultiplier; }
        }
    }
}
