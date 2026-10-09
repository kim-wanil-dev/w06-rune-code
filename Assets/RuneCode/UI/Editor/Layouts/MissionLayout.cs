using UnityEngine;

using TMPro;
using UnityEditor;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 미션 화면 Prefab과 미션 팝업(일시정지·디버그) Prefab을 원본 미션 화면 좌표 그대로 베이크하는 에디터 빌더다.
    /// 경기장, HUD, 결과 패널을 만들고 View의 모든 직렬화 참조를 연결한다.
    /// </summary>
    public static class MissionLayout
    {
        private const float PAUSE_WIDTH = 500f;
        private const float PAUSE_HEIGHT = 384f;
        private const float DEBUG_WIDTH = 510f;
        private const float DEBUG_HEIGHT = 420f;

        /// <summary>미션 화면과 미션 팝업 Prefab을 만든다.</summary>
        public static void Build()
        {
            UiFactory ui = LayoutUtility.CreateFactory();
            BuildScreen(ui);
            BuildPausePopup(ui);
            BuildDebugPopup(ui);
        }

        /// <summary>경기장·HUD·결과 패널로 미션 화면을 만들고 MissionScreen Prefab으로 저장한다.</summary>
        private static void BuildScreen(UiFactory ui)
        {
            RectTransform page = LayoutUtility.CreateViewRoot(nameof(MissionScreen));
            MissionScreen screen = page.gameObject.AddComponent<MissionScreen>();
            MissionHud hud = page.gameObject.AddComponent<MissionHud>();

            ui.Panel(page, 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT, UiTheme.Background, "Background");

            RectTransform arenaRect = UiFactory.Rect(page, "MissionArena", 12, 74, 1256, 572);
            RuneArenaGraphic arena = arenaRect.gameObject.AddComponent<RuneArenaGraphic>();

            ui.Panel(page, 0, 0, 1280, 74, UiTheme.Panel, "TopBar");
            TextMeshProUGUI hpCaption = ui.Text(page, 18, 11, 238, 24, GameData.L("ui.hp"), 12, UiTheme.Muted, FontStyles.Normal, "HpCaption");
            ui.Panel(page, 18, 40, 238, 9, new Color(0.20f, 0.15f, 0.20f), "HpTrack");
            Image hpFill = ui.Panel(page, 18, 40, 238, 9, new Color(1f, 0.38f, 0.4f), "HpFill").GetComponent<Image>();
            TextMeshProUGUI energyCaption = ui.Text(page, 282, 11, 238, 24, GameData.L("ui.energy"), 12, UiTheme.Muted, FontStyles.Normal, "EnergyCaption");
            ui.Panel(page, 282, 40, 238, 9, new Color(0.1f, 0.2f, 0.28f), "EnergyTrack");
            Image energyFill = ui.Panel(page, 282, 40, 238, 9, UiTheme.Cyan, "EnergyFill").GetComponent<Image>();
            TextMeshProUGUI stageLabel = ui.Text(page, 542, 10, 280, 23, "", 16, Color.white, FontStyles.Bold, "StageLabel");
            TextMeshProUGUI statsLabel = ui.Text(page, 542, 37, 280, 26, "", 15, UiTheme.Cyan, FontStyles.Normal, "StatsLabel");
            TextMeshProUGUI timerLabel = ui.Text(page, 836, 14, 274, 50, "", 29, new Color(0.98f, 0.82f, 0.45f), FontStyles.Bold, "TimerLabel");
            Button pauseButton = ui.Button(page, 1142, 18, 114, 34, GameData.L("ui.pause"), null, UiTheme.Muted, 14, "PauseButton");

            ui.Panel(page, 0, 650, 1280, 70, UiTheme.Panel, "BottomBar");
            TextMeshProUGUI spellLabel = ui.Text(page, 270, 661, 968, 30, "", 17, UiTheme.Cyan, FontStyles.Normal, "SpellLabel");
            ui.Text(page, 20, 698, 1220, 19, GameData.L("ui.manualBattleControls"), 11, UiTheme.Muted, FontStyles.Normal, "ControlsHint");
            ui.Text(page, 1036, 96, 214, 32, GameData.L("ui.incoming"), 14, new Color(0.97f, 0.57f, 0.45f), FontStyles.Normal, "IncomingLabel");
            TextMeshProUGUI fragmentToast = ui.Text(page, 46, 98, 300, 35, "", 18, new Color(0.4f, 0.97f, 0.77f), FontStyles.Normal, "FragmentToast");
            Button debugButton = ui.Button(page, 20, 654, 116, 36, GameData.L("ui.debug"), null, UiTheme.Muted, 11, "DebugButton");

            ResultPanel resultPanel = BuildResultPanel(ui, page);

            LayoutUtility.SetReference(screen, "_arena", arena);
            LayoutUtility.SetReference(screen, "_hud", hud);
            LayoutUtility.SetReference(screen, "_resultPanel", resultPanel);
            LayoutUtility.SetReference(screen, "_font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LayoutUtility.FONT_PATH));

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

        /// <summary>조각 지급·전체 해금·무적·소환 버튼의 디버그 팝업을 만들고 MissionDebugPopup Prefab으로 저장한다.</summary>
        private static void BuildDebugPopup(UiFactory ui)
        {
            RectTransform root = LayoutUtility.CreateViewRoot(nameof(MissionDebugPopup));
            RectTransform card = LayoutUtility.BuildPopupFrame(ui, root, GameData.L("ui.debug"), DEBUG_WIDTH, DEBUG_HEIGHT, out Button closeButton);
            Button grantButton = ui.Button(card, 24, 82, 444, 48, GameData.L("ui.grant"), null, UiTheme.Cyan, 14, "GrantButton");
            Button unlockButton = ui.Button(card, 24, 147, 444, 48, GameData.L("ui.unlockAll"), null, UiTheme.Muted, 14, "UnlockButton");
            Button invulnerableButton = ui.Button(card, 24, 212, 444, 48, GameData.L("ui.invulnerable"), null, UiTheme.Muted, 14, "InvulnerableButton");
            Button spawnButton = ui.Button(card, 24, 277, 444, 48, GameData.L("ui.spawn"), null, UiTheme.Muted, 14, "SpawnButton");

            MissionDebugPopup popup = root.gameObject.AddComponent<MissionDebugPopup>();
            LayoutUtility.SetReference(popup, "_closeButton", closeButton);
            LayoutUtility.SetReference(popup, "_grantButton", grantButton);
            LayoutUtility.SetReference(popup, "_unlockButton", unlockButton);
            LayoutUtility.SetReference(popup, "_invulnerableButton", invulnerableButton);
            LayoutUtility.SetReference(popup, "_spawnButton", spawnButton);
            LayoutUtility.SaveViewPrefab(root);
        }
    }
}
