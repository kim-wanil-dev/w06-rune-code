using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 전격 연쇄 한 구간을 두 적 사이의 지그재그 번개로 표시한다. 넓은 속성 색 빛과 가는 흰 심선을 겹쳐 그리고,
    /// 경과 틱에 따라 흐려지며 몇 틱마다 꺾임 모양을 바꿔 깜빡인다.
    /// </summary>
    public sealed class ArcLinkView : MonoBehaviour
    {
        private const float GLOW_WIDTH = 6f;
        private const float CORE_WIDTH = 2f;
        private const float GLOW_ALPHA = 0.55f;
        private const float AMPLITUDE = 0.18f;
        private const int FLICKER_TICKS = 3;

        private static readonly Color CoreColor = new Color(0.95f, 0.92f, 1f);

        [Header("표시")]
        [SerializeField] private SpriteRenderer[] _glowSegments;
        [SerializeField] private SpriteRenderer[] _coreSegments;

        private Vector2[] _points;

        void Awake()
        {
            _points = new Vector2[_glowSegments.Length + 1];
        }

        /// <summary>연쇄 구간의 두 위치와 발생 후 경과 틱으로 맵 중앙(origin) 기준 번개 선분들을 갱신한다.</summary>
        public void Apply(ArcLink link, int elapsedTicks, SimVector origin)
        {
            Vector2 from = MissionWorldSpace.ToWorld(link.From, origin) * MissionWorldSpace.PIXELS_PER_UNIT;
            Vector2 to = MissionWorldSpace.ToWorld(link.To, origin) * MissionWorldSpace.PIXELS_PER_UNIT;
            transform.localPosition = Vector3.zero;
            RuneMesh.ArcPoints(from, to, link.Tick + elapsedTicks / FLICKER_TICKS, AMPLITUDE, _points);
            float fade = 1f - (float)elapsedTicks / (RuneSimulation.ARC_LINK_TICKS + 1);
            Color glow = RuneMesh.ElementColor("arc"); glow.a = GLOW_ALPHA * fade;
            Color core = CoreColor; core.a = fade;
            for (int i = 0; i < _glowSegments.Length; i++)
            {
                MissionWorldSpace.PlaceLine(_glowSegments[i], _points[i], _points[i + 1], GLOW_WIDTH, glow);
                MissionWorldSpace.PlaceLine(_coreSegments[i], _points[i], _points[i + 1], CORE_WIDTH, core);
            }
        }
    }
}
