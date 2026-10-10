using System;

using UnityEngine;

using TMPro;
using UnityEditor;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>작업실 화면 Prefab의 헤더, 공유 전투 패널을 포함한 6개 탭과 상태줄을 생성하고 참조를 연결한다.</summary>
    public static class WorkshopLayout
    {
        private const string UPGRADE_CARD_PREFAB_PATH = LayoutUtility.PREFAB_FOLDER + "/UpgradeCard.prefab";
        private const string UPGRADE_TREE_NODE_PREFAB_PATH = LayoutUtility.PREFAB_FOLDER + "/UpgradeTreeNode.prefab";
        private const string UPGRADE_TREE_ASSET_PATH = "Assets/RuneCode/Resources/RuneCode/UpgradeTree.asset";
        private static readonly Color RESET_COLOR = new Color(1f, 0.43f, 0.43f);
        private static readonly string[] TABS = { "editor", "bench", "tree", "deploy", "puzzle", "settings" };

        /// <summary>작업실 헤더·탭 패널과 화면 컴포넌트를 만들고 모든 직렬화 참조를 연결해 WorkshopScreen Prefab으로 저장한다.</summary>
        public static void Build()
        {
            if (LayoutUtility.ViewPrefabExists(nameof(WorkshopScreen))) return;
            UiFactory ui = LayoutUtility.CreateFactory();
            RectTransform root = LayoutUtility.CreateViewRoot(nameof(WorkshopScreen));
            RectTransform page = ui.Panel(root, 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT, UiTheme.Background, "Page");

            ui.Panel(page, 0, 0, 1280, 70, UiTheme.Panel, "Header");
            ui.Text(page, 18, 18, 200, 36, GameData.L("ui.title"), 27, Color.white, FontStyles.Bold, "HeaderTitle");
            Button[] tabButtons = new Button[TABS.Length];
            TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[TABS.Length];
            for (int i = 0; i < TABS.Length; i++)
            {
                tabButtons[i] = ui.Button(page, 220 + i * 96, 18, 90, 34, GameData.L("ui." + TABS[i]), null, UiTheme.Muted, 13, TABS[i] + "TabButton");
                tabLabels[i] = tabButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            }
            TextMeshProUGUI headerStats = ui.Text(page, 816, 23, 440, 30, "", 16, UiTheme.Cyan, FontStyles.Normal, "HeaderStats");
            TextMeshProUGUI status = ui.Text(page, 18, 686, 1240, 22, "", 12, UiTheme.Muted, FontStyles.Normal, "Status");

            RectTransform editorRoot = UiFactory.Rect(page, "EditorTab", 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT);
            SpellEditorPanel spellEditor = SpellEditorLayout.Build(ui, editorRoot);
            DockPanel dockPanel = BuildDock(ui, editorRoot);
            RectTransform benchRoot = UiFactory.Rect(page, "BenchTab", 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT);
            BenchPanel benchPanel = BuildBench(ui, benchRoot);
            RectTransform treeRoot = UiFactory.Rect(page, "UpgradeTreeTab", 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT);
            UpgradeTreePanel upgradeTreePanel = BuildUpgradeTree(ui, treeRoot);
            RectTransform deployRoot = UiFactory.Rect(page, "DeployTab", 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT);
            DeployPanel deployPanel = BuildDeploy(ui, deployRoot);
            RectTransform settingsRoot = UiFactory.Rect(page, "SettingsTab", 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT);
            SettingsPanel settingsPanel = BuildSettings(ui, settingsRoot);
            benchRoot.gameObject.SetActive(false);
            treeRoot.gameObject.SetActive(false);
            deployRoot.gameObject.SetActive(false);
            settingsRoot.gameObject.SetActive(false);

            WorkshopScreen screen = root.gameObject.AddComponent<WorkshopScreen>();
            LayoutUtility.SetReference(screen, "_editorRoot", editorRoot);
            LayoutUtility.SetReference(screen, "_benchRoot", benchRoot);
            LayoutUtility.SetReference(screen, "_treeRoot", treeRoot);
            LayoutUtility.SetReference(screen, "_deployRoot", deployRoot);
            LayoutUtility.SetReference(screen, "_settingsRoot", settingsRoot);
            LayoutUtility.SetReferences(screen, "_tabButtons", tabButtons);
            LayoutUtility.SetReferences(screen, "_tabLabels", tabLabels);
            LayoutUtility.SetReference(screen, "_spellEditor", spellEditor);
            LayoutUtility.SetReference(screen, "_dockPanel", dockPanel);
            LayoutUtility.SetReference(screen, "_benchPanel", benchPanel);
            LayoutUtility.SetReference(screen, "_upgradeTreePanel", upgradeTreePanel);
            LayoutUtility.SetReference(screen, "_deployPanel", deployPanel);
            LayoutUtility.SetReference(screen, "_settingsPanel", settingsPanel);
            LayoutUtility.SetReference(screen, "_headerStats", headerStats);
            LayoutUtility.SetReference(screen, "_status", status);
            LayoutUtility.SaveViewPrefab(root);
        }

        /// <summary>시험 도크의 경기장, 시나리오·시험·리셋·자동 발사·적응·배속 조작과 지표 문구를 만들고 DockPanel 참조를 연결한다.</summary>
        private static DockPanel BuildDock(UiFactory ui, RectTransform parent)
        {
            ui.Panel(parent, 222, 512, 772, 143, UiTheme.Panel, "DockBackground");
            RectTransform arena = UiFactory.Rect(parent, "DockArena", 222, 541, 292, 110);
            RuneArenaGraphic arenaGraphic = arena.gameObject.AddComponent<RuneArenaGraphic>();
            Button scenarioButton = ui.Button(parent, 226, 515, 126, 22, GameData.L("scenario.dummy_single"), null, UiTheme.Muted, 10, "ScenarioButton");
            Button testButton = ui.Button(parent, 360, 515, 50, 22, GameData.L("ui.test"), null, UiTheme.Cyan, 10, "TestButton");
            Button resetButton = ui.Button(parent, 418, 515, 86, 22, GameData.L("ui.reset"), null, UiTheme.Muted, 10, "ResetButton");
            Button autoFireButton = ui.Button(parent, 528, 521, 128, 27, GameData.L("ui.autofire"), null, UiTheme.Cyan, 11, "AutoFireButton");
            Button adaptationButton = ui.Button(parent, 666, 521, 124, 27, GameData.L("ui.adaptation"), null, UiTheme.Cyan, 11, "AdaptationButton");
            Button speedButton = ui.Button(parent, 800, 521, 76, 27, "0.5 / 1 / 2×", null, UiTheme.Muted, 10, "SpeedButton");
            TextMeshProUGUI metrics = ui.Text(parent, 528, 562, 455, 46, "", 12, Color.white, FontStyles.Normal, "DockMetrics");
            TextMeshProUGUI adaptation = ui.Text(parent, 528, 605, 452, 45, "", 10, UiTheme.Muted, FontStyles.Normal, "AdaptationSummary");

            DockPanel dockPanel = parent.gameObject.AddComponent<DockPanel>();
            LayoutUtility.SetReference(dockPanel, "_arena", arenaGraphic);
            LayoutUtility.SetReference(dockPanel, "_font", ui.Font);
            LayoutUtility.SetReference(dockPanel, "_scenarioButton", scenarioButton);
            LayoutUtility.SetReference(dockPanel, "_testButton", testButton);
            LayoutUtility.SetReference(dockPanel, "_resetButton", resetButton);
            LayoutUtility.SetReference(dockPanel, "_autoFireButton", autoFireButton);
            LayoutUtility.SetReference(dockPanel, "_adaptationButton", adaptationButton);
            LayoutUtility.SetReference(dockPanel, "_speedButton", speedButton);
            LayoutUtility.SetReference(dockPanel, "_metrics", metrics);
            LayoutUtility.SetReference(dockPanel, "_adaptationText", adaptation);
            return dockPanel;
        }

        /// <summary>성장 카드 3장과 룬 해금 목록, 행 Prefab을 만들고 BenchPanel 참조를 연결한다.</summary>
        private static BenchPanel BuildBench(UiFactory ui, RectTransform parent)
        {
            ui.Text(parent, 30, 98, 850, 40, GameData.L("ui.bench"), 29, Color.white, FontStyles.Bold, "BenchTitle");
            TextMeshProUGUI balance = ui.Text(parent, 30, 144, 800, 34, "", 20, UiTheme.Cyan, FontStyles.Normal, "Balance");
            string[] types = { "capacity", "energy", "duration" };
            UpgradeCardView[] upgradeCards = new UpgradeCardView[types.Length];
            UpgradeCardView upgradeCardPrefab = EnsureUpgradeCardPrefab(ui);
            for (int i = 0; i < types.Length; i++)
            {
                upgradeCards[i] = LayoutUtility.InstantiatePrefab(upgradeCardPrefab, parent, types[i] + "UpgradeCard");
                LayoutUtility.SetTopLeftRect((RectTransform)upgradeCards[i].transform, 30 + i * 406, 196, 386, 184);
            }
            ui.Text(parent, 30, 393, 1200, 24, GameData.L("ui.runeUnlocks"), 15, Color.white, FontStyles.Bold, "RuneUnlocksTitle");
            RectTransform unlockList = UiFactory.ScrollList(parent, "UnlockList", 28, 426, 1224, 228);

            BenchPanel benchPanel = parent.gameObject.AddComponent<BenchPanel>();
            LayoutUtility.SetReference(benchPanel, "_balanceText", balance);
            LayoutUtility.SetReferences(benchPanel, "_upgradeCards", upgradeCards);
            LayoutUtility.SetReference(benchPanel, "_unlockList", unlockList);
            UiRow runeRow = LayoutUtility.GetSharedRowPrefab(ui);
            LayoutUtility.SetReference(benchPanel, "_runeRowPrefab", runeRow);
            return benchPanel;
        }

        /// <summary>트리 뷰포트·우클릭 이동·휠 확대, 포인터 정보 패널과 ScriptableObject·노드 Prefab 참조를 연결한다.</summary>
        private static UpgradeTreePanel BuildUpgradeTree(UiFactory ui, RectTransform parent)
        {
            ui.Text(parent, 30, 98, 850, 40, GameData.L("ui.tree.title"), 29, Color.white, FontStyles.Bold, "UpgradeTreeTitle");
            TextMeshProUGUI summary = ui.Text(parent, 30, 139, 1210, 23, "", 14, UiTheme.Cyan, FontStyles.Normal, "UpgradeTreeSummary");

            RectTransform viewport = UiFactory.Rect(parent, "UpgradeTreeViewport", 28, 171, 1224, 436);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1, 1, 1, 0.005f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = UiFactory.Rect(viewport, "Content", 0, 0, 1800, 560);

            RectTransform hoverPanel = ui.Panel(parent, 28, 615, 1224, 58, UiTheme.Panel, "UpgradeTreeHoverInfo");
            TextMeshProUGUI hoverTitle = ui.Text(hoverPanel, 14, 5, 1190, 20, "", 14, Color.white, FontStyles.Bold, "HoverTitle");
            TextMeshProUGUI hoverDescription = ui.Text(hoverPanel, 14, 27, 1190, 25, "", 12, UiTheme.Muted, FontStyles.Normal, "HoverDescription");
            hoverPanel.gameObject.SetActive(false);

            UpgradeTreeDefinition definition = AssetDatabase.LoadAssetAtPath<UpgradeTreeDefinition>(UPGRADE_TREE_ASSET_PATH);
            if (definition == null) throw new InvalidOperationException("업그레이드 트리 자산을 찾을 수 없습니다: " + UPGRADE_TREE_ASSET_PATH);
            if (!definition.Validate(out string error)) throw new InvalidOperationException(error);
            UpgradeTreeNodeView nodePrefab = EnsureUpgradeTreeNodePrefab();
            UpgradeTreeCanvasInput canvasInput = viewport.gameObject.AddComponent<UpgradeTreeCanvasInput>();
            LayoutUtility.SetReference(canvasInput, "_viewport", viewport);
            LayoutUtility.SetReference(canvasInput, "_content", content);
            UpgradeTreePanel panel = parent.gameObject.AddComponent<UpgradeTreePanel>();
            LayoutUtility.SetReference(panel, "_definition", definition);
            LayoutUtility.SetReference(panel, "_nodePrefab", nodePrefab);
            LayoutUtility.SetReference(panel, "_summary", summary);
            LayoutUtility.SetReference(panel, "_viewport", viewport);
            LayoutUtility.SetReference(panel, "_content", content);
            LayoutUtility.SetReference(panel, "_hoverPanel", hoverPanel);
            LayoutUtility.SetReference(panel, "_hoverTitle", hoverTitle);
            LayoutUtility.SetReference(panel, "_hoverDescription", hoverDescription);
            return panel;
        }

        /// <summary>노드 Prefab이 있으면 그대로 반환하고, 없으면 아이콘 전용 정사각형 버튼으로 만들어 뷰 참조를 연결한다.</summary>
        private static UpgradeTreeNodeView EnsureUpgradeTreeNodePrefab()
        {
            UpgradeTreeNodeView existing = AssetDatabase.LoadAssetAtPath<UpgradeTreeNodeView>(UPGRADE_TREE_NODE_PREFAB_PATH);
            if (existing != null) return existing;
            bool isExistingPrefab = existing != null;
            GameObject source = isExistingPrefab
                ? PrefabUtility.LoadPrefabContents(UPGRADE_TREE_NODE_PREFAB_PATH)
                : new GameObject("UpgradeTreeNode", typeof(RectTransform), typeof(Image));
            source.name = "UpgradeTreeNode";
            for (int childIndex = source.transform.childCount - 1; childIndex >= 0; childIndex--)
                UnityEngine.Object.DestroyImmediate(source.transform.GetChild(childIndex).gameObject);
            LayoutUtility.SetTopLeftRect((RectTransform)source.transform, 0, 0, 76, 76);
            Image background = source.GetComponent<Image>();
            if (background == null) background = source.AddComponent<Image>();
            background.color = UiTheme.Panel;
            background.raycastTarget = true;
            Button button = source.GetComponent<Button>();
            if (button == null) button = source.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            Outline outline = source.GetComponent<Outline>();
            if (outline == null) outline = source.AddComponent<Outline>();
            outline.effectDistance = new Vector2(2, -2);
            outline.useGraphicAlpha = true;

            RectTransform iconRect = UiFactory.Rect(source.transform, "Icon", 0, 0, 42, 42);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            UpgradeTreeNodeIconGraphic icon = iconRect.gameObject.AddComponent<UpgradeTreeNodeIconGraphic>();
            icon.raycastTarget = false;

            UpgradeTreeNodeView view = source.GetComponent<UpgradeTreeNodeView>();
            if (view == null) view = source.AddComponent<UpgradeTreeNodeView>();
            LayoutUtility.SetReference(view, "_background", background);
            LayoutUtility.SetReference(view, "_outline", outline);
            LayoutUtility.SetReference(view, "_icon", icon);
            LayoutUtility.SetReference(view, "_button", button);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, UPGRADE_TREE_NODE_PREFAB_PATH);
            if (isExistingPrefab) PrefabUtility.UnloadPrefabContents(source);
            else UnityEngine.Object.DestroyImmediate(source);
            return prefab.GetComponent<UpgradeTreeNodeView>();
        }

        /// <summary>성장 카드 공통 Prefab이 없을 때 기존 화면의 크기와 타이포그래피로 생성해 자산으로 저장한다.</summary>
        private static UpgradeCardView EnsureUpgradeCardPrefab(UiFactory ui)
        {
            UpgradeCardView existing = AssetDatabase.LoadAssetAtPath<UpgradeCardView>(UPGRADE_CARD_PREFAB_PATH);
            if (existing != null) return existing;

            GameObject source = new GameObject("UpgradeCard", typeof(RectTransform));
            LayoutUtility.SetTopLeftRect((RectTransform)source.transform, 0, 0, 386, 184);
            ui.Panel(source.transform, 0, 0, 386, 184, UiTheme.Panel, "Background");
            TextMeshProUGUI title = ui.Text(source.transform, 18, 14, 350, 31, "", 21, Color.white, FontStyles.Normal, "CardTitle");
            TextMeshProUGUI value = ui.Text(source.transform, 18, 52, 350, 34, "", 28, UiTheme.Cyan, FontStyles.Bold, "CardValue");
            TextMeshProUGUI description = ui.Text(source.transform, 18, 93, 350, 38, "", 12, UiTheme.Muted, FontStyles.Normal, "CardDescription");
            Button buyButton = ui.Button(source.transform, 16, 142, 354, 28, "", null, UiTheme.Muted, 12, "BuyButton");

            UpgradeCardView view = source.AddComponent<UpgradeCardView>();
            LayoutUtility.SetReference(view, "_title", title);
            LayoutUtility.SetReference(view, "_value", value);
            LayoutUtility.SetReference(view, "_description", description);
            LayoutUtility.SetReference(view, "_buyButton", buyButton);
            LayoutUtility.SetReference(view, "_buyButtonImage", buyButton.GetComponent<Image>());
            LayoutUtility.SetReference(view, "_buyLabel", buyButton.GetComponentInChildren<TextMeshProUGUI>());
            return LayoutUtility.SavePrefab(source, "UpgradeCard").GetComponent<UpgradeCardView>();
        }

        /// <summary>시간제 전투 안내, 스테이지 선택과 출격 버튼을 만들고 DeployPanel 참조를 연결한다.</summary>
        private static DeployPanel BuildDeploy(UiFactory ui, RectTransform parent)
        {
            RectTransform card = ui.Panel(parent, 64, 122, 1152, 498, UiTheme.Panel, "DeployCard");
            TextMeshProUGUI title = ui.Text(card, 38, 32, 1070, 46, GameData.L("ui.timedBattle"), 32, Color.white, FontStyles.Bold, "BattleTitle");
            TextMeshProUGUI description = ui.Text(card, 38, 98, 1010, 72, GameData.L("ui.battleDescription"), 20, UiTheme.Muted, FontStyles.Normal, "BattleDescription");
            TextMeshProUGUI highest = ui.Text(card, 38, 202, 480, 30, "", 16, UiTheme.Muted, FontStyles.Normal, "HighestStage");
            TextMeshProUGUI stage = ui.Text(card, 38, 246, 238, 36, "", 28, UiTheme.Cyan, FontStyles.Bold, "Stage");
            Button stageDown = ui.Button(card, 292, 240, 52, 44, "−", null, UiTheme.Muted, 22, "StageDownButton");
            Button stageUp = ui.Button(card, 358, 240, 52, 44, "+", null, UiTheme.Cyan, 22, "StageUpButton");
            TextMeshProUGUI duration = ui.Text(card, 510, 202, 560, 40, "", 27, Color.white, FontStyles.Normal, "Duration");
            TextMeshProUGUI battleMode = ui.Text(card, 510, 253, 560, 35, GameData.L("ui.manualBattle"), 18, UiTheme.Cyan, FontStyles.Normal, "BattleMode");
            TextMeshProUGUI spell = ui.Text(card, 38, 310, 1032, 52, "", 18, Color.white, FontStyles.Normal, "SpellSummary");
            Button launch = ui.Button(card, 38, 386, 320, 60, GameData.L("ui.launch"), null, UiTheme.Cyan, 22, "LaunchButton");
            ui.Text(card, 406, 390, 680, 56, GameData.L("ui.manualBattleControls"), 14, UiTheme.Muted, FontStyles.Normal, "BattleControls");

            DeployPanel deployPanel = parent.gameObject.AddComponent<DeployPanel>();
            LayoutUtility.SetReference(deployPanel, "_titleText", title);
            LayoutUtility.SetReference(deployPanel, "_descriptionText", description);
            LayoutUtility.SetReference(deployPanel, "_battleModeText", battleMode);
            LayoutUtility.SetReference(deployPanel, "_highestText", highest);
            LayoutUtility.SetReference(deployPanel, "_stageText", stage);
            LayoutUtility.SetReference(deployPanel, "_durationText", duration);
            LayoutUtility.SetReference(deployPanel, "_spellText", spell);
            LayoutUtility.SetReference(deployPanel, "_stageDownButton", stageDown);
            LayoutUtility.SetReference(deployPanel, "_stageUpButton", stageUp);
            LayoutUtility.SetReference(deployPanel, "_launchButton", launch);
            return deployPanel;
        }

        /// <summary>피드백 설정, 저장 초기화 버튼과 디버그 도구를 만들고 SettingsPanel 참조를 연결한다. 초기화 확인은 공용 확인 팝업을 쓴다.</summary>
        private static SettingsPanel BuildSettings(UiFactory ui, RectTransform parent)
        {
            ui.Text(parent, 42, 112, 800, 42, GameData.L("ui.settings"), 30, Color.white, FontStyles.Bold, "SettingsTitle");
            Button shakeButton = ui.Button(parent, 42, 194, 446, 48, "", null, UiTheme.Cyan, 19, "ScreenShakeButton");
            Button hitStopButton = ui.Button(parent, 42, 264, 446, 48, "", null, UiTheme.Cyan, 19, "HitStopButton");
            Button resetButton = ui.Button(parent, 42, 420, 446, 48, GameData.L("ui.resetSave"), null, RESET_COLOR, 18, "ResetSaveButton");
            RectTransform debugGroup = UiFactory.Rect(parent, "DebugGroup", 560, 190, 444, 243);
            Button grantButton = ui.Button(debugGroup, 0, 0, 444, 48, GameData.L("ui.grant"), null, UiTheme.Cyan, 14, "DebugGrantButton");
            Button unlockButton = ui.Button(debugGroup, 0, 65, 444, 48, GameData.L("ui.unlockAll"), null, UiTheme.Muted, 14, "DebugUnlockAllButton");
            ui.Button(debugGroup, 0, 130, 444, 48, GameData.L("ui.invulnerable"), null, UiTheme.Muted, 14, "DebugInvulnerableButton");
            ui.Button(debugGroup, 0, 195, 444, 48, GameData.L("ui.spawn"), null, UiTheme.Muted, 14, "DebugSpawnButton");


            SettingsPanel settingsPanel = parent.gameObject.AddComponent<SettingsPanel>();
            LayoutUtility.SetReference(settingsPanel, "_screenShakeButton", shakeButton);
            LayoutUtility.SetReference(settingsPanel, "_screenShakeLabel", shakeButton.GetComponentInChildren<TextMeshProUGUI>());
            LayoutUtility.SetReference(settingsPanel, "_hitStopButton", hitStopButton);
            LayoutUtility.SetReference(settingsPanel, "_hitStopLabel", hitStopButton.GetComponentInChildren<TextMeshProUGUI>());
            LayoutUtility.SetReference(settingsPanel, "_resetSaveButton", resetButton);
            LayoutUtility.SetReference(settingsPanel, "_debugGroup", debugGroup.gameObject);
            LayoutUtility.SetReference(settingsPanel, "_debugGrantButton", grantButton);
            LayoutUtility.SetReference(settingsPanel, "_debugUnlockButton", unlockButton);
            return settingsPanel;
        }
    }
}
