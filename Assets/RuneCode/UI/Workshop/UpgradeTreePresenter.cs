using System;

namespace RuneCode
{
    /// <summary>세션의 강화 진행을 트리 노드와 호버 정보에 표시하고 노드 구매를 처리한다.</summary>
    public sealed class UpgradeTreePresenter
    {
        private readonly RuneCodeSession _session;
        private readonly UpgradeTreePanel _view;
        private readonly UpgradeTreeDefinition _definition;
        private readonly Action _onPurchased;
        private string _hoveredNodeId;

        /// <summary>세션과 트리 화면을 받아 노드를 생성하고 구매·호버 이벤트를 연결한다.</summary>
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

        /// <summary>현재 스크랩, 노드 레벨과 연결선, 열린 툴팁을 세션 상태로 갱신한다.</summary>
        public void Refresh()
        {
            _view.SetSummary(GameData.L("ui.tree.summary") + "  ·  " + _session.Save.Currency + " " + GameData.L("ui.fragments")
                + "  ·  " + GameData.L("ui.tree.controls"));
            foreach (UpgradeTreeNodeDefinition node in _definition.Nodes)
            {
                if (node != null) RefreshNode(node);
            }
            _view.SetConnectionStates((nodeId, requiredLevel) => _session.IsDebugEnabled || _session.GetUpgradeNodeLevel(nodeId) >= requiredLevel);
            if (!string.IsNullOrEmpty(_hoveredNodeId)) RefreshTooltip(_hoveredNodeId);
        }

        /// <summary>노드의 잠금·비용·레벨 상태를 계산해 아이콘에 적용한다.</summary>
        private void RefreshNode(UpgradeTreeNodeDefinition node)
        {
            int level = _session.GetUpgradeNodeLevel(node.Id);
            bool isComplete = level >= node.MaxLevel;
            bool canPurchase = _session.CanPurchaseUpgradeNode(node.Id, out string reasonKey);
            bool isAvailable = isComplete || (reasonKey != "tree.stageLocked" && reasonKey != "tree.prerequisiteLocked"
                && reasonKey != "tree.invalidNode" && reasonKey != "tree.invalidRune");
            string runeCategory = null;
            if (node.EffectType == UpgradeEffectType.RuneUnlock && GameData.Runes.TryGet(node.RuneId, out RuneDefinition rune))
                runeCategory = rune.Category;
            _view.SetNodeState(node.Id, node.EffectType, runeCategory, node.Icon, level, isAvailable, isComplete, canPurchase);
        }

        /// <summary>호버 중인 노드 ID를 저장하고 그 노드의 툴팁을 표시하거나 닫는다.</summary>
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

        /// <summary>호버 노드의 이름·설명·다음 상승량·비용과 강화 노드의 현재 레벨을 표시한다.</summary>
        private void RefreshTooltip(string nodeId)
        {
            UpgradeTreeNodeDefinition node = _definition.FindNode(nodeId);
            if (node == null) return;
            string title = GameData.L(node.TitleKey);
            if (node.EffectType == UpgradeEffectType.RuneUnlock && GameData.Runes.TryGet(node.RuneId, out RuneDefinition rune))
                title = rune.Name;
            int currentLevel = _session.GetUpgradeNodeLevel(nodeId);
            string level = node.EffectType == UpgradeEffectType.RuneUnlock ? null :
                GameData.L("ui.tree.level") + " " + currentLevel + "/" + node.MaxLevel;
            _view.ShowTooltip(nodeId, title, GameData.L(node.DescriptionKey), level,
                NextIncreaseText(node, currentLevel), CostText(node, currentLevel));
        }

        /// <summary>다음 구매로 증가하는 효과량을 노드 종류의 단위와 함께 반환한다.</summary>
        private static string NextIncreaseText(UpgradeTreeNodeDefinition node, int currentLevel)
        {
            if (currentLevel >= node.MaxLevel) return GameData.L("tree.maxed");
            if (node.EffectType == UpgradeEffectType.RuneUnlock) return GameData.L("ui.tree.unlockEffect");
            if (node.EffectType == UpgradeEffectType.CastSpeed) return GameData.L("ui.tree.pendingEffect");
            float amount = node.Levels[currentLevel].Amount;
            string unit;
            switch (node.EffectType)
            {
                case UpgradeEffectType.EnergyRegen: unit = "/s"; break;
                case UpgradeEffectType.MaxEnergy: unit = " 전력"; break;
                case UpgradeEffectType.RamCapacity: unit = " RAM"; break;
                case UpgradeEffectType.MaxHp: unit = " HP"; break;
                case UpgradeEffectType.Damage:
                case UpgradeEffectType.ScrapGain:
                case UpgradeEffectType.MoveSpeed:
                    amount *= 100f;
                    unit = "%";
                    break;
                default: unit = ""; break;
            }
            return GameData.L("ui.tree.nextIncrease") + " +" + amount.ToString("0.##") + unit;
        }

        /// <summary>다음 노드 구매의 실제 스크랩 비용을 반환하며 최대 레벨이면 완료 상태를 반환한다.</summary>
        private string CostText(UpgradeTreeNodeDefinition node, int currentLevel)
        {
            if (currentLevel >= node.MaxLevel) return GameData.L("tree.maxed");
            return GameData.L("ui.tree.cost") + " " + _session.GetUpgradeNodeCost(node.Id) + " " + GameData.L("ui.fragments");
        }

        /// <summary>노드 ID를 세션에 구매 요청하고 성공하면 작업실의 다른 표시도 갱신한다.</summary>
        private void Purchase(string nodeId)
        {
            bool isPurchased = _session.BuyUpgradeNode(nodeId);
            Refresh();
            if (isPurchased) _onPurchased();
        }
    }
}
