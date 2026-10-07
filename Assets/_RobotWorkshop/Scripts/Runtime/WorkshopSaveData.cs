using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WorkshopResourceAmount
{
    [SerializeField] private string _resourceId;
    [SerializeField] private int _quantity;

    public string ResourceId { get => _resourceId; set => _resourceId = value; }
    public int Quantity { get => _quantity; set => _quantity = value; }

    /// <summary>Creates a saved stack for one resource definition.</summary>
    public WorkshopResourceAmount(string resourceId, int quantity)
    {
        _resourceId = resourceId;
        _quantity = quantity;
    }
}

[Serializable]
public sealed class WorkshopPartInstance
{
    [SerializeField] private string _instanceId;
    [SerializeField] private string _definitionId;
    [SerializeField] private string _gradeId;

    public string InstanceId { get => _instanceId; set => _instanceId = value; }
    public string DefinitionId { get => _definitionId; set => _definitionId = value; }
    public string GradeId { get => _gradeId; set => _gradeId = value; }

    /// <summary>Creates a uniquely owned part instance from a definition and grade.</summary>
    public WorkshopPartInstance(string instanceId, string definitionId, string gradeId)
    {
        _instanceId = instanceId;
        _definitionId = definitionId;
        _gradeId = gradeId;
    }
}

[Serializable]
public sealed class WorkshopSupplyState
{
    [SerializeField] private string _sourceId;
    [SerializeField] private int _quantity;
    [SerializeField] private float _refillRemaining;

    public string SourceId { get => _sourceId; set => _sourceId = value; }
    public int Quantity { get => _quantity; set => _quantity = value; }
    public float RefillRemaining { get => _refillRemaining; set => _refillRemaining = value; }

    /// <summary>Creates saved stock and its remaining refill timer.</summary>
    public WorkshopSupplyState(string sourceId, int quantity, float refillRemaining)
    {
        _sourceId = sourceId;
        _quantity = quantity;
        _refillRemaining = refillRemaining;
    }
}

[Serializable]
public sealed class WorkshopBonusState
{
    [SerializeField] private string _instanceId;
    [SerializeField] private string _resourceId;
    [SerializeField] private int _quantity;
    [SerializeField] private Vector3 _position;

    public string InstanceId { get => _instanceId; set => _instanceId = value; }
    public string ResourceId { get => _resourceId; set => _resourceId = value; }
    public int Quantity { get => _quantity; set => _quantity = value; }
    public Vector3 Position { get => _position; set => _position = value; }

    /// <summary>Creates a saved bonus debris pile and its world location.</summary>
    public WorkshopBonusState(string instanceId, string resourceId, int quantity, Vector3 position)
    {
        _instanceId = instanceId;
        _resourceId = resourceId;
        _quantity = quantity;
        _position = position;
    }
}

[Serializable]
public sealed class WorkshopRobotState
{
    [SerializeField] private string _instanceId;
    [SerializeField] private string _coreDefinitionId;
    [SerializeField] private string _coreGradeId;
    [SerializeField] private string _name;
    [SerializeField] private int _level = 1;
    [SerializeField] private int _experience;
    [SerializeField] private bool _isFirstRobot;
    [SerializeField] private bool _isOperating;
    [SerializeField] private bool _wantsAssembly;
    [SerializeField] private string _headInstanceId;
    [SerializeField] private string _legsInstanceId;
    [SerializeField] private string _leftArmInstanceId;
    [SerializeField] private string _rightArmInstanceId;
    [SerializeField] private ResourceKind _primaryResource;
    [SerializeField] private ResourceKind _firstFallback;
    [SerializeField] private ResourceKind _secondFallback;
    [SerializeField] private RobotWorkPhase _phase;
    [SerializeField] private string _targetSourceId;
    [SerializeField] private string _targetBonusId;
    [SerializeField] private float _workRemaining;
    [SerializeField] private float _energyRemaining;
    [SerializeField] private float _restRemaining;
    [SerializeField] private Vector3 _position;
    [SerializeField] private List<WorkshopResourceAmount> _cargo = new List<WorkshopResourceAmount>();

    public string InstanceId { get => _instanceId; set => _instanceId = value; }
    public string CoreDefinitionId { get => _coreDefinitionId; set => _coreDefinitionId = value; }
    public string CoreGradeId { get => _coreGradeId; set => _coreGradeId = value; }
    public string Name { get => _name; set => _name = value; }
    public int Level { get => _level; set => _level = value; }
    public int Experience { get => _experience; set => _experience = value; }
    public bool IsFirstRobot { get => _isFirstRobot; set => _isFirstRobot = value; }
    public bool IsOperating { get => _isOperating; set => _isOperating = value; }
    public bool WantsAssembly { get => _wantsAssembly; set => _wantsAssembly = value; }
    public string HeadInstanceId { get => _headInstanceId; set => _headInstanceId = value; }
    public string LegsInstanceId { get => _legsInstanceId; set => _legsInstanceId = value; }
    public string LeftArmInstanceId { get => _leftArmInstanceId; set => _leftArmInstanceId = value; }
    public string RightArmInstanceId { get => _rightArmInstanceId; set => _rightArmInstanceId = value; }
    public ResourceKind PrimaryResource { get => _primaryResource; set => _primaryResource = value; }
    public ResourceKind FirstFallback { get => _firstFallback; set => _firstFallback = value; }
    public ResourceKind SecondFallback { get => _secondFallback; set => _secondFallback = value; }
    public RobotWorkPhase Phase { get => _phase; set => _phase = value; }
    public string TargetSourceId { get => _targetSourceId; set => _targetSourceId = value; }
    public string TargetBonusId { get => _targetBonusId; set => _targetBonusId = value; }
    public float WorkRemaining { get => _workRemaining; set => _workRemaining = value; }
    public float EnergyRemaining { get => _energyRemaining; set => _energyRemaining = value; }
    public float RestRemaining { get => _restRemaining; set => _restRemaining = value; }
    public Vector3 Position { get => _position; set => _position = value; }
    public List<WorkshopResourceAmount> Cargo => _cargo;

    /// <summary>Creates a core-owned robot record with independent equipment and work state.</summary>
    public WorkshopRobotState(string instanceId, string coreDefinitionId, string coreGradeId, string name, bool isFirstRobot, Vector3 position)
    {
        _instanceId = instanceId;
        _coreDefinitionId = coreDefinitionId;
        _coreGradeId = coreGradeId;
        _name = name;
        _level = 1;
        _isFirstRobot = isFirstRobot;
        _phase = RobotWorkPhase.Selecting;
        _position = position;
        _primaryResource = ResourceKind.Iron;
        _firstFallback = ResourceKind.Copper;
        _secondFallback = ResourceKind.Electronics;
    }

    /// <summary>Restores an empty cargo collection if a saved record omitted it.</summary>
    public void EnsureCollections()
    {
        _cargo ??= new List<WorkshopResourceAmount>();
    }
}

[Serializable]
public sealed class WorkshopSaveData
{
    [SerializeField] private int _saveVersion = 1;
    [SerializeField] private int _recycledScrap;
    [SerializeField] private bool _storageExpanded;
    [SerializeField] private bool _districtExpanded;
    [SerializeField] private float _bonusSpawnRemaining;
    [SerializeField] private List<WorkshopRobotState> _robots = new List<WorkshopRobotState>();
    [SerializeField] private List<WorkshopPartInstance> _parts = new List<WorkshopPartInstance>();
    [SerializeField] private List<WorkshopResourceAmount> _storedResources = new List<WorkshopResourceAmount>();
    [SerializeField] private List<WorkshopSupplyState> _supplies = new List<WorkshopSupplyState>();
    [SerializeField] private List<WorkshopBonusState> _bonuses = new List<WorkshopBonusState>();

    public int SaveVersion { get => _saveVersion; set => _saveVersion = value; }
    public int RecycledScrap { get => _recycledScrap; set => _recycledScrap = value; }
    public bool StorageExpanded { get => _storageExpanded; set => _storageExpanded = value; }
    public bool DistrictExpanded { get => _districtExpanded; set => _districtExpanded = value; }
    public float BonusSpawnRemaining { get => _bonusSpawnRemaining; set => _bonusSpawnRemaining = value; }
    public List<WorkshopRobotState> Robots => _robots;
    public List<WorkshopPartInstance> Parts => _parts;
    public List<WorkshopResourceAmount> StoredResources => _storedResources;
    public List<WorkshopSupplyState> Supplies => _supplies;
    public List<WorkshopBonusState> Bonuses => _bonuses;

    /// <summary>Restores saved collections that are absent in older or incomplete JSON data.</summary>
    public void EnsureCollections()
    {
        _robots ??= new List<WorkshopRobotState>();
        _parts ??= new List<WorkshopPartInstance>();
        _storedResources ??= new List<WorkshopResourceAmount>();
        _supplies ??= new List<WorkshopSupplyState>();
        _bonuses ??= new List<WorkshopBonusState>();
    }
}
