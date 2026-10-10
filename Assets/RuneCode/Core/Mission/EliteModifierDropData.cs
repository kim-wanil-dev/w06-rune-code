using System;
using System.Collections.Generic;
using System.Globalization;

namespace RuneCode
{
    /// <summary>엘리트 Modifier 등급 확률표의 한 항목이다. Percent의 합은 100이다.</summary>
    public readonly struct EliteModifierGradeChance
    {
        private readonly string _grade;
        private readonly int _percent;
        public string Grade => _grade;
        public int Percent => _percent;

        /// <summary>등급 문자와 퍼센트 확률로 항목을 만든다.</summary>
        internal EliteModifierGradeChance(string grade, int percent) { _grade = grade; _percent = percent; }
    }

    /// <summary>스테이지 클리어 Modifier 선택 보상 설정이다. 후보 등급과 제시 수, 선택 수를 담는다.</summary>
    public sealed class EliteModifierClearChoice
    {
        private readonly string _grade;
        private readonly int _choiceCount;
        private readonly int _selectCount;
        public string Grade => _grade;
        public int ChoiceCount => _choiceCount;
        public int SelectCount => _selectCount;

        /// <summary>후보 등급, 제시 수, 선택 수로 선택 보상 설정을 만든다.</summary>
        internal EliteModifierClearChoice(string grade, int choiceCount, int selectCount)
        { _grade = grade; _choiceCount = choiceCount; _selectCount = selectCount; }
    }

    /// <summary>
    /// 엘리트 Modifier 등급표 조회 창구다. B 워커의 정식 로더가 이 인터페이스를 구현해 갈아끼운다.
    /// 데이터는 tables/elite_modifier_drops.json을 기준으로 한다.
    /// </summary>
    public interface IEliteModifierDropSource
    {
        /// <summary>스테이지의 엘리트 Modifier 등급 확률(C→S 파일 순서)을 반환한다. 엘리트 Modifier 드롭이 없는 스테이지(보스)면 null을 반환하고, 표에 없는 스테이지는 마지막 일반 스테이지의 확률을 반환한다.</summary>
        IReadOnlyList<EliteModifierGradeChance> GetGradeChances(int stage);

        /// <summary>지정 등급의 값이 null이 아닌 Modifier 룬 ID 목록을 파일 순서대로 반환한다.</summary>
        IReadOnlyList<string> GetModifiersWithGrade(string grade);

        /// <summary>스테이지 클리어 Modifier 선택 보상 설정을 반환하고 없는 스테이지면 null을 반환한다.</summary>
        EliteModifierClearChoice GetClearChoice(int stage);
    }

    /// <summary>
    /// elite_modifier_drops.json의 필요한 조회만 담는 임시 리더다. B 워커의 로더로 교체할 때
    /// RuneSimulation 생성자의 생성 지점과 이 클래스를 함께 바꾼다.
    /// </summary>
    public sealed class EliteModifierDropTable : IEliteModifierDropSource
    {
        private readonly Dictionary<int, List<EliteModifierGradeChance>> _gradeChances = new Dictionary<int, List<EliteModifierGradeChance>>();
        private readonly Dictionary<int, EliteModifierClearChoice> _clearChoices = new Dictionary<int, EliteModifierClearChoice>();
        private readonly Dictionary<string, List<string>> _modifiersByGrade = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private List<EliteModifierGradeChance> _fallbackChances;

        /// <summary>스테이지의 등급 확률을 반환하고 보스 스테이지는 null, 표에 없는 스테이지는 마지막 일반 스테이지 확률을 반환한다.</summary>
        public IReadOnlyList<EliteModifierGradeChance> GetGradeChances(int stage)
        {
            if (_gradeChances.TryGetValue(stage, out List<EliteModifierGradeChance> chances)) return chances;
            return _fallbackChances;
        }

        /// <summary>지정 등급의 값이 null이 아닌 Modifier 룬 ID 목록을 반환하고 없는 등급이면 빈 목록을 반환한다.</summary>
        public IReadOnlyList<string> GetModifiersWithGrade(string grade)
        {
            if (grade != null && _modifiersByGrade.TryGetValue(grade, out List<string> modifiers)) return modifiers;
            return Array.Empty<string>();
        }

        /// <summary>스테이지 클리어 Modifier 선택 보상 설정을 반환하고 없는 스테이지면 null을 반환한다.</summary>
        public EliteModifierClearChoice GetClearChoice(int stage)
        {
            _clearChoices.TryGetValue(stage, out EliteModifierClearChoice choice);
            return choice;
        }

        /// <summary>최소 JSON 파서로 elite_modifier_drops.json을 읽어 조회 표를 만든다. 파일 형식이 어긋나면 데이터 오류를 발생시킨다.</summary>
        public static EliteModifierDropTable FromJson(string json)
        {
            if (!(MiniJson.Parse(json) is List<KeyValuePair<string, object>> root)
                || !(JsonGet(root, "_modifierGradeEffects") is List<object> effects)
                || !(JsonGet(root, "_stageDropTables") is List<object> stageTables))
                throw new FormatException("elite_modifier_drops.json: 필수 목록이 없습니다.");
            var table = new EliteModifierDropTable();
            foreach (object effectObject in effects)
            {
                if (!(effectObject is List<KeyValuePair<string, object>> effect)
                    || !(JsonGet(effect, "_modifierId") is string modifierId)
                    || !(JsonGet(effect, "_gradeValues") is List<KeyValuePair<string, object>> gradeValues))
                    throw new FormatException("elite_modifier_drops.json: Modifier 등급값 형식이 잘못되었습니다.");
                foreach (KeyValuePair<string, object> gradeValue in gradeValues)
                {
                    if (gradeValue.Value == null) continue;
                    if (!table._modifiersByGrade.TryGetValue(gradeValue.Key, out List<string> modifiers))
                        table._modifiersByGrade[gradeValue.Key] = modifiers = new List<string>();
                    modifiers.Add(modifierId);
                }
            }
            foreach (object stageObject in stageTables)
            {
                if (!(stageObject is List<KeyValuePair<string, object>> stageEntry)
                    || !(JsonGet(stageEntry, "_stage") is double stageNumber))
                    throw new FormatException("elite_modifier_drops.json: 스테이지 항목 형식이 잘못되었습니다.");
                int stage = (int)stageNumber;
                if (JsonGet(stageEntry, "_eliteModifierGradeChancesPercent") is List<KeyValuePair<string, object>> chancesObject)
                {
                    var chances = new List<EliteModifierGradeChance>();
                    foreach (KeyValuePair<string, object> chance in chancesObject)
                        chances.Add(new EliteModifierGradeChance(chance.Key, (int)(double)chance.Value));
                    table._gradeChances[stage] = chances;
                    if (JsonGet(stageEntry, "_stageType") as string == "normal") table._fallbackChances = chances;
                }
                else table._gradeChances[stage] = null;
                if (JsonGet(stageEntry, "_stageClearModifierRewardChoice") is List<KeyValuePair<string, object>> choiceObject
                    && JsonGet(choiceObject, "_grade") is string grade
                    && JsonGet(choiceObject, "_choiceCount") is double choiceCount
                    && JsonGet(choiceObject, "_selectCount") is double selectCount)
                    table._clearChoices[stage] = new EliteModifierClearChoice(grade, (int)choiceCount, (int)selectCount);
            }
            return table;
        }

        /// <summary>키-값 목록으로 표현한 JSON 객체에서 키의 값을 반환하고 없으면 null을 반환한다.</summary>
        private static object JsonGet(List<KeyValuePair<string, object>> jsonObject, string key)
        {
            foreach (KeyValuePair<string, object> pair in jsonObject)
                if (pair.Key == key) return pair.Value;
            return null;
        }

        /// <summary>임시 리더용 최소 JSON 파서다. 객체는 키-값 목록, 배열은 목록, 값은 문자열·실수·bool·null로 풀어낸다.</summary>
        private static class MiniJson
        {
            /// <summary>JSON 텍스트 전체를 파싱해 루트 값을 반환한다. 형식이 어긋나면 FormatException을 발생시킨다.</summary>
            internal static object Parse(string json)
            {
                int index = 0;
                object value = ParseValue(json, ref index);
                SkipWhitespace(json, ref index);
                if (index != json.Length) throw new FormatException("elite_modifier_drops.json: 끝에 남은 문자가 있습니다.");
                return value;
            }

            /// <summary>현재 위치의 JSON 값을 하나 읽고 위치를 뒤로 옮긴다.</summary>
            private static object ParseValue(string json, ref int index)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length) throw new FormatException("elite_modifier_drops.json: 값이 잘렸습니다.");
                char current = json[index];
                if (current == '{') return ParseObject(json, ref index);
                if (current == '[') return ParseArray(json, ref index);
                if (current == '"') return ParseString(json, ref index);
                if (current == 't' || current == 'f') return ParseBool(json, ref index);
                if (current == 'n') return ParseLiteral(json, ref index, "null");
                return ParseNumber(json, ref index);
            }

            /// <summary>JSON 객체를 키-값 목록으로 읽는다.</summary>
            private static object ParseObject(string json, ref int index)
            {
                index++;
                var result = new List<KeyValuePair<string, object>>();
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == '}') { index++; return result; }
                while (true)
                {
                    SkipWhitespace(json, ref index);
                    string key = ParseString(json, ref index);
                    SkipWhitespace(json, ref index);
                    if (index >= json.Length || json[index] != ':') throw new FormatException("elite_modifier_drops.json: 객체 구분자가 없습니다.");
                    index++;
                    result.Add(new KeyValuePair<string, object>(key, ParseValue(json, ref index)));
                    SkipWhitespace(json, ref index);
                    if (index >= json.Length) throw new FormatException("elite_modifier_drops.json: 객체가 닫히지 않았습니다.");
                    if (json[index] == ',') { index++; continue; }
                    if (json[index] == '}') { index++; return result; }
                    throw new FormatException("elite_modifier_drops.json: 객체 구분자가 잘못되었습니다.");
                }
            }

            /// <summary>JSON 배열을 목록으로 읽는다.</summary>
            private static object ParseArray(string json, ref int index)
            {
                index++;
                var result = new List<object>();
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ']') { index++; return result; }
                while (true)
                {
                    result.Add(ParseValue(json, ref index));
                    SkipWhitespace(json, ref index);
                    if (index >= json.Length) throw new FormatException("elite_modifier_drops.json: 배열이 닫히지 않았습니다.");
                    if (json[index] == ',') { index++; continue; }
                    if (json[index] == ']') { index++; return result; }
                    throw new FormatException("elite_modifier_drops.json: 배열 구분자가 잘못되었습니다.");
                }
            }

            /// <summary>JSON 문자열을 이스케이프를 풀어 읽는다.</summary>
            private static string ParseString(string json, ref int index)
            {
                if (index >= json.Length || json[index] != '"') throw new FormatException("elite_modifier_drops.json: 문자열이 따옴표로 시작하지 않습니다.");
                index++;
                var result = new System.Text.StringBuilder();
                while (index < json.Length)
                {
                    char current = json[index++];
                    if (current == '"') return result.ToString();
                    if (current != '\\') { result.Append(current); continue; }
                    if (index >= json.Length) break;
                    char escape = json[index++];
                    switch (escape)
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u':
                            if (index + 4 > json.Length) throw new FormatException("elite_modifier_drops.json: 유니코드 이스케이프가 잘렸습니다.");
                            result.Append((char)int.Parse(json.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            index += 4;
                            break;
                        default: throw new FormatException("elite_modifier_drops.json: 알 수 없는 이스케이프입니다: \\" + escape);
                    }
                }
                throw new FormatException("elite_modifier_drops.json: 문자열이 닫히지 않았습니다.");
            }

            /// <summary>true 또는 false를 읽는다.</summary>
            private static object ParseBool(string json, ref int index)
            {
                if (json.IndexOf("true", index, StringComparison.Ordinal) == index) { index += 4; return true; }
                if (json.IndexOf("false", index, StringComparison.Ordinal) == index) { index += 5; return false; }
                throw new FormatException("elite_modifier_drops.json: bool 값이 잘못되었습니다.");
            }

            /// <summary>지정한 예약어를 읽는다.</summary>
            private static object ParseLiteral(string json, ref int index, string literal)
            {
                if (json.IndexOf(literal, index, StringComparison.Ordinal) != index) throw new FormatException("elite_modifier_drops.json: 예약어가 잘못되었습니다.");
                index += literal.Length;
                return null;
            }

            /// <summary>정수나 실수를 읽어 double로 반환한다.</summary>
            private static object ParseNumber(string json, ref int index)
            {
                int start = index;
                while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '-' || json[index] == '+'
                    || json[index] == '.' || json[index] == 'e' || json[index] == 'E')) index++;
                if (index == start) throw new FormatException("elite_modifier_drops.json: 숫자를 읽을 수 없습니다.");
                return double.Parse(json.Substring(start, index - start), CultureInfo.InvariantCulture);
            }

            /// <summary>공백 문자를 건너뛴다.</summary>
            private static void SkipWhitespace(string json, ref int index)
            {
                while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
            }
        }
    }
}
