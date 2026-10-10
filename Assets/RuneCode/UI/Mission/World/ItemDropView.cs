using UnityEngine;

namespace RuneCode
{
    /// <summary>바닥에 떨어진 Modifier 드롭을 정사각 코어와 흐린 링으로 표시한다.</summary>
    public sealed class ItemDropView : MonoBehaviour
    {
        private const float CORE_RADIUS = 7f;
        private const float RING_RADIUS = 10f;

        private static readonly Color CoreColor = new Color(0.78f, 0.55f, 1f);
        private static readonly Color RingColor = new Color(0.62f, 0.4f, 0.95f, 0.35f);

        [Header("표시")]
        [SerializeField] private SpriteRenderer _core;
        [SerializeField] private SpriteRenderer _ring;

        /// <summary>드롭 위치를 맵 중앙(origin) 기준 월드 위치로 옮기고 코어·링 크기와 색을 갱신한다.</summary>
        public void Apply(SimulationItemDrop drop, SimVector origin)
        {
            transform.localPosition = MissionWorldSpace.ToWorld(drop.Position, origin);
            MissionWorldSpace.Place(_core, Vector2.zero, CORE_RADIUS * 2, CoreColor);
            MissionWorldSpace.Place(_ring, Vector2.zero, RING_RADIUS * 2, RingColor);
        }
    }
}
