namespace RuneCode
{
    /// <summary>
    /// UIManager가 관리하는 표시 층이다. 층마다 별도 Overlay Canvas를 쓰므로 위 층은 아래 층의 계층 순서와 무관하게 항상 위에 그려진다.
    /// </summary>
    public enum UiLayer
    {
        Screen,
        Popup,
        System
    }
}
