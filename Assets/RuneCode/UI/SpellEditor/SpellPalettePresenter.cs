using System;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 룬 팔레트와 빠른 검색 팝업의 Presenter다. 분류·검색어로 배치 가능한 룬을 골라 표시하고,
    /// 행을 누르면 제안 위치(팔레트) 또는 더블클릭 위치(빠른 검색)에 룬을 배치한다. 잠긴 룬은 해금 조건을 보여 주고 배치하지 않는다.
    /// </summary>
    public sealed class SpellPalettePresenter
    {
        private const float UNLOCKED_ROW_HEIGHT = 40f;
        private const float LOCKED_ROW_HEIGHT = 53f;

        private static readonly Color LOCKED_TINT = new Color(0.35f, 0.42f, 0.50f);

        private readonly ISpellEditor _editor;
        private readonly UIManager _ui;
        private readonly SpellEditorPanel _view;
        private string _category = "all";

        /// <summary>편집 세션·UI 관리자·편집 View를 받아 검색어·분류 이벤트를 연결한다.</summary>
        public SpellPalettePresenter(ISpellEditor editor, UIManager ui, SpellEditorPanel view)
        {
            _editor = editor;
            _ui = ui;
            _view = view;
            _view.SearchChanged += _ => Refresh();
            _view.CategoryClicked += category =>
            {
                _category = category;
                Refresh();
            };
        }

        /// <summary>현재 분류·검색어에 맞는 팔레트 룬 행을 다시 채운다.</summary>
        public void Refresh()
        {
            _view.ClearPalette();
            string query = _view.SearchQuery;
            foreach (RuneDefinition rune in GameData.Runes.All)
            {
                if (!IsPaletteRune(rune) || (_category != "all" && rune.Category != _category) || !MatchesSearch(rune, query)) continue;
                bool isUnlocked = _editor.IsRuneUnlocked(rune.Id);
                string runeId = rune.Id;
                string label = rune.Name + "   " + rune.Ram + " RAM";
                if (!isUnlocked) label += "\n" + GameData.L("ui.locked") + " · " + UnlockCondition(rune);
                _view.AddPaletteRow(label, isUnlocked ? RuneMesh.CategoryColor(rune.Category) : LOCKED_TINT,
                    isUnlocked ? UNLOCKED_ROW_HEIGHT : LOCKED_ROW_HEIGHT,
                    () => { if (isUnlocked) _view.GraphCanvas.PlaceRune(runeId, _view.GraphCanvas.SuggestPlacement(runeId)); },
                    runeId, isUnlocked);
            }
        }

        /// <summary>빠른 검색 팝업을 열고, 고른 해금 룬을 지정 그래프 좌표에 배치하게 한다.</summary>
        public void OpenQuickPalette(Vector2 graphPosition)
        {
            QuickPalettePopup popup = _ui.OpenPopup<QuickPalettePopup>();
            popup.Begin();
            Action<string> onSearch = query => FillQuickPalette(popup, query, graphPosition);
            popup.SearchChanged += onSearch;
            popup.Closed += () => popup.SearchChanged -= onSearch;
            FillQuickPalette(popup, popup.Query, graphPosition);
        }

        /// <summary>해금된 팔레트 룬 중 검색어와 일치하는 것으로 빠른 검색 결과를 채운다.</summary>
        private void FillQuickPalette(QuickPalettePopup popup, string query, Vector2 graphPosition)
        {
            popup.ClearResults();
            foreach (RuneDefinition rune in GameData.Runes.All)
            {
                if (!IsPaletteRune(rune) || !_editor.IsRuneUnlocked(rune.Id) || !MatchesSearch(rune, query)) continue;
                string runeId = rune.Id;
                popup.AddResult(rune.Name + " · " + rune.Ram + " RAM", RuneMesh.CategoryColor(rune.Category), () =>
                {
                    popup.Close();
                    _view.GraphCanvas.PlaceRune(runeId, graphPosition);
                });
            }
        }

        /// <summary>팔레트에 놓을 수 있는 룬인지 반환한다. 시전 코어·내부 룬·속성(드롭다운 값)은 제외한다.</summary>
        private static bool IsPaletteRune(RuneDefinition rune)
        {
            return rune.Category != SpellGrammar.CATEGORY_CORE && rune.Category != SpellGrammar.CATEGORY_INTERNAL
                && rune.Category != SpellGrammar.CATEGORY_ELEMENT;
        }

        /// <summary>검색어가 비어 있거나 룬 이름·ID 중 하나와 대소문자 구분 없이 일치하는지 반환한다.</summary>
        private static bool MatchesSearch(RuneDefinition rune, string query)
        {
            return string.IsNullOrEmpty(query)
                || rune.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || rune.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>잠긴 룬의 해금 조건(보상 또는 조각 비용) 문구를 반환한다.</summary>
        private static string UnlockCondition(RuneDefinition rune)
        {
            return rune.UnlockType == "reward" ? GameData.L("ui.reward") : rune.UnlockCost + " " + GameData.L("ui.fragments");
        }
    }
}
