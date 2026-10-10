using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 인스펙터 Presenter다. 선택 노드의 파라미터 행·보조 정보·삭제 버튼과 컴파일 검증 목록을 정해 인스펙터 View에 표시하고,
    /// 입력을 편집 세션에 저장한다. 효과량은 기능 속성일 때만 보이고, 속성 선택은 잠긴 속성을 건너뛴다.
    /// </summary>
    public sealed class SpellInspectorPresenter
    {
        private static readonly Color ISSUE_ERROR_TINT = new Color(1f, 0.41f, 0.44f);
        private static readonly Color ISSUE_WARNING_TINT = new Color(1f, 0.76f, 0.38f);
        private static readonly Color DELETE_TINT = new Color(0.97f, 0.43f, 0.45f);

        private readonly ISpellEditor _editor;
        private readonly SpellInspectorView _view;
        private readonly RuneGraphCanvas _graph;
        private readonly Action<string> _openSpellPicker;
        private GraphNode _selected;

        /// <summary>편집 세션, 인스펙터 View, 그래프(검증 항목 포커스용)와 프리셋 대상 선택 팝업 열기 동작으로 Presenter를 만든다.</summary>
        public SpellInspectorPresenter(ISpellEditor editor, SpellInspectorView view, RuneGraphCanvas graph, Action<string> openSpellPicker)
        {
            _editor = editor;
            _view = view;
            _graph = graph;
            _openSpellPicker = openSpellPicker;
        }

        /// <summary>선택 노드를 바꾸고 인스펙터를 다시 그린다.</summary>
        public void Select(GraphNode node)
        {
            _selected = node;
            Refresh();
        }

        /// <summary>선택 노드를 해제하고 인스펙터를 다시 그린다.</summary>
        public void ClearSelection() => Select(null);

        /// <summary>선택 노드를 현재 그래프에서 다시 찾아 인스펙터와 검증 목록을 다시 그린다.</summary>
        public void Refresh()
        {
            if (_selected != null) _selected = _editor.Graph.FindNode(_selected.Id);
            _view.Begin();
            if (_selected == null) _view.AddEmptyHint(GameData.L("ui.selectNode"));
            else ShowNode(_selected);
            ShowValidation();
        }

        /// <summary>노드 이름·RAM, 표시 대상 파라미터, 효과 범위 안내, 기본 정보, 행동 수치, 삭제 버튼을 표시한다.</summary>
        private void ShowNode(GraphNode node)
        {
            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            _view.AddTitle(rune.Name + " / " + GameData.Runes.NodeRam(node) + " RAM", RuneMesh.CategoryColor(rune.Category));
            foreach (ParameterDefinition parameter in rune.Params)
            {
                if (IsParameterVisible(node, parameter)) ShowParameter(node, rune, parameter);
            }
            if (rune.Category == SpellGrammar.CATEGORY_MODIFIER)
            {
                ShowModifierGrade(node, rune);
                if (rune.Id == "mod.expand") _view.AddNote(GameData.L("ui.mod.expandHint"), UiTheme.Muted, 52, 6);
            }
            if (rune.Category == SpellGrammar.CATEGORY_SHAPE)
                _view.AddNote(GameData.L("ui.shapeSizeBounds"), UiTheme.Muted, 34, 6);
            if (rune.Id == "behavior.beam") _view.AddNote(GameData.L("ui.behavior.beamHint"), UiTheme.Muted, 70, 6);
            if (rune.Params.Count == 0 && rune.Category != SpellGrammar.CATEGORY_MODIFIER)
            {
                _view.AddNote(GameData.L("ui.energy") + " " + rune.Energy.ToString("0.#") + "  /  " + GameData.L("ui.damage") + " "
                    + rune.Stats.Damage.ToString("0.#"), UiTheme.Muted, 34, 8);
            }
            if (rune.Category == SpellGrammar.CATEGORY_BEHAVIOR) ShowBehaviorStats(node);
            if (rune.Category != SpellGrammar.CATEGORY_CORE) _view.AddButton(GameData.L("ui.delete"), DELETE_TINT, () => _editor.RemoveNode(node.Id));
        }

        /// <summary>Modifier 등급 선택 버튼과 현재 등급의 고정 수치 안내를 표시한다.</summary>
        private void ShowModifierGrade(GraphNode node, RuneDefinition rune)
        {
            string grade = SpellGrammar.GetModifierGrade(rune, node, GameData.ModifierGrades);
            _editor.TryGetModifierUsage(rune.Id, grade, out int used, out int owned);
            string label = GameData.L("ui.modifierGrade") + " " + grade + " · " + GameData.L("ui.modifierStock")
                + " " + Math.Max(0, owned - used) + "/" + owned;
            _view.AddButton(label, UiTheme.Cyan, () => SelectNextModifierGrade(node.Id, rune));
            _view.AddNote(GameData.L("ui.gradeStats") + " " + FormatModifierStats(rune, grade), UiTheme.Cyan, 70, 6);
        }

        /// <summary>소지량이 있는 다음 등급을 찾아 선택 노드에 저장한다.</summary>
        private void SelectNextModifierGrade(string nodeId, RuneDefinition rune)
        {
            GraphNode node = _editor.Graph.FindNode(nodeId);
            IReadOnlyList<string> grades = GameData.ModifierGrades.GetAvailableGrades(rune.Id);
            if (node == null || grades.Count < 2) return;
            string currentGrade = SpellGrammar.GetModifierGrade(rune, node, GameData.ModifierGrades);
            int currentIndex = -1;
            for (int index = 0; index < grades.Count; index++)
            {
                if (grades[index] == currentGrade) { currentIndex = index; break; }
            }
            for (int step = 1; step < grades.Count; step++)
            {
                string grade = grades[(currentIndex + step + grades.Count) % grades.Count];
                if (!_editor.TryGetModifierUsage(rune.Id, grade, out int used, out int owned) || owned <= used) continue;
                _editor.SetNodeText(nodeId, SpellGrammar.MODIFIER_GRADE_PARAM, grade);
                Refresh();
                return;
            }
        }

        /// <summary>등급 고정값과 Modifier의 보조 고정 수치를 안내 문자열로 반환한다.</summary>
        private static string FormatModifierStats(RuneDefinition rune, string grade)
        {
            var values = new List<string>();
            ModifierGradeTable grades = GameData.ModifierGrades;
            string effectType = grades.GetEffectType(rune.Id);
            object gradeValue = grades.GetGradeValue(rune.Id, grade);
            switch (effectType)
            {
                case "effectMultiplier": AddMultiplier(values, "ui.gradeDamage", gradeValue); break;
                case "multipleCount": values.Add(GameData.L("ui.gradeCount") + " " + gradeValue); break;
                case "pierceCount": values.Add(GameData.L("ui.gradePierce") + " " + gradeValue); break;
                case "radiusMultiplier": AddMultiplier(values, "ui.gradeRadius", gradeValue); break;
                case "durationMultiplier": AddMultiplier(values, "ui.gradeDuration", gradeValue); break;
                case "speedMultiplier": AddMultiplier(values, "ui.gradeSpeed", gradeValue); break;
                case "homingTier":
                    string tier = gradeValue as string;
                    HomingTierDefinition homing = rune.FindHomingTier(tier);
                    values.Add(tier);
                    if (homing != null)
                    {
                        values.Add(GameData.L("ui.gradeHomingTurn") + " " + homing.HomingTurn.ToString("0.#") + "°");
                        values.Add(GameData.L("ui.gradeHomingRange") + " " + homing.HomingRange.ToString("0.#"));
                    }
                    break;
            }
            RuneStats stats = rune.Stats;
            if (rune.Id == "mod.multi")
            {
                if (stats.OrbitCount > 0) values.Add(GameData.L("ui.gradeOrbitCount") + " " + stats.OrbitCount);
                if (stats.DamageMultiplier != 1f) values.Add(GameData.L("ui.gradeDamage") + " ×" + stats.DamageMultiplier.ToString("0.##"));
                if (stats.SpreadAngle > 0f) values.Add(GameData.L("ui.gradeSpread") + " " + stats.SpreadAngle.ToString("0.#") + "°");
            }
            if (rune.Id == "mod.expand" && stats.DamageMultiplier != 1f)
                values.Add(GameData.L("ui.gradeDamage") + " ×" + stats.DamageMultiplier.ToString("0.##"));
            if (rune.Id == "mod.pierce" && stats.PierceLoss > 0f)
                values.Add(GameData.L("ui.gradePierceLoss") + " " + stats.PierceLoss.ToString("0.##"));
            return string.Join(" · ", values);
        }

        /// <summary>숫자 등급 값에 번역된 효과 라벨과 배율 기호를 붙인다.</summary>
        private static void AddMultiplier(List<string> values, string labelKey, object value)
        {
            values.Add(GameData.L(labelKey) + " ×" + Convert.ToSingle(value).ToString("0.##"));
        }

        /// <summary>파라미터 종류(숫자·프리셋 대상·문자·선택지)에 맞는 행을 표시하고 입력을 편집 세션 저장으로 연결한다.</summary>
        private void ShowParameter(GraphNode node, RuneDefinition rune, ParameterDefinition parameter)
        {
            string nodeId = node.Id;
            string label = GameData.L("param." + parameter.Id);
            if (parameter.Kind == "number")
            {
                bool isScale = rune.Category == SpellGrammar.CATEGORY_MODIFIER;
                _view.AddNumberRow(label, GetParameterNumber(node, rune, parameter).ToString("0.###"), isScale,
                    text => { if (float.TryParse(text, out float amount)) _editor.SetNodeNumber(nodeId, parameter.Id, Mathf.Clamp(amount, parameter.Min, parameter.Max)); },
                    () => StepNumber(nodeId, parameter, -1), () => StepNumber(nodeId, parameter, 1));
            }
            else if (parameter.Kind == "text" && rune.Id == SpellGrammar.CALL_RUNE && parameter.Id == "spellId")
            {
                _view.AddSpellRow(label, CalledSpellLabel(node.GetText(parameter.Id, parameter.DefaultText)), () => _openSpellPicker(nodeId));
            }
            else if (parameter.Kind == "text")
            {
                _view.AddTextRow(label, node.GetText(parameter.Id, parameter.DefaultText), text => _editor.SetNodeText(nodeId, parameter.Id, text));
            }
            else
            {
                string current = GetParameterText(node, rune, parameter);
                _view.AddEnumRow(label, OptionLabel(parameter.Id, current), () => _editor.SetNodeText(nodeId, parameter.Id, NextOption(parameter, current)));
            }
        }

        /// <summary>Shape 노드에 저장된 값을 우선하고, 값이 없으면 연결된 Behavior 크기를 기본으로 반환한다.</summary>
        private float GetParameterNumber(GraphNode node, RuneDefinition rune, ParameterDefinition parameter)
        {
            if (HasParameter(node, parameter.Id)) return node.GetNumber(parameter.Id, parameter.DefaultNumber);
            if (rune.Category != SpellGrammar.CATEGORY_SHAPE) return parameter.DefaultNumber;
            RuneDefinition behavior = GetConnectedBehavior(node);
            float radius = behavior == null ? parameter.DefaultNumber : behavior.Stats.Radius;
            switch (parameter.Id)
            {
                case SpellGrammar.SHAPE_RADIUS_PARAM:
                case SpellGrammar.CONE_DISTANCE_PARAM: return radius;
                case SpellGrammar.BOX_WIDTH_PARAM:
                case SpellGrammar.BOX_HEIGHT_PARAM:
                case SpellGrammar.BOX_LENGTH_PARAM: return radius * 2f;
                case SpellGrammar.CONE_ANGLE_PARAM: return rune.Stats.ConeAngle > 0f ? rune.Stats.ConeAngle : parameter.DefaultNumber;
                default: return parameter.DefaultNumber;
            }
        }

        /// <summary>Shape 방향이 저장되지 않았으면 컴파일된 레거시 기본값을, 그 외에는 테이블 기본값을 반환한다.</summary>
        private string GetParameterText(GraphNode node, RuneDefinition rune, ParameterDefinition parameter)
        {
            if (HasParameter(node, parameter.Id)) return node.GetText(parameter.Id, parameter.DefaultText);
            if (rune.Category == SpellGrammar.CATEGORY_SHAPE && parameter.Id == SpellGrammar.BOX_DIRECTION_PARAM)
            {
                RuneDefinition behavior = GetConnectedBehavior(node);
                SpellAction action = behavior == null ? null : _editor.CompileResult.Spell?.FindAction(behavior.Id);
                if (action != null) return action.Stats?.IsBoxWorldAligned == true ? "world" : "aim";
                return behavior == null || behavior.Id == "behavior.launch" ? "aim" : "world";
            }
            return parameter.DefaultText;
        }

        /// <summary>Shape 체인의 다음 Behavior 룬을 반환하고 연결이 없으면 null을 반환한다.</summary>
        private RuneDefinition GetConnectedBehavior(GraphNode shape)
        {
            GraphEdge edge = _editor.Graph.Edges.FirstOrDefault(item => item.FromNode == shape.Id && item.FromPort == SpellGrammar.CHAIN_OUT);
            GraphNode behavior = edge == null ? null : _editor.Graph.FindNode(edge.ToNode);
            return behavior != null && GameData.Runes.TryGet(behavior.RuneId, out RuneDefinition rune)
                && rune.Category == SpellGrammar.CATEGORY_BEHAVIOR ? rune : null;
        }

        /// <summary>노드에 지정 파라미터가 저장되어 있는지 반환한다.</summary>
        private static bool HasParameter(GraphNode node, string key)
        {
            foreach (NodeParameter parameter in node.Params)
                if (parameter.Key == key) return true;
            return false;
        }

        /// <summary>컴파일된 행동의 사거리·반경·속도를 표시한다. 컴파일 결과가 없으면 표시하지 않는다.</summary>
        private void ShowBehaviorStats(GraphNode node)
        {
            SpellAction action = _editor.CompileResult.Spell?.FindAction(node.Id);
            if (action?.Stats == null) return;
            SpellStats stats = action.Stats;
            if (action.Form == SpellGrammar.FORM_BEAM)
            {
                // Beam의 크기 원본은 RuneSimulation.GetBeamBox 한 곳에만 있다(W1 통합 시 함께 바뀐다).
                RuneSimulation.GetBeamBox(stats, out double width, out double length);
                _view.AddNote(GameData.L("ui.spellRange") + " " + length.ToString("0.#") + "px\n"
                    + GameData.L("ui.spellRadius") + " " + (width * 0.5).ToString("0.#") + "px", UiTheme.Cyan, 70, 6);
                return;
            }
            float reach = action.Form == SpellGrammar.FORM_BOLT ? stats.Speed * stats.Lifetime
                : action.Form == SpellGrammar.FORM_ORBIT ? stats.OrbitRadius : stats.Offset;
            string speed = action.Form == SpellGrammar.FORM_ORBIT ? stats.AngularSpeed.ToString("0.#") + "°/s" : stats.Speed.ToString("0.#") + "px/s";
            _view.AddNote(GameData.L("ui.spellRange") + " " + reach.ToString("0.#") + "px\n"
                + GameData.L("ui.spellRadius") + " " + stats.Radius.ToString("0.#") + "px\n"
                + GameData.L("ui.spellSpeed") + " " + speed, UiTheme.Cyan, 70, 6);
        }

        /// <summary>컴파일 성공 여부, 오류·경고(누르면 해당 노드로 이동)와 조작 안내를 검증 목록에 표시한다.</summary>
        private void ShowValidation()
        {
            RectTransform list = _view.BeginValidation();
            CompileResult result = _editor.CompileResult;
            if (result.Ok) _view.AddIssue(list, GameData.L("ui.valid"), UiTheme.Cyan, null);
            foreach (CompileIssue issue in result.Errors) AddIssue(list, issue, ISSUE_ERROR_TINT);
            foreach (CompileIssue issue in result.Warnings) AddIssue(list, issue, ISSUE_WARNING_TINT);
            _view.AddIssue(list, GameData.L("ui.controls"), UiTheme.Muted, null);
        }

        /// <summary>검증 항목 하나를 코드·문구로 표시하고 누르면 관련 노드로 그래프를 이동하게 한다.</summary>
        private void AddIssue(RectTransform list, CompileIssue issue, Color tint)
        {
            string nodeId = issue.NodeId;
            _view.AddIssue(list, issue.Code + " · " + CompileIssueText.Format(issue), tint, () => { if (nodeId != null) _graph.FocusNode(nodeId); });
        }

        /// <summary>수식 배율을 데이터의 한 단계만큼 바꾸고 범위 안으로 제한해 저장한 뒤 인스펙터를 다시 그린다.</summary>
        private void StepNumber(string nodeId, ParameterDefinition parameter, int direction)
        {
            GraphNode node = _editor.Graph.FindNode(nodeId);
            if (node == null) return;
            float value = GetParameterNumber(node, GameData.Runes.Get(node.RuneId), parameter) + parameter.Step * direction;
            _editor.SetNodeNumber(nodeId, parameter.Id, Mathf.Clamp(value, parameter.Min, parameter.Max));
            Refresh();
        }

        /// <summary>프리셋 호출 대상 ID를 보관함 이름과 ID, 미선택 또는 없는 마법 안내 문구로 바꿔 반환한다.</summary>
        private string CalledSpellLabel(string spellId)
        {
            SpellGraph called = _editor.Library.FirstOrDefault(graph => graph.Id == spellId);
            if (called != null) return called.Name + "\n" + called.Id;
            return string.IsNullOrEmpty(spellId) ? GameData.L("ui.selectSpell") : spellId + " · " + GameData.L("ui.missingSpell");
        }

        /// <summary>
        /// 인스펙터에 파라미터를 표시할지 반환한다. 효과량·보호막 지속시간은 속성이 Functional(회복·보호)인 Shape·Apply에서만 표시한다.
        /// </summary>
        private static bool IsParameterVisible(GraphNode node, ParameterDefinition parameter)
        {
            if (parameter.Id == SpellGrammar.BOX_HEIGHT_PARAM) return false;
            if (parameter.Id != SpellGrammar.POWER_PARAM && parameter.Id != SpellGrammar.BUFF_DURATION_PARAM) return true;
            return GameData.Runes.TryGetNodeElement(node, out ElementDefinition element) && element.IsFunctional;
        }

        /// <summary>
        /// 선택지 파라미터의 다음 값을 반환한다. 속성(element)은 해금하지 않은 속성을 건너뛰며, 선택 가능한 값이 없으면 현재 값을 유지한다.
        /// </summary>
        private string NextOption(ParameterDefinition definition, string current)
        {
            int index = 0;
            for (int i = 0; i < definition.Options.Count; i++)
            {
                if (definition.Options[i] == current) index = i;
            }
            for (int step = 1; step <= definition.Options.Count; step++)
            {
                string candidate = definition.Options[(index + step) % definition.Options.Count];
                bool isLockedElement = definition.Id == SpellGrammar.ELEMENT_PARAM && GameData.Runes.TryGetElement(candidate, out ElementDefinition element)
                    && !_editor.IsRuneUnlocked(element.RuneId);
                if (!isLockedElement) return candidate;
            }
            return current;
        }

        /// <summary>선택지 값을 한국어 라벨로 반환하고 번역이 없으면 원문 값을 반환한다.</summary>
        private static string OptionLabel(string parameter, string value)
        {
            string prefix = parameter == "trigger" ? "trigger." : parameter == "magicType" ? "magicType."
                : parameter == "element" ? "element." : parameter == "form" ? "magicForm."
                : parameter == SpellGrammar.BOX_DIRECTION_PARAM ? "boxDirection."
                : parameter == "status" ? "status." : "condition.";
            string localized = GameData.L(prefix + value);
            return localized == prefix + value ? value : localized;
        }
    }
}
