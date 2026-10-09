using System;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 강화 트리 탭 Presenter다. 세이브의 노드 레벨·스테이지·선행 조건으로 노드와 연결선 상태, 정보 패널 문구를 계산해 표시하고
    /// 노드 구매를 세션에 요청한다. 구매에 성공하면 진행 변경 동작(도크·벤치·출격·헤더 갱신)을 실행한다.
    /// </summary>
    public sealed class UpgradeTreePresenter
    {
        private const int MAX_TREE_STAGE = 10;

        private readonly RuneCodeSession _session;
        private readonly UpgradeTreePanel _view;
        private readonly UpgradeTreeDefinition _definition;
        private readonly Action _onPurchased;
        private string _hoveredNodeId;

        /// <summary>세션·트리 View와 구매 성공 후 동작을 받아 트리를 배치하고 노드 이벤트를 연결한다.</summary>
        public UpgradeTreePresenter(RuneCodeSession session, UpgradeTreePanel view, Action onPurchased)
        {
            _session = session;
            _view = view;
            _definition = view.Definition;
            _onPurchased = onPurchased;
            _view.Build();
            _view.NodeClicked += Purchase;
            _view.NodeHovered += OnNodeHovered;
        }

        /// <summary>보유 재화·다음 스테이지 요약, 노드 상태, 연결선과 열린 정보 패널을 최신 세이브 기준으로 갱신한다.</summary>
        public void Refresh()
        {
            int nextStage = Mathf.Min(_session.HighestClearedStage + 1, MAX_TREE_STAGE);
            _view.SetSummary(GameData.L("ui.tree.summary") + "  ·  " + _session.Save.Currency + " " + GameData.L("ui.fragments")
                + "  ·  " + GameData.L("ui.tree.availableStage") + " " + nextStage + "  ·  " + GameData.L("ui.tree.controls"));
            foreach (UpgradeTreeNodeDefinition node in _definition.Nodes)
            {
                if (node != null) RefreshNode(node);
            }
            _view.SetConnectionStates((nodeId, requiredLevel) => _session.GetUpgradeNodeLevel(nodeId) >= requiredLevel);
            if (!string.IsNullOrEmpty(_hoveredNodeId)) RefreshTooltip(_hoveredNodeId);
        }

        /// <summary>노드의 저장 레벨과 구매 가능 사유로 열림·완료·구매 가능 상태를 계산해 표시한다.</summary>
        private void RefreshNode(UpgradeTreeNodeDefinition node)
        {
            int level = _session.GetUpgradeNodeLevel(node.Id);
            bool isComplete = level >= node.MaxLevel;
            bool canPurchase = _session.CanPurchaseUpgradeNode(node.Id, out string reasonKey);
            bool isAvailable = isComplete || (reasonKey != "tree.stageLocked" && reasonKey != "tree.prerequisiteLocked"
                && reasonKey != "tree.invalidNode" && reasonKey != "tree.invalidRune");
            string runeCategory = null;
            if (node.EffectType == UpgradeEffectType.RuneUnlock && GameData.Runes.TryGet(node.RuneId, out RuneDefinition rune)) runeCategory = rune.Category;
            _view.SetNodeState(node.Id, node.EffectType, runeCategory, isAvailable, isComplete, canPurchase);
        }

        /// <summary>포인터가 노드에 들어오면 정보 패널을 표시하고, 표시 중인 노드에서 나가면 닫는다.</summary>
        private void OnNodeHovered(string nodeId, bool isHovered)
        {
            if (isHovered)
            {
                _hoveredNodeId = nodeId;
                RefreshTooltip(nodeId);
                return;
            }
            if (_hoveredNodeId != nodeId) return;
            _hoveredNodeId = null;
            _view.HideTooltip();
        }

        /// <summary>노드 이름·설명·다음 효과량, 해금 스테이지·레벨과 구매 비용 또는 잠금 사유를 정보 패널에 표시한다.</summary>
        private void RefreshTooltip(string nodeId)
        {
            UpgradeTreeNodeDefinition node = _definition.FindNode(nodeId);
            if (node == null) return;
            int level = _session.GetUpgradeNodeLevel(nodeId);
            bool isComplete = level >= node.MaxLevel;
            string title = GameData.L(node.TitleKey);
            string description = GameData.L(node.DescriptionKey);
            if (node.EffectType == UpgradeEffectType.RuneUnlock && GameData.Runes.TryGet(node.RuneId, out RuneDefinition rune))
            {
                title += " · " + rune.Name;
                description += "  ·  " + GameData.L("category." + rune.Category) + "  ·  " + rune.Ram + " RAM";
            }
            else if (!isComplete && level < node.Levels.Count)
            {
                description += "  ·  +" + node.Levels[level].Amount.ToString("0.##") + EffectUnit(node.EffectType);
            }
            _view.ShowTooltip(title + "   |   " + LevelText(node, level, isComplete), description);
        }

        /// <summary>해금 스테이지·현재 레벨과 완료·잠금 사유·구매 비용 중 해당하는 문구를 반환한다.</summary>
        private string LevelText(UpgradeTreeNodeDefinition node, int level, bool isComplete)
        {
            string text = GameData.L("ui.tree.stage") + " " + node.RequiredStage + "  ·  " + level + "/" + node.MaxLevel;
            _session.CanPurchaseUpgradeNode(node.Id, out string reasonKey);
            int cost = _session.GetUpgradeNodeCost(node.Id);
            if (isComplete) return text + "  ·  " + GameData.L("ui.complete");
            if (reasonKey == "tree.stageLocked" || reasonKey == "tree.prerequisiteLocked") return text + "  ·  " + GameData.L(reasonKey);
            if (cost >= 0) return text + "  ·  " + cost + " " + GameData.L("ui.fragments");
            return text;
        }

        /// <summary>능력치 노드 효과의 단위를 반환한다(에너지 회복 /s, 최대 에너지 EN, 그 외 RAM).</summary>
        private static string EffectUnit(UpgradeEffectType effectType)
        {
            switch (effectType)
            {
                case UpgradeEffectType.EnergyRegen: return "/s";
                case UpgradeEffectType.MaxEnergy: return " EN";
                default: return " RAM";
            }
        }

        /// <summary>노드 구매를 요청하고 표시를 갱신한다. 성공하면 구매 성공 후 동작을 실행한다.</summary>
        private void Purchase(string nodeId)
        {
            bool isPurchased = _session.BuyUpgradeNode(nodeId);
            Refresh();
            if (isPurchased) _onPurchased();
        }
    }
}
