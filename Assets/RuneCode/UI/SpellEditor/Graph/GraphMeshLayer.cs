using UnityEngine;

namespace RuneCode
{
    /// <summary>노드 본체 또는 조작 표시를 별도 uGUI 레이어에 그려 텍스트와의 앞뒤 순서를 유지한다.</summary>
    public sealed class GraphMeshLayer : UnityEngine.UI.MaskableGraphic
    {
        private GraphMeshPainter _painter;
        private GraphNode _node;

        /// <summary>부모와 같은 좌표계를 가진 레이어를 생성하고 포인터 입력을 통과시킨다.</summary>
        internal static GraphMeshLayer Create(RectTransform parent, string name)
        {
            var layerObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(GraphMeshLayer));
            var layer = layerObject.GetComponent<GraphMeshLayer>();
            RectTransform rect = layer.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = parent.pivot;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            layer.raycastTarget = false;
            return layer;
        }

        /// <summary>그리기 도구와 현재 노드를 연결한다. 노드가 없으면 최상단 조작 표시를 그린다.</summary>
        internal void Initialize(GraphMeshPainter painter, GraphNode node = null)
        {
            _painter = painter;
            _node = node;
            SetVerticesDirty();
        }

        /// <summary>현재 노드 또는 조작 표시의 메시를 생성한다.</summary>
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            if (_painter == null) mesh.Clear();
            else if (_node != null) _painter.PaintNode(mesh, _node);
            else _painter.PaintOverlay(mesh);
        }
    }
}
