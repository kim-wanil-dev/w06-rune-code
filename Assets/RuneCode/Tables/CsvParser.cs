using System.Collections.Generic;
using System.Text;

namespace RuneCode.Tables
{
    /// <summary>CSV 레코드 하나와 그 레코드가 시작한 파일 행 번호(1부터)다.</summary>
    internal readonly struct CsvRecord
    {
        public int Line { get; }
        public IReadOnlyList<string> Fields { get; }

        /// <summary>시작 행 번호와 필드 목록으로 레코드를 만든다.</summary>
        public CsvRecord(int line, IReadOnlyList<string> fields)
        {
            Line = line;
            Fields = fields;
        }
    }

    /// <summary>
    /// RFC 4180 CSV를 레코드 목록으로 읽는다. 쉼표 구분, 큰따옴표 감싸기와 "" 이스케이프,
    /// 따옴표 안의 줄바꿈, CRLF/LF, UTF-8 BOM을 지원하며 빈 줄은 건너뛴다.
    /// </summary>
    internal static class CsvParser
    {
        private const char BYTE_ORDER_MARK = '﻿';

        /// <summary>CSV 텍스트를 레코드 목록으로 반환한다. 닫히지 않은 따옴표는 지정 로그에 오류로 기록한다.</summary>
        public static List<CsvRecord> Parse(string tableName, string text, TableErrorLog log)
        {
            var records = new List<CsvRecord>();
            var fields = new List<string>();
            var field = new StringBuilder();
            bool isQuoted = false;
            bool isFieldStarted = false;
            int line = 1;
            int recordLine = 1;
            int start = text.Length > 0 && text[0] == BYTE_ORDER_MARK ? 1 : 0;

            for (int i = start; i < text.Length; i++)
            {
                char current = text[i];
                if (isQuoted)
                {
                    if (current == '"')
                    {
                        // 따옴표 안의 "" 는 따옴표 문자 하나, 단독 " 는 따옴표 구간의 끝이다.
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            isQuoted = false;
                        }
                    }
                    else if (current != '\r')
                    {
                        if (current == '\n')
                        {
                            line++;
                        }
                        field.Append(current);
                    }
                    continue;
                }

                switch (current)
                {
                    case '"' when !isFieldStarted:
                        isQuoted = true;
                        isFieldStarted = true;
                        break;
                    case ',':
                        fields.Add(field.ToString());
                        field.Clear();
                        isFieldStarted = false;
                        break;
                    case '\r':
                        break;
                    case '\n':
                        EndRecord(records, fields, field, recordLine);
                        isFieldStarted = false;
                        line++;
                        recordLine = line;
                        break;
                    default:
                        field.Append(current);
                        isFieldStarted = true;
                        break;
                }
            }

            if (isQuoted)
            {
                log.Add(tableName, recordLine, null, "닫히지 않은 큰따옴표가 있습니다.");
            }
            EndRecord(records, fields, field, recordLine);
            return records;
        }

        /// <summary>현재 필드와 필드 목록을 레코드로 확정해 추가하고 버퍼를 비운다. 내용이 없는 빈 줄은 추가하지 않는다.</summary>
        private static void EndRecord(List<CsvRecord> records, List<string> fields, StringBuilder field, int recordLine)
        {
            fields.Add(field.ToString());
            field.Clear();
            bool isBlank = fields.Count == 1 && fields[0].Length == 0;
            if (!isBlank)
            {
                records.Add(new CsvRecord(recordLine, fields.ToArray()));
            }
            fields.Clear();
        }
    }
}
