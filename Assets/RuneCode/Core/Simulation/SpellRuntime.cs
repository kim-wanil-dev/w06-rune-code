using System;
using System.Collections.Generic;
using System.Text;

namespace RuneCode
{
    /// <summary>실행 그래프 위에서 토큰의 시전, 노드 도착 처리, 합류 대기, 발사 요청을 관리하는 마법 런타임이다.</summary>
    public sealed class SpellRuntime
    {
        private const int NO_TICK = -1;
        private const int NO_PORT = -1;
        private const int PORT_COUNT = 2;
        private const int FIRST_PORT = 0;
        private const int SECOND_PORT = 1;
        private const double PERCENT_SCALE = 100.0;
        private const double DEGREES_TO_RADIANS = Math.PI / 180.0;
        private const double SEGMENT_EPSILON = 0.000001;
        private const double RANGE_EPSILON = 1e-9;
        private const double WAIT_EPSILON = 1e-9;

        private enum ArriveResult { Continue, Ended, Waiting, Merged }

        private readonly SpellSettings _settings;
        private readonly double _tileSize;
        private readonly Action<int> _nodePaid;
        private readonly Action<SimulationEnemy, SpellProjectile> _projectileHit;
        private readonly List<SpellToken> _tokens = new List<SpellToken>();
        private readonly List<SpellProjectile> _projectiles = new List<SpellProjectile>();
        private readonly List<TokenEndEvent> _endEvents = new List<TokenEndEvent>();
        private readonly List<FireRequest> _fireRequests = new List<FireRequest>();
        private readonly List<FireBatch> _batches = new List<FireBatch>();
        private readonly List<FireBatch> _batchPool = new List<FireBatch>();
        private readonly Queue<SpellToken> _pendingClones = new Queue<SpellToken>();
        private SpellProgram _program;
        private List<SpellToken>[] _joinQueues = new List<SpellToken>[0];
        private double[] _lastPaidCost = new double[0];
        private int[] _lastPaidTick = new int[0];
        private int _activeTokenCount;
        private int _nextTokenId;
        private int _nextCastId;
        private int _nextProjectileId;
        private CastResult _lastCastResult = CastResult.None;
        private int _lastCastTick = NO_TICK;
        private int _lastOverloadTick = NO_TICK;

        public SpellProgram Program => _program;
        public IReadOnlyList<SpellToken> Tokens => _tokens;
        public IReadOnlyList<SpellProjectile> Projectiles => _projectiles;
        public IReadOnlyList<TokenEndEvent> EndEvents => _endEvents;
        public int ActiveTokenCount => _activeTokenCount;
        public CastResult LastCastResult => _lastCastResult;
        public int LastCastTick => _lastCastTick;
        public int LastOverloadTick => _lastOverloadTick;
        internal IReadOnlyList<FireRequest> FireRequests => _fireRequests;

        /// <summary>마법 설정, 타일 크기, 노드 지불 알림 콜백, 투사체 적중 콜백을 받아 프로그램이 없는 빈 런타임을 생성한다. 콜백이 null이면 알림을 보내지 않는다.</summary>
        public SpellRuntime(SpellSettings settings, double tileSize, Action<int> nodePaid, Action<SimulationEnemy, SpellProjectile> projectileHit)
        {
            _settings = settings;
            _tileSize = tileSize;
            _nodePaid = nodePaid;
            _projectileHit = projectileHit;
        }

        /// <summary>노드 색인의 마지막 지불 비용을 반환하며, 지불 기록이 없으면 NaN을 반환한다.</summary>
        public double GetLastPaidCost(int node) => _lastPaidCost[node];

        /// <summary>노드 색인의 마지막 지불 틱을 반환하며, 지불 기록이 없으면 -1을 반환한다.</summary>
        public int GetLastPaidTick(int node) => _lastPaidTick[node];

        /// <summary>실행 상태를 모두 지운 뒤 새 프로그램을 저장하고, 노드 수에 맞춰 지불 기록과 합류 대기열을 새로 만든다. 프로그램이 null이면 노드 수는 0이다.</summary>
        internal void SetProgram(SpellProgram program)
        {
            Clear();
            _program = program;
            int nodeCount = program == null ? 0 : program.NodeCount;
            _lastPaidCost = new double[nodeCount];
            _lastPaidTick = new int[nodeCount];
            _joinQueues = new List<SpellToken>[nodeCount * PORT_COUNT];
            for (int node = 0; node < nodeCount; node++)
            {
                _lastPaidCost[node] = double.NaN;
                _lastPaidTick[node] = NO_TICK;
            }
            for (int slot = 0; slot < _joinQueues.Length; slot++) _joinQueues[slot] = new List<SpellToken>();
        }

        /// <summary>토큰, 합류 대기열, 발사 요청, 투사체, 소멸 기록을 모두 비우고 노드 지불 기록을 초기화한다. 마나는 돌려주지 않는다.</summary>
        internal void Clear()
        {
            _tokens.Clear();
            _pendingClones.Clear();
            _fireRequests.Clear();
            _projectiles.Clear();
            _endEvents.Clear();
            for (int slot = 0; slot < _joinQueues.Length; slot++) _joinQueues[slot].Clear();
            for (int node = 0; node < _lastPaidCost.Length; node++)
            {
                _lastPaidCost[node] = double.NaN;
                _lastPaidTick[node] = NO_TICK;
            }
            _activeTokenCount = 0;
        }

        /// <summary>시전 노드의 적재량을 플레이어 마나에서 차감하고 시전 토큰을 만든다. 프로그램이 유효하지 않으면 Invalid, 마나가 부족하면 NoMana, 토큰 상한이면 Overload를 반환하며 실패 시 차감하지 않는다.</summary>
        internal CastResult TryCast(SimulationPlayer player, int tick)
        {
            if (_program == null || !_program.IsValid) return SetCastResult(CastResult.Invalid, tick);
            int castNode = _program.CastNode;
            double load = _program.GetLoad(castNode);
            if (player.Energy < load) return SetCastResult(CastResult.NoMana, tick);
            if (_activeTokenCount >= _settings.MaxTokens)
            {
                _lastOverloadTick = tick;
                return SetCastResult(CastResult.Overload, tick);
            }
            if (!player.TrySpendEnergy(load)) return SetCastResult(CastResult.NoMana, tick);

            SpellToken token = new SpellToken(NewTokenId(), _nextCastId++, castNode, _program.NodeCount);
            token.NextPort = FIRST_PORT;
            token.RemainingWait = _program.GetDwell(castNode);
            token.Mana = load;
            token.Power = _settings.BasePower;
            token.Count = 1;
            token.IsCharacterOrigin = true;
            token.BirthTick = tick;
            _tokens.Add(token);
            _activeTokenCount++;
            return SetCastResult(CastResult.Success, tick);
        }

        /// <summary>이번 틱의 토큰을 생성 순서대로 처리한다. 분배 복제 토큰은 원본 처리 직후 같은 틱에 진행 루프부터 처리하고, 끝난 토큰은 마지막에 목록에서 뺀다.</summary>
        internal void ProcessTokens(int tick, double dt, SimVector playerPosition, IReadOnlyList<SimulationEnemy> enemies)
        {
            if (_program == null) return;
            for (int index = 0; index < _tokens.Count; index++)
            {
                SpellToken token = _tokens[index];
                if (token.IsEnded || token.ProcessedTick == tick) continue;
                if (token.IsFromHit && token.BirthTick == tick) continue;
                ProcessOne(token, tick, dt, playerPosition, enemies);
                while (_pendingClones.Count > 0)
                {
                    SpellToken clone = _pendingClones.Dequeue();
                    clone.ProcessedTick = tick;
                    RunLoop(clone, tick, playerPosition, enemies);
                }
            }
            RemoveEndedTokens();
        }

        /// <summary>표시 시간이 지난 소멸 기록을 앞에서부터 제거한다. 기준은 설정의 소멸 표시 시간이다.</summary>
        internal void TrimEvents(int tick)
        {
            double limitTicks = (double)_settings.EndEventSeconds * RuneSimulation.TICK_RATE;
            int removeCount = 0;
            while (removeCount < _endEvents.Count && tick - _endEvents[removeCount].Tick > limitTicks) removeCount++;
            if (removeCount > 0) _endEvents.RemoveRange(0, removeCount);
        }

        /// <summary>토큰을 생성 순서대로, 합류 대기열을 노드와 포트 순서대로, 발사 요청 수와 투사체를 결정성 해시 버퍼에 기록한다.</summary>
        internal void WriteState(StringBuilder state)
        {
            state.Append(FormattableString.Invariant($"|tokens:{_tokens.Count}"));
            for (int index = 0; index < _tokens.Count; index++)
            {
                state.Append(';');
                _tokens[index].WriteState(state);
            }
            for (int slot = 0; slot < _joinQueues.Length; slot++)
            {
                List<SpellToken> queue = _joinQueues[slot];
                state.Append(FormattableString.Invariant($"|join{slot}:"));
                for (int index = 0; index < queue.Count; index++)
                {
                    if (index > 0) state.Append(',');
                    state.Append(queue[index].Id);
                }
            }
            state.Append(FormattableString.Invariant($"|fire:{_fireRequests.Count}|projectiles:{_projectiles.Count}"));
            for (int index = 0; index < _projectiles.Count; index++)
            {
                state.Append(';');
                _projectiles[index].WriteState(state);
            }
        }

        /// <summary>시전 결과와 틱을 마지막 시전 기록으로 저장하고 같은 결과를 반환한다.</summary>
        private CastResult SetCastResult(CastResult result, int tick)
        {
            _lastCastResult = result;
            _lastCastTick = tick;
            return result;
        }

        /// <summary>다음 토큰 id를 발급하고 카운터를 1 늘린 값을 반환한다.</summary>
        private int NewTokenId() => _nextTokenId++;

        /// <summary>토큰 하나의 경과 시간을 갱신하고 수명 초과와 합류 대기 시간 초과를 처리한 뒤 대기 시간을 줄이고 진행 루프를 돈다.</summary>
        private void ProcessOne(SpellToken token, int tick, double dt, SimVector playerPosition, IReadOnlyList<SimulationEnemy> enemies)
        {
            token.ProcessedTick = tick;
            token.Elapsed += dt;
            if (token.Elapsed > _settings.TokenLifetime)
            {
                End(token, TokenEndReason.Expired, token.Node, 0.0, token.Mana, tick);
                return;
            }
            if (token.IsJoinWaiting)
            {
                token.JoinWait += dt;
                if (token.JoinWait < _settings.JoinMaxWait) return;
                RemoveFromJoinQueue(token);
                token.IsJoinWaiting = false;
                token.RemainingWait = 0.0;
                token.NextPort = FIRST_PORT;
            }
            token.RemainingWait -= dt;
            RunLoop(token, tick, playerPosition, enemies);
        }

        /// <summary>대기 시간이 남아 있지 않은 동안 다음 노드로 진행하며 도착 처리를 반복한다. 한 번에 설정의 최대 노드 수까지만 진행하고 나머지는 다음 틱에 이어 간다.</summary>
        private void RunLoop(SpellToken token, int tick, SimVector playerPosition, IReadOnlyList<SimulationEnemy> enemies)
        {
            int steps = 0;
            while (steps < _settings.MaxNodesPerFrame)
            {
                // 1/60초 누적 뺄셈의 반올림 잔차로 도착이 한 틱 밀리지 않게 허용 오차를 둔다.
                if (token.RemainingWait > WAIT_EPSILON) return;
                if (!_program.TryGetNext(token.Node, token.NextPort, out int target, out int inPort))
                {
                    End(token, TokenEndReason.Finished, token.Node, 0.0, token.Mana, tick);
                    return;
                }
                steps++;
                token.Node = target;
                ArriveResult result = Arrive(token, target, inPort, tick, playerPosition, enemies);
                if (result != ArriveResult.Continue) return;
                token.RemainingWait += _program.GetDwell(target);
            }
        }

        /// <summary>토큰이 노드에 도착한 순간 비용을 계산해 지불하고 노드 효과를 적용한다. 지불하지 못하면 소멸하고, 합류 대기나 합쳐짐이면 그 결과를 반환한다.</summary>
        private ArriveResult Arrive(SpellToken token, int node, int inPort, int tick, SimVector playerPosition, IReadOnlyList<SimulationEnemy> enemies)
        {
            SpellNodeKind kind = _program.GetKind(node);
            double cost = SpellNodes.GetCost(kind, token.Power, token.Count, _program.GetRatio(node), _settings);
            if (token.Mana < cost)
            {
                End(token, TokenEndReason.Unpayable, node, cost, token.Mana, tick);
                return ArriveResult.Ended;
            }
            token.Mana -= cost;
            token.AddPass(node);
            _lastPaidCost[node] = cost;
            _lastPaidTick[node] = tick;
            _nodePaid?.Invoke(node);
            token.NextPort = FIRST_PORT;
            switch (kind)
            {
                case SpellNodeKind.Projectile:
                    return ArriveProjectile(token, node, tick);
                case SpellNodeKind.Amplify:
                    token.Power *= _program.GetRatio(node);
                    break;
                case SpellNodeKind.Add:
                    token.Power += _settings.AddPower;
                    break;
                case SpellNodeKind.Split:
                    token.Count += 1;
                    token.Power *= _settings.SplitPowerFactor;
                    break;
                case SpellNodeKind.Fork:
                    ApplyFork(token, node, tick);
                    break;
                case SpellNodeKind.Join:
                    return ApplyJoin(token, node, inPort, tick);
                case SpellNodeKind.Branch:
                    token.NextPort = EvaluateBranch(token, node, playerPosition, enemies) ? FIRST_PORT : SECOND_PORT;
                    break;
            }
            return ArriveResult.Continue;
        }

        /// <summary>투사체 노드 도착 처리다. 출력 엣지가 없으면 종착 발현으로 남은 마나를 발이 나눠 운반하게 요청하고 토큰을 끝낸다. 엣지가 있으면 운반 마나 0으로 요청만 만든다.</summary>
        private ArriveResult ArriveProjectile(SpellToken token, int node, int tick)
        {
            if (!_program.TryGetNext(node, FIRST_PORT, out _, out _))
            {
                double carried = token.Mana / token.Count;
                AddFireRequest(token, carried);
                token.Mana = 0.0;
                End(token, TokenEndReason.Terminal, node, 0.0, 0.0, tick);
                return ArriveResult.Ended;
            }
            AddFireRequest(token, 0.0);
            return ArriveResult.Continue;
        }

        /// <summary>분배 노드의 마나를 비율로 나눈다. 원본은 앞쪽 몫으로 포트 0을 따르고, 상한 안이면 뒤쪽 몫의 복제 토큰을 포트 1로 만들어 대기 목록에 넣는다. 상한이면 뒤쪽 몫을 버리고 과부하 틱을 기록한다.</summary>
        private void ApplyFork(SpellToken token, int node, int tick)
        {
            double keep = token.Mana * _program.GetShare(node);
            double rest = token.Mana - keep;
            token.Mana = keep;
            if (_activeTokenCount >= _settings.MaxTokens)
            {
                _lastOverloadTick = tick;
                return;
            }
            SpellToken clone = token.CloneForFork(NewTokenId(), rest);
            clone.NextPort = SECOND_PORT;
            clone.RemainingWait = token.RemainingWait + _program.GetDwell(node);
            clone.ProcessedTick = tick;
            _tokens.Add(clone);
            _activeTokenCount++;
            _pendingClones.Enqueue(clone);
        }

        /// <summary>합류 노드 도착 처리다. 반대 포트 대기열 맨 앞 토큰이 있으면 그 토큰이 이 토큰을 흡수하고 이 토큰은 합쳐짐으로 끝난다. 없으면 이 토큰이 도착한 포트의 대기열 끝에 선다.</summary>
        private ArriveResult ApplyJoin(SpellToken token, int node, int inPort, int tick)
        {
            int otherPort = inPort == FIRST_PORT ? SECOND_PORT : FIRST_PORT;
            List<SpellToken> waiting = _joinQueues[node * PORT_COUNT + otherPort];
            if (waiting.Count > 0)
            {
                SpellToken absorber = waiting[0];
                waiting.RemoveAt(0);
                absorber.Mana += token.Mana;
                absorber.Power += token.Power;
                absorber.Count += token.Count;
                absorber.IsJoinWaiting = false;
                absorber.JoinWait = 0.0;
                absorber.JoinPort = NO_PORT;
                absorber.RemainingWait = _program.GetDwell(node);
                absorber.NextPort = FIRST_PORT;
                token.Mana = 0.0;
                End(token, TokenEndReason.Merged, node, 0.0, 0.0, tick);
                return ArriveResult.Merged;
            }
            token.IsJoinWaiting = true;
            token.JoinPort = inPort;
            token.JoinWait = 0.0;
            _joinQueues[node * PORT_COUNT + inPort].Add(token);
            return ArriveResult.Waiting;
        }

        /// <summary>토큰을 끝내고 상태를 정리한다. 이미 끝난 토큰이면 무시하고, 합류 대기 중이었다면 대기열에서 뺀다. 지불 불가와 수명 초과만 표시용 소멸 기록을 남긴다.</summary>
        private void End(SpellToken token, TokenEndReason reason, int node, double need, double have, int tick)
        {
            if (token.IsEnded) return;
            token.IsEnded = true;
            _activeTokenCount--;
            if (token.IsJoinWaiting)
            {
                RemoveFromJoinQueue(token);
                token.IsJoinWaiting = false;
            }
            if (reason == TokenEndReason.Unpayable || reason == TokenEndReason.Expired)
                _endEvents.Add(new TokenEndEvent(node, reason, need, have, tick));
        }

        /// <summary>합류 대기 중인 토큰을 자기 노드와 대기 포트의 대기열에서 제거한다.</summary>
        private void RemoveFromJoinQueue(SpellToken token)
        {
            _joinQueues[token.Node * PORT_COUNT + token.JoinPort].Remove(token);
        }

        /// <summary>토큰의 위력, 개수, 세대, 무시할 적, 시전 id와 원점을 담은 발사 요청을 이번 틱의 요청 목록 끝에 추가한다.</summary>
        private void AddFireRequest(SpellToken token, double carriedMana)
        {
            _fireRequests.Add(new FireRequest(token.Id, token.Count, token.Power, carriedMana, token.Generation, token.IgnoreEnemyId, token.CastId, token.IsCharacterOrigin, token.OriginPoint, token.Direction));
        }

        /// <summary>토큰이 도착한 순간의 조건 값을 읽어 분기 기준값과 비교한 결과를 반환한다. 대상 조건은 원점에서 가장 가까운 살아 있는 적을 쓰며, 그런 적이 없으면 거짓을 반환한다.</summary>
        private bool EvaluateBranch(SpellToken token, int node, SimVector playerPosition, IReadOnlyList<SimulationEnemy> enemies)
        {
            BranchCondition condition = _program.GetCondition(node);
            double value;
            switch (condition)
            {
                case BranchCondition.TargetDistance:
                case BranchCondition.TargetHpPercent:
                {
                    SimVector origin = token.IsCharacterOrigin ? playerPosition : token.OriginPoint;
                    SimulationEnemy target = FindNearestAliveEnemy(origin, enemies);
                    if (target == null) return false;
                    value = condition == BranchCondition.TargetDistance
                        ? SimVector.Distance(origin, target.Position)
                        : target.Hp / target.MaxHp * PERCENT_SCALE;
                    break;
                }
                case BranchCondition.Mana:
                    value = token.Mana;
                    break;
                case BranchCondition.Power:
                    value = token.Power;
                    break;
                case BranchCondition.Generation:
                    value = token.Generation;
                    break;
                default:
                    value = token.GetPassCount(node);
                    break;
            }
            double threshold = _program.GetThreshold(node);
            return _program.IsGreaterOrEqual(node) ? value >= threshold : value <= threshold;
        }

        /// <summary>주어진 원점에서 가장 가까운 살아 있는 적을 반환한다. 거리가 같으면 목록 앞쪽 적을 고르며, 살아 있는 적이 없으면 null을 반환한다.</summary>
        private static SimulationEnemy FindNearestAliveEnemy(SimVector origin, IReadOnlyList<SimulationEnemy> enemies)
        {
            SimulationEnemy nearest = null;
            double nearestSquared = double.MaxValue;
            for (int index = 0; index < enemies.Count; index++)
            {
                SimulationEnemy enemy = enemies[index];
                if (!enemy.IsAlive) continue;
                double squared = (enemy.Position - origin).LengthSquared;
                if (squared < nearestSquared)
                {
                    nearest = enemy;
                    nearestSquared = squared;
                }
            }
            return nearest;
        }

        /// <summary>끝난 토큰을 생성 순서를 유지한 채 목록에서 제거한다. 앞으로 당기는 압축으로 할당 없이 처리한다.</summary>
        private void RemoveEndedTokens()
        {
            int write = 0;
            for (int read = 0; read < _tokens.Count; read++)
            {
                SpellToken token = _tokens[read];
                if (token.IsEnded) continue;
                _tokens[write] = token;
                write++;
            }
            _tokens.RemoveRange(write, _tokens.Count - write);
        }

        /// <summary>이번 틱의 발사 요청을 위치, 방향과 토큰 조건으로 묶고, 묶음마다 부채꼴로 투사체를 만든 뒤 요청 목록을 비운다.</summary>
        internal void FlushFireRequests(int tick, SimVector playerPosition, SimVector cursorDirection, IReadOnlyList<SimulationEnemy> enemies)
        {
            if (_fireRequests.Count == 0) return;
            for (int index = 0; index < _fireRequests.Count; index++)
            {
                FireRequest request = _fireRequests[index];
                ResolveFireOrigin(request, playerPosition, cursorDirection, enemies, out SimVector position, out SimVector direction);
                FireBatch batch = FindMatchingBatch(request, position, direction);
                if (batch == null)
                {
                    batch = AcquireBatch();
                    batch.Reset(position, direction);
                    _batches.Add(batch);
                }
                batch.AddMember(index, request.Count);
            }
            for (int index = 0; index < _batches.Count; index++) SpawnBatch(_batches[index], tick);
            ReleaseBatches();
            _fireRequests.Clear();
        }

        /// <summary>투사체 전체를 한 틱 전진시키며 벽, 사거리, 적 충돌을 처리한다. 적중하면 피해 콜백을 부르고 적중 토큰을 만든 뒤 투사체를 빼며, 끝난 투사체는 순서를 유지한 채 목록에서 뺀다.</summary>
        internal void AdvanceProjectiles(int tick, double dt, MissionMap map, IReadOnlyList<SimulationEnemy> enemies)
        {
            double speed = _settings.ProjectileSpeedTiles * _tileSize;
            double range = _settings.ProjectileRangeTiles * _tileSize;
            double radius = _settings.ProjectileRadius;
            int write = 0;
            for (int read = 0; read < _projectiles.Count; read++)
            {
                SpellProjectile projectile = _projectiles[read];
                if (!AdvanceProjectile(projectile, tick, dt, speed, range, radius, map, enemies)) continue;
                _projectiles[write] = projectile;
                write++;
            }
            _projectiles.RemoveRange(write, _projectiles.Count - write);
        }

        /// <summary>발사 요청의 발사 위치와 기준 방향을 구한다. 캐릭터 원점은 플레이어 위치와 시전 조준 모드를, 지점 원점은 토큰이 저장한 좌표와 방향을 쓴다.</summary>
        private void ResolveFireOrigin(FireRequest request, SimVector playerPosition, SimVector cursorDirection, IReadOnlyList<SimulationEnemy> enemies, out SimVector position, out SimVector direction)
        {
            if (!request.IsCharacterOrigin)
            {
                position = request.Point;
                direction = request.Direction;
                return;
            }
            position = playerPosition;
            direction = cursorDirection.Normalized();
            if (_program.GetAim(_program.CastNode) != AimMode.Nearest) return;
            SimulationEnemy target = FindNearestAliveEnemy(playerPosition, enemies);
            if (target != null) direction = (target.Position - playerPosition).Normalized();
        }

        /// <summary>발사 위치와 방향이 묶음 허용 범위 안이고 같은 토큰의 요청이 없는 첫 묶음을 반환하며, 없으면 null을 반환한다.</summary>
        private FireBatch FindMatchingBatch(FireRequest request, SimVector position, SimVector direction)
        {
            double positionTolerance = _settings.BatchPositionTolerance;
            double angleTolerance = _settings.BatchAngleToleranceDegrees;
            for (int index = 0; index < _batches.Count; index++)
            {
                FireBatch batch = _batches[index];
                if (SimVector.Distance(batch.Position, position) > positionTolerance) continue;
                if (GetAngleDegrees(batch.Direction, direction) > angleTolerance) continue;
                if (HasTokenMember(batch, request.TokenId)) continue;
                return batch;
            }
            return null;
        }

        /// <summary>묶음 안에 같은 토큰 id의 발사 요청이 이미 있으면 true를 반환한다.</summary>
        private bool HasTokenMember(FireBatch batch, int tokenId)
        {
            List<int> members = batch.Members;
            for (int index = 0; index < members.Count; index++)
                if (_fireRequests[members[index]].TokenId == tokenId) return true;
            return false;
        }

        /// <summary>두 방향 벡터 사이의 각도를 도 단위의 0 이상 값으로 반환한다.</summary>
        private static double GetAngleDegrees(SimVector first, SimVector second)
        {
            double cross = first.X * second.Y - first.Y * second.X;
            double dot = SimVector.Dot(first, second);
            return Math.Abs(Math.Atan2(cross, dot)) / DEGREES_TO_RADIANS;
        }

        /// <summary>풀에서 묶음 객체를 꺼내 반환하며, 풀이 비어 있을 때만 새로 만든다.</summary>
        private FireBatch AcquireBatch()
        {
            if (_batchPool.Count == 0) return new FireBatch();
            int last = _batchPool.Count - 1;
            FireBatch batch = _batchPool[last];
            _batchPool.RemoveAt(last);
            return batch;
        }

        /// <summary>이번 틱의 묶음 객체를 모두 비운 뒤 풀로 되돌린다.</summary>
        private void ReleaseBatches()
        {
            for (int index = 0; index < _batches.Count; index++)
            {
                _batches[index].Clear();
                _batchPool.Add(_batches[index]);
            }
            _batches.Clear();
        }

        /// <summary>묶음의 총 발 수에 맞춰 부채꼴 간격을 정하고, 요청 순서대로 자리를 배정해 투사체를 만든다. 투사체 상한을 넘는 발은 만들지 않고 과부하 틱을 기록한다.</summary>
        private void SpawnBatch(FireBatch batch, int tick)
        {
            int total = batch.TotalCount;
            double step = _settings.FanStepDegrees;
            double maxSpread = _settings.FanMaxDegrees;
            if ((total - 1) * step > maxSpread) step = maxSpread / total;
            int slot = 0;
            List<int> members = batch.Members;
            for (int member = 0; member < members.Count; member++)
            {
                FireRequest request = _fireRequests[members[member]];
                for (int shot = 0; shot < request.Count; shot++)
                {
                    double angle = (-(total - 1) / 2.0 + slot) * step;
                    slot++;
                    if (_projectiles.Count >= _settings.MaxProjectiles)
                    {
                        _lastOverloadTick = tick;
                        continue;
                    }
                    SpellProjectile projectile = new SpellProjectile(_nextProjectileId++);
                    projectile.CastId = request.CastId;
                    projectile.Power = request.Power;
                    projectile.CarriedMana = request.CarriedMana;
                    projectile.Generation = request.Generation;
                    projectile.IgnoreEnemyId = request.IgnoreEnemyId;
                    projectile.Position = batch.Position;
                    projectile.Direction = batch.Direction.Rotated(angle * DEGREES_TO_RADIANS);
                    projectile.BirthTick = tick;
                    _projectiles.Add(projectile);
                }
            }
        }

        /// <summary>투사체 하나를 한 틱 전진시키고 충돌을 처리한다. 새 투사체는 이번 틱에 움직이지 않으며, 적중·벽·사거리 초과면 제거하고 살아남으면 true를 반환한다.</summary>
        private bool AdvanceProjectile(SpellProjectile projectile, int tick, double dt, double speed, double range, double radius, MissionMap map, IReadOnlyList<SimulationEnemy> enemies)
        {
            if (projectile.BirthTick == tick) return true;
            double stepLength = Math.Min(speed * dt, range - projectile.Traveled);
            SimVector start = projectile.Position;
            SimVector next = start + projectile.Direction * stepLength;
            bool isBlocked = !map.CanOccupy(next, radius);
            SimVector end = isBlocked ? map.Move(start, next - start, radius) : next;
            SimulationEnemy target = FindFirstHitEnemy(projectile, start, end, radius, enemies);
            if (target != null)
            {
                SimVector hitPoint = ClosestPointOnSegment(start, end, target.Position);
                projectile.Traveled += SimVector.Distance(start, hitPoint);
                projectile.Position = hitPoint;
                _projectileHit?.Invoke(target, projectile);
                if (projectile.CarriedMana > 0.0 && _program != null && _program.OnHitNode >= 0) CreateHitToken(projectile, target, hitPoint, tick);
                return false;
            }
            projectile.Position = end;
            if (isBlocked) return false;
            projectile.Traveled += stepLength;
            return projectile.Traveled < range - RANGE_EPSILON;
        }

        /// <summary>이동 선분에 닿은 살아 있는 적 가운데 투사체가 무시하는 적을 뺀 가장 가까운 적을 반환하며, 없으면 null을 반환한다. 거리가 같으면 목록 앞쪽 적을 고른다.</summary>
        private static SimulationEnemy FindFirstHitEnemy(SpellProjectile projectile, SimVector start, SimVector end, double radius, IReadOnlyList<SimulationEnemy> enemies)
        {
            SimulationEnemy hit = null;
            double nearestSquared = double.MaxValue;
            for (int index = 0; index < enemies.Count; index++)
            {
                SimulationEnemy enemy = enemies[index];
                if (!enemy.IsAlive || enemy.Id == projectile.IgnoreEnemyId) continue;
                if (SegmentDistance(start, end, enemy.Position) > radius + enemy.Radius) continue;
                double squared = (enemy.Position - start).LengthSquared;
                if (squared < nearestSquared)
                {
                    hit = enemy;
                    nearestSquared = squared;
                }
            }
            return hit;
        }

        /// <summary>선분 위에서 점과 가장 가까운 좌표를 반환하며, 선분 길이 제곱이 기준 미만이면 시작점을 반환한다.</summary>
        private static SimVector ClosestPointOnSegment(SimVector start, SimVector end, SimVector point)
        {
            SimVector segment = end - start;
            double lengthSquared = segment.LengthSquared;
            if (lengthSquared < SEGMENT_EPSILON) return start;
            double progress = Math.Max(0.0, Math.Min(1.0, SimVector.Dot(point - start, segment) / lengthSquared));
            return start + segment * progress;
        }

        /// <summary>선분과 점 사이의 최단 거리를 반환한다.</summary>
        private static double SegmentDistance(SimVector start, SimVector end, SimVector point)
        {
            return SimVector.Distance(ClosestPointOnSegment(start, end, point), point);
        }

        /// <summary>적중한 투사체의 운반 마나로 적중 노드에서 시작하는 토큰을 만든다. 개수는 1, 세대는 한 늘고, 맞은 적을 무시한다. 토큰 상한이면 만들지 않고 과부하 틱을 기록하며, 어느 경우든 투사체의 운반 마나는 0이 된다.</summary>
        private void CreateHitToken(SpellProjectile projectile, SimulationEnemy enemy, SimVector hitPoint, int tick)
        {
            double carriedMana = projectile.CarriedMana;
            projectile.CarriedMana = 0.0;
            if (_activeTokenCount >= _settings.MaxTokens)
            {
                _lastOverloadTick = tick;
                return;
            }
            SpellToken token = new SpellToken(NewTokenId(), projectile.CastId, _program.OnHitNode, _program.NodeCount);
            token.NextPort = FIRST_PORT;
            token.RemainingWait = 0.0;
            token.Mana = carriedMana;
            token.Power = projectile.Power;
            token.Count = 1;
            token.IsCharacterOrigin = false;
            token.OriginPoint = hitPoint;
            token.Direction = projectile.Direction;
            token.Generation = projectile.Generation + 1;
            token.Elapsed = 0.0;
            token.IgnoreEnemyId = enemy.Id;
            token.BirthTick = tick;
            token.IsFromHit = true;
            _tokens.Add(token);
            _activeTokenCount++;
        }

        /// <summary>같은 위치와 방향으로 한 틱에 나갈 발사 요청을 모은 묶음이다. 풀에서 재사용하며, 요청 색인 목록과 총 발 수를 가진다.</summary>
        private sealed class FireBatch
        {
            private readonly List<int> _members = new List<int>();
            private SimVector _position;
            private SimVector _direction;
            private int _totalCount;

            public List<int> Members => _members;
            public SimVector Position => _position;
            public SimVector Direction => _direction;
            public int TotalCount => _totalCount;

            /// <summary>묶음의 기준 위치와 방향을 정하고 요청 목록과 총 발 수를 초기화한다.</summary>
            public void Reset(SimVector position, SimVector direction)
            {
                _members.Clear();
                _position = position;
                _direction = direction;
                _totalCount = 0;
            }

            /// <summary>요청 색인과 그 요청의 발 수를 묶음에 더한다.</summary>
            public void AddMember(int requestIndex, int count)
            {
                _members.Add(requestIndex);
                _totalCount += count;
            }

            /// <summary>묶음의 요청 목록과 총 발 수를 비워 풀에 넣을 준비를 한다.</summary>
            public void Clear()
            {
                _members.Clear();
                _totalCount = 0;
            }
        }
    }
}
