using System;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 룬 팔레트와 빠른 검색 팝업의 Presenter다. 분류·검색어로 배치 가능한 룬을 골라 표시하고,
    /// 행을 누르면 제안 위치(팔레트) 또는 더블클릭 위치(빠른 검색)에 룬을 배치한다. 미해금 룬과 남은 소지량이 0인 Modifier 등급은 표시하지 않는다.
    /// </summary>
    public sealed class SpellPalettePresenter
    {
        private const float ROW_HEIGHT = 40f;

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
        /// 현재 분류·검색어에 맞는 해금 룬 행을 다시 채운다. Modifier는 남은 소지량이 있는 등급만 등급별 행으로 남은 소지량/소지량과 함께 표시한다.
        /// </summary>
        public void Refresh()
        {
            _view.ClearPalette();
            string query = _view.SearchQuery;
            foreach (RuneDefinition rune in GameData.Runes.All)
            {
                if (!IsPaletteRune(rune) || !_editor.IsRuneUnlocked(rune.Id) || (_category != "all" && rune.Category != _category) || !MatchesSearch(rune, query)) continue;
                if (rune.Category != SpellGrammar.CATEGORY_MODIFIER)
                {
                    AddPaletteRune(rune, null, rune.Name + "   " + rune.Ram + " RAM");
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

        /// <summary>룬(Modifier면 지정 등급) 팔레트 행을 만들고 누르면 제안 위치에 배치하게 한다.</summary>
        private void AddPaletteRune(RuneDefinition rune, string grade, string label)
        {
            string runeId = rune.Id;
            _view.AddPaletteRow(label, RuneMesh.CategoryColor(rune.Category), ROW_HEIGHT,
                () => _view.GraphCanvas.PlaceRune(runeId, _view.GraphCanvas.SuggestPlacement(runeId), grade), runeId, grade, true);
        }

        /// <summary>Modifier 등급의 남은 소지량(remaining)과 소지량(owned)을 구하고 남은 소지량이 1 이상이면 true를 반환한다.</summary>
        private bool TryGetRemaining(string runeId, string grade, out int remaining, out int owned)
        {
            remaining = 0;
            if (!_editor.TryGetModifierUsage(runeId, grade, out int used, out owned)) return false;
            remaining = owned - used;
            return remaining > 0;
        }

        /// <summary>Modifier 행 문구(이름·등급·남은 소지량/소지량)를 반환한다.</summary>
        private static string ModifierLabel(RuneDefinition rune, string grade, int remaining, int owned)
        {
            return rune.Name + " " + grade + " · " + GameData.L("ui.modifierStock") + " " + remaining + "/" + owned;
        }

        /// <summary>빠른 검색에 등급과 남은 소지량을 표시하고 지정 등급으로 배치하는 결과를 추가한다.</summary>
        private void AddQuickRune(QuickPalettePopup popup, RuneDefinition rune, string grade, Vector2 graphPosition, int remaining, int owned)
        {
            string label = rune.Category == SpellGrammar.CATEGORY_MODIFIER
                ? ModifierLabel(rune, grade, remaining, owned) : rune.Name + " · " + rune.Ram + " RAM";
            string runeId = rune.Id;
            popup.AddResult(label, RuneMesh.CategoryColor(rune.Category), () =>
            {
                popup.Close();
                _view.GraphCanvas.PlaceRune(runeId, graphPosition, grade);
            });
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
    }
}
