using System;

using UnityEngine;
using UnityEditor;

namespace RuneCode
{
    public static class UpgradeTreeAssetBuilder
    {
        private const string ASSET_PATH = "Assets/RuneCode/Resources/RuneCode/UpgradeTree.asset";
        private const int TOPOLOGY_VERSION = 2;
        private const string REGEN_NODE = "regen";
        private const string ENERGY_NODE = "max_energy";
        private const string RAM_NODE = "ram_capacity";
        private const float STAGE_SPACING = 184;
        private const float TRACK_SPACING = 124;

        private static readonly string[] _runeOrder =
        {
            "mod.speed", "element.ice", "mod.pierce", "mod.expand", "shape.remain",
            "flow.repeat", "element.electric", "mod.homing", "flow.if"
        };

        /// <summary>트리 자산이 없으면 기본 데이터를 만들고 구형 배치라면 노드 위치·선행 연결만 교차형으로 갱신한다.</summary>
        public static void EnsureAsset()
        {
            UpgradeTreeDefinition definition = AssetDatabase.LoadAssetAtPath<UpgradeTreeDefinition>(ASSET_PATH);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<UpgradeTreeDefinition>();
                AssetDatabase.CreateAsset(definition, ASSET_PATH);
                CreateDefaultNodes(definition);
                return;
            }
            if (definition.LayoutVersion >= TOPOLOGY_VERSION) return;
            MigrateTopology(definition);
        }

        /// <summary>빈 트리에 기본 강화량·레벨별 비용과 서로 교차하는 1~10 스테이지 선행 연결을 추가한다.</summary>
        private static void CreateDefaultNodes(UpgradeTreeDefinition definition)
        {
            SerializedObject serialized = new SerializedObject(definition);
            SerializedProperty nodes = serialized.FindProperty("_nodes");
            nodes.ClearArray();
            for (int stage = 1; stage <= 10; stage++)
            {
                AddStatNode(nodes, stage, REGEN_NODE, "ui.tree.regen", "ui.tree.regenDescription",
                    UpgradeEffectType.EnergyRegen, 5, 0.5f);
                AddStatNode(nodes, stage, ENERGY_NODE, "ui.tree.maxEnergy", "ui.tree.maxEnergyDescription",
                    UpgradeEffectType.MaxEnergy, 6, 5);
                AddStatNode(nodes, stage, RAM_NODE, "ui.tree.ramCapacity", "ui.tree.ramDescription",
                    UpgradeEffectType.RamCapacity, 8, 1);
                if (stage <= _runeOrder.Length) AddRuneNode(nodes, stage, _runeOrder[stage - 1]);
            }
            serialized.FindProperty("_layoutVersion").intValue = TOPOLOGY_VERSION;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ValidateAndSave(definition);
        }

        /// <summary>기존 기본 노드의 ID·비용·효과량과 추가 사용자 노드를 보존하면서 기본 노드 연결망을 한 번만 재배치한다.</summary>
        private static void MigrateTopology(UpgradeTreeDefinition definition)
        {
            SerializedObject serialized = new SerializedObject(definition);
            SerializedProperty nodes = serialized.FindProperty("_nodes");
            for (int index = 0; index < nodes.arraySize; index++)
            {
                SerializedProperty node = nodes.GetArrayElementAtIndex(index);
                string nodeId = node.FindPropertyRelative("_id").stringValue;
                if (!TryReadDefaultNode(nodeId, out int stage, out string trackId)) continue;
                node.FindPropertyRelative("_requiredStage").intValue = stage;
                node.FindPropertyRelative("_position").vector2Value = GetNodePosition(stage, trackId);
                SetPrerequisites(node.FindPropertyRelative("_prerequisites"), GetPrerequisites(stage, trackId));
            }
            serialized.FindProperty("_layoutVersion").intValue = TOPOLOGY_VERSION;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ValidateAndSave(definition);
        }

        /// <summary>알려진 기본 ID만 해석해 스테이지와 노드 계열을 반환하고 사용자 노드면 false를 반환한다.</summary>
        private static bool TryReadDefaultNode(string nodeId, out int stage, out string trackId)
        {
            stage = 0;
            trackId = null;
            if (string.IsNullOrEmpty(nodeId) || !nodeId.StartsWith("stage_", StringComparison.Ordinal)) return false;
            int separator = nodeId.IndexOf('.');
            if (separator < 0 || !int.TryParse(nodeId.Substring(6, separator - 6), out stage)) return false;
            trackId = nodeId.Substring(separator + 1);
            if (trackId == REGEN_NODE || trackId == ENERGY_NODE || trackId == RAM_NODE) return stage >= 1 && stage <= 10;
            if (!trackId.StartsWith("rune.", StringComparison.Ordinal) || stage < 1 || stage > _runeOrder.Length) return false;
            return trackId.Substring(5) == _runeOrder[stage - 1];
        }

        /// <summary>트랙마다 스테이지 열과 행을 달리 배치해 정사각형 노드 사이에 대각선 경로가 교차하게 한다.</summary>
        private static Vector2 GetNodePosition(int stage, string trackId)
        {
            int track = trackId == REGEN_NODE ? 0 : trackId == ENERGY_NODE ? 1 : trackId == RAM_NODE ? 2 : 3;
            float stageOffset = (stage - 1) * STAGE_SPACING;
            float trackOffset = track * TRACK_SPACING;
            float stagger = stage % 2 == 0 && track % 2 == 1 ? 34 : 0;
            return new Vector2(46 + stageOffset + stagger, 40 + trackOffset);
        }

        /// <summary>현재 트랙이 이전 스테이지의 서로 다른 노드들을 요구하도록 교차 선행 ID를 반환한다.</summary>
        private static string[] GetPrerequisites(int stage, string trackId)
        {
            if (stage == 1)
            {
                if (trackId == REGEN_NODE) return Array.Empty<string>();
                if (trackId == ENERGY_NODE || trackId == RAM_NODE) return new[] { NodeId(1, REGEN_NODE) };
                return new[] { NodeId(1, ENERGY_NODE), NodeId(1, RAM_NODE) };
            }

            int previousStage = stage - 1;
            if (trackId == REGEN_NODE)
                return new[] { NodeId(previousStage, ENERGY_NODE), RuneNodeId(previousStage) };
            if (trackId == ENERGY_NODE)
                return new[] { NodeId(previousStage, REGEN_NODE), NodeId(previousStage, RAM_NODE) };
            if (trackId == RAM_NODE)
                return new[] { NodeId(previousStage, REGEN_NODE), RuneNodeId(previousStage) };
            return new[] { NodeId(previousStage, ENERGY_NODE), NodeId(previousStage, RAM_NODE), RuneNodeId(previousStage) };
        }

        /// <summary>스테이지와 기본 트랙 이름으로 세이브에 연결되는 안정적인 노드 ID를 반환한다.</summary>
        private static string NodeId(int stage, string trackId)
        {
            return "stage_" + stage.ToString("00") + "." + trackId;
        }

        /// <summary>스테이지 순서에 해당하는 기본 룬 해금 노드 ID를 반환한다.</summary>
        private static string RuneNodeId(int stage)
        {
            return NodeId(stage, "rune." + _runeOrder[stage - 1]);
        }

        /// <summary>능력치 노드와 기본 레벨별 비용·효과량·선행 노드 구성을 직렬화 목록에 추가한다.</summary>
        private static void AddStatNode(SerializedProperty nodes, int stage, string trackId, string titleKey,
            string descriptionKey, UpgradeEffectType effectType, int baseCost, float amount)
        {
            string nodeId = NodeId(stage, trackId);
            float stageScale = Mathf.Pow(1.12f, stage - 1);
            int firstCost = Mathf.Max(1, Mathf.RoundToInt(baseCost * stageScale));
            int secondCost = Mathf.Max(firstCost + 1, Mathf.RoundToInt(baseCost * 1.6f * stageScale));
            Vector2 position = GetNodePosition(stage, trackId);
            AddNode(nodes, nodeId, stage, titleKey, descriptionKey, position, effectType, null,
                GetPrerequisites(stage, trackId), new[] { firstCost, secondCost }, new[] { amount, amount });
        }

        /// <summary>현재 룬 하나를 이전 스테이지 능력치와 룬에 교차 연결된 해금 노드로 추가한다.</summary>
        private static void AddRuneNode(SerializedProperty nodes, int stage, string runeId)
        {
            if (!GameData.Runes.TryGet(runeId, out RuneDefinition rune))
                throw new InvalidOperationException("기본 업그레이드 트리에서 룬을 찾을 수 없습니다: " + runeId);
            AddNode(nodes, NodeId(stage, "rune." + runeId), stage, "ui.tree.unlockRune", "ui.tree.unlockRuneDescription",
                GetNodePosition(stage, "rune." + runeId), UpgradeEffectType.RuneUnlock, runeId,
                GetPrerequisites(stage, "rune." + runeId), new[] { rune.UnlockCost }, new[] { 0f });
        }

        /// <summary>새 노드와 레벨별 구매 값을 기록하고 선행 조건 목록을 채운다.</summary>
        private static void AddNode(SerializedProperty nodes, string nodeId, int stage, string titleKey,
            string descriptionKey, Vector2 position, UpgradeEffectType effectType, string runeId,
            string[] prerequisiteIds, int[] costs, float[] amounts)
        {
            int index = nodes.arraySize;
            nodes.InsertArrayElementAtIndex(index);
            SerializedProperty node = nodes.GetArrayElementAtIndex(index);
            node.FindPropertyRelative("_id").stringValue = nodeId;
            node.FindPropertyRelative("_requiredStage").intValue = stage;
            node.FindPropertyRelative("_titleKey").stringValue = titleKey;
            node.FindPropertyRelative("_descriptionKey").stringValue = descriptionKey;
            node.FindPropertyRelative("_position").vector2Value = position;
            node.FindPropertyRelative("_effectType").enumValueIndex = (int)effectType;
            node.FindPropertyRelative("_runeId").stringValue = runeId;
            SetPrerequisites(node.FindPropertyRelative("_prerequisites"), prerequisiteIds);

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

        /// <summary>기존 선행 조건 배열을 지우고 전달된 노드 ID를 1레벨 요구량으로 설정한다.</summary>
        private static void SetPrerequisites(SerializedProperty prerequisites, string[] prerequisiteIds)
        {
            prerequisites.ClearArray();
            for (int index = 0; index < prerequisiteIds.Length; index++)
            {
                prerequisites.InsertArrayElementAtIndex(index);
                SerializedProperty prerequisite = prerequisites.GetArrayElementAtIndex(index);
                prerequisite.FindPropertyRelative("_nodeId").stringValue = prerequisiteIds[index];
                prerequisite.FindPropertyRelative("_requiredLevel").intValue = 1;
            }
        }

        /// <summary>변경된 트리를 검증하고 저장하며 설정 오류가 있으면 씬 생성을 중단한다.</summary>
        private static void ValidateAndSave(UpgradeTreeDefinition definition)
        {
            if (!definition.Validate(out string error)) throw new InvalidOperationException(error);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
        }
    }
}
