using UnityEngine;

namespace RuneCode
{
    /// <summary>적의 체력 회복을 초록 십자가로 표시한다. 경과 틱에 따라 위로 떠오르며 흐려진다.</summary>
    public sealed class HealPulseView : MonoBehaviour
    {
        private const float START_OFFSET = 10f;
        private const float RISE_PER_TICK = 0.6f;
        private const float FADE_TICKS = 42f;
        private const float ARM_PIXELS = 9.8f;
        private const float THICKNESS_PIXELS = 3.5f;

        private readonly SpriteRenderer[] _bars = new SpriteRenderer[2];
        private Color _color;

        /// <summary>맵용 정사각 스프라이트 두 장으로 세로·가로 막대 십자가를 만든다. 피해 숫자와 같은 정렬 순서를 쓴다.</summary>
        public void Initialize(Transform parent, Sprite squareSprite, Material spriteMaterial, Color color, int order)
        {
            transform.SetParent(parent, false);
            _color = color;
            _bars[0] = CreateBar(squareSprite, spriteMaterial, new Vector2(THICKNESS_PIXELS, ARM_PIXELS * 2), order);
            _bars[1] = CreateBar(squareSprite, spriteMaterial, new Vector2(ARM_PIXELS * 2, THICKNESS_PIXELS), order);
        }

        /// <summary>회복 위치와 경과 틱으로 맵 중앙(origin) 기준 십자가 위치와 알파를 갱신한다.</summary>
        public void Apply(HealPulse pulse, int elapsedTicks, SimVector origin)
        {
            transform.localPosition = MissionWorldSpace.ToWorld(pulse.Position, origin)
                + new Vector2(0, (START_OFFSET + elapsedTicks * RISE_PER_TICK) / MissionWorldSpace.PIXELS_PER_UNIT);
            Color tint = _color; tint.a *= 1 - elapsedTicks / FADE_TICKS;
            foreach (SpriteRenderer bar in _bars) bar.color = tint;
        }

        /// <summary>십자가 중심에 정사각 스프라이트를 지정 픽셀 크기로 늘려 막대 하나를 만든다.</summary>
        private SpriteRenderer CreateBar(Sprite squareSprite, Material spriteMaterial, Vector2 dimensionsPixels, int order)
        {
            var bar = new GameObject("Bar", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            bar.transform.SetParent(transform, false);
            bar.sprite = squareSprite;
            bar.sharedMaterial = spriteMaterial;
            bar.sortingOrder = order;
            MissionWorldSpace.Place(bar, Vector2.zero, dimensionsPixels, _color);
            return bar;
        }
    }
}
