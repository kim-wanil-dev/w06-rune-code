using System.Collections.Generic;
using System.Linq;

namespace RuneCode
{
    /// <summary>
    /// 세션의 세이브와 성장 값으로 마법 편집 규칙(보관함 한도, 해금, RAM 용량, Modifier 소지량, 튜토리얼, 상태 문구)을 적용한다.
    /// </summary>
    public sealed class SessionSpellPolicy : ISpellEditPolicy
    {
        private readonly RuneCodeSession _session;

        public int MaxLibrary => GameData.Balance.Economy.MaxLibrary;

        /// <summary>규칙을 적용할 세션 참조를 받는다.</summary>
        public SessionSpellPolicy(RuneCodeSession session)
        {
            _session = session;
        }

        /// <summary>현재 해금 룬, 성장 값과 Modifier 소지량으로 컴파일 문맥을 만든다.</summary>
        public SpellCompileContext GetCompileContext()
        {
            return new SpellCompileContext(_session.Save.UnlockedRunes, _session.Capacity, _session.MaxEnergy, _session.Save.Library,
                _session.Save.CreateModifierStockMap(), legacyBoxWorldAligned: _session.LegacyBoxWorldAligned);
        }

        /// <summary>룬이 세이브의 해금 목록에 있는지 반환한다.</summary>
        public bool IsRuneUnlocked(string runeId)
        {
            return _session.Save.UnlockedRunes.Contains(runeId);
        }

        /// <summary>후보 그래프를 장착 슬롯에 반영했을 때 공유·비공유 RAM 규칙을 지키는지 반환한다.</summary>
        public bool IsWithinRam(SpellGraph candidate)
        {
            if (candidate == null) return false;
            PlayerSave save = _session.Save;
            var equippedCopies = 0;
            for (var slot = 0; slot < save.SlotCount; slot++)
                if (save.Loadout[slot] == candidate.Id) equippedCopies++;
            if (equippedCopies == 0) return true;
            if (GameData.Balance.Ram.Mode == "shared") return CalculateEquippedRam(candidate) <= _session.Capacity;
            return GetGraphRam(candidate) <= _session.Capacity;
        }

        /// <summary>
        /// 현재 그래프를 후보로 바꿨을 때 수가 늘어나는 Modifier마다, 후보와 보관함의 다른 마법에 배치된 수의 합이 소지량 이하인지 반환한다.
        /// 이미 초과한 Modifier라도 늘리지 않는 편집(다른 노드 배치 등)은 막지 않는다.
        /// </summary>
        public bool IsWithinModifierStock(SpellGraph current, SpellGraph candidate)
        {
            if (candidate == null) return false;
            Dictionary<string, int> stock = _session.Save.CreateModifierStockMap();
            Dictionary<string, int> before = GraphCompiler.CountModifiers(current, GameData.Runes);
            foreach (KeyValuePair<string, int> pair in GraphCompiler.CountModifiers(candidate, GameData.Runes))
            {
                before.TryGetValue(pair.Key, out int previous);
                if (pair.Value <= previous) continue;
                stock.TryGetValue(pair.Key, out int owned);
                if (pair.Value + CountInOtherSpells(pair.Key, candidate.Id) > owned) return false;
            }
            return true;
        }

        /// <summary>편집 그래프와 보관함의 다른 마법에서 지정 등급 Modifier 배치 수와 소지량을 구한다.</summary>
        public bool TryGetModifierUsage(string runeId, string grade, SpellGraph editing, out int used, out int owned)
        {
            used = 0;
            owned = 0;
            if (!GameData.Runes.TryGet(runeId, out RuneDefinition rune) || rune.Category != SpellGrammar.CATEGORY_MODIFIER
                || !GameData.ModifierGrades.IsGradeAvailable(runeId, grade)) return false;
            string key = SpellGrammar.ModifierStockKey(runeId, grade);
            GraphCompiler.CountModifiers(editing, GameData.Runes).TryGetValue(key, out used);
            used += CountInOtherSpells(key, editing?.Id);
            owned = _session.Save.GetModifierStock(runeId, grade);
            return true;
        }

        /// <summary>보관함에서 지정 ID를 제외한 마법들에 배치된 Modifier 키별 수를 반환한다.</summary>
        private int CountInOtherSpells(string key, string excludedSpellId)
        {
            var count = 0;
            foreach (SpellGraph graph in _session.Save.Library)
            {
                if (graph == null || graph.Id == excludedSpellId) continue;
                if (GraphCompiler.CountModifiers(graph, GameData.Runes).TryGetValue(key, out int placed)) count += placed;
            }
            return count;
        }

        /// <summary>첫 발사 Behavior 블록 배치에서 튜토리얼 배치 단계를 기록한다.</summary>
        public void OnRunePlaced(string runeId)
        {
            if (runeId == "behavior.launch") _session.Save.SetTutorialStep(1);
        }

        /// <summary>Shape·Apply의 속성을 화염으로 고르면 튜토리얼 속성 단계를 기록한다.</summary>
        public void OnElementSelected(string elementId)
        {
            if (elementId == "fire") _session.Save.SetTutorialStep(2);
        }

        /// <summary>문자열 키를 세션 상태 문구로 전달한다.</summary>
        public void ReportStatus(string key)
        {
            _session.SetStatus(key);
        }

        /// <summary>이미 번역된 문구를 세션 상태 문구로 전달한다.</summary>
        public void ReportStatusText(string text)
        {
            _session.SetStatusText(text);
        }

        /// <summary>그래프에 배치된 모든 룬의 RAM을 합산한다.</summary>
        private int GetGraphRam(SpellGraph graph)
        {
            if (graph == null) return 0;
            var ram = 0;
            foreach (var node in graph.Nodes)
                ram += GameData.Runes.NodeRam(node);
            return ram;
        }

        /// <summary>후보 그래프를 자신을 참조하는 장착 슬롯에 반영한 전체 RAM 사용량을 반환한다.</summary>
        private int CalculateEquippedRam(SpellGraph candidate)
        {
            var ram = 0;
            PlayerSave save = _session.Save;
            for (var slot = 0; slot < save.SlotCount; slot++)
            {
                var id = save.Loadout[slot];
                ram += GetGraphRam(candidate != null && id == candidate.Id ? candidate : save.FindGraph(id));
            }
            return ram;
        }
    }
}
