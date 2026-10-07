using System;
using UnityEngine;

[Serializable]
public struct WorkshopStatBlock
{
    [SerializeField] private float _intelligence;
    [SerializeField] private float _strength;
    [SerializeField] private float _moveSpeed;
    [SerializeField] private float _durability;
    [SerializeField] private float _supportWeight;
    [SerializeField] private float _cargoBonus;

    public float Intelligence => _intelligence;
    public float Strength => _strength;
    public float MoveSpeed => _moveSpeed;
    public float Durability => _durability;
    public float SupportWeight => _supportWeight;
    public float CargoBonus => _cargoBonus;

    /// <summary>Returns the sum of two part stat blocks.</summary>
    public static WorkshopStatBlock operator +(WorkshopStatBlock left, WorkshopStatBlock right)
    {
        return new WorkshopStatBlock
        {
            _intelligence = left._intelligence + right._intelligence,
            _strength = left._strength + right._strength,
            _moveSpeed = left._moveSpeed + right._moveSpeed,
            _durability = left._durability + right._durability,
            _supportWeight = left._supportWeight + right._supportWeight,
            _cargoBonus = left._cargoBonus + right._cargoBonus
        };
    }

    /// <summary>Scales every stat by a grade multiplier.</summary>
    public WorkshopStatBlock Scaled(float multiplier)
    {
        return new WorkshopStatBlock
        {
            _intelligence = _intelligence * multiplier,
            _strength = _strength * multiplier,
            _moveSpeed = _moveSpeed * multiplier,
            _durability = _durability * multiplier,
            _supportWeight = _supportWeight * multiplier,
            _cargoBonus = _cargoBonus * multiplier
        };
    }
}

[Serializable]
public sealed class ResourceEfficiencyEntry
{
    [SerializeField] private ResourceKind _resourceKind;
    [SerializeField] private float _efficiency;

    public ResourceKind ResourceKind => _resourceKind;
    public float Efficiency => _efficiency;

    /// <summary>Creates an efficiency entry for one resource type.</summary>
    public ResourceEfficiencyEntry(ResourceKind resourceKind, float efficiency)
    {
        _resourceKind = resourceKind;
        _efficiency = efficiency;
    }
}

[Serializable]
public sealed class WorkshopGradeEntry
{
    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [SerializeField] private float _statMultiplier = 1f;
    [SerializeField] private float _drawWeight = 1f;
    [SerializeField] private Color _displayColor = Color.white;

    public string Id => _id;
    public string DisplayName => _displayName;
    public float StatMultiplier => _statMultiplier;
    public float DrawWeight => _drawWeight;
    public Color DisplayColor => _displayColor;

    /// <summary>Creates a grade row used by both stat calculation and weighted draws.</summary>
    public WorkshopGradeEntry(string id, string displayName, float statMultiplier, float drawWeight, Color displayColor)
    {
        _id = id;
        _displayName = displayName;
        _statMultiplier = statMultiplier;
        _drawWeight = drawWeight;
        _displayColor = displayColor;
    }
}

[Serializable]
public sealed class GachaSlotCost
{
    [SerializeField] private RobotPartSlot _slot;
    [SerializeField] private int _cost;

    public RobotPartSlot Slot => _slot;
    public int Cost => _cost;

    /// <summary>Creates the material cost for drawing one part slot.</summary>
    public GachaSlotCost(RobotPartSlot slot, int cost)
    {
        _slot = slot;
        _cost = cost;
    }
}

[Serializable]
public sealed class BonusSpawnEntry
{
    [SerializeField] private ResourceDefinition _resource;
    [SerializeField] private float _weight = 1f;
    [SerializeField] private int _minimumQuantity = 20;
    [SerializeField] private int _maximumQuantity = 30;

    public ResourceDefinition Resource => _resource;
    public float Weight => _weight;
    public int MinimumQuantity => _minimumQuantity;
    public int MaximumQuantity => _maximumQuantity;

    /// <summary>Creates a weighted bonus-debris resource entry.</summary>
    public BonusSpawnEntry(ResourceDefinition resource, float weight, int minimumQuantity, int maximumQuantity)
    {
        _resource = resource;
        _weight = weight;
        _minimumQuantity = minimumQuantity;
        _maximumQuantity = maximumQuantity;
    }
}

public enum ResourceKind
{
    Iron,
    Copper,
    Electronics
}

public enum RobotPartSlot
{
    Core,
    Head,
    Legs,
    Arms
}

public enum RobotWorkPhase
{
    Selecting,
    MovingToTarget,
    Gathering,
    Returning,
    Resting,
    WaitingForStorage,
    AssemblyWait,
    Idle
}
