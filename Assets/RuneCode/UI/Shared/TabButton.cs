using UnityEngine;

namespace RuneCode
{
    /// <summary>탭 버튼 프리팹의 선택 표시를 제어한다. 클릭 동작은 화면에서 연결한다.</summary>
    public sealed class TabButton : MonoBehaviour
    {
        [Header("선택 표시")]
        [SerializeField] private UiStateVisual _visual;

        /// <summary>isSelected에 따라 기본 또는 선택 표시를 활성화한다.</summary>
        public void SetSelected(bool isSelected) => _visual.Show(isSelected ? 1 : 0);
    }
}
