using System;
using System.Collections.Generic;
using System.Linq;

namespace RuneCode
{
    /// <summary>
    /// 씬 전환과 무관하게 유지되는 앱 상태(세이브, 성장, 설정, 상태 문구, 마법 편집 세션)를 소유한다.
    /// Boot 씬의 RuneCodeApp이 생성하고 각 화면 컴포넌트의 Initialize로 전달한다.
    /// </summary>
    public sealed class RuneCodeSession
    {
        private PlayerSave _save;
        private readonly IncrementalDefinition _incremental;
        private readonly bool _isDebugEnabled;
        private readonly bool _isAreaBoxUpright;
        private readonly SpellEditSession _spells;
        private string _statusMessage;

        private string _dockScenario = "dummy_single";
        private bool _dockAutoFire;
        private bool _dockAdaptation;
        private float _dockSpeed = 1;

        public PlayerSave Save => _save;
        public ISpellEditor Spells => _spells;
        public bool IsDebugEnabled => _isDebugEnabled;
        public bool IsAreaBoxUpright => _isAreaBoxUpright;
        public string StatusMessage => _statusMessage;

        public string DockScenario => _dockScenario;
        public bool DockAutoFire => _dockAutoFire;
        public bool DockAdaptation => _dockAdaptation;
        public float DockSpeed => _dockSpeed;

        public int Capacity => GameData.Balance.Economy.BaseCapacity + _save.CapacityLevel * GameData.Balance.Economy.CapacityStep;
        public float MaxEnergy => GameData.Balance.Player.MaxEnergy + _save.EnergyLevel * GameData.Balance.Economy.StatStep;
        public float MaxHp => GameData.Balance.Player.MaxHp + _save.HpLevel * GameData.Balance.Economy.StatStep;
        public float EnergyRegen => GameData.Balance.Player.EnergyRegen + _save.EnergyLevel * GameData.Balance.Economy.EnergyRegenStep;
        public float BattleDuration => (float)_incremental.BaseDuration + _save.DurationLevel * GameData.Balance.Economy.DurationStep;
        public int EquippedRam => CalculateEquippedRam(_spells.Graph);
        public int SelectedStage => _save.SelectedStage;
        public int HighestClearedStage => _save.HighestClearedStage;

        /// <summary>화면 전환 요청을 받는 콜백이다. RuneCodeApp만 등록한다.</summary>
        public event Action<AppScreen> ScreenRequested;

        /// <summary>
        /// 세이브, 시간제 스테이지 정의, 디버그 여부, 시작 경고 문구와
        /// 범위 사각형 판정의 월드 축 고정 여부로 세션과 마법 편집 세션을 만든다.
        /// </summary>
        public RuneCodeSession(PlayerSave save, IncrementalDefinition incremental, bool isDebugEnabled, string initialStatus, bool isAreaBoxUpright)
        {
            _save = save;
            _incremental = incremental;
            _isDebugEnabled = isDebugEnabled;
            _isAreaBoxUpright = isAreaBoxUpright;
            _statusMessage = initialStatus;
            var storage = new SessionSpellStorage(() => _save);
            var policy = new SessionSpellPolicy(this);
            _spells = new SpellEditSession(storage, policy);
        }

        /// <summary>등록된 전환 콜백으로 지정 화면 전환을 요청한다.</summary>
        public void RequestScreen(AppScreen screen) { ScreenRequested?.Invoke(screen); }

        /// <summary>문자열 키를 번역해 상태 문구로 저장한다.</summary>
        public void SetStatus(string key) { _statusMessage = GameData.L(key); }

        /// <summary>이미 번역된 문구를 상태 문구로 저장한다.</summary>
        public void SetStatusText(string text) { _statusMessage = text; }

        /// <summary>시험 도크의 시나리오와 자동 시전·적응·배속을 세션에 저장해 작업실 재진입 후에도 유지한다.</summary>
        public void SetDockOptions(string scenario, bool autoFire, bool adaptation, float speed)
        {
            _dockScenario = scenario;
            _dockAutoFire = autoFire;
            _dockAdaptation = adaptation;
            _dockSpeed = Math.Max(0.5f, Math.Min(2, speed));
        }

        /// <summary>노이즈 후보로 사용할 해금 속성 태그와 raw를 반환한다.</summary>
        public IReadOnlyList<string> GetUnlockedElements()
        {
            var elements = new List<string> { "raw" };
            foreach (var id in _save.UnlockedRunes) if (id.StartsWith("elem.", StringComparison.Ordinal)) elements.Add(id.Substring(5));
            return elements;
        }

        /// <summary>성장 항목 또는 벤치 룬의 구매 비용을 반환하며 구매 불가면 -1을 반환한다.</summary>
        public int GetUpgradeCost(string kind)
        {
            var economy = GameData.Balance.Economy;
            switch (kind)
            {
                case "capacity": return economy.GetGrowthCost(kind, _save.CapacityLevel);
                case "energy": return economy.GetGrowthCost(kind, _save.EnergyLevel);
                case "duration": return economy.GetGrowthCost(kind, _save.DurationLevel);
                default:
                    return GameData.Runes.TryGet(kind, out var rune) && rune.UnlockType == "bench" && !_save.UnlockedRunes.Contains(kind) ? rune.UnlockCost : -1;
            }
        }

        /// <summary>비용을 지불하고 성장 또는 룬 해금을 적용해 저장한다. 성공하면 true를 반환한다.</summary>
        public bool BuyUpgrade(string kind)
        {
            var cost = GetUpgradeCost(kind);
            if (cost < 0) { SetStatus("bench.maxed"); return false; }
            if (!_save.Spend(cost)) { SetStatus("bench.insufficient"); return false; }
            if (kind.Contains(".")) _save.Unlock(kind); else _save.Upgrade(kind);
            SaveStore.Write(_save);
            _spells.Recompile();
            LocalTelemetry.Record(0, "bench.purchase", kind + ":" + cost);
            return true;
        }

        /// <summary>해금 범위 안의 출격 스테이지를 선택하고 저장한다.</summary>
        public void SelectStage(int stage)
        {
            if (stage < 1 || stage > HighestClearedStage + 1) return;
            _save.SelectStage(stage);
            SaveStore.Write(_save);
        }

        /// <summary>현재 마법을 컴파일 규칙과 RAM 규칙으로 검증해 지정 슬롯에 장착한다.</summary>
        public void Equip(int slot)
        {
            if (slot != 0) return;
            _spells.Recompile();
            if (!_spells.CompileResult.Ok) { SetStatus("editor.invalidEquip"); return; }
            var editingId = _spells.Graph.Id;
            var ram = 0;
            for (var i = 0; i < _save.SlotCount; i++)
                ram += i == slot ? _spells.CompileResult.Spell.RamUsed : GetGraphRam(_save.Loadout[i] == editingId ? _spells.Graph : _save.FindGraph(_save.Loadout[i]));
            if (GameData.Balance.Ram.Mode == "shared" && ram > Capacity) { SetStatus("editor.ramBlocked"); return; }
            _spells.Save();
            _save.SetLoadout(slot, editingId);
            SaveStore.Write(_save);
        }

        /// <summary>screenShake 또는 hitStop 설정을 저장한다.</summary>
        public void SetSetting(string key, bool enabled)
        {
            _save.SetFeedback(key == "screenShake" ? enabled : _save.ScreenShake, key == "hitStop" ? enabled : _save.HitStop);
            SaveStore.Write(_save);
        }

        /// <summary>진행을 초기화하고 새 시작 마법으로 편집 세션을 다시 연다.</summary>
        public void ResetSave()
        {
            _save = PlayerSave.CreateNew();
            _spells.Reload();
            SaveStore.Write(_save);
        }

        /// <summary>튜토리얼 완료 단계를 저장한다.</summary>
        public void AdvanceTutorial(int step)
        {
            _save.SetTutorialStep(step);
            SaveStore.Write(_save);
        }

        /// <summary>마법을 저장·컴파일하고 장착 가능하면 시전할 마법을 반환한다. 불가하면 상태 문구를 남기고 false를 반환한다.</summary>
        public bool TryPrepareMission(out CompiledSpell spell)
        {
            _spells.Save();
            _spells.Recompile();
            if (!_spells.CompileResult.Ok || EquippedRam > Capacity)
            {
                SetStatus("editor.invalidEquip");
                spell = null;
                return false;
            }
            spell = _spells.CompileResult.Spell;
            return true;
        }

        /// <summary>전투 보상과 처치 기록을 세이브에 반영하고 저장한다. 클리어면 다음 스테이지를 해금한다.</summary>
        public void SettleMission(int stageNumber, int fragments, bool isCleared, IEnumerable<KeyValuePair<string, int>> killCounts)
        {
            if (isCleared) _save.RecordStageClear(stageNumber);
            _save.Settle(fragments, isCleared);
            foreach (var pair in killCounts) _save.RecordKills(pair.Key, pair.Value);
            SaveStore.Write(_save);
        }

        /// <summary>디버그 실행에서만 조각을 지급한다.</summary>
        public void DebugGrant()
        {
            if (!_isDebugEnabled) return;
            _save.Settle(500, false);
            SaveStore.Write(_save);
        }

        /// <summary>디버그 실행에서만 모든 룬을 해금한다.</summary>
        public void DebugUnlock()
        {
            if (!_isDebugEnabled) return;
            foreach (var rune in GameData.Runes.All) _save.Unlock(rune.Id);
            SaveStore.Write(_save);
            _spells.Recompile();
        }

        /// <summary>편집 중인 마법과 세이브를 저장한다. 앱 종료 시 호출한다.</summary>
        public void SaveAll()
        {
            _spells.Save();
        }

        /// <summary>배치된 모든 룬의 RAM을 합산한다.</summary>
        private int GetGraphRam(SpellGraph graph)
        {
            if (graph == null) return 0;
            return graph.Nodes.Sum(node => GameData.Runes.NodeRam(node));
        }

        /// <summary>편집 사본을 반영한 장착 마법 전체의 RAM 사용량을 반환한다.</summary>
        private int CalculateEquippedRam(SpellGraph edited)
        {
            var ram = 0;
            foreach (var id in _save.Loadout.Take(_save.SlotCount)) ram += GetGraphRam(edited != null && id == edited.Id ? edited : _save.FindGraph(id));
            return ram;
        }
    }
}
