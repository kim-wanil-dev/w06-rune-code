using System;
using System.Collections.Generic;

using UnityEngine;

using UnityEditor;

namespace RuneCode
{
    public static class UpgradeTreeAssetBuilder
    {
        private const string ASSET_PATH = "Assets/RuneCode/Resources/RuneCode/UpgradeTree.asset";
        private const int BASE_TOPOLOGY_VERSION = 5;
        private const int TOPOLOGY_VERSION = 6;
        private const string MAINBOARD_NODE_ID = "upgrade.mainboard";
        private const string RAM_NODE_ID = "upgrade.ram";
        private const string POWER_NODE_ID = "upgrade.maxEnergy";
        private const string CPU_NODE_ID = "upgrade.cpu";
        private const string GPU_NODE_ID = "upgrade.gpu";
        private const string SCRAP_NODE_ID = "upgrade.scrapGain";
        private const string HP_NODE_ID = "upgrade.maxHp";
        private const string MOVE_SPEED_NODE_ID = "upgrade.moveSpeed";

        private static readonly HashSet<string> _placedNodeIds = new HashSet<string>
        {
            MAINBOARD_NODE_ID, RAM_NODE_ID, POWER_NODE_ID, CPU_NODE_ID, GPU_NODE_ID,
            SCRAP_NODE_ID, HP_NODE_ID, MOVE_SPEED_NODE_ID,
            "unlock.element.fire", "unlock.element.ice", "unlock.behavior.burst", "unlock.behavior.persist"
        };

        private static readonly RuneNodeSeed[] _runeNodeSeeds =
        {
            new RuneNodeSeed("element.fire", MAINBOARD_NODE_ID, new Vector2(650, 220), 0),
            new RuneNodeSeed("element.ice", MAINBOARD_NODE_ID, new Vector2(1350, 220), 15),
            new RuneNodeSeed("element.neutral", "unlock.element.fire", new Vector2(430, 410), 5),
            new RuneNodeSeed("element.healing", "unlock.element.fire", new Vector2(610, 410), 20),
            new RuneNodeSeed("element.lightning", "unlock.element.ice", new Vector2(1390, 410), 35),
            new RuneNodeSeed("element.protection", "unlock.element.ice", new Vector2(1570, 410), 25),
            new RuneNodeSeed("behavior.burst", GPU_NODE_ID, new Vector2(900, 600), 15),
            new RuneNodeSeed("behavior.persist", GPU_NODE_ID, new Vector2(1170, 600), 20),
            new RuneNodeSeed("behavior.orbit", "unlock.behavior.persist", new Vector2(1710, 800), 25),
            new RuneNodeSeed("behavior.apply", "unlock.behavior.persist", new Vector2(1890, 800), 35),
            new RuneNodeSeed("mod.amplify", "unlock.behavior.burst", new Vector2(510, 800), 20),
            new RuneNodeSeed("mod.multi", "unlock.behavior.burst", new Vector2(690, 800), 30),
            new RuneNodeSeed("mod.pierce", "unlock.behavior.burst", new Vector2(870, 800), 30),
            new RuneNodeSeed("mod.expand", "unlock.behavior.burst", new Vector2(1050, 800), 30),
            new RuneNodeSeed("mod.duration", "unlock.behavior.burst", new Vector2(1230, 800), 25),
            new RuneNodeSeed("mod.speed", "unlock.behavior.persist", new Vector2(2070, 800), 20),
            new RuneNodeSeed("mod.homing", "unlock.behavior.persist", new Vector2(2250, 800), 50),
            new RuneNodeSeed("flow.delay", CPU_NODE_ID, new Vector2(260, 600), 10),
            new RuneNodeSeed("flow.repeat", CPU_NODE_ID, new Vector2(440, 600), 40),
            new RuneNodeSeed("flow.if", CPU_NODE_ID, new Vector2(620, 600), 60),
            new RuneNodeSeed("spell.call", CPU_NODE_ID, new Vector2(800, 600), 25)
        };

        /// <summary>트리 자산이 없으면 기본 데이터를 만들고 구버전이면 배치·미배치 목록으로 한 번 이전한다.</summary>
        public static void EnsureAsset()
        {
            UpgradeTreeDefinition definition = AssetDatabase.LoadAssetAtPath<UpgradeTreeDefinition>(ASSET_PATH);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<UpgradeTreeDefinition>();
                AssetDatabase.CreateAsset(definition, ASSET_PATH);
            }
            if (definition.LayoutVersion < BASE_TOPOLOGY_VERSION) CreateDefaultNodes(definition);
            if (definition.LayoutVersion < TOPOLOGY_VERSION) SeparateUnplacedNodes(definition);
        }

        /// <summary>기존 노드를 새 하드웨어 트리·레벨별 비용·효과량·선행 관계로 교체하고 검증한 뒤 저장한다.</summary>
        private static void CreateDefaultNodes(UpgradeTreeDefinition definition)
        {
            SerializedObject serialized = new SerializedObject(definition);
            SerializedProperty nodes = serialized.FindProperty("_nodes");
            nodes.ClearArray();
            serialized.FindProperty("_unplacedNodes").ClearArray();

            AddUpgradeNode(nodes, MAINBOARD_NODE_ID, "ui.tree.mainboard", "ui.tree.mainboardDescription",
                new Vector2(1000, 40), UpgradeEffectType.EnergyRegen, null,
                new[] { 0, 10, 20 }, new[] { 0.5f, 0.5f, 0.5f });
            AddUpgradeNode(nodes, RAM_NODE_ID, "ui.tree.ramCapacity", "ui.tree.ramDescription",
                new Vector2(1000, 220), UpgradeEffectType.RamCapacity, MAINBOARD_NODE_ID,
                new[] { 10, 20, 40 }, new[] { 1f, 1f, 1f });
            AddUpgradeNode(nodes, POWER_NODE_ID, "ui.tree.maxEnergy", "ui.tree.maxEnergyDescription",
                new Vector2(830, 410), UpgradeEffectType.MaxEnergy, RAM_NODE_ID,
                new[] { 8, 16, 32 }, new[] { 5f, 5f, 5f });
            AddUpgradeNode(nodes, CPU_NODE_ID, "ui.tree.cpu", "ui.tree.cpuDescription",
                new Vector2(1000, 410), UpgradeEffectType.CastSpeed, RAM_NODE_ID,
                new[] { 10, 20, 40 }, new[] { 0f, 0f, 0f });
            AddUpgradeNode(nodes, GPU_NODE_ID, "ui.tree.gpu", "ui.tree.gpuDescription",
                new Vector2(1170, 410), UpgradeEffectType.Damage, RAM_NODE_ID,
                new[] { 12, 24, 48 }, new[] { 0.1f, 0.1f, 0.1f });
            AddUpgradeNode(nodes, SCRAP_NODE_ID, "ui.tree.scrapGain", "ui.tree.scrapGainDescription",
                new Vector2(1440, 600), UpgradeEffectType.ScrapGain, GPU_NODE_ID,
                new[] { 15, 30, 60 }, new[] { 0.1f, 0.1f, 0.1f });
            AddUpgradeNode(nodes, HP_NODE_ID, "ui.tree.maxHp", "ui.tree.maxHpDescription",
                new Vector2(1350, 800), UpgradeEffectType.MaxHp, SCRAP_NODE_ID,
                new[] { 10, 20, 40 }, new[] { 10f, 10f, 10f });
            AddUpgradeNode(nodes, MOVE_SPEED_NODE_ID, "ui.tree.moveSpeed", "ui.tree.moveSpeedDescription",
                new Vector2(1530, 800), UpgradeEffectType.MoveSpeed, SCRAP_NODE_ID,
                new[] { 15, 30, 60 }, new[] { 0.05f, 0.05f, 0.05f });

            foreach (RuneNodeSeed seed in _runeNodeSeeds) AddRuneNode(nodes, seed);

            serialized.FindProperty("_layoutVersion").intValue = BASE_TOPOLOGY_VERSION;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ValidateAndSave(definition);
        }

        /// <summary>기존 노드의 비용·아이콘·레벨을 보존하면서 지정된 12개만 배치 목록에 남기고 나머지 선행 조건을 비운다.</summary>
        private static void SeparateUnplacedNodes(UpgradeTreeDefinition definition)
        {
            SerializedObject serialized = new SerializedObject(definition);
            SerializedProperty placed = serialized.FindProperty("_nodes");
            SerializedProperty unplaced = serialized.FindProperty("_unplacedNodes");
            for (int index = 0; index < placed.arraySize;)
            {
                SerializedProperty node = placed.GetArrayElementAtIndex(index);
                if (_placedNodeIds.Contains(node.FindPropertyRelative("_id").stringValue))
                {
                    index++;
                    continue;
                }
                int destinationIndex = unplaced.arraySize;
                unplaced.InsertArrayElementAtIndex(destinationIndex);
                SerializedProperty destination = unplaced.GetArrayElementAtIndex(destinationIndex);
                destination.boxedValue = node.boxedValue;
                destination.FindPropertyRelative("_prerequisites").ClearArray();
                placed.DeleteArrayElementAtIndex(index);
            }
            serialized.FindProperty("_layoutVersion").intValue = TOPOLOGY_VERSION;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ValidateAndSave(definition);
        }

        /// <summary>룬 카탈로그의 해금 대상 룬과 지정한 부모·위치·비용으로 1회 구매 노드를 추가한다.</summary>
        private static void AddRuneNode(SerializedProperty nodes, RuneNodeSeed seed)
        {
            if (!GameData.Runes.TryGet(seed.RuneId, out RuneDefinition rune) || rune.UnlockType != "tree")
                throw new InvalidOperationException("트리 해금 대상 룬을 찾을 수 없습니다: " + seed.RuneId);
            AddNode(nodes, RuneNodeId(seed.RuneId), "ui.tree.unlockRune", "ui.tree.unlockRuneDescription",
                seed.Position, UpgradeEffectType.RuneUnlock, seed.RuneId, seed.ParentNodeId,
                new[] { seed.Cost }, new[] { 0f });
        }

        /// <summary>강화 노드와 레벨별 비용·증가량을 기록하고 선택한 부모의 1레벨을 선행 조건으로 설정한다.</summary>
        private static void AddUpgradeNode(SerializedProperty nodes, string nodeId, string titleKey, string descriptionKey,
            Vector2 position, UpgradeEffectType effectType, string parentNodeId, int[] costs, float[] amounts)
        {
            AddNode(nodes, nodeId, titleKey, descriptionKey, position, effectType, null, parentNodeId, costs, amounts);
        }

        /// <summary>노드 ID·표시 키·아이콘 자리·효과·부모와 단계별 값을 직렬화 배열에 기록한다.</summary>
        private static void AddNode(SerializedProperty nodes, string nodeId, string titleKey, string descriptionKey,
            Vector2 position, UpgradeEffectType effectType, string runeId, string parentNodeId, int[] costs, float[] amounts)
        {
            int index = nodes.arraySize;
            nodes.InsertArrayElementAtIndex(index);
            SerializedProperty node = nodes.GetArrayElementAtIndex(index);
            node.FindPropertyRelative("_id").stringValue = nodeId;
            node.FindPropertyRelative("_requiredStage").intValue = 1;
            node.FindPropertyRelative("_titleKey").stringValue = titleKey;
            node.FindPropertyRelative("_descriptionKey").stringValue = descriptionKey;
            node.FindPropertyRelative("_position").vector2Value = position;
            node.FindPropertyRelative("_icon").objectReferenceValue = null;
            node.FindPropertyRelative("_effectType").enumValueIndex = (int)effectType;
            node.FindPropertyRelative("_runeId").stringValue = runeId;

            SerializedProperty prerequisites = node.FindPropertyRelative("_prerequisites");
            prerequisites.ClearArray();
            if (!string.IsNullOrEmpty(parentNodeId))
            {
                prerequisites.InsertArrayElementAtIndex(0);
                SerializedProperty prerequisite = prerequisites.GetArrayElementAtIndex(0);
                prerequisite.FindPropertyRelative("_nodeId").stringValue = parentNodeId;
                prerequisite.FindPropertyRelative("_requiredLevel").intValue = 1;
            }

            SerializedProperty levels = node.FindPropertyRelative("_levels");
            levels.ClearArray();
            for (int levelIndex = 0; levelIndex < costs.Length; levelIndex++)
            {
                levels.InsertArrayElementAtIndex(levelIndex);
                SerializedProperty level = levels.GetArrayElementAtIndex(levelIndex);
                level.FindPropertyRelative("_cost").intValue = costs[levelIndex];
                level.FindPropertyRelative("_amount").floatValue = amounts[levelIndex];
            }
        }

        /// <summary>룬 ID에 접두어를 붙여 ScriptableObject에서 안정적으로 참조할 노드 ID를 반환한다.</summary>
        private static string RuneNodeId(string runeId)
        {
            return "unlock." + runeId;
        }

        /// <summary>업그레이드 트리의 구조·룬 커버리지·선행 조건을 검증해 잘못된 자산 저장을 막는다.</summary>
        private static void ValidateAndSave(UpgradeTreeDefinition definition)
        {
            if (!definition.Validate(out string error)) throw new InvalidOperationException(error);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
        }

        private readonly struct RuneNodeSeed
        {
            private readonly string _runeId;
            private readonly string _parentNodeId;
            private readonly Vector2 _position;
            private readonly int _cost;

            public string RuneId => _runeId;
            public string ParentNodeId => _parentNodeId;
            public Vector2 Position => _position;
            public int Cost => _cost;

            /// <summary>룬 해금 ID와 부모, 시작 위치, 기본 비용을 저장해 에셋 노드 생성 입력을 만든다.</summary>
            public RuneNodeSeed(string runeId, string parentNodeId, Vector2 position, int cost)
            {
                _runeId = runeId;
                _parentNodeId = parentNodeId;
                _position = position;
                _cost = cost;
            }
        }
    }
}
