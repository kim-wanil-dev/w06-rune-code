using UnityEngine;

using UnityEditor;

namespace RuneCode
{
    /// <summary>
    /// 에디터에서 Play를 시작할 때 UIManager가 불러올 화면·팝업 Prefab(Resources/RuneCode/UI)이 하나라도 없으면
    /// 레이아웃 빌더로 먼저 생성한다. 씬은 열거나 바꾸지 않는다. 전체 재생성은 Rune Code &gt; Build UI로 한다.
    /// </summary>
    [InitializeOnLoad]
    public static class UiPrefabGuard
    {
        private static readonly string[] REQUIRED_VIEWS =
        {
            nameof(ConfirmPopup), nameof(TitleScreen), nameof(WorkshopScreen), nameof(MissionScreen), nameof(PausePopup), nameof(MissionDebugPopup)
        };

        static UiPrefabGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>Play 진입 직전에 빠진 View Prefab이 있으면 화면·팝업 Prefab을 생성한다.</summary>
        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode || !HasMissingView()) return;
            Debug.Log("[UI] 빠진 UI Prefab이 있어 Resources/RuneCode/UI에 생성합니다.");
            RuneCodeBuild.BuildViewPrefabs();
        }

        /// <summary>필수 View Prefab 중 하나라도 없으면 true를 반환한다.</summary>
        private static bool HasMissingView()
        {
            foreach (string view in REQUIRED_VIEWS)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(LayoutUtility.VIEW_FOLDER + view + ".prefab") == null) return true;
            }
            return false;
        }
    }
}
