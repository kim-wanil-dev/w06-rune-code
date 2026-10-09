using System;
using System.Collections.Generic;

namespace RuneCode
{
    /// <summary>
    /// 룬 테이블 검증 중 발견한 오류를 "runes.json:룬ID:필드 메시지" 형식으로 모은다.
    /// 첫 오류에서 멈추지 않고 전부 모은 뒤 ThrowIfAny로 한 번에 보고한다.
    /// </summary>
    public sealed class TableErrorLog
    {
        private const string TABLE_FILE = "runes.json";

        private readonly List<string> _errors = new List<string>();

        public bool HasErrors => _errors.Count > 0;
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>오류 룬 ID(카탈로그 전체 오류면 null), 필드 경로(없으면 null)와 메시지로 오류를 추가한다.</summary>
        public void Add(string runeId, string field, string message)
        {
            string location = TABLE_FILE;
            if (!string.IsNullOrEmpty(runeId))
            {
                location += ":" + runeId;
            }
            if (!string.IsNullOrEmpty(field))
            {
                location += ":" + field;
            }
            _errors.Add(location + " " + message);
        }

        /// <summary>오류가 하나라도 있으면 모든 오류를 담은 TableLoadException을 발생시킨다.</summary>
        public void ThrowIfAny()
        {
            if (HasErrors)
            {
                throw new TableLoadException(_errors);
            }
        }
    }

    /// <summary>
    /// 테이블 로드 실패 예외다. 기존 데이터 형식 오류 처리와 호환되도록 FormatException을 상속한다.
    /// </summary>
    public sealed class TableLoadException : FormatException
    {
        private readonly IReadOnlyList<string> _errors;

        public IReadOnlyList<string> Errors => _errors;

        /// <summary>모은 오류 목록으로 예외 메시지를 만든다.</summary>
        public TableLoadException(IReadOnlyList<string> errors)
            : base("테이블 로드 실패 (" + errors.Count + "건)" + Environment.NewLine + string.Join(Environment.NewLine, errors))
        {
            _errors = errors;
        }
    }
}
