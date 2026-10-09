using UnityEngine;

using TMPro;
using UnityEditor;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 작업실 에디터 탭의 비주얼 스크립팅 편집 패널과 편집기 팝업(빠른 검색·마법 목록·튜토리얼·이름 변경·공유) Prefab을
    /// 원본 좌표·색으로 만들고 직렬화 참조를 연결하는 빌더다. 이미 있는 팝업 Prefab은 덮어쓰지 않는다.
    /// </summary>
    public static class SpellEditorLayout
    {
        /// <summary>parent 아래에 1280×720 SpellEditorPanel과 편집 영역 UI, RuneGraphCanvas 뷰포트를 만들고 직렬화 참조를 연결한 뒤 패널을 반환한다.</summary>
        public static SpellEditorPanel Build(UiFactory ui, Transform parent)
        {
            GameData.Load();
            RectTransform root = UiFactory.Rect(parent, "SpellEditorPanel", 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT);
            SpellEditorPanel panel = root.gameObject.AddComponent<SpellEditorPanel>();

            ui.Text(root, 18, 91, 190, 30, GameData.L("ui.singleSpell"), 15, UiTheme.Cyan);
            TextMeshProUGUI spellTitle = ui.Text(root, 222, 89, 300, 33, "", 22, Color.white, FontStyles.Bold);
            Button libraryButton = ui.Button(root, 528, 86, 88, 34, GameData.L("ui.library"), null, UiTheme.Muted, 12);
            Button renameButton = ui.Button(root, 628, 86, 116, 34, GameData.L("ui.rename"), null, UiTheme.Muted, 13);
            Button saveButton = ui.Button(root, 756, 86, 92, 34, GameData.L("ui.save"), null, UiTheme.Cyan, 13);
            Button shareButton = ui.Button(root, 862, 86, 132, 34, GameData.L("ui.share"), null, UiTheme.Muted, 13);
            TextMeshProUGUI metrics = ui.Text(root, 224, 129, 766, 23, "", 12, UiTheme.Cyan);

            RectTransform palette = ui.Panel(root, 16, 136, 190, 526, UiTheme.Panel, "Palette");
            ui.Text(palette, 12, 12, 166, 26, GameData.L("ui.palette"), 17, Color.white, FontStyles.Bold);
            TMP_InputField search = ui.Input(palette, 10, 47, 170, 32, "", GameData.L("ui.search"));
            Button[] categoryButtons = new Button[SpellEditorPanel.CATEGORY_IDS.Length];
            for (int i = 0; i < SpellEditorPanel.CATEGORY_IDS.Length; i++)
            {
                string category = SpellEditorPanel.CATEGORY_IDS[i];
                categoryButtons[i] = ui.Button(palette, 10 + i % 3 * 58, 87 + i / 3 * 29, 54, 25,
                    category == "all" ? GameData.L("ui.all") : GameData.L("category." + category), null, UiTheme.Muted, 10);
            }
            RectTransform runeList = UiFactory.ScrollList(palette, "RuneList", 8, 177, 174, 338);

            RectTransform graphViewport = UiFactory.Rect(root, "GraphViewport", 222, 162, 772, 306);
            graphViewport.gameObject.AddComponent<RectMask2D>();
            RectTransform graphSurface = UiFactory.Rect(graphViewport, "GraphCanvas", 0, 0, 772, 306);
            RuneGraphCanvas graphCanvas = graphSurface.gameObject.AddComponent<RuneGraphCanvas>();

            RectTransform inspector = ui.Panel(root, 1010, 136, 254, 526, UiTheme.Panel, "Inspector");

            Button undoButton = ui.Button(root, 222, 476, 72, 27, GameData.L("ui.undo"), null, UiTheme.Muted, 11);
            Button redoButton = ui.Button(root, 300, 476, 72, 27, GameData.L("ui.redo"), null, UiTheme.Muted, 11);
            Button arrangeButton = ui.Button(root, 378, 476, 84, 27, GameData.L("ui.arrange"), null, UiTheme.Muted, 11);
            Button copyButton = ui.Button(root, 468, 476, 58, 27, GameData.L("ui.copy"), null, UiTheme.Muted, 11);
            Button pasteButton = ui.Button(root, 532, 476, 78, 27, GameData.L("ui.paste"), null, UiTheme.Muted, 11);
            TextMeshProUGUI tooltip = ui.Text(root, 619, 476, 372, 28, GameData.L("ui.executionOrder"), 11, UiTheme.Muted);
            TextMeshProUGUI tutorial = ui.Text(root, 222, 662, 1026, 22, "", 12, UiTheme.Cyan);
            Button tutorialButton = ui.Button(root, 16, 666, 190, 19, GameData.L("ui.tutorial"), null, UiTheme.Cyan, 11);

            UiRow rowPrefab = LayoutUtility.GetSharedRowPrefab(ui);
            LayoutUtility.SetReference(panel, "_spellTitle", spellTitle);
            LayoutUtility.SetReference(panel, "_libraryButton", libraryButton);
            LayoutUtility.SetReference(panel, "_renameButton", renameButton);
            LayoutUtility.SetReference(panel, "_saveButton", saveButton);
            LayoutUtility.SetReference(panel, "_shareButton", shareButton);
            LayoutUtility.SetReference(panel, "_metricsText", metrics);
            LayoutUtility.SetReference(panel, "_searchField", search);
            LayoutUtility.SetReferences(panel, "_categoryButtons", categoryButtons);
            LayoutUtility.SetReference(panel, "_runeList", runeList);
            LayoutUtility.SetReference(panel, "_runeRowPrefab", rowPrefab);
            LayoutUtility.SetReference(panel, "_graphCanvas", graphCanvas);
            LayoutUtility.SetReference(panel, "_inspectorPanel", inspector);
            LayoutUtility.SetReference(panel, "_parameterRowPrefab", EnsureParameterRowPrefab(ui));
            LayoutUtility.SetReference(panel, "_issueRowPrefab", rowPrefab);
            LayoutUtility.SetReference(panel, "_undoButton", undoButton);
            LayoutUtility.SetReference(panel, "_redoButton", redoButton);
            LayoutUtility.SetReference(panel, "_arrangeButton", arrangeButton);
            LayoutUtility.SetReference(panel, "_copyButton", copyButton);
            LayoutUtility.SetReference(panel, "_pasteButton", pasteButton);
            LayoutUtility.SetReference(panel, "_tooltipText", tooltip);
            LayoutUtility.SetReference(panel, "_tutorialText", tutorial);
            LayoutUtility.SetReference(panel, "_tutorialButton", tutorialButton);
            LayoutUtility.SetReference(panel, "_font", ui.Font);
            LayoutUtility.SetReference(panel, "_panelPrefab", ui.PanelPrefab);
            LayoutUtility.SetReference(panel, "_buttonPrefab", ui.ButtonPrefab);
            return panel;
        }

        /// <summary>편집기 팝업 Prefab 중 없는 것만 만든다.</summary>
        public static void BuildPopups()
        {
            UiFactory ui = LayoutUtility.CreateFactory();
            BuildQuickPalettePopup(ui);
            BuildSpellListPopup(ui);
            BuildTutorialPopup(ui);
            BuildRenamePopup(ui);
            BuildSharePopup(ui);
        }

        /// <summary>검색 입력과 결과 목록의 빠른 룬 검색 팝업을 만들고 QuickPalettePopup Prefab으로 저장한다.</summary>
        private static void BuildQuickPalettePopup(UiFactory ui)
        {
            if (LayoutUtility.ViewPrefabExists(nameof(QuickPalettePopup))) return;
            RectTransform root = LayoutUtility.CreateViewRoot(nameof(QuickPalettePopup));
            RectTransform card = LayoutUtility.BuildPopupFrame(ui, root, GameData.L("ui.search"), 550, 470, out Button closeButton);
            TMP_InputField search = ui.Input(card, 24, 80, 502, 38, "", GameData.L("ui.search"));
            RectTransform list = UiFactory.ScrollList(card, "QuickPalette", 24, 138, 502, 300);
            QuickPalettePopup popup = root.gameObject.AddComponent<QuickPalettePopup>();
            LayoutUtility.SetReference(popup, "_closeButton", closeButton);
            LayoutUtility.SetReference(popup, "_search", search);
            LayoutUtility.SetReference(popup, "_list", list);
            LayoutUtility.SetReference(popup, "_rowPrefab", LayoutUtility.GetSharedRowPrefab(ui));
            LayoutUtility.SaveViewPrefab(root);
        }

        /// <summary>마법 목록과 새로 만들기·복제 버튼의 팝업을 만들고 SpellListPopup Prefab으로 저장한다(보관함·호출 대상 선택 공용).</summary>
        private static void BuildSpellListPopup(UiFactory ui)
        {
            if (LayoutUtility.ViewPrefabExists(nameof(SpellListPopup))) return;
            RectTransform root = LayoutUtility.CreateViewRoot(nameof(SpellListPopup));
            RectTransform card = LayoutUtility.BuildPopupFrame(ui, root, GameData.L("ui.library"), 620, 500, out Button closeButton);
            RectTransform list = UiFactory.ScrollList(card, "SpellList", 24, 74, 572, 340);
            Button newButton = ui.Button(card, 24, 430, 240, 42, GameData.L("ui.new"), null, UiTheme.Cyan, 13, "NewSpellButton");
            Button duplicateButton = ui.Button(card, 278, 430, 240, 42, GameData.L("ui.duplicate"), null, UiTheme.Muted, 13, "DuplicateSpellButton");
            SpellListPopup popup = root.gameObject.AddComponent<SpellListPopup>();
            LayoutUtility.SetReference(popup, "_closeButton", closeButton);
            LayoutUtility.SetReference(popup, "_title", card.Find("Title").GetComponent<TextMeshProUGUI>());
            LayoutUtility.SetReference(popup, "_list", list);
            LayoutUtility.SetReference(popup, "_rowPrefab", LayoutUtility.GetSharedRowPrefab(ui));
            LayoutUtility.SetReference(popup, "_newButton", newButton);
            LayoutUtility.SetReference(popup, "_duplicateButton", duplicateButton);
            LayoutUtility.SaveViewPrefab(root);
        }

        /// <summary>3단계 안내 문구의 튜토리얼 팝업을 만들고 TutorialPopup Prefab으로 저장한다.</summary>
        private static void BuildTutorialPopup(UiFactory ui)
        {
            if (LayoutUtility.ViewPrefabExists(nameof(TutorialPopup))) return;
            RectTransform root = LayoutUtility.CreateViewRoot(nameof(TutorialPopup));
            RectTransform card = LayoutUtility.BuildPopupFrame(ui, root, GameData.L("ui.tutorial"), 660, 380, out Button closeButton);
            ui.Text(card, 30, 92, 600, 68, GameData.L("ui.tutorial1"), 18, Color.white);
            ui.Text(card, 30, 172, 600, 68, GameData.L("ui.tutorial2"), 18, Color.white);
            ui.Text(card, 30, 252, 600, 68, GameData.L("ui.tutorial3"), 18, UiTheme.Cyan);
            TutorialPopup popup = root.gameObject.AddComponent<TutorialPopup>();
            LayoutUtility.SetReference(popup, "_closeButton", closeButton);
            LayoutUtility.SaveViewPrefab(root);
        }

        /// <summary>이름 입력과 저장 버튼의 이름 변경 팝업을 만들고 RenamePopup Prefab으로 저장한다.</summary>
        private static void BuildRenamePopup(UiFactory ui)
        {
            if (LayoutUtility.ViewPrefabExists(nameof(RenamePopup))) return;
            RectTransform root = LayoutUtility.CreateViewRoot(nameof(RenamePopup));
            RectTransform card = LayoutUtility.BuildPopupFrame(ui, root, GameData.L("ui.rename"), 560, 250, out Button closeButton);
            TMP_InputField nameField = ui.Input(card, 28, 88, 504, 42, "", "");
            Button saveButton = ui.Button(card, 352, 164, 180, 40, GameData.L("ui.save"), null, UiTheme.Cyan, 14, "SaveButton");
            RenamePopup popup = root.gameObject.AddComponent<RenamePopup>();
            LayoutUtility.SetReference(popup, "_closeButton", closeButton);
            LayoutUtility.SetReference(popup, "_nameField", nameField);
            LayoutUtility.SetReference(popup, "_saveButton", saveButton);
            LayoutUtility.SaveViewPrefab(root);
        }

        /// <summary>공유 코드 입력과 복사·붙여넣기·가져오기 버튼의 공유 팝업을 만들고 SharePopup Prefab으로 저장한다.</summary>
        private static void BuildSharePopup(UiFactory ui)
        {
            if (LayoutUtility.ViewPrefabExists(nameof(SharePopup))) return;
            RectTransform root = LayoutUtility.CreateViewRoot(nameof(SharePopup));
            RectTransform card = LayoutUtility.BuildPopupFrame(ui, root, GameData.L("ui.share"), 770, 400, out Button closeButton);
            ui.Text(card, 26, 74, 718, 40, GameData.L("ui.importCurrent"), 12, UiTheme.Muted);
            TMP_InputField codeField = ui.Input(card, 26, 122, 718, 168, "", "RC1.");
            codeField.lineType = TMP_InputField.LineType.MultiLineNewline;
            Button exportButton = ui.Button(card, 26, 322, 180, 42, GameData.L("ui.export"), null, UiTheme.Cyan, 14, "ExportButton");
            Button pasteButton = ui.Button(card, 220, 322, 180, 42, GameData.L("ui.paste"), null, UiTheme.Muted, 14, "PasteButton");
            Button importButton = ui.Button(card, 564, 322, 180, 42, GameData.L("ui.import"), null, UiTheme.Cyan, 14, "ImportButton");
            SharePopup popup = root.gameObject.AddComponent<SharePopup>();
            LayoutUtility.SetReference(popup, "_closeButton", closeButton);
            LayoutUtility.SetReference(popup, "_codeField", codeField);
            LayoutUtility.SetReference(popup, "_exportButton", exportButton);
            LayoutUtility.SetReference(popup, "_pasteButton", pasteButton);
            LayoutUtility.SetReference(popup, "_importButton", importButton);
            LayoutUtility.SaveViewPrefab(root);
        }

        /// <summary>기존 인스펙터 파라미터 행 Prefab을 보존해 반환하고 없을 때만 기본 구조로 생성한다.</summary>
        private static SpellParameterRow EnsureParameterRowPrefab(UiFactory ui)
        {
            SpellParameterRow existing = AssetDatabase.LoadAssetAtPath<SpellParameterRow>(LayoutUtility.PREFAB_FOLDER + "/SpellParameterRow.prefab");
            if (existing != null) return existing;

            RectTransform rect = UiFactory.Rect(null, "SpellParameterRow", 0, 0, 224, 64);
            SpellParameterRow row = rect.gameObject.AddComponent<SpellParameterRow>();
            TextMeshProUGUI label = ui.Text(rect, 0, 0, 224, 20, "", 12, UiTheme.Muted);
            TMP_InputField input = ui.Input(rect, 0, 24, 222, 30, "", "");
            Button minusButton = ui.Button(rect, 0, 24, 34, 30, "−", null, UiTheme.Muted, 18);
            Button plusButton = ui.Button(rect, 182, 24, 40, 30, "+", null, UiTheme.Cyan, 18);
            Button optionButton = ui.Button(rect, 0, 24, 222, 30, "", null, UiTheme.Muted, 12);
            Button spellButton = ui.Button(rect, 0, 24, 222, 42, "", null, UiTheme.Muted, 11);
            LayoutUtility.SetReference(row, "_label", label);
            LayoutUtility.SetReference(row, "_input", input);
            LayoutUtility.SetReference(row, "_minusButton", minusButton);
            LayoutUtility.SetReference(row, "_plusButton", plusButton);
            LayoutUtility.SetReference(row, "_optionButton", optionButton);
            LayoutUtility.SetReference(row, "_optionLabel", optionButton.GetComponentInChildren<TextMeshProUGUI>());
            LayoutUtility.SetReference(row, "_spellButton", spellButton);
            LayoutUtility.SetReference(row, "_spellLabel", spellButton.GetComponentInChildren<TextMeshProUGUI>());
            GameObject prefab = LayoutUtility.SavePrefab(rect.gameObject, "SpellParameterRow");
            return prefab.GetComponent<SpellParameterRow>();
        }
    }
}
