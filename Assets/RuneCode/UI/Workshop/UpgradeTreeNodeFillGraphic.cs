using UnityEngine;

using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>프리팹 색상의 채움 메시를 노드 테두리 안에 아래부터 레벨 비율만큼 그린다.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UpgradeTreeNodeFillGraphic : MaskableGraphic
    {
        [Header("테두리 안쪽 채움 영역")]
        [SerializeField, Min(0)] private float _inset = 3;
        [SerializeField, Min(0)] private float _cornerCut = 5;

        private readonly Vector2[] _outline = new Vector2[8];
        private readonly Vector2[] _clipped = new Vector2[10];
        private float _fillAmount = 1;

        /// <summary>채움 비율을 0~1로 제한하고 바뀐 경우 노드 메시를 다시 그리도록 요청한다.</summary>
        public void SetFill(float amount)
        {
            float clamped = Mathf.Clamp01(amount);
            if (Mathf.Approximately(_fillAmount, clamped)) return;
            _fillAmount = clamped;
            SetVerticesDirty();
        }

        /// <summary>모서리가 꺾인 내부 영역을 채움 높이로 잘라 프리팹의 Graphic Color로 메시를 생성한다.</summary>
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            float inset = Mathf.Min(_inset, Mathf.Min(bounds.width, bounds.height) * 0.5f);
            float left = bounds.xMin + inset;
            float right = bounds.xMax - inset;
            float bottom = bounds.yMin + inset;
            float top = bounds.yMax - inset;
            if (_fillAmount <= 0 || right <= left || top <= bottom) return;
            float cut = Mathf.Min(_cornerCut, Mathf.Min(right - left, top - bottom) * 0.5f);
            _outline[0] = new Vector2(left + cut, bottom);
            _outline[1] = new Vector2(right - cut, bottom);
            _outline[2] = new Vector2(right, bottom + cut);
            _outline[3] = new Vector2(right, top - cut);
            _outline[4] = new Vector2(right - cut, top);
            _outline[5] = new Vector2(left + cut, top);
            _outline[6] = new Vector2(left, top - cut);
            _outline[7] = new Vector2(left, bottom + cut);
            float fillTop = Mathf.Lerp(bottom, top, _fillAmount);
            int count = 0;
            Vector2 previous = _outline[_outline.Length - 1];
            foreach (Vector2 current in _outline)
            {
                bool wasInside = previous.y <= fillTop;
                bool isInside = current.y <= fillTop;
                if (wasInside != isInside)
                    _clipped[count++] = Vector2.Lerp(previous, current, (fillTop - previous.y) / (current.y - previous.y));
                if (isInside) _clipped[count++] = current;
                previous = current;
            }
            for (int index = 0; index < count; index++)
                mesh.AddVert(_clipped[index], color, Vector2.zero);
            for (int index = 1; index < count - 1; index++)
                mesh.AddTriangle(0, index, index + 1);
        }
    }
}
