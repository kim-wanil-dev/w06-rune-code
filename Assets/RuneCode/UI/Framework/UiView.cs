using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// MVP의 View 기본형이다. 직렬화 참조와 표시 함수, 사용자 입력 이벤트만 두고 규칙 판단은 Presenter에 맡긴다.
    /// UIManager가 Prefab에서 한 번 만들어 숨김·재표시로 재사용한다.
    /// </summary>
    public abstract class UiView : MonoBehaviour
    {
        /// <summary>현재 표시 중인지 반환한다.</summary>
        public bool IsVisible => gameObject.activeSelf;

        /// <summary>View를 표시한다. 재표시할 때마다 호출되므로 진입 시 갱신은 OnShown에서 한다.</summary>
        public void Show()
        {
            if (IsVisible) return;
            gameObject.SetActive(true);
            OnShown();
        }

        /// <summary>View를 숨긴다. 숨길 때 정리할 작업은 OnHidden에서 한다.</summary>
        public void Hide()
        {
            if (!IsVisible) return;
            OnHidden();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Esc 입력을 처리하고 소비했으면 true를 반환한다. UIManager가 맨 위 팝업, 없으면 현재 화면에 전달한다.
        /// 기본 구현은 처리하지 않는다.
        /// </summary>
        public virtual bool HandleEscape() => false;

        /// <summary>표시된 직후 호출된다.</summary>
        protected virtual void OnShown() { }

        /// <summary>숨기기 직전 호출된다.</summary>
        protected virtual void OnHidden() { }
    }
}
