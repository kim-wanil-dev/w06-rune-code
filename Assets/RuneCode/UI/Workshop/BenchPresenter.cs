using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 벤치 탭 Presenter다. 성장 항목과 벤치 룬의 현재 값·비용을 계산해 표시하고 구매를 세션에 요청한다.
    /// 구매에 성공하면 진행 변경 동작(도크 재시작·헤더 갱신)을 실행한다.
    /// </summary>
    public sealed class BenchPresenter
    {
        private static readonly string[] UPGRADE_TYPES = { "capacity", "energy", "duration" };

        private readonly RuneCodeSession _session;
        private readonly BenchPanel _view;
        private readonly Action _onPurchased;

        /// <summary>세션·벤치 View와 구매 성공 후 동작을 받아 카드 구매 이벤트를 연결한다.</summary>
        public BenchPresenter(RuneCodeSession session, BenchPanel view, Action onPurchased)
        {
            _session = session;
            _view = view;
            _onPurchased = onPurchased;
            _view.Bind();
            _view.CardPurchaseClicked += index => Purchase(UPGRADE_TYPES[index]);
        }

        /// <summary>보유 조각, 성장 카드와 룬 해금 목록을 현재 세이브 기준으로 다시 표시한다.</summary>
        public void Refresh()
        {
            _view.SetBalance(GameData.L("ui.balance") + "  " + _session.Save.Currency + " " + GameData.L("ui.fragments"));
            for (int i = 0; i < UPGRADE_TYPES.Length && i < _view.CardCount; i++) RefreshCard(i, UPGRADE_TYPES[i]);
            RefreshRuneRows();
        }

        /// <summary>구매를 세션에 요청하고 성공하면 표시를 갱신한 뒤 구매 성공 후 동작을 실행한다.</summary>
        private void Purchase(string kind)
        {
            if (!_session.BuyUpgrade(kind)) return;
            Refresh();
            _onPurchased();
        }

        /// <summary>성장 카드 하나의 현재 값, 설명, 구매 비용과 구매 가능 색을 표시한다.</summary>
        private void RefreshCard(int index, string type)
        {
            string description = GameData.L("ui." + type + "Description");
            string value;
            if (type == "capacity") value = _session.Capacity.ToString();
            else if (type == "energy")
            {
                value = _session.MaxEnergy.ToString("0");
                description += "  " + GameData.L("ui.regen") + " " + _session.EnergyRegen.ToString("0.#") + "/s";
            }
            else value = _session.BattleDuration.ToString("0") + "s";
            int cost = _session.GetUpgradeCost(type);
            Color accent = cost >= 0 && _session.Save.Currency >= cost ? UiTheme.Cyan : UiTheme.Muted;
            string purchaseLabel = cost < 0 ? GameData.L("ui.maxed") : GameData.L("ui.upgrade") + "  ·  " + cost + " " + GameData.L("ui.fragments");
            _view.SetCard(index, GameData.L("ui." + type), value, description, purchaseLabel, accent);
        }

        /// <summary>시작 해금이 아닌 룬마다 이름·분류·RAM·비용(또는 완료) 행을 만들고 미해금 행에 구매 동작을 연결한다.</summary>
        private void RefreshRuneRows()
        {
            _view.ClearRuneRows();
            foreach (RuneDefinition rune in GameData.Runes.All)
            {
                if (rune.UnlockType == "start") continue;
                string runeId = rune.Id;
                bool isUnlocked = Contains(_session.Save.UnlockedRunes, runeId);
                string cost = rune.UnlockType == "reward" ? GameData.L("ui.reward") : _session.GetUpgradeCost(runeId) + " " + GameData.L("ui.fragments");
                string label = rune.Name + "  /  " + GameData.L("category." + rune.Category) + "  /  " + rune.Ram + " RAM  ·  "
                    + (isUnlocked ? GameData.L("ui.complete") : cost);
                Color accent = isUnlocked ? UiTheme.Muted : RuneMesh.CategoryColor(rune.Category);
                _view.AddRuneRow(label, accent, () => { if (!isUnlocked) Purchase(runeId); });
            }
        }

        /// <summary>특정 값이 읽기 전용 문자열 목록에 포함되어 있는지 반환한다.</summary>
        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            foreach (string entry in values)
            {
                if (entry == value) return true;
            }
            return false;
        }
    }
}
