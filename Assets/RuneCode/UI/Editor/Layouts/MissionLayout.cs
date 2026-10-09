using UnityEngine;

using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 미션 씬 레이아웃을 원본 미션 화면 좌표 그대로 베이크하는 에디터 빌더다.
    /// 미션 카메라·월드 표시 루트, HUD, 일시정지·디버그 모달, 결과 패널을 만들고 화면 컴포넌트의 모든 직렬화 참조를 연결한다.
    /// </summary>
    public static class MissionLayout
    {
        private const float CAMERA_DEPTH = -10f;
        private const float CAMERA_PRIORITY = 1f;
        private const float CAMERA_FAR_CLIP = 100f;
        private const float TIMER_MIN_FONT_SIZE = 16f;
        private const float TOP_BAR_HEIGHT = 74f;
        private const float BOTTOM_BAR_HEIGHT = 70f;

        private static readonly Color ArenaBackground = new Color(0.02f, 0.04f, 0.075f);

        /// <summary>빈 씬에 미션 화면을 만들고 Assets/Scenes/Mission.unity로 저장한다.</summary>
        public static void BuildScene()
        {
            Scene scene = LayoutUtility.CreateEmptyScene();
            UiFactory ui = LayoutUtility.CreateFactory();

            RectTransform page = UiFactory.CreateCanvas(null, "MissionCanvas");
            GameObject canvasObject = page.parent.gameObject;
            MissionScreen screen = canvasObject.AddComponent<MissionScreen>();
            MissionHud hud = canvasObject.AddComponent<MissionHud>();

            MissionWorldAssets.Result worldAssets = MissionWorldAssets.Ensure();
            MissionCamera missionCamera = CreateCamera();
            MissionWorldView worldView = new GameObject("MissionWorld", typeof(MissionWorldView)).GetComponent<MissionWorldView>();

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

            RectTransform pauseRoot = ui.Panel(page, 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT, UiTheme.ModalShade, "PauseModal");
            pauseRoot.GetComponent<Image>().raycastTarget = true;
            RectTransform pauseCard = ui.Panel(pauseRoot, (UiTheme.SCREEN_WIDTH - 500f) / 2f, (UiTheme.SCREEN_HEIGHT - 384f) / 2f, 500, 384, UiTheme.Panel, "Card");
            ui.Text(pauseCard, 24, 21, 500 - 90, 35, GameData.L("ui.pause"), 24, Color.white, FontStyles.Bold, "Title");
            Button pauseCloseButton = ui.Button(pauseCard, 500 - 60, 20, 36, 32, "×", null, UiTheme.Muted, 23, "CloseButton");
            Button resumeButton = ui.Button(pauseCard, 28, 90, 444, 48, GameData.L("ui.resume"), null, UiTheme.Cyan, 19, "ResumeButton");
            Button shakeButton = ui.Button(pauseCard, 28, 155, 444, 42, "", null, UiTheme.Muted, 16, "ShakeButton");
            Button hitStopButton = ui.Button(pauseCard, 28, 214, 444, 42, "", null, UiTheme.Muted, 16, "HitStopButton");
            Button retreatButton = ui.Button(pauseCard, 28, 292, 444, 48, GameData.L("ui.retreat"), null, UiTheme.Muted, 18, "RetreatButton");
            PausePanel pausePanel = pauseRoot.gameObject.AddComponent<PausePanel>();

            RectTransform debugRoot = ui.Panel(page, 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT, UiTheme.ModalShade, "DebugModal");
            debugRoot.GetComponent<Image>().raycastTarget = true;
            RectTransform debugCard = ui.Panel(debugRoot, (UiTheme.SCREEN_WIDTH - 510f) / 2f, (UiTheme.SCREEN_HEIGHT - 420f) / 2f, 510, 420, UiTheme.Panel, "Card");
            ui.Text(debugCard, 24, 21, 510 - 90, 35, GameData.L("ui.debug"), 24, Color.white, FontStyles.Bold, "Title");
            Button debugCloseButton = ui.Button(debugCard, 510 - 60, 20, 36, 32, "×", null, UiTheme.Muted, 23, "CloseButton");
            Button debugGrantButton = ui.Button(debugCard, 24, 82, 444, 48, GameData.L("ui.grant"), null, UiTheme.Cyan, 14, "GrantButton");
            Button debugUnlockButton = ui.Button(debugCard, 24, 147, 444, 48, GameData.L("ui.unlockAll"), null, UiTheme.Muted, 14, "UnlockButton");
            Button debugInvulnerableButton = ui.Button(debugCard, 24, 212, 444, 48, GameData.L("ui.invulnerable"), null, UiTheme.Muted, 14, "InvulnerableButton");
            Button debugSpawnButton = ui.Button(debugCard, 24, 277, 444, 48, GameData.L("ui.spawn"), null, UiTheme.Muted, 14, "SpawnButton");
            Button debugEliteSpawnButton = ui.Button(debugCard, 24, 342, 444, 48, GameData.L("ui.spawnElite"), null, UiTheme.Muted, 14, "EliteSpawnButton");

            RectTransform resultRoot = ui.Panel(page, 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT, UiTheme.Background, "ResultPanel");
            resultRoot.GetComponent<Image>().raycastTarget = true;
            RectTransform resultCard = ui.Panel(resultRoot, 92, 72, 1096, 570, UiTheme.Panel, "Card");
            ui.Text(resultCard, 40, 32, 1016, 48, GameData.L("ui.result"), 34, Color.white, FontStyles.Bold, "Title");
            TextMeshProUGUI resultSummary = ui.Text(resultCard, 40, 108, 1016, 108, "", 26, UiTheme.Cyan, FontStyles.Normal, "Summary");
            TextMeshProUGUI resultStage = ui.Text(resultCard, 40, 250, 1016, 35, "", 23, Color.white, FontStyles.Normal, "Stage");
            TextMeshProUGUI resultProgress = ui.Text(resultCard, 40, 308, 1016, 35, "", 22, UiTheme.Muted, FontStyles.Normal, "Progress");
            ui.Text(resultCard, 40, 366, 1016, 64, GameData.L("ui.improveHint"), 20, UiTheme.Muted, FontStyles.Normal, "Hint");
            Button improveButton = ui.Button(resultCard, 40, 464, 344, 58, GameData.L("ui.improveSpell"), null, UiTheme.Cyan, 20, "ImproveButton");
            Button retryButton = ui.Button(resultCard, 412, 464, 344, 58, GameData.L("ui.retryStage"), null, UiTheme.Muted, 20, "RetryButton");
            ResultPanel resultPanel = resultRoot.gameObject.AddComponent<ResultPanel>();

            LayoutUtility.SetReference(screen, "_missionCamera", missionCamera);
            LayoutUtility.SetReference(screen, "_worldView", worldView);
            LayoutUtility.SetReference(screen, "_hud", hud);
            LayoutUtility.SetReference(screen, "_pausePanel", pausePanel);
            LayoutUtility.SetReference(screen, "_resultPanel", resultPanel);

            LayoutUtility.SetReference(worldView, "_camera", missionCamera);
            LayoutUtility.SetReference(worldView, "_playerPrefab", worldAssets.Player);
            LayoutUtility.SetReference(worldView, "_enemyPrefab", worldAssets.Enemy);
            LayoutUtility.SetReference(worldView, "_spellPrefab", worldAssets.Spell);
            LayoutUtility.SetReference(worldView, "_projectilePrefab", worldAssets.Projectile);
            LayoutUtility.SetReference(worldView, "_orbPrefab", worldAssets.Orb);
            LayoutUtility.SetReference(worldView, "_damageNumberPrefab", worldAssets.DamageNumber);
            LayoutUtility.SetReference(worldView, "_squareSprite", worldAssets.Square);
            LayoutUtility.SetReference(worldView, "_spriteMaterial", worldAssets.Material);

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
            LayoutUtility.SetReference(hud, "_debugModal", debugRoot.gameObject);
            LayoutUtility.SetReference(hud, "_debugCloseButton", debugCloseButton);
            LayoutUtility.SetReference(hud, "_debugGrantButton", debugGrantButton);
            LayoutUtility.SetReference(hud, "_debugUnlockButton", debugUnlockButton);
            LayoutUtility.SetReference(hud, "_debugInvulnerableButton", debugInvulnerableButton);
            LayoutUtility.SetReference(hud, "_debugSpawnButton", debugSpawnButton);
            LayoutUtility.SetReference(hud, "_debugEliteSpawnButton", debugEliteSpawnButton);

            LayoutUtility.SetReference(pausePanel, "_resumeButton", resumeButton);
            LayoutUtility.SetReference(pausePanel, "_shakeButton", shakeButton);
            LayoutUtility.SetReference(pausePanel, "_shakeLabel", shakeButton.GetComponentInChildren<TextMeshProUGUI>());
            LayoutUtility.SetReference(pausePanel, "_hitStopButton", hitStopButton);
            LayoutUtility.SetReference(pausePanel, "_hitStopLabel", hitStopButton.GetComponentInChildren<TextMeshProUGUI>());
            LayoutUtility.SetReference(pausePanel, "_retreatButton", retreatButton);
            LayoutUtility.SetReference(pausePanel, "_closeButton", pauseCloseButton);

            LayoutUtility.SetReference(resultPanel, "_summaryLabel", resultSummary);
            LayoutUtility.SetReference(resultPanel, "_stageLabel", resultStage);
            LayoutUtility.SetReference(resultPanel, "_progressLabel", resultProgress);
            LayoutUtility.SetReference(resultPanel, "_improveButton", improveButton);
            LayoutUtility.SetReference(resultPanel, "_retryButton", retryButton);

            pauseRoot.gameObject.SetActive(false);
            debugRoot.gameObject.SetActive(false);
            resultRoot.gameObject.SetActive(false);

            LayoutUtility.SaveScene(scene, "Mission");
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
            var serialized = new UnityEditor.SerializedObject(missionCamera);
            serialized.FindProperty("_referenceResolution").vector2Value = new Vector2(UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT);
            serialized.FindProperty("_topHudHeight").floatValue = TOP_BAR_HEIGHT;
            serialized.FindProperty("_bottomHudHeight").floatValue = BOTTOM_BAR_HEIGHT;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return missionCamera;
        }
    }
}
