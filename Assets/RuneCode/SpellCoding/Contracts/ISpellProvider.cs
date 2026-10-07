using System;

namespace RuneCode
{
    /// <summary>
    /// 마법 코딩 영역 밖의 기능(도크, 미션, 출격, 헤더)이 현재 마법을 읽는 유일한 경계다.
    /// 그래프 편집 API나 SpellGraph 내부 구조에 의존하지 않고 컴파일 결과만 사용한다.
    /// </summary>
    public interface ISpellProvider
    {
        string SpellId { get; }
        string SpellName { get; }
        CompileResult CompileResult { get; }

        /// <summary>컴파일 결과가 갱신될 때마다 호출된다.</summary>
        event Action Compiled;

        /// <summary>현재 마법을 즉시 다시 컴파일하고 Compiled를 호출한다.</summary>
        void Recompile();
    }
}
