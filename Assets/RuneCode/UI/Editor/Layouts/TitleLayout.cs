using UnityEngine;

using TMPro;
using UnityEditor;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>타이틀 화면 Prefab을 원본 좌표로 생성하고 TitleScreen 참조를 연결한다.</summary>
    public static class TitleLayout
    {
        /// <summary>타이틀 Prefab에 일반·디버그 시작 버튼을 만들거나 기존 Prefab에 빠진 디버그 버튼만 추가한다.</summary>
        public static void Build()
        {
            if (LayoutUtility.ViewPrefabExists(nameof(TitleScreen)))
            {
                EnsureModeChoice();
                return;
            }
            UiFactory ui = LayoutUtility.CreateFactory();
            RectTransform root = LayoutUtility.CreateViewRoot(nameof(TitleScreen));
            RectTransform page = ui.Panel(root, 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT, UiTheme.Background, "Page");
            ui.Panel(page, 72, 72, 1136, 576, UiTheme.Panel, "IntroPanel");
            ui.Panel(page, 110, 116, 6, 430, UiTheme.Cyan, "AccentBar");
            ui.Text(page, 150, 132, 960, 30, GameData.L("ui.introCode"), 16, UiTheme.Cyan, FontStyles.Normal, "IntroCode");
            ui.Text(page, 146, 190, 980, 92, GameData.L("ui.title"), 76, Color.white, FontStyles.Bold, "TitleLabel");
            ui.Text(page, 152, 310, 850, 60, GameData.L("ui.subtitle"), 24, UiTheme.Muted, FontStyles.Normal, "Subtitle");
            ui.Text(page, 152, 400, 920, 34, GameData.L("ui.help"), 25, UiTheme.Cyan, FontStyles.Normal, "Help");
            Button startButton = ui.Button(page, 154, 494, 274, 60, GameData.L("ui.startNormal"), null, UiTheme.Cyan, 21, "StartButton");
            Button debugStartButton = ui.Button(page, 154, 568, 274, 60, GameData.L("ui.startDebug"), null, UiTheme.Muted, 21, "DebugStartButton");
            ui.Text(page, 640, 518, 440, 60, GameData.L("ui.incrementalLoop"), 14, UiTheme.Muted, FontStyles.Normal, "IncrementalLoop");
            TitleScreen screen = root.gameObject.AddComponent<TitleScreen>();
            LayoutUtility.SetReference(screen, "_startButton", startButton);
            LayoutUtility.SetReference(screen, "_debugStartButton", debugStartButton);
            LayoutUtility.SaveViewPrefab(root);
        }

        /// <summary>기존 TitleScreen Prefab의 Page와 시작 버튼을 확인해 디버그 버튼과 참조만 보충한다.</summary>
        private static void EnsureModeChoice()
        {
            GameData.Load();
            string path = LayoutUtility.VIEW_FOLDER + nameof(TitleScreen) + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                TitleScreen screen = root.GetComponent<TitleScreen>();
                Transform page = root.transform.Find("Page");
                Button startButton = page?.Find("StartButton")?.GetComponent<Button>();
                if (screen == null || page == null || startButton == null)
                    throw new System.InvalidOperationException("TitleScreen Prefab의 Page 또는 StartButton을 찾을 수 없습니다.");

                bool hasChanged = false;
                TextMeshProUGUI startLabel = startButton.GetComponentInChildren<TextMeshProUGUI>();
                if (startLabel != null && startLabel.text == GameData.L("ui.start"))
                {
                    startLabel.text = GameData.L("ui.startNormal");
                    hasChanged = true;
                }
                Button debugStartButton = page.Find("DebugStartButton")?.GetComponent<Button>();
                if (debugStartButton == null)
                {
                    UiFactory ui = LayoutUtility.CreateFactory();
                    debugStartButton = ui.Button(page, 154, 568, 274, 60, GameData.L("ui.startDebug"), null,
                        UiTheme.Muted, 21, "DebugStartButton");
                    hasChanged = true;
                }
                SerializedObject serialized = new SerializedObject(screen);
                if (serialized.FindProperty("_debugStartButton").objectReferenceValue != debugStartButton)
                {
                    LayoutUtility.SetReference(screen, "_debugStartButton", debugStartButton);
                    hasChanged = true;
                }
                if (hasChanged) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
