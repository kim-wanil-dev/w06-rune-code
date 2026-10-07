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
                case "core": return new Color(0.98f, 0.79f, 0.37f);
                case "form": return new Color(0.22f, 0.82f, 0.96f);
                case "element": return new Color(0.99f, 0.43f, 0.34f);
                case "modifier": return new Color(0.64f, 0.46f, 0.97f);
                case "flow": return new Color(0.30f, 0.91f, 0.65f);
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
