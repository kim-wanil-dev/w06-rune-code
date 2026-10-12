using System;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 룬 팔레트와 빠른 검색 팝업의 Presenter다. 분류·검색어로 배치 가능한 룬을 골라 표시하고,
    /// 행을 누르면 제안 위치(팔레트) 또는 더블클릭 위치(빠른 검색)에 룬을 배치한다. 미해금 룬과 남은 소지량이 0인 Modifier 등급은 숨긴다.
    /// </summary>
    public sealed class SpellPalettePresenter
    {
        public const float ROW_HEIGHT = 52f;

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

        /// <summary>
        /// 현재 분류·검색어에 맞는 해금 룬을 표시한다. Modifier는 이름·등급과 남은 소지량/소지량을 표시한다.
        /// </summary>
        public void Refresh()
        {
            _view.ShowCategory(_category);
            _view.ClearPalette();
            string query = _view.SearchQuery;
            foreach (RuneDefinition rune in GameData.Runes.All)
            {
                if (!IsPaletteRune(rune) || !_editor.IsRuneUnlocked(rune.Id)
                    || (_category != "all" && TabCategory(rune) != _category) || !MatchesSearch(rune, query)) continue;
                if (rune.Category != SpellGrammar.CATEGORY_MODIFIER)
                {
                    AddPaletteRune(rune, null, rune.Name);
                    continue;
                }
                foreach (string grade in GameData.ModifierGrades.GetAvailableGrades(rune.Id))
                {
                    if (!TryGetRemaining(rune.Id, grade, out int remaining, out int owned)) continue;
                    AddPaletteRune(rune, grade, ModifierLabel(rune, grade, remaining, owned));
                }
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
                if (rune.Category == SpellGrammar.CATEGORY_MODIFIER)
                {
                    foreach (string grade in GameData.ModifierGrades.GetAvailableGrades(rune.Id))
                    {
                        if (!TryGetRemaining(rune.Id, grade, out int remaining, out int owned)) continue;
                        AddQuickRune(popup, rune, grade, graphPosition, remaining, owned);
                    }
                }
                else AddQuickRune(popup, rune, null, graphPosition, 0, 0);
            }
        }

        /// <summary>해금 룬의 팔레트 행을 만들고 누르면 지정 등급으로 그래프에 추가한다.</summary>
        private void AddPaletteRune(RuneDefinition rune, string grade, string label)
        {
            string runeId = rune.Id;
            Action onClick = () => _view.GraphCanvas.PlaceRune(runeId, _view.GraphCanvas.SuggestPlacement(runeId), grade);
            _view.AddPaletteRow(label, ROW_HEIGHT, onClick, runeId, grade, true);
        }

        /// <summary>Modifier 등급의 남은 소지량(remaining)과 소지량(owned)을 구하고 남은 소지량이 1 이상이면 true를 반환한다.</summary>
        private bool TryGetRemaining(string runeId, string grade, out int remaining, out int owned)
        {
            remaining = 0;
            if (!_editor.TryGetModifierUsage(runeId, grade, out int used, out owned)) return false;
            remaining = owned - used;
            return remaining > 0;
        }

        /// <summary>Modifier의 첫 줄에 이름·등급, 둘째 줄에 남은 소지량/소지량을 담은 문구를 반환한다.</summary>
        private static string ModifierLabel(RuneDefinition rune, string grade, int remaining, int owned)
        {
            return rune.Name + " " + grade + "\n" + GameData.L("ui.modifierStock") + " " + remaining + "/" + owned;
        }

        /// <summary>빠른 검색에 등급과 남은 소지량을 표시하고 지정 등급으로 배치하는 결과를 추가한다.</summary>
        private void AddQuickRune(QuickPalettePopup popup, RuneDefinition rune, string grade, Vector2 graphPosition, int remaining, int owned)
        {
            string label = rune.Category == SpellGrammar.CATEGORY_MODIFIER
                ? ModifierLabel(rune, grade, remaining, owned) : rune.Name;
            string runeId = rune.Id;
            popup.AddResult(label, () =>
            {
                popup.Close();
                _view.GraphCanvas.PlaceRune(runeId, graphPosition, grade);
            }, rune.Category);
        }

        /// <summary>룬이 표시될 팔레트 탭 계열을 반환한다.</summary>
        private static string TabCategory(RuneDefinition rune)
        {
            return rune.Category;
        }

        /// <summary>팔레트에 놓을 수 있는 룬인지 반환한다. 시전 코어·내부 룬·기본 중립 속성은 제외한다.</summary>
        private static bool IsPaletteRune(RuneDefinition rune)
        {
            return rune.Category != SpellGrammar.CATEGORY_CORE && rune.Category != SpellGrammar.CATEGORY_INTERNAL
                && rune.Id != SpellGrammar.ElementRune(SpellGrammar.ELEMENT_NEUTRAL);
        }

        /// <summary>검색어가 비어 있거나 룬 이름·ID 중 하나와 대소문자 구분 없이 일치하는지 반환한다.</summary>
        private static bool MatchesSearch(RuneDefinition rune, string query)
        {
            return string.IsNullOrEmpty(query)
                || rune.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || rune.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
