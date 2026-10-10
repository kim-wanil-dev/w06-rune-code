using System;
using System.Collections.Generic;

namespace RuneCode
{
    internal readonly struct SpawnPlacement
    {
        private readonly int _slot;
        private readonly double _outward;
        public int Slot => _slot;
        public double Outward => _outward;
        public bool HasSlot => _slot >= 0;
        public static SpawnPlacement None => new SpawnPlacement(-1, 0);

        /// <summary>배치할 슬롯 번호(-1이면 쓸 수 있는 슬롯 없음)와 슬롯에서 바깥쪽으로 밀어낼 거리를 보관한다.</summary>
        public SpawnPlacement(int slot, double outward) { _slot = slot; _outward = outward; }
    }

    /// <summary>
    /// 기준점을 둘러싼 원 위의 같은 간격 슬롯과, 현재 기준점에서 맵 안에 들어가는 슬롯 목록을 계산한다.
    /// 시뮬레이션이 하나를 소유하고 배치마다 Refresh로 다시 계산해 재사용한다.
    /// </summary>
    internal sealed class SpawnSlots
    {
        private readonly List<int> _validSlots = new List<int>();
        private readonly Func<double> _random;
        private SimVector _center;
        private double _radius;
        private int _count;
        private double _startAngle;

        public int ValidCount => _validSlots.Count;

        /// <summary>슬롯 무작위 선택에 쓸 시뮬레이션의 결정적 난수 함수를 연결한다.</summary>
        public SpawnSlots(Func<double> random) { _random = random; }

        /// <summary>기준점, 반경, 슬롯 수, 시작 각도로 슬롯을 다시 계산하고 지정 반경의 개체가 맵 안에 들어가는 슬롯만 후보로 남긴다.</summary>
        public void Refresh(MissionMap map, SimVector center, double radius, int count, double startAngle, double occupantRadius)
        {
            _center = center; _radius = radius; _count = count; _startAngle = startAngle;
            _validSlots.Clear();
            for (int slot = 0; slot < count; slot++)
                if (map.CanOccupy(GetPosition(slot), occupantRadius)) _validSlots.Add(slot);
        }

        /// <summary>슬롯 번호의 각도(라디안)를 반환한다.</summary>
        public double GetAngle(int slot) => _startAngle + slot * 2 * Math.PI / _count;

        /// <summary>슬롯 번호의 바깥 방향 단위 벡터를 반환한다.</summary>
        public SimVector GetDirection(int slot) { double angle = GetAngle(slot); return new SimVector(Math.Cos(angle), Math.Sin(angle)); }

        /// <summary>슬롯 번호의 위치를 반환한다.</summary>
        public SimVector GetPosition(int slot) => _center + GetDirection(slot) * _radius;

        /// <summary>슬롯이 맵 안 후보인지 반환한다.</summary>
        public bool IsValid(int slot) => _validSlots.Contains(slot);

        /// <summary>맵 안 후보 중 순서 번호에 해당하는 슬롯을 반환한다.</summary>
        public int GetValid(int index) => _validSlots[index];

        /// <summary>맵 안 후보 중 하나를 난수로 고르며 후보가 없으면 -1을 반환하고 난수를 소비하지 않는다.</summary>
        public int PickRandomValid() => _validSlots.Count == 0 ? -1 : _validSlots[Math.Min(_validSlots.Count - 1, (int)(_random() * _validSlots.Count))];

        /// <summary>지정 슬롯과 각도 차이가 가장 작은 맵 안 후보를 반환하며 후보가 없으면 -1을 반환한다.</summary>
        public int NearestValid(int slot)
        {
            int best = -1; int bestGap = int.MaxValue;
            foreach (int candidate in _validSlots)
            {
                int gap = Math.Abs(candidate - slot); gap = Math.Min(gap, _count - gap);
                if (gap < bestGap) { best = candidate; bestGap = gap; }
            }
            return best;
        }
    }

    /// <summary>진형 모양 하나의 배치 규칙이다. 상태 없는 공유 인스턴스이며 값만 반환한다.</summary>
    internal abstract class SpawnFormation
    {
        /// <summary>웨이브 시작 시 각도 방식에 맞춰 기준 슬롯을 하나 고른다. 기준 슬롯을 쓰지 않는 모양은 -1을 반환한다.</summary>
        public abstract int ChooseAnchor(SpawnSlots slots, in FormationSettings settings);

        /// <summary>웨이브의 index번째(전체 count기) 기체가 나올 슬롯과 바깥 오프셋을 반환한다. 맵 안 슬롯이 없으면 None을 반환한다.</summary>
        public abstract SpawnPlacement Place(int index, int count, int anchor, SpawnSlots slots, in FormationSettings settings);
    }

    internal sealed class RingRandomSlotFormation : SpawnFormation
    {
        /// <summary>기준 슬롯을 쓰지 않으므로 -1을 반환한다.</summary>
        public override int ChooseAnchor(SpawnSlots slots, in FormationSettings settings) => -1;

        /// <summary>맵 안 슬롯 중 하나를 무작위로 고른다(흩뿌림).</summary>
        public override SpawnPlacement Place(int index, int count, int anchor, SpawnSlots slots, in FormationSettings settings)
        {
            int slot = slots.PickRandomValid();
            return slot < 0 ? SpawnPlacement.None : new SpawnPlacement(slot, 0);
        }
    }

    internal sealed class RingAllSlotsFormation : SpawnFormation
    {
        /// <summary>기준 슬롯을 쓰지 않으므로 -1을 반환한다.</summary>
        public override int ChooseAnchor(SpawnSlots slots, in FormationSettings settings) => -1;

        /// <summary>맵 안 슬롯 전체에 기체를 고르게 나눈다(포위). 기체가 슬롯보다 많으면 다시 처음 슬롯부터 채운다.</summary>
        public override SpawnPlacement Place(int index, int count, int anchor, SpawnSlots slots, in FormationSettings settings)
        {
            int valid = slots.ValidCount;
            if (valid == 0) return SpawnPlacement.None;
            int order = count <= valid ? (int)((long)index * valid / count) : index % valid;
            return new SpawnPlacement(slots.GetValid(order), 0);
        }
    }

    internal sealed class RingDoubleFormation : SpawnFormation
    {
        /// <summary>기준 슬롯을 쓰지 않으므로 -1을 반환한다.</summary>
        public override int ChooseAnchor(SpawnSlots slots, in FormationSettings settings) => -1;

        /// <summary>
        /// 기체를 안쪽 원(앞 절반, 홀수면 1기 더)과 바깥 원으로 나눈다(이중 포위). 안쪽 원은 짝수 슬롯, 바깥 원은 홀수 슬롯에 고르게
        /// 나눠 엇갈리게 두고, 바깥 원은 간격만큼 바깥쪽으로 민다. 해당 짝수·홀수 슬롯이 맵 안에 없으면 맵 안 슬롯 전체를 쓴다.
        /// </summary>
        public override SpawnPlacement Place(int index, int count, int anchor, SpawnSlots slots, in FormationSettings settings)
        {
            if (slots.ValidCount == 0) return SpawnPlacement.None;
            int innerCount = (count + 1) / 2;
            bool isOuter = index >= innerCount;
            int ringIndex = isOuter ? index - innerCount : index;
            int ringCount = isOuter ? count - innerCount : innerCount;
            int parity = isOuter ? 1 : 0;
            int matching = CountParity(slots, parity);
            int slot;
            if (matching == 0)
            {
                int valid = slots.ValidCount;
                slot = slots.GetValid(ringCount <= valid ? (int)((long)ringIndex * valid / ringCount) : ringIndex % valid);
            }
            else slot = GetParitySlot(slots, parity, ringCount <= matching ? (int)((long)ringIndex * matching / ringCount) : ringIndex % matching);
            return new SpawnPlacement(slot, isOuter ? settings.Spacing : 0);
        }

        /// <summary>맵 안 슬롯 중 번호의 짝수·홀수가 parity와 같은 슬롯 수를 반환한다.</summary>
        private static int CountParity(SpawnSlots slots, int parity)
        {
            int total = 0;
            for (int i = 0; i < slots.ValidCount; i++) if (slots.GetValid(i) % 2 == parity) total++;
            return total;
        }

        /// <summary>맵 안 슬롯 중 번호의 짝수·홀수가 parity와 같은 order번째 슬롯을 반환한다. 호출부가 order를 개수보다 작게 맞춘다.</summary>
        private static int GetParitySlot(SpawnSlots slots, int parity, int order)
        {
            for (int i = 0; i < slots.ValidCount; i++)
            {
                int slot = slots.GetValid(i);
                if (slot % 2 != parity) continue;
                if (order-- == 0) return slot;
            }
            return slots.GetValid(0);
        }
    }

    internal sealed class PointSequenceFormation : SpawnFormation
    {
        /// <summary>
        /// 무작위 각도면 맵 안 슬롯 중 하나를, 진행 방향 각도면 진행 방향에 맞춘 0번 슬롯(맵 밖이면 가장 가까운 후보)을 기준으로 고른다.
        /// 진행 방향 각도에서는 시뮬레이션이 시작 각도를 진행 방향으로 맞춰 둔다.
        /// </summary>
        public override int ChooseAnchor(SpawnSlots slots, in FormationSettings settings)
            => settings.Angle == SpawnFormations.ANGLE_AHEAD ? slots.NearestValid(0) : slots.PickRandomValid();

        /// <summary>기준 슬롯(맵 밖이 되었으면 가장 가까운 후보)에서 순서마다 간격만큼 바깥쪽으로 물러난 위치를 반환한다(일렬).</summary>
        public override SpawnPlacement Place(int index, int count, int anchor, SpawnSlots slots, in FormationSettings settings)
        {
            int slot = anchor >= 0 && slots.IsValid(anchor) ? anchor : slots.NearestValid(Math.Max(0, anchor));
            return slot < 0 ? SpawnPlacement.None : new SpawnPlacement(slot, index * settings.Spacing);
        }
    }

    internal static class SpawnFormations
    {
        public const string ANGLE_RANDOM = "random";
        public const string ANGLE_AHEAD = "ahead";

        private static readonly Dictionary<string, SpawnFormation> _formations = new Dictionary<string, SpawnFormation>
        {
            { "ringRandomSlot", new RingRandomSlotFormation() },
            { "ringAllSlots", new RingAllSlotsFormation() },
            { "ringDouble", new RingDoubleFormation() },
            { "pointSequence", new PointSequenceFormation() }
        };

        /// <summary>진형 모양 이름이 구현되어 있는지 반환한다.</summary>
        public static bool IsKnownShape(string shape) => shape != null && _formations.ContainsKey(shape);

        /// <summary>진형 모양 이름에 해당하는 공유 배치 규칙을 반환한다.</summary>
        public static SpawnFormation Get(string shape) => _formations[shape];
    }
}
