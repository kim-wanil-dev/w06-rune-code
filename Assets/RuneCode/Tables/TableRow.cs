using System.Collections.Generic;
using System.Globalization;

namespace RuneCode.Tables
{
    /// <summary>
    /// 테이블의 데이터 행 하나다. 열 이름으로 타입별 값을 읽고, 잘못된 값은 "파일:행:열" 위치와 함께 오류 로그에 기록한 뒤 기본값을 반환한다.
    /// Get은 값이 반드시 있어야 하는 열, GetOptional은 빈 칸이면 지정 기본값을 쓰는 열에 사용한다.
    /// 숫자는 로캘과 무관하게 항상 '.' 소수점(Invariant)으로 읽고, bool은 true/false만 허용한다.
    /// </summary>
    public sealed class TableRow
    {
        private readonly DataTable _table;
        private readonly int _line;
        private readonly IReadOnlyList<string> _cells;

        public int Line => _line;

        /// <summary>소속 테이블, 파일 행 번호와 셀 값으로 행을 만든다.</summary>
        internal TableRow(DataTable table, int line, IReadOnlyList<string> cells)
        {
            _table = table;
            _line = line;
            _cells = cells;
        }

        /// <summary>비어 있으면 안 되는 문자열 열의 값을 반환한다. 비어 있으면 오류를 기록하고 빈 문자열을 반환한다.</summary>
        public string GetString(string column)
        {
            string raw = Raw(column);
            if (raw != null && raw.Length == 0)
            {
                ReportError(column, "값이 비어 있습니다.");
            }
            return raw ?? "";
        }

        /// <summary>문자열 열의 값을 반환하며 빈 칸이면 지정 기본값을 반환한다.</summary>
        public string GetOptionalString(string column, string fallback)
        {
            string raw = Raw(column);
            return string.IsNullOrEmpty(raw) ? fallback : raw;
        }

        /// <summary>비어 있으면 안 되는 정수 열의 값을 반환한다. 비었거나 정수가 아니면 오류를 기록하고 0을 반환한다.</summary>
        public int GetInt(string column)
        {
            return ParseInt(column, Raw(column), 0, true);
        }

        /// <summary>정수 열의 값을 반환하며 빈 칸이면 지정 기본값을 반환한다. 정수가 아니면 오류를 기록한다.</summary>
        public int GetOptionalInt(string column, int fallback)
        {
            return ParseInt(column, Raw(column), fallback, false);
        }

        /// <summary>비어 있으면 안 되는 실수 열의 값을 반환한다. 비었거나 유한한 실수가 아니면 오류를 기록하고 0을 반환한다.</summary>
        public float GetFloat(string column)
        {
            return ParseFloat(column, Raw(column), 0f, true);
        }

        /// <summary>실수 열의 값을 반환하며 빈 칸이면 지정 기본값을 반환한다. 유한한 실수가 아니면 오류를 기록한다.</summary>
        public float GetOptionalFloat(string column, float fallback)
        {
            return ParseFloat(column, Raw(column), fallback, false);
        }

        /// <summary>비어 있으면 안 되는 bool 열의 값을 반환한다. true/false가 아니면 오류를 기록하고 false를 반환한다.</summary>
        public bool GetBool(string column)
        {
            return ParseBool(column, Raw(column), false, true);
        }

        /// <summary>bool 열의 값을 반환하며 빈 칸이면 지정 기본값을 반환한다. true/false가 아니면 오류를 기록한다.</summary>
        public bool GetOptionalBool(string column, bool fallback)
        {
            return ParseBool(column, Raw(column), fallback, false);
        }

        /// <summary>이 행의 지정 열 위치로 데이터 규칙 위반 오류를 기록한다. 열이 행 전체에 해당하면 null을 전달한다.</summary>
        public void ReportError(string column, string message)
        {
            _table.Log.Add(_table.Name, _line, column, message);
        }

        /// <summary>열의 원본 셀 값을 앞뒤 공백 없이 반환한다. 열이 없으면 오류를 기록하고 null을 반환한다.</summary>
        private string Raw(string column)
        {
            int index = _table.ColumnIndex(column);
            if (index < 0)
            {
                ReportError(column, "열이 없습니다.");
                return null;
            }
            return _cells[index].Trim();
        }

        /// <summary>셀 값을 정수로 읽는다. 필수 열의 빈 칸이나 잘못된 형식은 오류로 기록하고 기본값을 반환한다.</summary>
        private int ParseInt(string column, string raw, int fallback, bool isRequired)
        {
            if (string.IsNullOrEmpty(raw))
            {
                if (isRequired && raw != null)
                {
                    ReportError(column, "값이 비어 있습니다.");
                }
                return fallback;
            }
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return value;
            }
            ReportError(column, "정수가 아닙니다: " + raw);
            return fallback;
        }

        /// <summary>셀 값을 유한한 실수로 읽는다. 필수 열의 빈 칸, 잘못된 형식, NaN·무한대는 오류로 기록하고 기본값을 반환한다.</summary>
        private float ParseFloat(string column, string raw, float fallback, bool isRequired)
        {
            if (string.IsNullOrEmpty(raw))
            {
                if (isRequired && raw != null)
                {
                    ReportError(column, "값이 비어 있습니다.");
                }
                return fallback;
            }
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                && !float.IsNaN(value) && !float.IsInfinity(value))
            {
                return value;
            }
            ReportError(column, "유한한 실수가 아닙니다 (소수점은 '.' 사용): " + raw);
            return fallback;
        }

        /// <summary>셀 값을 true/false로 읽는다. 대소문자는 구분하지 않으며 그 외 값은 오류로 기록하고 기본값을 반환한다.</summary>
        private bool ParseBool(string column, string raw, bool fallback, bool isRequired)
        {
            if (string.IsNullOrEmpty(raw))
            {
                if (isRequired && raw != null)
                {
                    ReportError(column, "값이 비어 있습니다.");
                }
                return fallback;
            }
            if (bool.TryParse(raw, out bool value))
            {
                return value;
            }
            ReportError(column, "true 또는 false가 아닙니다: " + raw);
            return fallback;
        }
    }
}
