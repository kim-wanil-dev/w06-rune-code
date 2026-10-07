using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public sealed class WorkshopUI : MonoBehaviour
{
    private const int OPERATION_TAB = 0;
    private const int ASSEMBLY_TAB = 1;
    private const int FACILITY_TAB = 2;

    [Header("화면 설정")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Font _font;
    [SerializeField] private Sprite _currencyIcon;

    private readonly Dictionary<int, int> _equipmentSelectionIndices = new Dictionary<int, int>();
    private readonly Dictionary<int, string> _equipmentSelectionIds = new Dictionary<int, string>();
    private readonly UnityEngine.UI.Text[] _preferenceLabels = new UnityEngine.UI.Text[3];
    private readonly UnityEngine.UI.Text[] _equipmentLabels = new UnityEngine.UI.Text[4];
    private readonly UnityEngine.UI.Text[] _equipmentReasonLabels = new UnityEngine.UI.Text[4];
    private readonly UnityEngine.UI.Button[] _equipmentInstallButtons = new UnityEngine.UI.Button[4];

    private WorkshopRuntime _runtime;
    private WorkshopSimulation _simulation;
    private GameObject _operationsPage;
    private GameObject _assemblyPage;
    private GameObject _facilityPage;
    private UnityEngine.UI.Text _currencyLabel;
    private UnityEngine.UI.Text _storageLabel;
    private UnityEngine.UI.Text _selectedRobotLabel;
    private UnityEngine.UI.Text _identityLabel;
    private UnityEngine.UI.Text _progressLabel;
    private UnityEngine.UI.Text _energyLabel;
    private UnityEngine.UI.Text _statsLabel;
    private UnityEngine.UI.Text _cargoLabel;
    private UnityEngine.UI.Text _statusLabel;
    private UnityEngine.UI.Text _gachaSelectionLabel;
    private UnityEngine.UI.Text _gachaResultLabel;
    private UnityEngine.UI.Text _kitStatusLabel;
    private UnityEngine.UI.Text _storageFacilityLabel;
    private UnityEngine.UI.Text _expansionFacilityLabel;
    private UnityEngine.UI.Text _resourceSummaryLabel;
    private UnityEngine.UI.Text _debugSpeedLabel;
    private UnityEngine.UI.Text _debugPartLabel;
    private UnityEngine.UI.Text _debugGrantLabel;
    private UnityEngine.UI.Button _convertButton;
    private UnityEngine.UI.Button _storageBuyButton;
    private UnityEngine.UI.Button _expansionBuyButton;
    private UnityEngine.UI.Button _operateButton;
    private UnityEngine.UI.Button _callButton;
    private UnityEngine.UI.Button _kitButton;
    private UnityEngine.UI.Button _drawButton;
    private WorkshopRobotState _renamingRobot;
    private string _renameDraft;
    private int _selectedRobotIndex;
    private int _debugPartIndex;
    private int _debugGradeIndex;
    private int _activeTab = OPERATION_TAB;
    private float _refreshRemaining;

    /// <summary>Builds the workshop interface and connects it to the active simulation.</summary>
    public void Initialize(WorkshopRuntime runtime)
    {
        _runtime = runtime;
        _simulation = runtime.Simulation;
        if (_canvas == null)
        {
            _canvas = GetComponent<Canvas>();
        }

        if (_font == null)
        {
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 26);
        }

        EnsureCanvasComponents();
        BuildInterface();
        RefreshInterface();
    }

    private void Update()
    {
        if (_simulation == null)
        {
            return;
        }

        _refreshRemaining -= Time.unscaledDeltaTime;
        if (_refreshRemaining <= 0f)
        {
            RefreshInterface();
            _refreshRemaining = 0.2f;
        }

        if (_renamingRobot != null && Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                FinishRename(true);
            }
            else if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                FinishRename(false);
            }
        }
    }

    private void OnDestroy()
    {
        if (Keyboard.current != null)
        {
            Keyboard.current.onTextInput -= OnRenameTextInput;
        }
    }

    /// <summary>Creates required Canvas scaling, graphic raycasting, and click input components.</summary>
    private void EnsureCanvasComponents()
    {
        if (_canvas == null)
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        UnityEngine.UI.CanvasScaler scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        }

        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            InputSystemUIInputModule inputModule = eventSystem.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }
    }

    /// <summary>Creates the persistent top bar, three tab pages, their controls, and the development panel.</summary>
    private void BuildInterface()
    {
        RectTransform canvasRect = transform as RectTransform;
        canvasRect.sizeDelta = Vector2.zero;
        CreatePanel("TopBar", transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 106f), Vector2.zero, new Color(0.06f, 0.09f, 0.13f, 0.96f));

        _currencyLabel = CreateText("Currency", transform, "재화  0", 27, new Color(0.98f, 0.82f, 0.41f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(260f, 64f), new Vector2(70f, -18f), TextAnchor.MiddleLeft);
        if (_currencyIcon != null)
        {
            CreateImage("CurrencyIcon", transform, _currencyIcon, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, 36f), new Vector2(24f, -30f));
        }

        _storageLabel = CreateText("Storage", transform, "정비소 적재 0 / 600", 22, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(470f, 64f), new Vector2(28f, -58f), TextAnchor.MiddleLeft);
        _convertButton = CreateButton("Convert", transform, "자원 환전", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(154f, 58f), new Vector2(510f, -30f), TryConvertResources);
        _storageBuyButton = CreateButton("StorageFacility", transform, "자원함 확장", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(186f, 58f), new Vector2(684f, -30f), BuyStorage);
        _expansionBuyButton = CreateButton("ExpandFacility", transform, "오른쪽 증축", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(186f, 58f), new Vector2(886f, -30f), BuyExpansion);
        CreateButton("CameraLeft", transform, "◀ 작업장", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(166f, 58f), new Vector2(-208f, -30f), () => _runtime.ScrollCamera(-1f));
        CreateButton("CameraRight", transform, "오른쪽 ▶", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(166f, 58f), new Vector2(-28f, -30f), () => _runtime.ScrollCamera(1f));

        GameObject bottomPanel = CreatePanel("ManagementPanel", transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 440f), new Vector2(0f, 0f), new Color(0.04f, 0.07f, 0.1f, 0.97f));
        CreateButton("OperationTab", bottomPanel.transform, "운영", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(170f, 46f), new Vector2(20f, -8f), () => SetTab(OPERATION_TAB));
        CreateButton("AssemblyTab", bottomPanel.transform, "조립 · 뽑기", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, 46f), new Vector2(202f, -8f), () => SetTab(ASSEMBLY_TAB));
        CreateButton("FacilityTab", bottomPanel.transform, "시설 · 데이터", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, 46f), new Vector2(398f, -8f), () => SetTab(FACILITY_TAB));

        _operationsPage = CreateContentPage("OperationsPage", bottomPanel.transform);
        _assemblyPage = CreateContentPage("AssemblyPage", bottomPanel.transform);
        _facilityPage = CreateContentPage("FacilityPage", bottomPanel.transform);
        BuildOperationsPage(_operationsPage.transform);
        BuildAssemblyPage(_assemblyPage.transform);
        BuildFacilityPage(_facilityPage.transform);
        SetTab(_activeTab);
    }

    /// <summary>Creates robot selection, rename, work controls, resource preferences, and live operating statistics.</summary>
    private void BuildOperationsPage(Transform parent)
    {
        CreateColumnPanel("RobotCard", parent, 0f, 0.34f, new Color(0.1f, 0.15f, 0.21f, 0.94f));
        CreateColumnPanel("JobCard", parent, 0.34f, 0.33f, new Color(0.1f, 0.15f, 0.21f, 0.94f));
        CreateColumnPanel("StatsCard", parent, 0.67f, 0.33f, new Color(0.1f, 0.15f, 0.21f, 0.94f));

        _selectedRobotLabel = CreateText("RobotName", parent, "UNIT-01", 29, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(350f, 38f), new Vector2(22f, -14f), TextAnchor.MiddleLeft);
        CreateButton("RobotPrevious", parent, "◀", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(58f, 48f), new Vector2(24f, -57f), () => SelectRobot(-1));
        CreateButton("RobotNext", parent, "▶", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(58f, 48f), new Vector2(90f, -57f), () => SelectRobot(1));
        CreateButton("RenameRobot", parent, "이름 변경", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(144f, 48f), new Vector2(158f, -57f), BeginRename);
        _operateButton = CreateButton("OperateRobot", parent, "작업 시작", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(148f, 48f), new Vector2(316f, -57f), ToggleOperation);
        _callButton = CreateButton("CallRobot", parent, "호출", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(100f, 48f), new Vector2(474f, -57f), CallRobot);

        _identityLabel = CreateText("RobotIdentity", parent, "◆ F급 코어 · Lv.1", 20, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(438f, 35f), new Vector2(22f, -112f), TextAnchor.MiddleLeft);
        _progressLabel = CreateText("Experience", parent, "XP 0 / 20", 20, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(438f, 35f), new Vector2(22f, -151f), TextAnchor.MiddleLeft);
        _energyLabel = CreateText("Energy", parent, "가동 180 / 180초", 20, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(438f, 35f), new Vector2(22f, -190f), TextAnchor.MiddleLeft);

        CreateText("JobHeading", parent, "작업 지정 · 지능에 따라 대체·잔해 처리", 19, new Color(0.65f, 0.82f, 0.92f, 1f), new Vector2(0.34f, 1f), new Vector2(0.34f, 1f), new Vector2(0f, 1f), new Vector2(610f, 36f), new Vector2(20f, -14f), TextAnchor.MiddleLeft);
        for (int i = 0; i < 3; i++)
        {
            int preferenceIndex = i;
            float y = -59f - i * 52f;
            _preferenceLabels[i] = CreateText($"Preference_{i}", parent, "주 자원 · 철", 18, Color.white, new Vector2(0.34f, 1f), new Vector2(0.34f, 1f), new Vector2(0f, 1f), new Vector2(220f, 42f), new Vector2(20f, y), TextAnchor.MiddleLeft);
            CreateButton($"PreferenceButton_{i}", parent, "자원 바꾸기", new Vector2(0.34f, 1f), new Vector2(0.34f, 1f), new Vector2(0f, 1f), new Vector2(156f, 42f), new Vector2(248f, y), () => CyclePreference(preferenceIndex));
            CreateText($"PreferenceHint_{i}", parent, "", 15, new Color(0.7f, 0.77f, 0.83f, 1f), new Vector2(0.34f, 1f), new Vector2(0.34f, 1f), new Vector2(0f, 1f), new Vector2(240f, 42f), new Vector2(420f, y), TextAnchor.MiddleLeft);
        }

        _statsLabel = CreateText("RobotStats", parent, "", 19, Color.white, new Vector2(0.67f, 1f), new Vector2(0.67f, 1f), new Vector2(0f, 1f), new Vector2(585f, 134f), new Vector2(20f, -14f), TextAnchor.UpperLeft);
        _cargoLabel = CreateText("RobotCargo", parent, "", 18, new Color(0.9f, 0.83f, 0.65f, 1f), new Vector2(0.67f, 1f), new Vector2(0.67f, 1f), new Vector2(0f, 1f), new Vector2(585f, 82f), new Vector2(20f, -157f), TextAnchor.UpperLeft);
        _statusLabel = CreateText("Status", parent, "", 18, new Color(0.65f, 0.88f, 0.72f, 1f), new Vector2(0.67f, 1f), new Vector2(0.67f, 1f), new Vector2(0f, 1f), new Vector2(585f, 63f), new Vector2(20f, -246f), TextAnchor.UpperLeft);
    }

    /// <summary>Creates four equipment controls, a slot-specific draw button, and the basic kit action.</summary>
    private void BuildAssemblyPage(Transform parent)
    {
        CreateColumnPanel("EquipmentCard", parent, 0f, 0.57f, new Color(0.1f, 0.15f, 0.21f, 0.94f));
        CreateColumnPanel("GachaCard", parent, 0.57f, 0.43f, new Color(0.1f, 0.15f, 0.21f, 0.94f));
        CreateText("EquipmentHeading", parent, "정비소 조립 · 부품은 개체마다 소유", 20, new Color(0.65f, 0.82f, 0.92f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(690f, 38f), new Vector2(18f, -12f), TextAnchor.MiddleLeft);

        string[] names = { "머리", "다리", "왼팔", "오른팔" };
        for (int i = 0; i < 4; i++)
        {
            int equipmentIndex = i;
            float y = -59f - i * 68f;
            _equipmentLabels[i] = CreateText($"Equipment_{i}", parent, names[i] + " · 미장착", 18, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(280f, 42f), new Vector2(18f, y), TextAnchor.MiddleLeft);
            _equipmentReasonLabels[i] = CreateText($"EquipmentReason_{i}", parent, "", 14, new Color(0.72f, 0.78f, 0.84f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(294f, 28f), new Vector2(18f, y - 28f), TextAnchor.MiddleLeft);
            CreateButton($"PartPrevious_{i}", parent, "◀", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, 46f), new Vector2(324f, y), () => CycleEquipment(equipmentIndex, -1));
            CreateButton($"PartNext_{i}", parent, "▶", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, 46f), new Vector2(382f, y), () => CycleEquipment(equipmentIndex, 1));
            _equipmentInstallButtons[i] = CreateButton($"InstallPart_{i}", parent, "장착", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(98f, 46f), new Vector2(440f, y), () => InstallEquipment(equipmentIndex));
        }

        _kitButton = CreateButton("StarterKit", parent, "F급 기본 파츠 세트 제작", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f, 48f), new Vector2(20f, -320f), CraftStarterKit);
        _kitStatusLabel = CreateText("KitHint", parent, "신규 코어 선택 후 세트를 만드세요.", 16, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(370f, 38f), new Vector2(288f, -324f), TextAnchor.MiddleLeft);

        CreateText("GachaHeading", parent, "부위별 뽑기 · 등급과 종류는 별도 가중치", 20, new Color(0.65f, 0.82f, 0.92f, 1f), new Vector2(0.57f, 1f), new Vector2(0.57f, 1f), new Vector2(0f, 1f), new Vector2(680f, 38f), new Vector2(18f, -12f), TextAnchor.MiddleLeft);
        _gachaSelectionLabel = CreateText("GachaSlot", parent, "머리 · 비용 20", 25, Color.white, new Vector2(0.57f, 1f), new Vector2(0.57f, 1f), new Vector2(0f, 1f), new Vector2(280f, 48f), new Vector2(22f, -75f), TextAnchor.MiddleLeft);
        CreateButton("GachaSlotCycle", parent, "부위 선택", new Vector2(0.57f, 1f), new Vector2(0.57f, 1f), new Vector2(0f, 1f), new Vector2(150f, 48f), new Vector2(310f, -75f), CycleGachaSlot);
        _drawButton = CreateButton("DrawPart", parent, "1회 뽑기", new Vector2(0.57f, 1f), new Vector2(0.57f, 1f), new Vector2(0f, 1f), new Vector2(196f, 54f), new Vector2(22f, -142f), DrawPart);
        _gachaResultLabel = CreateText("GachaResult", parent, "결과는 인벤토리에 보관되며 자동 장착되지 않습니다.", 17, new Color(0.93f, 0.83f, 0.6f, 1f), new Vector2(0.57f, 1f), new Vector2(0.57f, 1f), new Vector2(0f, 1f), new Vector2(700f, 100f), new Vector2(22f, -212f), TextAnchor.UpperLeft);
    }

    /// <summary>Creates facility purchase summaries, fixed-source stock, and a development-only control area.</summary>
    private void BuildFacilityPage(Transform parent)
    {
        CreateColumnPanel("FacilityCard", parent, 0f, 0.47f, new Color(0.1f, 0.15f, 0.21f, 0.94f));
        CreateColumnPanel("SupplyCard", parent, 0.47f, 0.28f, new Color(0.1f, 0.15f, 0.21f, 0.94f));
        CreateColumnPanel("DevelopmentCard", parent, 0.75f, 0.25f, new Color(0.1f, 0.15f, 0.21f, 0.94f));

        CreateText("FacilityHeading", parent, "시설 투자", 20, new Color(0.65f, 0.82f, 0.92f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(500f, 36f), new Vector2(18f, -12f), TextAnchor.MiddleLeft);
        _storageFacilityLabel = CreateText("StorageFacilityInfo", parent, "", 18, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(540f, 90f), new Vector2(18f, -58f), TextAnchor.UpperLeft);
        _expansionFacilityLabel = CreateText("ExpansionFacilityInfo", parent, "", 18, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(540f, 116f), new Vector2(18f, -162f), TextAnchor.UpperLeft);

        CreateText("SupplyHeading", parent, "고정 공급원 · 열린 구역", 19, new Color(0.65f, 0.82f, 0.92f, 1f), new Vector2(0.47f, 1f), new Vector2(0.47f, 1f), new Vector2(0f, 1f), new Vector2(450f, 36f), new Vector2(16f, -12f), TextAnchor.MiddleLeft);
        _resourceSummaryLabel = CreateText("ResourceSummary", parent, "", 17, Color.white, new Vector2(0.47f, 1f), new Vector2(0.47f, 1f), new Vector2(0f, 1f), new Vector2(450f, 238f), new Vector2(16f, -56f), TextAnchor.UpperLeft);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CreateText("DevelopmentHeading", parent, "개발 패널", 19, new Color(1f, 0.72f, 0.38f, 1f), new Vector2(0.75f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, 1f), new Vector2(400f, 36f), new Vector2(16f, -12f), TextAnchor.MiddleLeft);
        _debugSpeedLabel = CreateText("DevelopmentSpeed", parent, "배속 ×1", 16, Color.white, new Vector2(0.75f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, 1f), new Vector2(150f, 34f), new Vector2(16f, -51f), TextAnchor.MiddleLeft);
        CreateButton("DevelopmentSpeedButton", parent, "배속 변경", new Vector2(0.75f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, 1f), new Vector2(132f, 38f), new Vector2(166f, -52f), CycleSimulationSpeed);
        CreateButton("DevelopmentScrapButton", parent, "재화 +1000", new Vector2(0.75f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, 1f), new Vector2(150f, 40f), new Vector2(16f, -98f), () => _simulation.GrantDevelopmentScrap(1000));
        _debugPartLabel = CreateText("DevelopmentPart", parent, "지급: - · F급", 15, Color.white, new Vector2(0.75f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, 1f), new Vector2(388f, 34f), new Vector2(16f, -145f), TextAnchor.MiddleLeft);
        CreateButton("DevelopmentPartCycle", parent, "부품/등급 선택", new Vector2(0.75f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, 1f), new Vector2(182f, 40f), new Vector2(16f, -184f), CycleDevelopmentPart);
        CreateButton("DevelopmentGrantPart", parent, "지정 지급", new Vector2(0.75f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, 1f), new Vector2(128f, 40f), new Vector2(208f, -184f), GrantDevelopmentPart);
        CreateButton("DevelopmentBonus", parent, "잔해 생성", new Vector2(0.75f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, 1f), new Vector2(132f, 40f), new Vector2(16f, -232f), SpawnDevelopmentBonus);
        CreateButton("DevelopmentReset", parent, "저장 초기화", new Vector2(0.75f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, 1f), new Vector2(150f, 40f), new Vector2(164f, -232f), ResetSave);
        _debugGrantLabel = CreateText("DevelopmentMessage", parent, "", 14, new Color(0.88f, 0.76f, 0.61f, 1f), new Vector2(0.75f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, 1f), new Vector2(400f, 34f), new Vector2(16f, -277f), TextAnchor.MiddleLeft);
#endif
    }

    /// <summary>Updates economy, robot, job, part, facility, and development text from the current saved state.</summary>
    private void RefreshInterface()
    {
        if (_simulation == null || _runtime == null)
        {
            return;
        }

        WorkshopSimulation sim = _simulation;
        WorkshopRobotState robot = sim.GetSelectedRobot();
        for (int i = 0; i < sim.Robots.Count; i++)
        {
            if (sim.Robots[i].InstanceId == sim.SelectedRobotId)
            {
                _selectedRobotIndex = i;
                break;
            }
        }

        sim.GetStorageSummary(out float storageWeight, out float storageCapacity, out int exchangeValue);
        _currencyLabel.text = $"재화  {_simulation.State.RecycledScrap:N0}";
        _storageLabel.text = $"정비소 적재  {storageWeight:0.#} / {storageCapacity:0.#}   ·   환전 예상 +{exchangeValue:N0}";
        _convertButton.interactable = sim.State.StoredResources.Count > 0;
        _storageBuyButton.interactable = !sim.IsFacilityPurchased(_runtime.StorageFacility) && sim.State.RecycledScrap >= _runtime.StorageFacility.Cost;
        _expansionBuyButton.interactable = !sim.IsFacilityPurchased(_runtime.ExpansionFacility) && sim.State.RecycledScrap >= _runtime.ExpansionFacility.Cost;

        if (robot == null)
        {
            return;
        }

        _selectedRobotLabel.text = robot.Name;
        _identityLabel.text = sim.GetRobotIdentityLabel(robot);
        sim.GetExperienceProgress(robot, out int experience, out int requiredExperience);
        _progressLabel.text = $"코어 경험치  {experience} / {requiredExperience} XP";
        sim.GetEnergyProgress(robot, out float energy, out float maximumEnergy);
        _energyLabel.text = $"연속 가동  {Mathf.CeilToInt(energy)} / {Mathf.CeilToInt(maximumEnergy)}초   ·   {sim.GetPhaseLabel(robot)}";
        _statsLabel.text = FormatStats(sim.GetStats(robot));
        _cargoLabel.text = FormatCargo(robot);
        _statusLabel.text = $"{sim.StatusMessage}\n저장 위치: {WorkshopSimulation.GetSavePath()}";
        _operateButton.GetComponentInChildren<UnityEngine.UI.Text>().text = robot.IsOperating ? "작업 중지" : "작업 재개";
        _operateButton.interactable = robot.IsOperating || sim.CanOperate(robot, out _);
        _callButton.interactable = robot.IsOperating || (robot.Phase != RobotWorkPhase.AssemblyWait && robot.Phase != RobotWorkPhase.Idle);

        for (int i = 0; i < 3; i++)
        {
            ResourceKind kind = sim.GetResourcePreference(robot, i);
            string title = i == 0 ? "주 자원" : i == 1 ? "대체 1" : "대체 2";
            _preferenceLabels[i].text = $"{title} · {sim.GetResourceName(kind)}";
            UnityEngine.UI.Text hint = _preferenceLabels[i].transform.parent.Find($"PreferenceHint_{i}").GetComponent<UnityEngine.UI.Text>();
            string reason = sim.GetResourceAvailabilityReason(kind, robot);
            hint.text = reason == "지정 가능" ? (sim.IsResourceUnlocked(kind) ? "공급 상태에 따라 수집" : "" ) : reason;
            hint.color = sim.IsResourceUnlocked(kind) ? new Color(0.72f, 0.78f, 0.84f, 1f) : new Color(0.96f, 0.68f, 0.48f, 1f);
        }

        RefreshEquipment(robot);
        _gachaSelectionLabel.text = $"{WorkshopSimulation.GetSlotName(sim.SelectedGachaSlot)} · 비용 {sim.GetGachaCost()}";
        _drawButton.interactable = sim.State.RecycledScrap >= sim.GetGachaCost();
        _storageFacilityLabel.text = FormatFacility(_runtime.StorageFacility);
        _expansionFacilityLabel.text = FormatFacility(_runtime.ExpansionFacility);
        _resourceSummaryLabel.text = FormatResources();
        if (_kitStatusLabel != null)
        {
            _kitStatusLabel.text = $"제작 비용 {_runtime.GachaConfig.StarterKitCost} · 선택 코어 {robot.Name}";
            _kitButton.interactable = sim.CanEditRobot(robot, out _) && sim.State.RecycledScrap >= _runtime.GachaConfig.StarterKitCost;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RefreshDevelopmentPanel();
#endif
    }

    /// <summary>Refreshes four slot-specific inventory choices and their installation availability.</summary>
    private void RefreshEquipment(WorkshopRobotState robot)
    {
        for (int i = 0; i < 4; i++)
        {
            RobotPartSlot slot = i < 2 ? (i == 0 ? RobotPartSlot.Head : RobotPartSlot.Legs) : RobotPartSlot.Arms;
            bool isLeftArm = i != 3;
            WorkshopPartInstance equipped = _simulation.GetEquippedPart(robot, slot, isLeftArm);
            List<WorkshopPartInstance> available = _simulation.GetAvailableParts(robot, slot, isLeftArm);
            string currentLabel = _simulation.GetEquipmentLabel(robot, slot, isLeftArm);

            if (!_equipmentSelectionIds.TryGetValue(i, out string selectedId) || !ContainsPart(available, selectedId))
            {
                selectedId = equipped != null ? equipped.InstanceId : available.Count > 0 ? available[0].InstanceId : string.Empty;
                _equipmentSelectionIds[i] = selectedId;
                _equipmentSelectionIndices[i] = FindPartIndex(available, selectedId);
            }

            WorkshopPartInstance selected = FindPart(available, selectedId);
            string selectionText = selected == null ? "보유 부품 없음" : FormatPart(selected);
            _equipmentLabels[i].text = $"{GetEquipmentName(i)} · 현재 {currentLabel}";
            _equipmentReasonLabels[i].text = available.Count == 0 ? "부품 없음 · 뽑기 또는 세트 제작 필요" : $"선택 · {selectionText}";
            _equipmentInstallButtons[i].interactable = selected != null && selected.InstanceId != (equipped == null ? string.Empty : equipped.InstanceId) && _simulation.CanEditRobot(robot, out _);
        }
    }

    /// <summary>Changes the visible operations, assembly, or facility page.</summary>
    private void SetTab(int tab)
    {
        _activeTab = tab;
        _operationsPage.SetActive(tab == OPERATION_TAB);
        _assemblyPage.SetActive(tab == ASSEMBLY_TAB);
        _facilityPage.SetActive(tab == FACILITY_TAB);
        RefreshInterface();
    }

    /// <summary>Cycles through owned core instances and selects the next robot record.</summary>
    private void SelectRobot(int direction)
    {
        if (_simulation.Robots.Count == 0)
        {
            return;
        }

        _selectedRobotIndex = Mathf.Abs(_selectedRobotIndex + direction) % _simulation.Robots.Count;
        _simulation.SelectRobot(_simulation.Robots[_selectedRobotIndex].InstanceId);
        _equipmentSelectionIds.Clear();
        RefreshInterface();
    }

    /// <summary>Renames the selected robot from keyboard text input until Enter or Escape is pressed.</summary>
    private void BeginRename()
    {
        _renamingRobot = _simulation.GetSelectedRobot();
        if (_renamingRobot == null || Keyboard.current == null)
        {
            _renamingRobot = null;
            return;
        }

        _renameDraft = _renamingRobot.Name;
        Keyboard.current.onTextInput += OnRenameTextInput;
        _selectedRobotLabel.text = $"{_renameDraft}_";
        _statusLabel.text = "이름 입력 중 · Enter 완료 · Esc 취소";
    }

    /// <summary>Appends valid keyboard text to the active robot name draft.</summary>
    private void OnRenameTextInput(char character)
    {
        if (_renamingRobot == null || char.IsControl(character) || _renameDraft.Length >= 20)
        {
            return;
        }

        _renameDraft += character;
        _selectedRobotLabel.text = $"{_renameDraft}_";
    }

    /// <summary>Commits or cancels the active name draft and releases the keyboard callback.</summary>
    private void FinishRename(bool shouldCommit)
    {
        if (_renamingRobot == null)
        {
            return;
        }

        if (Keyboard.current != null)
        {
            Keyboard.current.onTextInput -= OnRenameTextInput;
        }

        if (shouldCommit)
        {
            _simulation.RenameRobot(_renamingRobot.InstanceId, _renameDraft);
        }

        _renamingRobot = null;
        RefreshInterface();
    }

    /// <summary>Starts or stops the selected robot after the simulation checks its assembly and active slot.</summary>
    private void ToggleOperation()
    {
        WorkshopRobotState robot = _simulation.GetSelectedRobot();
        if (robot != null)
        {
            _simulation.TrySetOperating(robot.InstanceId, !robot.IsOperating, out _);
        }

        RefreshInterface();
    }

    /// <summary>Calls the selected robot to the service bay for unloading and assembly.</summary>
    private void CallRobot()
    {
        WorkshopRobotState robot = _simulation.GetSelectedRobot();
        if (robot != null)
        {
            _simulation.CallRobot(robot.InstanceId);
        }

        RefreshInterface();
    }

    /// <summary>Cycles a primary or fallback resource preference through the three configured resource kinds.</summary>
    private void CyclePreference(int preferenceIndex)
    {
        WorkshopRobotState robot = _simulation.GetSelectedRobot();
        if (robot == null)
        {
            return;
        }

        ResourceKind next = (ResourceKind)(((int)_simulation.GetResourcePreference(robot, preferenceIndex) + 1) % 3);
        _simulation.SetResourcePreference(robot, preferenceIndex, next);
        RefreshInterface();
    }

    /// <summary>Cycles the selected robot's gacha slot.</summary>
    private void CycleGachaSlot()
    {
        _simulation.CycleGachaSlot();
        RefreshInterface();
    }

    /// <summary>Draws one configured part and presents its grade, function, and selected-robot comparison.</summary>
    private void DrawPart()
    {
        _simulation.TryDraw(out string result);
        _gachaResultLabel.text = result;
        RefreshInterface();
    }

    /// <summary>Builds the configured F-grade starter parts for the selected stored core.</summary>
    private void CraftStarterKit()
    {
        WorkshopRobotState robot = _simulation.GetSelectedRobot();
        _simulation.TryCraftStarterKit(robot?.InstanceId, out string reason);
        _kitStatusLabel.text = reason;
        RefreshInterface();
    }

    /// <summary>Cycles the selected inventory candidate for the requested head, leg, or arm slot.</summary>
    private void CycleEquipment(int equipmentIndex, int direction)
    {
        WorkshopRobotState robot = _simulation.GetSelectedRobot();
        if (robot == null)
        {
            return;
        }

        RobotPartSlot slot = equipmentIndex < 2 ? (equipmentIndex == 0 ? RobotPartSlot.Head : RobotPartSlot.Legs) : RobotPartSlot.Arms;
        bool isLeftArm = equipmentIndex != 3;
        List<WorkshopPartInstance> available = _simulation.GetAvailableParts(robot, slot, isLeftArm);
        if (available.Count == 0)
        {
            return;
        }

        int index = _equipmentSelectionIndices.TryGetValue(equipmentIndex, out int currentIndex) ? currentIndex : 0;
        index = (index + direction + available.Count) % available.Count;
        _equipmentSelectionIndices[equipmentIndex] = index;
        _equipmentSelectionIds[equipmentIndex] = available[index].InstanceId;
        RefreshInterface();
    }

    /// <summary>Installs the selected owned part after station, cargo, slot, and uniqueness checks.</summary>
    private void InstallEquipment(int equipmentIndex)
    {
        WorkshopRobotState robot = _simulation.GetSelectedRobot();
        if (robot == null || !_equipmentSelectionIds.TryGetValue(equipmentIndex, out string partId))
        {
            return;
        }

        RobotPartSlot slot = equipmentIndex < 2 ? (equipmentIndex == 0 ? RobotPartSlot.Head : RobotPartSlot.Legs) : RobotPartSlot.Arms;
        bool isLeftArm = equipmentIndex != 3;
        _simulation.TryEquip(robot.InstanceId, slot, isLeftArm, partId, out string reason);
        _gachaResultLabel.text = reason;
        RefreshInterface();
    }

    /// <summary>Exchanges only resources currently stored at the service station into recycled scrap.</summary>
    private void TryConvertResources()
    {
        _simulation.TryExchangeStoredResources(out _);
        RefreshInterface();
    }

    /// <summary>Purchases the resource-bin capacity upgrade and updates the local display.</summary>
    private void BuyStorage()
    {
        _simulation.TryBuyFacility(_runtime.StorageFacility.Id, out _);
        RefreshInterface();
    }

    /// <summary>Purchases the eastern expansion and unlocks its resource sources and active robot bay.</summary>
    private void BuyExpansion()
    {
        _simulation.TryBuyFacility(_runtime.ExpansionFacility.Id, out _);
        _runtime.UpdateWorldVisuals();
        RefreshInterface();
    }

    /// <summary>Returns localized rows for stock quantity and resource source unlock state.</summary>
    private string FormatResources()
    {
        List<ResourceDefinition> resources = _simulation.GetUnlockedResources();
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < resources.Count; i++)
        {
            ResourceDefinition resource = resources[i];
            builder.Append(resource.DisplayName)
                .Append("  공급 ")
                .Append(_simulation.GetAvailableSourceQuantity(resource.Kind))
                .Append("  ·  보관 ")
                .Append(_simulation.GetStoredQuantity(resource.Id))
                .Append("\n");
        }

        if (!_simulation.State.DistrictExpanded)
        {
            builder.Append("구리·전자부품은 작업 구역 증축 후 공급됩니다.");
        }

        return builder.ToString();
    }

    /// <summary>Formats one facility's price, effect, and purchased state.</summary>
    private string FormatFacility(FacilityDefinition facility)
    {
        bool purchased = _simulation.IsFacilityPurchased(facility);
        string state = purchased ? "완료 · 다음 단계" : $"비용 {facility.Cost} 재화";
        if (facility.UnlocksDistrict)
        {
            return $"오른쪽 작업 구역 증축 · {state}\n구리·전자 공급원, 전문 팔 뽑기, 가동 자리 1→{1 + facility.ActiveRobotIncrease}\n작업장 화면 오른쪽 이동 버튼으로 새 구역을 볼 수 있습니다.";
        }

        return $"미환전 자원함 확장 · {state}\n정비소 적재 한도 +{facility.StorageCapacityIncrease:0} · 시설 그림이 업그레이드됩니다.";
    }

    /// <summary>Formats all six final stats and the derived cargo and work time.</summary>
    private string FormatStats(WorkshopStatBlock stats)
    {
        WorkshopRobotState robot = _simulation.GetSelectedRobot();
        float efficiency = _simulation.GetEffectiveArmEfficiency(robot, robot == null ? ResourceKind.Iron : robot.PrimaryResource);
        ResourceDefinition resource = robot == null ? null : _simulation.GetResourceDefinitionByKind(robot.PrimaryResource);
        float workTime = robot == null || efficiency <= 0f
            ? 0f
            : Mathf.Max(_runtime.GameConfig.MinimumGatherSeconds, resource.BaseWorkSeconds / ((1f + stats.Strength * _runtime.GameConfig.StrengthSpeedFactor) * efficiency));
        return $"최종 능력치\n지능 {stats.Intelligence:0.##}   힘 {stats.Strength:0.##}   이동 {stats.MoveSpeed:0.##}\n내구도 {stats.Durability:0.##}   지지력 {stats.SupportWeight:0.##}   적재 보정 {stats.CargoBonus:0.##}\n적재 한도 {_simulation.GetCargoCapacity(robot)}   ·   주 자원 작업 {workTime:0.##}초";
    }

    /// <summary>Formats current robot cargo and total workshop storage for the operating panel.</summary>
    private string FormatCargo(WorkshopRobotState robot)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder("운반 중 ");
        if (robot.Cargo.Count == 0)
        {
            builder.Append("없음");
        }
        else
        {
            for (int i = 0; i < robot.Cargo.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" · ");
                }

                ResourceDefinition resource = _simulation.GetResourceDefinition(robot.Cargo[i].ResourceId);
                builder.Append(resource == null ? "누락 자원" : resource.DisplayName)
                    .Append(' ')
                    .Append(robot.Cargo[i].Quantity);
            }
        }

        return builder.ToString();
    }

    /// <summary>Formats an owned part's name, grade, and instance suffix for assembly selection.</summary>
    private string FormatPart(WorkshopPartInstance part)
    {
        PartDefinition definition = _simulation.GetPartDefinition(part.DefinitionId);
        WorkshopGradeEntry grade = _simulation.GradeTable.Find(part.GradeId);
        return $"{definition?.DisplayName ?? "부품 정의 누락"} · {grade?.DisplayName ?? part.GradeId}급 · {part.InstanceId.Substring(0, 5)}";
    }

    /// <summary>Cycles simulation speed through the configured development choices.</summary>
    private void CycleSimulationSpeed()
    {
        float[] speeds = { 1f, 2f, 4f, 8f };
        int index = 0;
        for (int i = 0; i < speeds.Length; i++)
        {
            if (Mathf.Approximately(_simulation.SimulationSpeed, speeds[i]))
            {
                index = (i + 1) % speeds.Length;
                break;
            }
        }

        _simulation.SimulationSpeed = speeds[index];
        RefreshInterface();
    }

    /// <summary>Cycles available definition and grade choices for the development inventory grant.</summary>
    private void CycleDevelopmentPart()
    {
        _debugPartIndex++;
        if (_debugPartIndex >= _runtime.GameConfig.PartDefinitions.Count)
        {
            _debugPartIndex = 0;
            _debugGradeIndex = (_debugGradeIndex + 1) % _runtime.GameConfig.GradeTable.Grades.Count;
        }

        RefreshInterface();
    }

    /// <summary>Grants the development-selected part definition and grade.</summary>
    private void GrantDevelopmentPart()
    {
        PartDefinition definition = _runtime.GameConfig.PartDefinitions[_debugPartIndex % _runtime.GameConfig.PartDefinitions.Count];
        WorkshopGradeEntry grade = _runtime.GameConfig.GradeTable.Grades[_debugGradeIndex % _runtime.GameConfig.GradeTable.Grades.Count];
        _simulation.GrantDevelopmentPart(definition.Id, grade.Id, out string reason);
        _debugGrantLabel.text = reason;
        RefreshInterface();
    }

    /// <summary>Creates one eligible development bonus pile through the same bonus inventory used by robots.</summary>
    private void SpawnDevelopmentBonus()
    {
        _simulation.TrySpawnDevelopmentBonus(out string reason);
        _debugGrantLabel.text = reason;
        RefreshInterface();
    }

    /// <summary>Deletes the local save and restores the initial robot, resources, supplies, and facility state.</summary>
    private void ResetSave()
    {
        _simulation.ResetSave();
        _selectedRobotIndex = 0;
        _equipmentSelectionIds.Clear();
        _runtime.UpdateWorldVisuals();
        RefreshInterface();
    }

    /// <summary>Creates a stretchable panel with a background image and the requested layout anchors.</summary>
    private GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        UnityEngine.UI.Image image = panel.GetComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    /// <summary>Creates a non-interactive sprite icon at a fixed anchored UI position.</summary>
    private UnityEngine.UI.Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        UnityEngine.UI.Image image = imageObject.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Creates one page area positioned above the fixed tab strip.</summary>
    private GameObject CreateContentPage(string name, Transform parent)
    {
        GameObject page = new GameObject(name, typeof(RectTransform));
        page.transform.SetParent(parent, false);
        RectTransform rect = page.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 10f);
        rect.offsetMax = new Vector2(-12f, -60f);
        return page;
    }

    /// <summary>Creates a transparent anchored card occupying one horizontal share of a tab page.</summary>
    private GameObject CreateColumnPanel(string name, Transform parent, float start, float width, Color color)
    {
        GameObject panel = CreatePanel(name, parent, new Vector2(start, 0f), new Vector2(start + width, 1f), new Vector2(0f, 0f), Vector2.zero, Vector2.zero, color);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.offsetMin = new Vector2(0f, 0f);
        rect.offsetMax = new Vector2(-8f, 0f);
        return panel;
    }

    /// <summary>Creates a styled text label with explicit anchors, size, position, and alignment.</summary>
    private UnityEngine.UI.Text CreateText(string name, Transform parent, string value, int fontSize, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        UnityEngine.UI.Text text = textObject.GetComponent<UnityEngine.UI.Text>();
        text.font = _font;
        text.fontSize = fontSize;
        text.color = color;
        text.text = value;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>Creates an interactive, high-contrast button and connects its click action.</summary>
    private UnityEngine.UI.Button CreateButton(string name, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        UnityEngine.UI.Image image = buttonObject.GetComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.17f, 0.27f, 0.34f, 1f);
        UnityEngine.UI.Button button = buttonObject.GetComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        button.onClick.AddListener(action);

        UnityEngine.UI.Text text = CreateText("Label", buttonObject.transform, label, 18, Color.white, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        return button;
    }

    /// <summary>Checks whether an equipment inventory contains a selected owned part.</summary>
    private bool ContainsPart(List<WorkshopPartInstance> parts, string instanceId)
    {
        return FindPart(parts, instanceId) != null;
    }

    /// <summary>Finds a selected owned part by its unique instance identifier.</summary>
    private WorkshopPartInstance FindPart(List<WorkshopPartInstance> parts, string instanceId)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i].InstanceId == instanceId)
            {
                return parts[i];
            }
        }

        return null;
    }

    /// <summary>Finds the display index of a part in a slot's selectable inventory.</summary>
    private int FindPartIndex(List<WorkshopPartInstance> parts, string instanceId)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i].InstanceId == instanceId)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>Returns the localized display label for one robot equipment control.</summary>
    private string GetEquipmentName(int index)
    {
        switch (index)
        {
            case 0: return "머리";
            case 1: return "다리";
            case 2: return "왼팔";
            default: return "오른팔";
        }
    }

    /// <summary>Refreshes development panel labels from current speed and selected definitions.</summary>
    private void RefreshDevelopmentPanel()
    {
        if (_debugSpeedLabel == null || _runtime.GameConfig.PartDefinitions.Count == 0 || _runtime.GameConfig.GradeTable.Grades.Count == 0)
        {
            return;
        }

        PartDefinition part = _runtime.GameConfig.PartDefinitions[_debugPartIndex % _runtime.GameConfig.PartDefinitions.Count];
        WorkshopGradeEntry grade = _runtime.GameConfig.GradeTable.Grades[_debugGradeIndex % _runtime.GameConfig.GradeTable.Grades.Count];
        _debugSpeedLabel.text = $"배속 ×{_simulation.SimulationSpeed:0.#}";
        _debugPartLabel.text = $"지급: {part.DisplayName} · {grade.DisplayName}급";
    }
}
