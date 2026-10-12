using System;
using System.Collections.Generic;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 강화 트리 탭 View다. 배치 프리팹의 노드·연결선을 데이터 ID에 연결하고, 노드 클릭·포인터 진입을 알리며
    /// 요약·노드 상태·연결선 상태·정보 패널을 표시한다. 상태 계산과 구매는 UpgradeTreePresenter가 한다.
    /// </summary>
    public sealed class UpgradeTreePanel : MonoBehaviour
    {
        private const float INITIAL_ZOOM = 0.88f;
        private const float CONTENT_MARGIN = 42;

        [Header("트리 데이터")]
        [SerializeField] private UpgradeTreeDefinition _definition;

        [Header("트리 화면")]
        [SerializeField] private TextMeshProUGUI _summary;
        [SerializeField] private TextMeshProUGUI _statsSummary;
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private RectTransform _content;
        [SerializeField] private RectTransform _hoverPanel;
        [SerializeField] private TextMeshProUGUI _hoverTitle;
        [SerializeField] private TextMeshProUGUI _hoverDescription;
        [SerializeField] private TextMeshProUGUI _hoverLevel;
        [SerializeField] private TextMeshProUGUI _hoverIncrease;
        [SerializeField] private TextMeshProUGUI _hoverCost;

        private readonly Dictionary<string, UpgradeTreeNodeView> _nodeViews = new Dictionary<string, UpgradeTreeNodeView>();
        private UpgradeTreeConnectionView[] _connections = Array.Empty<UpgradeTreeConnectionView>();
        private readonly Vector3[] _tooltipCorners = new Vector3[4];
        private string _tooltipNodeId;

        /// <summary>표시 중인 트리 자산이다.</summary>
        public UpgradeTreeDefinition Definition => _definition;

        /// <summary>노드를 눌렀을 때 노드 ID와 함께 알린다.</summary>
        public event Action<string> NodeClicked;

        /// <summary>포인터가 노드에 들어오거나(true) 나갈 때(false) 노드 ID와 함께 알린다.</summary>
        public event Action<string, bool> NodeHovered;

        /// <summary>프리팹에 배치한 노드를 SO의 ID에 연결하고 기존 위치를 유지해 화면과 연결선을 준비한다.</summary>
        public void Build()
        {
            _nodeViews.Clear();
            _hoverPanel.gameObject.SetActive(false);
            foreach (UpgradeTreeNodeView view in _content.GetComponentsInChildren<UpgradeTreeNodeView>(true)) BindNode(view);
            foreach (UpgradeTreeNodeDefinition node in _definition.Nodes)
                if (node != null && !_nodeViews.ContainsKey(node.Id))
                    Debug.LogError("[UpgradeTree] 배치 프리팹에 노드가 없습니다: " + node.Id, this);
            _connections = _content.GetComponentsInChildren<UpgradeTreeConnectionView>(true);
            ResetContent();
            foreach (UpgradeTreeConnectionView connection in _connections) connection.RefreshLayout();
        }

        /// <summary>상단 요약 문구를 표시한다.</summary>
        public void SetSummary(string text) => _summary.text = text;

        /// <summary>기본 능력치를 제외한 트리 강화 합계를 화면 좌측 상단의 고정 텍스트에 표시한다.</summary>
        public void SetStatsSummary(string text) => _statsSummary.text = text;

        /// <summary>노드 효과·룬 분류와 레벨 채움·다음 구매 비용·열림·완료·구매 가능 상태를 표시한다.</summary>
        public void SetNodeState(string nodeId, UpgradeEffectType effectType, string runeCategory, Sprite icon, int level, int maxLevel, int nextCost, bool isAvailable, bool isComplete, bool canPurchase)
        {
            if (_nodeViews.TryGetValue(nodeId, out UpgradeTreeNodeView view)) view.SetContent(effectType, runeCategory, icon, level, maxLevel, nextCost, isAvailable, isComplete, canPurchase);
        }

        /// <summary>노드 ID별 학습 여부를 받아 연결된 양쪽 노드를 모두 배웠을 때만 연결선을 강조한다.</summary>
        public void SetConnectionStates(Func<string, bool> isLearned)
        {
            foreach (UpgradeTreeConnectionView connection in _connections)
                connection.SetLearned(isLearned(connection.SourceNodeId) && isLearned(connection.TargetNodeId));
        }

        /// <summary>노드 위 정보 상자에 이름·설명·강화 레벨·다음 상승량·비용을 표시한다.</summary>
        public void ShowTooltip(string nodeId, string title, string description, string level, string increase, string cost)
        {
            _tooltipNodeId = nodeId;
            _hoverPanel.gameObject.SetActive(true);
            _hoverTitle.text = title;
            _hoverDescription.text = description;
            _hoverLevel.gameObject.SetActive(!string.IsNullOrEmpty(level));
            _hoverLevel.text = level;
            _hoverIncrease.text = increase;
            _hoverCost.text = cost;
            PositionTooltip();
        }

        /// <summary>정보 패널을 닫는다.</summary>
        public void HideTooltip()
        {
            _tooltipNodeId = null;
            _hoverPanel.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_hoverPanel.gameObject.activeSelf) PositionTooltip();
        }

        /// <summary>확대·이동 후에도 툴팁을 포인터가 놓인 노드의 위쪽에 유지한다.</summary>
        private void PositionTooltip()
        {
            if (!_nodeViews.TryGetValue(_tooltipNodeId, out UpgradeTreeNodeView view)) return;
            RectTransform nodeRect = (RectTransform)view.transform;
            RectTransform parent = (RectTransform)_hoverPanel.parent;
            nodeRect.GetWorldCorners(_tooltipCorners);
            Vector3 left = parent.InverseTransformPoint(_tooltipCorners[1]);
            Vector3 right = parent.InverseTransformPoint(_tooltipCorners[2]);
            float halfWidth = _hoverPanel.rect.width * 0.5f;
            float x = Mathf.Clamp((left.x + right.x) * 0.5f, halfWidth + 8, parent.rect.width - halfWidth - 8);
            _hoverPanel.anchoredPosition = new Vector2(x, Mathf.Max(left.y, right.y) + 8);
        }

        /// <summary>콘텐츠를 좌상단 기준·초기 배율로 되돌리고 모든 노드가 들어가는 크기로 맞춘다.</summary>
        private void ResetContent()
        {
            _content.anchorMin = _content.anchorMax = new Vector2(0, 1);
            _content.pivot = new Vector2(0, 1);
            UpgradeTreeNodeDefinition root = null;
            foreach (UpgradeTreeNodeDefinition node in _definition.Nodes)
                if (node != null && node.Prerequisites.Count == 0) { root = node; break; }
            float rootCenter = root != null && _nodeViews.TryGetValue(root.Id, out UpgradeTreeNodeView rootView)
                ? GetNodeBounds(rootView).center.x : 0;
            _content.anchoredPosition = new Vector2(_viewport.rect.width * 0.5f - rootCenter * INITIAL_ZOOM, 0);
            _content.localScale = Vector3.one * INITIAL_ZOOM;
            float rightEdge = 0;
            float bottomEdge = 0;
            foreach (UpgradeTreeNodeView view in _nodeViews.Values)
            {
                Rect bounds = GetNodeBounds(view);
                rightEdge = Mathf.Max(rightEdge, bounds.xMax);
                bottomEdge = Mathf.Max(bottomEdge, -bounds.yMin);
            }
            _content.sizeDelta = new Vector2(Mathf.Max(_viewport.rect.width, rightEdge + CONTENT_MARGIN),
                Mathf.Max(_viewport.rect.height, bottomEdge + CONTENT_MARGIN));
        }

        /// <summary>배치한 노드의 사각형을 이동·확대 전 콘텐츠 로컬 좌표로 반환한다.</summary>
        private Rect GetNodeBounds(UpgradeTreeNodeView view)
        {
            RectTransform rect = (RectTransform)view.transform;
            rect.GetWorldCorners(_tooltipCorners);
            Vector2 minimum = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maximum = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in _tooltipCorners)
            {
                Vector2 local = _content.InverseTransformPoint(corner);
                minimum = Vector2.Min(minimum, local);
                maximum = Vector2.Max(maximum, local);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        /// <summary>프리팹 노드 ID의 데이터 존재·중복을 확인하고 클릭·호버 이벤트를 연결한다. 노드 위치는 변경하지 않는다.</summary>
        private void BindNode(UpgradeTreeNodeView view)
        {
            string nodeId = view.NodeId;
            if (string.IsNullOrEmpty(nodeId) || _definition.FindNode(nodeId) == null || _nodeViews.ContainsKey(nodeId))
            {
                Debug.LogError("[UpgradeTree] 배치 노드 ID가 비어 있거나 SO에 없거나 중복됩니다: " + nodeId, view);
                return;
            }
            view.SetPurchaseAction(() => NodeClicked?.Invoke(nodeId));
            view.SetHoverAction(isHovered => NodeHovered?.Invoke(nodeId, isHovered));
            _nodeViews.Add(nodeId, view);
        }
    }
}
