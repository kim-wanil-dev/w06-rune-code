using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace RuneCode
{
    [Serializable]
    public sealed class PlayerSave : ISerializationCallbackReceiver
    {
        private static readonly string[] DEFAULT_METHOD_IDS = { "magic_missile", "barrier" };

        [Header("저장 버전")]
        [SerializeField] private int _version = 2;

        [Header("완드 및 진행")]
        [SerializeField] private int _currency;
        [SerializeField] private int _capacityLevel;
        [SerializeField] private int _slotCount = 1;
        [SerializeField] private int _energyLevel;
        [SerializeField] private int _hpLevel;
        [SerializeField] private int _durationLevel;
        [SerializeField] private int _highestClearedStage;
        [SerializeField] private int _selectedStage = 1;
        [SerializeField] private string _activeSpellId;
        [SerializeField] private bool _sectorCleared;
        [SerializeField] private List<string> _unlockedRunes = new List<string>();
        [SerializeField] private List<SpellGraphData> _library = new List<SpellGraphData>();
        [SerializeField] private List<string> _loadout = new List<string>();
        [SerializeField] private List<KillRecord> _killCounts = new List<KillRecord>();

        [Header("피드백 설정")]
        [SerializeField] private bool _screenShake = true;
        [SerializeField] private bool _hitStop = true;
        [SerializeField] private int _tutorialStep;

        private List<SpellGraph> _graphs = new List<SpellGraph>();

        public int Version => _version;
        public int Currency => _currency;
        public int CapacityLevel => _capacityLevel;
        public int SlotCount => _slotCount;
        public int EnergyLevel => _energyLevel;
        public int HpLevel => _hpLevel;
        public int DurationLevel => _durationLevel;
        public int HighestClearedStage => _highestClearedStage;
        public int SelectedStage => _selectedStage;
        public string ActiveSpellId => _activeSpellId;
        public bool SectorCleared => _sectorCleared;
        public IReadOnlyList<string> UnlockedRunes => _unlockedRunes;
        public IReadOnlyList<SpellGraph> Library => _graphs;
        public IReadOnlyList<string> Loadout => _loadout;
        public IReadOnlyList<KillRecord> KillCounts => _killCounts;
        public bool ScreenShake => _screenShake;
        public bool HitStop => _hitStop;
        public int TutorialStep => _tutorialStep;

        /// <summary>JSON 저장 직전에 실행 중 보관함 그래프를 직렬화 형식(_library)으로 옮긴다. JSON 키는 기존과 같다.</summary>
        public void OnBeforeSerialize()
        {
            _library = new List<SpellGraphData>(_graphs.Count);
            foreach (SpellGraph graph in _graphs)
            {
                _library.Add(SpellGraphData.From(graph));
            }
        }

        /// <summary>JSON을 읽은 직후 직렬화 형식(_library)을 실행 중 보관함 그래프로 되돌린다. 검증은 Validate가 수행한다.</summary>
        public void OnAfterDeserialize()
        {
            _graphs = new List<SpellGraph>();
            if (_library == null)
            {
                return;
            }
            foreach (SpellGraphData data in _library)
            {
                _graphs.Add(data?.ToGraph());
            }
        }

        /// <summary>시작 룬, 계속 강화할 파이어 볼트와 기본 마법 메소드(매직 미사일·방어막)를 가진 새 진행 데이터를 반환한다.</summary>
        public static PlayerSave CreateNew()
        {
            var save = new PlayerSave();
            foreach (var rune in GameData.Runes.All)
                if (rune.UnlockType == "start" && rune.Category != SpellGrammar.CATEGORY_INTERNAL) save._unlockedRunes.Add(rune.Id);
            save._graphs.Add(GameData.Spells[0].Clone());
            save._activeSpellId = save._graphs[0].Id;
            save._loadout.Add(save._graphs[0].Id);
            save._loadout.Add(null);
            save._loadout.Add(null);
            save.AddDefaultMethods();
            return save;
        }

        /// <summary>
        /// 이전 룬으로 만든 보관함 그래프를 문법 블록으로 변환하고, 해금 룬 ID를 새 블록으로 바꾸며
        /// 카탈로그에 없는 ID를 정리한다. 기본 마법 메소드가 없으면 보관함 한도 안에서 추가한다. 여러 번 호출해도 결과가 같다.
        /// </summary>
        public void MigrateSpellGrammar()
        {
            if (_graphs == null || _unlockedRunes == null) return;
            foreach (var graph in _graphs) SpellGraphMigration.Migrate(graph);
            var unlocked = new List<string>();
            foreach (var runeId in _unlockedRunes)
                foreach (var mapped in SpellGraphMigration.MapUnlockedRune(runeId))
                    if (GameData.Runes.TryGet(mapped, out var rune) && rune.Category != SpellGrammar.CATEGORY_INTERNAL && !unlocked.Contains(mapped)) unlocked.Add(mapped);
            foreach (var rune in GameData.Runes.All)
                if (rune.UnlockType == "start" && rune.Category != SpellGrammar.CATEGORY_INTERNAL && !unlocked.Contains(rune.Id)) unlocked.Add(rune.Id);
            _unlockedRunes = unlocked;
            AddDefaultMethods();
        }

        /// <summary>기본 마법 메소드 템플릿 중 보관함에 없는 것을 한도 안에서 추가한다.</summary>
        private void AddDefaultMethods()
        {
            foreach (var id in DEFAULT_METHOD_IDS)
            {
                if (FindGraph(id) != null || _graphs.Count >= GameData.Balance.Economy.MaxLibrary) continue;
                var template = GameData.FindSpell(id);
                if (template != null) _graphs.Add(template.Clone());
            }
        }

        /// <summary>저장된 값과 룬·마법 참조를 검사하고 유효하지 않은 저장이면 원인을 반환한다.</summary>
        public bool Validate(out string error)
        {
            error = null;
            var economy = GameData.Balance.Economy;
            if (_version != 2 || _currency < 0 || _currency > 100000000 ||
                _capacityLevel < 0 || _capacityLevel > economy.MaxGrowthLevel ||
                _energyLevel < 0 || _energyLevel > economy.MaxGrowthLevel ||
                _hpLevel < 0 || _hpLevel > economy.StatCosts.Count ||
                _durationLevel < 0 || _durationLevel > economy.MaxDurationLevel ||
                _highestClearedStage < 0 || _highestClearedStage >= 1000000 ||
                _selectedStage < 1 || _selectedStage > _highestClearedStage + 1 ||
                _slotCount != 1 || _tutorialStep < 0 || _tutorialStep > 3)
            {
                error = "save.invalidValues";
                return false;
            }
            if (_graphs == null || _graphs.Count == 0 || _graphs.Count > economy.MaxLibrary ||
                _loadout == null || _loadout.Count != 3 || _unlockedRunes == null || _killCounts == null)
            {
                error = "save.invalidStructure";
                return false;
            }
            var ids = new HashSet<string>();
            foreach (var graph in _graphs)
            {
                if (graph == null || string.IsNullOrWhiteSpace(graph.Id) || !ids.Add(graph.Id))
                {
                    error = "save.invalidStructure";
                    return false;
                }
                try
                {
                    ShareCodec.Deserialize(ShareCodec.Serialize(graph));
                    foreach (var node in graph.Nodes)
                        if (!GameData.Runes.TryGet(node.RuneId, out _))
                        { error = "save.invalidStructure"; return false; }
                }
                catch (Exception exception) when (exception is FormatException || exception is ArgumentException)
                { error = "save.invalidStructure"; return false; }
            }
            foreach (var runeId in _unlockedRunes)
                if (!GameData.Runes.TryGet(runeId, out _))
                {
                    error = "save.invalidStructure";
                    return false;
                }
            var killIds = new HashSet<string>();
            foreach (var record in _killCounts)
                if (record == null || string.IsNullOrWhiteSpace(record.Id) || record.Id.Length > 80 || record.Count < 0 || !killIds.Add(record.Id))
                { error = "save.invalidStructure"; return false; }
            for (var slot = 0; slot < _loadout.Count; slot++)
                if (!string.IsNullOrEmpty(_loadout[slot]) && (!ids.Contains(_loadout[slot]) || slot >= _slotCount))
                {
                    error = "save.invalidStructure";
                    return false;
                }
            if (string.IsNullOrEmpty(_activeSpellId) || !ids.Contains(_activeSpellId) || _loadout[0] != _activeSpellId)
            { error = "save.invalidStructure"; return false; }
            return true;
        }

        /// <summary>버전 1의 설계와 성장 값을 보존하고 A 마법을 단일 활성 설계로 이전한다.</summary>
        public void MigrateToIncremental()
        {
            if (_version != 1) return;
            var economy = GameData.Balance.Economy;
            if (_slotCount < 2 || _slotCount > 3 || _capacityLevel < 0 || _capacityLevel > economy.CapacityCosts.Count ||
                _energyLevel < 0 || _energyLevel > economy.StatCosts.Count || _loadout == null || _loadout.Count != 3 ||
                _graphs == null || _graphs.Count == 0 || _currency < 0 || _currency > 100000000)
                throw new FormatException("이전 버전 저장 구조가 유효하지 않습니다.");
            _activeSpellId = !string.IsNullOrEmpty(_loadout[0]) && _graphs.Any(graph => graph != null && graph.Id == _loadout[0])
                ? _loadout[0] : _graphs[0]?.Id;
            if (_slotCount == 3) _currency = Math.Min(100000000, _currency + economy.SlotCost);
            _slotCount = 1;
            _loadout[0] = _activeSpellId; _loadout[1] = null; _loadout[2] = null;
            _durationLevel = 0; _highestClearedStage = _sectorCleared ? 1 : 0;
            _selectedStage = _highestClearedStage + 1;
            _version = 2;
        }

        /// <summary>해금한 범위 안의 전투 스테이지를 선택하고 저장 상태를 변경한다.</summary>
        public void SelectStage(int stage)
        { if (stage >= 1 && stage <= _highestClearedStage + 1) _selectedStage = stage; }

        /// <summary>제한시간을 완주한 스테이지 기록을 갱신하고 다음 스테이지를 선택한다.</summary>
        public void RecordStageClear(int stage)
        {
            if (stage < 1 || stage > _highestClearedStage + 1 || stage >= 1000000) return;
            _highestClearedStage = Math.Max(_highestClearedStage, stage);
            _selectedStage = _highestClearedStage + 1;
        }

        /// <summary>ID와 일치하는 보관함 마법을 반환하며 없으면 null을 반환한다.</summary>
        public SpellGraph FindGraph(string id)
        {
            return _graphs.FirstOrDefault(graph => graph.Id == id);
        }

        /// <summary>라이브러리에 존재하는 Spell ID를 활성 편집·첫 슬롯 참조로 설정한다.</summary>
        public bool SetActiveGraph(string id)
        {
            if (FindGraph(id) == null) return false;
            _activeSpellId = id;
            _loadout[0] = id;
            return true;
        }

        /// <summary>마법을 복사하여 보관함에 추가하거나 같은 ID의 저장본을 갱신한다.</summary>
        public bool StoreGraph(SpellGraph graph)
        {
            var index = _graphs.FindIndex(item => item.Id == graph.Id);
            if (index < 0)
            {
                if (_graphs.Count >= GameData.Balance.Economy.MaxLibrary) return false;
                _graphs.Add(graph.Clone());
            }
            else _graphs[index] = graph.Clone();
            return true;
        }

        /// <summary>ID의 마법을 삭제하고 해당 마법을 참조하는 장착 슬롯을 비운다.</summary>
        public void RemoveGraph(string id)
        {
            if (id == _activeSpellId) return;
            _graphs.RemoveAll(graph => graph.Id == id);
            for (var slot = 0; slot < _loadout.Count; slot++)
                if (_loadout[slot] == id) _loadout[slot] = null;
        }

        /// <summary>단일 활성 마법과 일치하는 ID만 첫 슬롯에 유지한다.</summary>
        public void SetLoadout(int slot, string id)
        {
            if (slot != 0 || id != _activeSpellId) return;
            _loadout[0] = _activeSpellId;
        }

        /// <summary>비용을 지불할 수 있으면 조각을 차감하고 성공 여부를 반환한다.</summary>
        public bool Spend(int amount)
        {
            if (amount < 0 || _currency < amount) return false;
            _currency -= amount;
            return true;
        }

        /// <summary>양수 RAM 보상을 누적하고 시간제 전투 완주 기록을 남긴다.</summary>
        public void Settle(int fragments, bool cleared)
        {
            _currency = (int)Math.Min(100000000L, _currency + (long)Math.Max(0, fragments));
            if (cleared) _sectorCleared = true;
        }

        /// <summary>처치한 적 ID별 기록에 전달된 횟수를 더한다.</summary>
        public void RecordKills(string enemyId, int count)
        {
            var record = _killCounts.FirstOrDefault(item => item.Id == enemyId);
            if (record == null)
            {
                record = new KillRecord(enemyId);
                _killCounts.Add(record);
            }
            record.Add(count);
        }

        /// <summary>지원하는 성장 종류의 단계를 한 단계 증가시킨다.</summary>
        public void Upgrade(string kind)
        {
            switch (kind)
            {
                case "capacity": if (_capacityLevel < GameData.Balance.Economy.MaxGrowthLevel) _capacityLevel++; break;
                case "energy": if (_energyLevel < GameData.Balance.Economy.MaxGrowthLevel) _energyLevel++; break;
                case "duration": if (_durationLevel < GameData.Balance.Economy.MaxDurationLevel) _durationLevel++; break;
            }
        }

        /// <summary>유효한 룬 ID를 중복 없이 해금 목록에 추가한다.</summary>
        public void Unlock(string runeId)
        {
            if (GameData.Runes.TryGet(runeId, out _) && !_unlockedRunes.Contains(runeId))
                _unlockedRunes.Add(runeId);
        }

        /// <summary>진행한 튜토리얼 단계를 완료 범위 안에서 저장한다.</summary>
        public void SetTutorialStep(int step)
        {
            _tutorialStep = Math.Max(_tutorialStep, Math.Min(3, step));
        }

        /// <summary>화면 흔들림과 히트스톱의 사용자 설정을 갱신한다.</summary>
        public void SetFeedback(bool screenShake, bool hitStop)
        {
            _screenShake = screenShake;
            _hitStop = hitStop;
        }
    }

    [Serializable]
    public sealed class KillRecord
    {
        [Header("처치 기록")]
        [SerializeField] private string _id;
        [SerializeField] private int _count;
        public string Id => _id;
        public int Count => _count;

        /// <summary>적 ID를 사용해 빈 처치 기록을 만든다.</summary>
        public KillRecord(string id) { _id = id; }

        /// <summary>양수 처치 횟수를 누적한다.</summary>
        public void Add(int count) { _count += Math.Max(0, count); }
    }
}
