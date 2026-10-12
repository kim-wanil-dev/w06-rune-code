using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    /// <summary>외부 JSON의 배열 순서대로 표시하는 게임 도움말 목록이다.</summary>
    [Serializable]
    public sealed class EncyclopediaData
    {
        [Header("도움말 목록")]
        [SerializeField] private EncyclopediaEntry[] _entries;

        public IReadOnlyList<EncyclopediaEntry> Entries => _entries;
    }

    /// <summary>목차와 본문에서 함께 사용하는 도움말 제목과 설명이다.</summary>
    [Serializable]
    public sealed class EncyclopediaEntry
    {
        [Header("도움말 내용")]
        [SerializeField] private string _title;
        [SerializeField] private string _body;

        public string Title => _title;
        public string Body => _body;
    }
}
