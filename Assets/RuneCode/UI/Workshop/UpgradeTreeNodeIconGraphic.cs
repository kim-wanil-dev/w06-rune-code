using UnityEngine;

using UnityEngine.UI;

namespace RuneCode
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UpgradeTreeNodeIconGraphic : MaskableGraphic
    {
        private const float ICON_LINE_WIDTH = 2.2f;

        private UpgradeEffectType _effectType;
        private string _runeCategory;

        /// <summary>노드 효과와 룬 분류를 저장해 해당 노드에 맞는 벡터 아이콘을 그리도록 갱신한다.</summary>
        public void Configure(UpgradeEffectType effectType, string runeCategory)
        {
            _effectType = effectType;
            _runeCategory = runeCategory;
            SetVerticesDirty();
        }

        /// <summary>노드 효과 종류 또는 룬 분류에 맞는 아이콘 메시를 생성한다.</summary>
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            float scale = Mathf.Min(bounds.width, bounds.height) / 40f;
            Vector2 center = bounds.center;
            Color tint = color;
            if (_effectType == UpgradeEffectType.EnergyRegen) DrawRegenerationIcon(mesh, center, scale, tint);
            else if (_effectType == UpgradeEffectType.MaxEnergy) DrawEnergyIcon(mesh, center, scale, tint);
            else if (_effectType == UpgradeEffectType.RamCapacity) DrawMemoryIcon(mesh, center, scale, tint);
            else if (_effectType == UpgradeEffectType.CastSpeed) DrawSpeedIcon(mesh, center, scale, tint);
            else if (_effectType == UpgradeEffectType.Damage) DrawDamageIcon(mesh, center, scale, tint);
            else if (_effectType == UpgradeEffectType.ScrapGain) DrawScrapIcon(mesh, center, scale, tint);
            else if (_effectType == UpgradeEffectType.ScrapPickupRange) DrawPickupRangeIcon(mesh, center, scale, tint);
            else if (_effectType == UpgradeEffectType.MaxHp) DrawHealthIcon(mesh, center, scale, tint);
            else if (_effectType == UpgradeEffectType.MoveSpeed) DrawMovementIcon(mesh, center, scale, tint);
            else DrawRuneIcon(mesh, center, scale, tint, _runeCategory);
        }

        /// <summary>CPU의 임시 아이콘으로 중심 칩과 빠른 진행 화살표를 그린다.</summary>
        private static void DrawSpeedIcon(VertexHelper mesh, Vector2 center, float scale, Color tint)
        {
            RuneMesh.SquareOutline(mesh, center, 10 * scale, 0, ICON_LINE_WIDTH * scale, tint);
            RuneMesh.Line(mesh, center + new Vector2(-5, -3) * scale, center + new Vector2(6, -3) * scale, 2 * scale, tint);
            RuneMesh.Polygon(mesh, center + new Vector2(8, -3) * scale, 4 * scale, tint, 3, -Mathf.PI * 0.5f);
        }

        /// <summary>GPU의 임시 아이콘으로 중앙의 피해 표적과 교차선을 그린다.</summary>
        private static void DrawDamageIcon(VertexHelper mesh, Vector2 center, float scale, Color tint)
        {
            RuneMesh.Ring(mesh, center, 10 * scale, ICON_LINE_WIDTH * scale, tint, 20);
            RuneMesh.Line(mesh, center + Vector2.left * 16 * scale, center + Vector2.right * 16 * scale, 2 * scale, tint);
            RuneMesh.Line(mesh, center + Vector2.up * 16 * scale, center + Vector2.down * 16 * scale, 2 * scale, tint);
        }

        /// <summary>스크랩 획득량의 임시 아이콘으로 중첩된 자원 조각을 그린다.</summary>
        private static void DrawScrapIcon(VertexHelper mesh, Vector2 center, float scale, Color tint)
        {
            RuneMesh.SquareOutline(mesh, center + new Vector2(-5, 4) * scale, 7 * scale, Mathf.PI * 0.25f, 2 * scale, tint);
            RuneMesh.SquareOutline(mesh, center + new Vector2(5, -4) * scale, 7 * scale, Mathf.PI * 0.25f, 2 * scale, tint);
        }

        /// <summary>스크랩 줍기 범위의 임시 아이콘으로 중심점 주위의 탐지 링을 그린다.</summary>
        private static void DrawPickupRangeIcon(VertexHelper mesh, Vector2 center, float scale, Color tint)
        {
            RuneMesh.Ring(mesh, center, 12 * scale, ICON_LINE_WIDTH * scale, tint, 20);
            RuneMesh.Polygon(mesh, center, 3 * scale, tint, 12);
        }

        /// <summary>최대 HP의 임시 아이콘으로 십자형 체력 표시를 그린다.</summary>
        private static void DrawHealthIcon(VertexHelper mesh, Vector2 center, float scale, Color tint)
        {
            RuneMesh.Line(mesh, center + Vector2.left * 13 * scale, center + Vector2.right * 13 * scale, 6 * scale, tint);
            RuneMesh.Line(mesh, center + Vector2.up * 13 * scale, center + Vector2.down * 13 * scale, 6 * scale, tint);
        }

        /// <summary>이동속도의 임시 아이콘으로 두 개의 진행 화살표를 그린다.</summary>
        private static void DrawMovementIcon(VertexHelper mesh, Vector2 center, float scale, Color tint)
        {
            RuneMesh.Line(mesh, center + new Vector2(-15, 7) * scale, center + new Vector2(7, 7) * scale, 2 * scale, tint);
            RuneMesh.Line(mesh, center + new Vector2(-15, -7) * scale, center + new Vector2(7, -7) * scale, 2 * scale, tint);
            RuneMesh.Polygon(mesh, center + new Vector2(11, 7) * scale, 5 * scale, tint, 3, -Mathf.PI * 0.5f);
            RuneMesh.Polygon(mesh, center + new Vector2(11, -7) * scale, 5 * scale, tint, 3, -Mathf.PI * 0.5f);
        }

        /// <summary>회복을 순환 화살표와 링으로 표시한다.</summary>
        private static void DrawRegenerationIcon(VertexHelper mesh, Vector2 center, float scale, Color tint)
        {
            RuneMesh.Ring(mesh, center, 12 * scale, ICON_LINE_WIDTH * scale, tint, 20);
            Vector2 arrowCenter = center + new Vector2(9, 10) * scale;
            RuneMesh.Polygon(mesh, arrowCenter, 5.5f * scale, tint, 3, Mathf.PI * 0.15f);
            RuneMesh.Line(mesh, center + new Vector2(-11, -4) * scale,
                center + new Vector2(-8, -11) * scale, ICON_LINE_WIDTH * scale, tint);
        }

        /// <summary>에너지 최대량을 배터리 테두리와 충전 표시로 그린다.</summary>
        private static void DrawEnergyIcon(VertexHelper mesh, Vector2 center, float scale, Color tint)
        {
            Vector2 bottomLeft = center + new Vector2(-12, -9) * scale;
            Vector2 topLeft = center + new Vector2(-12, 9) * scale;
            Vector2 bottomRight = center + new Vector2(11, -9) * scale;
            Vector2 topRight = center + new Vector2(11, 9) * scale;
            RuneMesh.Line(mesh, bottomLeft, topLeft, ICON_LINE_WIDTH * scale, tint);
            RuneMesh.Line(mesh, topLeft, topRight, ICON_LINE_WIDTH * scale, tint);
            RuneMesh.Line(mesh, topRight, bottomRight, ICON_LINE_WIDTH * scale, tint);
            RuneMesh.Line(mesh, bottomRight, bottomLeft, ICON_LINE_WIDTH * scale, tint);
            RuneMesh.Line(mesh, center + new Vector2(11, -4) * scale,
                center + new Vector2(15, -4) * scale, ICON_LINE_WIDTH * scale, tint);
            RuneMesh.Line(mesh, center + new Vector2(15, -4) * scale,
                center + new Vector2(15, 4) * scale, ICON_LINE_WIDTH * scale, tint);
            RuneMesh.Line(mesh, center + new Vector2(15, 4) * scale,
                center + new Vector2(11, 4) * scale, ICON_LINE_WIDTH * scale, tint);
            RuneMesh.Line(mesh, center + new Vector2(-5, 0) * scale,
                center + new Vector2(4, 0) * scale, ICON_LINE_WIDTH * scale, tint);
            RuneMesh.Line(mesh, center + new Vector2(-0.5f, -4.5f) * scale,
                center + new Vector2(-0.5f, 4.5f) * scale, ICON_LINE_WIDTH * scale, tint);
        }

        /// <summary>RAM 용량을 메모리 칩과 네 방향 연결 핀으로 표현한다.</summary>
        private static void DrawMemoryIcon(VertexHelper mesh, Vector2 center, float scale, Color tint)
        {
            float width = ICON_LINE_WIDTH * scale;
            RuneMesh.SquareOutline(mesh, center, 9 * scale, 0, width, tint);
            RuneMesh.SquareOutline(mesh, center, 5 * scale, 0, width, tint);
            for (int index = -1; index <= 1; index++)
            {
                float offset = index * 6;
                RuneMesh.Line(mesh, center + new Vector2(offset, 9) * scale,
                    center + new Vector2(offset, 14) * scale, width, tint);
                RuneMesh.Line(mesh, center + new Vector2(offset, -9) * scale,
                    center + new Vector2(offset, -14) * scale, width, tint);
                RuneMesh.Line(mesh, center + new Vector2(9, offset) * scale,
                    center + new Vector2(14, offset) * scale, width, tint);
                RuneMesh.Line(mesh, center + new Vector2(-9, offset) * scale,
                    center + new Vector2(-14, offset) * scale, width, tint);
            }
        }

        /// <summary>룬 분류에 맞는 다각형 실루엣과 중앙 표식을 그린다.</summary>
        private static void DrawRuneIcon(VertexHelper mesh, Vector2 center, float scale, Color tint, string runeCategory)
        {
            int sides = runeCategory == "element" ? 3 : runeCategory == "flow" ? 6 : runeCategory == "modifier" ? 8 : 4;
            float rotation = runeCategory == "shape" ? 0 : Mathf.PI * 0.25f;
            DrawPolygonOutline(mesh, center, 13 * scale, sides, rotation, ICON_LINE_WIDTH * scale, tint);
            RuneMesh.Ring(mesh, center, 4 * scale, ICON_LINE_WIDTH * scale, tint, sides);
        }

        /// <summary>다각형의 변마다 선분 메시를 추가해 룬 실루엣의 윤곽을 그린다.</summary>
        private static void DrawPolygonOutline(VertexHelper mesh, Vector2 center, float radius, int sides, float rotation,
            float width, Color tint)
        {
            Vector2 previous = center + new Vector2(Mathf.Cos(rotation), Mathf.Sin(rotation)) * radius;
            for (int index = 1; index <= sides; index++)
            {
                float angle = rotation + index * Mathf.PI * 2 / sides;
                Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                RuneMesh.Line(mesh, previous, next, width, tint);
                previous = next;
            }
        }
    }
}
