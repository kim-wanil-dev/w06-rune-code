using System;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 보관함 마법 목록 팝업 View다. 보관함 열기(새로 만들기·복제 버튼 표시)와 프리셋 호출 대상 선택(버튼 숨김)에 같이 쓴다.
    /// 목록 행은 재사용한다.
    /// </summary>
    public sealed class SpellListPopup : UiPopup
    {
        [Header("목록")]
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private RectTransform _list;
        [SerializeField] private UiRow _rowPrefab;

        [Header("보관함 버튼")]
        [SerializeField] private Button _newButton;
        [SerializeField] private Button _duplicateButton;

        private ViewPool<UiRow> _rows;
        private Action _onNew;
        private Action _onDuplicate;

        /// <summary>제목과 보관함 버튼 동작을 정하고 목록을 비운다. 버튼 동작이 null이면 그 버튼을 숨긴다.</summary>
        public void Configure(string title, Action onNew, Action onDuplicate)
        {
            if (_rows == null)
            {
                _rows = new ViewPool<UiRow>(_rowPrefab, _list);
                _newButton.onClick.AddListener(() => _onNew?.Invoke());
                _duplicateButton.onClick.AddListener(() => _onDuplicate?.Invoke());
            }
            _title.text = title;
            _onNew = onNew;
            _onDuplicate = onDuplicate;
            _newButton.gameObject.SetActive(onNew != null);
            _duplicateButton.gameObject.SetActive(onDuplicate != null);
            _rows.ReleaseAll();
        }

        /// <summary>마법 이름·ID 행을 하나 추가하고 클릭 동작을 연결한다.</summary>
        public void AddSpell(string name, string id, Action onClick)
        {
            _rows.Get().Configure(name + "\n" + id, UiTheme.Cyan, 50, onClick, 12);
        }
    }
}
