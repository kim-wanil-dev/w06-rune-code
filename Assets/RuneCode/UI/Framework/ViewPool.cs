using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 같은 부모 아래 반복 표시하는 목록 항목(행)을 재사용한다. 목록을 갱신할 때 ReleaseAll 후 필요한 수만큼 Get한다.
    /// 쓰지 않는 항목은 파괴하지 않고 비활성으로 보관한다.
    /// </summary>
    public sealed class ViewPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly List<T> _active = new List<T>();
        private readonly Stack<T> _idle = new Stack<T>();

        /// <summary>현재 사용 중인 항목을 표시 순서대로 반환한다.</summary>
        public IReadOnlyList<T> Active => _active;

        /// <summary>항목 Prefab과 항목을 둘 부모로 풀을 만든다.</summary>
        public ViewPool(T prefab, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
        }

        /// <summary>보관 중인 항목을 꺼내거나 새로 만들어 부모의 마지막 자식으로 활성화해 반환한다.</summary>
        public T Get()
        {
            T item = _idle.Count > 0 ? _idle.Pop() : Object.Instantiate(_prefab, _parent, false);
            item.transform.SetAsLastSibling();
            item.gameObject.SetActive(true);
            _active.Add(item);
            return item;
        }

        /// <summary>사용 중인 항목을 모두 비활성화해 보관한다.</summary>
        public void ReleaseAll()
        {
            foreach (T item in _active)
            {
                if (item == null) continue;
                item.gameObject.SetActive(false);
                _idle.Push(item);
            }
            _active.Clear();
        }
    }
}
