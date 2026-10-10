using UnityEngine;

using TMPro;
using UnityEditor;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 미션 화면 Prefab, 미션 월드(카메라·월드 표시) Prefab과 미션 팝업(일시정지·디버그) Prefab을 원본 미션 화면 좌표 그대로 베이크하는 에디터 빌더다.
    /// HUD와 결과 패널을 만들고 View의 모든 직렬화 참조를 연결한다. 전투 화면은 월드 카메라가 그리므로 화면 Prefab에는 배경을 두지 않는다.
    /// </summary>
    public static class MissionLayout
    {
        private const string WORLD_PREFAB_PATH = MissionWorldAssets.PREFAB_FOLDER + "/MissionWorld.prefab";
        private const float PAUSE_WIDTH = 500f;
        private const float PAUSE_HEIGHT = 384f;
        private const float DEBUG_WIDTH = 510f;
        private const float DEBUG_HEIGHT = 615f;
        private const float CAMERA_DEPTH = -10f;
        private const float CAMERA_PRIORITY = 1f;
        private const float CAMERA_FAR_CLIP = 100f;
        private const float TIMER_MIN_FONT_SIZE = 16f;
        private const float TOP_BAR_HEIGHT = 74f;
        private const float BOTTOM_BAR_HEIGHT = 70f;

        private static readonly Color ArenaBackground = new Color(0.02f, 0.04f, 0.075f);

        /// <summary>미션 화면과 미션 팝업 Prefab 중 없는 것만 만든다.</summary>
        public static void Build()
        {
            UiFactory ui = LayoutUtility.CreateFactory();
            BuildScreen(ui);
            BuildPausePopup(ui);
            BuildDebugPopup(ui);
        }

        /// <summary>HUD·결과 패널로 미션 화면을 만들고 미션 월드 Prefab을 연결해 MissionScreen Prefab으로 저장한다.</summary>
        private static void BuildScreen(UiFactory ui)
        {
            if (LayoutUtility.ViewPrefabExists(nameof(MissionScreen))) return;
            MissionWorldView worldView = EnsureWorldPrefab();
            RectTransform page = LayoutUtility.CreateViewRoot(nameof(MissionScreen));
            MissionScreen screen = page.gameObject.AddComponent<MissionScreen>();
            MissionHud hud = page.gameObject.AddComponent<MissionHud>();

            ui.Panel(page, 0, 0, UiTheme.SCREEN_WIDTH, TOP_BAR_HEIGHT, UiTheme.Panel, "TopBar");
            TextMeshProUGUI hpCaption = ui.Text(page, 18, 11, 238, 24, GameData.L("ui.hp"), 12, UiTheme.Muted, FontStyles.Normal, "HpCaption");
            ui.Panel(page, 18, 40, 238, 9, new Color(0.20f, 0.15f, 0.20f), "HpTrack");
            Image hpFill = ui.Panel(page, 18, 40, 238, 9, new Color(1f, 0.38f, 0.4f), "HpFill").GetComponent<Image>();
            TextMeshProUGUI energyCaption = ui.Text(page, 282, 11, 238, 24, GameData.L("ui.energy"), 12, UiTheme.Muted, FontStyles.Normal, "EnergyCaption");
            ui.Panel(page, 282, 40, 238, 9, new Color(0.1f, 0.2f, 0.28f), "EnergyTrack");
            Image energyFill = ui.Panel(page, 282, 40, 238, 9, UiTheme.Cyan, "EnergyFill").GetComponent<Image>();
            TextMeshProUGUI stageLabel = ui.Text(page, 542, 10, 280, 23, "", 16, Color.white, FontStyles.Bold, "StageLabel");
            TextMeshProUGUI statsLabel = ui.Text(page, 542, 37, 280, 26, "", 15, UiTheme.Cyan, FontStyles.Normal, "StatsLabel");
            TextMeshProUGUI timerLabel = ui.Text(page, 836, 14, 274, 50, "", 29, new Color(0.98f, 0.82f, 0.45f), FontStyles.Bold, "TimerLabel");
            timerLabel.enableAutoSizing = true;
            timerLabel.fontSizeMin = TIMER_MIN_FONT_SIZE;
            timerLabel.fontSizeMax = 29;
            Button pauseButton = ui.Button(page, 1142, 18, 114, 34, GameData.L("ui.pause"), null, UiTheme.Muted, 14, "PauseButton");

            ui.Panel(page, 0, UiTheme.SCREEN_HEIGHT - BOTTOM_BAR_HEIGHT, UiTheme.SCREEN_WIDTH, BOTTOM_BAR_HEIGHT, UiTheme.Panel, "BottomBar");
            TextMeshProUGUI spellLabel = ui.Text(page, 270, 661, 968, 30, "", 17, UiTheme.Cyan, FontStyles.Normal, "SpellLabel");
            ui.Text(page, 20, 698, 1220, 19, GameData.L("ui.manualBattleControls"), 11, UiTheme.Muted, FontStyles.Normal, "ControlsHint");
            TextMeshProUGUI fragmentToast = ui.Text(page, 46, 98, 300, 35, "", 18, new Color(0.4f, 0.97f, 0.77f), FontStyles.Normal, "FragmentToast");
            Button debugButton = ui.Button(page, 20, 654, 116, 36, GameData.L("ui.debug"), null, UiTheme.Muted, 11, "DebugButton");

            ResultPanel resultPanel = BuildResultPanel(ui, page);

            LayoutUtility.SetReference(screen, "_worldPrefab", worldView);
            LayoutUtility.SetReference(screen, "_hud", hud);
            LayoutUtility.SetReference(screen, "_resultPanel", resultPanel);

            LayoutUtility.SetReference(hud, "_hpCaption", hpCaption);
            LayoutUtility.SetReference(hud, "_energyCaption", energyCaption);
            LayoutUtility.SetReference(hud, "_hpFill", hpFill);
            LayoutUtility.SetReference(hud, "_energyFill", energyFill);
            LayoutUtility.SetReference(hud, "_stageLabel", stageLabel);
            LayoutUtility.SetReference(hud, "_statsLabel", statsLabel);
            LayoutUtility.SetReference(hud, "_timerLabel", timerLabel);
            LayoutUtility.SetReference(hud, "_spellLabel", spellLabel);
            LayoutUtility.SetReference(hud, "_fragmentToast", fragmentToast);
            LayoutUtility.SetReference(hud, "_pauseButton", pauseButton);
            LayoutUtility.SetReference(hud, "_debugButton", debugButton);

            LayoutUtility.SaveViewPrefab(page);
        }

        /// <summary>정산 문구와 개선·재도전 버튼의 전체 화면 결과 패널을 만들고 참조를 연결해 반환한다. 처음에는 숨겨 둔다.</summary>
        private static ResultPanel BuildResultPanel(UiFactory ui, RectTransform page)
        {
            RectTransform resultRoot = ui.Panel(page, 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT, UiTheme.Background, "ResultPanel");
            resultRoot.GetComponent<Image>().raycastTarget = true;
            RectTransform resultCard = ui.Panel(resultRoot, 92, 72, 1096, 570, UiTheme.Panel, "Card");
            ui.Text(resultCard, 40, 32, 1016, 48, GameData.L("ui.result"), 34, Color.white, FontStyles.Bold, "Title");
            TextMeshProUGUI summary = ui.Text(resultCard, 40, 108, 1016, 108, "", 26, UiTheme.Cyan, FontStyles.Normal, "Summary");
            TextMeshProUGUI stage = ui.Text(resultCard, 40, 250, 1016, 35, "", 23, Color.white, FontStyles.Normal, "Stage");
            TextMeshProUGUI progress = ui.Text(resultCard, 40, 308, 1016, 35, "", 22, UiTheme.Muted, FontStyles.Normal, "Progress");
            ui.Text(resultCard, 40, 366, 1016, 64, GameData.L("ui.improveHint"), 20, UiTheme.Muted, FontStyles.Normal, "Hint");
            Button improveButton = ui.Button(resultCard, 40, 464, 344, 58, GameData.L("ui.improveSpell"), null, UiTheme.Cyan, 20, "ImproveButton");
            Button retryButton = ui.Button(resultCard, 412, 464, 344, 58, GameData.L("ui.retryStage"), null, UiTheme.Muted, 20, "RetryButton");

            ResultPanel resultPanel = resultRoot.gameObject.AddComponent<ResultPanel>();
            LayoutUtility.SetReference(resultPanel, "_summaryLabel", summary);
            LayoutUtility.SetReference(resultPanel, "_stageLabel", stage);
            LayoutUtility.SetReference(resultPanel, "_progressLabel", progress);
            LayoutUtility.SetReference(resultPanel, "_improveButton", improveButton);
            LayoutUtility.SetReference(resultPanel, "_retryButton", retryButton);
            resultRoot.gameObject.SetActive(false);
            return resultPanel;
        }

        /// <summary>계속하기·피드백 설정·후퇴 버튼의 일시정지 팝업을 만들고 PausePopup Prefab으로 저장한다.</summary>
        private static void BuildPausePopup(UiFactory ui)
        {
            if (LayoutUtility.ViewPrefabExists(nameof(PausePopup))) return;
            RectTransform root = LayoutUtility.CreateViewRoot(nameof(PausePopup));
            RectTransform card = LayoutUtility.BuildPopupFrame(ui, root, GameData.L("ui.pause"), PAUSE_WIDTH, PAUSE_HEIGHT, out Button closeButton);
            Button resumeButton = ui.Button(card, 28, 90, 444, 48, GameData.L("ui.resume"), null, UiTheme.Cyan, 19, "ResumeButton");
            Button shakeButton = ui.Button(card, 28, 155, 444, 42, "", null, UiTheme.Muted, 16, "ShakeButton");
            Button hitStopButton = ui.Button(card, 28, 214, 444, 42, "", null, UiTheme.Muted, 16, "HitStopButton");
            Button retreatButton = ui.Button(card, 28, 292, 444, 48, GameData.L("ui.retreat"), null, UiTheme.Muted, 18, "RetreatButton");

            PausePopup popup = root.gameObject.AddComponent<PausePopup>();
            LayoutUtility.SetReference(popup, "_closeButton", closeButton);
            LayoutUtility.SetReference(popup, "_resumeButton", resumeButton);
            LayoutUtility.SetReference(popup, "_shakeButton", shakeButton);
            LayoutUtility.SetReference(popup, "_shakeLabel", shakeButton.GetComponentInChildren<TextMeshProUGUI>());
            LayoutUtility.SetReference(popup, "_hitStopButton", hitStopButton);
            LayoutUtility.SetReference(popup, "_hitStopLabel", hitStopButton.GetComponentInChildren<TextMeshProUGUI>());
            LayoutUtility.SetReference(popup, "_retreatButton", retreatButton);
            LayoutUtility.SaveViewPrefab(root);
        }

        /// <summary>조각 지급·전체 해금·무적·소환·엘리트 소환·분열형 소환·엘리트 분열형 소환 버튼의 디버그 팝업을 만들고 MissionDebugPopup Prefab으로 저장한다.</summary>
        private static void BuildDebugPopup(UiFactory ui)
        {
            if (LayoutUtility.ViewPrefabExists(nameof(MissionDebugPopup))) return;
            RectTransform root = LayoutUtility.CreateViewRoot(nameof(MissionDebugPopup));
            RectTransform card = LayoutUtility.BuildPopupFrame(ui, root, GameData.L("ui.debug"), DEBUG_WIDTH, DEBUG_HEIGHT, out Button closeButton);
            Button grantButton = ui.Button(card, 24, 82, 444, 48, GameData.L("ui.grant"), null, UiTheme.Cyan, 14, "GrantButton");
            Button unlockButton = ui.Button(card, 24, 147, 444, 48, GameData.L("ui.unlockAll"), null, UiTheme.Muted, 14, "UnlockButton");
            Button invulnerableButton = ui.Button(card, 24, 212, 444, 48, GameData.L("ui.invulnerable"), null, UiTheme.Muted, 14, "InvulnerableButton");
            Button spawnButton = ui.Button(card, 24, 277, 444, 48, GameData.L("ui.spawn"), null, UiTheme.Muted, 14, "SpawnButton");
            Button eliteSpawnButton = ui.Button(card, 24, 342, 444, 48, GameData.L("ui.spawnElite"), null, UiTheme.Muted, 14, "EliteSpawnButton");
            Button splitterSpawnButton = ui.Button(card, 24, 407, 444, 48, GameData.L("ui.spawnSplitter"), null, UiTheme.Muted, 14, "SplitterSpawnButton");
            Button splitterEliteSpawnButton = ui.Button(card, 24, 472, 444, 48, GameData.L("ui.spawnEliteSplitter"), null, UiTheme.Muted, 14, "SplitterEliteSpawnButton");

            MissionDebugPopup popup = root.gameObject.AddComponent<MissionDebugPopup>();
            LayoutUtility.SetReference(popup, "_closeButton", closeButton);
            LayoutUtility.SetReference(popup, "_grantButton", grantButton);
            LayoutUtility.SetReference(popup, "_unlockButton", unlockButton);
            LayoutUtility.SetReference(popup, "_invulnerableButton", invulnerableButton);
            LayoutUtility.SetReference(popup, "_spawnButton", spawnButton);
            LayoutUtility.SetReference(popup, "_eliteSpawnButton", eliteSpawnButton);
            LayoutUtility.SetReference(popup, "_splitterSpawnButton", splitterSpawnButton);
            LayoutUtility.SetReference(popup, "_splitterEliteSpawnButton", splitterEliteSpawnButton);
            LayoutUtility.SaveViewPrefab(root);
        }

        /// <summary>
        /// 미션 월드 Prefab(Prefabs/Mission/MissionWorld)이 없으면 월드 표시 루트와 미션 카메라로 만들어 저장하고, 있으면 그대로 불러온다.
        /// 월드 표시에는 개체 뷰 Prefab·도형 스프라이트·재질과 카메라를 연결한다.
        /// </summary>
        private static MissionWorldView EnsureWorldPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(WORLD_PREFAB_PATH);
            if (existing != null) return existing.GetComponent<MissionWorldView>();
            MissionWorldAssets.Result worldAssets = MissionWorldAssets.Ensure();
            MissionWorldView worldView = new GameObject("MissionWorld", typeof(MissionWorldView)).GetComponent<MissionWorldView>();
            MissionCamera missionCamera = CreateCamera();
            missionCamera.transform.SetParent(worldView.transform, false);
            LayoutUtility.SetReference(worldView, "_camera", missionCamera);
            LayoutUtility.SetReference(worldView, "_playerPrefab", worldAssets.Player);
            LayoutUtility.SetReference(worldView, "_enemyPrefab", worldAssets.Enemy);
            LayoutUtility.SetReference(worldView, "_spellPrefab", worldAssets.Spell);
            LayoutUtility.SetReference(worldView, "_projectilePrefab", worldAssets.Projectile);
            LayoutUtility.SetReference(worldView, "_orbPrefab", worldAssets.Orb);
            LayoutUtility.SetReference(worldView, "_damageNumberPrefab", worldAssets.DamageNumber);
            LayoutUtility.SetReference(worldView, "_squareSprite", worldAssets.Square);
            LayoutUtility.SetReference(worldView, "_spriteMaterial", worldAssets.Material);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(worldView.gameObject, WORLD_PREFAB_PATH);
            Object.DestroyImmediate(worldView.gameObject);
            return saved.GetComponent<MissionWorldView>();
        }

        /// <summary>
        /// 미션 전용 정사영 카메라를 만든다. Boot 씬 카메라보다 늦게 그리도록 우선순위를 높이고 경기장 배경색으로 화면을 지우며,
        /// HUD와 같은 기준 해상도·상하단 바 높이를 카메라 경계 계산에 넘긴다.
        /// HUD는 Overlay 캔버스라 이 카메라 위에 그려진다.
        /// </summary>
        private static MissionCamera CreateCamera()
        {
            var cameraObject = new GameObject("MissionCamera", typeof(Camera), typeof(MissionCamera));
            cameraObject.transform.position = new Vector3(0, 0, CAMERA_DEPTH);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ArenaBackground;
            camera.depth = CAMERA_PRIORITY;
            camera.farClipPlane = CAMERA_FAR_CLIP;
            MissionCamera missionCamera = cameraObject.GetComponent<MissionCamera>();
            var serialized = new SerializedObject(missionCamera);
            serialized.FindProperty("_referenceResolution").vector2Value = new Vector2(UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT);
            serialized.FindProperty("_topHudHeight").floatValue = TOP_BAR_HEIGHT;
            serialized.FindProperty("_bottomHudHeight").floatValue = BOTTOM_BAR_HEIGHT;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return missionCamera;
        }
    }
}
