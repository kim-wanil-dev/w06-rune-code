using System;

using UnityEngine;

namespace RuneCode
{
    /// <summary>룬 분류 ID와 해당 분류에 사용할 팔레트 행 프리팹을 연결한다.</summary>
    [Serializable]
    public sealed class RunePaletteRowBinding
    {
        [Header("분류별 행")]
        [SerializeField] private string _category;
        [SerializeField] private UiRow _prefab;

        public string Category => _category;
        public UiRow Prefab => _prefab;

        /// <summary>category에 연결된 프리팹을 반환한다. 연결이 없거나 비어 있으면 fallback을 사용한다.</summary>
        public static UiRow Resolve(RunePaletteRowBinding[] bindings, string category, UiRow fallback)
        {
            foreach (RunePaletteRowBinding binding in bindings)
                if (binding != null && binding.Category == category && binding.Prefab != null) return binding.Prefab;
            return fallback;
        }
    }
}
