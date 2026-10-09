using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RuneCode
{
    internal readonly struct SpawnOrder
    {
        private readonly int _tick;
        private readonly int _sequence;
        private readonly int _group;
        private readonly int _index;
        private readonly int _count;
        private readonly string _enemyId;
        public int Tick => _tick;
        public int Sequence => _sequence;
        public int Group => _group;
        public int Index => _index;
        public int Count => _count;
        public string EnemyId => _enemyId;
        public bool IsStream => _enemyId == null;

        /// <summary>
        /// 스폰 예정 틱, 같은 틱 안의 처리 순서, 묶음(0 = 기본 흐름, 1부터 웨이브), 묶음 안 순서와 기체 수,
        /// 적 ID(null이면 기본 흐름 후보에서 고름)를 보관한다.
        /// </summary>
        public SpawnOrder(int tick, int sequence, int group, int index, int count, string enemyId)
        { _tick = tick; _sequence = sequence; _group = group; _index = index; _count = count; _enemyId = enemyId; }
    }

    /// <summary>
    /// 스테이지 편성을 시작 시점에 스폰 예약 목록으로 펼치고, 매 틱 도달한 예약을 순서대로 내준다.
    /// 예약 총수가 시작 시 정해지므로 남은 적 수 계산의 기준이 된다. 위치 계산과 적 생성은 시뮬레이션이 한다.
    /// </summary>
    internal sealed class SpawnDirector
    {
        private const int STREAM_GROUP = 0;

        private readonly List<SpawnOrder> _orders = new List<SpawnOrder>();
        private readonly FormationSettings[] _settings;
        private readonly bool[] _isStarted;
        private readonly double[] _startAngles;
        private readonly int[] _anchors;
        private int _next;

        public int PendingCount => _orders.Count - _next;

        /// <summary>
        /// 스테이지 정의의 기본 흐름 간격과 웨이브 시각·등장 간격을 틱 단위 예약으로 펼친다.
        /// 같은 틱에서는 기본 흐름이 먼저, 웨이브는 데이터 순서와 유닛 목록 순서를 따른다.
        /// </summary>
        public SpawnDirector(StageDefinition stage, int stageNumber, FormationCatalog formations, int tickRate)
        {
            IReadOnlyList<WaveDefinition> waves = stage.Waves;
            int groupCount = waves.Count + 1;
            _settings = new FormationSettings[groupCount]; _isStarted = new bool[groupCount]; _startAngles = new double[groupCount]; _anchors = new int[groupCount];
            _settings[STREAM_GROUP] = formations.Get(stage.Stream.Formation).ToSettings();
            var streamTicks = new List<int>();
            stage.Stream.AddSpawnTicks(stageNumber, tickRate, streamTicks);
            foreach (int tick in streamTicks) _orders.Add(new SpawnOrder(tick, _orders.Count, STREAM_GROUP, 0, 1, null));
            for (int wave = 0; wave < waves.Count; wave++)
            {
                WaveDefinition definition = waves[wave];
                int group = wave + 1;
                FormationSettings settings = definition.Formation.ApplyTo(formations.Get(definition.Formation.Id).ToSettings());
                _settings[group] = settings;
                int count = definition.UnitCount; int index = 0;
                foreach (WaveUnit unit in definition.Units)
                    for (int i = 0; i < unit.Count; i++, index++)
                    {
                        int tick = (int)Math.Round((definition.At + index * settings.Interval) * tickRate);
                        _orders.Add(new SpawnOrder(tick, _orders.Count, group, index, count, unit.EnemyId));
                    }
            }
            _orders.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Sequence.CompareTo(b.Sequence));
        }

        /// <summary>현재 틱까지 도달한 다음 예약이 있으면 꺼내지 않고 반환한다.</summary>
        public bool TryPeek(int tick, out SpawnOrder order)
        {
            order = _next < _orders.Count ? _orders[_next] : default;
            return _next < _orders.Count && order.Tick <= tick;
        }

        /// <summary>TryPeek로 확인한 예약을 처리 완료로 표시한다.</summary>
        public void Consume() { _next++; }

        /// <summary>묶음의 진형 설정(웨이브 덮어쓰기 적용 후)을 반환한다.</summary>
        public FormationSettings GetSettings(int group) => _settings[group];

        /// <summary>묶음의 시작 각도와 기준 슬롯이 정해졌는지 반환한다.</summary>
        public bool IsStarted(int group) => _isStarted[group];

        /// <summary>묶음이 처음 스폰할 때 정한 슬롯 시작 각도와 기준 슬롯을 기록한다.</summary>
        public void Start(int group, double startAngle, int anchor) { _isStarted[group] = true; _startAngles[group] = startAngle; _anchors[group] = anchor; }

        /// <summary>묶음의 슬롯 시작 각도(라디안)를 반환한다.</summary>
        public double GetStartAngle(int group) => _startAngles[group];

        /// <summary>묶음의 기준 슬롯을 반환한다. 기준 슬롯을 쓰지 않으면 -1이다.</summary>
        public int GetAnchor(int group) => _anchors[group];

        /// <summary>진행 위치와 시작된 묶음의 각도·기준 슬롯을 결정성 해시 버퍼에 기록한다.</summary>
        public void WriteState(StringBuilder state)
        {
            state.Append(_next).Append('|').Append(_orders.Count).Append('|');
            for (int group = 0; group < _isStarted.Length; group++)
                if (_isStarted[group]) state.Append(group).Append(':').Append(_startAngles[group].ToString("R", CultureInfo.InvariantCulture)).Append(':').Append(_anchors[group]).Append('|');
        }
    }
}
