using System.Collections.Generic;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    public sealed class UpgradeTreePanel : MonoBehaviour
    {
        private const float NODE_SIZE = 76;
        private const float INITIAL_ZOOM = 0.88f;

        [Header("트리 데이터")]
        [SerializeField] private UpgradeTreeDefinition _definition;
        [SerializeField] private UpgradeTreeNodeView _nodePrefab;

        [Header("트리 화면")]
        [SerializeField] private TextMeshProUGUI _summary;
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private RectTransform _content;
        [SerializeField] private RectTransform _hoverPanel;
        [SerializeField] private TextMeshProUGUI _hoverTitle;
        [SerializeField] private TextMeshProUGUI _hoverDescription;

        private readonly Dictionary<string, UpgradeTreeNodeView> _nodeViews = new Dictionary<string, UpgradeTreeNodeView>();
        private readonly List<TreeConnection> _connections = new List<TreeConnection>();
        private RuneCodeSession _session;
        private WorkshopScreen _screen;
        private string _hoveredNodeId;

        /// <summary>세션과 트리 자산을 연결하고 교차 연결·아이콘 노드·포인터 설명을 구성한다.</summary>
        public void Initialize(RuneCodeSession session, WorkshopScreen screen)
        {
            _session = session;
            _screen = screen;
            _hoverPanel.gameObject.SetActive(false);
            BuildTree();
            Refresh();
        }

        /// <summary>보유 재화, 스테이지 잠금, 노드 레벨과 연결 상태를 최신 세이브 기준으로 갱신한다.</summary>
        public void Refresh()
        {
            if (_session == null) return;
            int nextStage = Mathf.Min(_session.HighestClearedStage + 1, 10);
            _summary.text = GameData.L("ui.tree.summary") + "  ·  " + _session.Save.Currency + " " + GameData.L("ui.fragments") +
                "  ·  " + GameData.L("ui.tree.availableStage") + " " + nextStage + "  ·  " + GameData.L("ui.tree.controls");
            foreach (UpgradeTreeNodeDefinition node in _definition.Nodes) RefreshNode(node);
            RefreshConnections();
            if (!string.IsNullOrEmpty(_hoveredNodeId)) RefreshTooltip(_hoveredNodeId);
        }

        /// <summary>자산의 노드 위치와 선행 조건을 따라 연결선을 그리고 공용 사각형 아이콘 Prefab을 배치한다.</summary>
        private void BuildTree()
        {
            UiFactory.ClearChildren(_content);
            _content.anchorMin = _content.anchorMax = new Vector2(0, 1);
            _content.pivot = new Vector2(0, 1);
            _content.anchoredPosition = Vector2.zero;
            _content.localScale = Vector3.one * INITIAL_ZOOM;
            _nodeViews.Clear();
            _connections.Clear();

            float rightEdge = 0;
            float bottomEdge = 0;
            foreach (UpgradeTreeNodeDefinition node in _definition.Nodes)
            {
                if (node == null) continue;
                rightEdge = Mathf.Max(rightEdge, node.Position.x + NODE_SIZE);
                bottomEdge = Mathf.Max(bottomEdge, node.Position.y + NODE_SIZE);
            }
            _content.sizeDelta = new Vector2(Mathf.Max(_viewport.rect.width, rightEdge + 42),
                Mathf.Max(_viewport.rect.height, bottomEdge + 42));

            foreach (UpgradeTreeNodeDefinition node in _definition.Nodes)
            {
                if (node == null || node.Prerequisites == null) continue;
                foreach (UpgradeTreePrerequisite prerequisite in node.Prerequisites)
                {
                    UpgradeTreeNodeDefinition source = _definition.FindNode(prerequisite.NodeId);
                    if (source != null) CreateConnection(source, node, prerequisite);
                }
            }
            foreach (UpgradeTreeNodeDefinition node in _definition.Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id)) continue;
                UpgradeTreeNodeView view = Instantiate(_nodePrefab, _content, false);
                view.name = node.Id;
                RectTransform rect = (RectTransform)view.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(node.Position.x, -node.Position.y);
                rect.sizeDelta = new Vector2(NODE_SIZE, NODE_SIZE);
                string nodeId = node.Id;
                view.SetPurchaseAction(() => Purchase(nodeId));
                view.SetHoverAction(isHovered => ShowTooltip(nodeId, isHovered));
                _nodeViews.Add(node.Id, view);
            }
        }

        /// <summary>두 사각형 노드 중심을 직선으로 연결하고 선행 노드 상태를 저장한다.</summary>
        private void CreateConnection(UpgradeTreeNodeDefinition source, UpgradeTreeNodeDefinition target, UpgradeTreePrerequisite prerequisite)
        {
            Vector2 start = new Vector2(source.Position.x + NODE_SIZE * 0.5f, -source.Position.y - NODE_SIZE * 0.5f);
            Vector2 end = new Vector2(target.Position.x + NODE_SIZE * 0.5f, -target.Position.y - NODE_SIZE * 0.5f);
            Vector2 delta = end - start;
            GameObject lineObject = new GameObject(source.Id + "To" + target.Id, typeof(RectTransform), typeof(Image));
            lineObject.transform.SetParent(_content, false);
            RectTransform lineRect = lineObject.GetComponent<RectTransform>();
            lineRect.anchorMin = lineRect.anchorMax = new Vector2(0, 1);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.anchoredPosition = (start + end) * 0.5f;
            lineRect.sizeDelta = new Vector2(delta.magnitude, 2.5f);
            lineRect.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            Image image = lineObject.GetComponent<Image>();
            image.raycastTarget = false;
            _connections.Add(new TreeConnection(image, prerequisite.NodeId, prerequisite.RequiredLevel));
            lineObject.transform.SetAsFirstSibling();
        }

        /// <summary>저장 레벨과 스테이지·선행 조건을 바탕으로 노드 아이콘과 열림·구매 상태를 표시한다.</summary>
        private void RefreshNode(UpgradeTreeNodeDefinition node)
        {
            if (node == null || !_nodeViews.TryGetValue(node.Id, out UpgradeTreeNodeView view)) return;
            int level = _session.GetUpgradeNodeLevel(node.Id);
            bool isComplete = level >= node.MaxLevel;
            bool canPurchase = _session.CanPurchaseUpgradeNode(node.Id, out string reasonKey);
            bool isAvailable = isComplete || (reasonKey != "tree.stageLocked" && reasonKey != "tree.prerequisiteLocked" &&
                reasonKey != "tree.invalidNode" && reasonKey != "tree.invalidRune");
            string runeCategory = null;
            if (node.EffectType == UpgradeEffectType.RuneUnlock && GameData.Runes.TryGet(node.RuneId, out RuneDefinition rune))
                runeCategory = rune.Category;
            view.SetContent(node.EffectType, runeCategory, isAvailable, isComplete, canPurchase);
        }

        /// <summary>선행 노드 레벨에 따라 교차 연결선을 완료 색상 또는 잠금 색상으로 갱신한다.</summary>
        private void RefreshConnections()
        {
            foreach (TreeConnection connection in _connections)
            {
                bool isComplete = _session.GetUpgradeNodeLevel(connection.NodeId) >= connection.RequiredLevel;
                connection.Image.color = isComplete ? UiTheme.Cyan : UiTheme.Muted;
            }
        }

        /// <summary>포인터가 진입한 노드의 이름과 설명을 고정 정보 패널에 표시하거나 닫는다.</summary>
        private void ShowTooltip(string nodeId, bool isHovered)
        {
            if (!isHovered)
            {
                if (_hoveredNodeId != nodeId) return;
                _hoveredNodeId = null;
                _hoverPanel.gameObject.SetActive(false);
                return;
            }
            _hoveredNodeId = nodeId;
            _hoverPanel.gameObject.SetActive(true);
            RefreshTooltip(nodeId);
        }

        /// <summary>노드 설명, 효과 미리보기, 레벨, 구매 비용 또는 잠금 사유를 정보 패널에 채운다.</summary>
        private void RefreshTooltip(string nodeId)
        {
            UpgradeTreeNodeDefinition node = _definition.FindNode(nodeId);
            if (node == null) return;
            int level = _session.GetUpgradeNodeLevel(nodeId);
            int cost = _session.GetUpgradeNodeCost(nodeId);
            bool isComplete = level >= node.MaxLevel;
            _session.CanPurchaseUpgradeNode(nodeId, out string reasonKey);

            string title = GameData.L(node.TitleKey);
            string description = GameData.L(node.DescriptionKey);
            if (node.EffectType == UpgradeEffectType.RuneUnlock && GameData.Runes.TryGet(node.RuneId, out RuneDefinition rune))
            {
                title += " · " + rune.Name;
                description += "  ·  " + GameData.L("category." + rune.Category) + "  ·  " + rune.Ram + " RAM";
            }
            else if (!isComplete && level < node.Levels.Count)
            {
                UpgradeTreeLevel nextLevel = node.Levels[level];
                string unit = node.EffectType == UpgradeEffectType.EnergyRegen ? "/s" :
                    node.EffectType == UpgradeEffectType.MaxEnergy ? " EN" : " RAM";
                description += "  ·  +" + nextLevel.Amount.ToString("0.##") + unit;
            }

            string levelText = GameData.L("ui.tree.stage") + " " + node.RequiredStage + "  ·  " + level + "/" + node.MaxLevel;
            if (isComplete) levelText += "  ·  " + GameData.L("ui.complete");
            else if (reasonKey == "tree.stageLocked") levelText += "  ·  " + GameData.L(reasonKey);
            else if (reasonKey == "tree.prerequisiteLocked") levelText += "  ·  " + GameData.L(reasonKey);
            else if (cost >= 0) levelText += "  ·  " + cost + " " + GameData.L("ui.fragments");
            _hoverTitle.text = title + "   |   " + levelText;
            _hoverDescription.text = description;
        }

        /// <summary>노드 구매를 요청하고 성공하면 도크·기반 강화·헤더의 진행 정보를 갱신한다.</summary>
        private void Purchase(string nodeId)
        {
            if (!_session.BuyUpgradeNode(nodeId))
            {
                Refresh();
                return;
            }
            Refresh();
            _screen.RefreshProgression();
        }

        private sealed class TreeConnection
        {
            private readonly Image _image;
            private readonly string _nodeId;
            private readonly int _requiredLevel;

            public Image Image => _image;
            public string NodeId => _nodeId;
            public int RequiredLevel => _requiredLevel;

            /// <summary>연결선과 이 선이 표시하는 선행 노드 요구 레벨을 저장한다.</summary>
            public TreeConnection(Image image, string nodeId, int requiredLevel)
            {
                _image = image;
                _nodeId = nodeId;
                _requiredLevel = requiredLevel;
            }
        }
    }
}
