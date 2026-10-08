using System;
using System.Collections.Generic;
using System.Globalization;

namespace RuneCode
{
    public enum SpellNodeKind { Cast, Projectile, Amplify, Add, Split, Fork, Join, Branch, OnHit }

    public enum AimMode { Cursor, Nearest }

    public enum BranchCondition { TargetDistance, TargetHpPercent, Mana, Power, Generation, PassCount }

    public static class SpellNodes
    {
        private const string NUMBER_FORMAT = "0.0#";
        private const string FORMULA_POWER_KEY = "formula.power";
        private const string FORMULA_COUNT_KEY = "formula.count";

        public const string CAST_ID = "cast";
        public const string PROJECTILE_ID = "projectile";
        public const string AMPLIFY_ID = "amplify";
        public const string ADD_ID = "add";
        public const string SPLIT_ID = "split";
        public const string FORK_ID = "fork";
        public const string JOIN_ID = "join";
        public const string BRANCH_ID = "branch";
        public const string ON_HIT_ID = "onHit";

        public const string PARAM_AIM = "aim";
        public const string PARAM_LOAD = "load";
        public const string PARAM_RATIO = "ratio";
        public const string PARAM_SHARE = "share";
        public const string PARAM_CONDITION = "condition";
        public const string PARAM_COMPARE = "compare";
        public const string PARAM_THRESHOLD = "threshold";

        public const string AIM_CURSOR = "cursor";
        public const string AIM_NEAREST = "nearest";
        public const string COMPARE_GE = "ge";
        public const string COMPARE_LE = "le";
        public const string CONDITION_TARGET_DISTANCE = "targetDistance";
        public const string CONDITION_TARGET_HP = "targetHpPercent";
        public const string CONDITION_MANA = "mana";
        public const string CONDITION_POWER = "power";
        public const string CONDITION_GENERATION = "generation";
        public const string CONDITION_PASS_COUNT = "passCount";

        private static readonly string[] _kindIds = { CAST_ID, PROJECTILE_ID, AMPLIFY_ID, ADD_ID, SPLIT_ID, FORK_ID, JOIN_ID, BRANCH_ID, ON_HIT_ID };
        private static readonly string[] _castParameterKeys = { PARAM_AIM, PARAM_LOAD };
        private static readonly string[] _amplifyParameterKeys = { PARAM_RATIO };
        private static readonly string[] _forkParameterKeys = { PARAM_SHARE };
        private static readonly string[] _branchParameterKeys = { PARAM_CONDITION, PARAM_COMPARE, PARAM_THRESHOLD };
        private static readonly string[] _aimOptions = { AIM_CURSOR, AIM_NEAREST };
        private static readonly string[] _compareOptions = { COMPARE_GE, COMPARE_LE };
        private static readonly string[] _conditionOptions =
        {
            CONDITION_TARGET_DISTANCE, CONDITION_TARGET_HP, CONDITION_MANA, CONDITION_POWER, CONDITION_GENERATION, CONDITION_PASS_COUNT
        };

        /// <summary>노드 ID 문자열을 종류 enum으로 바꾼다. 대소문자를 구분하며 알 수 없는 ID나 null이면 false를 반환한다.</summary>
        public static bool TryParse(string id, out SpellNodeKind kind)
        {
            for (int index = 0; index < _kindIds.Length; index++)
            {
                if (string.Equals(id, _kindIds[index], StringComparison.Ordinal))
                {
                    kind = (SpellNodeKind)index;
                    return true;
                }
            }
            kind = SpellNodeKind.Cast;
            return false;
        }

        /// <summary>노드 종류에 해당하는 노드 ID 문자열을 반환한다.</summary>
        public static string GetId(SpellNodeKind kind)
        {
            return _kindIds[(int)kind];
        }

        /// <summary>노드 종류의 입력 포트 수를 반환한다. 시전과 적중은 0, 합류는 2, 나머지는 1이다.</summary>
        public static int GetInputCount(SpellNodeKind kind)
        {
            switch (kind)
            {
                case SpellNodeKind.Cast:
                case SpellNodeKind.OnHit:
                    return 0;
                case SpellNodeKind.Join:
                    return 2;
                default:
                    return 1;
            }
        }

        /// <summary>노드 종류의 출력 포트 수를 반환한다. 분배와 분기는 2, 나머지는 1이다.</summary>
        public static int GetOutputCount(SpellNodeKind kind)
        {
            switch (kind)
            {
                case SpellNodeKind.Fork:
                case SpellNodeKind.Branch:
                    return 2;
                default:
                    return 1;
            }
        }

        /// <summary>노드 종류가 토큰을 머무르게 하는 시간(초)을 설정에서 읽어 반환한다. 투사체, 적중, 그 밖의 노드 순으로 값을 고른다.</summary>
        public static double GetDwell(SpellNodeKind kind, SpellSettings settings)
        {
            switch (kind)
            {
                case SpellNodeKind.Projectile:
                    return settings.ProjectileDwell;
                case SpellNodeKind.OnHit:
                    return settings.OnHitDwell;
                default:
                    return settings.DefaultDwell;
            }
        }

        /// <summary>토큰이 노드에 도착한 순간의 위력, 개수, 배율로 노드 비용을 계산해 반환한다. 시전과 적중은 0이고 그 밖은 비용 바닥값 이상이다.</summary>
        public static double GetCost(SpellNodeKind kind, double power, int count, double ratio, SpellSettings settings)
        {
            double floor = settings.CostFloor;
            switch (kind)
            {
                case SpellNodeKind.Projectile:
                    return Math.Max(floor, (double)settings.ProjectileCostPerShot * count);
                case SpellNodeKind.Amplify:
                    return Math.Max(floor, power * (ratio - 1.0) * settings.AmplifyCostFactor);
                case SpellNodeKind.Add:
                    return Math.Max(floor, settings.AddCost);
                case SpellNodeKind.Split:
                    return Math.Max(floor, power * settings.SplitCostFactor);
                case SpellNodeKind.Fork:
                    return Math.Max(floor, settings.ForkCost);
                case SpellNodeKind.Join:
                    return Math.Max(floor, settings.JoinCost);
                case SpellNodeKind.Branch:
                    return Math.Max(floor, settings.BranchCost);
                case SpellNodeKind.Cast:
                case SpellNodeKind.OnHit:
                default:
                    return 0.0;
            }
        }

        /// <summary>노드 종류가 편집기에 보여 줄 파라미터 키 목록을 표시 순서대로 반환한다. 파라미터가 없는 종류는 빈 목록이다.</summary>
        public static IReadOnlyList<string> GetParameterKeys(SpellNodeKind kind)
        {
            switch (kind)
            {
                case SpellNodeKind.Cast:
                    return _castParameterKeys;
                case SpellNodeKind.Amplify:
                    return _amplifyParameterKeys;
                case SpellNodeKind.Fork:
                    return _forkParameterKeys;
                case SpellNodeKind.Branch:
                    return _branchParameterKeys;
                default:
                    return Array.Empty<string>();
            }
        }

        /// <summary>파라미터 키가 숫자 입력을 쓰는지 판정한다. 적재량, 배율, 비율, 기준값이면 true이다.</summary>
        public static bool IsNumberParameter(string key)
        {
            switch (key)
            {
                case PARAM_LOAD:
                case PARAM_RATIO:
                case PARAM_SHARE:
                case PARAM_THRESHOLD:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>숫자 파라미터의 최소, 최대, 기본값, 조절 단위를 설정에서 읽어 반환한다. 숫자 파라미터가 아니면 모든 출력값을 0으로 하고 false를 반환한다.</summary>
        public static bool TryGetNumberRange(string key, SpellSettings settings, out float min, out float max, out float defaultValue, out float step)
        {
            switch (key)
            {
                case PARAM_LOAD:
                    min = settings.LoadMin;
                    max = settings.LoadMax;
                    defaultValue = settings.LoadDefault;
                    step = settings.LoadStep;
                    return true;
                case PARAM_RATIO:
                    min = settings.AmplifyMin;
                    max = settings.AmplifyMax;
                    defaultValue = settings.AmplifyDefault;
                    step = settings.AmplifyStep;
                    return true;
                case PARAM_SHARE:
                    min = settings.ForkShareMin;
                    max = settings.ForkShareMax;
                    defaultValue = settings.ForkShareDefault;
                    step = settings.ForkShareStep;
                    return true;
                case PARAM_THRESHOLD:
                    min = settings.BranchThresholdMin;
                    max = settings.BranchThresholdMax;
                    defaultValue = settings.BranchThresholdDefault;
                    step = settings.BranchThresholdStep;
                    return true;
                default:
                    min = 0f;
                    max = 0f;
                    defaultValue = 0f;
                    step = 0f;
                    return false;
            }
        }

        /// <summary>텍스트 파라미터 키의 선택 가능한 옵션 목록을 반환한다. 텍스트 파라미터가 아니면 빈 목록을 반환한다.</summary>
        public static IReadOnlyList<string> GetTextOptions(string key)
        {
            switch (key)
            {
                case PARAM_AIM:
                    return _aimOptions;
                case PARAM_COMPARE:
                    return _compareOptions;
                case PARAM_CONDITION:
                    return _conditionOptions;
                default:
                    return Array.Empty<string>();
            }
        }

        /// <summary>텍스트 파라미터 키의 기본 옵션 문자열을 반환한다. 해당 키가 없으면 빈 문자열을 반환한다.</summary>
        public static string GetDefaultText(string key)
        {
            switch (key)
            {
                case PARAM_AIM:
                    return AIM_CURSOR;
                case PARAM_COMPARE:
                    return COMPARE_GE;
                case PARAM_CONDITION:
                    return CONDITION_POWER;
                default:
                    return "";
            }
        }

        /// <summary>조준 모드 옵션 문자열을 AimMode로 바꾼다. 모르는 값이면 false를 반환한다.</summary>
        public static bool TryParseAim(string text, out AimMode aim)
        {
            switch (text)
            {
                case AIM_CURSOR:
                    aim = AimMode.Cursor;
                    return true;
                case AIM_NEAREST:
                    aim = AimMode.Nearest;
                    return true;
                default:
                    aim = AimMode.Cursor;
                    return false;
            }
        }

        /// <summary>분기 조건 옵션 문자열을 BranchCondition으로 바꾼다. 모르는 값이면 false를 반환한다.</summary>
        public static bool TryParseCondition(string text, out BranchCondition condition)
        {
            switch (text)
            {
                case CONDITION_TARGET_DISTANCE:
                    condition = BranchCondition.TargetDistance;
                    return true;
                case CONDITION_TARGET_HP:
                    condition = BranchCondition.TargetHpPercent;
                    return true;
                case CONDITION_MANA:
                    condition = BranchCondition.Mana;
                    return true;
                case CONDITION_POWER:
                    condition = BranchCondition.Power;
                    return true;
                case CONDITION_GENERATION:
                    condition = BranchCondition.Generation;
                    return true;
                case CONDITION_PASS_COUNT:
                    condition = BranchCondition.PassCount;
                    return true;
                default:
                    condition = BranchCondition.Power;
                    return false;
            }
        }

        /// <summary>노드 종류의 비용 공식을 현지화된 글자와 설정 값으로 만들어 반환한다. 시전과 적중은 "0"이고 그 밖은 비용 바닥값을 덧붙인다.</summary>
        public static string GetCostFormula(SpellNodeKind kind, double ratio, SpellSettings settings)
        {
            switch (kind)
            {
                case SpellNodeKind.Projectile:
                    return AppendCostFloor(FormatNumber(settings.ProjectileCostPerShot) + "×" + GameData.L(FORMULA_COUNT_KEY), settings);
                case SpellNodeKind.Amplify:
                    return AppendCostFloor(GameData.L(FORMULA_POWER_KEY) + "×(" + FormatNumber(ratio) + "−1)×" + FormatNumber(settings.AmplifyCostFactor), settings);
                case SpellNodeKind.Add:
                    return AppendCostFloor(FormatNumber(settings.AddCost), settings);
                case SpellNodeKind.Split:
                    return AppendCostFloor(GameData.L(FORMULA_POWER_KEY) + "×" + FormatNumber(settings.SplitCostFactor), settings);
                case SpellNodeKind.Fork:
                    return AppendCostFloor(FormatNumber(settings.ForkCost), settings);
                case SpellNodeKind.Join:
                    return AppendCostFloor(FormatNumber(settings.JoinCost), settings);
                case SpellNodeKind.Branch:
                    return AppendCostFloor(FormatNumber(settings.BranchCost), settings);
                case SpellNodeKind.Cast:
                case SpellNodeKind.OnHit:
                default:
                    return "0";
            }
        }

        /// <summary>비용 공식 문자열 끝에 비용 바닥값 표기를 덧붙여 반환한다.</summary>
        private static string AppendCostFloor(string formula, SpellSettings settings)
        {
            return formula + " (≥" + FormatNumber(settings.CostFloor) + ")";
        }

        /// <summary>숫자를 소수 첫째 자리 이상의 불변 문화권 문자열로 바꿔 반환한다.</summary>
        private static string FormatNumber(double value)
        {
            return value.ToString(NUMBER_FORMAT, CultureInfo.InvariantCulture);
        }
    }
}
