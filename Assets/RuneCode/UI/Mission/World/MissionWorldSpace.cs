using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 시뮬레이션 좌표(px, 맵 왼쪽 위 원점, y 아래 방향)와 Unity 월드 좌표(unit, 맵 중앙 원점, y 위 방향) 사이의 변환과
    /// 월드 뷰가 함께 쓰는 스프라이트 배치 도우미다. 월드 1 unit은 시뮬레이션 타일 한 칸(32px)이다.
    /// </summary>
    public static class MissionWorldSpace
    {
        public const float PIXELS_PER_UNIT = 32f;

        /// <summary>시뮬레이션 위치를 맵 중앙(origin)을 원점으로 하는 월드 위치로 바꿔 반환한다.</summary>
        public static Vector2 ToWorld(SimVector position, SimVector origin)
            => new Vector2((float)(position.X - origin.X) / PIXELS_PER_UNIT, -(float)(position.Y - origin.Y) / PIXELS_PER_UNIT);

        /// <summary>월드 위치를 맵 중앙(origin) 기준의 시뮬레이션 위치로 바꿔 반환한다.</summary>
        public static SimVector ToSim(Vector2 world, SimVector origin)
            => new SimVector(origin.X + world.x * PIXELS_PER_UNIT, origin.Y - world.y * PIXELS_PER_UNIT);

        /// <summary>시뮬레이션 방향 벡터를 월드 방향(y 반전)으로 바꿔 반환한다.</summary>
        public static Vector2 ToWorldDirection(SimVector direction) => new Vector2((float)direction.X, -(float)direction.Y);

        /// <summary>시뮬레이션 방향의 월드 회전 각도(도)를 반환한다.</summary>
        public static float ToWorldAngle(SimVector direction) => Mathf.Atan2(-(float)direction.Y, (float)direction.X) * Mathf.Rad2Deg;

        /// <summary>시뮬레이션 길이(px)를 월드 길이(unit)로 바꿔 반환한다.</summary>
        public static float ToWorldLength(double pixels) => (float)pixels / PIXELS_PER_UNIT;

        /// <summary>스프라이트를 부모 기준 위치(px)에 지정 지름(px)의 정사각 크기와 색으로 둔다.</summary>
        public static void Place(SpriteRenderer target, Vector2 localPixels, float diameterPixels, Color color)
        {
            target.transform.localPosition = localPixels / PIXELS_PER_UNIT;
            target.transform.localScale = Vector3.one * (diameterPixels / PIXELS_PER_UNIT);
            target.color = color;
        }

        /// <summary>정사각 스프라이트를 부모 기준 두 점(px) 사이의 지정 두께(px) 선분으로 늘여 둔다.</summary>
        public static void PlaceLine(SpriteRenderer target, Vector2 fromPixels, Vector2 toPixels, float widthPixels, Color color)
        {
            Vector2 delta = toPixels - fromPixels;
            target.transform.localPosition = (fromPixels + toPixels) * 0.5f / PIXELS_PER_UNIT;
            target.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            target.transform.localScale = new Vector3(delta.magnitude / PIXELS_PER_UNIT, widthPixels / PIXELS_PER_UNIT, 1);
            target.color = color;
        }

        /// <summary>렌더러 오브젝트의 활성 상태가 다를 때만 바꾼다.</summary>
        public static void SetVisible(Component target, bool isVisible)
        {
            if (target.gameObject.activeSelf != isVisible) target.gameObject.SetActive(isVisible);
        }
    }
}
