using UnityEngine;

using TMPro;
using UnityEditor;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>작업실 에디터 탭의 비주얼 스크립팅 편집 패널, 내부 모달과 목록 행 Prefab을 원본 좌표·색으로 만들고 직렬화 참조를 연결하는 빌더다.</summary>
    public static class SpellEditorLayout
    {
        /// <summary>parent 아래에 이미지 없는 1280×720 SpellEditorPanel과 편집 영역 UI, RuneGraphCanvas 뷰포트, 비활성 모달, 행 Prefab을 만들고 모든 직렬화 참조를 연결한 뒤 패널을 반환한다.</summary>
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

            UiRow runeRowPrefab = LayoutUtility.GetSharedRowPrefab(ui);
            UiRow quickPaletteRowPrefab = runeRowPrefab;
            UiRow issueRowPrefab = runeRowPrefab;
            UiRow libraryRowPrefab = runeRowPrefab;
            SpellParameterRow parameterRowPrefab = EnsureParameterRowPrefab(ui);

            RectTransform quickCard = BuildModal(ui, root, "QuickPaletteModal", GameData.L("ui.search"), 550, 470,
                out RectTransform quickShade, out Button quickClose);
            TMP_InputField quickSearch = ui.Input(quickCard, 24, 80, 502, 38, "", GameData.L("ui.search"));
            RectTransform quickList = UiFactory.ScrollList(quickCard, "QuickPalette", 24, 138, 502, 300);

            RectTransform libraryCard = BuildModal(ui, root, "SpellLibraryModal", GameData.L("ui.library"), 620, 500,
                out RectTransform libraryShade, out Button libraryClose);
            RectTransform libraryList = UiFactory.ScrollList(libraryCard, "SpellLibrary", 24, 74, 572, 340);
            Button newSpellButton = ui.Button(libraryCard, 24, 430, 240, 42, GameData.L("ui.new"), null, UiTheme.Cyan, 13);
            Button duplicateSpellButton = ui.Button(libraryCard, 278, 430, 240, 42, GameData.L("ui.duplicate"), null, UiTheme.Muted, 13);

            RectTransform pickerCard = BuildModal(ui, root, "SpellPickerModal", GameData.L("ui.selectSpell"), 620, 500,
                out RectTransform pickerShade, out Button pickerClose);
            RectTransform pickerList = UiFactory.ScrollList(pickerCard, "SpellCallTargets", 24, 74, 572, 390);

            RectTransform tutorialCard = BuildModal(ui, root, "TutorialModal", GameData.L("ui.tutorial"), 660, 380,
                out RectTransform tutorialShade, out Button tutorialClose);
            ui.Text(tutorialCard, 30, 92, 600, 68, GameData.L("ui.tutorial1"), 18, Color.white);
            ui.Text(tutorialCard, 30, 172, 600, 68, GameData.L("ui.tutorial2"), 18, Color.white);
            ui.Text(tutorialCard, 30, 252, 600, 68, GameData.L("ui.tutorial3"), 18, UiTheme.Cyan);

            RectTransform renameCard = BuildModal(ui, root, "RenameModal", GameData.L("ui.rename"), 560, 250,
                out RectTransform renameShade, out Button renameClose);
            TMP_InputField renameField = ui.Input(renameCard, 28, 88, 504, 42, "", "");
            Button renameSaveButton = ui.Button(renameCard, 352, 164, 180, 40, GameData.L("ui.save"), null, UiTheme.Cyan);

            RectTransform shareCard = BuildModal(ui, root, "ShareModal", GameData.L("ui.share"), 770, 400,
                out RectTransform shareShade, out Button shareClose);
            ui.Text(shareCard, 26, 74, 718, 40, GameData.L("ui.importCurrent"), 12, UiTheme.Muted);
            TMP_InputField shareField = ui.Input(shareCard, 26, 122, 718, 168, "", "RC1.");
            shareField.lineType = TMP_InputField.LineType.MultiLineNewline;
            Button shareExportButton = ui.Button(shareCard, 26, 322, 180, 42, GameData.L("ui.export"), null, UiTheme.Cyan);
            Button sharePasteButton = ui.Button(shareCard, 220, 322, 180, 42, GameData.L("ui.paste"), null, UiTheme.Muted);
            Button shareImportButton = ui.Button(shareCard, 564, 322, 180, 42, GameData.L("ui.import"), null, UiTheme.Cyan);

            LayoutUtility.SetReference(panel, "_spellTitle", spellTitle);
            LayoutUtility.SetReference(panel, "_libraryButton", libraryButton);
            LayoutUtility.SetReference(panel, "_renameButton", renameButton);
            LayoutUtility.SetReference(panel, "_saveButton", saveButton);
            LayoutUtility.SetReference(panel, "_shareButton", shareButton);
            LayoutUtility.SetReference(panel, "_metricsText", metrics);
            LayoutUtility.SetReference(panel, "_searchField", search);
            LayoutUtility.SetReferences(panel, "_categoryButtons", categoryButtons);
            LayoutUtility.SetReference(panel, "_runeList", runeList);
            LayoutUtility.SetReference(panel, "_runeRowPrefab", runeRowPrefab);
            LayoutUtility.SetReference(panel, "_graphCanvas", graphCanvas);
            LayoutUtility.SetReference(panel, "_inspectorPanel", inspector);
            LayoutUtility.SetReference(panel, "_parameterRowPrefab", parameterRowPrefab);
            LayoutUtility.SetReference(panel, "_undoButton", undoButton);
            LayoutUtility.SetReference(panel, "_redoButton", redoButton);
            LayoutUtility.SetReference(panel, "_arrangeButton", arrangeButton);
            LayoutUtility.SetReference(panel, "_copyButton", copyButton);
            LayoutUtility.SetReference(panel, "_pasteButton", pasteButton);
            LayoutUtility.SetReference(panel, "_tooltipText", tooltip);
            LayoutUtility.SetReference(panel, "_tutorialText", tutorial);
            LayoutUtility.SetReference(panel, "_tutorialButton", tutorialButton);
            LayoutUtility.SetReference(panel, "_quickPaletteModal", quickShade);
            LayoutUtility.SetReference(panel, "_quickPaletteCloseButton", quickClose);
            LayoutUtility.SetReference(panel, "_quickPaletteSearch", quickSearch);
            LayoutUtility.SetReference(panel, "_quickPaletteList", quickList);
            LayoutUtility.SetReference(panel, "_quickPaletteRowPrefab", quickPaletteRowPrefab);
            LayoutUtility.SetReference(panel, "_issueRowPrefab", issueRowPrefab);
            LayoutUtility.SetReference(panel, "_libraryModal", libraryShade);
            LayoutUtility.SetReference(panel, "_libraryCloseButton", libraryClose);
            LayoutUtility.SetReference(panel, "_libraryList", libraryList);
            LayoutUtility.SetReference(panel, "_newSpellButton", newSpellButton);
            LayoutUtility.SetReference(panel, "_duplicateSpellButton", duplicateSpellButton);
            LayoutUtility.SetReference(panel, "_libraryRowPrefab", libraryRowPrefab);
            LayoutUtility.SetReference(panel, "_spellPickerModal", pickerShade);
            LayoutUtility.SetReference(panel, "_spellPickerCloseButton", pickerClose);
            LayoutUtility.SetReference(panel, "_spellPickerList", pickerList);
            LayoutUtility.SetReference(panel, "_tutorialModal", tutorialShade);
            LayoutUtility.SetReference(panel, "_tutorialCloseButton", tutorialClose);
            LayoutUtility.SetReference(panel, "_renameModal", renameShade);
            LayoutUtility.SetReference(panel, "_renameCloseButton", renameClose);
            LayoutUtility.SetReference(panel, "_renameField", renameField);
            LayoutUtility.SetReference(panel, "_renameSaveButton", renameSaveButton);
            LayoutUtility.SetReference(panel, "_shareModal", shareShade);
            LayoutUtility.SetReference(panel, "_shareCloseButton", shareClose);
            LayoutUtility.SetReference(panel, "_shareField", shareField);
            LayoutUtility.SetReference(panel, "_shareExportButton", shareExportButton);
            LayoutUtility.SetReference(panel, "_sharePasteButton", sharePasteButton);
            LayoutUtility.SetReference(panel, "_shareImportButton", shareImportButton);
            LayoutUtility.SetReference(panel, "_font", ui.Font);
            LayoutUtility.SetReference(panel, "_panelPrefab", ui.PanelPrefab);
            LayoutUtility.SetReference(panel, "_buttonPrefab", ui.ButtonPrefab);
            return panel;
        }

        /// <summary>입력 뒤의 화면을 가리는 비활성 모달 그늘과 카드, 제목, 닫기 버튼을 만들고 카드를 반환한다.</summary>
        private static RectTransform BuildModal(UiFactory ui, Transform parent, string modalName, string title, float width, float height,
            out RectTransform shade, out Button closeButton)
        {
            shade = ui.Panel(parent, 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT, UiTheme.ModalShade, modalName);
            shade.GetComponent<Image>().raycastTarget = true;
            RectTransform card = ui.Panel(shade, (UiTheme.SCREEN_WIDTH - width) / 2, (UiTheme.SCREEN_HEIGHT - height) / 2, width, height, UiTheme.Panel, "Card");
            ui.Text(card, 24, 21, width - 90, 35, title, 24, Color.white, FontStyles.Bold);
            closeButton = ui.Button(card, width - 60, 20, 36, 32, "×", null, UiTheme.Muted, 23);
            shade.gameObject.SetActive(false);
            return card;
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
