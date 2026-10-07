using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    [Serializable]
    public sealed class LocalizedEntry
    {
        [Header("현지화 항목")]
        [SerializeField] private string _key;
        [SerializeField] private string _value;
        public string Key => _key;
        public string Value => _value;
    }

    [Serializable]
    public sealed class LocalizationData
    {
        [Header("한국어 문자열")]
        [SerializeField] private LocalizedEntry[] _entries;
        public IReadOnlyList<LocalizedEntry> Entries => _entries;
    }

    public static class GameData
    {
        private static RuneCatalog _runes;
        private static BalanceData _balance;
        private static IReadOnlyList<SpellGraph> _spells;
        private static Dictionary<string, string> _strings;
        public static bool IsLoaded => _runes != null;
        public static RuneCatalog Runes => _runes;
        public static BalanceData Balance => _balance;
        public static IReadOnlyList<SpellGraph> Spells => _spells;

        /// <summary>Resources의 룬·밸런스·현지화·시작 마법 JSON을 한 번 읽고 스키마를 검증한다.</summary>
        public static void Load()
        {
            if (IsLoaded) return;
            LocalizationData strings = JsonUtility.FromJson<LocalizationData>(ReadResource("strings.ko"));
            if (strings?.Entries == null) throw new FormatException("한국어 문자열 데이터가 없습니다.");
            _strings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (LocalizedEntry entry in strings.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Key) || !_strings.TryAdd(entry.Key, entry.Value))
                    throw new FormatException("중복되거나 잘못된 한국어 문자열 키입니다.");
            }
            RuneCatalog runes = RuneCatalog.FromJson(ReadResource("runes"));
            BalanceData balance = BalanceData.FromJson(ReadResource("balance"));
            List<SpellGraph> spells = new List<SpellGraph>
            {
                ShareCodec.Deserialize(ReadResource("spells/firebolt")),
                ShareCodec.Deserialize(ReadResource("spells/shockwave")),
                ShareCodec.Deserialize(ReadResource("spells/triplefire"))
            };
            foreach (SpellGraph graph in spells)
            {
                CompileResult result = GraphCompiler.Compile(graph, runes, balance, runes.StartRunes, int.MaxValue);
                if (!result.Ok) throw new FormatException("시작 마법이 유효하지 않습니다: " + graph.Name);
            }
            _balance = balance;
            _spells = spells;
            _runes = runes;
        }

        /// <summary>현지화 키의 한국어 문구를 반환하고 누락된 키는 그대로 표시한다.</summary>
        public static string L(string key)
        {
            if (_strings != null && _strings.TryGetValue(key, out string value)) return value;
            return key;
        }

        /// <summary>RuneCode Resources 경로의 필수 텍스트 자산을 읽는다.</summary>
        private static string ReadResource(string path)
        {
            TextAsset asset = Resources.Load<TextAsset>("RuneCode/" + path);
            if (asset == null) throw new FormatException("필수 데이터 자산이 없습니다: " + path);
            return asset.text;
        }
    }
}
