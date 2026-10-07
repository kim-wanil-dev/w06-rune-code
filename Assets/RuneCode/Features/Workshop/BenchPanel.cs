using System.Collections.Generic;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 벤치 탭의 성장 카드 3장과 룬 해금 목록을 표시하고 구매를 세션에 요청한다.
    /// 구매 성공 시 도크를 다시 시작하고 벤치와 헤더를 갱신한다.
    /// </summary>
    public sealed class BenchPanel : MonoBehaviour
    {
        private static readonly string[] UPGRADE_TYPES = { "capacity", "energy", "duration" };

        [Header("성장 카드")]
        [SerializeField] private TextMeshProUGUI _balanceText;
        [SerializeField] private TextMeshProUGUI[] _cardValues;
        [SerializeField] private TextMeshProUGUI[] _cardDescriptions;
        [SerializeField] private Button[] _cardButtons;
        [SerializeField] private TextMeshProUGUI[] _cardLabels;

        [Header("룬 해금")]
        [SerializeField] private RectTransform _unlockList;
        [SerializeField] private UiRow _runeRowPrefab;

        private RuneCodeSession _session;
        private DockRun _dockRun;
        private WorkshopScreen _screen;

        /// <summary>세션·도크 구동·화면을 받아 성장 카드 구매 리스너를 등록하고 값을 채운다.</summary>
        public void Initialize(RuneCodeSession session, DockRun dockRun, WorkshopScreen screen)
        {
            _session = session;
            _dockRun = dockRun;
            _screen = screen;
            for (int i = 0; i < UPGRADE_TYPES.Length; i++)
            {
                string type = UPGRADE_TYPES[i];
                _cardButtons[i].onClick.AddListener(() => Purchase(type));
            }
            Refresh();
        }

        /// <summary>보유 조각, 성장 수치, 구매 비용과 룬 해금 목록을 현재 세이브 기준으로 다시 표시한다.</summary>
        public void Refresh()
        {
            _balanceText.text = GameData.L("ui.balance") + "  " + _session.Save.Currency + " " + GameData.L("ui.fragments");
            for (int i = 0; i < UPGRADE_TYPES.Length; i++) RefreshCard(i);
            RefreshRuneRows();
        }

        /// <summary>구매를 세션에 요청하고 성공 시 도크를 다시 시작해 벤치와 헤더를 갱신한다.</summary>
        private void Purchase(string kind)
        {
            if (!_session.BuyUpgrade(kind)) return;
            _dockRun.ResetDock();
            Refresh();
            _screen.RefreshHeader();
        }

        /// <summary>지정 성장 카드의 현재 값, 설명, 구매 비용과 구매 가능 색을 반영한다.</summary>
        private void RefreshCard(int index)
        {
            string type = UPGRADE_TYPES[index];
            string description = GameData.L("ui." + type + "Description");
            string value;
            if (type == "capacity") value = _session.Capacity.ToString();
            else if (type == "energy")
            {
                value = _session.MaxEnergy.ToString("0");
                description += "  " + GameData.L("ui.regen") + " " + _session.EnergyRegen.ToString("0.#") + "/s";
            }
            else value = _session.BattleDuration.ToString("0") + "s";
            _cardValues[index].text = value;
            _cardDescriptions[index].text = description;
            int cost = _session.GetUpgradeCost(type);
            Color accent = cost >= 0 && _session.Save.Currency >= cost ? UiTheme.Cyan : UiTheme.Muted;
            _cardLabels[index].text = cost < 0 ? GameData.L("ui.maxed") : GameData.L("ui.upgrade") + "  ·  " + cost + " " + GameData.L("ui.fragments");
            _cardLabels[index].color = accent;
            _cardButtons[index].image.color = new Color(accent.r * 0.19f + 0.03f, accent.g * 0.19f + 0.05f, accent.b * 0.19f + 0.07f);
        }

        /// <summary>해금 가능한 룬 행을 Prefab으로 다시 만들어 이름·분류·비용과 구매 동작을 연결한다.</summary>
        private void RefreshRuneRows()
        {
            UiFactory.ClearChildren(_unlockList);
            foreach (RuneDefinition rune in GameData.Runes.All)
            {
                if (rune.UnlockType == "start") continue;
                RuneDefinition current = rune;
                bool unlocked = Contains(_session.Save.UnlockedRunes, rune.Id);
                string cost = rune.UnlockType == "reward" ? GameData.L("ui.reward") : rune.UnlockCost + " " + GameData.L("ui.fragments");
                string label = rune.Name + "  /  " + GameData.L("category." + rune.Category) + "  /  " + rune.Ram + " RAM  ·  " + (unlocked ? GameData.L("ui.complete") : cost);
                UiRow row = Instantiate(_runeRowPrefab, _unlockList);
                row.Configure(label, unlocked ? UiTheme.Muted : RuneMesh.CategoryColor(rune.Category), 44,
                    () => { if (!unlocked) Purchase(current.Id); });
            }
        }

        /// <summary>특정 값이 읽기 전용 문자열 목록에 포함되어 있는지 반환한다.</summary>
        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            foreach (string entry in values) if (entry == value) return true;
            return false;
        }
    }
}
