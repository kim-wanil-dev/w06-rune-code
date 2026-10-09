using System;

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

        private ViewPool<UiRow> _rows;

        /// <summary>검색어가 바뀌었을 때 알린다.</summary>
        public event Action<string> SearchChanged;

        /// <summary>현재 검색어다.</summary>
        public string Query => _search.text;

        /// <summary>검색 입력을 처음 한 번만 이벤트로 연결하고 검색어를 비운 뒤 입력 칸에 포커스를 준다.</summary>
        public void Begin()
        {
            if (_rows == null)
            {
                _rows = new ViewPool<UiRow>(_rowPrefab, _list);
                _search.onValueChanged.AddListener(query => SearchChanged?.Invoke(query));
            }
            _search.SetTextWithoutNotify("");
            _search.ActivateInputField();
        }

        /// <summary>결과 목록을 비운다.</summary>
        public void ClearResults() => _rows.ReleaseAll();

        /// <summary>결과 행을 하나 추가하고 클릭 동작을 연결한다.</summary>
        public void AddResult(string label, Color accent, Action onClick)
        {
            _rows.Get().Configure(label, accent, 36, onClick, 14);
        }
    }
}
