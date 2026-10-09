using UnityEngine;

namespace RuneCode
{
    /// <summary>적 탄환(마름모와 잔상)과 보스 위험 장판(채움, 외곽 링, 사선 표시)을 표시한다. 장판은 예고 중에 더 흐리게 그린다.</summary>
    public sealed class HostileProjectileView : MonoBehaviour
    {
        private const float TRAIL_LENGTH = 14f;
        private const float HAZARD_WARNING_ALPHA = 0.10f;
        private const float HAZARD_ACTIVE_ALPHA = 0.30f;
        private const float HAZARD_MARK_SCALE = 0.3f;
        private const float HAZARD_LINE_WIDTH = 2f;

        private static readonly Color Tint = new Color(1f, 0.33f, 0.42f);
        private static readonly Color TrailColor = new Color(1f, 0.25f, 0.3f, 0.25f);

        [Header("탄환")]
        [SerializeField] private SpriteRenderer _bullet;
        [SerializeField] private SpriteRenderer _trail;

        [Header("장판")]
        [SerializeField] private SpriteRenderer _hazardFill;
        [SerializeField] private SpriteRenderer _hazardRing;
        [SerializeField] private SpriteRenderer _hazardMark;

        /// <summary>적 탄환 또는 장판의 위치·크기·예고 상태로 맵 중앙(origin) 기준 월드 표시를 갱신한다.</summary>
        public void Apply(SimulationProjectile projectile, SimVector origin)
        {
            transform.localPosition = MissionWorldSpace.ToWorld(projectile.Position, origin);
            float radius = (float)projectile.Radius;
            bool isHazard = projectile.IsHazard;
            MissionWorldSpace.SetVisible(_bullet, !isHazard);
            MissionWorldSpace.SetVisible(_trail, !isHazard);
            MissionWorldSpace.SetVisible(_hazardFill, isHazard);
            MissionWorldSpace.SetVisible(_hazardRing, isHazard);
            MissionWorldSpace.SetVisible(_hazardMark, isHazard);
            if (isHazard)
            {
                Color fill = Tint; fill.a = projectile.IsWarning ? HAZARD_WARNING_ALPHA : HAZARD_ACTIVE_ALPHA;
                MissionWorldSpace.Place(_hazardFill, Vector2.zero, radius * 2, fill);
                MissionWorldSpace.Place(_hazardRing, Vector2.zero, radius * 2, Tint);
                MissionWorldSpace.PlaceLine(_hazardMark, -Vector2.one * radius * HAZARD_MARK_SCALE, Vector2.one * radius * HAZARD_MARK_SCALE, HAZARD_LINE_WIDTH, Tint);
                return;
            }
            MissionWorldSpace.Place(_bullet, Vector2.zero, radius * 2, Tint);
            MissionWorldSpace.PlaceLine(_trail, -MissionWorldSpace.ToWorldDirection(projectile.Direction) * TRAIL_LENGTH, Vector2.zero, radius, TrailColor);
        }
    }
}
