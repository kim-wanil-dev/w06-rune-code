using UnityEngine;

namespace RuneCode
{
    /// <summary>모든 화면이 공유하는 기준 해상도와 색상 값이다.</summary>
    public static class UiTheme
    {
        public const float SCREEN_WIDTH = 1280;
        public const float SCREEN_HEIGHT = 720;

        public static readonly Color Background = new Color(0.025f, 0.045f, 0.075f);
        public static readonly Color Panel = new Color(0.065f, 0.10f, 0.155f);
        public static readonly Color Muted = new Color(0.54f, 0.67f, 0.77f);
        public static readonly Color Cyan = new Color(0.24f, 0.84f, 0.94f);
        public static readonly Color InputBackground = new Color(0.027f, 0.052f, 0.09f);
        public static readonly Color ModalShade = new Color(0, 0.015f, 0.035f, 0.88f);
    }
}
