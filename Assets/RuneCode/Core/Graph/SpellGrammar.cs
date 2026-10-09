namespace RuneCode
{
    /// <summary>
    /// 마법 비주얼 스크립팅 문법(Trigger → Shape(Element) → Behavior, Trigger → Apply(Element))의 블록 ID·카테고리·포트와
    /// 컴파일 내부 표현인 Inline Magic 파라미터 값 사이의 대응을 정의한다. 규범은 「마법 비주얼 스크립팅 — 문법 구현 백서」다.
    /// </summary>
    public static class SpellGrammar
    {
        public const string CATEGORY_SHAPE = "shape";
        public const string CATEGORY_BEHAVIOR = "behavior";
        public const string CATEGORY_ELEMENT = "element";
        public const string CATEGORY_METHOD = "method";
        public const string CATEGORY_INTERNAL = "internal";
        public const string CATEGORY_CORE = "core";
        public const string CATEGORY_MODIFIER = "modifier";
        public const string CATEGORY_FLOW = "flow";
        public const string CATEGORY_LEGACY_FORM = "form";

        public const string DIRECTION_IN = "in";
        public const string DIRECTION_OUT = "out";

        public const string EXEC_KIND = "exec";
        public const string MODIFIER_KIND = "mod";
        public const string CHAIN_KIND = "chain";

        public const string EXEC_PORT = "exec";
        public const string MODIFIER_PORT = "mod";
        public const string CHAIN_IN = "in";
        public const string CHAIN_OUT = "next";
        public const string ON_HIT_PORT = "onHit";
        public const string ON_EXPIRE_PORT = "onExpire";
        public const string ON_FIRST_HIT_OR_EXPIRE_PORT = "onFirstHitOrExpire";
        public const string LEGACY_COMPLETE_PORT = "onComplete";

        public const string PORT_ACTIVE = "active";
        public const string PORT_DEPRECATED = "deprecated";

        public const string FORM_BOLT = "bolt";
        public const string FORM_BURST = "burst";
        public const string FORM_ORBIT = "orbit";
        public const string FORM_ZONE = "zone";

        public const string MAGIC_TYPE_SPHERE = "sphere";
        public const string MAGIC_TYPE_BOX = "box";
        public const string MAGIC_TYPE_BUFF = "buff";

        public const string INLINE_RUNE = "magic.inline";
        public const string CALL_RUNE = "spell.call";
        public const string CORE_RUNE = "core.cast";
        public const string APPLY_RUNE = "behavior.apply";

        public const string ELEMENT_PARAM = "element";
        public const string TRIGGER_PARAM = "trigger";
        public const string POWER_PARAM = "power";
        public const string BUFF_DURATION_PARAM = "buffDuration";

        public const string TRIGGER_ON_ATTACK = "onAttack";
        public const string TRIGGER_ON_MOVE = "onMove";
        public const string TRIGGER_ON_DASH_START = "onDashStart";
        public const string TRIGGER_ON_DASH_END = "onDashEnd";
        public const string TRIGGER_ON_HIT_TAKEN = "onHitTaken";

        public const string ELEMENT_NEUTRAL = "neutral";
        public const string ELEMENT_CATEGORY_ELEMENTAL = "elemental";
        public const string ELEMENT_CATEGORY_FUNCTIONAL = "functional";

        private const string SHAPE_PREFIX = "shape.";
        private const string BEHAVIOR_PREFIX = "behavior.";
        private const string ELEMENT_PREFIX = "element.";

        /// <summary>Shape 블록(shape.sphere 등)이면 Inline Magic의 magicType 값(sphere 등)을, 아니면 null을 반환한다.</summary>
        public static string MagicTypeOf(string runeId) => ValueOf(runeId, SHAPE_PREFIX);

        /// <summary>
        /// Behavior 블록이면 Inline Magic의 form 값을 반환한다. 내부 값은 이전 문법을 유지한다
        /// (Launch=launch, Burst=explosion, Orbit=orbit, Persist·Apply=remain). Behavior가 아니면 null을 반환한다.
        /// </summary>
        public static string FormOf(string runeId)
        {
            switch (ValueOf(runeId, BEHAVIOR_PREFIX))
            {
                case "launch": return "launch";
                case "burst": return "explosion";
                case "orbit": return "orbit";
                case "persist": return "remain";
                case "apply": return "remain";
                default: return null;
            }
        }

        /// <summary>속성 식별자(neutral, fire 등)에 해당하는 속성 정의 룬 ID(element.neutral 등)를 반환한다.</summary>
        public static string ElementRune(string elementId) => ELEMENT_PREFIX + elementId;

        /// <summary>Shape의 체인 출력에서 Apply를 제외한 Behavior의 체인 입력으로 가는 연결인지 반환한다.</summary>
        public static bool IsValidChainLink(RuneDefinition from, RuneDefinition to)
        {
            return from.Category == CATEGORY_SHAPE && to.Category == CATEGORY_BEHAVIOR && to.Id != APPLY_RUNE;
        }

        /// <summary>개체 이벤트 출력 포트(onHit, onExpire, onFirstHitOrExpire, 이전 onComplete)인지 반환한다.</summary>
        public static bool IsEventPort(string portId)
        {
            return portId == ON_HIT_PORT || portId == ON_EXPIRE_PORT || portId == ON_FIRST_HIT_OR_EXPIRE_PORT || portId == LEGACY_COMPLETE_PORT;
        }

        /// <summary>룬 ID가 지정 접두사로 시작하면 접두사 뒤 값을, 아니면 null을 반환한다.</summary>
        private static string ValueOf(string runeId, string prefix)
        {
            if (string.IsNullOrEmpty(runeId) || !runeId.StartsWith(prefix, System.StringComparison.Ordinal)) return null;
            return runeId.Substring(prefix.Length);
        }
    }
}
