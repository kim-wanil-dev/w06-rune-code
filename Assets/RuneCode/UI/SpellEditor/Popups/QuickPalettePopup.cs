using System;
using System.Collections.Generic;

using UnityEngine;

using TMPro;

namespace RuneCode
{
    /// <summary>
    /// 그래프 빈 곳 더블클릭으로 여는 빠른 룬 검색 팝업 View다. 검색어 변경을 알리고 결과 행을 재사용해 표시한다.
    /// </summary>
    public sealed class QuickPalettePopup : UiPopup
    {
        [Header("검색")]
        [SerializeField] private TMP_InputField _search;
        [SerializeField] private RectTransform _list;
        [SerializeField] private UiRow _rowPrefab;
        [SerializeField] private RunePaletteRowBinding[] _categoryRowPrefabs = Array.Empty<RunePaletteRowBinding>();

        private readonly Dictionary<UiRow, ViewPool<UiRow>> _rows = new Dictionary<UiRow, ViewPool<UiRow>>();
        private bool _isBound;

        /// <summary>검색어가 바뀌었을 때 알린다.</summary>
        public event Action<string> SearchChanged;

        /// <summary>현재 검색어다.</summary>
        public string Query => _search.text;

        /// <summary>검색 입력을 처음 한 번만 이벤트로 연결하고 검색어를 비운 뒤 입력 칸에 포커스를 준다.</summary>
        public void Begin()
        {
            if (!_isBound)
            {
                _search.onValueChanged.AddListener(query => SearchChanged?.Invoke(query));
                _isBound = true;
            }
            _search.SetTextWithoutNotify("");
            _search.ActivateInputField();
        }

        /// <summary>결과 목록을 비운다.</summary>
        public void ClearResults()
        {
            foreach (ViewPool<UiRow> rows in _rows.Values) rows.ReleaseAll();
        }

        /// <summary>category에 연결된 프리팹으로 결과 행을 추가하고 문구와 클릭 동작을 연결한다.</summary>
        public void AddResult(string label, Action onClick, string category = null)
        {
            UiRow prefab = RunePaletteRowBinding.Resolve(_categoryRowPrefabs, category, _rowPrefab);
            if (!_rows.TryGetValue(prefab, out ViewPool<UiRow> rows))
            {
                rows = new ViewPool<UiRow>(prefab, _list);
                _rows.Add(prefab, rows);
            }
            rows.Get().Configure(label, SpellPalettePresenter.ROW_HEIGHT, onClick, 14);
        }
    }
}
