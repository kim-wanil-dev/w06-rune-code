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
        private const int STATUS_TYPE_COUNT = 4;
        private const int PLAYER_TARGET_ID = -1;
        private readonly BalanceData _balance;
        private readonly EnemyCatalog _enemyCatalog;
        private readonly SectorDefinition _sector;
        private readonly GovernorDefinition _governor;
        private readonly IncrementalDefinition _incremental;
        private readonly bool _isMission;
        private readonly int _stageNumber;
        private readonly double _battleDuration;
        private readonly double _maxHp;
        private readonly double _maxEnergy;
        private readonly double _energyRegen;
        private readonly List<SimulationEnemy> _enemies = new List<SimulationEnemy>();
        private readonly List<SimulationSpellEntity> _spellEntities = new List<SimulationSpellEntity>();
        private readonly List<SimulationProjectile> _enemyProjectiles = new List<SimulationProjectile>();
        private readonly List<FragmentOrb> _orbs = new List<FragmentOrb>();
        private readonly List<DamageNumber> _damageNumbers = new List<DamageNumber>();
        private readonly List<NodeExecutionEvent> _nodeEvents = new List<NodeExecutionEvent>();
        private readonly List<ScheduledExecution> _scheduled = new List<ScheduledExecution>();
        private readonly List<PendingEnemyShot> _pendingEnemyShots = new List<PendingEnemyShot>();
        private readonly List<EnemyStatusEffect> _enemyStatuses = new List<EnemyStatusEffect>();
        private readonly Dictionary<int, EnemyStatusEffect> _statusIndex = new Dictionary<int, EnemyStatusEffect>();
        private readonly Queue<DamageSample> _damageWindow = new Queue<DamageSample>();
        private readonly Dictionary<string, int> _killCounts = new Dictionary<string, int>();
        private readonly CompiledSpell[] _loadout = new CompiledSpell[3];
        private readonly List<string> _unlockedElements = new List<string> { "fire" };
        private readonly int _initialSeed;
        private uint _rngState;
        private SimulationPlayer _player;
        private MissionMap _map;
        private MissionStage _stage;
        private int _tick;
        private double _statusTime;
        private int _nextEntityId;
        private int _nextSpawnTick;
        private int _spawnedEnemies;
        private int _killCount;
        private int _actionBudgetTick = -1;
        private int _actionsThisTick;
        private int _droppedExecutions;
        private int _collectedFragments;
        private double _totalDamage;
        private double _rollingDamage;
        private double _energySpent;
        private int _peakSpellEntities;
        private int _nodeExecutionCount;
        private string _scenario;
        private bool _debugInvulnerable;
        private bool _isAreaBoxUpright = true;
        private string[] _lastPatchTags = Array.Empty<string>();
        public int Tick => _tick;
        public double Time => _tick * STEP_SECONDS;
        public SimulationPlayer Player => _player;
        public IReadOnlyList<SimulationEnemy> Enemies => _enemies;
        public IReadOnlyList<SimulationSpellEntity> SpellEntities => _spellEntities;
        public IReadOnlyList<SimulationProjectile> EnemyProjectiles => _enemyProjectiles;
        public IReadOnlyList<FragmentOrb> Orbs => _orbs;
        public IReadOnlyList<DamageNumber> DamageNumbers => _damageNumbers;
        public IReadOnlyList<NodeExecutionEvent> NodeEvents => _nodeEvents;
        public IReadOnlyDictionary<string, int> KillCounts => _killCounts;
        public AdaptationNet Adaptation { get; }
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
        public int DroppedExecutions => _droppedExecutions;
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
        public int PeakSpellEntities => _peakSpellEntities;
        public int NodeExecutionCount => _nodeExecutionCount;
        public IReadOnlyList<string> LastPatchTags => _lastPatchTags;
        public bool AdaptationEnabled => Adaptation.Enabled;
        public bool DebugInvulnerable => _debugInvulnerable;
        public bool IsAreaBoxUpright => _isAreaBoxUpright;

        /// <summary>
        /// 폭발·잔류·공전 사각형 판정을 월드 축에 고정할지(true) 개체 진행 방향으로 회전할지(false) 변경한다.
        /// 발사 사각형은 이 설정과 무관하게 항상 진행 방향을 따른다.
        /// </summary>
        public void SetAreaBoxUpright(bool isUpright) { _isAreaBoxUpright = isUpright; }

        /// <summary>시험 도크의 적응 학습 및 피해 감쇠 사용 여부를 변경한다.</summary>
        public void SetAdaptationEnabled(bool isEnabled) { Adaptation.SetEnabled(isEnabled); }

        /// <summary>명시적으로 열린 디버그 기능에서 플레이어 무적 상태를 변경한다.</summary>
        public void SetDebugInvulnerable(bool isInvulnerable) { _debugInvulnerable = isInvulnerable; }

        /// <summary>검증된 데이터, 시드, 시간제 스테이지와 영구 능력치로 독립된 전투 또는 시험 도크를 생성한다.</summary>
        public RuneSimulation(int seed = 1, bool isMission = false, double maxHp = 0, double maxEnergy = 0, int stageNumber = 1, double battleDuration = 0, double energyRegen = 0)
        {
            GameData.Load();
            _balance = GameData.Balance;
            _enemyCatalog = EnemyCatalog.FromJson(LoadJson("enemies"));
            _sector = SectorDefinition.FromJson(LoadJson("sector1"), _enemyCatalog);
            _governor = GovernorDefinition.FromJson(LoadJson("governor"));
            _incremental = IncrementalDefinition.FromJson(LoadJson("incremental"), _enemyCatalog, _sector.TileSize);
            _isMission = isMission; _initialSeed = seed; _rngState = (uint)seed; _maxHp = maxHp > 0 ? maxHp : _balance.Player.Hp; _maxEnergy = maxEnergy > 0 ? maxEnergy : _balance.Player.Energy;
            _stageNumber = Math.Max(1, stageNumber); _battleDuration = isMission ? (battleDuration > 0 ? battleDuration : _incremental.BaseDuration) : 0;
            _energyRegen = energyRegen > 0 ? energyRegen : _balance.Player.EnergyRegen;
            Adaptation = new AdaptationNet(_balance);
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

        /// <summary>전투에서는 하나의 마법, 시험 도크에서는 최대 3슬롯의 컴파일 마법을 다음 시전부터 사용한다.</summary>
        public void SetLoadout(IReadOnlyList<CompiledSpell> spells)
        { for (int i = 0; i < _loadout.Length; i++) _loadout[i] = i < spells.Count && (!_isMission || i == 0) ? spells[i] : null; }

        /// <summary>해금한 속성 태그를 노이즈의 무작위 후보로 설정하고 기본 화염 후보를 보존한다.</summary>
        public void SetUnlockedElements(IReadOnlyList<string> elements)
        {
            _unlockedElements.Clear();
            for (int i = 0; i < elements.Count; i++)
            { string tag = elements[i].StartsWith("elem.", StringComparison.Ordinal) ? elements[i].Substring(5) : elements[i]; if ((tag == "fire" || tag == "ice" || tag == "arc") && !_unlockedElements.Contains(tag)) _unlockedElements.Add(tag); }
            if (_unlockedElements.Count == 0) _unlockedElements.Add("fire");
        }

        /// <summary>
        /// 시전 가능 상태와 슬롯 쿨다운을 확인하고 루트 실행을 시작한다. 마나는 선지불하지 않고 실행 노드에 도달할 때마다 차감한다(백서 7.1).
        /// </summary>
        public bool TryCast(CompiledSpell spell, int slot = 0)
        {
            if (spell == null || slot < 0 || slot >= _loadout.Length || (_stage != MissionStage.Bench && _stage != MissionStage.Combat)) return false;
            if (_isMission && slot != 0) return false;
            if (!_player.TryStartCooldown(spell, slot)) return false;
            RecordNode(spell.CoreNodeId);
            string noiseElement = ContainsNoise(spell.Root) ? _unlockedElements[(int)(NextRandom() * _unlockedElements.Count)] : null;
            var context = new SpellContext(_player.Position + _player.AimDirection * (_sector.PlayerRadius + _balance.Sim.WandOffset), _player.AimDirection, null, false, noiseElement,
                caster: _player);
            Execute(spell.Root, context);
            return true;
        }

        /// <summary>선택 Trigger와 일치하는 슬롯 마법을 시전하고 성공 여부를 반환한다.</summary>
        private bool TryCastTriggered(CompiledSpell spell, string trigger, int slot)
        {
            return spell != null && spell.Trigger == trigger && TryCast(spell, slot);
        }

        /// <summary>명시한 방향으로 조준을 갱신한 뒤 슬롯 마법을 시전하고 성공 여부를 반환한다.</summary>
        public bool TryCast(CompiledSpell spell, SimVector aimDirection, int slot = 0) { _player.Aim(aimDirection); return TryCast(spell, slot); }

        /// <summary>입력, 오른쪽 적 유입, 전투, 적응 및 제한시간 종료를 정확히 한 60Hz 틱만큼 갱신한다.</summary>
        public void Step(SimulationInput input)
        {
            if (Completed || IsDead) return;
            bool wasDashing = _player.DashRemaining > 0.000001;
            _player.Aim(input.AimDirection);
            _player.Advance(STEP_SECONDS, Time, _energyRegen);
            bool startedDash = MovePlayer(input);
            if (startedDash) TryCastTrigger(SpellGrammar.TRIGGER_ON_DASH_START);
            else if (wasDashing && _player.DashRemaining <= 0.000001) TryCastTrigger(SpellGrammar.TRIGGER_ON_DASH_END);
            if (input.Movement.LengthSquared > 0.000001) TryCastTrigger(SpellGrammar.TRIGGER_ON_MOVE);
            if (_isMission) AdvanceEnemyStreaming();
            RunScheduled();
            if (input.CastA) TryCastTriggered(_loadout[0], SpellGrammar.TRIGGER_ON_ATTACK, 0);
            if (input.CastB) TryCastTriggered(_loadout[1], SpellGrammar.TRIGGER_ON_ATTACK, 1);
            if (input.CastC) TryCastTriggered(_loadout[2], SpellGrammar.TRIGGER_ON_ATTACK, 2);
            AdvanceStatuses();
            AdvanceEnemies();
            RunEnemyShots();
            AdvanceSpellEntities();
            AdvanceHostileProjectiles();
            CleanupEnemies();
            CollectNearbyOrbs();
            if (_isMission && _player.Hp <= 0) { _stage = MissionStage.Dead; CollectAllOrbs(); CancelCombat(); }
            else if (!_isMission && _scenario == "adapt_loop" && _enemies.Count == 0) SpawnBenchDummy(new SimVector(_balance.Sim.BenchDummyX, _balance.Sim.BenchDummyY), _balance.Sim.BenchHp);
            Adaptation.Advance(Time, Time + STEP_SECONDS);
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
            _enemyStatuses.Clear(); _statusIndex.Clear(); _statusTime = 0;
            _totalDamage = 0; _rollingDamage = 0; _energySpent = 0; _peakSpellEntities = 0; _nodeExecutionCount = 0; _collectedFragments = 0; _killCount = 0; _spawnedEnemies = 0; _actionBudgetTick = -1; _actionsThisTick = 0; _droppedExecutions = 0;
            SimulationBalance bench = _balance.Sim;
            _map = new MissionMap(_sector.Rooms[0].Tiles, _sector.TileSize); _player = new SimulationPlayer(_maxHp, _maxEnergy, new SimVector(bench.BenchPlayerX, bench.BenchPlayerY));
            Adaptation.Clear(); Adaptation.SetEnabled(scenario == "adapt_loop");
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
            state.Append('|').Append(_actionBudgetTick).Append('|').Append(_actionsThisTick).Append('|').Append(_droppedExecutions);
            AppendNumber(state, _battleDuration); AppendNumber(state, _energyRegen);
            _player.WriteState(state);
            foreach (SimulationEnemy enemy in _enemies) enemy.WriteState(state);
            foreach (EnemyStatusEffect status in _enemyStatuses) status.WriteState(state);
            foreach (SimulationSpellEntity entity in _spellEntities) entity.WriteState(state);
            foreach (SimulationProjectile projectile in _enemyProjectiles) projectile.WriteState(state);
            Adaptation.WriteState(state);
            foreach (ScheduledExecution pending in _scheduled)
            {
                state.Append(pending.Tick);
                foreach (SpellAction action in pending.Actions) state.Append('|').Append(action.NodeId);
                AppendNumber(state, pending.Context.Origin.X); AppendNumber(state, pending.Context.Origin.Y);
                AppendNumber(state, pending.Context.Direction.X); AppendNumber(state, pending.Context.Direction.Y);
                state.Append(pending.Context.Target?.Id ?? 0).Append('|').Append(pending.Context.FromEvent).Append('|').Append(pending.Context.NoiseElement);
                state.Append('|').Append(pending.IsIteration);
                AppendNumber(state, pending.Context.CostMultiplier);
                state.Append(pending.Context.SourceEntityId).Append('|');
                pending.Context.Repeat?.AppendState(state);
                pending.Context.Modifiers.AppendState(state);
                pending.Context.CallEvents?.AppendState(state);
                if (pending.Context.Target != null && !pending.Context.Target.IsAlive) pending.Context.Target.WriteState(state);
            }
            foreach (FragmentOrb orb in _orbs) { AppendNumber(state, orb.Position.X); AppendNumber(state, orb.Position.Y); state.Append(orb.Amount); }
            foreach (CompiledSpell spell in _loadout) state.Append('|').Append(spell?.Signature);
            foreach (string element in _unlockedElements) state.Append('|').Append(element);
            foreach (PendingEnemyShot shot in _pendingEnemyShots) { state.Append(shot.Tick).Append('|').Append(shot.Owner.Id); AppendNumber(state, shot.Direction.X); AppendNumber(state, shot.Direction.Y); AppendNumber(state, shot.SpeedMultiplier); }
            AppendNumber(state, _totalDamage); state.Append(_collectedFragments).Append(_nodeExecutionCount);
            uint hash = 2166136261;
            foreach (char value in state.ToString()) { hash ^= value; hash *= 16777619; }
            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }

        /// <summary>불변 문화권 숫자 표기를 상태 해시 버퍼에 추가한다.</summary>
        private static void AppendNumber(StringBuilder state, double value) { state.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('|'); }

        /// <summary>대시와 일반 이동을 처리하고 이번 입력으로 대시가 시작됐는지 반환한다.</summary>
        private bool MovePlayer(SimulationInput input)
        {
            SimVector move = input.Movement.Length > 1 ? input.Movement.Normalized() : input.Movement;
            bool startedDash = input.Dash && _stage != MissionStage.Terminal
                && _player.StartDash(move.LengthSquared > 0.000001 ? move : _player.AimDirection, _balance.Player.DashSeconds, _balance.Player.DashCooldown, Time);
            SimVector displacement = _player.DashRemaining > 0 ? _player.GetDashDirection() * (_balance.Player.DashDistance / _balance.Player.DashSeconds * Math.Min(STEP_SECONDS, _player.DashRemaining)) : move * (_balance.Player.Speed * STEP_SECONDS);
            _player.Move(_map.Move(_player.Position, displacement, _sector.PlayerRadius));
            return startedDash;
        }

        /// <summary>지정 Trigger를 가진 슬롯을 순서대로 시전해 입력 이벤트 실행을 결정적으로 유지한다.</summary>
        private void TryCastTrigger(string trigger)
        {
            for (int slot = 0; slot < _loadout.Length; slot++) TryCastTriggered(_loadout[slot], trigger, slot);
        }

        /// <summary>
        /// 예약된 흐름 실행 중 현재 틱에 도달한 명령을 등록 순서대로 실행한다.
        /// 실패한 Repeat의 남은 회차는 실행하지 않고 건너뛰며, 실행이 끝난 예약은 소속 Repeat의 대기 수에서 뺀다.
        /// </summary>
        private void RunScheduled()
        {
            for (int i = 0; i < _scheduled.Count;)
            {
                ScheduledExecution pending = _scheduled[i];
                if (pending.Tick > _tick) { i++; continue; }
                _scheduled.RemoveAt(i);
                RepeatScope scope = pending.Context.Repeat;
                if (!(pending.IsIteration && scope != null && scope.IsFailed)) Execute(pending.Actions, pending.Context);
                if (scope != null) ReleaseRepeat(scope);
            }
        }

        /// <summary>
        /// Repeat의 첫 회차를 즉시 실행하고 나머지 회차를 간격마다 예약한다. 모든 회차와 회차 안의 Delay가 끝나면
        /// OnComplete를 한 번 실행하며, 회차가 실패하면 남은 회차를 취소하고 OnComplete를 실행하지 않는다.
        /// 회차가 만든 개체의 수명이나 개체 이벤트는 기다리지 않는다.
        /// </summary>
        private void ExecuteRepeat(SpellAction action, SpellContext context)
        {
            var scope = new RepeatScope(action.OnComplete, context, context.Repeat);
            context.Repeat?.Acquire();
            scope.Acquire();
            SpellContext bodyContext = context.WithRepeat(scope);
            Execute(action.Body, bodyContext);
            for (int repeat = 1; repeat < action.Times && !scope.IsFailed; repeat++) Schedule(action.Body, bodyContext, repeat * action.Interval, true);
            ReleaseRepeat(scope);
        }

        /// <summary>
        /// Repeat의 대기 수를 하나 줄이고 0이 되면 완료 처리한다. 실패하지 않았으면 OnComplete를 Repeat 진입 문맥으로 실행하고,
        /// 바깥 Repeat의 대기 수도 줄인다.
        /// </summary>
        private void ReleaseRepeat(RepeatScope scope)
        {
            if (!scope.Release()) return;
            if (!scope.IsFailed) Execute(scope.OnComplete, scope.CompleteContext);
            if (scope.Parent != null) ReleaseRepeat(scope.Parent);
        }

        /// <summary>실행 명령 목록을 컨텍스트 스냅샷으로 처리하고 모든 실제 실행 노드를 기록한다.</summary>
        private void Execute(IReadOnlyList<SpellAction> actions, SpellContext context)
        {
            if (_actionBudgetTick != _tick) { _actionBudgetTick = _tick; _actionsThisTick = 0; }
            for (int i = 0; i < actions.Count; i++)
            {
                if (_actionsThisTick >= _balance.Limits.MaxActionsPerTick)
                {
                    // 안전 상한으로 실행을 생략하면 그 경로를 포함한 Repeat는 실패로 본다(결정 Q8).
                    _droppedExecutions += actions.Count - i;
                    context.Repeat?.Fail();
                    return;
                }
                _actionsThisTick++;
                SpellAction action = actions[i];
                if (!TrySpendFor(action, context))
                {
                    // 마나가 부족하면 이 노드와 그 후속 경로만 중단하고 같은 출력의 다음 연결은 계속 시도한다(결정 Q12).
                    context.Repeat?.Fail();
                    continue;
                }
                RecordNode(action.NodeId);
                switch (action.Kind)
                {
                    case "spawn": SpawnForm(action, context); break;
                    case "buff": ApplyBuff(action, context); break;
                    case "call": ExecuteCall(action, context); break;
                    case "delay": Schedule(action.Then, context, action.Seconds); break;
                    case "repeat": ExecuteRepeat(action, context); break;
                    case "if": Execute(Evaluate(action.Condition, context) ? action.Then : action.Else, context); break;
                    case "blink":
                        SimVector destination = context.FromEvent ? _map.NearestFree(context.Origin, _sector.PlayerRadius) : _map.Move(_player.Position, context.Direction * action.Distance, _sector.PlayerRadius);
                        _player.Move(destination); Execute(action.Next, context.WithOrigin(destination)); break;
                    case "shield": _player.GiveShield(action.ShieldAmount, action.ShieldSeconds, Time); Execute(action.Next, context); break;
                }
            }
        }

        /// <summary>
        /// 실행 노드에 도달했을 때 노드 비용 × 문맥 비용 배율만큼 마나를 차감하고 성공 여부를 반환한다.
        /// 흐름 제어(Delay·Repeat·Condition)는 비용이 없고, 비용이 0이면 항상 성공한다.
        /// </summary>
        private bool TrySpendFor(SpellAction action, SpellContext context)
        {
            if (action.Kind == "delay" || action.Kind == "repeat" || action.Kind == "if") return true;
            double cost = action.NodeEnergy * context.CostMultiplier;
            if (cost <= 0) return true;
            if (!_player.TrySpend(cost)) return false;
            _energySpent += cost;
            return true;
        }

        /// <summary>SpellCall의 컴파일된 대상과 호출부 수식·이벤트 범위를 적용해 반복 실행한다.</summary>
        private void ExecuteCall(SpellAction action, SpellContext context)
        {
            foreach (string attachedNodeId in action.AttachedNodeIds) RecordNode(attachedNodeId);
            SpellModifierValues modifiers = context.Modifiers.Combine(action.ModifierValues);
            SpellEventScope events = new SpellEventScope(action.OnHit, action.OnExpire, action.OnFirstHitOrExpire, context.CallEvents, context.Modifiers,
                context.CostMultiplier);
            // 호출 노드에 붙은 효과의 비용 배율은 호출된 마법의 각 노드 비용에 곱한다.
            SpellContext callContext = new SpellContext(context.Origin, context.Direction, context.Target,
                context.FromEvent, context.NoiseElement, modifiers, events, context.Repeat, context.CostMultiplier * action.EnergyMultiplier,
                context.Caster, context.SourceEntityId);
            for (int instance = 0; instance < action.Count; instance++)
            {
                double spread = action.Count > 1
                    ? (instance / (double)(action.Count - 1) * 2 - 1) * action.ModifierValues.SpreadAngle * DEGREES_TO_RADIANS
                    : 0;
                SpellContext instanceContext = new SpellContext(callContext.Origin, callContext.Direction.Rotated(spread),
                    callContext.Target, callContext.FromEvent, callContext.NoiseElement, callContext.Modifiers, callContext.CallEvents, callContext.Repeat,
                    callContext.CostMultiplier, callContext.Caster, callContext.SourceEntityId);
                Execute(action.CalledSpell.Root, instanceContext);
            }
        }

        /// <summary>
        /// Apply의 자기 적용 효과를 호출부 수식에 맞춰 시전자에게 적용하고 완료 분기를 실행한다.
        /// 회복은 즉시 회복, 보호는 보호막, 화염은 시전자에게 화상(자해 허용)을 부여한다.
        /// </summary>
        private void ApplyBuff(SpellAction action, SpellContext context)
        {
            foreach (string attachedNodeId in action.AttachedNodeIds) RecordNode(attachedNodeId);
            // Apply는 전달된 명중 대상이 아니라 문맥의 시전자에게 적용한다(백서 6장).
            SimulationPlayer caster = context.Caster;
            double amount = action.Power * action.Count * action.ModifierValues.DamageMultiplier * context.Modifiers.DamageMultiplier;
            if (action.SourceElement == "heal") caster.Heal(amount);
            else if (action.SourceElement == "protection")
                caster.GiveShield(amount, action.BuffDuration * action.ModifierValues.DurationMultiplier
                    * context.Modifiers.DurationMultiplier, Time);
            else if (action.SourceElement == "fire") caster.ApplyBurn(Time, _balance.Combat.BurnInterval, _balance.Combat.BurnSeconds);
            for (int instance = 0; instance < action.Count; instance++) Execute(action.OnFirstHitOrExpire, context);
        }

        /// <summary>
        /// 지연 시간에 맞는 틱에 후속 명령과 이벤트 스냅샷을 예약한다. isIteration은 Repeat의 남은 회차 예약 표시다.
        /// 문맥이 Repeat 안이면 그 Repeat가 예약 종료를 기다리고, 예약 상한으로 생략하면 그 Repeat를 실패로 표시한다.
        /// </summary>
        private void Schedule(IReadOnlyList<SpellAction> actions, SpellContext context, double seconds, bool isIteration = false)
        {
            if (actions.Count == 0) return;
            if (_scheduled.Count >= _balance.Limits.MaxScheduledExecutions)
            {
                _droppedExecutions += actions.Count;
                context.Repeat?.Fail();
                return;
            }
            context.Repeat?.Acquire();
            _scheduled.Add(new ScheduledExecution(_tick + Math.Max(1, (int)Math.Round(seconds * TICK_RATE)), actions, context, isIteration));
        }

        /// <summary>대상 또는 자신 상태에 따른 조건 분기를 평가하며 대상이 없으면 대상 조건을 false로 반환한다.</summary>
        private bool Evaluate(SpellCondition condition, SpellContext context)
        {
            if (condition.Type == "selfHpBelow") return _player.Hp / _player.MaxHp * 100 < condition.Pct;
            if (context.Target == null) return false;
            switch (condition.Type)
            { case "targetHpBelow": return context.Target.Hp / context.Target.MaxHp * 100 < condition.Pct; case "targetHasStatus": return HasEnemyStatus(context.Target, condition.Status); case "targetDistanceBelow": return SimVector.Distance(_player.Position, context.Target.Position) < condition.Px; default: return false; }
        }

        /// <summary>Form 종류별 개체 수, 다중 배치, 이벤트 앵커 및 노이즈 속성을 반영하여 마법 개체를 생성한다.</summary>
        private void SpawnForm(SpellAction action, SpellContext context)
        {
            foreach (string attachedNodeId in action.AttachedNodeIds) RecordNode(attachedNodeId);
            string element = action.Noise ? context.NoiseElement : action.Element;
            SpellStats stats = action.Stats.Apply(context.Modifiers);
            for (int i = 0; i < action.Count; i++)
            {
                if (_spellEntities.Count >= _balance.Limits.MaxLiveSpellEntities) return;
                SimVector direction = context.Direction;
                if (action.Form == "bolt" && action.Count > 1) direction = direction.Rotated((i / (double)(action.Count - 1) * 2 - 1) * stats.SpreadAngle * DEGREES_TO_RADIANS);
                SimVector position = context.Origin;
                if (action.Form == "burst" || action.Form == "zone")
                {
                    // 회복·보호 범위는 시전자에게 적용되므로 생성 거리 없이 현재 실행 위치에 만든다.
                    // 부채꼴은 실행 위치를 꼭짓점으로 진행 방향에 펼친다(결정 Q17).
                    bool isFunctional = action.SourceElement == "heal" || action.SourceElement == "protection";
                    bool isCone = action.MagicType == SpellGrammar.MAGIC_TYPE_CONE;
                    if (!context.FromEvent && !isFunctional && !isCone) position += context.Direction * stats.Offset;
                    if (action.Count > 1) position += new SimVector(Math.Cos(i * 2 * Math.PI / action.Count), Math.Sin(i * 2 * Math.PI / action.Count)) * _balance.Sim.MultiOffset;
                }
                double angle = i * 2 * Math.PI / action.Count;
                if (action.Form == "orbit") position = (context.FromEvent ? context.Origin : _player.Position) + new SimVector(Math.Cos(angle), Math.Sin(angle)) * stats.OrbitRadius;
                var entity = new SimulationSpellEntity(++_nextEntityId, action, stats, element, position, direction,
                    context.FromEvent, context.Origin, angle, context.NoiseElement, _balance.Sim.SpellVisualSeconds,
                    context.CallEvents, context.Modifiers, context.CostMultiplier, context.Caster);
                _spellEntities.Add(entity); _peakSpellEntities = Math.Max(_peakSpellEntities, _spellEntities.Count);
                // 보호 Orbit(방벽)은 생성 시 보호막을 준다. 보호 Burst는 범위 안의 시전자에게만 HitArea에서 준다.
                if (action.SourceElement == "protection" && action.Form == SpellGrammar.FORM_ORBIT)
                    _player.GiveShield(action.Power * action.ModifierValues.DamageMultiplier * context.Modifiers.DamageMultiplier,
                        action.BuffDuration * action.ModifierValues.DurationMultiplier * context.Modifiers.DurationMultiplier, Time);
                if (action.Form == "burst")
                { HitArea(entity, true); Expire(entity); }
                else if (action.Form == "zone" && action.SourceElement != "protection") HitArea(entity, false);
            }
        }

        /// <summary>투사체 이동, 유도, 회전 및 지속 범위 피해를 처리하고 소멸 이벤트를 한 번만 발생시킨다.</summary>
        private void AdvanceSpellEntities()
        {
            int initialCount = _spellEntities.Count;
            int processed = 0;
            for (int i = 0; processed < initialCount && i < _spellEntities.Count; processed++)
            {
                SimulationSpellEntity entity = _spellEntities[i];
                if (entity.Kind == "bolt") AdvanceBolt(entity);
                else if (entity.Kind == "orbit")
                {
                    entity.Angle += entity.Stats.AngularSpeed * DEGREES_TO_RADIANS * STEP_SECONDS;
                    SimVector anchor = entity.FromEvent ? entity.Anchor : _player.Position;
                    SimVector position = anchor + new SimVector(Math.Cos(entity.Angle), Math.Sin(entity.Angle)) * entity.Stats.OrbitRadius;
                    entity.Move(position, new SimVector(-Math.Sin(entity.Angle), Math.Cos(entity.Angle)));
                    if (entity.Action.SourceElement != "protection") HitArea(entity, false);
                }
                else if (entity.Kind == "zone") HitArea(entity, false);
                entity.Advance(STEP_SECONDS);
                if ((entity.Kind != "burst" && entity.HasExpired) || entity.Age + 0.000001 >= entity.Lifetime)
                { Expire(entity); _spellEntities.RemoveAt(i); }
                else i++;
            }
        }

        /// <summary>볼트의 유도 방향, 벽 충돌 및 가장 가까운 순서의 관통 적중을 처리한다.</summary>
        private void AdvanceBolt(SimulationSpellEntity entity)
        {
            SimVector direction = entity.Direction;
            SpellStats stats = entity.Stats;
            if (stats.HomingTurn > 0)
            {
                SimulationEnemy target = NearestEnemy(entity.Position, stats.HomingRange, entity.HitTimes);
                if (target != null)
                {
                    double current = Math.Atan2(direction.Y, direction.X); SimVector toTarget = target.Position - entity.Position;
                    double wanted = Math.Atan2(toTarget.Y, toTarget.X); double delta = Math.Atan2(Math.Sin(wanted - current), Math.Cos(wanted - current));
                    double turn = stats.HomingTurn * DEGREES_TO_RADIANS * STEP_SECONDS;
                    direction = direction.Rotated(Math.Max(-turn, Math.Min(turn, delta)));
                }
            }
            SimVector previous = entity.Position;
            SimVector next = previous + direction * (stats.Speed * STEP_SECONDS);
            if (!_map.CanOccupy(next, entity.Radius))
            {
                // 벽·지형 충돌은 직접 충돌이므로 접촉점에서 OnHit을 내고 OnExpire 없이 사라진다.
                SimVector contact = _map.Move(previous, next - previous, entity.Radius);
                entity.Move(contact, direction);
                RaiseHitEvent(entity, contact, null);
                entity.HasExpired = true;
                return;
            }
            entity.Move(next, direction);
            var targets = new List<SimulationEnemy>();
            foreach (SimulationEnemy enemy in _enemies)
            {
                bool isInside = entity.IsBox
                    ? IntersectsBoxSweep(previous, next, direction, entity.Radius, enemy.Position, enemy.Radius)
                    : SegmentDistance(previous, next, enemy.Position) <= entity.Radius + enemy.Radius;
                if (enemy.IsAlive && !entity.HitTimes.ContainsKey(enemy.Id) && isInside) targets.Add(enemy);
            }
            targets.Sort((a, b) =>
            {
                int distanceOrder = (a.Position - previous).LengthSquared.CompareTo((b.Position - previous).LengthSquared);
                return distanceOrder != 0 ? distanceOrder : a.Id.CompareTo(b.Id);
            });
            foreach (SimulationEnemy enemy in targets)
            {
                if (!enemy.IsAlive) continue;
                double multiplier = Math.Max(0, 1 - entity.Hits * stats.PierceLoss);
                entity.HitTimes[enemy.Id] = Time; Hit(entity, enemy, stats.Damage * multiplier, false); entity.Hits++;
                RaiseHitEvent(entity, enemy.Position, enemy);
                if (entity.Hits >= 1 + stats.Pierce) { entity.Move(enemy.Position, direction); entity.HasExpired = true; break; }
            }
        }

        /// <summary>방향을 따라 이동하는 정사각형 투사체와 대상 원의 겹침 여부를 반환한다.</summary>
        private static bool IntersectsBoxSweep(SimVector start, SimVector end, SimVector direction,
            double halfSize, SimVector target, double targetRadius)
        {
            SimVector movement = end - start;
            double length = movement.Length;
            SimVector forward = length > 0.000001 ? movement / length : direction.Normalized();
            SimVector relative = target - start;
            double extent = halfSize + targetRadius;
            double along = SimVector.Dot(relative, forward);
            double across = Math.Abs(SimVector.Dot(relative, new SimVector(-forward.Y, forward.X)));
            return along >= -extent && along <= length + extent && across <= extent;
        }

        /// <summary>
        /// 범위 마법 정사각형(반 변 길이 = 개체 반경)과 대상 원의 겹침 여부를 반환한다.
        /// 똑바로 세우기 설정이면 월드 축 기준, 아니면 개체 진행 방향 기준으로 판정한다.
        /// </summary>
        private bool IntersectsAreaBox(SimulationSpellEntity entity, SimVector target, double targetRadius)
        {
            SimVector forward = _isAreaBoxUpright ? new SimVector(1, 0) : entity.Direction.Normalized();
            SimVector side = new SimVector(-forward.Y, forward.X);
            SimVector relative = target - entity.Position;
            double extent = entity.Radius + targetRadius;
            return Math.Abs(SimVector.Dot(relative, forward)) <= extent && Math.Abs(SimVector.Dot(relative, side)) <= extent;
        }

        /// <summary>범위 마법(원·사각형·부채꼴)이 대상 원과 겹치는지 반환한다.</summary>
        private bool IsInsideArea(SimulationSpellEntity entity, SimVector target, double targetRadius)
        {
            if (entity.IsBox) return IntersectsAreaBox(entity, target, targetRadius);
            if (entity.IsCone) return IntersectsCone(entity, target, targetRadius);
            return SimVector.Distance(entity.Position, target) <= entity.Radius + targetRadius;
        }

        /// <summary>
        /// 꼭짓점(개체 위치)에서 진행 방향으로 반경·전체 각도만큼 펼친 부채꼴과 대상 원의 겹침 여부를 반환한다.
        /// 대상 반경만큼 거리와 각도에 여유를 준다.
        /// </summary>
        private static bool IntersectsCone(SimulationSpellEntity entity, SimVector target, double targetRadius)
        {
            SimVector relative = target - entity.Position;
            double distance = relative.Length;
            if (distance > entity.Radius + targetRadius) return false;
            if (distance <= targetRadius) return true;
            SimVector forward = entity.Direction.Normalized();
            double cosine = Math.Max(-1, Math.Min(1, SimVector.Dot(relative / distance, forward)));
            double tolerance = Math.Asin(Math.Min(1, targetRadius / distance));
            return Math.Acos(cosine) <= entity.ConeAngle * 0.5 * DEGREES_TO_RADIANS + tolerance;
        }

        /// <summary>
        /// 범위 마법의 가까운 적부터 대상별 주기에 맞춰 피해를 적용한다. 범위 적중은 OnHit을 만들지 않는다.
        /// 공전만 대상과의 접촉이 시작될 때(떨어졌다 다시 닿을 때 포함) OnHit을 낸다.
        /// </summary>
        private void HitArea(SimulationSpellEntity entity, bool isBurst)
        {
            string source = entity.Action.SourceElement;
            if (source == "heal" || (source == "protection" && isBurst))
            {
                ApplyFunctionalArea(entity, isBurst);
                return;
            }
            if (source == "protection") return;
            var targets = new List<SimulationEnemy>();
            foreach (SimulationEnemy enemy in _enemies)
            {
                if (enemy.IsAlive && IsInsideArea(entity, enemy.Position, enemy.Radius)) targets.Add(enemy);
            }
            targets.Sort((a, b) =>
            {
                int distanceOrder = (a.Position - entity.Position).LengthSquared.CompareTo((b.Position - entity.Position).LengthSquared);
                return distanceOrder != 0 ? distanceOrder : a.Id.CompareTo(b.Id);
            });
            double interval = entity.Kind == "zone" ? entity.Stats.TickInterval : entity.Stats.HitInterval;
            bool isOrbit = entity.Kind == SpellGrammar.FORM_ORBIT;
            foreach (SimulationEnemy enemy in targets)
            {
                if (!enemy.IsAlive) continue;
                bool isNewContact = isOrbit && entity.MarkContact(enemy.Id);
                bool hasHit = entity.HitTimes.TryGetValue(enemy.Id, out double lastTime);
                if (!hasHit || (!isBurst && Time - lastTime + 0.000001 >= interval))
                {
                    entity.HitTimes[enemy.Id] = Time;
                    Hit(entity, enemy, entity.Stats.Damage, entity.Kind == "zone");
                }
                if (isNewContact) RaiseHitEvent(entity, enemy.Position, enemy);
            }
            if (isOrbit) entity.EndContactScan();
        }

        /// <summary>
        /// 회복·보호(Functional) 범위 효과를 범위 안의 시전자에게만 적용한다(결정 Q15). 적에게는 효과가 없다.
        /// Burst는 한 번, Persist는 시전자 기준 TickInterval마다 즉시 회복하며 별도 상태 이상을 만들지 않는다.
        /// </summary>
        private void ApplyFunctionalArea(SimulationSpellEntity entity, bool isBurst)
        {
            if (!IsInsideArea(entity, _player.Position, _sector.PlayerRadius)) return;
            bool hasApplied = entity.HitTimes.TryGetValue(PLAYER_TARGET_ID, out double lastTime);
            if (hasApplied && (isBurst || Time - lastTime + 0.000001 < entity.Stats.TickInterval)) return;
            entity.HitTimes[PLAYER_TARGET_ID] = Time;
            SpellAction action = entity.Action;
            double amount = action.Power * action.ModifierValues.DamageMultiplier * entity.Modifiers.DamageMultiplier;
            if (action.SourceElement == "heal") _player.Heal(amount);
            else _player.GiveShield(amount, action.BuffDuration * action.ModifierValues.DurationMultiplier * entity.Modifiers.DurationMultiplier, Time);
        }

        /// <summary>스펠 피해와 상태를 적용하고 전격 연쇄를 처리한다. 이벤트 실행은 호출부가 RaiseHitEvent로 따로 처리한다.</summary>
        private void Hit(SimulationSpellEntity entity, SimulationEnemy enemy, double damage, bool isDot)
        {
            ApplyDamage(enemy, damage, entity.Element, entity.Kind, entity.Action.Noise, isDot, entity.Direction, true);
            if (entity.Element == "arc")
            {
                RuneStats arc = GameData.Runes.Get("elem.arc").Stats;
                var chain = new List<SimulationEnemy>();
                foreach (SimulationEnemy other in _enemies)
                    if (other != enemy && other.IsAlive && SimVector.Distance(other.Position, enemy.Position) <= arc.ArcRange) chain.Add(other);
                chain.Sort((a, b) =>
                {
                    int distanceOrder = (a.Position - enemy.Position).LengthSquared.CompareTo((b.Position - enemy.Position).LengthSquared);
                    return distanceOrder != 0 ? distanceOrder : a.Id.CompareTo(b.Id);
                });
                for (int i = 0; i < Math.Min(arc.ArcTargets, chain.Count); i++) ApplyDamage(chain[i], damage * arc.ArcMultiplier, "arc", entity.Kind, entity.Action.Noise, isDot, (chain[i].Position - enemy.Position).Normalized(), true);
            }
        }

        /// <summary>
        /// 개체의 OnHit 이벤트를 지정 위치·대상(벽이면 null)으로 실행한다. 발사체는 직접 충돌로 기록하고
        /// 첫 이벤트이면 OnFirstHitOrExpire도 실행한다. 개체당 이벤트 상한을 넘으면 실행하지 않는다.
        /// </summary>
        private void RaiseHitEvent(SimulationSpellEntity entity, SimVector position, SimulationEnemy target)
        {
            bool isBolt = entity.Kind == SpellGrammar.FORM_BOLT;
            if (isBolt) entity.HasDirectHit = true;
            if (entity.TriggerCount >= _balance.Limits.HitTriggerCap) return;
            entity.TriggerCount++;
            SpellContext context = new SpellContext(position, entity.Direction, target, true,
                entity.CastNoiseElement, entity.Modifiers, entity.CallEvents, null, entity.CostMultiplier, entity.Caster, entity.Id);
            bool isFirstEvent = isBolt && entity.TryMarkFirstEvent();
            Execute(entity.Action.OnHit, context);
            if (isFirstEvent) Execute(entity.Action.OnFirstHitOrExpire, context);
            ExecuteCallEvent(entity, true, isFirstEvent, position, target);
        }

        /// <summary>
        /// 개체를 만든 호출 객체들의 이벤트 분기를 안쪽에서 바깥쪽으로 각각 한 번 실행한다. 이벤트 원본은 그 개체다.
        /// 개체의 첫 적중·소멸 이벤트이면 적중·소멸 분기 다음에 onFirstHitOrExpire 분기도 실행한다.
        /// </summary>
        private void ExecuteCallEvent(SimulationSpellEntity entity, bool isHit, bool isFirstEvent, SimVector origin, SimulationEnemy target)
        {
            for (SpellEventScope current = entity.CallEvents; current != null; current = current.Parent)
            {
                IReadOnlyList<SpellAction> actions = isHit ? current.OnHit : current.OnExpire;
                SpellContext context = new SpellContext(origin, entity.Direction, target, true, entity.CastNoiseElement,
                    current.ParentModifiers, current.Parent, null, current.ParentCostMultiplier, entity.Caster, entity.Id);
                Execute(actions, context);
                if (isFirstEvent) Execute(current.OnFirstHitOrExpire, context);
            }
        }

        /// <summary>피해 파이프라인에 적응, 이지스 방패, 릴레이 방어 및 보스 무적을 반영하고 실제 피해와 학습을 기록한다.</summary>
        private double ApplyDamage(SimulationEnemy enemy, double damage, string element, string form, bool noise, bool isDot, SimVector direction, bool applyStatus)
        {
            if (!enemy.IsAlive || enemy.IsPatching) return 0;
            double actual = damage * Adaptation.GetMultiplier(element, form);
            if (enemy.Kind == "enemy.aegis" && !HasEnemyStatus(enemy, EnemyStatusType.Emp) && form == "bolt" && !isDot && SimVector.Dot(enemy.Facing, direction * -1) >= Math.Cos(_balance.Combat.AegisAngle * 0.5 * DEGREES_TO_RADIANS)) actual *= 1 - _balance.Combat.AegisReduction;
            bool relayAlive = false;
            bool hasRelayAura = false;
            foreach (SimulationEnemy relay in _enemies)
            {
                if (relay.Kind != "enemy.relay" || !relay.IsAlive) continue;
                relayAlive = true;
                if (relay != enemy && SimVector.Distance(relay.Position, enemy.Position) <= _balance.Combat.RelayRadius) hasRelayAura = true;
            }
            if (hasRelayAura) actual *= 1 - _balance.Combat.RelayReduction;
            actual = enemy.Hurt(actual, Time);
            if (applyStatus) ApplyStatus(enemy, element, form, noise);
            if (actual > 0)
            {
                enemy.SetDamageTags(element, form);
                _totalDamage += actual; _rollingDamage += actual; _damageWindow.Enqueue(new DamageSample(_tick, actual));
                _damageNumbers.Add(new DamageNumber(enemy.Position, actual, element, _tick));
                Adaptation.Learn(element, form, Time, actual, isDot, noise, relayAlive);
            }
            if (enemy.Kind == "boss.governor" && enemy.IsAlive) CheckBossPatch(enemy);
            return actual;
        }

        /// <summary>
        /// 상태 판정 시각을 현재 틱으로 갱신하고, 적 목록 순서로 화염의 마지막 만료 틱까지 지속 피해를 적용한 뒤 만료된 상태 이상을 제거한다.
        /// </summary>
        private void AdvanceStatuses()
        {
            _statusTime = Time;
            while (_player.Hp > 0 && _player.TryTakeBurnTick(Time, _balance.Combat.BurnInterval))
                HurtPlayerByStatus(_balance.Combat.BurnDps * _balance.Combat.BurnInterval);
            foreach (SimulationEnemy enemy in _enemies)
            {
                enemy.Observe(Time);
                EnemyStatusEffect burn = FindStatus(enemy.Id, EnemyStatusType.Burn);
                if (burn == null) continue;
                while (enemy.IsAlive && burn.NextTickTime <= burn.Until + 0.000001 && Time + 0.000001 >= burn.NextTickTime)
                { ApplyDamage(enemy, _balance.Combat.BurnDps * _balance.Combat.BurnInterval, "fire", burn.SourceForm, burn.IsNoise, true, SimVector.Zero, false); burn.NextTickTime += _balance.Combat.BurnInterval; }
            }
            for (int i = _enemyStatuses.Count - 1; i >= 0; i--)
                if (IsStatusExpired(_enemyStatuses[i])) { RemoveStatusIndex(_enemyStatuses[i]); _enemyStatuses.RemoveAt(i); }
        }

        /// <summary>
        /// 상태 이상이 더 이상 판정·진행에 쓰이지 않는지 반환한다.
        /// 화염은 마지막 지속 피해 틱까지 처리된 뒤, 빙결은 빙결 면역까지 끝난 뒤 만료된다.
        /// </summary>
        private bool IsStatusExpired(EnemyStatusEffect status)
        {
            switch (status.Type)
            {
                case EnemyStatusType.Burn: return status.NextTickTime > status.Until + 0.000001 && Time >= status.Until;
                case EnemyStatusType.Freeze: return Time >= status.ImmuneUntil;
                default: return Time >= status.Until;
            }
        }

        /// <summary>피해 속성에 맞는 상태 이상(화염·냉기·EMP)을 현재 시각 기준으로 부여한다.</summary>
        private void ApplyStatus(SimulationEnemy enemy, string element, string form, bool noise)
        {
            if (element == "fire") ApplyBurn(enemy, form, noise);
            else if (element == "ice") ApplyChill(enemy);
            else if (element == "arc") GetOrAddStatus(enemy.Id, EnemyStatusType.Emp).Until = Time + _balance.Combat.EmpSeconds;
        }

        /// <summary>화염의 출처 형태와 노이즈를 보관하고 지속시간을 갱신하되, 진행 중인 화염이면 다음 틱 시각을 유지한다.</summary>
        private void ApplyBurn(SimulationEnemy enemy, string form, bool noise)
        {
            EnemyStatusEffect burn = FindStatus(enemy.Id, EnemyStatusType.Burn);
            if (burn == null || Time >= burn.Until)
            {
                burn ??= AddStatus(enemy.Id, EnemyStatusType.Burn);
                burn.NextTickTime = Time + _balance.Combat.BurnInterval;
            }
            burn.Until = Time + _balance.Combat.BurnSeconds; burn.SourceForm = form; burn.IsNoise = noise;
        }

        /// <summary>
        /// 냉기 스택과 지속시간을 갱신하고, 최대 스택에서 빙결 면역이 아니면 빙결을 부여하며 냉기를 제거한다.
        /// 빙결 면역 중에는 냉기 스택이 면역 중 상한(백서 기준 2)을 넘지 않는다.
        /// </summary>
        private void ApplyChill(SimulationEnemy enemy)
        {
            CombatBalance combat = _balance.Combat;
            EnemyStatusEffect freeze = FindStatus(enemy.Id, EnemyStatusType.Freeze);
            bool isImmune = freeze != null && Time < freeze.ImmuneUntil;
            EnemyStatusEffect chill = GetOrAddStatus(enemy.Id, EnemyStatusType.Chill);
            int maxStacks = isImmune ? combat.ChillImmuneMaxStacks : combat.ChillMaxStacks;
            chill.Stacks = Math.Min(maxStacks, chill.Stacks + 1); chill.Until = Time + combat.ChillSeconds;
            if (isImmune || chill.Stacks < combat.ChillMaxStacks) return;
            freeze ??= AddStatus(enemy.Id, EnemyStatusType.Freeze);
            freeze.Until = Time + combat.FreezeSeconds; freeze.ImmuneUntil = freeze.Until + combat.FreezeImmunity;
            RemoveStatusIndex(chill); _enemyStatuses.Remove(chill);
        }

        /// <summary>적의 상태 이상이 현재 상태 판정 시각에 활성인지 반환한다. 냉기는 스택 보유 여부로 판정한다.</summary>
        public bool HasEnemyStatus(SimulationEnemy enemy, EnemyStatusType type)
        {
            EnemyStatusEffect status = FindStatus(enemy.Id, type);
            if (status == null) return false;
            return type == EnemyStatusType.Chill ? status.Stacks > 0 : _statusTime < status.Until;
        }

        /// <summary>조건 룬의 상태 이름(burn, chill, freeze, emp)으로 활성 여부를 반환하며 알 수 없는 이름은 false를 반환한다.</summary>
        private bool HasEnemyStatus(SimulationEnemy enemy, string status)
        {
            switch (status)
            {
                case "burn": return HasEnemyStatus(enemy, EnemyStatusType.Burn);
                case "chill": return HasEnemyStatus(enemy, EnemyStatusType.Chill);
                case "freeze": return HasEnemyStatus(enemy, EnemyStatusType.Freeze);
                case "emp": return HasEnemyStatus(enemy, EnemyStatusType.Emp);
                default: return false;
            }
        }

        /// <summary>적의 현재 냉기 스택을 반환하며 냉기가 없으면 0을 반환한다.</summary>
        public int GetChillStacks(SimulationEnemy enemy) => FindStatus(enemy.Id, EnemyStatusType.Chill)?.Stacks ?? 0;

        /// <summary>적 ID와 종류로 상태 이상을 찾고 없으면 null을 반환한다.</summary>
        private EnemyStatusEffect FindStatus(int enemyId, EnemyStatusType type)
        { _statusIndex.TryGetValue(GetStatusKey(enemyId, type), out EnemyStatusEffect status); return status; }

        /// <summary>적 ID와 종류의 상태 이상을 찾고 없으면 새로 추가하여 반환한다.</summary>
        private EnemyStatusEffect GetOrAddStatus(int enemyId, EnemyStatusType type) => FindStatus(enemyId, type) ?? AddStatus(enemyId, type);

        /// <summary>새 상태 이상을 목록 끝과 조회 인덱스에 추가하여 반환한다.</summary>
        private EnemyStatusEffect AddStatus(int enemyId, EnemyStatusType type)
        { var status = new EnemyStatusEffect(enemyId, type); _enemyStatuses.Add(status); _statusIndex.Add(GetStatusKey(enemyId, type), status); return status; }

        /// <summary>제거된 적의 모든 상태 이상을 목록과 조회 인덱스에서 삭제한다.</summary>
        private void RemoveEnemyStatuses(int enemyId)
        {
            for (int i = _enemyStatuses.Count - 1; i >= 0; i--)
                if (_enemyStatuses[i].EnemyId == enemyId) { RemoveStatusIndex(_enemyStatuses[i]); _enemyStatuses.RemoveAt(i); }
        }

        /// <summary>상태 이상을 조회 인덱스에서 삭제한다.</summary>
        private void RemoveStatusIndex(EnemyStatusEffect status) { _statusIndex.Remove(GetStatusKey(status.EnemyId, status.Type)); }

        /// <summary>적 ID와 상태 이상 종류를 하나의 조회 키로 합친다.</summary>
        private static int GetStatusKey(int enemyId, EnemyStatusType type) => enemyId * STATUS_TYPE_COUNT + (int)type;

        /// <summary>
        /// 적마다 이동 전 플레이어 방향을 구해 이동 판단을 적용한 뒤 접촉, 예고 사격, 돌진 및 거버너 페이즈 패턴을 진행한다.
        /// 공격 판정은 이동 전 거리·방향을, 접촉과 발사 위치는 이동 후 위치를 사용한다.
        /// </summary>
        private void AdvanceEnemies()
        {
            int initialCount = _enemies.Count;
            for (int i = 0; i < initialCount; i++)
            {
                SimulationEnemy enemy = _enemies[i]; if (!enemy.IsAlive) continue;
                enemy.Observe(Time);
                if (enemy.IsDummy || HasEnemyStatus(enemy, EnemyStatusType.Freeze)) continue;
                SimVector offset = _player.Position - enemy.Position; SimVector direction = offset.Normalized();
                bool isBoss = enemy.Kind == "boss.governor";
                if (isBoss) { CheckBossPatch(enemy); if (enemy.IsPatching) continue; }
                double speedMultiplier = isBoss && enemy.Phase == 3 ? _governor.PhaseThreeMultiplier : 1;
                var context = new EnemyMoveContext(offset, direction, Time, _isMission, _incremental.StationaryAdvanceSpeed, _governor.PreferredDistance, speedMultiplier);
                ApplyMove(enemy, EnemyMovements.Decide(enemy, context));
                if (isBoss) AdvanceBoss(enemy, direction);
                else AdvanceEnemyAttack(enemy, offset, direction);
            }
        }

        /// <summary>이동 판단 결과에 따라 정지, 방향 전환 또는 감속·벽 충돌을 반영한 이동을 적용한다.</summary>
        private void ApplyMove(SimulationEnemy enemy, EnemyMoveDecision decision)
        {
            if (decision.Mode == EnemyMoveMode.Face) enemy.Move(enemy.Position, decision.Direction);
            else if (decision.Mode == EnemyMoveMode.Move) MoveEnemy(enemy, decision.Direction, decision.Speed);
        }

        /// <summary>
        /// 보스가 아닌 적의 공격 일정을 이동 전 거리·방향으로 진행한다.
        /// 센트리는 예고 후 연사, 헌터는 예고 후 돌진·돌진 중 접촉, 릴레이는 공격하지 않으며 그 외는 주기적 접촉 피해를 준다.
        /// </summary>
        private void AdvanceEnemyAttack(SimulationEnemy enemy, SimVector offset, SimVector direction)
        {
            EnemyDefinition definition = enemy.Definition;
            if (enemy.Kind == "enemy.relay") return;
            if (enemy.Kind == "enemy.sentry")
            {
                if (enemy.WarningUntil > 0 && Time + 0.000001 >= enemy.WarningUntil)
                { ShootBurst(enemy, enemy.AttackDirection, 1); enemy.WarningUntil = 0; enemy.AttackAt = Time + definition.AttackInterval - definition.WarningSeconds; }
                else if (enemy.WarningUntil == 0 && Time + 0.000001 >= enemy.AttackAt)
                { enemy.AttackDirection = direction; enemy.WarningUntil = Time + definition.WarningSeconds; }
                return;
            }
            if (enemy.Kind == "enemy.hunter")
            {
                if (Time < enemy.DashUntil) { HurtByContact(enemy); return; }
                if (enemy.WarningUntil > 0)
                { if (Time + 0.000001 >= enemy.WarningUntil) { enemy.WarningUntil = 0; enemy.DashUntil = Time + definition.DashSeconds; enemy.AttackAt = Time + definition.AttackInterval; } return; }
                if (offset.Length < definition.AttackRange && Time >= enemy.AttackAt)
                { enemy.WarningUntil = Time + definition.WarningSeconds; enemy.AttackDirection = direction; }
                return;
            }
            if (Time >= enemy.AttackAt && HurtByContact(enemy)) enemy.AttackAt = Time + definition.AttackInterval;
        }

        /// <summary>냉기 감속을 반영하여 적을 벽 충돌 가능한 방향으로 이동시킨다.</summary>
        private void MoveEnemy(SimulationEnemy enemy, SimVector direction, double speed)
        { double slow = Math.Max(0, 1 - _balance.Combat.ChillSlow * GetChillStacks(enemy)); enemy.Move(_map.Move(enemy.Position, direction * (speed * slow * STEP_SECONDS), enemy.Radius), direction); }

        /// <summary>적과 플레이어가 겹치면 피해를 적용하고 접촉 여부를 반환한다.</summary>
        private bool HurtByContact(SimulationEnemy enemy)
        { if (!_isMission || SimVector.Distance(enemy.Position, _player.Position) > enemy.Radius + _sector.PlayerRadius) return false; HurtPlayer(enemy.Damage); return true; }

        /// <summary>플레이어 피해에 디버그 무적과 기본 피격 무적을 반영한다.</summary>
        private void HurtPlayer(double damage)
        {
            if (_debugInvulnerable || !_isMission) return;
            double shieldBefore = _player.Shield;
            double actual = _player.Hurt(damage, Time, _balance.Player.HurtInvulnerability);
            if (actual > 0) _damageNumbers.Add(new DamageNumber(_player.Position, actual, "player", _tick));
            if (actual > 0 || _player.Shield < shieldBefore) TryCastTrigger(SpellGrammar.TRIGGER_ON_HIT_TAKEN);
        }

        /// <summary>
        /// 시전자의 상태 이상 피해(화상 DoT)를 적용한다. 피격 무적을 무시하고 주지 않으며 OnHitTaken을 발생시키지 않는다.
        /// 시험 도크(비전투)에서는 상태만 표시하고 체력을 깎지 않는다(결정 Q13).
        /// </summary>
        private void HurtPlayerByStatus(double damage)
        {
            if (_debugInvulnerable || !_isMission) return;
            double actual = _player.HurtByStatus(damage);
            if (actual > 0) _damageNumbers.Add(new DamageNumber(_player.Position, actual, "player", _tick));
        }

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
                if (!shot.Owner.IsAlive || shot.Owner.IsPatching || HasEnemyStatus(shot.Owner, EnemyStatusType.Freeze)) { _pendingEnemyShots.RemoveAt(i); continue; }
                if (shot.Tick > _tick) { i++; continue; }
                _pendingEnemyShots.RemoveAt(i); ShootFan(shot.Owner, shot.Direction, 1, shot.SpeedMultiplier);
            }
        }

        /// <summary>
        /// 거버너의 방사 사격, 조준 사격, 예고 장판, 증원 주기 및 접촉 피해를 페이즈에 맞춰 처리한다.
        /// 페이즈 전환과 이동은 호출 전에 끝난 상태여야 하며 조준은 이동 전 방향을 사용한다.
        /// </summary>
        private void AdvanceBoss(SimulationEnemy boss, SimVector direction)
        {
            double multiplier = boss.Phase == 3 ? _governor.PhaseThreeMultiplier : 1;
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

        /// <summary>거버너 체력 문턱에서 패치 무적과 최고 피해 태그 잠금을 적용한다.</summary>
        private void CheckBossPatch(SimulationEnemy boss)
        {
            if (boss.IsPatching) return;
            int next = boss.Phase == 1 && boss.Hp / boss.MaxHp <= _governor.PhaseTwoThreshold ? 2 : boss.Phase == 2 && boss.Hp / boss.MaxHp <= _governor.PhaseThreeThreshold ? 3 : boss.Phase;
            if (next == boss.Phase) return;
            boss.Patch(next, Time, _governor.PatchSeconds); boss.Observe(Time); _lastPatchTags = Adaptation.LockTopDamageTags();
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

        /// <summary>
        /// 개체의 자연 소멸(수명·사거리 종료)을 처리하고 OnExpire를 소멸 위치에서 한 번 실행한다.
        /// Burst는 이벤트 출력이 없고, 직접 충돌한 발사체는 OnExpire를 내지 않는다. 명중 없이 사라지는 발사체만 OnFirstHitOrExpire도 실행한다.
        /// </summary>
        private void Expire(SimulationSpellEntity entity)
        {
            if (entity.HasExpired) return;
            entity.HasExpired = true;
            if (entity.Kind == SpellGrammar.FORM_BURST || entity.HasDirectHit) return;
            SpellContext context = new SpellContext(entity.Position, entity.Direction, null, true,
                entity.CastNoiseElement, entity.Modifiers, entity.CallEvents, null, entity.CostMultiplier, entity.Caster, entity.Id);
            bool isFirstEvent = entity.Kind == SpellGrammar.FORM_BOLT && entity.TryMarkFirstEvent();
            Execute(entity.Action.OnExpire, context);
            if (isFirstEvent) Execute(entity.Action.OnFirstHitOrExpire, context);
            ExecuteCallEvent(entity, false, isFirstEvent, entity.Position, null);
        }

        /// <summary>처치된 적과 그 상태 이상을 제거하고 시간제 전투의 처치 통계와 RAM 오브를 생성한다.</summary>
        private void CleanupEnemies()
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                SimulationEnemy enemy = _enemies[i]; if (enemy.IsAlive) continue;
                if (_isMission)
                { _orbs.Add(new FragmentOrb(enemy.Position, enemy.Reward)); _killCounts.TryGetValue(enemy.Kind, out int count); _killCounts[enemy.Kind] = count + 1; _killCount++; }
                RemoveEnemyStatuses(enemy.Id); _enemies.RemoveAt(i);
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
            Adaptation.SetEnabled(_incremental.IsAdaptationEnabled);
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
            var enemy = new SimulationEnemy(++_nextEntityId, definition, EnemyMovements.Resolve(id), _map.NearestFree(position, definition.Radius), isDummy, hp, _balance.Sim.HitFlashSeconds, hpMultiplier, damageMultiplier, reward);
            enemy.AttackAt = Time + definition.AttackInterval;
            if (id == "boss.governor") { enemy.ReinforcementAt = Time + _governor.ReinforcementInterval; enemy.HazardAt = Time + _governor.HazardInterval; }
            enemy.Observe(Time); _enemies.Add(enemy); if (_isMission) _spawnedEnemies++; return enemy;
        }

        /// <summary>방 전환, 사망 및 클리어 시 지속 개체와 예약 실행을 취소한다.</summary>
        private void CancelCombat() { _spellEntities.Clear(); _enemyProjectiles.Clear(); _scheduled.Clear(); _pendingEnemyShots.Clear(); }

        /// <summary>노드 실행 틱을 기록하고 누적 실행 수를 증가시킨다.</summary>
        private void RecordNode(string nodeId) { _nodeEvents.Add(new NodeExecutionEvent(nodeId, _tick)); _nodeExecutionCount++; }

        /// <summary>표시 수명이 지난 피해 숫자, 노드 이벤트 및 5초 DPS 샘플을 제거한다.</summary>
        private void TrimFeedback()
        {
            for (int i = _damageNumbers.Count - 1; i >= 0; i--) if (_tick - _damageNumbers[i].Tick > _balance.Sim.DamageNumberSeconds * TICK_RATE) _damageNumbers.RemoveAt(i);
            for (int i = _nodeEvents.Count - 1; i >= 0; i--) if (_tick - _nodeEvents[i].Tick > TICK_RATE) _nodeEvents.RemoveAt(i);
            while (_damageWindow.Count > 0 && _tick - _damageWindow.Peek().Tick > 5 * TICK_RATE) _rollingDamage -= _damageWindow.Dequeue().Amount;
        }

        /// <summary>시드 기반 mulberry32 연산으로 0 이상 1 미만의 결정적 난수를 반환한다.</summary>
        private double NextRandom()
        {
            unchecked { _rngState += 0x6D2B79F5; uint value = _rngState; value = (value ^ (value >> 15)) * (value | 1); value ^= value + ((value ^ (value >> 7)) * (value | 61)); return (value ^ (value >> 14)) / 4294967296.0; }
        }

        /// <summary>이미 적중한 적을 제외하고 탐지 반경 내 가장 가까운 생존 적을 반환한다.</summary>
        private SimulationEnemy NearestEnemy(SimVector position, double range, Dictionary<int, double> excluded)
        { SimulationEnemy result = null; double best = range * range; foreach (SimulationEnemy enemy in _enemies) { if (!enemy.IsAlive || excluded.ContainsKey(enemy.Id)) continue; double distance = (enemy.Position - position).LengthSquared; if (distance < best) { result = enemy; best = distance; } } return result; }

        /// <summary>점과 선분 사이의 최단 거리를 반환하여 빠른 투사체의 연속 충돌을 판정한다.</summary>
        private static double SegmentDistance(SimVector start, SimVector end, SimVector point)
        { SimVector segment = end - start; double progress = segment.LengthSquared < 0.000001 ? 0 : Math.Max(0, Math.Min(1, SimVector.Dot(point - start, segment) / segment.LengthSquared)); return SimVector.Distance(start + segment * progress, point); }

        /// <summary>시전의 모든 실행 분기를 읽어 노이즈가 있을 때만 시전 단위 속성 선택이 필요한지 반환한다.</summary>
        private static bool ContainsNoise(IReadOnlyList<SpellAction> actions)
        {
            foreach (SpellAction action in actions)
                if (action.Noise || (action.CalledSpell != null && ContainsNoise(action.CalledSpell.Root))
                    || ContainsNoise(action.OnHit) || ContainsNoise(action.OnExpire) || ContainsNoise(action.OnFirstHitOrExpire)
                    || ContainsNoise(action.Then) || ContainsNoise(action.Else) || ContainsNoise(action.Body) || ContainsNoise(action.OnComplete)
                    || ContainsNoise(action.Next)) return true;
            return false;
        }

        private readonly struct SpellContext
        {
            private readonly SimVector _origin;
            private readonly SimVector _direction;
            private readonly SimulationEnemy _target;
            private readonly bool _fromEvent;
            private readonly string _noiseElement;
            private readonly SpellModifierValues _modifiers;
            private readonly SpellEventScope _callEvents;
            private readonly RepeatScope _repeat;
            private readonly double _costMultiplier;
            private readonly SimulationPlayer _caster;
            private readonly int _sourceEntityId;
            public SimVector Origin => _origin;
            public SimVector Direction => _direction;
            public SimulationEnemy Target => _target;
            public bool FromEvent => _fromEvent;
            public string NoiseElement => _noiseElement;
            public SpellModifierValues Modifiers => _modifiers;
            public SpellEventScope CallEvents => _callEvents;

            /// <summary>이 실행 경로가 속한 가장 안쪽 Repeat다. 개체 이벤트로 시작한 경로는 Repeat에 속하지 않는다.</summary>
            public RepeatScope Repeat => _repeat;

            /// <summary>이 경로의 노드 비용에 곱하는 배율이다. 프리셋 호출 안에서는 호출 노드 효과의 비용 배율이 곱해진다.</summary>
            public double CostMultiplier => _costMultiplier;

            /// <summary>마법을 시전한 주체다. Apply 등 자기 적용 효과의 대상이다.</summary>
            public SimulationPlayer Caster => _caster;

            /// <summary>이 경로를 시작한 이벤트의 원본 개체 ID다. 시전·흐름 제어로 시작한 경로는 0이다.</summary>
            public int SourceEntityId => _sourceEntityId;

            /// <summary>
            /// 발생 위치, 방향, 선택 대상, 이벤트 유래 여부, 소속 Repeat, 비용 배율, 시전자와 이벤트 원본을 예약 가능한 스냅샷으로 보관한다.
            /// </summary>
            public SpellContext(SimVector origin, SimVector direction, SimulationEnemy target, bool fromEvent,
                string noiseElement, SpellModifierValues modifiers = null, SpellEventScope callEvents = null, RepeatScope repeat = null,
                double costMultiplier = 1d, SimulationPlayer caster = null, int sourceEntityId = 0)
            {
                _origin = origin; _direction = direction; _target = target; _fromEvent = fromEvent; _noiseElement = noiseElement;
                _modifiers = modifiers ?? SpellModifierValues.None; _callEvents = callEvents; _repeat = repeat; _costMultiplier = costMultiplier;
                _caster = caster; _sourceEntityId = sourceEntityId;
            }

            /// <summary>소속 Repeat만 바꾼 문맥을 반환한다.</summary>
            public SpellContext WithRepeat(RepeatScope repeat)
                => new SpellContext(_origin, _direction, _target, _fromEvent, _noiseElement, _modifiers, _callEvents, repeat, _costMultiplier, _caster, _sourceEntityId);

            /// <summary>실행 위치만 바꾼 문맥을 반환한다.</summary>
            public SpellContext WithOrigin(SimVector origin)
                => new SpellContext(origin, _direction, _target, _fromEvent, _noiseElement, _modifiers, _callEvents, _repeat, _costMultiplier, _caster, _sourceEntityId);
        }

        /// <summary>
        /// 실행 중인 Repeat 하나의 완료 추적 상태다. 대기 수는 아직 끝나지 않은 회차 예약·Delay 예약·안쪽 Repeat와
        /// 회차 예약 단계 자체를 센다. 0이 되면 완료이며, 실패 표시가 있으면 OnComplete를 실행하지 않는다.
        /// </summary>
        private sealed class RepeatScope
        {
            private readonly IReadOnlyList<SpellAction> _onComplete;
            private readonly SpellContext _completeContext;
            private readonly RepeatScope _parent;
            private int _pending;
            private bool _isFailed;
            public IReadOnlyList<SpellAction> OnComplete => _onComplete;
            public SpellContext CompleteContext => _completeContext;
            public RepeatScope Parent => _parent;
            public bool IsFailed => _isFailed;

            /// <summary>완료 분기와 Repeat 진입 문맥(바깥 Repeat 소속), 바깥 Repeat로 추적 상태를 만든다.</summary>
            public RepeatScope(IReadOnlyList<SpellAction> onComplete, SpellContext entryContext, RepeatScope parent)
            {
                _onComplete = onComplete;
                _completeContext = entryContext;
                _parent = parent;
            }

            /// <summary>기다릴 작업을 하나 추가한다.</summary>
            public void Acquire() { _pending++; }

            /// <summary>기다리던 작업 하나를 끝내고 모든 작업이 끝났으면 true를 반환한다.</summary>
            public bool Release() { _pending--; return _pending == 0; }

            /// <summary>회차 실패를 표시한다. 남은 회차는 실행하지 않으며 OnComplete도 실행하지 않는다.</summary>
            public void Fail() { _isFailed = true; }

            /// <summary>대기 수와 실패 여부를 바깥 Repeat까지 결정성 해시 버퍼에 기록한다.</summary>
            public void AppendState(StringBuilder state)
            {
                for (RepeatScope scope = this; scope != null; scope = scope._parent)
                    state.Append("|r").Append(scope._pending).Append(scope._isFailed ? 'f' : 'o');
            }
        }

        private readonly struct ScheduledExecution
        {
            private readonly int _tick;
            private readonly IReadOnlyList<SpellAction> _actions;
            private readonly SpellContext _context;
            private readonly bool _isIteration;
            public int Tick => _tick;
            public IReadOnlyList<SpellAction> Actions => _actions;
            public SpellContext Context => _context;

            /// <summary>Repeat의 남은 회차 예약인지 나타낸다. 소속 Repeat가 실패하면 실행하지 않는다.</summary>
            public bool IsIteration => _isIteration;

            /// <summary>목표 틱, 실행 목록, 컨텍스트와 Repeat 회차 여부를 예약 실행 데이터에 보관한다.</summary>
            public ScheduledExecution(int tick, IReadOnlyList<SpellAction> actions, SpellContext context, bool isIteration)
            { _tick = tick; _actions = actions; _context = context; _isIteration = isIteration; }
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
