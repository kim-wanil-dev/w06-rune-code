using System;
using System.Collections.Generic;
using System.Text;

namespace RuneCode
{
    public sealed partial class RuneSimulation
    {
        private const int MAX_MAGIC_TRACE = 16;
        private const double MAGIC_WALL_THICKNESS = 8;
        private readonly Dictionary<int, ModuleState> _modules = new Dictionary<int, ModuleState>();
        private readonly List<MagicEvent> _magicEvents = new List<MagicEvent>();
        private readonly List<string> _magicTrace = new List<string>();
        private int _nextCastId;
        private CompiledSpell _preparingSpell;
        private int _prepareTick;
        private CastRun _heldRun;
        public IReadOnlyList<string> MagicTrace => _magicTrace;
        public string CastState => _preparingSpell != null ? "차징 " + ChargeSeconds.ToString("0.0") + "s"
            : _heldRun != null ? "유지 중" : "좌클릭 시전";
        public double ChargeSeconds => _preparingSpell == null ? 0 : Math.Min((_tick - _prepareTick) * STEP_SECONDS, _preparingSpell.Input.Number("maxCharge"));

        /// <summary>Root의 즉시·차징·유지 설정에 따라 한 틱의 마우스 입력을 소비한다.</summary>
        private void AdvanceMagicInput(SimulationInput input)
        {
            CompiledSpell spell = _loadout[0];
            if (spell == null) { CancelMagicInput(); return; }
            if (!spell.IsModular) { if (input.CastA) TryCast(spell); return; }
            string mode = spell.Input.Text("inputMode");
            if (_preparingSpell != null)
            {
                if (input.Movement.LengthSquared > 0 || input.Dash)
                { TraceMagic("차징 취소: 이동", null); _preparingSpell = null; }
                else if (input.CastReleased)
                {
                    double seconds = ChargeSeconds;
                    CompiledSpell prepared = _preparingSpell; _preparingSpell = null;
                    if (seconds + 0.000001 < prepared.Input.Number("minCharge")) TraceMagic("차징 취소: 최소 시간 미달", null);
                    else StartModularCast(prepared, 0, seconds + 0.000001 >= prepared.Input.Number("maxCharge") ? 3 : 1);
                }
                else if (!input.CastHeld) { TraceMagic("차징 취소: 입력 해제", null); _preparingSpell = null; }
            }
            if (_heldRun != null)
            {
                if (!input.CastHeld || input.CastReleased) CancelHeldRun("유지 종료: 사용자 해제");
                else if (!PayMagic(spell.Input.Number("upkeep") * STEP_SECONDS, _heldRun, null)) CancelHeldRun("유지 종료: EN 부족");
            }
            if (!input.CastA && !(mode == "instant" && spell.Input.Text("repeatHold") == "on" && input.CastHeld)) return;
            if (_preparingSpell != null || _heldRun != null || _player.Cooldowns[0] > 0.000001) return;
            if (mode == "charge")
            {
                if (input.Movement.LengthSquared > 0 || input.Dash) { TraceMagic("차징 실패: 정지 필요", null); return; }
                if (!PayMagic(spell.Input.Number("prepCost"), null, null)) return;
                _preparingSpell = spell; _prepareTick = _tick; TraceMagic("차징 시작: 이동 시 취소 / 완전 차징 피해 3배", null);
            }
            else if (mode == "maintain")
            {
                CastRun run = StartModularCast(spell, 0, 1);
                if (run != null && run.HasEffect) _heldRun = run;
            }
            else StartModularCast(spell, 0, 1);
        }

        /// <summary>준비 입력과 유지 시전의 예약·개체를 보상 사건 없이 취소한다.</summary>
        public void CancelMagicInput()
        {
            if (_preparingSpell != null) TraceMagic("차징 취소: 입력 관측 종료", null);
            _preparingSpell = null; CancelHeldRun("유지 취소");
        }

        /// <summary>활성 마법 교체 또는 앱 종료 시 모듈·관측·예약과 입력 상태를 정리한다.</summary>
        public void CancelActiveMagic()
        { CancelMagicInput(); _modules.Clear(); _magicEvents.Clear(); _spellEntities.Clear(); _scheduled.Clear(); }

        /// <summary>유지 시전 계보의 개체와 미실행 후속을 삭제하고 종료 이유만 기록한다.</summary>
        private void CancelHeldRun(string reason)
        {
            if (_heldRun == null) return;
            CastRun run = _heldRun; _heldRun = null; run.IsCancelled = true;
            for (int i = _spellEntities.Count - 1; i >= 0; i--)
                if (_modules.TryGetValue(_spellEntities[i].Id, out ModuleState state) && state.Context.Run == run)
                { _modules.Remove(_spellEntities[i].Id); _spellEntities.RemoveAt(i); }
            _scheduled.RemoveAll(pending => pending.Context.Run == run);
            _magicEvents.RemoveAll(pending => pending.State.Context.Run == run);
            TraceMagic(reason, run);
        }

        /// <summary>새 계보로 Root를 실행하며 효과가 발생한 경우에만 슬롯 쿨다운과 유지 핸들을 반환한다.</summary>
        private CastRun StartModularCast(CompiledSpell spell, int slot, double chargeMultiplier)
        {
            if (_player.Cooldowns[slot] > 0.000001) return null;
            CastRun run = new CastRun(++_nextCastId);
            RecordNode(spell.CoreNodeId);
            string noise = ContainsNoise(spell.Root) ? _unlockedElements[(int)(NextRandom() * _unlockedElements.Count)] : null;
            SpellContext context = new SpellContext(_player.Position + _player.AimDirection * (_sector.PlayerRadius + _balance.Sim.WandOffset),
                _player.AimDirection, null, false, noise, run, chargeMultiplier, 1, 0, chargeMultiplier > 1);
            Execute(spell.Root, context);
            FlushMagicEvents();
            if (run.HasEffect) _player.StartSpellCooldown(slot, spell.Cooldown);
            return run;
        }

        /// <summary>원자적인 단계 지불에 성공한 경우에만 실제 EN 소비와 계보 지불량을 기록한다.</summary>
        private bool PayMagic(double amount, CastRun run, string node)
        {
            if (!_player.PayStage(amount)) { TraceMagic("실패: EN 부족", run, node); return false; }
            _energySpent += amount;
            if (run != null) run.Paid += amount;
            if (amount >= 1) TraceMagic("EN -" + amount.ToString("0.#"), run, node);
            return true;
        }

        /// <summary>등록 프로필의 제약을 검사하고 다음 형태의 기본 비용과 대가를 한 번에 지불하도록 전달한다.</summary>
        private void ExecuteEmpower(SpellAction action, SpellContext context)
        {
            MagicRewardProfile profile = GameData.Runes.FindProfile(action.Text("profile"));
            string failure = null;
            if (profile == null) failure = "프로필 없음";
            else if (context.ExtraEnergy > 0 || context.DamageMultiplier > 1) failure = "강화 중첩 불가";
            else if (profile.HpThreshold > 0 && _player.Hp / _player.MaxHp > profile.HpThreshold) failure = "HP 조건 미충족";
            else if (!string.IsNullOrEmpty(profile.ConsumeStatus)
                && (context.Target == null || !context.Target.IsAlive || !context.Target.HasStatus(profile.ConsumeStatus))) failure = "대상 상태 미충족";
            if (failure != null)
            { TraceMagic("제약 실패: " + failure, context.Run, action.NodeId); Execute(action.Else, context); return; }
            double extra = _player.MaxEnergy * profile.EnergyFraction;
            if (action.Then.Count != 1 || _player.Energy + 0.000001 < action.Then[0].OwnEnergy + extra)
            { TraceMagic("제약 실패: EN 부족", context.Run, action.NodeId); Execute(action.Else, context); return; }
            SpellContext empowered = new SpellContext(context.Origin, context.Direction, context.Target, context.FromEvent,
                context.NoiseElement, context.Run, profile.DamageMultiplier, profile.RadiusMultiplier, extra,
                action.Text("scope") == "whole", profile.ConsumeStatus);
            Execute(action.Then, empowered);
        }

        /// <summary>실행 위치·타깃·개체 상한을 확인한 다음 비용과 상태를 원자적으로 소모하고 형태를 생성한다.</summary>
        private void SpawnModularForm(SpellAction action, SpellContext context)
        {
            if (_spellEntities.Count + action.Count > _balance.Limits.MaxLiveSpellEntities)
            { _droppedExecutions++; TraceMagic("실패: 개체 상한", context.Run, action.NodeId); return; }
            if ((action.Form == "tether" || action.Form == "infusion") && (context.Target == null || !context.Target.IsAlive))
            { TraceMagic("실패: 생존 대상 없음", context.Run, action.NodeId); return; }
            if (action.Form == "tether" && (SimVector.Distance(_player.Position, context.Target.Position) > action.Number("maxDistance")
                || !HasMagicSight(_player.Position, context.Target.Position)))
            { TraceMagic("실패: 연결 거리 / 지형", context.Run, action.NodeId); return; }
            if (!PayMagic(action.OwnEnergy + context.ExtraEnergy, context.Run, action.NodeId)) return;
            if (context.ConsumeStatus == "freeze") context.Target.ConsumeFreeze(Time);
            context.Run.HasEffect = true;
            foreach (string id in action.AttachedNodeIds) RecordNode(id);
            SpellContext follow = new SpellContext(context.Origin, context.Direction, context.Target, context.FromEvent,
                context.NoiseElement, context.Run, context.IsWholeBuff ? context.DamageMultiplier : 1,
                context.IsWholeBuff ? context.RadiusMultiplier : 1, 0, context.IsWholeBuff);
            for (int i = 0; i < action.Count; i++)
            {
                SimVector direction = context.Direction;
                if (action.Count > 1) direction = direction.Rotated((i / (double)(action.Count - 1) * 2 - 1) * action.Stats.SpreadAngle * DEGREES_TO_RADIANS);
                SimVector position = context.Origin;
                if (!context.FromEvent && action.Form != "vector" && action.Form != "bolt") position += direction * action.Stats.Offset;
                if (action.Form == "tether" || action.Form == "infusion") position = context.Target.Position;
                if (action.Form == "orbit") position = (context.FromEvent ? context.Origin : _player.Position)
                    + new SimVector(Math.Cos(i * 2 * Math.PI / action.Count), Math.Sin(i * 2 * Math.PI / action.Count)) * action.Stats.OrbitRadius;
                SimulationSpellEntity entity = new SimulationSpellEntity(++_nextEntityId, action, action.Noise ? context.NoiseElement : action.Element,
                    position, direction, context.FromEvent, context.Origin, i * 2 * Math.PI / action.Count, context.NoiseElement,
                    _balance.Sim.SpellVisualSeconds, context.DamageMultiplier, context.RadiusMultiplier);
                ModuleState state = new ModuleState(entity, follow, _tick);
                _spellEntities.Add(entity); _modules.Add(entity.Id, state);
                _peakSpellEntities = Math.Max(_peakSpellEntities, _spellEntities.Count);
                TraceMagic("생성 " + action.Form + " / 피해×" + context.DamageMultiplier.ToString("0.#") + " 반경×" + context.RadiusMultiplier.ToString("0.#"), context.Run, action.NodeId, entity.Id);
                if (action.Form == "wall" && action.Text("variant") == "wall") DisplaceWallOverlaps(entity);
                if (action.Form == "infusion")
                {
                    string status = entity.Element == "ice" ? "freeze" : entity.Element == "arc" ? "emp" : "burn";
                    bool hadStatus = context.Target.HasStatus(status);
                    int priorChill = context.Target.ChillStacks;
                    ApplyDamage(context.Target, 0, entity.Element, "infusion", action.Noise, false, direction, true);
                    if (!hadStatus && context.Target.HasStatus(status) || context.Target.ChillStacks > priorChill) EmitMagic(state, "status", context.Target);
                    TraceMagic("불안정 마력 부여", context.Run, action.NodeId, entity.Id);
                }
                EmitMagic(state, "generated", context.Target);
            }
        }

        /// <summary>모듈의 이동·적중·대상 수명·시간 관측을 진행하며 새로 생성된 개체는 다음 틱부터 진행한다.</summary>
        private void AdvanceModularForm(ModuleState state)
        {
            SimulationSpellEntity entity = state.Entity;
            if (state.CreatedTick == _tick || entity.HasExpired || state.Context.Run.IsCancelled) return;
            SpellAction action = entity.Action;
            SimulationEnemy target = state.Context.Target;
            if (entity.Kind == "vector" || entity.Kind == "bolt")
            {
                if (action.Text("variant") == "beam") AdvanceMagicBeam(state);
                else AdvanceBolt(entity);
            }
            else if (entity.Kind == "construct")
            {
                if (!state.HasActed && entity.Age + 0.000001 >= action.Number("landingDelay"))
                { state.HasActed = true; HitArea(entity, true); EmitMagic(state, "landed", null); }
            }
            else if (entity.Kind == "infusion" || entity.Kind == "tether")
            {
                if (target == null || !target.IsAlive)
                {
                    EmitMagic(state, "death", target); Expire(entity, "targetDeath"); return;
                }
                entity.Move(target.Position, (_player.Position - target.Position).Normalized());
                if (entity.Kind == "tether")
                {
                    if (SimVector.Distance(_player.Position, target.Position) > action.Number("maxDistance") || !HasMagicSight(_player.Position, target.Position))
                    { EmitMagic(state, "disconnected", target); Expire(entity, "disconnected"); return; }
                    target.Move(MoveWithMagicWalls(target.Position, ( _player.Position - target.Position).Normalized() * action.Number("pullForce") * STEP_SECONDS, target.Radius), target.Facing);
                    if (Time + 0.000001 >= state.NextDamageTime) { state.NextDamageTime = Time + action.Stats.TickInterval; Hit(entity, target, entity.Damage, true, true); }
                }
            }
            else if (entity.Kind == "wall")
            {
                if (action.Text("variant") == "wave") entity.Move(_map.Move(entity.Position, entity.Direction * action.Stats.Speed * STEP_SECONDS, entity.Radius), entity.Direction);
                HitArea(entity, action.Text("variant") == "wave");
            }
            else if (entity.Kind == "domain" || entity.Kind == "zone")
            {
                ObserveDomain(state); HitArea(entity, false);
            }
            else if (entity.Kind == "burst" && !state.HasActed) { state.HasActed = true; HitArea(entity, true); Expire(entity); }
            else if (entity.Kind == "orbit")
            {
                entity.Angle += action.Stats.AngularSpeed * DEGREES_TO_RADIANS * STEP_SECONDS;
                SimVector anchor = entity.FromEvent ? entity.Anchor : _player.Position;
                entity.Move(anchor + new SimVector(Math.Cos(entity.Angle), Math.Sin(entity.Angle)) * action.Stats.OrbitRadius,
                    new SimVector(-Math.Sin(entity.Angle), Math.Cos(entity.Angle)));
                HitArea(entity, false);
            }
            if (!entity.HasExpired) EmitMagic(state, "clock", target);
        }

        /// <summary>광선의 전체 경로를 지형까지 계산하여 경로 순서로 관통 적중하고 종료 원인을 확정한다.</summary>
        private void AdvanceMagicBeam(ModuleState state)
        {
            SimulationSpellEntity entity = state.Entity;
            SimVector start = entity.Position;
            double length = entity.Action.Stats.Speed * entity.Action.Stats.Lifetime;
            SimVector end = start; string reason = "range";
            for (double distance = Math.Min(4, length); distance <= length + 0.000001; distance += 4)
            {
                SimVector next = start + entity.Direction * distance;
                if (!_map.CanOccupy(next, entity.Radius)) { reason = "terrain"; break; }
                end = next;
            }
            List<SimulationEnemy> hits = new List<SimulationEnemy>();
            foreach (SimulationEnemy enemy in _enemies)
                if (enemy.IsAlive && SegmentDistance(start, end, enemy.Position) <= entity.Radius + enemy.Radius) hits.Add(enemy);
            hits.Sort((left, right) => (left.Position - start).LengthSquared.CompareTo((right.Position - start).LengthSquared));
            foreach (SimulationEnemy enemy in hits) { entity.HitTimes[enemy.Id] = Time; Hit(entity, enemy, entity.Damage, false, true); entity.Hits++; }
            entity.Move(end, entity.Direction); Expire(entity, reason);
        }

        /// <summary>영역의 진입·이탈을 대상별로 감지하고 영역 안의 살아 있는 적에만 흡입을 적용한다.</summary>
        private void ObserveDomain(ModuleState state)
        {
            SimulationSpellEntity entity = state.Entity;
            foreach (SimulationEnemy enemy in _enemies)
            {
                bool wasInside = state.Inside.Contains(enemy.Id);
                bool isInside = enemy.IsAlive && SimVector.Distance(entity.Position, enemy.Position) <= entity.Radius + enemy.Radius;
                if (isInside && !wasInside) { state.Inside.Add(enemy.Id); EmitMagic(state, "enter", enemy); }
                else if (!isInside && wasInside) { state.Inside.Remove(enemy.Id); EmitMagic(state, "exit", enemy); }
                if (isInside && entity.Kind == "domain")
                    enemy.Move(MoveWithMagicWalls(enemy.Position, (entity.Position - enemy.Position).Normalized() * entity.Action.Number("pullForce") * STEP_SECONDS, enemy.Radius), enemy.Facing);
            }
        }

        /// <summary>시전자와 목표를 잇는 선분이 지형 또는 활성 장벽으로 막히는지 검사한다.</summary>
        private bool HasMagicSight(SimVector start, SimVector end)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(SimVector.Distance(start, end) / 8));
            for (int i = 1; i <= steps; i++) if (_map.IsWall(start + (end - start) * (i / (double)steps))) return false;
            return !CrossesMagicWall(start, end, 0);
        }

        /// <summary>장벽 선분과 이동 선분의 거리로 실제 이동·투사체 통과를 차단한다.</summary>
        private bool CrossesMagicWall(SimVector start, SimVector end, double radius)
        {
            foreach (ModuleState state in _modules.Values)
            {
                SimulationSpellEntity wall = state.Entity;
                if (wall.HasExpired || wall.Kind != "wall" || wall.Action.Text("variant") != "wall") continue;
                SimVector tangent = wall.Direction.Rotated(Math.PI * 0.5);
                SimVector a = wall.Position - tangent * wall.Radius, b = wall.Position + tangent * wall.Radius;
                double d0 = SimVector.Dot(start - wall.Position, wall.Direction), d1 = SimVector.Dot(end - wall.Position, wall.Direction);
                if (SegmentDistance(a, b, end) < radius + MAGIC_WALL_THICKNESS) return true;
                if (d0 * d1 <= 0 && Math.Abs(d0 - d1) > 0.000001)
                {
                    SimVector cross = start + (end - start) * (d0 / (d0 - d1));
                    if (SimVector.Distance(cross, wall.Position) <= wall.Radius + radius) return true;
                }
            }
            return false;
        }

        /// <summary>기존 맵 충돌 결과에 마법 장벽을 적용하여 통과 가능한 이동 위치를 반환한다.</summary>
        private SimVector MoveWithMagicWalls(SimVector position, SimVector displacement, double radius)
        {
            SimVector next = _map.Move(position, displacement, radius);
            return CrossesMagicWall(position, next, radius) ? position : next;
        }

        /// <summary>장벽 생성 시 겹친 적과 시전자를 벽의 가까운 면으로 보정한다.</summary>
        private void DisplaceWallOverlaps(SimulationSpellEntity wall)
        {
            foreach (SimulationEnemy enemy in _enemies)
            {
                SimVector fixedPosition = CorrectWallOverlap(wall, enemy.Position, enemy.Radius);
                enemy.Move(fixedPosition, enemy.Facing);
            }
            _player.Move(CorrectWallOverlap(wall, _player.Position, _sector.PlayerRadius));
        }

        /// <summary>장벽과 겹친 원의 중심을 맵에서 허용하는 가까운 면으로 옮긴 좌표를 반환한다.</summary>
        private SimVector CorrectWallOverlap(SimulationSpellEntity wall, SimVector point, double radius)
        {
            SimVector tangent = wall.Direction.Rotated(Math.PI * .5);
            if (SegmentDistance(wall.Position - tangent * wall.Radius, wall.Position + tangent * wall.Radius, point) >= radius + MAGIC_WALL_THICKNESS) return point;
            double projection = SimVector.Dot(point - wall.Position, wall.Direction);
            SimVector corrected = point + wall.Direction * ((projection < 0 ? -1 : 1) * (radius + MAGIC_WALL_THICKNESS + 1) - projection);
            return _map.NearestFree(corrected, radius);
        }

        /// <summary>현재 적이 속한 활성 Domain의 가장 강한 감속 배율을 반환한다.</summary>
        private double MagicSlow(SimulationEnemy enemy)
        {
            double slow = 0;
            foreach (ModuleState state in _modules.Values)
                if (!state.Entity.HasExpired && state.Entity.Kind == "domain" && SimVector.Distance(state.Entity.Position, enemy.Position) <= state.Entity.Radius + enemy.Radius)
                    slow = Math.Max(slow, state.Entity.Action.Number("slow"));
            return 1 - slow;
        }

        /// <summary>고정 장벽에는 선분 두께를, 다른 형태에는 원형 범위를 사용하여 실제 적중 포함 여부를 반환한다.</summary>
        private static bool SpellOverlaps(SimulationSpellEntity entity, SimulationEnemy enemy)
        {
            if (entity.Kind == "wall" && entity.Variant == "wall")
            {
                SimVector across = entity.Direction.Rotated(Math.PI * .5) * entity.Radius;
                return SegmentDistance(entity.Position - across, entity.Position + across, enemy.Position) <= MAGIC_WALL_THICKNESS + enemy.Radius;
            }
            return SimVector.Distance(entity.Position, enemy.Position) <= entity.Radius + enemy.Radius;
        }

        /// <summary>개체의 사건과 대상 스냅샷을 유한 큐에 넣어 후속 생성과 현재 개체 순회를 분리한다.</summary>
        private void EmitMagic(ModuleState state, string type, SimulationEnemy target, double damage = 0)
        {
            if (state.Entity.Action.Events.Count == 0 || state.Context.Run.IsCancelled) return;
            if (_magicEvents.Count >= _balance.Limits.MaxScheduledExecutions) { _droppedExecutions++; return; }
            bool usesTargetPosition = type == "hit" || type == "damage" || type == "status" || type == "kill"
                || type == "death" || type == "enter" || type == "exit";
            _magicEvents.Add(new MagicEvent(state, type, target, usesTargetPosition && target != null ? target.Position : state.Entity.Position, damage));
        }

        /// <summary>기존 적중·종료 분기에도 모듈 계보와 전체 범위 강화만 보존한 사건 컨텍스트를 반환한다.</summary>
        private SpellContext EventContext(SimulationSpellEntity entity, SimVector position, SimulationEnemy target)
        {
            if (!_modules.TryGetValue(entity.Id, out ModuleState state))
                return new SpellContext(position, entity.Direction, target, true, entity.CastNoiseElement);
            SpellContext context = state.Context;
            return new SpellContext(position, entity.Direction, target, true, context.NoiseElement,
                context.Run, context.DamageMultiplier, context.RadiusMultiplier, 0, context.IsWholeBuff);
        }

        /// <summary>등록 순서의 사건을 트리거 정책과 실제 누적량으로 평가하고 통과한 후속을 한 번씩 실행한다.</summary>
        private void FlushMagicEvents()
        {
            int processed = 0;
            while (_magicEvents.Count > 0 && processed++ < _balance.Limits.MaxActionsPerTick)
            {
                MagicEvent signal = _magicEvents[0]; _magicEvents.RemoveAt(0);
                ModuleState state = signal.State;
                if (state.Context.Run.IsCancelled) continue;
                if (signal.Type == "damage") state.ActualDamage += signal.Damage;
                if (signal.Type == "hit") state.HitCount++;
                foreach (SpellAction trigger in state.Entity.Action.Events)
                {
                    string type = trigger.Text("eventType");
                    bool matches = type == signal.Type;
                    if (type == "elapsed") matches = signal.Type == "clock" && state.Entity.Age + 0.000001 >= trigger.Number("seconds");
                    else if (type == "tick") matches = signal.Type == "clock" && state.Entity.Age + 0.000001 >= trigger.Number("interval");
                    else if (type == "deathOrTime") matches = signal.Type == "death" || (signal.Type == "clock" && (signal.Target != null && !signal.Target.IsAlive || state.Entity.Age + 0.000001 >= trigger.Number("seconds")));
                    else if (type == "damage") matches = signal.Type == "damage" && state.ActualDamage >= trigger.Number("threshold");
                    else if (type == "hitCount") matches = signal.Type == "hit" && state.HitCount >= trigger.Number("threshold");
                    if (!matches) continue;
                    if (type == "status" && trigger.Text("status") != "any" && (signal.Target == null || !signal.Target.HasStatus(trigger.Text("status")))) continue;
                    string key = trigger.NodeId;
                    bool perTarget = trigger.Text("policy") == "perTarget";
                    int limit = trigger.Text("policy") == "once" || type == "deathOrTime" || type == "damage" || type == "hitCount" || type == "elapsed" ? 1 : (int)trigger.Number("limit");
                    state.Counts.TryGetValue(key, out int count);
                    if (count >= limit || (perTarget && (signal.Target == null || state.FiredTargets.Contains(key + ":" + signal.Target.Id)))) continue;
                    string timingKey = perTarget ? key + ":" + signal.Target.Id : key;
                    if (state.LastTimes.TryGetValue(timingKey, out double last) && Time - last + 0.000001 < trigger.Number("interval")) continue;
                    state.Counts[key] = count + 1; state.LastTimes[timingKey] = Time;
                    if (perTarget) state.FiredTargets.Add(key + ":" + signal.Target.Id);
                    bool deathWins = type == "deathOrTime" && signal.Target != null && !signal.Target.IsAlive;
                    RecordNode(trigger.NodeId); TraceMagic("사건 " + type + (type == "deathOrTime" ? deathWins ? " (사망)" : " (시간)" : "") + " → " + trigger.NodeId, state.Context.Run, state.Entity.NodeId, state.Entity.Id);
                    SpellContext context = state.Context;
                    Execute(trigger.Then, new SpellContext(deathWins ? signal.Target.Position : signal.Position, state.Entity.Direction, signal.Target, true,
                        context.NoiseElement, context.Run, context.DamageMultiplier, context.RadiusMultiplier, 0, context.IsWholeBuff));
                }
            }
            if (_magicEvents.Count > 0) { _droppedExecutions += _magicEvents.Count; _magicEvents.Clear(); }
            if (_heldRun != null)
            {
                bool isActive = false;
                foreach (ModuleState state in _modules.Values)
                    if (state.Context.Run == _heldRun && !state.Entity.HasExpired) { isActive = true; break; }
                if (!isActive && !_scheduled.Exists(pending => pending.Context.Run == _heldRun))
                { TraceMagic("유지 완료", _heldRun); _heldRun = null; }
            }
        }

        /// <summary>최근 비용·사건·실패의 계보와 개체 ID를 도크 표시용 유한 기록에 저장한다.</summary>
        private void TraceMagic(string text, CastRun run, string node = null, int entity = 0)
        {
            if (_magicTrace.Count == MAX_MAGIC_TRACE) _magicTrace.RemoveAt(0);
            _magicTrace.Add("t" + _tick + " / c" + (run?.Id ?? 0) + " / e" + entity + (node == null ? "" : " / " + node) + " : " + text);
        }

        /// <summary>모듈의 관측·강화·시전 입력과 큐를 결정성 해시에 추가한다.</summary>
        private void WriteMagicState(StringBuilder state)
        {
            state.Append('|').Append(_nextCastId).Append('|').Append(_preparingSpell?.Signature).Append('|').Append(_prepareTick).Append('|').Append(_heldRun?.Id ?? 0);
            List<int> ids = new List<int>(_modules.Keys); ids.Sort();
            foreach (int id in ids)
            {
                ModuleState module = _modules[id];
                state.Append(FormattableString.Invariant($"|{id}|{module.Context.Run.Id}|{module.Context.Run.Paid:R}|{module.Context.Target?.Id}|{module.Context.DamageMultiplier:R}|{module.Context.RadiusMultiplier:R}|{module.Context.IsWholeBuff}|{module.CreatedTick}|{module.HasActed}|{module.ActualDamage:R}|{module.HitCount}|{module.NextDamageTime:R}"));
                foreach (KeyValuePair<string, int> count in module.Counts) state.Append('|').Append(count.Key).Append(':').Append(count.Value);
                foreach (KeyValuePair<string, double> time in module.LastTimes) state.Append(FormattableString.Invariant($"|{time.Key}:{time.Value:R}"));
                List<string> targets = new List<string>(module.FiredTargets); targets.Sort(StringComparer.Ordinal);
                foreach (string target in targets) state.Append('|').Append(target);
                List<int> inside = new List<int>(module.Inside); inside.Sort(); foreach (int target in inside) state.Append('|').Append(target);
            }
        }

        private sealed class CastRun
        {
            private readonly int _id;
            private double _paid;
            private bool _hasEffect;
            private bool _isCancelled;
            public int Id => _id;
            public double Paid { get => _paid; set => _paid = value; }
            public bool HasEffect { get => _hasEffect; set => _hasEffect = value; }
            public bool IsCancelled { get => _isCancelled; set => _isCancelled = value; }
            /// <summary>시전 ID로 단계별 지불과 취소를 공유하는 계보를 생성한다.</summary>
            public CastRun(int id) { _id = id; }
        }

        private sealed class ModuleState
        {
            private readonly SimulationSpellEntity _entity;
            private readonly SpellContext _context;
            private readonly int _createdTick;
            private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
            private readonly Dictionary<string, double> _lastTimes = new Dictionary<string, double>();
            private readonly HashSet<string> _firedTargets = new HashSet<string>();
            private readonly HashSet<int> _inside = new HashSet<int>();
            private bool _hasActed;
            private double _nextDamageTime;
            private double _actualDamage;
            private int _hitCount;
            public SimulationSpellEntity Entity => _entity;
            public SpellContext Context => _context;
            public int CreatedTick => _createdTick;
            public Dictionary<string, int> Counts => _counts;
            public Dictionary<string, double> LastTimes => _lastTimes;
            public HashSet<string> FiredTargets => _firedTargets;
            public HashSet<int> Inside => _inside;
            public bool HasActed { get => _hasActed; set => _hasActed = value; }
            public double NextDamageTime { get => _nextDamageTime; set => _nextDamageTime = value; }
            public double ActualDamage { get => _actualDamage; set => _actualDamage = value; }
            public int HitCount { get => _hitCount; set => _hitCount = value; }
            /// <summary>단일 모듈 개체와 후속 강화 범위 및 생성 틱으로 독립된 사건 관측을 생성한다.</summary>
            public ModuleState(SimulationSpellEntity entity, SpellContext context, int tick) { _entity = entity; _context = context; _createdTick = tick; }
        }

        private readonly struct MagicEvent
        {
            private readonly ModuleState _state;
            private readonly string _type;
            private readonly SimulationEnemy _target;
            private readonly SimVector _position;
            private readonly double _damage;
            public ModuleState State => _state;
            public string Type => _type;
            public SimulationEnemy Target => _target;
            public SimVector Position => _position;
            public double Damage => _damage;
            /// <summary>사건 공급 개체·종류·마지막 대상 위치·실제 피해를 후속 실행 스냅샷으로 저장한다.</summary>
            public MagicEvent(ModuleState state, string type, SimulationEnemy target, SimVector position, double damage)
            { _state = state; _type = type; _target = target; _position = position; _damage = damage; }
        }
    }
}
