using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    public static class LocalTelemetry
    {
        private const int MAX_ENTRIES = 500;
        private static readonly Queue<TelemetryEntry> _entries = new Queue<TelemetryEntry>();
        public static int Count => _entries.Count;

        /// <summary>시뮬레이션 tick과 종류·상세를 최대 500건의 로컬 링 버퍼에 기록한다.</summary>
        public static void Record(int tick, string kind, string detail)
        {
            if (_entries.Count == MAX_ENTRIES) _entries.Dequeue();
            _entries.Enqueue(new TelemetryEntry(tick, kind, detail));
        }

        /// <summary>콘솔이나 파일로 복사할 수 있는 전체 로컬 기록 JSON을 반환한다.</summary>
        public static string DumpJson()
        {
            return JsonUtility.ToJson(new TelemetryDump(_entries.ToArray()), true);
        }

        /// <summary>로컬 기록을 Unity 콘솔에 JSON 형식으로 출력한다.</summary>
        public static void DumpToConsole()
        {
            Debug.Log(DumpJson());
        }
    }

    [Serializable]
    public sealed class TelemetryEntry
    {
        [Header("로컬 기록")]
        [SerializeField] private int _tick;
        [SerializeField] private string _kind;
        [SerializeField] private string _detail;

        /// <summary>기록 시점·종류·상세를 직렬화 가능한 데이터로 보관한다.</summary>
        public TelemetryEntry(int tick, string kind, string detail)
        {
            _tick = tick;
            _kind = kind;
            _detail = detail;
        }
    }

    [Serializable]
    public sealed class TelemetryDump
    {
        [Header("로컬 기록 목록")]
        [SerializeField] private TelemetryEntry[] _entries;

        /// <summary>전달된 기록 배열을 JSON 내보내기용으로 보관한다.</summary>
        public TelemetryDump(TelemetryEntry[] entries) { _entries = entries; }
    }
}
