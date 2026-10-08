using System;
using System.Text;

namespace RuneCode
{
    /// <summary>토큰이 노드 흐름을 끝낸 사유를 나타낸다.</summary>
    public enum TokenEndReason { Finished, Terminal, Merged, Unpayable, Expired }

    /// <summary>시전 입력 한 번의 처리 결과를 나타낸다.</summary>
    public enum CastResult { None, Success, NoMana, Overload, Invalid }

    /// <summary>그래프 노드를 따라 이동하며 마나와 위력을 나르는 토큰의 실행 상태다. 변경자는 시뮬레이션 내부에서만 쓴다.</summary>
    public sealed class SpellToken
    {
        private const int NO_ENEMY = -1;
        private const int NO_PORT = -1;
        private const int NO_TICK = -1;

        private readonly int[] _passCounts;
        private int _id;
        private int _castId;
        private int _node;
        private int _nextPort;
        private double _remainingWait;
        private double _mana;
        private double _power;
        private int _count = 1;
        private bool _isCharacterOrigin;
        private SimVector _originPoint;
        private SimVector _direction;
        private int _generation;
        private double _elapsed;
        private int _ignoreEnemyId = NO_ENEMY;
        private bool _isJoinWaiting;
        private int _joinPort = NO_PORT;
        private double _joinWait;
        private int _birthTick;
        private bool _isFromHit;
        private bool _isEnded;
        private int _processedTick = NO_TICK;

        public int Id { get => _id; internal set => _id = value; }
        public int CastId { get => _castId; internal set => _castId = value; }
        public int Node { get => _node; internal set => _node = value; }
        public int NextPort { get => _nextPort; internal set => _nextPort = value; }
        public double RemainingWait { get => _remainingWait; internal set => _remainingWait = value; }
        public double Mana { get => _mana; internal set => _mana = value; }
        public double Power { get => _power; internal set => _power = value; }
        public int Count { get => _count; internal set => _count = value; }
        public bool IsCharacterOrigin { get => _isCharacterOrigin; internal set => _isCharacterOrigin = value; }
        public SimVector OriginPoint { get => _originPoint; internal set => _originPoint = value; }
        public SimVector Direction { get => _direction; internal set => _direction = value; }
        public int Generation { get => _generation; internal set => _generation = value; }
        public double Elapsed { get => _elapsed; internal set => _elapsed = value; }
        public int IgnoreEnemyId { get => _ignoreEnemyId; internal set => _ignoreEnemyId = value; }
        public bool IsJoinWaiting { get => _isJoinWaiting; internal set => _isJoinWaiting = value; }
        public int JoinPort { get => _joinPort; internal set => _joinPort = value; }
        public double JoinWait { get => _joinWait; internal set => _joinWait = value; }
        public int BirthTick { get => _birthTick; internal set => _birthTick = value; }
        public bool IsFromHit { get => _isFromHit; internal set => _isFromHit = value; }
        public bool IsEnded { get => _isEnded; internal set => _isEnded = value; }
        public int ProcessedTick { get => _processedTick; internal set => _processedTick = value; }

        /// <summary>토큰 id, 시전 id, 시작 노드 색인과 프로그램 노드 수를 받아 통과 횟수 배열을 가진 토큰을 생성한다. 나머지 값은 기본값으로 둔다.</summary>
        internal SpellToken(int id, int castId, int node, int nodeCount)
        {
            _id = id;
            _castId = castId;
            _node = node;
            _passCounts = new int[nodeCount];
        }

        /// <summary>지정한 노드 색인에서 지금까지 비용을 지불한 횟수를 반환한다.</summary>
        public int GetPassCount(int node) => _passCounts[node];

        /// <summary>지정한 노드 색인의 통과 횟수를 1 늘린다.</summary>
        internal void AddPass(int node) { _passCounts[node]++; }

        /// <summary>마나를 뺀 모든 필드와 통과 횟수 내용을 복사한 새 토큰을 만들고, 지정한 id와 마나를 넣어 반환한다.</summary>
        internal SpellToken CloneForFork(int newId, double mana)
        {
            SpellToken clone = new SpellToken(newId, _castId, _node, _passCounts.Length);
            Array.Copy(_passCounts, clone._passCounts, _passCounts.Length);
            clone._mana = mana;
            clone._nextPort = _nextPort;
            clone._remainingWait = _remainingWait;
            clone._power = _power;
            clone._count = _count;
            clone._isCharacterOrigin = _isCharacterOrigin;
            clone._originPoint = _originPoint;
            clone._direction = _direction;
            clone._generation = _generation;
            clone._elapsed = _elapsed;
            clone._ignoreEnemyId = _ignoreEnemyId;
            clone._isJoinWaiting = _isJoinWaiting;
            clone._joinPort = _joinPort;
            clone._joinWait = _joinWait;
            clone._birthTick = _birthTick;
            clone._isFromHit = _isFromHit;
            clone._isEnded = _isEnded;
            clone._processedTick = _processedTick;
            return clone;
        }

        /// <summary>토큰의 모든 필드와 0이 아닌 통과 횟수를 결정성 해시 버퍼에 기록한다.</summary>
        internal void WriteState(StringBuilder state)
        {
            state.Append(FormattableString.Invariant($"{_id}|{_castId}|{_node}|{_nextPort}|{_remainingWait:R}|{_mana:R}|{_power:R}|{_count}|{_isCharacterOrigin}|{_originPoint.X:R}|{_originPoint.Y:R}|{_direction.X:R}|{_direction.Y:R}|{_generation}|{_elapsed:R}|{_ignoreEnemyId}|{_isJoinWaiting}|{_joinPort}|{_joinWait:R}|{_birthTick}|{_isFromHit}|{_isEnded}|{_processedTick}"));
            for (int node = 0; node < _passCounts.Length; node++)
            {
                if (_passCounts[node] != 0) state.Append(FormattableString.Invariant($"|{node}:{_passCounts[node]}"));
            }
        }
    }

    /// <summary>적중 토큰이나 종착 발현에서 발사되어 직선으로 비행하는 투사체의 상태다.</summary>
    public sealed class SpellProjectile
    {
        private const int NO_ENEMY = -1;

        private int _id;
        private int _castId;
        private double _power;
        private double _carriedMana;
        private int _generation;
        private int _ignoreEnemyId = NO_ENEMY;
        private SimVector _position;
        private SimVector _direction;
        private double _traveled;
        private int _birthTick;

        public int Id { get => _id; internal set => _id = value; }
        public int CastId { get => _castId; internal set => _castId = value; }
        public double Power { get => _power; internal set => _power = value; }
        public double CarriedMana { get => _carriedMana; internal set => _carriedMana = value; }
        public int Generation { get => _generation; internal set => _generation = value; }
        public int IgnoreEnemyId { get => _ignoreEnemyId; internal set => _ignoreEnemyId = value; }
        public SimVector Position { get => _position; internal set => _position = value; }
        public SimVector Direction { get => _direction; internal set => _direction = value; }
        public double Traveled { get => _traveled; internal set => _traveled = value; }
        public int BirthTick { get => _birthTick; internal set => _birthTick = value; }

        /// <summary>투사체 id를 받아 무시할 적이 없는 기본 상태의 투사체를 생성한다.</summary>
        internal SpellProjectile(int id)
        {
            _id = id;
        }

        /// <summary>투사체의 식별자, 위력, 운반 마나, 위치, 방향과 이동 거리를 결정성 해시 버퍼에 기록한다.</summary>
        internal void WriteState(StringBuilder state)
        {
            state.Append(FormattableString.Invariant($"{_id}|{_castId}|{_power:R}|{_carriedMana:R}|{_generation}|{_ignoreEnemyId}|{_position.X:R}|{_position.Y:R}|{_direction.X:R}|{_direction.Y:R}|{_traveled:R}|{_birthTick}"));
        }
    }

    /// <summary>투사체 노드가 한 프레임에 낸 발사 요청이다. 토큰 값을 복사해 두고 묶음 처리 때 투사체를 만든다.</summary>
    internal readonly struct FireRequest
    {
        private readonly int _tokenId;
        private readonly int _count;
        private readonly double _power;
        private readonly double _carriedMana;
        private readonly int _generation;
        private readonly int _ignoreEnemyId;
        private readonly int _castId;
        private readonly bool _isCharacterOrigin;
        private readonly SimVector _point;
        private readonly SimVector _direction;

        public int TokenId => _tokenId;
        public int Count => _count;
        public double Power => _power;
        public double CarriedMana => _carriedMana;
        public int Generation => _generation;
        public int IgnoreEnemyId => _ignoreEnemyId;
        public int CastId => _castId;
        public bool IsCharacterOrigin => _isCharacterOrigin;
        public SimVector Point => _point;
        public SimVector Direction => _direction;

        /// <summary>토큰의 발사 값, 운반 마나, 원점 정보를 받아 발사 요청을 생성한다.</summary>
        internal FireRequest(int tokenId, int count, double power, double carriedMana, int generation, int ignoreEnemyId, int castId, bool isCharacterOrigin, SimVector point, SimVector direction)
        {
            _tokenId = tokenId;
            _count = count;
            _power = power;
            _carriedMana = carriedMana;
            _generation = generation;
            _ignoreEnemyId = ignoreEnemyId;
            _castId = castId;
            _isCharacterOrigin = isCharacterOrigin;
            _point = point;
            _direction = direction;
        }
    }

    /// <summary>토큰이 지불 불가 또는 수명 초과로 끝난 기록이며, 표시용으로 일정 시간 보관한다.</summary>
    public readonly struct TokenEndEvent
    {
        private readonly int _node;
        private readonly TokenEndReason _reason;
        private readonly double _need;
        private readonly double _have;
        private readonly int _tick;

        public int Node => _node;
        public TokenEndReason Reason => _reason;
        public double Need => _need;
        public double Have => _have;
        public int Tick => _tick;

        /// <summary>소멸한 노드 색인, 사유, 필요 비용, 보유 마나, 틱을 표시용 기록으로 보관한다.</summary>
        internal TokenEndEvent(int node, TokenEndReason reason, double need, double have, int tick)
        {
            _node = node;
            _reason = reason;
            _need = need;
            _have = have;
            _tick = tick;
        }
    }
}
