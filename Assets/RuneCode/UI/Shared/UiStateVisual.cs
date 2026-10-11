using UnityEngine;

namespace RuneCode
{
    /// <summary>프리팹에서 편집한 상태별 표시 오브젝트 중 하나만 활성화한다.</summary>
    public sealed class UiStateVisual : MonoBehaviour
    {
        [Header("상태별 표시 (순서대로)")]
        [SerializeField] private GameObject[] _states;

        [Header("선택적 버튼 상호작용")]
        [SerializeField] private UnityEngine.UI.Selectable _selectable;

        /// <summary>state 표시만 켜고, 연결된 버튼이 있으면 현재 표시를 Color Tint 대상으로 지정한다. 색상과 이미지는 변경하지 않는다.</summary>
        public void Show(int state)
        {
            for (int i = 0; i < _states.Length; i++) _states[i].SetActive(i == state);
            if (_selectable != null) _selectable.targetGraphic = _states[state].GetComponent<UnityEngine.UI.Graphic>();
        }
    }
}
