using System;
using System.Collections.Generic;

namespace RuneCode
{
    /// <summary>
    /// 세이브 객체의 마법 보관함 읽기·쓰기를 제공하고 일반 모드일 때만 저장 파일에 기록한다.
    /// 세이브 참조를 Func으로 받아 진행 초기화로 객체가 교체돼도 항상 최신 세이브를 사용한다.
    /// </summary>
    public sealed class SessionSpellStorage : ISpellStorage
    {
        private readonly Func<PlayerSave> _getSave;
        private readonly Func<bool> _canPersist;

        public IReadOnlyList<SpellGraph> Library => _getSave().Library;
        public string ActiveSpellId => _getSave().ActiveSpellId;

        /// <summary>현재 세이브와 파일 저장 허용 여부를 반환하는 함수를 받아 보관함 접근을 연다.</summary>
        public SessionSpellStorage(Func<PlayerSave> getSave, Func<bool> canPersist)
        {
            _getSave = getSave;
            _canPersist = canPersist;
        }

        /// <summary>ID와 일치하는 보관함 마법을 반환하며 없으면 null을 반환한다.</summary>
        public SpellGraph Find(string spellId)
        {
            return _getSave().FindGraph(spellId);
        }

        /// <summary>같은 ID는 교체하고 새 ID는 보관함에 독립 사본으로 추가한다.</summary>
        public void Store(SpellGraph graph)
        {
            _getSave().StoreGraph(graph);
        }

        /// <summary>보관함에 존재하는 마법 ID를 활성 편집·첫 슬롯 참조로 설정한다.</summary>
        public void SetActive(string spellId)
        {
            _getSave().SetActiveGraph(spellId);
        }

        /// <summary>ID의 마법을 삭제하고 해당 마법을 참조하는 장착 슬롯을 비운다.</summary>
        public void Remove(string spellId)
        {
            _getSave().RemoveGraph(spellId);
        }

        /// <summary>현재 세이브를 저장 파일에 기록하고 실패 시 경고 문구를 반환한다.</summary>
        public bool Persist(out string warning)
        {
            if (!_canPersist())
            {
                warning = null;
                return true;
            }
            if (SaveStore.Write(_getSave()))
            {
                warning = null;
                return true;
            }
            warning = SaveStore.LastWarning;
            return false;
        }
    }
}
