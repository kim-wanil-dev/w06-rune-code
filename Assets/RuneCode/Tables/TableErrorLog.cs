using System;
using System.Collections.Generic;

namespace RuneCode.Tables
{
    /// <summary>
    /// 테이블 로드 중 발견한 오류를 "파일:행:열 메시지" 형식으로 모은다.
    /// 첫 오류에서 멈추지 않고 전부 모은 뒤 ThrowIfAny로 한 번에 보고한다.
    /// </summary>
    public sealed class TableErrorLog
    {
        private readonly List<string> _errors = new List<string>();

        public bool HasErrors => _errors.Count > 0;
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>테이블 이름, 행 번호(0이면 파일 전체), 열 이름(없으면 null)과 메시지로 오류를 추가한다.</summary>
        public void Add(string tableName, int line, string column, string message)
        {
            string location = tableName + ".csv";
            if (line > 0)
            {
                location += ":" + line;
            }
            if (!string.IsNullOrEmpty(column))
            {
                location += ":" + column;
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
