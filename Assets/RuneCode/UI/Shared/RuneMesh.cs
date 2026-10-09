using UnityEngine;

namespace RuneCode
{
    internal static class RuneMesh
    {
        /// <summary>사각형의 위치와 색상으로 UI 메시의 네 꼭짓점과 삼각형을 추가한다.</summary>
        internal static void Rect(UnityEngine.UI.VertexHelper mesh, Rect rect, Color color)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }

        /// <summary>두 점 사이에 지정 두께와 색상의 선분 메시를 추가한다.</summary>
        internal static void Line(UnityEngine.UI.VertexHelper mesh, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 perpendicular = new Vector2(-(to - from).y, (to - from).x).normalized * width * 0.5f;
            int index = mesh.currentVertCount;
            mesh.AddVert(from + perpendicular, color, Vector2.zero);
            mesh.AddVert(to + perpendicular, color, Vector2.zero);
            mesh.AddVert(to - perpendicular, color, Vector2.zero);
            mesh.AddVert(from - perpendicular, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }

        /// <summary>중심, 반경, 변 수와 색상으로 원 또는 다각형 메시를 추가한다.</summary>
        internal static void Polygon(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius, Color color, int sides = 20, float rotation = 0)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i <= sides; i++)
            {
                float angle = rotation + i * Mathf.PI * 2 / sides;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(index, index + i, index + i + 1);
            }
        }

        /// <summary>중심, 반 변 길이와 회전(라디안)으로 채워진 정사각형 메시를 추가한다. 회전 0이면 화면 축에 맞춰 선다.</summary>
        internal static void Square(UnityEngine.UI.VertexHelper mesh, Vector2 center, float halfSize, float rotation, Color color)
        {
            // 꼭짓점이 대각선 방향에 오도록 외접 반경과 45도 보정으로 4각형을 그린다.
            Polygon(mesh, center, halfSize * Mathf.Sqrt(2), color, 4, rotation + Mathf.PI / 4);
        }

        /// <summary>중심, 반 변 길이와 회전(라디안)으로 지정 두께의 정사각형 윤곽선을 추가한다.</summary>
        internal static void SquareOutline(UnityEngine.UI.VertexHelper mesh, Vector2 center, float halfSize, float rotation, float width, Color color)
        {
            Vector2 axisX = new Vector2(Mathf.Cos(rotation), Mathf.Sin(rotation)) * halfSize;
            Vector2 axisY = new Vector2(-axisX.y, axisX.x);
            Vector2[] corners = { center - axisX - axisY, center + axisX - axisY, center + axisX + axisY, center - axisX + axisY };
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 from = corners[i];
                Vector2 to = corners[(i + 1) % corners.Length];

                // 선 두께의 절반만큼 양 끝을 늘려 모서리 이음새가 비지 않게 한다.
                Vector2 extension = (to - from).normalized * width * 0.5f;
                Line(mesh, from - extension, to + extension, width, color);
            }
        }

        /// <summary>꼭짓점, 반경, 중심 방향(라디안)과 전체 각도(라디안)로 채워진 부채꼴 메시를 추가한다.</summary>
        internal static void Sector(UnityEngine.UI.VertexHelper mesh, Vector2 apex, float radius, float rotation, float angle, Color color, int segments = 16)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(apex, color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float current = rotation - angle * 0.5f + angle * i / segments;
                mesh.AddVert(apex + new Vector2(Mathf.Cos(current), Mathf.Sin(current)) * radius, color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(index, index + i, index + i + 1);
            }
        }

        /// <summary>꼭짓점, 반경, 중심 방향(라디안)과 전체 각도(라디안)로 부채꼴 윤곽선(두 변과 호)을 추가한다.</summary>
        internal static void SectorOutline(UnityEngine.UI.VertexHelper mesh, Vector2 apex, float radius, float rotation, float angle, float width, Color color, int segments = 16)
        {
            Vector2 previous = apex;
            for (int i = 0; i <= segments; i++)
            {
                float current = rotation - angle * 0.5f + angle * i / segments;
                Vector2 point = apex + new Vector2(Mathf.Cos(current), Mathf.Sin(current)) * radius;
                Line(mesh, previous, point, width, color);
                previous = point;
            }
            Line(mesh, previous, apex, width, color);
        }

        /// <summary>중심과 반경으로 지정 색상의 원형 윤곽선을 추가한다.</summary>
        internal static void Ring(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius, float width, Color color, int sides = 24)
        {
            for (int i = 0; i < sides; i++)
            {
                float first = i * Mathf.PI * 2 / sides;
                float second = (i + 1) * Mathf.PI * 2 / sides;
                Line(mesh, center + new Vector2(Mathf.Cos(first), Mathf.Sin(first)) * radius,
                    center + new Vector2(Mathf.Cos(second), Mathf.Sin(second)) * radius, width, color);
            }
        }

        /// <summary>룬 계열을 구별할 수 있는 화면 색상을 반환한다.</summary>
        internal static Color CategoryColor(string category)
        {
            switch (category)
            {
                case SpellGrammar.CATEGORY_CORE: return new Color(0.98f, 0.79f, 0.37f);
                case SpellGrammar.CATEGORY_LEGACY_FORM:
                case SpellGrammar.CATEGORY_BEHAVIOR: return new Color(0.22f, 0.82f, 0.96f);
                case SpellGrammar.CATEGORY_SHAPE: return new Color(0.95f, 0.88f, 0.55f);
                case SpellGrammar.CATEGORY_ELEMENT: return new Color(0.99f, 0.43f, 0.34f);
                case "method": return new Color(0.42f, 0.70f, 1f);
                case SpellGrammar.CATEGORY_MODIFIER: return new Color(0.64f, 0.46f, 0.97f);
                case SpellGrammar.CATEGORY_FLOW: return new Color(0.30f, 0.91f, 0.65f);
                default: return new Color(0.97f, 0.62f, 0.28f);
            }
        }

        /// <summary>피해 속성의 시각적 구분에 사용하는 색상을 반환한다.</summary>
        internal static Color ElementColor(string element)
        {
            switch (element)
            {
                case "fire": return new Color(1f, 0.40f, 0.25f);
                case "ice": return new Color(0.34f, 0.83f, 1f);
                case "arc": return new Color(0.72f, 0.52f, 1f);
                default: return new Color(0.81f, 0.96f, 0.78f);
            }
        }
    }
}
