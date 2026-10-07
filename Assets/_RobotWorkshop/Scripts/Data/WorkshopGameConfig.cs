using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WorkshopGameConfig", menuName = "Robot Workshop/Game Config")]
public sealed class WorkshopGameConfig : ScriptableObject
{
    [Header("시작 개체와 장비")]
    [SerializeField] private PartDefinition _startingCore;
    [SerializeField] private PartDefinition _startingHead;
    [SerializeField] private PartDefinition _startingLegs;
    [SerializeField] private PartDefinition _startingMultiTool;
    [SerializeField] private PartDefinition _startingClaw;
    [SerializeField] private GradeTable _gradeTable;
    [SerializeField] private List<PartDefinition> _partDefinitions = new List<PartDefinition>();
    [SerializeField] private List<ResourceDefinition> _resourceDefinitions = new List<ResourceDefinition>();

    [Header("시작 경제와 적재")]
    [SerializeField] private int _startingScrap;
    [SerializeField] private float _baseCargoCapacity = 8f;
    [SerializeField] private float _initialStorageCapacity = 600f;
    [SerializeField] private ResourceKind _startingPrimaryResource = ResourceKind.Iron;

    [Header("작업 및 휴식")]
    [SerializeField] private float _baseOperatingSeconds = 60f;
    [SerializeField] private float _durabilitySecondsPerPoint = 6f;
    [SerializeField] private float _restSeconds = 30f;
    [SerializeField] private float _strengthSpeedFactor = 0.02f;
    [SerializeField] private float _minimumGatherSeconds = 0.25f;
    [SerializeField] private float _minimumMoveSpeed = 0.1f;
    [SerializeField] private float _midIntelligenceThreshold = 20f;
    [SerializeField] private float _highIntelligenceThreshold = 40f;

    [Header("코어 레벨 및 저장")]
    [SerializeField] private int _levelOneExperience = 20;
    [SerializeField] private int _experienceIncreasePerLevel = 10;
    [SerializeField] private float _autoSaveSeconds = 30f;
    [SerializeField] private string _firstRobotName = "UNIT-01";

    [Header("화면 이동")]
    [SerializeField] private float _cameraScrollDistance = 8f;

    public PartDefinition StartingCore => _startingCore;
    public PartDefinition StartingHead => _startingHead;
    public PartDefinition StartingLegs => _startingLegs;
    public PartDefinition StartingMultiTool => _startingMultiTool;
    public PartDefinition StartingClaw => _startingClaw;
    public GradeTable GradeTable => _gradeTable;
    public IReadOnlyList<PartDefinition> PartDefinitions => _partDefinitions;
    public IReadOnlyList<ResourceDefinition> ResourceDefinitions => _resourceDefinitions;
    public int StartingScrap => _startingScrap;
    public float BaseCargoCapacity => _baseCargoCapacity;
    public float InitialStorageCapacity => _initialStorageCapacity;
    public ResourceKind StartingPrimaryResource => _startingPrimaryResource;
    public float BaseOperatingSeconds => _baseOperatingSeconds;
    public float DurabilitySecondsPerPoint => _durabilitySecondsPerPoint;
    public float RestSeconds => _restSeconds;
    public float StrengthSpeedFactor => _strengthSpeedFactor;
    public float MinimumGatherSeconds => _minimumGatherSeconds;
    public float MinimumMoveSpeed => _minimumMoveSpeed;
    public float MidIntelligenceThreshold => _midIntelligenceThreshold;
    public float HighIntelligenceThreshold => _highIntelligenceThreshold;
    public int LevelOneExperience => _levelOneExperience;
    public int ExperienceIncreasePerLevel => _experienceIncreasePerLevel;
    public float AutoSaveSeconds => _autoSaveSeconds;
    public string FirstRobotName => _firstRobotName;
    public float CameraScrollDistance => _cameraScrollDistance;
}
