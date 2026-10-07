using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PartDefinition", menuName = "Robot Workshop/Part Definition")]
public sealed class PartDefinition : ScriptableObject
{
    [Header("정의")]
    [SerializeField] private string _id;
    [SerializeField] private RobotPartSlot _slot;
    [SerializeField] private string _displayName;

    [Header("기초 능력치")]
    [SerializeField] private WorkshopStatBlock _baseStats;
    [SerializeField] private WorkshopStatBlock _coreLevelGrowth;
    [SerializeField] private List<ResourceEfficiencyEntry> _resourceEfficiencies = new List<ResourceEfficiencyEntry>();

    [Header("뽑기 및 해금")]
    [SerializeField] private float _drawWeight = 1f;
    [SerializeField] private bool _requiresExpansion;

    [Header("표시 이미지")]
    [SerializeField] private Sprite _icon;
    [SerializeField] private Sprite _leftSprite;
    [SerializeField] private Sprite _rightSprite;

    public string Id => _id;
    public RobotPartSlot Slot => _slot;
    public string DisplayName => _displayName;
    public WorkshopStatBlock BaseStats => _baseStats;
    public WorkshopStatBlock CoreLevelGrowth => _coreLevelGrowth;
    public IReadOnlyList<ResourceEfficiencyEntry> ResourceEfficiencies => _resourceEfficiencies;
    public float DrawWeight => _drawWeight;
    public bool RequiresExpansion => _requiresExpansion;
    public Sprite Icon => _icon;
    public Sprite LeftSprite => _leftSprite;
    public Sprite RightSprite => _rightSprite;

    /// <summary>Gets this arm definition's effective collection speed for a resource.</summary>
    public float GetEfficiency(ResourceKind resourceKind)
    {
        for (int i = 0; i < _resourceEfficiencies.Count; i++)
        {
            if (_resourceEfficiencies[i].ResourceKind == resourceKind)
            {
                return _resourceEfficiencies[i].Efficiency;
            }
        }

        return 0f;
    }
}
