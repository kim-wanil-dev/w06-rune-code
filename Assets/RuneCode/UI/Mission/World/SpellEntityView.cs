using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 마법 개체 하나를 형태에 맞춰 표시한다. 발사·공전은 잔상과 속성별 도형, 폭발·잔류는 채움·외곽선·안쪽 선을 쓴다.
    /// 사각형 판정 개체는 사각형 모양으로 그리고, 범위 사각형은 똑바로 세우기 설정을 따른다.
    /// </summary>
    public sealed class SpellEntityView : MonoBehaviour
    {
        private const float TRAIL_LENGTH = 24f;
        private const float MIN_SHAPE_RADIUS = 2.5f;
        private const float MIN_TRAIL_WIDTH = 2f;
        private const float INNER_SCALE = 0.6f;
        private const float ZONE_FILL_ALPHA = 0.12f;
        private const float BURST_FILL_ALPHA = 0.22f;
        private const float TRAIL_ALPHA = 0.25f;

        [Header("모양")]
        [SerializeField] private Sprite _fireShape;
        [SerializeField] private Sprite _iceShape;
        [SerializeField] private Sprite _arcShape;
        [SerializeField] private Sprite _defaultShape;
        [SerializeField] private Sprite _squareShape;
        [SerializeField] private Sprite _ringShape;
        [SerializeField] private Sprite _squareOutlineShape;

        [Header("표시")]
        [SerializeField] private SpriteRenderer _trail;
        [SerializeField] private SpriteRenderer _shape;
        [SerializeField] private SpriteRenderer _fill;
        [SerializeField] private SpriteRenderer _outline;
        [SerializeField] private SpriteRenderer _inner;

        /// <summary>마법 개체의 형태·속성·크기·방향으로 맵 중앙(origin) 기준 월드 표시를 갱신한다.</summary>
        public void Apply(SimulationSpellEntity spell, bool isAreaBoxUpright, SimVector origin)
        {
            transform.localPosition = MissionWorldSpace.ToWorld(spell.Position, origin);
            Color tint = RuneMesh.ElementColor(spell.Element);
            float radius = (float)spell.Radius;
            float angle = MissionWorldSpace.ToWorldAngle(spell.Direction);
            // 사각형 판정과 같은 회전을 쓴다. 발사는 항상 진행 방향, 범위·공전은 똑바로 세우기 설정을 따른다.
            float boxRotation = spell.Kind != SpellGrammar.FORM_BOLT && isAreaBoxUpright ? 0 : angle;
            bool isArea = spell.Kind == SpellGrammar.FORM_ZONE || spell.Kind == SpellGrammar.FORM_BURST;
            MissionWorldSpace.SetVisible(_trail, !isArea);
            MissionWorldSpace.SetVisible(_shape, !isArea);
            MissionWorldSpace.SetVisible(_fill, isArea);
            MissionWorldSpace.SetVisible(_outline, isArea);
            MissionWorldSpace.SetVisible(_inner, isArea);
            if (isArea) ApplyArea(spell, tint, radius, boxRotation);
            else ApplyProjectile(spell, tint, radius, angle, boxRotation);
        }

        /// <summary>폭발·잔류를 원 또는 사각형의 채움, 외곽선, 안쪽 선으로 표시한다.</summary>
        private void ApplyArea(SimulationSpellEntity spell, Color tint, float radius, float boxRotation)
        {
            Color fill = tint; fill.a = spell.Kind == SpellGrammar.FORM_ZONE ? ZONE_FILL_ALPHA : BURST_FILL_ALPHA;
            _fill.sprite = spell.IsBox ? _squareShape : spell.Element == "ice" ? _iceShape : _defaultShape;
            _outline.sprite = _inner.sprite = spell.IsBox ? _squareOutlineShape : _ringShape;
            Quaternion rotation = Quaternion.Euler(0, 0, spell.IsBox ? boxRotation : 0);
            MissionWorldSpace.Place(_fill, Vector2.zero, radius * 2, fill);
            MissionWorldSpace.Place(_outline, Vector2.zero, radius * 2, tint);
            MissionWorldSpace.Place(_inner, Vector2.zero, radius * 2 * INNER_SCALE, fill * 2);
            _fill.transform.localRotation = _outline.transform.localRotation = _inner.transform.localRotation = rotation;
        }

        /// <summary>발사·공전을 진행 방향 잔상과 속성별 도형(사각형 판정이면 사각형)으로 표시한다.</summary>
        private void ApplyProjectile(SimulationSpellEntity spell, Color tint, float radius, float angle, float boxRotation)
        {
            Vector2 direction = MissionWorldSpace.ToWorldDirection(spell.Direction);
            Color trail = tint; trail.a = TRAIL_ALPHA;
            MissionWorldSpace.PlaceLine(_trail, -direction * TRAIL_LENGTH, Vector2.zero, Mathf.Max(MIN_TRAIL_WIDTH, radius), trail);
            _shape.sprite = spell.IsBox ? _squareShape : GetElementShape(spell.Element);
            MissionWorldSpace.Place(_shape, Vector2.zero, Mathf.Max(MIN_SHAPE_RADIUS, radius) * 2, tint);
            _shape.transform.localRotation = Quaternion.Euler(0, 0, spell.IsBox ? boxRotation : angle);
        }

        /// <summary>속성별 투사체 도형을 반환한다. 화염은 삼각형, 냉기는 육각형, 전격은 마름모, 그 외는 원이다.</summary>
        private Sprite GetElementShape(string element)
        {
            switch (element)
            {
                case "fire": return _fireShape;
                case "ice": return _iceShape;
                case "arc": return _arcShape;
                default: return _defaultShape;
            }
        }
    }
}
