using UnityEngine;

namespace RuneCode
{
    /// <summary>처치 보상 조각 오브를 마름모와 흐린 링으로 표시한다.</summary>
    public sealed class OrbView : MonoBehaviour
    {
        private const float CORE_RADIUS = 6f;
        private const float RING_RADIUS = 9f;

        private static readonly Color CoreColor = new Color(0.43f, 1f, 0.82f);
        private static readonly Color RingColor = new Color(0.3f, 0.9f, 0.7f, 0.3f);

        [Header("표시")]
        [SerializeField] private SpriteRenderer _core;
        [SerializeField] private SpriteRenderer _ring;

        /// <summary>오브 위치를 맵 중앙(origin) 기준 월드 위치로 갱신한다.</summary>
        public void Apply(FragmentOrb orb, SimVector origin)
        {
            transform.localPosition = MissionWorldSpace.ToWorld(orb.Position, origin);
            MissionWorldSpace.Place(_core, Vector2.zero, CORE_RADIUS * 2, CoreColor);
            MissionWorldSpace.Place(_ring, Vector2.zero, RING_RADIUS * 2, RingColor);
        }
    }
}
