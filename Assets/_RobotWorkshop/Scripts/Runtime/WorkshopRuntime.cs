using System.Collections.Generic;
using UnityEngine;

public sealed class WorkshopRuntime : MonoBehaviour
{
    [Header("진행 정의")]
    [SerializeField] private WorkshopGameConfig _gameConfig;
    [SerializeField] private GachaConfig _gachaConfig;
    [SerializeField] private BonusSpawnConfig _bonusSpawnConfig;
    [SerializeField] private FacilityDefinition _storageFacility;
    [SerializeField] private FacilityDefinition _expansionFacility;

    [Header("장면 배치")]
    [SerializeField] private List<WorkshopResourceSource> _resourceSources = new List<WorkshopResourceSource>();
    [SerializeField] private Transform _servicePoint;
    [SerializeField] private WorkshopRobotView _robotPrefab;
    [SerializeField] private Transform _robotParent;
    [SerializeField] private Transform _bonusParent;
    [SerializeField] private Camera _worldCamera;
    [SerializeField] private SpriteRenderer _storageRenderer;
    [SerializeField] private SpriteRenderer _expansionRenderer;
    [SerializeField] private GameObject _lockedZoneSign;
    [SerializeField] private float _minimumCameraX = 0f;
    [SerializeField] private float _maximumCameraX = 8f;

    [Header("화면")]
    [SerializeField] private WorkshopUI _workshopUI;

    private readonly Dictionary<string, WorkshopRobotView> _robotViews = new Dictionary<string, WorkshopRobotView>();
    private readonly Dictionary<string, GameObject> _bonusViews = new Dictionary<string, GameObject>();
    private readonly HashSet<string> _currentRobotIds = new HashSet<string>();
    private readonly List<string> _removedRobotIds = new List<string>();
    private readonly HashSet<string> _currentBonusIds = new HashSet<string>();
    private readonly List<string> _removedBonusIds = new List<string>();
    private WorkshopSimulation _simulation;
    private Font _worldFont;

    public WorkshopSimulation Simulation => _simulation;
    public WorkshopGameConfig GameConfig => _gameConfig;
    public GachaConfig GachaConfig => _gachaConfig;
    public BonusSpawnConfig BonusSpawnConfig => _bonusSpawnConfig;
    public FacilityDefinition StorageFacility => _storageFacility;
    public FacilityDefinition ExpansionFacility => _expansionFacility;
    public Camera WorldCamera => _worldCamera;

    private void Awake()
    {
        Application.runInBackground = true;
        if (_resourceSources.Count == 0)
        {
            _resourceSources.AddRange(FindObjectsByType<WorkshopResourceSource>(FindObjectsSortMode.None));
        }

        Vector3 servicePosition = _servicePoint == null ? Vector3.zero : _servicePoint.position;
        _simulation = new WorkshopSimulation(_gameConfig, _gachaConfig, _bonusSpawnConfig, _storageFacility, _expansionFacility, _resourceSources, servicePosition);
        _worldFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 32);
        TextMesh[] worldLabels = FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
        for (int i = 0; i < worldLabels.Length; i++)
        {
            worldLabels[i].font = _worldFont;
            worldLabels[i].GetComponent<MeshRenderer>().sharedMaterial = _worldFont.material;
        }

        if (_workshopUI != null)
        {
            _workshopUI.Initialize(this);
        }
    }

    private void Update()
    {
        if (_simulation == null)
        {
            return;
        }

        float realSeconds = Time.unscaledDeltaTime;
        _simulation.Tick(realSeconds * Mathf.Max(0f, _simulation.SimulationSpeed), realSeconds);
        UpdateWorldVisuals();
    }

    private void OnApplicationQuit()
    {
        _simulation?.Save();
    }

    /// <summary>Moves the camera through the work area while keeping the current vertical framing.</summary>
    public void ScrollCamera(float direction)
    {
        if (_worldCamera == null)
        {
            return;
        }

        float distance = _gameConfig.CameraScrollDistance * Mathf.Sign(direction);
        Vector3 position = _worldCamera.transform.position;
        position.x = Mathf.Clamp(position.x + distance, _minimumCameraX, _maximumCameraX);
        _worldCamera.transform.position = position;
    }

    /// <summary>Refreshes static facility art and synchronizes every robot and bonus pile to its persisted position.</summary>
    public void UpdateWorldVisuals()
    {
        for (int i = 0; i < _resourceSources.Count; i++)
        {
            WorkshopResourceSource source = _resourceSources[i];
            int quantity = _simulation.GetAvailableSourceQuantity(source.Resource.Kind);
            source.SetVisualState(quantity, _simulation.IsSourceOpen(source));
        }

        if (_storageRenderer != null)
        {
            _storageRenderer.sprite = _simulation.State.StorageExpanded ? _storageFacility.UpgradedSprite : _storageFacility.DamagedSprite;
        }

        if (_expansionRenderer != null)
        {
            _expansionRenderer.sprite = _simulation.State.DistrictExpanded ? _expansionFacility.RestoredSprite : _expansionFacility.DamagedSprite;
        }

        if (_lockedZoneSign != null)
        {
            _lockedZoneSign.SetActive(!_simulation.State.DistrictExpanded);
        }

        SyncRobotViews();
        SyncBonusViews();
    }

    /// <summary>Synchronizes robot views with current positions, selection, and equipment, removing views absent from the saved state.</summary>
    private void SyncRobotViews()
    {
        _currentRobotIds.Clear();
        _removedRobotIds.Clear();
        for (int i = 0; i < _simulation.Robots.Count; i++)
        {
            WorkshopRobotState robot = _simulation.Robots[i];
            _currentRobotIds.Add(robot.InstanceId);
            if (!_robotViews.TryGetValue(robot.InstanceId, out WorkshopRobotView view))
            {
                view = Instantiate(_robotPrefab, _robotParent);
                view.name = $"Robot_{robot.Name}";
                view.Configure(_simulation, robot);
                _robotViews.Add(robot.InstanceId, view);
            }

            view.SetWorldPosition(robot.Position);
            view.SetSelected(robot.InstanceId == _simulation.SelectedRobotId);
            view.Refresh(robot);
        }

        foreach (KeyValuePair<string, WorkshopRobotView> item in _robotViews)
        {
            if (!_currentRobotIds.Contains(item.Key))
            {
                Destroy(item.Value.gameObject);
                _removedRobotIds.Add(item.Key);
            }
        }

        for (int i = 0; i < _removedRobotIds.Count; i++)
        {
            _robotViews.Remove(_removedRobotIds[i]);
        }
    }

    /// <summary>Creates, positions, updates, and removes world views for active bonus piles.</summary>
    private void SyncBonusViews()
    {
        _currentBonusIds.Clear();
        _removedBonusIds.Clear();
        for (int i = 0; i < _simulation.Bonuses.Count; i++)
        {
            WorkshopBonusState bonus = _simulation.Bonuses[i];
            _currentBonusIds.Add(bonus.InstanceId);
            if (!_bonusViews.TryGetValue(bonus.InstanceId, out GameObject view))
            {
                ResourceDefinition resource = _simulation.GetResourceDefinition(bonus.ResourceId);
                view = new GameObject($"Bonus_{resource.DisplayName}");
                view.transform.SetParent(_bonusParent, false);
                SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
                renderer.sprite = resource.BonusSprite;
                renderer.sortingOrder = 1;
                TextMesh quantity = CreateWorldText(view.transform, $"×{bonus.Quantity}", 0.16f, new Vector3(0f, 0.8f, 0f));
                quantity.color = Color.white;
                _bonusViews.Add(bonus.InstanceId, view);
            }

            view.transform.position = bonus.Position;
            TextMesh label = view.GetComponentInChildren<TextMesh>();
            if (label != null)
            {
                label.text = $"×{bonus.Quantity}";
            }
        }

        foreach (KeyValuePair<string, GameObject> item in _bonusViews)
        {
            if (!_currentBonusIds.Contains(item.Key))
            {
                Destroy(item.Value);
                _removedBonusIds.Add(item.Key);
            }
        }

        for (int i = 0; i < _removedBonusIds.Count; i++)
        {
            _bonusViews.Remove(_removedBonusIds[i]);
        }
    }

    /// <summary>Creates a centered world-space label under the supplied parent at the local position.</summary>
    private TextMesh CreateWorldText(Transform parent, string text, float characterSize, Vector3 localPosition)
    {
        GameObject textObject = new GameObject("QuantityLabel");
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = localPosition;
        TextMesh textMesh = textObject.AddComponent<TextMesh>();
        textMesh.font = _worldFont;
        textObject.GetComponent<MeshRenderer>().sharedMaterial = _worldFont.material;
        textMesh.text = text;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 40;
        textMesh.characterSize = characterSize;
        return textMesh;
    }
}
