using System.Collections.Generic;

namespace RuneCode
{
    /// <summary>마법 코딩 영역이 저장 포맷(PlayerSave)을 알지 않고 마법 보관함을 읽고 쓰는 경계다.</summary>
    public interface ISpellStorage
    {
        IReadOnlyList<SpellGraph> Library { get; }
        string ActiveSpellId { get; }

        SpellGraph Find(string spellId);

        /// <summary>같은 ID는 교체하고 새 ID는 보관함에 독립 사본으로 추가한다.</summary>
        void Store(SpellGraph graph);

        void SetActive(string spellId);
        void Remove(string spellId);

        /// <summary>저장 파일에 기록하고 실패하면 경고 문구를 반환한다.</summary>
        bool Persist(out string warning);
    }
}
