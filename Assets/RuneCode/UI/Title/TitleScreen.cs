using UnityEngine;

using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>타이틀 화면의 시작 요청을 세션 화면 전환으로 전달한다.</summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [Header("화면 구성")]
        [SerializeField] private Button _startButton;

        private RuneCodeSession _session;

        /// <summary>세션을 받아 시작 버튼 클릭을 작업실 전환 요청으로 연결한다.</summary>
        public void Initialize(RuneCodeSession session)
        {
            _session = session;
            _startButton.onClick.AddListener(RequestWorkshop);
        }

        /// <summary>세션에 작업실 화면 전환을 요청한다.</summary>
        private void RequestWorkshop()
        {
            _session.RequestScreen(AppScreen.Workshop);
        }
    }
}
