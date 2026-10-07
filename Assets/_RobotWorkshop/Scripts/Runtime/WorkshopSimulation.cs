using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class WorkshopSimulation
{
    private const int SAVE_VERSION = 1;

    private readonly WorkshopGameConfig _config;
    private readonly GachaConfig _gachaConfig;
    private readonly BonusSpawnConfig _bonusConfig;
    private readonly FacilityDefinition _storageFacility;
    private readonly FacilityDefinition _expansionFacility;
    private readonly Vector3 _servicePosition;
    private readonly System.Random _random = new System.Random();
    private readonly Dictionary<string, PartDefinition> _partDefinitions = new Dictionary<string, PartDefinition>();
    private readonly Dictionary<string, ResourceDefinition> _resourceDefinitions = new Dictionary<string, ResourceDefinition>();
    private readonly Dictionary<string, WorkshopResourceSource> _sourceObjects = new Dictionary<string, WorkshopResourceSource>();
    private readonly List<WorkshopResourceSource> _sourceList = new List<WorkshopResourceSource>();
    private WorkshopSaveData _state;
    private string _selectedRobotId;
    private float _saveRemaining;
    private string _statusMessage = "작업장에 오신 것을 환영합니다.";
    private bool _saveRequested;

    public event Action StateChanged;

    public WorkshopSaveData State => _state;
    public GradeTable GradeTable => _config.GradeTable;
    public IReadOnlyList<WorkshopResourceSource> Sources => _sourceList;
    public string StatusMessage => _statusMessage;
    public string SelectedRobotId => _selectedRobotId;
    public RobotPartSlot SelectedGachaSlot { get; private set; } = RobotPartSlot.Head;
    public float SimulationSpeed { get; set; } = 1f;
    public IReadOnlyList<WorkshopRobotState> Robots => _state.Robots;
    public IReadOnlyList<WorkshopPartInstance> Parts => _state.Parts;
    public IReadOnlyList<WorkshopBonusState> Bonuses => _state.Bonuses;

    /// <summary>Creates or loads the workshop state and caches project definitions and scene supply points.</summary>
    public WorkshopSimulation(
        WorkshopGameConfig config,
        GachaConfig gachaConfig,
        BonusSpawnConfig bonusConfig,
        FacilityDefinition storageFacility,
        FacilityDefinition expansionFacility,
        IReadOnlyList<WorkshopResourceSource> sources,
        Vector3 servicePosition)
    {
        _config = config;
        _gachaConfig = gachaConfig;
        _bonusConfig = bonusConfig;
        _storageFacility = storageFacility;
        _expansionFacility = expansionFacility;
        _servicePosition = servicePosition;
        CacheDefinitions();
        CacheSources(sources);
        LoadOrCreate();
        _selectedRobotId = _state.Robots.Count > 0 ? _state.Robots[0].InstanceId : string.Empty;
        ValidateDefinitions();
    }

    /// <summary>Advances world work on simulation time and autosaves on real elapsed time.</summary>
    public void Tick(float simulationSeconds, float realSeconds)
    {
        if (simulationSeconds > 0f)
        {
            RefillSupplies(simulationSeconds);
            AdvanceBonusTimer(simulationSeconds);
            for (int i = 0; i < _state.Robots.Count; i++)
            {
                AdvanceRobot(_state.Robots[i], simulationSeconds);
            }
        }

        _saveRemaining -= realSeconds;
        if (_saveRequested || _saveRemaining <= 0f)
        {
            Save();
            _saveRequested = false;
            _saveRemaining = Mathf.Max(1f, _config.AutoSaveSeconds);
        }

        StateChanged?.Invoke();
    }

    /// <summary>Returns the currently selected core-owned robot record.</summary>
    public WorkshopRobotState GetSelectedRobot()
    {
        return FindRobot(_selectedRobotId);
    }

    /// <summary>Selects a robot instance without changing its equipment, name, or progression.</summary>
    public void SelectRobot(string robotId)
    {
        if (FindRobot(robotId) == null)
        {
            return;
        }

        _selectedRobotId = robotId;
        SetMessage($"{GetSelectedRobot().Name} 선택됨");
    }

    /// <summary>Returns a part definition by its stable save identifier.</summary>
    public PartDefinition GetPartDefinition(string definitionId)
    {
        return _partDefinitions.TryGetValue(definitionId, out PartDefinition definition) ? definition : null;
    }

    /// <summary>Returns a resource definition by its stable save identifier.</summary>
    public ResourceDefinition GetResourceDefinition(string definitionId)
    {
        return _resourceDefinitions.TryGetValue(definitionId, out ResourceDefinition definition) ? definition : null;
    }

    /// <summary>Returns the configured resource definition for a gameplay resource kind.</summary>
    public ResourceDefinition GetResourceDefinitionByKind(ResourceKind kind)
    {
        return FindResource(kind);
    }

    /// <summary>Returns the highest grade-adjusted collection efficiency across both equipped arms.</summary>
    public float GetEffectiveArmEfficiency(WorkshopRobotState robot, ResourceKind kind)
    {
        return robot == null ? 0f : GetArmEfficiency(robot, kind);
    }

    /// <summary>Returns the part currently installed in a robot slot.</summary>
    public WorkshopPartInstance GetEquippedPart(WorkshopRobotState robot, RobotPartSlot slot, bool isLeftArm)
    {
        string instanceId = GetEquippedInstanceId(robot, slot, isLeftArm);
        return FindPart(instanceId);
    }

    /// <summary>Returns inventory candidates for a slot, excluding parts owned by another robot's loadout.</summary>
    public List<WorkshopPartInstance> GetAvailableParts(WorkshopRobotState robot, RobotPartSlot slot, bool isLeftArm)
    {
        List<WorkshopPartInstance> available = new List<WorkshopPartInstance>();
        for (int i = 0; i < _state.Parts.Count; i++)
        {
            WorkshopPartInstance part = _state.Parts[i];
            PartDefinition definition = GetPartDefinition(part.DefinitionId);
            if (definition == null || definition.Slot != slot ||
                IsEquippedByOtherRobot(part.InstanceId, robot) || IsEquippedInOtherSlot(part.InstanceId, robot, slot, isLeftArm))
            {
                continue;
            }

            available.Add(part);
        }

        return available;
    }

    /// <summary>Returns the selected robot's total ability stats after grade and core level growth.</summary>
    public WorkshopStatBlock GetStats(WorkshopRobotState robot)
    {
        WorkshopStatBlock stats = default;
        PartDefinition core = GetPartDefinition(robot.CoreDefinitionId);
        AddPartStats(ref stats, core, robot.CoreGradeId);
        AddPartStats(ref stats, GetPartDefinitionForInstance(robot.HeadInstanceId), GetGradeForInstance(robot.HeadInstanceId));
        AddPartStats(ref stats, GetPartDefinitionForInstance(robot.LegsInstanceId), GetGradeForInstance(robot.LegsInstanceId));
        AddPartStats(ref stats, GetPartDefinitionForInstance(robot.LeftArmInstanceId), GetGradeForInstance(robot.LeftArmInstanceId));
        AddPartStats(ref stats, GetPartDefinitionForInstance(robot.RightArmInstanceId), GetGradeForInstance(robot.RightArmInstanceId));

        if (core != null && robot.Level > 1)
        {
            stats += core.CoreLevelGrowth.Scaled(robot.Level - 1);
        }

        return stats;
    }

    /// <summary>Calculates the robot's integer cargo limit from support weight and installed cargo bonuses.</summary>
    public int GetCargoCapacity(WorkshopRobotState robot)
    {
        WorkshopStatBlock stats = GetStats(robot);
        return Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(_config.BaseCargoCapacity + stats.CargoBonus, stats.SupportWeight)));
    }

    /// <summary>Returns total weight currently carried by a robot.</summary>
    public float GetCargoWeight(WorkshopRobotState robot)
    {
        return GetResourceListWeight(robot.Cargo);
    }

    /// <summary>Returns current stored weight, configured storage capacity, and currency value.</summary>
    public void GetStorageSummary(out float weight, out float capacity, out int exchangeValue)
    {
        weight = GetResourceListWeight(_state.StoredResources);
        capacity = _config.InitialStorageCapacity + (_state.StorageExpanded ? _storageFacility.StorageCapacityIncrease : 0f);
        exchangeValue = CalculateScrapValue(_state.StoredResources);
    }

    /// <summary>Returns a short status reason for a resource based on expansion and the selected robot's arms.</summary>
    public string GetResourceAvailabilityReason(ResourceKind kind, WorkshopRobotState robot)
    {
        if (kind != ResourceKind.Iron && !_state.DistrictExpanded)
        {
            return "오른쪽 작업 구역 증축 필요";
        }

        ResourceDefinition resource = FindResource(kind);
        if (resource == null)
        {
            return "자원 정의 누락";
        }

        if (robot != null && GetArmEfficiency(robot, kind) <= 0f)
        {
            return "현재 장착 팔로 채집 불가";
        }

        return "지정 가능";
    }

    /// <summary>Changes the selected robot's main or fallback resource preference.</summary>
    public void SetResourcePreference(WorkshopRobotState robot, int preferenceIndex, ResourceKind kind)
    {
        if (robot == null || preferenceIndex < 0 || preferenceIndex > 2)
        {
            return;
        }

        if (preferenceIndex == 0)
        {
            robot.PrimaryResource = kind;
        }
        else if (preferenceIndex == 1)
        {
            robot.FirstFallback = kind;
        }
        else
        {
            robot.SecondFallback = kind;
        }

        SetMessage($"{robot.Name} 작업 자원 지정됨");
    }

    /// <summary>Returns the configured main or fallback resource preference.</summary>
    public ResourceKind GetResourcePreference(WorkshopRobotState robot, int preferenceIndex)
    {
        if (preferenceIndex == 1)
        {
            return robot.FirstFallback;
        }

        return preferenceIndex == 2 ? robot.SecondFallback : robot.PrimaryResource;
    }

    /// <summary>Returns whether a resource type is available in the currently open workshop area.</summary>
    public bool IsResourceUnlocked(ResourceKind kind)
    {
        return kind == ResourceKind.Iron || _state.DistrictExpanded;
    }

    /// <summary>Returns a localized label for a resource type.</summary>
    public string GetResourceName(ResourceKind kind)
    {
        ResourceDefinition definition = FindResource(kind);
        return definition == null ? kind.ToString() : definition.DisplayName;
    }

    /// <summary>Returns the selected gacha part slot and its configured price.</summary>
    public int GetGachaCost()
    {
        return _gachaConfig.GetCost(SelectedGachaSlot);
    }

    /// <summary>Cycles the selected gacha slot in its four-part order.</summary>
    public void CycleGachaSlot()
    {
        SelectedGachaSlot = (RobotPartSlot)(((int)SelectedGachaSlot + 1) % 4);
        SetMessage($"{GetSlotName(SelectedGachaSlot)} 뽑기 선택");
    }

    /// <summary>Draws one part, charging currency only after the grade and definition pools are valid.</summary>
    public bool TryDraw(out string result)
    {
        result = string.Empty;
        int cost = _gachaConfig.GetCost(SelectedGachaSlot);
        if (cost < 0)
        {
            result = "선택한 부위의 비용 설정이 없습니다.";
            SetMessage(result);
            return false;
        }

        if (_state.RecycledScrap < cost)
        {
            result = $"재화 부족 · 필요 {cost}";
            SetMessage(result);
            return false;
        }

        if (!TrySelectGrade(out WorkshopGradeEntry grade) || !TrySelectPartDefinition(SelectedGachaSlot, out PartDefinition definition))
        {
            result = "유효한 등급 또는 부품 뽑기 풀이 없습니다.";
            SetMessage(result);
            return false;
        }

        _state.RecycledScrap -= cost;
        if (SelectedGachaSlot == RobotPartSlot.Core)
        {
            WorkshopRobotState newRobot = new WorkshopRobotState(
                CreateId(), definition.Id, grade.Id, CreateAvailableRobotName(), false, _servicePosition)
            {
                Phase = RobotWorkPhase.Idle,
                IsOperating = false,
                EnergyRemaining = CalculateMaximumEnergy(definition, grade.Id, 1, null)
            };
            _state.Robots.Add(newRobot);
            _selectedRobotId = newRobot.InstanceId;
            result = $"새 코어 · {definition.DisplayName} · {grade.DisplayName}급 · {newRobot.Name}";
        }
        else
        {
            _state.Parts.Add(new WorkshopPartInstance(CreateId(), definition.Id, grade.Id));
            WorkshopRobotState selectedRobot = GetSelectedRobot();
            string comparison = GetPartComparison(selectedRobot, definition, grade);
            string function = GetPartFunctionSummary(definition, grade);
            result = $"{GetSlotName(definition.Slot)} · {definition.DisplayName} · {grade.DisplayName}급\n{function}\n{comparison}";
        }

        SetMessage(result);
        return true;
    }

    /// <summary>Creates one basic four-part kit for an unassembled core after checking all definitions and the price.</summary>
    public bool TryCraftStarterKit(string robotId, out string reason)
    {
        reason = string.Empty;
        WorkshopRobotState robot = FindRobot(robotId);
        if (robot == null || !CanEditRobot(robot, out reason))
        {
            return false;
        }

        if (_gachaConfig.StarterKitCost < 0 || _state.RecycledScrap < _gachaConfig.StarterKitCost)
        {
            reason = $"기본 파츠 세트 제작에 재화 {_gachaConfig.StarterKitCost} 필요";
            SetMessage(reason);
            return false;
        }

        PartDefinition[] definitions =
        {
            _gachaConfig.StarterHead,
            _gachaConfig.StarterLegs,
            _gachaConfig.StarterMultiTool,
            _gachaConfig.StarterClaw
        };
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i] == null || definitions[i].Id == robot.CoreDefinitionId || definitions[i].Slot == RobotPartSlot.Core)
            {
                reason = "기본 파츠 세트 정의가 완성되지 않았습니다.";
                SetMessage(reason);
                return false;
            }
        }

        _state.RecycledScrap -= _gachaConfig.StarterKitCost;
        for (int i = 0; i < definitions.Length; i++)
        {
            _state.Parts.Add(new WorkshopPartInstance(CreateId(), definitions[i].Id, GetStartingGradeId()));
        }

        reason = "F급 기본 파츠 4개를 인벤토리에 제작했습니다.";
        SetMessage(reason);
        return true;
    }

    /// <summary>Installs an unowned-by-others matching part while the robot is docked and carrying no resources.</summary>
    public bool TryEquip(string robotId, RobotPartSlot slot, bool isLeftArm, string partInstanceId, out string reason)
    {
        reason = string.Empty;
        WorkshopRobotState robot = FindRobot(robotId);
        WorkshopPartInstance part = FindPart(partInstanceId);
        PartDefinition definition = part == null ? null : GetPartDefinition(part.DefinitionId);
        if (robot == null || definition == null || definition.Slot != slot || !CanEditRobot(robot, out reason))
        {
            if (reason.Length == 0)
            {
                reason = "부품이 없거나 선택한 슬롯과 다릅니다.";
            }

            SetMessage(reason);
            return false;
        }

        if (slot == RobotPartSlot.Arms && GetEquippedInstanceId(robot, slot, !isLeftArm) == partInstanceId)
        {
            reason = "같은 팔 부품을 두 슬롯에 동시에 장착할 수 없습니다.";
            SetMessage(reason);
            return false;
        }

        if (IsEquippedByOtherRobot(partInstanceId, robot))
        {
            reason = "다른 개체가 장착 중인 부품입니다.";
            SetMessage(reason);
            return false;
        }

        if (slot == RobotPartSlot.Head)
        {
            robot.HeadInstanceId = partInstanceId;
        }
        else if (slot == RobotPartSlot.Legs)
        {
            robot.LegsInstanceId = partInstanceId;
        }
        else if (slot == RobotPartSlot.Arms && isLeftArm)
        {
            robot.LeftArmInstanceId = partInstanceId;
        }
        else if (slot == RobotPartSlot.Arms)
        {
            robot.RightArmInstanceId = partInstanceId;
        }

        CapRobotResources(robot);
        reason = $"{definition.DisplayName} 장착 완료";
        SetMessage(reason);
        return true;
    }

    /// <summary>Renames a robot while preserving its core identity and progression.</summary>
    public void RenameRobot(string robotId, string newName)
    {
        WorkshopRobotState robot = FindRobot(robotId);
        string safeName = string.IsNullOrWhiteSpace(newName) ? "UNIT" : newName.Trim();
        if (robot == null)
        {
            return;
        }

        robot.Name = safeName.Substring(0, Mathf.Min(20, safeName.Length));
        SetMessage($"개체 이름 변경 · {robot.Name}");
    }

    /// <summary>Starts or stops a robot, returning it to the service bay before it becomes editable.</summary>
    public bool TrySetOperating(string robotId, bool shouldOperate, out string reason)
    {
        reason = string.Empty;
        WorkshopRobotState robot = FindRobot(robotId);
        if (robot == null)
        {
            reason = "개체를 찾을 수 없습니다.";
            return false;
        }

        if (shouldOperate)
        {
            if (!CanOperate(robot, out reason))
            {
                SetMessage(reason);
                return false;
            }

            int maxActive = 1 + (_state.DistrictExpanded ? _expansionFacility.ActiveRobotIncrease : 0);
            if (!robot.IsOperating && CountOperatingRobots() >= maxActive)
            {
                reason = "가동 자리가 가득 찼습니다. 오른쪽 작업 구역을 증축하세요.";
                SetMessage(reason);
                return false;
            }

            robot.IsOperating = true;
            robot.WantsAssembly = false;
            if (robot.Phase == RobotWorkPhase.Idle || robot.Phase == RobotWorkPhase.AssemblyWait)
            {
                robot.Phase = RobotWorkPhase.Selecting;
            }

            reason = $"{robot.Name} 작업 재개";
        }
        else
        {
            robot.IsOperating = false;
            robot.WantsAssembly = true;
            if (robot.Phase != RobotWorkPhase.Resting && robot.Phase != RobotWorkPhase.WaitingForStorage)
            {
                robot.Phase = IsAtService(robot) && GetCargoWeight(robot) <= 0f
                    ? RobotWorkPhase.AssemblyWait
                    : RobotWorkPhase.Returning;
                robot.TargetSourceId = string.Empty;
                robot.TargetBonusId = string.Empty;
            }

            reason = $"{robot.Name} 호출 · 정비소로 복귀 중";
        }

        SetMessage(reason);
        return true;
    }

    /// <summary>Returns a robot to the service bay and pauses work for assembly.</summary>
    public void CallRobot(string robotId)
    {
        TrySetOperating(robotId, false, out _);
    }

    /// <summary>Returns whether the robot has its core, head, legs, and at least one resource-capable arm.</summary>
    public bool CanOperate(WorkshopRobotState robot, out string reason)
    {
        reason = string.Empty;
        if (robot == null)
        {
            reason = "코어를 선택하세요.";
            return false;
        }

        if (GetPartDefinition(robot.CoreDefinitionId) == null || GetPartDefinitionForInstance(robot.HeadInstanceId) == null || GetPartDefinitionForInstance(robot.LegsInstanceId) == null)
        {
            reason = "코어·머리·다리를 장착해야 합니다.";
            return false;
        }

        if (GetArmEfficiency(robot, robot.PrimaryResource) <= 0f)
        {
            bool hasAnyWorkArm = GetAnyArmEfficiency(robot) > 0f;
            if (!hasAnyWorkArm)
            {
                reason = "채집 가능한 팔을 하나 이상 장착해야 합니다.";
                return false;
            }
        }

        if (GetCargoCapacity(robot) < 1)
        {
            reason = "장착한 다리의 지지력보다 적재량이 낮습니다.";
            return false;
        }

        return true;
    }

    /// <summary>Renews a resource stack into recycled scrap and then allows robots to deposit later in a following transaction.</summary>
    public bool TryExchangeStoredResources(out string reason)
    {
        reason = string.Empty;
        if (_state.StoredResources.Count == 0)
        {
            reason = "환전할 자원이 없습니다.";
            SetMessage(reason);
            return false;
        }

        int value = CalculateScrapValue(_state.StoredResources);
        if (value <= 0)
        {
            reason = "환전 가능한 자원 가치가 0입니다.";
            SetMessage(reason);
            return false;
        }

        int updatedCurrency = checked(_state.RecycledScrap + value);
        _state.StoredResources.Clear();
        _state.RecycledScrap = updatedCurrency;
        reason = $"자원 환전 완료 · 재화 +{value}";
        SetMessage(reason);
        return true;
    }

    /// <summary>Purchases one facility after checking price, prior purchase state, and its configured effect.</summary>
    public bool TryBuyFacility(string facilityId, out string reason)
    {
        reason = string.Empty;
        FacilityDefinition facility = facilityId == _storageFacility.Id ? _storageFacility : facilityId == _expansionFacility.Id ? _expansionFacility : null;
        if (facility == null)
        {
            reason = "시설 정의를 찾을 수 없습니다.";
            SetMessage(reason);
            return false;
        }

        bool alreadyPurchased = facility.UnlocksDistrict ? _state.DistrictExpanded : _state.StorageExpanded;
        if (alreadyPurchased)
        {
            reason = $"{facility.DisplayName} 구매 완료";
            SetMessage(reason);
            return false;
        }

        if (facility.Cost < 0 || _state.RecycledScrap < facility.Cost)
        {
            reason = $"재화 부족 · {facility.DisplayName} 비용 {facility.Cost}";
            SetMessage(reason);
            return false;
        }

        _state.RecycledScrap -= facility.Cost;
        if (facility.UnlocksDistrict)
        {
            _state.DistrictExpanded = true;
            AddExpansionSupplyNodes();
        }
        else
        {
            _state.StorageExpanded = true;
        }

        reason = $"{facility.DisplayName} 구매 완료";
        SetMessage(reason);
        return true;
    }

    /// <summary>Creates a development-only resource pile in an open world position.</summary>
    public bool TrySpawnDevelopmentBonus(out string reason)
    {
        reason = string.Empty;
        if (!TrySelectBonusEntry(out BonusSpawnEntry entry))
        {
            reason = "보너스 자원 풀이 비어 있습니다.";
            SetMessage(reason);
            return false;
        }

        if (_state.Bonuses.Count >= _bonusConfig.MaximumWaitingBonuses)
        {
            reason = "보너스 잔해 대기 슬롯이 가득 찼습니다.";
            SetMessage(reason);
            return false;
        }

        SpawnBonus(entry);
        reason = "개발 보너스 잔해를 생성했습니다.";
        SetMessage(reason);
        return true;
    }

    /// <summary>Provides a development-only resource grant without changing production, experience, or draw results.</summary>
    public void GrantDevelopmentScrap(int amount)
    {
        _state.RecycledScrap = Math.Max(0, checked(_state.RecycledScrap + amount));
        SetMessage($"개발 지급 · 재화 {amount:+#;-#;0}");
    }

    /// <summary>Creates a selected part or core of a specified grade in the normal owned inventory.</summary>
    public bool GrantDevelopmentPart(string definitionId, string gradeId, out string reason)
    {
        reason = string.Empty;
        PartDefinition definition = GetPartDefinition(definitionId);
        WorkshopGradeEntry grade = _config.GradeTable.Find(gradeId);
        if (definition == null || grade == null)
        {
            reason = "개발 지급 부품 또는 등급을 찾을 수 없습니다.";
            SetMessage(reason);
            return false;
        }

        if (definition.Slot == RobotPartSlot.Core)
        {
            WorkshopRobotState robot = new WorkshopRobotState(CreateId(), definition.Id, grade.Id, CreateAvailableRobotName(), false, _servicePosition)
            {
                Phase = RobotWorkPhase.Idle,
                EnergyRemaining = CalculateMaximumEnergy(definition, grade.Id, 1, null)
            };
            _state.Robots.Add(robot);
            _selectedRobotId = robot.InstanceId;
        }
        else
        {
            _state.Parts.Add(new WorkshopPartInstance(CreateId(), definition.Id, grade.Id));
        }

        reason = $"개발 지급 · {definition.DisplayName} {grade.DisplayName}급";
        SetMessage(reason);
        return true;
    }

    /// <summary>Clears local progression and rebuilds the initial workshop state.</summary>
    public void ResetSave()
    {
        if (File.Exists(GetSavePath()))
        {
            File.Delete(GetSavePath());
        }

        _state = CreateNewState();
        _selectedRobotId = _state.Robots.Count > 0 ? _state.Robots[0].InstanceId : string.Empty;
        _saveRemaining = Mathf.Max(1f, _config.AutoSaveSeconds);
        SetMessage("저장 초기화 완료 · 새 작업장을 시작했습니다.");
    }

    /// <summary>Writes current individual robot, inventory, supply, facility, and economy state to the local JSON save.</summary>
    public void Save()
    {
        _state.SaveVersion = SAVE_VERSION;
        string path = GetSavePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(_state, true));
        _saveRequested = false;
    }

    /// <summary>Returns the local save path used by this workshop.</summary>
    public static string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, "RobotWorkshop", "save.json");
    }

    /// <summary>Returns localized display text for a robot work phase.</summary>
    public string GetPhaseLabel(WorkshopRobotState robot)
    {
        switch (robot.Phase)
        {
            case RobotWorkPhase.MovingToTarget: return "이동 중";
            case RobotWorkPhase.Gathering: return "채집 중";
            case RobotWorkPhase.Returning: return "정비소 복귀";
            case RobotWorkPhase.Resting: return "휴식 중";
            case RobotWorkPhase.WaitingForStorage: return "적재 공간 대기";
            case RobotWorkPhase.AssemblyWait: return "정비소 대기";
            case RobotWorkPhase.Idle: return "조립 대기";
            default: return robot.IsOperating ? "작업 대상 선택" : "정지";
        }
    }

    /// <summary>Returns current stored quantity for a resource definition.</summary>
    public int GetStoredQuantity(string resourceId)
    {
        return FindAmount(_state.StoredResources, resourceId)?.Quantity ?? 0;
    }

    /// <summary>Returns a facility's current purchase state.</summary>
    public bool IsFacilityPurchased(FacilityDefinition facility)
    {
        return facility.UnlocksDistrict ? _state.DistrictExpanded : _state.StorageExpanded;
    }

    /// <summary>Returns the set of unlocked resources in definition order.</summary>
    public List<ResourceDefinition> GetUnlockedResources()
    {
        List<ResourceDefinition> result = new List<ResourceDefinition>();
        for (int i = 0; i < _config.ResourceDefinitions.Count; i++)
        {
            ResourceDefinition resource = _config.ResourceDefinitions[i];
            if (IsResourceUnlocked(resource.Kind))
            {
                result.Add(resource);
            }
        }

        return result;
    }

    /// <summary>Changes gacha result selection without spending resources.</summary>
    public void SetGachaSlot(RobotPartSlot slot)
    {
        SelectedGachaSlot = slot;
        SetMessage($"{GetSlotName(slot)} 뽑기 선택");
    }

    /// <summary>Returns the selected slot's configured draw price.</summary>
    public int GetSlotCost(RobotPartSlot slot)
    {
        return _gachaConfig.GetCost(slot);
    }

    /// <summary>Returns a localized display name for a robot part slot.</summary>
    public static string GetSlotName(RobotPartSlot slot)
    {
        switch (slot)
        {
            case RobotPartSlot.Core: return "코어";
            case RobotPartSlot.Head: return "머리";
            case RobotPartSlot.Legs: return "다리";
            default: return "팔";
        }
    }

    /// <summary>Returns the selected robot's core grade label and its persistent identity mark.</summary>
    public string GetRobotIdentityLabel(WorkshopRobotState robot)
    {
        WorkshopGradeEntry grade = _config.GradeTable.Find(robot.CoreGradeId);
        return $"{(robot.IsFirstRobot ? "◆" : "◇")} {grade?.DisplayName ?? robot.CoreGradeId}급 코어 · Lv.{robot.Level}";
    }

    /// <summary>Returns XP progress and the amount needed for the next core level.</summary>
    public void GetExperienceProgress(WorkshopRobotState robot, out int current, out int required)
    {
        current = robot.Experience;
        required = _config.LevelOneExperience + _config.ExperienceIncreasePerLevel * Mathf.Max(0, robot.Level - 1);
    }

    /// <summary>Returns remaining and maximum continuous operation time for a robot.</summary>
    public void GetEnergyProgress(WorkshopRobotState robot, out float remaining, out float maximum)
    {
        remaining = robot.EnergyRemaining;
        maximum = CalculateMaximumEnergy(robot);
    }

    /// <summary>Returns the selected robot's core and parts as a concise inventory comparison.</summary>
    public string GetEquipmentLabel(WorkshopRobotState robot, RobotPartSlot slot, bool isLeftArm)
    {
        WorkshopPartInstance part = GetEquippedPart(robot, slot, isLeftArm);
        if (part == null)
        {
            return "미장착";
        }

        PartDefinition definition = GetPartDefinition(part.DefinitionId);
        WorkshopGradeEntry grade = _config.GradeTable.Find(part.GradeId);
        return definition == null ? "정의 누락" : $"{definition.DisplayName} · {grade?.DisplayName ?? part.GradeId}급";
    }

    /// <summary>Describes a part's grade-adjusted stats, arm work coverage, or core level growth.</summary>
    public string GetPartFunctionSummary(PartDefinition definition, WorkshopGradeEntry grade)
    {
        float multiplier = grade == null ? 1f : grade.StatMultiplier;
        WorkshopStatBlock stats = definition.BaseStats.Scaled(multiplier);
        if (definition.Slot == RobotPartSlot.Arms)
        {
            System.Text.StringBuilder armSummary = new System.Text.StringBuilder();
            for (int i = 0; i < definition.ResourceEfficiencies.Count; i++)
            {
                if (i > 0)
                {
                    armSummary.Append(" · ");
                }

                armSummary.Append(GetResourceName(definition.ResourceEfficiencies[i].ResourceKind))
                    .Append(" 효율 ")
                    .Append(definition.ResourceEfficiencies[i].Efficiency * multiplier)
                    .Append("배");
            }

            if (definition.ResourceEfficiencies.Count == 0)
            {
                armSummary.Append("채집 기능 없음 · 적재 보정 +").Append(stats.CargoBonus.ToString("0.##"));
            }

            return $"기능 · {armSummary} · 힘 +{stats.Strength:0.##}";
        }

        if (definition.Slot == RobotPartSlot.Core)
        {
            WorkshopStatBlock growth = definition.CoreLevelGrowth;
            return $"기능 · 코어 레벨 성장 · Lv당 지능 +{growth.Intelligence:0.##}, 힘 +{growth.Strength:0.##}, 이동 +{growth.MoveSpeed:0.##}, 내구도 +{growth.Durability:0.##}";
        }

        return $"능력 · 지능 +{stats.Intelligence:0.##} · 힘 +{stats.Strength:0.##} · 이동 +{stats.MoveSpeed:0.##} · 내구도 +{stats.Durability:0.##} · 지지력 +{stats.SupportWeight:0.##}";
    }

    /// <summary>Returns whether a given resource source or bonus is open for current operations.</summary>
    public bool IsSourceOpen(WorkshopResourceSource source)
    {
        return source != null && (!source.RequiresExpansion || _state.DistrictExpanded);
    }

    /// <summary>Returns the total visible quantity in fixed sources for one resource.</summary>
    public int GetAvailableSourceQuantity(ResourceKind kind)
    {
        int total = 0;
        for (int i = 0; i < _sourceList.Count; i++)
        {
            WorkshopResourceSource source = _sourceList[i];
            if (source.Resource != null && source.Resource.Kind == kind && IsSourceOpen(source))
            {
                total += FindSupply(source.SourceId)?.Quantity ?? source.InitialQuantity;
            }
        }

        return total;
    }

    /// <summary>Caches part and resource definitions by their stable identifiers.</summary>
    private void CacheDefinitions()
    {
        for (int i = 0; i < _config.PartDefinitions.Count; i++)
        {
            PartDefinition definition = _config.PartDefinitions[i];
            if (definition != null && !_partDefinitions.ContainsKey(definition.Id))
            {
                _partDefinitions.Add(definition.Id, definition);
            }
        }

        for (int i = 0; i < _config.ResourceDefinitions.Count; i++)
        {
            ResourceDefinition definition = _config.ResourceDefinitions[i];
            if (definition != null && !_resourceDefinitions.ContainsKey(definition.Id))
            {
                _resourceDefinitions.Add(definition.Id, definition);
            }
        }
    }

    /// <summary>Caches the supplied scene resource sources by identifier and resource kind.</summary>
    private void CacheSources(IReadOnlyList<WorkshopResourceSource> sources)
    {
        for (int i = 0; i < sources.Count; i++)
        {
            WorkshopResourceSource source = sources[i];
            if (source == null || string.IsNullOrWhiteSpace(source.SourceId) || _sourceObjects.ContainsKey(source.SourceId))
            {
                continue;
            }

            _sourceList.Add(source);
            _sourceObjects.Add(source.SourceId, source);
        }
    }

    /// <summary>Loads persisted workshop state, repairs it, or creates a new starting state.</summary>
    private void LoadOrCreate()
    {
        string path = GetSavePath();
        if (!File.Exists(path))
        {
            _state = CreateNewState();
            _saveRemaining = Mathf.Max(1f, _config.AutoSaveSeconds);
            return;
        }

        try
        {
            _state = JsonUtility.FromJson<WorkshopSaveData>(File.ReadAllText(path));
            if (_state == null || _state.SaveVersion != SAVE_VERSION)
            {
                Debug.LogWarning("Robot Workshop save version or data is invalid; starting a new local state.");
                _state = CreateNewState();
            }
            else
            {
                _state.EnsureCollections();
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"Robot Workshop save could not be loaded: {exception.Message}");
            _state = CreateNewState();
        }

        RepairLoadedState();
        _saveRemaining = Mathf.Max(1f, _config.AutoSaveSeconds);
    }

    /// <summary>Creates and installs the initial robot, parts, supplies, and progression state, then returns it.</summary>
    private WorkshopSaveData CreateNewState()
    {
        WorkshopSaveData state = new WorkshopSaveData
        {
            SaveVersion = SAVE_VERSION,
            RecycledScrap = Mathf.Max(0, _config.StartingScrap),
            BonusSpawnRemaining = RollBonusInterval()
        };

        for (int i = 0; i < _sourceList.Count; i++)
        {
            WorkshopResourceSource source = _sourceList[i];
            state.Supplies.Add(new WorkshopSupplyState(source.SourceId, source.InitialQuantity, source.RefillIntervalSeconds));
        }

        string startingGradeId = GetStartingGradeId();
        WorkshopRobotState robot = new WorkshopRobotState(
            CreateId(), _config.StartingCore.Id, startingGradeId, _config.FirstRobotName, true, _servicePosition)
        {
            PrimaryResource = _config.StartingPrimaryResource,
            IsOperating = true,
            Phase = RobotWorkPhase.Selecting
        };

        WorkshopPartInstance head = CreatePart(state, _config.StartingHead, startingGradeId);
        WorkshopPartInstance legs = CreatePart(state, _config.StartingLegs, startingGradeId);
        WorkshopPartInstance multiTool = CreatePart(state, _config.StartingMultiTool, startingGradeId);
        WorkshopPartInstance claw = CreatePart(state, _config.StartingClaw, startingGradeId);
        robot.HeadInstanceId = head.InstanceId;
        robot.LegsInstanceId = legs.InstanceId;
        robot.LeftArmInstanceId = multiTool.InstanceId;
        robot.RightArmInstanceId = claw.InstanceId;
        state.Robots.Add(robot);
        _state = state;
        robot.EnergyRemaining = CalculateMaximumEnergy(robot);
        return state;
    }

    /// <summary>Repairs missing collections, identifiers, equipment links, and saved robot values.</summary>
    private void RepairLoadedState()
    {
        _state.EnsureCollections();
        _state.RecycledScrap = Mathf.Max(0, _state.RecycledScrap);
        for (int i = 0; i < _sourceList.Count; i++)
        {
            WorkshopResourceSource source = _sourceList[i];
            WorkshopSupplyState savedSupply = FindSupply(source.SourceId);
            if (savedSupply == null)
            {
                _state.Supplies.Add(new WorkshopSupplyState(source.SourceId, source.InitialQuantity, source.RefillIntervalSeconds));
            }
            else
            {
                savedSupply.Quantity = Mathf.Clamp(savedSupply.Quantity, 0, source.MaximumQuantity);
                savedSupply.RefillRemaining = Mathf.Max(0f, savedSupply.RefillRemaining);
            }
        }

        for (int i = 0; i < _state.Robots.Count; i++)
        {
            WorkshopRobotState robot = _state.Robots[i];
            robot.EnsureCollections();
            robot.Level = Mathf.Max(1, robot.Level);
            robot.Experience = Mathf.Max(0, robot.Experience);
            CapRobotResources(robot);
            if (robot.Phase == RobotWorkPhase.Gathering)
            {
                robot.WorkRemaining = Mathf.Max(robot.WorkRemaining, _config.MinimumGatherSeconds);
            }
        }
    }

    /// <summary>Checks required game, gacha, facility, grade, part, and resource definitions.</summary>
    private void ValidateDefinitions()
    {
        HashSet<string> partIds = new HashSet<string>();
        for (int i = 0; i < _config.PartDefinitions.Count; i++)
        {
            PartDefinition part = _config.PartDefinitions[i];
            if (part == null || string.IsNullOrWhiteSpace(part.Id) || !partIds.Add(part.Id))
            {
                Debug.LogError("Robot Workshop has a missing or duplicate part definition ID.");
            }
        }

        HashSet<string> resourceIds = new HashSet<string>();
        for (int i = 0; i < _config.ResourceDefinitions.Count; i++)
        {
            ResourceDefinition resource = _config.ResourceDefinitions[i];
            if (resource == null || string.IsNullOrWhiteSpace(resource.Id) || !resourceIds.Add(resource.Id) || resource.UnitWeight <= 0f || resource.BaseWorkSeconds <= 0f)
            {
                Debug.LogError("Robot Workshop has an invalid or duplicate resource definition.");
            }
        }

        if (_config.StartingCore == null || _config.StartingHead == null || _config.StartingLegs == null || _config.StartingMultiTool == null || _config.StartingClaw == null)
        {
            Debug.LogError("Robot Workshop starting robot definitions are incomplete.");
        }
    }

    /// <summary>Restores source quantities over the elapsed simulation time.</summary>
    private void RefillSupplies(float elapsed)
    {
        for (int i = 0; i < _sourceList.Count; i++)
        {
            WorkshopResourceSource source = _sourceList[i];
            if (!IsSourceOpen(source) || source.RefillIntervalSeconds <= 0f)
            {
                continue;
            }

            WorkshopSupplyState supply = FindSupply(source.SourceId);
            if (supply == null || supply.Quantity >= source.MaximumQuantity)
            {
                continue;
            }

            supply.RefillRemaining -= elapsed;
            while (supply.RefillRemaining <= 0f && supply.Quantity < source.MaximumQuantity)
            {
                supply.Quantity = Mathf.Min(source.MaximumQuantity, supply.Quantity + Mathf.Max(1, source.RefillQuantity));
                supply.RefillRemaining += source.RefillIntervalSeconds;
            }
        }
    }

    /// <summary>Advances the bonus spawn timer and creates a configured bonus when due.</summary>
    private void AdvanceBonusTimer(float elapsed)
    {
        if (_state.Bonuses.Count >= _bonusConfig.MaximumWaitingBonuses)
        {
            return;
        }

        _state.BonusSpawnRemaining -= elapsed;
        if (_state.BonusSpawnRemaining > 0f)
        {
            return;
        }

        if (TrySelectBonusEntry(out BonusSpawnEntry entry))
        {
            SpawnBonus(entry);
            _state.BonusSpawnRemaining = RollBonusInterval();
        }
    }

    /// <summary>Advances one robot state by elapsed simulation time through work, travel, and rest phases.</summary>
    private void AdvanceRobot(WorkshopRobotState robot, float elapsed)
    {
        if (robot.Phase == RobotWorkPhase.Idle || robot.Phase == RobotWorkPhase.AssemblyWait)
        {
            return;
        }

        if (robot.Phase == RobotWorkPhase.Resting)
        {
            robot.RestRemaining -= elapsed;
            if (robot.RestRemaining <= 0f)
            {
                robot.EnergyRemaining = CalculateMaximumEnergy(robot);
                if (GetCargoWeight(robot) > 0f)
                {
                    robot.Phase = RobotWorkPhase.WaitingForStorage;
                    TryDepositRobotCargo(robot);
                }
                else if (robot.WantsAssembly)
                {
                    robot.Phase = RobotWorkPhase.AssemblyWait;
                }
                else
                {
                    robot.Phase = robot.IsOperating ? RobotWorkPhase.Selecting : RobotWorkPhase.Idle;
                }
            }

            return;
        }

        if (robot.Phase == RobotWorkPhase.WaitingForStorage)
        {
            TryDepositRobotCargo(robot);
            return;
        }

        if (!robot.IsOperating && robot.Phase != RobotWorkPhase.Returning)
        {
            robot.Phase = RobotWorkPhase.Returning;
            robot.TargetSourceId = string.Empty;
            robot.TargetBonusId = string.Empty;
        }

        if (robot.Phase == RobotWorkPhase.Selecting)
        {
            SelectNextTarget(robot);
        }

        if (robot.Phase == RobotWorkPhase.MovingToTarget)
        {
            MoveRobot(robot, elapsed);
        }

        if (robot.Phase == RobotWorkPhase.Gathering)
        {
            GatherAtTarget(robot, elapsed);
        }

        if (robot.Phase == RobotWorkPhase.Returning)
        {
            MoveRobotToService(robot, elapsed);
        }
    }

    /// <summary>Chooses the next available resource or bonus target from the robot preferences.</summary>
    private void SelectNextTarget(WorkshopRobotState robot)
    {
        if (!robot.IsOperating || !CanOperate(robot, out _))
        {
            robot.Phase = robot.WantsAssembly ? RobotWorkPhase.AssemblyWait : RobotWorkPhase.Idle;
            return;
        }

        if (GetCargoWeight(robot) + 0.001f >= GetCargoCapacity(robot))
        {
            robot.Phase = RobotWorkPhase.Returning;
            return;
        }

        WorkshopStatBlock stats = GetStats(robot);
        List<ResourceKind> preferences = GetResourcePreferenceOrder(robot);
        bool canPrioritizeBonus = stats.Intelligence >= _config.HighIntelligenceThreshold;
        if (canPrioritizeBonus && TryFindBonusForPreferences(robot, preferences, out WorkshopBonusState preferredBonus))
        {
            BeginMovingToBonus(robot, preferredBonus);
            return;
        }

        if (stats.Intelligence < _config.MidIntelligenceThreshold)
        {
            preferences.Clear();
            preferences.Add(robot.PrimaryResource);
        }

        for (int i = 0; i < preferences.Count; i++)
        {
            if (TryFindNearestTarget(robot, preferences[i], out WorkshopResourceSource source, out WorkshopBonusState bonus))
            {
                if (bonus != null)
                {
                    BeginMovingToBonus(robot, bonus);
                }
                else
                {
                    BeginMovingToSource(robot, source);
                }

                return;
            }
        }

        robot.TargetSourceId = string.Empty;
        robot.TargetBonusId = string.Empty;
        robot.Phase = RobotWorkPhase.Selecting;
    }

    /// <summary>Returns the robot resource preference order including intelligence-driven choices.</summary>
    private List<ResourceKind> GetResourcePreferenceOrder(WorkshopRobotState robot)
    {
        List<ResourceKind> preferences = new List<ResourceKind>(3) { robot.PrimaryResource };
        if (!preferences.Contains(robot.FirstFallback))
        {
            preferences.Add(robot.FirstFallback);
        }

        if (!preferences.Contains(robot.SecondFallback))
        {
            preferences.Add(robot.SecondFallback);
        }

        return preferences;
    }

    /// <summary>Finds the nearest available bonus matching preferences and robot arm efficiency.</summary>
    private bool TryFindBonusForPreferences(WorkshopRobotState robot, List<ResourceKind> preferences, out WorkshopBonusState result)
    {
        result = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < _state.Bonuses.Count; i++)
        {
            WorkshopBonusState bonus = _state.Bonuses[i];
            ResourceDefinition resource = GetResourceDefinition(bonus.ResourceId);
            if (resource == null || !preferences.Contains(resource.Kind) || !CanCollect(robot, resource.Kind))
            {
                continue;
            }

            float distance = Vector3.Distance(robot.Position, bonus.Position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                result = bonus;
            }
        }

        return result != null;
    }

    /// <summary>Returns the nearest usable bonus or supply source for the requested resource kind.</summary>
    private bool TryFindNearestTarget(WorkshopRobotState robot, ResourceKind kind, out WorkshopResourceSource foundSource, out WorkshopBonusState foundBonus)
    {
        foundSource = null;
        foundBonus = null;
        if (!CanCollect(robot, kind))
        {
            return false;
        }

        float bestDistance = float.MaxValue;
        for (int i = 0; i < _sourceList.Count; i++)
        {
            WorkshopResourceSource source = _sourceList[i];
            WorkshopSupplyState supply = FindSupply(source.SourceId);
            if (source.Resource == null || source.Resource.Kind != kind || !IsSourceOpen(source) || supply == null || supply.Quantity <= 0)
            {
                continue;
            }

            float distance = Vector3.Distance(robot.Position, source.transform.position);
            if (distance < bestDistance)
            {
                foundSource = source;
                foundBonus = null;
                bestDistance = distance;
            }
        }

        for (int i = 0; i < _state.Bonuses.Count; i++)
        {
            WorkshopBonusState bonus = _state.Bonuses[i];
            ResourceDefinition resource = GetResourceDefinition(bonus.ResourceId);
            if (resource == null || resource.Kind != kind || bonus.Quantity <= 0)
            {
                continue;
            }

            float distance = Vector3.Distance(robot.Position, bonus.Position);
            if (distance < bestDistance)
            {
                foundSource = null;
                foundBonus = bonus;
                bestDistance = distance;
            }
        }

        return foundSource != null || foundBonus != null;
    }

    /// <summary>Sets the robot target and travel phase for the selected resource source.</summary>
    private void BeginMovingToSource(WorkshopRobotState robot, WorkshopResourceSource source)
    {
        robot.TargetSourceId = source.SourceId;
        robot.TargetBonusId = string.Empty;
        robot.Phase = RobotWorkPhase.MovingToTarget;
    }

    /// <summary>Sets the robot target and travel phase for the selected bonus pile.</summary>
    private void BeginMovingToBonus(WorkshopRobotState robot, WorkshopBonusState bonus)
    {
        robot.TargetSourceId = string.Empty;
        robot.TargetBonusId = bonus.InstanceId;
        robot.Phase = RobotWorkPhase.MovingToTarget;
    }

    /// <summary>Moves the robot toward its current target using elapsed time and movement speed.</summary>
    private void MoveRobot(WorkshopRobotState robot, float elapsed)
    {
        Vector3 targetPosition = GetTargetPosition(robot);
        if (targetPosition == Vector3.positiveInfinity)
        {
            robot.Phase = RobotWorkPhase.Selecting;
            return;
        }

        float moveSeconds = Mathf.Min(elapsed, robot.EnergyRemaining);
        float speed = Mathf.Max(_config.MinimumMoveSpeed, GetStats(robot).MoveSpeed);
        robot.Position = Vector3.MoveTowards(robot.Position, targetPosition, speed * moveSeconds);
        robot.EnergyRemaining = Mathf.Max(0f, robot.EnergyRemaining - moveSeconds);
        if (Vector3.Distance(robot.Position, targetPosition) <= 0.025f)
        {
            robot.Position = targetPosition;
            if (HasTargetStock(robot) && GetCargoCapacity(robot) > GetCargoWeight(robot))
            {
                robot.WorkRemaining = CalculateWorkTime(robot);
                robot.Phase = RobotWorkPhase.Gathering;
            }
            else
            {
                robot.Phase = RobotWorkPhase.Selecting;
            }
        }

        if (robot.EnergyRemaining <= 0f && robot.Phase == RobotWorkPhase.MovingToTarget)
        {
            robot.TargetSourceId = string.Empty;
            robot.TargetBonusId = string.Empty;
            robot.Phase = RobotWorkPhase.Returning;
        }
    }

    /// <summary>Accumulates work time at the current source or bonus target.</summary>
    private void GatherAtTarget(WorkshopRobotState robot, float elapsed)
    {
        if (!robot.IsOperating || !HasTargetStock(robot))
        {
            robot.WorkRemaining = 0f;
            robot.Phase = robot.IsOperating ? RobotWorkPhase.Selecting : RobotWorkPhase.Returning;
            return;
        }

        if (GetCargoCapacity(robot) <= GetCargoWeight(robot))
        {
            robot.Phase = RobotWorkPhase.Returning;
            return;
        }

        float workSeconds = Mathf.Min(elapsed, robot.EnergyRemaining);
        robot.WorkRemaining -= workSeconds;
        robot.EnergyRemaining = Mathf.Max(0f, robot.EnergyRemaining - workSeconds);
        if (robot.WorkRemaining > 0f)
        {
            if (robot.EnergyRemaining <= 0f)
            {
                robot.WorkRemaining = 0f;
                robot.TargetSourceId = string.Empty;
                robot.TargetBonusId = string.Empty;
                robot.Phase = RobotWorkPhase.Returning;
            }

            return;
        }

        CommitGather(robot);
        if (!robot.IsOperating || robot.EnergyRemaining <= 0f || GetCargoCapacity(robot) <= GetCargoWeight(robot))
        {
            robot.TargetSourceId = string.Empty;
            robot.TargetBonusId = string.Empty;
            robot.Phase = robot.IsOperating || robot.WantsAssembly ? RobotWorkPhase.Returning : RobotWorkPhase.Idle;
        }
        else
        {
            robot.TargetSourceId = string.Empty;
            robot.TargetBonusId = string.Empty;
            robot.Phase = RobotWorkPhase.Selecting;
        }
    }

    /// <summary>Consumes target stock and adds capacity-limited resources and experience to the robot.</summary>
    private void CommitGather(WorkshopRobotState robot)
    {
        ResourceDefinition resource = GetCurrentTargetResource(robot);
        if (resource == null)
        {
            return;
        }

        int available = GetCurrentTargetQuantity(robot);
        float freeWeight = Mathf.Max(0f, GetCargoCapacity(robot) - GetCargoWeight(robot));
        int byWeight = Mathf.FloorToInt((freeWeight + 0.0001f) / resource.UnitWeight);
        int quantity = Mathf.Min(resource.QuantityPerGather, available, byWeight);
        if (quantity <= 0)
        {
            return;
        }

        if (robot.TargetSourceId.Length > 0)
        {
            WorkshopSupplyState supply = FindSupply(robot.TargetSourceId);
            if (supply == null || supply.Quantity < quantity)
            {
                return;
            }

            supply.Quantity -= quantity;
        }
        else
        {
            WorkshopBonusState bonus = FindBonus(robot.TargetBonusId);
            if (bonus == null || bonus.Quantity < quantity)
            {
                return;
            }

            bonus.Quantity -= quantity;
            if (bonus.Quantity <= 0)
            {
                _state.Bonuses.Remove(bonus);
            }
        }

        AddResourceAmount(robot.Cargo, resource.Id, quantity);
        AddExperience(robot, quantity * resource.ExperiencePerUnit);
    }

    /// <summary>Moves a loaded robot toward the service point before unloading its cargo.</summary>
    private void MoveRobotToService(WorkshopRobotState robot, float elapsed)
    {
        float speed = Mathf.Max(_config.MinimumMoveSpeed, GetStats(robot).MoveSpeed);
        robot.Position = Vector3.MoveTowards(robot.Position, _servicePosition, speed * elapsed);
        if (Vector3.Distance(robot.Position, _servicePosition) > 0.025f)
        {
            return;
        }

        robot.Position = _servicePosition;
        if (GetCargoWeight(robot) > 0f)
        {
            TryDepositRobotCargo(robot);
        }
        else if (robot.EnergyRemaining <= 0f)
        {
            BeginRest(robot);
        }
        else if (robot.WantsAssembly || !robot.IsOperating)
        {
            robot.Phase = RobotWorkPhase.AssemblyWait;
        }
        else
        {
            robot.Phase = RobotWorkPhase.Selecting;
        }
    }

    /// <summary>Transfers carried resources into storage up to remaining storage capacity.</summary>
    private void TryDepositRobotCargo(WorkshopRobotState robot)
    {
        GetStorageSummary(out float storageWeight, out float storageCapacity, out _);
        float freeWeight = Mathf.Max(0f, storageCapacity - storageWeight);
        for (int i = robot.Cargo.Count - 1; i >= 0; i--)
        {
            WorkshopResourceAmount cargo = robot.Cargo[i];
            ResourceDefinition resource = GetResourceDefinition(cargo.ResourceId);
            if (resource == null || cargo.Quantity <= 0 || freeWeight < resource.UnitWeight)
            {
                continue;
            }

            int quantity = Mathf.Min(cargo.Quantity, Mathf.FloorToInt((freeWeight + 0.0001f) / resource.UnitWeight));
            if (quantity <= 0)
            {
                continue;
            }

            cargo.Quantity -= quantity;
            freeWeight -= quantity * resource.UnitWeight;
            AddResourceAmount(_state.StoredResources, resource.Id, quantity);
            if (cargo.Quantity <= 0)
            {
                robot.Cargo.RemoveAt(i);
            }
        }

        if (robot.Cargo.Count > 0)
        {
            if (robot.EnergyRemaining <= 0f)
            {
                BeginRest(robot);
            }
            else
            {
                robot.Phase = RobotWorkPhase.WaitingForStorage;
            }

            return;
        }

        if (robot.EnergyRemaining <= 0f)
        {
            BeginRest(robot);
        }
        else if (robot.WantsAssembly || !robot.IsOperating)
        {
            robot.Phase = RobotWorkPhase.AssemblyWait;
        }
        else
        {
            robot.Phase = RobotWorkPhase.Selecting;
        }
    }

    /// <summary>Starts the robot recovery phase and clears its active work target.</summary>
    private void BeginRest(WorkshopRobotState robot)
    {
        robot.Phase = RobotWorkPhase.Resting;
        robot.RestRemaining = Mathf.Max(0f, _config.RestSeconds);
        robot.WorkRemaining = 0f;
        robot.TargetSourceId = string.Empty;
        robot.TargetBonusId = string.Empty;
    }

    /// <summary>Adds experience to a robot and applies any levels earned from the configured threshold.</summary>
    private void AddExperience(WorkshopRobotState robot, int amount)
    {
        robot.Experience = checked(robot.Experience + amount);
        while (robot.Experience >= _config.LevelOneExperience + _config.ExperienceIncreasePerLevel * (robot.Level - 1))
        {
            robot.Experience -= _config.LevelOneExperience + _config.ExperienceIncreasePerLevel * (robot.Level - 1);
            robot.Level++;
            CapRobotResources(robot);
            SetMessage($"{robot.Name} 레벨 상승 · Lv.{robot.Level}");
        }
    }

    /// <summary>Adds supply records for sources made available by the district expansion.</summary>
    private void AddExpansionSupplyNodes()
    {
        for (int i = 0; i < _sourceList.Count; i++)
        {
            WorkshopResourceSource source = _sourceList[i];
            if (source.RequiresExpansion && FindSupply(source.SourceId) == null)
            {
                _state.Supplies.Add(new WorkshopSupplyState(source.SourceId, source.InitialQuantity, source.RefillIntervalSeconds));
            }
        }
    }

    /// <summary>Selects a grade entry using configured gacha weights.</summary>
    private bool TrySelectGrade(out WorkshopGradeEntry selected)
    {
        return TryPickWeighted(_config.GradeTable.Grades, grade => grade.DrawWeight, out selected);
    }

    /// <summary>Selects a part definition for the supplied equipment slot using configured weights.</summary>
    private bool TrySelectPartDefinition(RobotPartSlot slot, out PartDefinition selected)
    {
        List<PartDefinition> eligible = new List<PartDefinition>();
        for (int i = 0; i < _config.PartDefinitions.Count; i++)
        {
            PartDefinition part = _config.PartDefinitions[i];
            if (part != null && part.Slot == slot && (!part.RequiresExpansion || _state.DistrictExpanded) && part.DrawWeight > 0f)
            {
                eligible.Add(part);
            }
        }

        return TryPickWeighted(eligible, part => part.DrawWeight, out selected);
    }

    /// <summary>Selects a bonus resource entry using the configured weighted pool.</summary>
    private bool TrySelectBonusEntry(out BonusSpawnEntry selected)
    {
        List<BonusSpawnEntry> eligible = new List<BonusSpawnEntry>();
        for (int i = 0; i < _bonusConfig.ResourcePool.Count; i++)
        {
            BonusSpawnEntry entry = _bonusConfig.ResourcePool[i];
            if (entry.Resource != null && (entry.Resource.Kind == ResourceKind.Iron || _state.DistrictExpanded) && entry.Weight > 0f)
            {
                eligible.Add(entry);
            }
        }

        return TryPickWeighted(eligible, entry => entry.Weight, out selected);
    }

    private bool TryPickWeighted<T>(IReadOnlyList<T> entries, Func<T, float> getWeight, out T selected)
    {
        selected = default;
        float total = 0f;
        T lastValid = default;
        for (int i = 0; i < entries.Count; i++)
        {
            float weight = getWeight(entries[i]);
            if (weight > 0f && !float.IsNaN(weight) && !float.IsInfinity(weight))
            {
                total += weight;
                lastValid = entries[i];
            }
        }

        if (total <= 0f)
        {
            return false;
        }

        float roll = (float)_random.NextDouble() * total;
        for (int i = 0; i < entries.Count; i++)
        {
            float weight = getWeight(entries[i]);
            if (weight <= 0f || float.IsNaN(weight) || float.IsInfinity(weight))
            {
                continue;
            }

            roll -= weight;
            if (roll <= 0f)
            {
                selected = entries[i];
                return true;
            }
        }

        selected = lastValid;
        return true;
    }

    /// <summary>Creates a bonus pile from its resource entry at a configured spawn position.</summary>
    private void SpawnBonus(BonusSpawnEntry entry)
    {
        float minX = Mathf.Min(_bonusConfig.MinimumWorldPosition.x, _bonusConfig.MaximumWorldPosition.x);
        float maxX = Mathf.Max(_bonusConfig.MinimumWorldPosition.x, _bonusConfig.MaximumWorldPosition.x);
        float minY = Mathf.Min(_bonusConfig.MinimumWorldPosition.y, _bonusConfig.MaximumWorldPosition.y);
        float maxY = Mathf.Max(_bonusConfig.MinimumWorldPosition.y, _bonusConfig.MaximumWorldPosition.y);
        Vector3 position = new Vector3(
            Mathf.Lerp(minX, maxX, (float)_random.NextDouble()),
            Mathf.Lerp(minY, maxY, (float)_random.NextDouble()),
            0f);
        int minQuantity = Mathf.Max(1, entry.MinimumQuantity);
        int maxQuantity = Mathf.Max(minQuantity, entry.MaximumQuantity);
        int quantity = _random.Next(minQuantity, maxQuantity + 1);
        _state.Bonuses.Add(new WorkshopBonusState(CreateId(), entry.Resource.Id, quantity, position));
    }

    /// <summary>Returns the randomized delay before the next bonus spawn.</summary>
    private float RollBonusInterval()
    {
        float minimum = Mathf.Max(1f, _bonusConfig.MinimumIntervalSeconds);
        float maximum = Mathf.Max(minimum, _bonusConfig.MaximumIntervalSeconds);
        return Mathf.Lerp(minimum, maximum, (float)_random.NextDouble());
    }

    /// <summary>Returns the robot work duration after applying its intelligence stat.</summary>
    private float CalculateWorkTime(WorkshopRobotState robot)
    {
        ResourceDefinition resource = GetCurrentTargetResource(robot);
        if (resource == null)
        {
            return _config.MinimumGatherSeconds;
        }

        float armEfficiency = GetArmEfficiency(robot, resource.Kind);
        float denominator = (1f + GetStats(robot).Strength * _config.StrengthSpeedFactor) * armEfficiency;
        float workSeconds = resource.BaseWorkSeconds / Mathf.Max(0.01f, denominator);
        if (robot.TargetBonusId.Length > 0)
        {
            workSeconds *= _bonusConfig.WorkTimeMultiplier;
        }

        return Mathf.Max(_config.MinimumGatherSeconds, workSeconds);
    }

    /// <summary>Returns the highest efficiency among a robot arms for the requested resource.</summary>
    private float GetArmEfficiency(WorkshopRobotState robot, ResourceKind kind)
    {
        return Mathf.Max(GetPartEfficiency(robot.LeftArmInstanceId, kind), GetPartEfficiency(robot.RightArmInstanceId, kind));
    }

    /// <summary>Returns the highest configured resource efficiency across both equipped arms.</summary>
    private float GetAnyArmEfficiency(WorkshopRobotState robot)
    {
        return Mathf.Max(
            GetPartEfficiency(robot.LeftArmInstanceId, ResourceKind.Iron),
            Mathf.Max(GetPartEfficiency(robot.LeftArmInstanceId, ResourceKind.Copper),
                Mathf.Max(GetPartEfficiency(robot.LeftArmInstanceId, ResourceKind.Electronics),
                    Mathf.Max(GetPartEfficiency(robot.RightArmInstanceId, ResourceKind.Iron),
                        Mathf.Max(GetPartEfficiency(robot.RightArmInstanceId, ResourceKind.Copper), GetPartEfficiency(robot.RightArmInstanceId, ResourceKind.Electronics))))));
    }

    /// <summary>Returns a part efficiency for the requested resource after grade scaling.</summary>
    private float GetPartEfficiency(string partInstanceId, ResourceKind kind)
    {
        WorkshopPartInstance part = FindPart(partInstanceId);
        PartDefinition definition = part == null ? null : GetPartDefinition(part.DefinitionId);
        WorkshopGradeEntry grade = part == null ? null : _config.GradeTable.Find(part.GradeId);
        return definition == null || grade == null ? 0f : definition.GetEfficiency(kind) * grade.StatMultiplier;
    }

    /// <summary>Returns whether the robot has positive arm efficiency for the resource kind.</summary>
    private bool CanCollect(WorkshopRobotState robot, ResourceKind kind)
    {
        return IsResourceUnlocked(kind) && FindResource(kind) != null && GetArmEfficiency(robot, kind) > 0f;
    }

    /// <summary>Checks whether a stopped robot is at the station with an empty cargo before assembly changes.</summary>
    public bool CanEditRobot(WorkshopRobotState robot, out string reason)
    {
        reason = string.Empty;
        if (robot.IsOperating || !IsAtService(robot))
        {
            reason = "개체를 호출해 정비소에 세워야 합니다.";
            return false;
        }

        if (GetCargoWeight(robot) > 0f)
        {
            reason = "개체가 운반 중입니다. 입고 또는 환전을 기다리세요.";
            return false;
        }

        if (robot.Phase == RobotWorkPhase.Returning || robot.Phase == RobotWorkPhase.Gathering || robot.Phase == RobotWorkPhase.MovingToTarget)
        {
            reason = "개체가 아직 정비소에 도착하지 않았습니다.";
            return false;
        }

        return true;
    }

    /// <summary>Returns whether another robot currently owns the equipment instance.</summary>
    private bool IsEquippedByOtherRobot(string instanceId, WorkshopRobotState selectedRobot)
    {
        for (int i = 0; i < _state.Robots.Count; i++)
        {
            WorkshopRobotState other = _state.Robots[i];
            if (other == selectedRobot)
            {
                continue;
            }

            if (other.HeadInstanceId == instanceId || other.LegsInstanceId == instanceId || other.LeftArmInstanceId == instanceId || other.RightArmInstanceId == instanceId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the instance is equipped in another slot or arm on this robot.</summary>
    private bool IsEquippedInOtherSlot(string instanceId, WorkshopRobotState selectedRobot, RobotPartSlot slot, bool isLeftArm)
    {
        if (selectedRobot == null)
        {
            return false;
        }

        if (slot == RobotPartSlot.Arms)
        {
            return isLeftArm ? selectedRobot.RightArmInstanceId == instanceId : selectedRobot.LeftArmInstanceId == instanceId;
        }

        return false;
    }

    /// <summary>Clamps robot energy and stored resource amounts to the current limits.</summary>
    private void CapRobotResources(WorkshopRobotState robot)
    {
        float maximumEnergy = CalculateMaximumEnergy(robot);
        robot.EnergyRemaining = Mathf.Min(Mathf.Max(0f, robot.EnergyRemaining), maximumEnergy);
    }

    /// <summary>Returns maximum energy from the robot core, grade, level, and equipped stats.</summary>
    private float CalculateMaximumEnergy(WorkshopRobotState robot)
    {
        return CalculateMaximumEnergy(GetPartDefinition(robot.CoreDefinitionId), robot.CoreGradeId, robot.Level, robot);
    }

    /// <summary>Returns maximum energy from the robot core, grade, level, and equipped stats.</summary>
    private float CalculateMaximumEnergy(PartDefinition core, string gradeId, int level, WorkshopRobotState robot)
    {
        if (core == null)
        {
            return _config.BaseOperatingSeconds;
        }

        WorkshopStatBlock stats = robot == null
            ? core.BaseStats.Scaled(_config.GradeTable.Find(gradeId)?.StatMultiplier ?? 1f)
            : GetStats(robot);
        return Mathf.Max(0f, _config.BaseOperatingSeconds + stats.Durability * _config.DurabilitySecondsPerPoint);
    }

    /// <summary>Returns the robot currently targeting a source of a resource.</summary>
    private bool TrySelectCurrentTarget(out WorkshopRobotState robot, out ResourceDefinition resource)
    {
        robot = GetSelectedRobot();
        resource = robot == null ? null : GetCurrentTargetResource(robot);
        return resource != null;
    }

    /// <summary>Returns the world position of the robot current bonus or source target.</summary>
    private Vector3 GetTargetPosition(WorkshopRobotState robot)
    {
        if (robot.TargetSourceId.Length > 0 && _sourceObjects.TryGetValue(robot.TargetSourceId, out WorkshopResourceSource source) && source != null)
        {
            return source.transform.position;
        }

        WorkshopBonusState bonus = FindBonus(robot.TargetBonusId);
        return bonus == null ? Vector3.positiveInfinity : bonus.Position;
    }

    /// <summary>Returns whether the current target still contains its requested resource.</summary>
    private bool HasTargetStock(WorkshopRobotState robot)
    {
        return GetCurrentTargetQuantity(robot) > 0 && GetCurrentTargetResource(robot) != null;
    }

    /// <summary>Returns remaining quantity at the robot current source or bonus target.</summary>
    private int GetCurrentTargetQuantity(WorkshopRobotState robot)
    {
        if (robot.TargetSourceId.Length > 0)
        {
            return FindSupply(robot.TargetSourceId)?.Quantity ?? 0;
        }

        return FindBonus(robot.TargetBonusId)?.Quantity ?? 0;
    }

    /// <summary>Returns the resource definition for the robot current target.</summary>
    private ResourceDefinition GetCurrentTargetResource(WorkshopRobotState robot)
    {
        if (robot.TargetSourceId.Length > 0 && _sourceObjects.TryGetValue(robot.TargetSourceId, out WorkshopResourceSource source))
        {
            return source.Resource;
        }

        WorkshopBonusState bonus = FindBonus(robot.TargetBonusId);
        return bonus == null ? null : GetResourceDefinition(bonus.ResourceId);
    }

    /// <summary>Returns whether the robot is close enough to the service point.</summary>
    private bool IsAtService(WorkshopRobotState robot)
    {
        return Vector3.Distance(robot.Position, _servicePosition) <= 0.025f;
    }

    /// <summary>Returns the number of robot records currently operating.</summary>
    private int CountOperatingRobots()
    {
        int count = 0;
        for (int i = 0; i < _state.Robots.Count; i++)
        {
            if (_state.Robots[i].IsOperating)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Returns the combined weight of the supplied resource amounts.</summary>
    private float GetResourceListWeight(List<WorkshopResourceAmount> amounts)
    {
        float weight = 0f;
        for (int i = 0; i < amounts.Count; i++)
        {
            ResourceDefinition resource = GetResourceDefinition(amounts[i].ResourceId);
            if (resource != null)
            {
                weight += resource.UnitWeight * amounts[i].Quantity;
            }
        }

        return weight;
    }

    /// <summary>Returns the exchange value of the supplied resource amounts.</summary>
    private int CalculateScrapValue(List<WorkshopResourceAmount> amounts)
    {
        int value = 0;
        for (int i = 0; i < amounts.Count; i++)
        {
            ResourceDefinition resource = GetResourceDefinition(amounts[i].ResourceId);
            if (resource != null)
            {
                value = checked(value + resource.ScrapValue * amounts[i].Quantity);
            }
        }

        return value;
    }

    /// <summary>Adds a positive quantity to a resource entry or creates a new entry in the list.</summary>
    private void AddResourceAmount(List<WorkshopResourceAmount> amounts, string resourceId, int quantity)
    {
        WorkshopResourceAmount amount = FindAmount(amounts, resourceId);
        if (amount == null)
        {
            amounts.Add(new WorkshopResourceAmount(resourceId, quantity));
        }
        else
        {
            amount.Quantity = checked(amount.Quantity + quantity);
        }
    }

    /// <summary>Returns the resource amount entry matching the supplied resource identifier.</summary>
    private WorkshopResourceAmount FindAmount(List<WorkshopResourceAmount> amounts, string resourceId)
    {
        for (int i = 0; i < amounts.Count; i++)
        {
            if (amounts[i].ResourceId == resourceId)
            {
                return amounts[i];
            }
        }

        return null;
    }

    /// <summary>Returns the saved supply state matching a scene source identifier.</summary>
    private WorkshopSupplyState FindSupply(string sourceId)
    {
        for (int i = 0; i < _state.Supplies.Count; i++)
        {
            if (_state.Supplies[i].SourceId == sourceId)
            {
                return _state.Supplies[i];
            }
        }

        return null;
    }

    /// <summary>Returns the active bonus state matching its instance identifier.</summary>
    private WorkshopBonusState FindBonus(string bonusId)
    {
        for (int i = 0; i < _state.Bonuses.Count; i++)
        {
            if (_state.Bonuses[i].InstanceId == bonusId)
            {
                return _state.Bonuses[i];
            }
        }

        return null;
    }

    /// <summary>Returns the current simulation record for this view robot identifier.</summary>
    private WorkshopRobotState FindRobot(string robotId)
    {
        for (int i = 0; i < _state.Robots.Count; i++)
        {
            if (_state.Robots[i].InstanceId == robotId)
            {
                return _state.Robots[i];
            }
        }

        return null;
    }

    /// <summary>Returns the part instance matching its saved instance identifier.</summary>
    private WorkshopPartInstance FindPart(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
        {
            return null;
        }

        for (int i = 0; i < _state.Parts.Count; i++)
        {
            if (_state.Parts[i].InstanceId == instanceId)
            {
                return _state.Parts[i];
            }
        }

        return null;
    }

    /// <summary>Resolves a saved part instance to its part definition.</summary>
    private PartDefinition GetPartDefinitionForInstance(string instanceId)
    {
        WorkshopPartInstance instance = FindPart(instanceId);
        return instance == null ? null : GetPartDefinition(instance.DefinitionId);
    }

    /// <summary>Returns the grade identifier stored on a part instance.</summary>
    private string GetGradeForInstance(string instanceId)
    {
        return FindPart(instanceId)?.GradeId;
    }

    /// <summary>Returns the resource definition matching the gameplay resource kind.</summary>
    private ResourceDefinition FindResource(ResourceKind kind)
    {
        for (int i = 0; i < _config.ResourceDefinitions.Count; i++)
        {
            if (_config.ResourceDefinitions[i].Kind == kind)
            {
                return _config.ResourceDefinitions[i];
            }
        }

        return null;
    }

    /// <summary>Returns the part instance identifier installed in the requested robot slot or arm.</summary>
    private string GetEquippedInstanceId(WorkshopRobotState robot, RobotPartSlot slot, bool isLeftArm)
    {
        if (robot == null)
        {
            return string.Empty;
        }

        switch (slot)
        {
            case RobotPartSlot.Head: return robot.HeadInstanceId;
            case RobotPartSlot.Legs: return robot.LegsInstanceId;
            case RobotPartSlot.Arms: return isLeftArm ? robot.LeftArmInstanceId : robot.RightArmInstanceId;
            default: return robot.InstanceId;
        }
    }

    /// <summary>Adds grade-scaled definition stats to the running robot stat block.</summary>
    private void AddPartStats(ref WorkshopStatBlock stats, PartDefinition definition, string gradeId)
    {
        if (definition == null || string.IsNullOrEmpty(gradeId))
        {
            return;
        }

        WorkshopGradeEntry grade = _config.GradeTable.Find(gradeId);
        stats += definition.BaseStats.Scaled(grade?.StatMultiplier ?? 1f);
    }

    /// <summary>Creates a uniquely identified part instance and appends it to the supplied save state.</summary>
    private WorkshopPartInstance CreatePart(WorkshopSaveData targetState, PartDefinition definition, string gradeId)
    {
        WorkshopPartInstance part = new WorkshopPartInstance(CreateId(), definition.Id, gradeId);
        targetState.Parts.Add(part);
        return part;
    }

    /// <summary>Returns the configured initial core grade or the first available grade.</summary>
    private string GetStartingGradeId()
    {
        WorkshopGradeEntry grade = _config.GradeTable.Find("F");
        return grade == null && _config.GradeTable.Grades.Count > 0 ? _config.GradeTable.Grades[0].Id : grade?.Id ?? "F";
    }

    /// <summary>Creates a localized robot name that is not already in use.</summary>
    private string CreateAvailableRobotName()
    {
        int suffix = _state.Robots.Count + 1;
        string candidate = $"UNIT-{suffix:00}";
        while (FindRobotByName(candidate) != null)
        {
            suffix++;
            candidate = $"UNIT-{suffix:00}";
        }

        return candidate;
    }

    /// <summary>Returns the robot whose saved name matches the supplied name.</summary>
    private WorkshopRobotState FindRobotByName(string name)
    {
        for (int i = 0; i < _state.Robots.Count; i++)
        {
            if (_state.Robots[i].Name == name)
            {
                return _state.Robots[i];
            }
        }

        return null;
    }

    /// <summary>Compares all final stat changes against the selected robot's installed part in the same slot.</summary>
    private string GetPartComparison(WorkshopRobotState robot, PartDefinition newDefinition, WorkshopGradeEntry newGrade)
    {
        if (robot == null || newDefinition.Slot == RobotPartSlot.Core)
        {
            return "개체 인벤토리에 보관";
        }

        string currentId = newDefinition.Slot == RobotPartSlot.Head
            ? robot.HeadInstanceId
            : newDefinition.Slot == RobotPartSlot.Legs ? robot.LegsInstanceId : robot.LeftArmInstanceId;
        PartDefinition currentDefinition = GetPartDefinitionForInstance(currentId);
        WorkshopGradeEntry currentGrade = _config.GradeTable.Find(GetGradeForInstance(currentId));
        if (currentDefinition == null || currentGrade == null)
        {
            return "현재 미장착 · 인벤토리에 보관";
        }

        WorkshopStatBlock oldStats = currentDefinition.BaseStats.Scaled(currentGrade.StatMultiplier);
        WorkshopStatBlock newStats = newDefinition.BaseStats.Scaled(newGrade.StatMultiplier);
        System.Text.StringBuilder comparison = new System.Text.StringBuilder($"{currentDefinition.DisplayName} 대비 ");
        int initialLength = comparison.Length;
        AppendStatDelta(comparison, "지능", newStats.Intelligence - oldStats.Intelligence);
        AppendStatDelta(comparison, "힘", newStats.Strength - oldStats.Strength);
        AppendStatDelta(comparison, "이동", newStats.MoveSpeed - oldStats.MoveSpeed);
        AppendStatDelta(comparison, "내구도", newStats.Durability - oldStats.Durability);
        AppendStatDelta(comparison, "지지력", newStats.SupportWeight - oldStats.SupportWeight);
        AppendStatDelta(comparison, "적재", newStats.CargoBonus - oldStats.CargoBonus);
        if (comparison.Length == initialLength)
        {
            comparison.Append("능력치 변화 없음");
        }

        comparison.Append(" · 자동 장착 안 함");
        return comparison.ToString();
    }

    /// <summary>Appends one nonzero ability change to a part comparison summary.</summary>
    private void AppendStatDelta(System.Text.StringBuilder builder, string label, float delta)
    {
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        builder.Append(label).Append(delta > 0f ? " +" : " ").Append(delta.ToString("0.##")).Append(' ');
    }

    /// <summary>Returns a new stable-format identifier for a saved gameplay instance.</summary>
    private string CreateId()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>Updates the current localized status message shown in the workshop UI.</summary>
    private void SetMessage(string message)
    {
        _statusMessage = message;
        _saveRequested = true;
        StateChanged?.Invoke();
    }
}
