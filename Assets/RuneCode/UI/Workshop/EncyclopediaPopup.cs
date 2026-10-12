using System.Collections.Generic;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>전달받은 게임 도움말의 목차와 설명을 독립적으로 스크롤하는 공용 팝업이다.</summary>
    public sealed class EncyclopediaPopup : UiPopup
    {
        private const string EMPTY_TITLE = "도움말 준비 중";
        private const string EMPTY_BODY = "표시할 도움말이 없습니다. 잠시 후 다시 확인해 주세요.";

        [Header("목차")]
        [SerializeField] private ScrollRect _contentsScroll;
        [SerializeField] private UiRow _rowPrefab;
        [SerializeField] private TextMeshProUGUI _entryCount;
        [SerializeField] private float _rowHeight = 54;

        private readonly List<UiRow> _rows = new List<UiRow>();
        private readonly List<TabButton> _tabs = new List<TabButton>();
        private EncyclopediaData _data;
        private int _selectedIndex = -1;

        [Header("설명")]
        [SerializeField] private ScrollRect _bodyScroll;
        [SerializeField] private TextMeshProUGUI _entryTitle;
        [SerializeField] private TextMeshProUGUI _entryBody;

        [Header("창 바깥 닫기")]
        [SerializeField] private Button _backgroundButton;

        void Awake()
        {
            _backgroundButton.onClick.AddListener(Close);
        }

        /// <summary>data의 목차 행을 보관해 재사용하고 첫 항목을 선택한 뒤 양쪽 스크롤을 맨 위로 초기화한다.</summary>
        public void Configure(EncyclopediaData data)
        {
            if (_data != data)
            {
                _data = data;
                ConfigureRows();
            }

            int count = _data?.Entries?.Count ?? 0;
            _entryCount.text = "전체 " + count + "개 항목";
            if (_selectedIndex >= 0) _tabs[_selectedIndex].SetSelected(false);
            _selectedIndex = -1;
            if (count > 0) SelectEntry(0);
            else
            {
                _entryTitle.text = EMPTY_TITLE;
                _entryBody.text = EMPTY_BODY;
                ResetScroll(_bodyScroll);
            }
            ResetScroll(_contentsScroll);
        }

        /// <summary>현재 데이터의 번호·제목·클릭 동작을 목차 행에 연결하고 여분의 행은 숨긴다.</summary>
        private void ConfigureRows()
        {
            int count = _data?.Entries?.Count ?? 0;
            for (int index = 0; index < count; index++)
            {
                if (index == _rows.Count)
                {
                    UiRow row = Instantiate(_rowPrefab, _contentsScroll.content, false);
                    _rows.Add(row);
                    _tabs.Add(row.GetComponent<TabButton>());
                }
                int entryIndex = index;
                UiRow current = _rows[index];
                current.gameObject.SetActive(true);
                current.Configure((index + 1).ToString("00") + "   " + (_data.Entries[index]?.Title ?? EMPTY_TITLE),
                    _rowHeight, () => SelectEntry(entryIndex), _rowPrefab.Label.fontSize);
            }
            for (int index = count; index < _rows.Count; index++) _rows[index].gameObject.SetActive(false);
        }

        /// <summary>index에 해당하는 제목·본문·목차 선택 표시를 갱신하고 오른쪽 스크롤만 초기화한다.</summary>
        private void SelectEntry(int index)
        {
            if (_data?.Entries == null || index < 0 || index >= _data.Entries.Count) return;
            if (_selectedIndex >= 0) _tabs[_selectedIndex].SetSelected(false);
            _selectedIndex = index;
            _tabs[index].SetSelected(true);
            EncyclopediaEntry entry = _data.Entries[index];
            _entryTitle.text = entry?.Title ?? EMPTY_TITLE;
            _entryBody.text = entry?.Body ?? EMPTY_BODY;
            ResetScroll(_bodyScroll);
        }

        /// <summary>scroll의 변경된 콘텐츠 높이를 반영하고 관성을 멈춘 뒤 세로 위치를 맨 위로 이동한다.</summary>
        private static void ResetScroll(ScrollRect scroll)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }
    }
}
