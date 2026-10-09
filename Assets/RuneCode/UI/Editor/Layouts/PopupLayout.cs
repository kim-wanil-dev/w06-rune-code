using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>UIManager가 Popup 층에 띄우는 공용 팝업 Prefab을 원본 모달 스타일로 생성하고 참조를 연결하는 빌더다.</summary>
    public static class PopupLayout
    {
        private const float CONFIRM_WIDTH = 540;
        private const float CONFIRM_HEIGHT = 260;

        /// <summary>공용 팝업 Prefab을 모두 다시 만든다.</summary>
        public static void Build()
        {
            UiFactory ui = LayoutUtility.CreateFactory();
            BuildConfirmPopup(ui);
        }

        /// <summary>제목·내용, 확인·취소 버튼의 공용 확인 팝업을 만들고 ConfirmPopup Prefab으로 저장한다.</summary>
        private static void BuildConfirmPopup(UiFactory ui)
        {
            RectTransform root = LayoutUtility.CreateViewRoot(nameof(ConfirmPopup));
            RectTransform card = LayoutUtility.BuildPopupFrame(ui, root, "", CONFIRM_WIDTH, CONFIRM_HEIGHT, out Button closeButton);
            TextMeshProUGUI title = card.Find("Title").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI message = ui.Text(card, 28, 72, CONFIRM_WIDTH - 56, 70, "", 16, UiTheme.Muted, FontStyles.Normal, "Message");
            Button confirmButton = ui.Button(card, 28, 170, 232, 60, "", null, UiTheme.Cyan, 14, "ConfirmButton");
            Button cancelButton = ui.Button(card, 280, 170, 232, 60, GameData.L("ui.close"), null, UiTheme.Muted, 14, "CancelButton");

            ConfirmPopup popup = root.gameObject.AddComponent<ConfirmPopup>();
            LayoutUtility.SetReference(popup, "_closeButton", closeButton);
            LayoutUtility.SetReference(popup, "_title", title);
            LayoutUtility.SetReference(popup, "_message", message);
            LayoutUtility.SetReference(popup, "_confirmButton", confirmButton);
            LayoutUtility.SetReference(popup, "_confirmLabel", confirmButton.GetComponentInChildren<TextMeshProUGUI>());
            LayoutUtility.SetReference(popup, "_cancelButton", cancelButton);
            LayoutUtility.SaveViewPrefab(root);
        }
    }
}
