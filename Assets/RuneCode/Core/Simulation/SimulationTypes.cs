using System;
using System.Collections.Generic;
using System.Text;

namespace RuneCode
{
    public readonly struct SimVector
    {
        private readonly double _x;
        private readonly double _y;
        public double X => _x;
        public double Y => _y;
        public double Length => Math.Sqrt(_x * _x + _y * _y);
        public double LengthSquared => _x * _x + _y * _y;
        public static SimVector Zero => new SimVector(0, 0);

        /// <summary>주어진 좌표로 시뮬레이션 위치 또는 방향을 생성한다.</summary>
        public SimVector(double x, double y) { _x = x; _y = y; }

        /// <summary>길이가 0이면 기본 우측 방향, 그 외에는 단위 방향을 반환한다.</summary>
        public SimVector Normalized() => Length > 0.000001 ? this / Length : new SimVector(1, 0);

        /// <summary>벡터를 라디안 각도로 회전한 결과를 반환한다.</summary>
        public SimVector Rotated(double radians) => new SimVector(_x * Math.Cos(radians) - _y * Math.Sin(radians), _x * Math.Sin(radians) + _y * Math.Cos(radians));

        /// <summary>두 좌표의 거리를 반환한다.</summary>
        public static double Distance(SimVector a, SimVector b) => (a - b).Length;

        /// <summary>두 벡터의 내적을 반환한다.</summary>
        public static double Dot(SimVector a, SimVector b) => a._x * b._x + a._y * b._y;

        /// <summary>두 벡터를 더한 좌표를 반환한다.</summary>
        public static SimVector operator +(SimVector a, SimVector b) => new SimVector(a._x + b._x, a._y + b._y);

        /// <summary>두 벡터의 차이를 반환한다.</summary>
        public static SimVector operator -(SimVector a, SimVector b) => new SimVector(a._x - b._x, a._y - b._y);

        /// <summary>벡터 각 좌표에 배율을 적용한 결과를 반환한다.</summary>
        public static SimVector operator *(SimVector a, double scale) => new SimVector(a._x * scale, a._y * scale);

        /// <summary>벡터 각 좌표를 나눈 결과를 반환한다.</summary>
        public static SimVector operator /(SimVector a, double scale) => new SimVector(a._x / scale, a._y / scale);
    }

    public readonly struct SimulationInput
    {
        private readonly SimVector _movement;
        private readonly SimVector _aimDirection;
        private readonly bool _castA;
        private readonly bool _castB;
        private readonly bool _castC;
        private readonly bool _dash;
        private readonly bool _interact;
        private readonly bool _castHeld;
        private readonly bool _castReleased;
        public bool CastHeld => _castHeld;
        public bool CastReleased => _castReleased;
        public SimVector Movement => _movement;
        public SimVector AimDirection => _aimDirection;
        public bool CastA => _castA;
        public bool CastB => _castB;
        public bool CastC => _castC;
        public bool Dash => _dash;
        public bool Interact => _interact;

        /// <summary>한 고정 틱의 이동, 조준, 슬롯 시전, 대시, 상호작용 입력을 보관한다.</summary>
        public SimulationInput(SimVector movement, SimVector aimDirection, bool castA = false, bool castB = false, bool castC = false, bool dash = false, bool interact = false, bool castHeld = false, bool castReleased = false)
        { _movement = movement; _aimDirection = aimDirection; _castA = castA; _castB = castB; _castC = castC; _dash = dash; _interact = interact; _castHeld = castHeld; _castReleased = castReleased; }
    }

    public enum MissionStage { Bench, Combat, Terminal, Cleared, Dead }

    public sealed class SimulationPlayer
    {
        private readonly double[] _cooldowns = new double[3];
        private SimVector _position;
        private SimVector _aimDirection = new SimVector(1, 0);
        private SimVector _dashDirection;
        private double _hp;
        private double _maxHp;
        private double _energy;
        private double _maxEnergy;
        private double _shield;
        private double _shieldUntil;
        private double _dashCooldown;
        private double _dashRemaining;
        private double _invulnerableUntil;
        public SimVector Position => _position;
        public SimVector AimDirection => _aimDirection;
        public double Hp => _hp;
        public double MaxHp => _maxHp;
        public double Energy => _energy;
        public double MaxEnergy => _maxEnergy;
        public double Shield => _shield;
        public double DashCooldown => _dashCooldown;
        public double DashRemaining => _dashRemaining;
        public IReadOnlyList<double> Cooldowns => _cooldowns;

        /// <summary>최대 체력과 에너지 및 시작 위치로 플레이어의 전투 상태를 초기화한다.</summary>
        internal SimulationPlayer(double hp, double energy, SimVector position) { _maxHp = hp; _hp = hp; _maxEnergy = energy; _energy = energy; _position = position; }

        /// <summary>이동 처리 후 플레이어 위치를 갱신한다.</summary>
        internal void Move(SimVector position) { _position = position; }

        /// <summary>입력 방향으로 조준 상태를 갱신한다.</summary>
        internal void Aim(SimVector direction) { if (direction.LengthSquared > 0.000001) _aimDirection = direction.Normalized(); }

        /// <summary>쿨다운과 에너지를 확인하여 슬롯의 시전 비용을 선지불하고 성공 여부를 반환한다.</summary>
        internal bool Pay(CompiledSpell spell, int slot)
        { if (_cooldowns[slot] > 0.000001 || _energy + 0.000001 < spell.EnergyCost) return false; _energy -= spell.EnergyCost; _cooldowns[slot] = spell.Cooldown; return true; }

        /// <summary>단계 EN을 원자적으로 차감하고 부족하면 아무 값도 바꾸지 않고 false를 반환한다.</summary>
        internal bool PayStage(double amount)
        { if (amount < 0 || _energy + 0.000001 < amount) return false; _energy = Math.Max(0, _energy - amount); return true; }

        /// <summary>실제 효과가 발생한 Root 슬롯의 쿨다운을 설정한다.</summary>
        internal void StartSpellCooldown(int slot, double seconds) { _cooldowns[slot] = seconds; }

        /// <summary>시간 경과에 따라 에너지를 회복하고 쿨다운, 보호막 및 대시 지속 상태를 갱신한다.</summary>
        internal void Advance(double dt, double time, double regen)
        { _energy = Math.Min(_maxEnergy, _energy + dt * regen); for (int i = 0; i < _cooldowns.Length; i++) _cooldowns[i] = Math.Max(0, _cooldowns[i] - dt); _dashCooldown = Math.Max(0, _dashCooldown - dt); _dashRemaining = Math.Max(0, _dashRemaining - dt); if (time >= _shieldUntil) _shield = 0; }

        /// <summary>대시가 가능한 경우 방향, 지속 시간, 무적 및 쿨다운을 설정한다.</summary>
        internal bool StartDash(SimVector direction, double seconds, double cooldown, double time)
        { if (_dashCooldown > 0.000001) return false; _dashDirection = direction.Normalized(); _dashRemaining = seconds; _dashCooldown = cooldown; _invulnerableUntil = Math.Max(_invulnerableUntil, time + seconds); return true; }

        /// <summary>진행 중인 대시의 이동 방향을 반환한다.</summary>
        internal SimVector GetDashDirection() => _dashDirection;

        /// <summary>무적을 확인하고 보호막과 체력에 피해를 적용하여 실제 체력 피해를 반환한다.</summary>
        internal double Hurt(double damage, double time, double invulnerability)
        { if (time < _invulnerableUntil) return 0; double absorbed = Math.Min(_shield, damage); _shield -= absorbed; double actual = Math.Min(_hp, damage - absorbed); _hp -= actual; _invulnerableUntil = time + invulnerability; return actual; }

        /// <summary>보호막 수치와 만료 시간을 룬 설정에 따라 갱신한다.</summary>
        internal void GiveShield(double amount, double seconds, double time) { _shield = Math.Max(_shield, amount); _shieldUntil = time + seconds; }

        /// <summary>터미널에 도착하여 최대 체력 비율을 회복하고 에너지를 충전한다.</summary>
        internal void Rest(double healFraction) { _hp = Math.Min(_maxHp, _hp + _maxHp * healFraction); _energy = _maxEnergy; for (int i = 0; i < _cooldowns.Length; i++) _cooldowns[i] = 0; _dashRemaining = 0; }

        /// <summary>위치, 능력치, 보호막, 대시 및 무적의 현재와 예정 상태를 결정성 해시 버퍼에 기록한다.</summary>
        internal void WriteState(StringBuilder state)
        {
            state.Append(FormattableString.Invariant($"|{_position.X:R}|{_position.Y:R}|{_aimDirection.X:R}|{_aimDirection.Y:R}|{_dashDirection.X:R}|{_dashDirection.Y:R}|{_hp:R}|{_maxHp:R}|{_energy:R}|{_maxEnergy:R}|{_shield:R}|{_shieldUntil:R}|{_dashCooldown:R}|{_dashRemaining:R}|{_invulnerableUntil:R}"));
            foreach (double cooldown in _cooldowns) state.Append(FormattableString.Invariant($"|{cooldown:R}"));
        }
    }

    public sealed class SimulationEnemy
    {
        private readonly int _id;
        private readonly EnemyDefinition _definition;
        private readonly bool _isDummy;
        private readonly double _hitFlashSeconds;
        private readonly double _damage;
        private readonly double _damageMultiplier;
        private readonly int _reward;
        private SimVector _position;
        private SimVector _facing = new SimVector(-1, 0);
        private double _hp;
        private double _maxHp;
        private double _attackAt;
        private double _warningUntil;
        private double _dashUntil;
        private SimVector _attackDirection;
        private double _burnUntil;
        private double _burnNext;
        private string _burnForm = "bolt";
        private bool _burnNoise;
        private double _chillUntil;
        private int _chillStacks;
        private double _freezeUntil;
        private double _freezeImmuneUntil;
        private double _empUntil;
        private double _flashUntil;
        private int _phase = 1;
        private double _patchUntil;
        private double _reinforcementAt;
        private double _hazardAt;
        private double _observedTime;
        private string _lastDamageElement = "raw";
        private string _lastDamageForm = "";
        public int Id => _id;
        public string Kind => _definition.Id;
        public SimVector Position => _position;
        public SimVector Facing => _facing;
        public double Hp => _hp;
        public double MaxHp => _maxHp;
        public double Radius => _definition.Radius;
        public double Damage => _damage;
        public int Reward => _reward;
        public bool IsDummy => _isDummy;
        public bool IsAlive => _hp > 0;
        public bool IsBurning => _observedTime < _burnUntil;
        public int ChillStacks => _chillStacks;
        public bool IsFrozen => _observedTime < _freezeUntil;
        public bool IsFreezeImmune => _observedTime < _freezeImmuneUntil;
        public bool IsEmp => _observedTime < _empUntil;
        public bool IsFlashing => _observedTime < _flashUntil;
        public bool IsWarning => _observedTime < _warningUntil;
        public int Phase => _phase;
        public bool IsPatching => _observedTime < _patchUntil;
        public string LastDamageElement => _lastDamageElement;
        public string LastDamageForm => _lastDamageForm;
        internal EnemyDefinition Definition => _definition;
        internal double DamageMultiplier => _damageMultiplier;
        internal double AttackAt { get => _attackAt; set => _attackAt = value; }
        internal double WarningUntil { get => _warningUntil; set => _warningUntil = value; }
        internal double DashUntil { get => _dashUntil; set => _dashUntil = value; }
        internal SimVector AttackDirection { get => _attackDirection; set => _attackDirection = value; }
        internal double BurnNext { get => _burnNext; set => _burnNext = value; }
        internal double BurnUntil => _burnUntil;
        internal string BurnForm => _burnForm;
        internal bool BurnNoise => _burnNoise;
        internal double ReinforcementAt { get => _reinforcementAt; set => _reinforcementAt = value; }
        internal double HazardAt { get => _hazardAt; set => _hazardAt = value; }

        /// <summary>공유 적 설정을 변경하지 않고 위치, 개별 체력·피해 배율, 보상 및 무반격 여부로 실행 상태를 생성한다.</summary>
        internal SimulationEnemy(int id, EnemyDefinition definition, SimVector position, bool isDummy, double hp, double hitFlashSeconds, double hpMultiplier = 1, double damageMultiplier = 1, int reward = -1)
        { _id = id; _definition = definition; _position = position; _isDummy = isDummy; _hitFlashSeconds = hitFlashSeconds; _maxHp = hp > 0 ? hp : definition.Hp * hpMultiplier; _hp = _maxHp; _damageMultiplier = damageMultiplier; _damage = definition.Damage * damageMultiplier; _reward = reward >= 0 ? reward : definition.Reward; _attackAt = definition.AttackInterval; }

        /// <summary>틱 시간을 반영하고 만료된 냉기 스택을 제거한다.</summary>
        internal void Observe(double time) { _observedTime = time; if (time >= _chillUntil) _chillStacks = 0; }

        /// <summary>적 위치 및 플레이어를 향한 방향을 갱신한다.</summary>
        internal void Move(SimVector position, SimVector facing) { _position = position; if (facing.LengthSquared > 0.000001) _facing = facing.Normalized(); }

        /// <summary>피해를 체력에 적용하고 실제 감소량을 반환하며 피격 표시 시간을 설정한다.</summary>
        internal double Hurt(double damage, double time) { double actual = Math.Min(_hp, Math.Max(0, damage)); _hp -= actual; _flashUntil = time + _hitFlashSeconds; return actual; }

        /// <summary>화염 피해의 출처를 보관하고 지속시간을 갱신하되 진행 중인 틱을 유지한다.</summary>
        internal void Burn(double time, double duration, double interval, string form, bool noise)
        { if (time >= _burnUntil) _burnNext = time + interval; _burnUntil = time + duration; _burnForm = form; _burnNoise = noise; }

        /// <summary>냉기 스택과 지속시간을 갱신하고 빙결 면역이 없으면 3중첩에서 정지 상태를 적용한다.</summary>
        internal void Chill(double time, double duration, int maxStacks, double freeze, double immunity)
        { _chillStacks = Math.Min(maxStacks, _chillStacks + 1); _chillUntil = time + duration; if (_chillStacks >= maxStacks && time >= _freezeImmuneUntil) { _freezeUntil = time + freeze; _freezeImmuneUntil = _freezeUntil + immunity; _chillStacks = 0; } }

        /// <summary>전격 적중에 따른 방패 무력화 만료 시간을 갱신한다.</summary>
        internal void Emp(double time, double duration) { _empUntil = time + duration; }

        /// <summary>보스 페이즈를 변경하고 패치 무적 만료 시간을 설정한다.</summary>
        internal void Patch(int phase, double time, double duration) { _phase = phase; _patchUntil = time + duration; }

        /// <summary>실제로 받은 최근 피해의 속성과 형태를 기록하여 해당 적의 내성 아이콘에 사용한다.</summary>
        internal void SetDamageTags(string element, string form) { _lastDamageElement = element; _lastDamageForm = form; }

        /// <summary>제약 성공 시 현재 빙결을 한 번 소모하고 빙결 상태를 해제한다.</summary>
        internal void ConsumeFreeze(double time) { _freezeUntil = time; _chillStacks = 0; }

        /// <summary>조건 룬에서 요구하는 상태 보유 여부를 반환한다.</summary>
        public bool HasStatus(string status) => status == "burn" ? IsBurning : status == "chill" ? _chillStacks > 0 : status == "freeze" ? IsFrozen : status == "emp" && IsEmp;

        /// <summary>적의 체력, 이동, 공격 예약, 상태 만료 및 페이즈를 결정성 해시 버퍼에 기록한다.</summary>
        internal void WriteState(StringBuilder state)
        {
            state.Append(_id).Append('|').Append(Kind).Append('|').Append(_isDummy);
            state.Append(FormattableString.Invariant($"|{_damage:R}|{_damageMultiplier:R}|{_reward}"));
            state.Append('|').Append(_lastDamageElement).Append('|').Append(_lastDamageForm);
            state.Append(FormattableString.Invariant($"|{_position.X:R}|{_position.Y:R}|{_facing.X:R}|{_facing.Y:R}|{_hp:R}|{_maxHp:R}|{_attackAt:R}|{_warningUntil:R}|{_dashUntil:R}|{_attackDirection.X:R}|{_attackDirection.Y:R}|{_burnUntil:R}|{_burnNext:R}|{_burnForm}|{_burnNoise}|{_chillUntil:R}|{_chillStacks}|{_freezeUntil:R}|{_freezeImmuneUntil:R}|{_empUntil:R}|{_flashUntil:R}|{_phase}|{_patchUntil:R}|{_reinforcementAt:R}|{_hazardAt:R}|{_observedTime:R}"));
        }
    }

    public sealed class SimulationSpellEntity
    {
        private readonly int _id;
        private readonly SpellAction _action;
        private readonly string _element;
        private readonly string _castNoiseElement;
        private readonly bool _fromEvent;
        private readonly double _visualSeconds;
        private readonly double _damageMultiplier;
        private readonly double _radiusMultiplier;
        private readonly SimVector _anchor;
        private readonly Dictionary<int, double> _hitTimes = new Dictionary<int, double>();
        private SimVector _position;
        private SimVector _direction;
        private double _age;
        private double _angle;
        private int _hits;
        private int _triggerCount;
        private bool _hasExpired;
        public int Id => _id;
        public string Kind => _action.Form;
        public string Variant => _action.Text("variant");
        public float LandingDelay => _action.Number("landingDelay");
        public string Element => _element;
        public SimVector Position => _position;
        public SimVector Direction => _direction;
        public double Radius => _action.Stats.Radius * _radiusMultiplier;
        public double Damage => _action.Stats.Damage * _damageMultiplier;
        public double Age => _age;
        public double Lifetime => _action.Form == "vector" && _action.Text("variant") == "beam" ? Math.Min(_visualSeconds, .15) : _action.Form == "burst" ? _visualSeconds : _action.Stats.Lifetime;
        public string NodeId => _action.NodeId;
        internal SpellAction Action => _action;
        internal bool FromEvent => _fromEvent;
        internal string CastNoiseElement => _castNoiseElement;
        public SimVector Anchor => _anchor;
        internal Dictionary<int, double> HitTimes => _hitTimes;
        internal int Hits { get => _hits; set => _hits = value; }
        internal int TriggerCount { get => _triggerCount; set => _triggerCount = value; }
        internal double Angle { get => _angle; set => _angle = value; }
        internal bool HasExpired { get => _hasExpired; set => _hasExpired = value; }

        /// <summary>컴파일된 Form, 속성, 위치, 방향 및 앵커 컨텍스트로 스펠 개체를 생성한다.</summary>
        internal SimulationSpellEntity(int id, SpellAction action, string element, SimVector position, SimVector direction, bool fromEvent, SimVector anchor, double angle, string castNoiseElement, double visualSeconds, double damageMultiplier = 1, double radiusMultiplier = 1)
        { _damageMultiplier = damageMultiplier; _radiusMultiplier = radiusMultiplier; _id = id; _action = action; _element = element; _castNoiseElement = castNoiseElement; _position = position; _direction = direction; _fromEvent = fromEvent; _visualSeconds = visualSeconds; _anchor = anchor; _angle = angle; }

        /// <summary>스펠 개체의 위치와 진행 방향을 갱신한다.</summary>
        internal void Move(SimVector position, SimVector direction) { _position = position; _direction = direction; }

        /// <summary>스펠 개체의 수명 경과를 고정 시간만큼 증가시킨다.</summary>
        internal void Advance(double dt) { _age += dt; }

        /// <summary>마법의 이동, 앵커, 수명, 적중 대상 및 이벤트 실행 상태를 결정성 해시 버퍼에 기록한다.</summary>
        internal void WriteState(StringBuilder state)
        {
            state.Append(FormattableString.Invariant($"|{_damageMultiplier:R}|{_radiusMultiplier:R}"));
            state.Append(_id).Append('|').Append(_action.NodeId).Append('|').Append(_element).Append('|').Append(_castNoiseElement).Append('|').Append(_fromEvent);
            state.Append(FormattableString.Invariant($"|{_anchor.X:R}|{_anchor.Y:R}|{_position.X:R}|{_position.Y:R}|{_direction.X:R}|{_direction.Y:R}|{_age:R}|{_angle:R}|{_hits}|{_triggerCount}|{_hasExpired}"));
            var ids = new List<int>(_hitTimes.Keys); ids.Sort();
            foreach (int id in ids) state.Append(FormattableString.Invariant($"|{id}:{_hitTimes[id]:R}"));
        }
    }

    public sealed class SimulationProjectile
    {
        private readonly SimVector _direction;
        private readonly double _speed;
        private readonly double _damage;
        private readonly double _radius;
        private readonly double _lifetime;
        private readonly bool _isHazard;
        private readonly double _warning;
        private SimVector _position;
        private double _age;
        public SimVector Position => _position;
        public SimVector Direction => _direction;
        public double Radius => _radius;
        public double Age => _age;
        public bool IsHazard => _isHazard;
        public bool IsWarning => _isHazard && _age < _warning;
        internal double Damage => _damage;
        internal double Lifetime => _lifetime;

        /// <summary>적 탄환 또는 예고 장판을 위치와 공격 설정으로 생성한다.</summary>
        internal SimulationProjectile(SimVector position, SimVector direction, double speed, double damage, double radius, double lifetime, bool isHazard = false, double warning = 0)
        { _position = position; _direction = direction; _speed = speed; _damage = damage; _radius = radius; _lifetime = lifetime; _isHazard = isHazard; _warning = warning; }

        /// <summary>탄환의 위치와 수명을 고정 시간만큼 진행한다.</summary>
        internal void Advance(double dt) { _age += dt; _position += _direction * (_speed * dt); }

        /// <summary>적 탄환의 이동, 예고, 피해 및 수명 상태를 결정성 해시 버퍼에 기록한다.</summary>
        internal void WriteState(StringBuilder state)
        { state.Append(FormattableString.Invariant($"|{_position.X:R}|{_position.Y:R}|{_direction.X:R}|{_direction.Y:R}|{_speed:R}|{_damage:R}|{_radius:R}|{_lifetime:R}|{_isHazard}|{_warning:R}|{_age:R}")); }
    }

    public readonly struct FragmentOrb
    {
        private readonly SimVector _position;
        private readonly int _amount;
        public SimVector Position => _position;
        public int Amount => _amount;
        /// <summary>처치 위치와 조각 개수로 회수 가능한 오브를 생성한다.</summary>
        internal FragmentOrb(SimVector position, int amount) { _position = position; _amount = amount; }
    }

    public readonly struct DamageNumber
    {
        private readonly SimVector _position;
        private readonly double _amount;
        private readonly string _element;
        private readonly int _tick;
        public SimVector Position => _position;
        public double Amount => _amount;
        public string Element => _element;
        public int Tick => _tick;
        /// <summary>피해 발생 위치, 수치, 속성 및 틱을 표시용 기록으로 보관한다.</summary>
        internal DamageNumber(SimVector position, double amount, string element, int tick) { _position = position; _amount = amount; _element = element; _tick = tick; }
    }

    public readonly struct NodeExecutionEvent
    {
        private readonly string _nodeId;
        private readonly int _tick;
        public string NodeId => _nodeId;
        public int Tick => _tick;
        /// <summary>실행된 노드 ID와 시뮬레이션 틱을 하이라이트 및 통계용으로 보관한다.</summary>
        internal NodeExecutionEvent(string nodeId, int tick) { _nodeId = nodeId; _tick = tick; }
    }
}
