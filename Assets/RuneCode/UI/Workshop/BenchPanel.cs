using System;

using UnityEngine;

using TMPro;

namespace RuneCode
{
    /// <summary>
    /// 벤치 탭 View다. 보유 조각, 성장 카드 3장과 룬 해금 목록을 표시하고 카드 구매·룬 행 클릭을 알린다.
    /// 룬 행은 갱신 때마다 파괴하지 않고 재사용한다.
    /// </summary>
    public sealed class BenchPanel : MonoBehaviour
    {
        [Header("성장 카드")]
        [SerializeField] private TextMeshProUGUI _balanceText;
        [SerializeField] private UpgradeCardView[] _upgradeCards;

        [Header("룬 해금")]
        [SerializeField] private RectTransform _unlockList;
        [SerializeField] private UiRow _runeRowPrefab;

        private ViewPool<UiRow> _runeRows;

        /// <summary>성장 카드 수를 반환한다.</summary>
        public int CardCount => _upgradeCards.Length;

        /// <summary>성장 카드 구매 버튼을 눌렀을 때 카드 순번과 함께 알린다.</summary>
        public event Action<int> CardPurchaseClicked;

        /// <summary>카드 구매 버튼을 이벤트로 연결하고 룬 행 풀을 만든다. Presenter가 한 번만 호출한다.</summary>
        public void Bind()
        {
            for (int i = 0; i < _upgradeCards.Length; i++)
            {
                int index = i;
                _upgradeCards[i].SetPurchaseAction(() => CardPurchaseClicked?.Invoke(index));
            }
            _runeRows = new ViewPool<UiRow>(_runeRowPrefab, _unlockList);
        }

        /// <summary>보유 조각 문구를 표시한다.</summary>
        public void SetBalance(string text) => _balanceText.text = text;

        /// <summary>지정 순번 성장 카드의 제목·값·설명·구매 문구와 강조 색을 표시한다.</summary>
        public void SetCard(int index, string title, string value, string description, string purchaseLabel, Color accent)
        {
            _upgradeCards[index].SetContent(title, value, description, purchaseLabel, accent);
        }

        /// <summary>룬 해금 목록을 비운다. 이어서 AddRuneRow로 행을 다시 채운다.</summary>
        public void ClearRuneRows() => _runeRows.ReleaseAll();

        /// <summary>룬 해금 목록에 문구·강조 색·클릭 동작의 행을 하나 추가한다.</summary>
        public void AddRuneRow(string label, Color accent, Action onClick)
        {
            _runeRows.Get().Configure(label, accent, 44, onClick, 15);
        }
    }
}
