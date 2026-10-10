using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 씬 전환과 무관하게 유지되는 앱 상태(세이브, 성장, 설정, 상태 문구, 마법 편집 세션)를 소유한다.
    /// Boot 씬의 RuneCodeApp이 생성하고 각 화면 컴포넌트의 Initialize로 전달한다.
    /// </summary>
    public sealed class RuneCodeSession
    {
        private PlayerSave _save;
        private PlayerSave _normalSave;
        private readonly StageCatalog _stages;
        private readonly UpgradeTreeDefinition _upgradeTree;
        private bool _isDebugEnabled;
        private readonly bool _legacyBoxWorldAligned;
        private readonly SpellEditSession _spells;
        private string _statusMessage;

        private string _dockScenario = "dummy_single";
        private bool _dockAutoFire;
        private bool _dockAdaptation;
        private float _dockSpeed = 1;

        public PlayerSave Save => _save;
        public ISpellEditor Spells => _spells;
        public bool IsDebugEnabled => _isDebugEnabled;
        public bool LegacyBoxWorldAligned => _legacyBoxWorldAligned;
        public string StatusMessage => _statusMessage;

        public string DockScenario => _dockScenario;
        public bool DockAutoFire => _dockAutoFire;
        public bool DockAdaptation => _dockAdaptation;
        public float DockSpeed => _dockSpeed;

        public int Capacity => GameData.Balance.Economy.BaseCapacity + Mathf.RoundToInt(GetUpgradeTreeEffectTotal(UpgradeEffectType.RamCapacity));
        public float MaxEnergy => GameData.Balance.Player.MaxEnergy + GetUpgradeTreeEffectTotal(UpgradeEffectType.MaxEnergy);
        public float MaxHp => GameData.Balance.Player.MaxHp + GetUpgradeTreeEffectTotal(UpgradeEffectType.MaxHp);
        public float EnergyRegen => GameData.Balance.Player.EnergyRegen + GetUpgradeTreeEffectTotal(UpgradeEffectType.EnergyRegen);
        public float DamageMultiplier => 1 + GetUpgradeTreeEffectTotal(UpgradeEffectType.Damage);
        public float MoveSpeedMultiplier => 1 + GetUpgradeTreeEffectTotal(UpgradeEffectType.MoveSpeed);
        public float ScrapGainMultiplier => 1 + GetUpgradeTreeEffectTotal(UpgradeEffectType.ScrapGain);
        public float BattleDuration => (float)_stages.Get(1).Stream.RampSeconds;
        public int EquippedRam => CalculateEquippedRam(_spells.Graph);
        public int SelectedStage => _save.SelectedStage;
        public int HighestClearedStage => _save.HighestClearedStage;

        /// <summary>화면 전환 요청을 받는 콜백이다. RuneCodeApp만 등록한다.</summary>
        public event Action<AppScreen> ScreenRequested;

        /// <summary>
        /// 세이브, 스테이지 정의(제한시간 성장 표시 기준값), 디버그 여부, 시작 경고 문구와
        /// 기존 저장 그래프의 Box 방향 기본값과 함께 세션과 마법 편집 세션을 만든다.
        /// </summary>
        public RuneCodeSession(PlayerSave save, StageCatalog stages, UpgradeTreeDefinition upgradeTree, bool isDebugEnabled,
            string initialStatus, bool legacyBoxWorldAligned)
        {
            _normalSave = save;
            _save = isDebugEnabled ? PlayerSave.CreateNew() : save;
            _stages = stages;
            _upgradeTree = upgradeTree;
            _isDebugEnabled = isDebugEnabled;
            _legacyBoxWorldAligned = legacyBoxWorldAligned;
            _statusMessage = initialStatus;
            if (isDebugEnabled) UnlockDebugRunes();
            var storage = new SessionSpellStorage(() => _save, () => !_isDebugEnabled);
            var policy = new SessionSpellPolicy(this);
            _spells = new SpellEditSession(storage, policy);
        }

        /// <summary>등록된 전환 콜백으로 지정 화면 전환을 요청한다.</summary>
        public void RequestScreen(AppScreen screen) { ScreenRequested?.Invoke(screen); }

        /// <summary>타이틀에서 선택한 일반 저장 또는 새 메모리 디버그 진행을 열고 작업실로 전환한다.</summary>
        public void StartGame(bool isDebug)
        {
            _isDebugEnabled = isDebug;
            _save = isDebug ? PlayerSave.CreateNew() : _normalSave;
            if (isDebug) UnlockDebugRunes();
            _spells.Reload();
            RequestScreen(AppScreen.Workshop);
        }

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
            // 문법 v3 속성은 element.* 룬으로 해금하므로 해금된 속성의 시뮬레이션 태그도 넣는다.
            foreach (ElementDefinition element in GameData.Runes.Elements)
                if (!string.IsNullOrEmpty(element.RuntimeTag) && _save.UnlockedRunes.Contains(element.RuneId) && !elements.Contains(element.RuntimeTag))
                    elements.Add(element.RuntimeTag);
            return elements;
        }

        /// <summary>노드의 구매 단계를 반환하고, 기존 해금 화면에서 이미 얻은 룬은 트리에서도 완료로 처리한다.</summary>
        public int GetUpgradeNodeLevel(string nodeId)
        {
            UpgradeTreeNodeDefinition node = _upgradeTree.FindNode(nodeId);
            if (node == null) return 0;
            if (node.EffectType == UpgradeEffectType.RuneUnlock && _save.UnlockedRunes.Contains(node.RuneId)) return node.MaxLevel;
            return Math.Min(_save.GetUpgradeNodeLevel(nodeId), node.MaxLevel);
        }

        /// <summary>노드의 다음 강화 비용을 반환하고 최대 레벨이거나 등록되지 않은 노드는 -1을 반환한다.</summary>
        public int GetUpgradeNodeCost(string nodeId)
        {
            UpgradeTreeNodeDefinition node = _upgradeTree.FindNode(nodeId);
            if (node == null) return -1;
            int cost = node.GetNextCost(GetUpgradeNodeLevel(nodeId));
            return _isDebugEnabled && cost >= 0 ? 0 : cost;
        }

        /// <summary>스테이지, 선행 노드, 재화를 확인하고 노드를 구매할 수 없으면 이유 키를 반환한다.</summary>
        public bool CanPurchaseUpgradeNode(string nodeId, out string reasonKey)
        {
            reasonKey = null;
            UpgradeTreeNodeDefinition node = _upgradeTree.FindNode(nodeId);
            if (node == null || node.MaxLevel == 0)
            {
                reasonKey = "tree.invalidNode";
                return false;
            }
            int currentLevel = GetUpgradeNodeLevel(nodeId);
            if (currentLevel >= node.MaxLevel)
            {
                reasonKey = "tree.maxed";
                return false;
            }
            if (node.EffectType == UpgradeEffectType.RuneUnlock && !GameData.Runes.TryGet(node.RuneId, out _))
            {
                reasonKey = "tree.invalidRune";
                return false;
            }
            if (!_isDebugEnabled && node.RequiredStage > _save.HighestClearedStage + 1)
            {
                reasonKey = "tree.stageLocked";
                return false;
            }
            if (!_isDebugEnabled)
                foreach (UpgradeTreePrerequisite prerequisite in node.Prerequisites)
                    if (GetUpgradeNodeLevel(prerequisite.NodeId) < prerequisite.RequiredLevel)
                    {
                        reasonKey = "tree.prerequisiteLocked";
                        return false;
                    }
            int cost = GetUpgradeNodeCost(nodeId);
            if (cost < 0 || (!_isDebugEnabled && _save.Currency < cost))
            {
                reasonKey = "tree.insufficient";
                return false;
            }
            return true;
        }

        /// <summary>선행 조건을 만족하는 트리 노드를 구매하고 효과·룬 해금·저장을 반영한다.</summary>
        public bool BuyUpgradeNode(string nodeId)
        {
            if (!CanPurchaseUpgradeNode(nodeId, out string reasonKey))
            {
                SetStatus(reasonKey);
                return false;
            }
            UpgradeTreeNodeDefinition node = _upgradeTree.FindNode(nodeId);
            int currentLevel = GetUpgradeNodeLevel(nodeId);
            int cost = GetUpgradeNodeCost(nodeId);
            if (!_save.Spend(cost))
            {
                SetStatus("tree.insufficient");
                return false;
            }
            _save.SetUpgradeNodeLevel(nodeId, currentLevel + 1);
            if (node.EffectType == UpgradeEffectType.RuneUnlock) _save.Unlock(node.RuneId);
            PersistProgress();
            _spells.Recompile();
            LocalTelemetry.Record(0, "upgradeTree.purchase", nodeId + ":" + (currentLevel + 1) + ":" + cost);
            return true;
        }

        /// <summary>해금 범위 안의 출격 스테이지를 선택하고 저장한다.</summary>
        public void SelectStage(int stage)
        {
            if (stage < 1 || stage > HighestClearedStage + 1) return;
            _save.SelectStage(stage);
            PersistProgress();
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
            PersistProgress();
        }

        /// <summary>screenShake 또는 hitStop 설정을 저장한다.</summary>
        public void SetSetting(string key, bool enabled)
        {
            _save.SetFeedback(key == "screenShake" ? enabled : _save.ScreenShake, key == "hitStop" ? enabled : _save.HitStop);
            PersistProgress();
        }

        /// <summary>진행을 초기화하고 새 시작 마법으로 편집 세션을 다시 연다.</summary>
        public void ResetSave()
        {
            _save = PlayerSave.CreateNew();
            if (_isDebugEnabled) UnlockDebugRunes();
            else _normalSave = _save;
            _spells.Reload();
            PersistProgress();
        }

        /// <summary>디버그 진행의 배치·미배치 스탯 레벨을 0으로 되돌리고 룬과 일반 저장은 유지한다.</summary>
        public void ResetDebugTree()
        {
            if (!_isDebugEnabled) return;
            foreach (UpgradeTreeNodeDefinition node in _upgradeTree.Nodes)
                if (node != null && node.EffectType != UpgradeEffectType.RuneUnlock)
                    _save.SetUpgradeNodeLevel(node.Id, 0);
            foreach (UpgradeTreeNodeDefinition node in _upgradeTree.UnplacedNodes)
                if (node != null && node.EffectType != UpgradeEffectType.RuneUnlock)
                    _save.SetUpgradeNodeLevel(node.Id, 0);
            _spells.Recompile();
        }

        /// <summary>튜토리얼 완료 단계를 저장한다.</summary>
        public void AdvanceTutorial(int step)
        {
            _save.SetTutorialStep(step);
            PersistProgress();
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

        /// <summary>
        /// 전투 보상과 처치 기록을 세이브에 반영하고 저장한다. 클리어면 다음 스테이지를 해금하고, 처음 클리어한 스테이지면
        /// 스테이지 데이터의 Modifier 소지량 보상을 지급한다. 지급한 Modifier 보상 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<ModifierRewardDefinition> SettleMission(int stageNumber, int fragments, bool isCleared, IEnumerable<KeyValuePair<string, int>> killCounts)
        {
            var modifierRewards = new List<ModifierRewardDefinition>();
            if (isCleared && stageNumber > _save.HighestClearedStage)
            {
                foreach (ModifierRewardDefinition reward in _stages.Get(stageNumber).ModifierRewards)
                {
                    // Modifier가 아닌 ID는 데이터 오류로 보고 지급하지 않는다.
                    if (!GameData.Runes.TryGet(reward.RuneId, out RuneDefinition rune) || rune.Category != SpellGrammar.CATEGORY_MODIFIER)
                    { Debug.LogWarning("[Stage] Modifier가 아닌 보상 ID를 건너뜁니다: " + reward.RuneId); continue; }
                    GrantModifier(reward.RuneId, string.IsNullOrEmpty(reward.Grade) ? GameData.ModifierGrades.GetLowestAvailableGrade(reward.RuneId) : reward.Grade, reward.Count);
                    modifierRewards.Add(reward);
                }
            }
            if (isCleared) _save.RecordStageClear(stageNumber);
            _save.Settle(fragments, isCleared);
            foreach (var pair in killCounts) _save.RecordKills(pair.Key, pair.Value);
            PersistProgress();
            if (modifierRewards.Count > 0) _spells.Recompile();
            return modifierRewards;
        }

        /// <summary>주운·선택·보상받은 Modifier를 (종류, 등급) 소지량에 즉시 반영하고 저장한다. 이후 사망·시간 초과·후퇴와 무관하게 유지된다. 그 Modifier에 없는 등급이면 지급하지 않는다.</summary>
        public void GrantModifier(string runeId, string grade, int count)
        {
            if (count <= 0 || !GameData.Runes.TryGet(runeId, out RuneDefinition rune) || rune.Category != SpellGrammar.CATEGORY_MODIFIER) return;
            _save.AddModifierStock(runeId, grade, count);
            PersistProgress();
        }

        /// <summary>디버그 실행에서만 조각을 지급한다.</summary>
        public void DebugGrant()
        {
            if (!_isDebugEnabled) return;
            _save.Settle(500, false);
            PersistProgress();
        }

        /// <summary>디버그 실행에서만 모든 룬을 해금한다.</summary>
        public void DebugUnlock()
        {
            if (!_isDebugEnabled) return;
            foreach (var rune in GameData.Runes.All) _save.Unlock(rune.Id);
            PersistProgress();
            _spells.Recompile();
        }

        /// <summary>편집 중인 마법과 세이브를 저장한다. 앱 종료 시 호출한다.</summary>
        public void SaveAll()
        {
            _spells.Save();
        }

        /// <summary>일반 모드의 진행만 저장 파일에 기록하고 디버그 진행은 메모리에만 둔다.</summary>
        private void PersistProgress()
        {
            if (!_isDebugEnabled) SaveStore.Write(_save);
        }

        /// <summary>디버그 진행에 배치 여부와 관계없이 SO의 모든 룬 해금 노드를 적용한다.</summary>
        private void UnlockDebugRunes()
        {
            foreach (UpgradeTreeNodeDefinition node in _upgradeTree.Nodes)
                if (node != null && node.EffectType == UpgradeEffectType.RuneUnlock)
                    _save.Unlock(node.RuneId);
            foreach (UpgradeTreeNodeDefinition node in _upgradeTree.UnplacedNodes)
                if (node != null && node.EffectType == UpgradeEffectType.RuneUnlock)
                    _save.Unlock(node.RuneId);
        }

        /// <summary>배치된 모든 룬의 RAM을 합산한다.</summary>
        private int GetGraphRam(SpellGraph graph)
        {
            if (graph == null) return 0;
            return graph.Nodes.Sum(node => GameData.Runes.NodeRam(node));
        }

        /// <summary>해당 강화 유형에 속한 트리 노드의 저장 레벨까지 효과량을 합산한다.</summary>
        private float GetUpgradeTreeEffectTotal(UpgradeEffectType effectType)
        {
            float total = 0;
            foreach (UpgradeTreeNodeDefinition node in _upgradeTree.Nodes)
                if (node != null && node.EffectType == effectType)
                    total += node.GetTotalAmount(GetUpgradeNodeLevel(node.Id));
            return total;
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
