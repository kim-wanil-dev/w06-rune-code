namespace RuneCode
{
    /// <summary>
    /// 마법 비주얼 스크립팅 문법(시작 조건 → 마법 타입 → 속성 부여 → 형태)의 블록 ID와
    /// 컴파일 내부 표현인 Inline Magic 파라미터 값 사이의 대응을 정의한다.
    /// </summary>
    public static class SpellGrammar
    {
        public const string CATEGORY_MAGIC_TYPE = "magicType";
        public const string CATEGORY_ELEMENT = "element";
        public const string CATEGORY_SHAPE = "shape";
        public const string CATEGORY_METHOD = "method";
        public const string CATEGORY_INTERNAL = "internal";

        public const string CHAIN_KIND = "chain";
        public const string CHAIN_IN = "in";
        public const string CHAIN_OUT = "next";

        public const string INLINE_RUNE = "magic.inline";
        public const string CALL_RUNE = "spell.call";
        public const string CORE_RUNE = "core.cast";

        private const string TYPE_PREFIX = "type.";
        private const string ELEMENT_PREFIX = "element.";
        private const string SHAPE_PREFIX = "shape.";

        /// <summary>마법 타입 블록이면 Inline Magic의 magicType 값을, 아니면 null을 반환한다.</summary>
        public static string MagicTypeOf(string runeId) => ValueOf(runeId, TYPE_PREFIX);

        /// <summary>속성 부여 블록이면 Inline Magic의 element 값을, 아니면 null을 반환한다.</summary>
        public static string ElementOf(string runeId) => ValueOf(runeId, ELEMENT_PREFIX);

        /// <summary>형태 블록이면 Inline Magic의 form 값을, 아니면 null을 반환한다.</summary>
        public static string ShapeOf(string runeId) => ValueOf(runeId, SHAPE_PREFIX);

        /// <summary>magicType 값에 해당하는 마법 타입 블록 ID를 반환한다.</summary>
        public static string TypeRune(string magicType) => TYPE_PREFIX + magicType;

        /// <summary>element 값에 해당하는 속성 부여 블록 ID를 반환한다.</summary>
        public static string ElementRune(string element) => ELEMENT_PREFIX + element;

        /// <summary>form 값에 해당하는 형태 블록 ID를 반환한다.</summary>
        public static string ShapeRune(string form) => SHAPE_PREFIX + form;

        /// <summary>체인 출력에서 체인 입력으로의 연결이 문법 순서(타입→속성/형태, 속성→형태)를 지키는지 반환한다.</summary>
        public static bool IsValidChainLink(RuneDefinition from, RuneDefinition to)
        {
            if (from.Category == CATEGORY_MAGIC_TYPE) return to.Category == CATEGORY_ELEMENT || to.Category == CATEGORY_SHAPE;
            if (from.Category == CATEGORY_ELEMENT) return to.Category == CATEGORY_SHAPE;
            return false;
        }

        /// <summary>룬 ID가 지정 접두사로 시작하면 접두사 뒤 값을, 아니면 null을 반환한다.</summary>
        private static string ValueOf(string runeId, string prefix)
        {
            if (string.IsNullOrEmpty(runeId) || !runeId.StartsWith(prefix, System.StringComparison.Ordinal)) return null;
            return runeId.Substring(prefix.Length);
        }
    }
}
