using System;
using System.Collections.Generic;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 강화 트리 탭 View다. 트리 자산의 위치·선행 조건대로 노드 아이콘과 연결선을 만들고, 노드 클릭·포인터 진입을 알리며
    /// 요약·노드 상태·연결선 상태·정보 패널을 표시한다. 상태 계산과 구매는 UpgradeTreePresenter가 한다.
    /// </summary>
    public sealed class UpgradeTreePanel : MonoBehaviour
    {
        private const float NODE_SIZE = 76;
        private const float INITIAL_ZOOM = 0.88f;
        private const float CONTENT_MARGIN = 42;
        private const float LINE_WIDTH = 2.5f;

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

        /// <summary>표시 중인 트리 자산이다.</summary>
        public UpgradeTreeDefinition Definition => _definition;

        /// <summary>노드를 눌렀을 때 노드 ID와 함께 알린다.</summary>
        public event Action<string> NodeClicked;

        /// <summary>포인터가 노드에 들어오거나(true) 나갈 때(false) 노드 ID와 함께 알린다.</summary>
        public event Action<string, bool> NodeHovered;

        /// <summary>트리 자산으로 연결선과 노드 아이콘을 배치하고 정보 패널을 닫아 둔다. Presenter가 한 번만 호출한다.</summary>
        public void Build()
        {
            UiFactory.ClearChildren(_content);
            _nodeViews.Clear();
            _connections.Clear();
            _hoverPanel.gameObject.SetActive(false);
            ResetContent();
            foreach (UpgradeTreeNodeDefinition node in _definition.Nodes)
            {
                if (node?.Prerequisites == null) continue;
                foreach (UpgradeTreePrerequisite prerequisite in node.Prerequisites)
                {
                    UpgradeTreeNodeDefinition source = _definition.FindNode(prerequisite.NodeId);
                    if (source != null) CreateConnection(source, node, prerequisite);
                }
            }
            foreach (UpgradeTreeNodeDefinition node in _definition.Nodes)
            {
                if (node != null && !string.IsNullOrWhiteSpace(node.Id)) CreateNode(node);
            }
        }

        /// <summary>상단 요약 문구를 표시한다.</summary>
        public void SetSummary(string text) => _summary.text = text;

        /// <summary>노드 아이콘의 효과 종류·룬 분류와 열림·완료·구매 가능 상태를 표시한다.</summary>
        public void SetNodeState(string nodeId, UpgradeEffectType effectType, string runeCategory, bool isAvailable, bool isComplete, bool canPurchase)
        {
            if (_nodeViews.TryGetValue(nodeId, out UpgradeTreeNodeView view)) view.SetContent(effectType, runeCategory, isAvailable, isComplete, canPurchase);
        }

        /// <summary>각 연결선을 선행 노드 조건 충족 여부(노드 ID, 요구 레벨 → 충족)에 따라 완료 색 또는 잠금 색으로 표시한다.</summary>
        public void SetConnectionStates(Func<string, int, bool> isSatisfied)
        {
            foreach (TreeConnection connection in _connections)
                connection.Image.color = isSatisfied(connection.NodeId, connection.RequiredLevel) ? UiTheme.Cyan : UiTheme.Muted;
        }

        /// <summary>정보 패널에 제목·설명을 표시한다.</summary>
        public void ShowTooltip(string title, string description)
        {
            _hoverPanel.gameObject.SetActive(true);
            _hoverTitle.text = title;
            _hoverDescription.text = description;
        }

        /// <summary>정보 패널을 닫는다.</summary>
        public void HideTooltip() => _hoverPanel.gameObject.SetActive(false);

        /// <summary>콘텐츠를 좌상단 기준·초기 배율로 되돌리고 모든 노드가 들어가는 크기로 맞춘다.</summary>
        private void ResetContent()
        {
            _content.anchorMin = _content.anchorMax = new Vector2(0, 1);
            _content.pivot = new Vector2(0, 1);
            _content.anchoredPosition = Vector2.zero;
            _content.localScale = Vector3.one * INITIAL_ZOOM;
            float rightEdge = 0;
            float bottomEdge = 0;
            foreach (UpgradeTreeNodeDefinition node in _definition.Nodes)
            {
                if (node == null) continue;
                rightEdge = Mathf.Max(rightEdge, node.Position.x + NODE_SIZE);
                bottomEdge = Mathf.Max(bottomEdge, node.Position.y + NODE_SIZE);
            }
            _content.sizeDelta = new Vector2(Mathf.Max(_viewport.rect.width, rightEdge + CONTENT_MARGIN),
                Mathf.Max(_viewport.rect.height, bottomEdge + CONTENT_MARGIN));
        }

        /// <summary>노드 아이콘 Prefab을 자산 위치에 배치하고 클릭·포인터 진입을 이벤트로 연결한다.</summary>
        private void CreateNode(UpgradeTreeNodeDefinition node)
        {
            UpgradeTreeNodeView view = Instantiate(_nodePrefab, _content, false);
            view.name = node.Id;
            RectTransform rect = (RectTransform)view.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(node.Position.x, -node.Position.y);
            rect.sizeDelta = new Vector2(NODE_SIZE, NODE_SIZE);
            string nodeId = node.Id;
            view.SetPurchaseAction(() => NodeClicked?.Invoke(nodeId));
            view.SetHoverAction(isHovered => NodeHovered?.Invoke(nodeId, isHovered));
            _nodeViews.Add(node.Id, view);
        }

        /// <summary>두 노드 중심을 잇는 직선을 노드 뒤에 만들고 선행 노드 조건과 함께 저장한다.</summary>
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
            lineRect.sizeDelta = new Vector2(delta.magnitude, LINE_WIDTH);
            lineRect.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            Image image = lineObject.GetComponent<Image>();
            image.raycastTarget = false;
            _connections.Add(new TreeConnection(image, prerequisite.NodeId, prerequisite.RequiredLevel));
            lineObject.transform.SetAsFirstSibling();
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
