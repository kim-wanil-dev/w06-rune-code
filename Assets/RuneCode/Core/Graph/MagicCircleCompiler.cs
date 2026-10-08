using System;
using System.Collections.Generic;

namespace RuneCode
{
    public sealed class MagicCirclePlan
    {
        private readonly IReadOnlyList<MagicCircleSource> _circles;
        private readonly IReadOnlyList<MagicCircleInvocation> _invocations;
        public IReadOnlyList<MagicCircleSource> Circles => _circles;
        public IReadOnlyList<MagicCircleInvocation> Invocations => _invocations;

        /// <summary>컴파일된 Spell별 원과 함수 호출 링크를 읽기 전용 계획으로 저장한다.</summary>
        public MagicCirclePlan(IReadOnlyList<MagicCircleSource> circles, IReadOnlyList<MagicCircleInvocation> invocations)
        {
            _circles = circles ?? Array.Empty<MagicCircleSource>();
            _invocations = invocations ?? Array.Empty<MagicCircleInvocation>();
        }
    }

    public sealed class MagicCircleSource
    {
        private readonly string _id;
        private readonly string _name;
        private readonly string _trigger;
        private readonly IReadOnlyList<MagicCircleRing> _rings;
        public string Id => _id;
        public string Name => _name;
        public string Trigger => _trigger;
        public IReadOnlyList<MagicCircleRing> Rings => _rings;

        /// <summary>Spell ID, 표시 이름, 시작 Trigger와 소스 순서의 링을 저장한다.</summary>
        public MagicCircleSource(string id, string name, string trigger, IReadOnlyList<MagicCircleRing> rings)
        {
            _id = id;
            _name = name;
            _trigger = trigger;
            _rings = rings ?? Array.Empty<MagicCircleRing>();
        }
    }

    public sealed class MagicCircleRing
    {
        private readonly int _order;
        private readonly string _nodeId;
        private readonly string _kind;
        private readonly string _geometry;
        private readonly string _element;
        private readonly string _form;
        private readonly string _spellId;
        private readonly int _count;
        private readonly int _times;
        private readonly float _seconds;
        private readonly IReadOnlyList<string> _modifiers;
        private readonly IReadOnlyList<MagicCircleBranch> _branches;
        public int Order => _order;
        public string NodeId => _nodeId;
        public string Kind => _kind;
        public string Geometry => _geometry;
        public string Element => _element;
        public string Form => _form;
        public string SpellId => _spellId;
        public int Count => _count;
        public int Times => _times;
        public float Seconds => _seconds;
        public IReadOnlyList<string> Modifiers => _modifiers;
        public IReadOnlyList<MagicCircleBranch> Branches => _branches;

        /// <summary>실행 명령의 마법 분류, 호출 참조, 수식 및 이벤트 분기를 링에 보존한다.</summary>
        public MagicCircleRing(int order, SpellAction action, IReadOnlyList<MagicCircleBranch> branches)
        {
            _order = order;
            _nodeId = action.NodeId;
            _kind = action.Kind;
            _geometry = action.MagicType;
            _element = action.SourceElement;
            _form = action.Form;
            _spellId = action.CalledSpellId;
            _count = action.Count;
            _times = action.Times;
            _seconds = action.Seconds;
            _modifiers = action.Mods;
            _branches = branches ?? Array.Empty<MagicCircleBranch>();
        }
    }

    public sealed class MagicCircleBranch
    {
        private readonly string _port;
        private readonly IReadOnlyList<MagicCircleRing> _rings;
        public string Port => _port;
        public IReadOnlyList<MagicCircleRing> Rings => _rings;

        /// <summary>Event 또는 Flow 포트 이름과 해당 후속 링 순서를 저장한다.</summary>
        public MagicCircleBranch(string port, IReadOnlyList<MagicCircleRing> rings)
        {
            _port = port;
            _rings = rings ?? Array.Empty<MagicCircleRing>();
        }
    }

    public sealed class MagicCircleInvocation
    {
        private readonly string _sourceCircleId;
        private readonly string _sourceNodeId;
        private readonly string _spellId;
        private readonly string _targetCircleId;
        public string SourceCircleId => _sourceCircleId;
        public string SourceNodeId => _sourceNodeId;
        public string SpellId => _spellId;
        public string TargetCircleId => _targetCircleId;

        /// <summary>호출 링과 대상 보조 원을 연결하는 stable ID 참조를 저장한다.</summary>
        public MagicCircleInvocation(string sourceCircleId, string sourceNodeId, string spellId, string targetCircleId)
        {
            _sourceCircleId = sourceCircleId;
            _sourceNodeId = sourceNodeId;
            _spellId = spellId;
            _targetCircleId = targetCircleId;
        }
    }

    public static class MagicCircleCompiler
    {
        /// <summary>CompiledSpell IR을 원·링·호출 링크로 투영하고 호출 Spell을 보조 원으로 수집한다.</summary>
        public static MagicCirclePlan Compile(CompiledSpell spell)
        {
            if (spell == null) throw new ArgumentNullException(nameof(spell));
            List<MagicCircleSource> circles = new List<MagicCircleSource>();
            List<MagicCircleInvocation> invocations = new List<MagicCircleInvocation>();
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            AppendCircle(spell, circles, invocations, visited);
            return new MagicCirclePlan(circles, invocations);
        }

        /// <summary>각 Spell을 한 번 원으로 만들고 실행 링의 호출 대상을 재귀적으로 수집한다.</summary>
        private static void AppendCircle(CompiledSpell spell, List<MagicCircleSource> circles,
            List<MagicCircleInvocation> invocations, HashSet<string> visited)
        {
            string circleId = CircleId(spell);
            if (!visited.Add(circleId)) return;
            List<MagicCircleRing> rings = BuildRings(spell.Root, circleId, invocations);
            circles.Add(new MagicCircleSource(circleId, spell.Name, spell.Trigger, rings));
            foreach (SpellAction action in spell.Root) AppendCalledCircles(action, circles, invocations, visited);
        }

        /// <summary>실행 목록을 연결 순서대로 링으로 변환하고 각 Event·Flow 분기를 중첩한다.</summary>
        private static List<MagicCircleRing> BuildRings(IReadOnlyList<SpellAction> actions, string circleId,
            List<MagicCircleInvocation> invocations)
        {
            List<MagicCircleRing> rings = new List<MagicCircleRing>(actions.Count);
            for (int index = 0; index < actions.Count; index++)
            {
                SpellAction action = actions[index];
                List<MagicCircleBranch> branches = new List<MagicCircleBranch>();
                AddBranch(SpellGrammar.ON_HIT_PORT, action.OnHit, circleId, invocations, branches);
                AddBranch(SpellGrammar.ON_EXPIRE_PORT, action.OnExpire, circleId, invocations, branches);
                AddBranch(SpellGrammar.ON_FIRST_HIT_OR_EXPIRE_PORT, action.OnFirstHitOrExpire, circleId, invocations, branches);
                AddBranch("then", action.Then, circleId, invocations, branches);
                AddBranch("else", action.Else, circleId, invocations, branches);
                AddBranch("body", action.Body, circleId, invocations, branches);
                AddBranch("next", action.Next, circleId, invocations, branches);
                rings.Add(new MagicCircleRing(index, action, branches));
                if (action.Kind == "call" && action.CalledSpell != null)
                    invocations.Add(new MagicCircleInvocation(circleId, action.NodeId, action.CalledSpellId, CircleId(action.CalledSpell)));
            }
            return rings;
        }

        /// <summary>비어 있지 않은 출력 분기를 링 목록에 추가하고 내부 호출 링크를 보존한다.</summary>
        private static void AddBranch(string port, IReadOnlyList<SpellAction> actions, string circleId,
            List<MagicCircleInvocation> invocations, List<MagicCircleBranch> branches)
        {
            if (actions.Count == 0) return;
            branches.Add(new MagicCircleBranch(port, BuildRings(actions, circleId, invocations)));
        }

        /// <summary>현재 Spell의 모든 분기에서 발견한 호출 대상을 각각 보조 원으로 추가한다.</summary>
        private static void AppendCalledCircles(SpellAction action, List<MagicCircleSource> circles,
            List<MagicCircleInvocation> invocations, HashSet<string> visited)
        {
            if (action.Kind == "call" && action.CalledSpell != null) AppendCircle(action.CalledSpell, circles, invocations, visited);
            AppendCalledInBranches(action.OnHit, circles, invocations, visited);
            AppendCalledInBranches(action.OnExpire, circles, invocations, visited);
            AppendCalledInBranches(action.OnFirstHitOrExpire, circles, invocations, visited);
            AppendCalledInBranches(action.Then, circles, invocations, visited);
            AppendCalledInBranches(action.Else, circles, invocations, visited);
            AppendCalledInBranches(action.Body, circles, invocations, visited);
            AppendCalledInBranches(action.Next, circles, invocations, visited);
        }

        /// <summary>중첩 실행 목록을 순서대로 순회해 호출 대상 원을 수집한다.</summary>
        private static void AppendCalledInBranches(IReadOnlyList<SpellAction> actions, List<MagicCircleSource> circles,
            List<MagicCircleInvocation> invocations, HashSet<string> visited)
        {
            foreach (SpellAction action in actions) AppendCalledCircles(action, circles, invocations, visited);
        }

        /// <summary>Spell ID 또는 시그니처에서 안정적인 원 식별자를 만든다.</summary>
        private static string CircleId(CompiledSpell spell)
        {
            return string.IsNullOrEmpty(spell.Id) ? "circle." + spell.Signature : spell.Id;
        }
    }
}
