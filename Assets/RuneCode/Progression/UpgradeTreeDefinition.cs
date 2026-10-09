using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    public enum UpgradeEffectType
    {
        EnergyRegen,
        MaxEnergy,
        RamCapacity,
        RuneUnlock
    }

    [Serializable]
    public sealed class UpgradeTreeLevel
    {
        [SerializeField] private int _cost;
        [SerializeField] private float _amount;

        public int Cost => _cost;
        public float Amount => _amount;
    }

    [Serializable]
    public sealed class UpgradeTreePrerequisite
    {
        [SerializeField] private string _nodeId;
        [SerializeField] private int _requiredLevel = 1;

        public string NodeId => _nodeId;
        public int RequiredLevel => _requiredLevel;
    }

    [Serializable]
    public sealed class UpgradeTreeNodeDefinition
    {
        [Header("식별 및 표시")]
        [SerializeField] private string _id;
        [SerializeField] private int _requiredStage = 1;
        [SerializeField] private string _titleKey;
        [SerializeField] private string _descriptionKey;
        [SerializeField] private Vector2 _position;

        [Header("강화 효과")]
        [SerializeField] private UpgradeEffectType _effectType;
        [SerializeField] private string _runeId;
        [SerializeField] private List<UpgradeTreePrerequisite> _prerequisites = new List<UpgradeTreePrerequisite>();
        [SerializeField] private List<UpgradeTreeLevel> _levels = new List<UpgradeTreeLevel>();

        public string Id => _id;
        public int RequiredStage => _requiredStage;
        public string TitleKey => _titleKey;
        public string DescriptionKey => _descriptionKey;
        public Vector2 Position => _position;
        public UpgradeEffectType EffectType => _effectType;
        public string RuneId => _runeId;
        public IReadOnlyList<UpgradeTreePrerequisite> Prerequisites => _prerequisites;
        public IReadOnlyList<UpgradeTreeLevel> Levels => _levels;
        public int MaxLevel => _levels == null ? 0 : _levels.Count;

        /// <summary>다음 레벨에 필요한 재화량을 반환하고 더 강화할 수 없으면 -1을 반환한다.</summary>
        public int GetNextCost(int currentLevel)
        {
            return _levels != null && currentLevel >= 0 && currentLevel < _levels.Count
                ? _levels[currentLevel].Cost
                : -1;
        }

        /// <summary>현재 레벨까지 누적되는 효과량을 반환한다.</summary>
        public float GetTotalAmount(int currentLevel)
        {
            if (_levels == null) return 0;
            float total = 0;
            for (int index = 0; index < Mathf.Min(currentLevel, _levels.Count); index++)
                total += _levels[index].Amount;
            return total;
        }
    }

    [CreateAssetMenu(fileName = "UpgradeTree", menuName = "Rune Code/Upgrade Tree")]
    public sealed class UpgradeTreeDefinition : ScriptableObject
    {
        [SerializeField, HideInInspector] private int _layoutVersion = 1;
        [SerializeField] private List<UpgradeTreeNodeDefinition> _nodes = new List<UpgradeTreeNodeDefinition>();

        public int LayoutVersion => _layoutVersion;
        public IReadOnlyList<UpgradeTreeNodeDefinition> Nodes => _nodes;

        /// <summary>ID가 일치하는 강화 노드를 반환하고 찾지 못하면 null을 반환한다.</summary>
        public UpgradeTreeNodeDefinition FindNode(string nodeId)
        {
            if (_nodes == null) return null;
            foreach (UpgradeTreeNodeDefinition node in _nodes)
                if (node != null && node.Id == nodeId) return node;
            return null;
        }

        /// <summary>룬 ID를 해금하는 트리 노드를 반환하고 등록되지 않았으면 null을 반환한다.</summary>
        public UpgradeTreeNodeDefinition FindRuneUnlock(string runeId)
        {
            if (_nodes == null) return null;
            foreach (UpgradeTreeNodeDefinition node in _nodes)
                if (node != null && node.EffectType == UpgradeEffectType.RuneUnlock && node.RuneId == runeId) return node;
            return null;
        }

        /// <summary>노드 ID, 스테이지, 비용, 효과, 룬과 선행 참조를 검사하고 문제가 있으면 설명을 반환한다.</summary>
        public bool Validate(out string error)
        {
            error = null;
            if (_nodes == null || _nodes.Count == 0)
            {
                error = "업그레이드 트리에 노드가 없습니다.";
                return false;
            }
            HashSet<string> nodeIds = new HashSet<string>();
            HashSet<string> runeIds = new HashSet<string>();
            foreach (UpgradeTreeNodeDefinition node in _nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || !nodeIds.Add(node.Id) || node.RequiredStage < 1 ||
                    string.IsNullOrWhiteSpace(node.TitleKey) || string.IsNullOrWhiteSpace(node.DescriptionKey) ||
                    node.Position.x < 0 || node.Position.y < 0 || node.Levels == null || node.Levels.Count == 0 ||
                    node.Prerequisites == null || !Enum.IsDefined(typeof(UpgradeEffectType), node.EffectType))
                {
                    error = "업그레이드 노드 식별·위치·레벨 데이터가 유효하지 않습니다.";
                    return false;
                }
                foreach (UpgradeTreeLevel level in node.Levels)
                    if (level == null || level.Cost < 0 || level.Amount < 0 || float.IsNaN(level.Amount) || float.IsInfinity(level.Amount))
                    {
                        error = "업그레이드 노드의 비용 또는 효과량이 유효하지 않습니다: " + node.Id;
                        return false;
                    }
                if (node.EffectType == UpgradeEffectType.RamCapacity)
                    foreach (UpgradeTreeLevel level in node.Levels)
                        if (Mathf.Abs(level.Amount - Mathf.Round(level.Amount)) > 0.001f)
                        {
                            error = "RAM 용량 강화량은 정수여야 합니다: " + node.Id;
                            return false;
                        }
                if (node.EffectType == UpgradeEffectType.RuneUnlock &&
                    (node.Levels.Count != 1 || !GameData.Runes.TryGet(node.RuneId, out RuneDefinition rune) || rune.UnlockType == "start" || !runeIds.Add(node.RuneId)))
                {
                    error = "룬 해금 노드의 룬 참조가 없거나 중복되었습니다: " + node.Id;
                    return false;
                }
            }
            foreach (UpgradeTreeNodeDefinition node in _nodes)
                foreach (UpgradeTreePrerequisite prerequisite in node.Prerequisites)
                    if (prerequisite == null || FindNode(prerequisite.NodeId) == null || prerequisite.RequiredLevel < 1 ||
                        prerequisite.RequiredLevel > FindNode(prerequisite.NodeId).MaxLevel)
                    {
                        error = "업그레이드 노드의 선행 조건이 유효하지 않습니다: " + node.Id;
                        return false;
                    }
            HashSet<string> visiting = new HashSet<string>();
            HashSet<string> visited = new HashSet<string>();
            foreach (UpgradeTreeNodeDefinition node in _nodes)
                if (!VisitPrerequisites(node, visiting, visited))
                {
                    error = "업그레이드 노드 선행 조건에 순환이 있습니다: " + node.Id;
                    return false;
                }
            return true;
        }

        /// <summary>선행 노드 연결을 재귀 검사해 순환이 없으면 현재 노드 ID를 완료 집합에 추가한다.</summary>
        private bool VisitPrerequisites(UpgradeTreeNodeDefinition node, HashSet<string> visiting, HashSet<string> visited)
        {
            if (visited.Contains(node.Id)) return true;
            if (!visiting.Add(node.Id)) return false;
            foreach (UpgradeTreePrerequisite prerequisite in node.Prerequisites)
                if (!VisitPrerequisites(FindNode(prerequisite.NodeId), visiting, visited)) return false;
            visiting.Remove(node.Id);
            visited.Add(node.Id);
            return true;
        }
    }
}
