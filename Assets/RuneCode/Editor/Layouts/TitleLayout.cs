using UnityEngine;

using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>Title 씬의 타이틀 UI를 원본 좌표로 생성하고 TitleScreen 참조를 연결한다.</summary>
    public static class TitleLayout
    {
        /// <summary>타이틀 캔버스와 시작 버튼을 만들고 화면 컴포넌트 참조를 연결해 Title 씬으로 저장한다.</summary>
        public static void BuildScene()
        {
            UiFactory ui = LayoutUtility.CreateFactory();
            Scene scene = LayoutUtility.CreateEmptyScene();
            GameObject root = new GameObject("Title");
            RectTransform canvas = UiFactory.CreateCanvas(root.transform, "TitleCanvas");
            RectTransform page = UiFactory.Panel(canvas, 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT, UiTheme.Background, "Page");
            UiFactory.Panel(page, 72, 72, 1136, 576, UiTheme.Panel, "IntroPanel");
            UiFactory.Panel(page, 110, 116, 6, 430, UiTheme.Cyan, "AccentBar");
            ui.Text(page, 150, 132, 960, 30, GameData.L("ui.introCode"), 16, UiTheme.Cyan, FontStyles.Normal, "IntroCode");
            ui.Text(page, 146, 190, 980, 92, GameData.L("ui.title"), 76, Color.white, FontStyles.Bold, "TitleLabel");
            ui.Text(page, 152, 310, 850, 60, GameData.L("ui.subtitle"), 24, UiTheme.Muted, FontStyles.Normal, "Subtitle");
            ui.Text(page, 152, 400, 920, 34, GameData.L("ui.help"), 25, UiTheme.Cyan, FontStyles.Normal, "Help");
            Button startButton = ui.Button(page, 154, 494, 274, 60, GameData.L("ui.start"), null, UiTheme.Cyan, 21, "StartButton");
            ui.Text(page, 640, 518, 440, 60, GameData.L("ui.incrementalLoop"), 14, UiTheme.Muted, FontStyles.Normal, "IncrementalLoop");
            TitleScreen screen = root.AddComponent<TitleScreen>();
            LayoutUtility.SetReference(screen, "_startButton", startButton);
            LayoutUtility.SaveScene(scene, "Title");
        }
    }
}
