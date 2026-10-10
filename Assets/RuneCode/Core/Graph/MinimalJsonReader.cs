using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RuneCode
{
    /// <summary>Unity 및 외부 패키지에 의존하지 않고 JSON 기본 자료형을 읽는다.</summary>
    internal sealed class MinimalJsonReader
    {
        private readonly string _json;
        private int _position;

        /// <summary>읽을 JSON 문자열을 받는다.</summary>
        private MinimalJsonReader(string json)
        {
            _json = json;
        }

        /// <summary>JSON 문자열을 객체·배열·기본값으로 변환하고 뒤따르는 문자가 없는지 확인한다.</summary>
        public static object Parse(string json)
        {
            if (json == null) throw new FormatException("JSON 문자열이 없습니다.");
            var reader = new MinimalJsonReader(json);
            object value = reader.ReadValue();
            reader.SkipWhitespace();
            if (reader._position != json.Length) throw reader.Error("JSON 끝에 불필요한 문자가 있습니다.");
            return value;
        }

        /// <summary>현재 위치에서 JSON 값 하나를 읽는다.</summary>
        private object ReadValue()
        {
            SkipWhitespace();
            if (_position >= _json.Length) throw Error("JSON 값이 끝나기 전에 입력이 끝났습니다.");
            switch (_json[_position])
            {
                case '{': return ReadObject();
                case '[': return ReadArray();
                case '"': return ReadString();
                case 't': ReadLiteral("true"); return true;
                case 'f': ReadLiteral("false"); return false;
                case 'n': ReadLiteral("null"); return null;
                default: return ReadNumber();
            }
        }

        /// <summary>현재 객체의 키와 값을 사전으로 읽는다.</summary>
        private Dictionary<string, object> ReadObject()
        {
            _position++;
            SkipWhitespace();
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (TryRead('}')) return result;
            while (true)
            {
                SkipWhitespace();
                if (_position >= _json.Length || _json[_position] != '"') throw Error("객체 키는 문자열이어야 합니다.");
                string key = ReadString();
                SkipWhitespace();
                Require(':');
                object value = ReadValue();
                if (result.ContainsKey(key)) throw Error("객체 키가 중복되었습니다: " + key);
                result.Add(key, value);
                SkipWhitespace();
                if (TryRead('}')) return result;
                Require(',');
            }
        }

        /// <summary>현재 배열의 값을 목록으로 읽는다.</summary>
        private List<object> ReadArray()
        {
            _position++;
            SkipWhitespace();
            var result = new List<object>();
            if (TryRead(']')) return result;
            while (true)
            {
                result.Add(ReadValue());
                SkipWhitespace();
                if (TryRead(']')) return result;
                Require(',');
            }
        }

        /// <summary>따옴표 문자열과 JSON escape를 읽는다.</summary>
        private string ReadString()
        {
            Require('"');
            var result = new StringBuilder();
            while (_position < _json.Length)
            {
                char character = _json[_position++];
                if (character == '"') return result.ToString();
                if (character < 0x20) throw Error("문자열에 제어 문자가 있습니다.");
                if (character != '\\')
                {
                    result.Append(character);
                    continue;
                }
                if (_position >= _json.Length) throw Error("문자열 escape가 끝나지 않았습니다.");
                switch (_json[_position++])
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u': result.Append(ReadUnicodeEscape()); break;
                    default: throw Error("알 수 없는 문자열 escape입니다.");
                }
            }
            throw Error("문자열이 닫히지 않았습니다.");
        }

        /// <summary>유니코드 escape 뒤의 16진수 네 자리를 읽는다.</summary>
        private char ReadUnicodeEscape()
        {
            if (_position + 4 > _json.Length) throw Error("유니코드 escape가 끝나지 않았습니다.");
            string digits = _json.Substring(_position, 4);
            if (!ushort.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort value))
                throw Error("유니코드 escape 형식이 잘못되었습니다.");
            _position += 4;
            return (char)value;
        }

        /// <summary>JSON 숫자 문법을 확인하고 invariant culture의 double로 읽는다.</summary>
        private double ReadNumber()
        {
            int start = _position;
            if (TryRead('-') && _position >= _json.Length) throw Error("숫자 형식이 잘못되었습니다.");
            if (TryRead('0'))
            {
                if (_position < _json.Length && char.IsDigit(_json[_position])) throw Error("숫자의 앞에 불필요한 0이 있습니다.");
            }
            else ReadDigits(true);
            if (TryRead('.')) ReadDigits(true);
            if (TryRead('e') || TryRead('E'))
            {
                if (!TryRead('+')) TryRead('-');
                ReadDigits(true);
            }
            string token = _json.Substring(start, _position - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || double.IsNaN(value) || double.IsInfinity(value))
                throw Error("숫자 값이 범위를 벗어났습니다.");
            return value;
        }

        /// <summary>요청된 JSON 리터럴을 읽고 일치하지 않으면 예외를 반환한다.</summary>
        private void ReadLiteral(string literal)
        {
            if (_position + literal.Length > _json.Length || string.CompareOrdinal(_json, _position, literal, 0, literal.Length) != 0)
                throw Error("JSON 리터럴 형식이 잘못되었습니다.");
            _position += literal.Length;
        }

        /// <summary>숫자의 연속된 숫자 문자를 읽고 필요할 때 한 자리 이상을 요구한다.</summary>
        private void ReadDigits(bool requireDigit)
        {
            int start = _position;
            while (_position < _json.Length && _json[_position] >= '0' && _json[_position] <= '9') _position++;
            if (requireDigit && start == _position) throw Error("숫자 자릿수가 없습니다.");
        }

        /// <summary>공백 문자를 건너뛴다.</summary>
        private void SkipWhitespace()
        {
            while (_position < _json.Length && char.IsWhiteSpace(_json[_position])) _position++;
        }

        /// <summary>문자 하나를 소비하고 예상 문자와 다르면 예외를 반환한다.</summary>
        private void Require(char expected)
        {
            if (!TryRead(expected)) throw Error("예상 문자가 없습니다: " + expected);
        }

        /// <summary>현재 문자가 일치하면 소비하고 결과를 반환한다.</summary>
        private bool TryRead(char expected)
        {
            if (_position >= _json.Length || _json[_position] != expected) return false;
            _position++;
            return true;
        }

        /// <summary>현재 위치를 포함한 JSON 형식 오류를 만든다.</summary>
        private FormatException Error(string message)
        {
            return new FormatException(message + " 위치: " + _position);
        }
    }
}
