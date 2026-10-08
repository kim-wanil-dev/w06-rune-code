using System;
using System.Collections.Generic;

namespace RuneCode.Tables
{
    /// <summary>
    /// 헤더가 있는 CSV 테이블 하나다. 첫 번째 일반 행이 열 이름이며 첫 칸이 '#'으로 시작하는 행은 설명·주석으로 건너뛴다.
    /// 행 접근, 필수 열 확인, 키 중복·참조 검증, 부모 키별 묶기를 제공하고 오류는 공유 로그에 모은다.
    /// </summary>
    public sealed class DataTable
    {
        private const string COMMENT_PREFIX = "#";
        private const char KEY_SEPARATOR = '\u001F';

        private readonly string _name;
        private readonly TableErrorLog _log;
        private readonly Dictionary<string, int> _columns = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<TableRow> _rows = new List<TableRow>();

        public string Name => _name;
        public IReadOnlyList<TableRow> Rows => _rows;
        internal TableErrorLog Log => _log;

        /// <summary>테이블 이름과 오류 로그로 빈 테이블을 만든다.</summary>
        private DataTable(string name, TableErrorLog log)
        {
            _name = name;
            _log = log;
        }

        /// <summary>테이블 소스에서 지정 이름의 테이블을 읽는다. 테이블이 없으면 오류를 기록하고 빈 테이블을 반환한다.</summary>
        public static DataTable Load(ITableSource source, string name, TableErrorLog log)
        {
            if (!source.TryRead(name, out string text))
            {
                log.Add(name, 0, null, "테이블 파일을 찾을 수 없습니다.");
                return new DataTable(name, log);
            }
            return Parse(name, text, log);
        }

        /// <summary>CSV 텍스트를 테이블로 읽는다. 헤더 누락·중복 열·열 개수 불일치 행은 오류로 기록하고 해당 행은 제외한다.</summary>
        public static DataTable Parse(string name, string text, TableErrorLog log)
        {
            var table = new DataTable(name, log);
            bool hasHeader = false;
            foreach (CsvRecord record in CsvParser.Parse(name, text, log))
            {
                if (record.Fields[0].StartsWith(COMMENT_PREFIX, StringComparison.Ordinal))
                {
                    continue;
                }
                if (!hasHeader)
                {
                    table.ReadHeader(record);
                    hasHeader = true;
                    continue;
                }
                if (record.Fields.Count != table._columns.Count)
                {
                    log.Add(name, record.Line, null, "열 개수가 헤더와 다릅니다 (헤더 " + table._columns.Count + ", 행 " + record.Fields.Count + ").");
                    continue;
                }
                table._rows.Add(new TableRow(table, record.Line, record.Fields));
            }

            if (!hasHeader)
            {
                log.Add(name, 0, null, "헤더 행이 없습니다.");
            }
            return table;
        }

        /// <summary>테이블에 지정 열이 있는지 반환한다.</summary>
        public bool HasColumn(string column)
        {
            return _columns.ContainsKey(column);
        }

        /// <summary>지정 열이 모두 있는지 확인하고 없는 열을 오류로 기록한다. 모두 있으면 true를 반환한다.</summary>
        public bool RequireColumns(params string[] columns)
        {
            bool hasAll = true;
            foreach (string column in columns)
            {
                if (!_columns.ContainsKey(column))
                {
                    _log.Add(_name, 0, column, "필수 열이 없습니다.");
                    hasAll = false;
                }
            }
            return hasAll;
        }

        /// <summary>
        /// 지정 열들의 조합이 행마다 유일한지 검증하고, 비어 있거나 중복된 키를 오류로 기록한다.
        /// 검증된 키 집합을 반환하며 다른 테이블의 참조 검증에 사용한다.
        /// </summary>
        public HashSet<string> RequireUniqueKeys(params string[] keyColumns)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (TableRow row in _rows)
            {
                string key = KeyOf(row, keyColumns);
                if (key == null)
                {
                    _log.Add(_name, row.Line, string.Join("+", keyColumns), "키 값이 비어 있습니다.");
                    continue;
                }
                if (!keys.Add(key))
                {
                    _log.Add(_name, row.Line, string.Join("+", keyColumns), "중복 키입니다: " + key.Replace(KEY_SEPARATOR, '+'));
                }
            }
            return keys;
        }

        /// <summary>지정 열들의 조합이 부모 테이블의 키 집합에 있는지 검증하고 없는 참조를 오류로 기록한다.</summary>
        public void RequireReferences(HashSet<string> parentKeys, string parentTable, params string[] keyColumns)
        {
            foreach (TableRow row in _rows)
            {
                string key = KeyOf(row, keyColumns);
                if (key != null && !parentKeys.Contains(key))
                {
                    _log.Add(_name, row.Line, string.Join("+", keyColumns), parentTable + "에 없는 키를 참조합니다: " + key.Replace(KEY_SEPARATOR, '+'));
                }
            }
        }

        /// <summary>
        /// 부모 키 열들의 값으로 행을 묶고 각 묶음을 정수 순서 열 기준으로 정렬해 반환한다.
        /// 순서 열이 null이면 파일의 행 순서를 유지한다.
        /// </summary>
        public Dictionary<string, List<TableRow>> GroupBy(string orderColumn, params string[] keyColumns)
        {
            var groups = new Dictionary<string, List<TableRow>>(StringComparer.Ordinal);
            foreach (TableRow row in _rows)
            {
                string key = KeyOf(row, keyColumns);
                if (key == null)
                {
                    continue;
                }
                if (!groups.TryGetValue(key, out List<TableRow> group))
                {
                    group = new List<TableRow>();
                    groups.Add(key, group);
                }
                group.Add(row);
            }

            if (orderColumn != null)
            {
                // 순서 값은 행마다 한 번만 읽어 잘못된 값의 오류가 중복 기록되지 않게 한다.
                var orders = new Dictionary<TableRow, int>();
                foreach (TableRow row in _rows)
                {
                    orders[row] = row.GetInt(orderColumn);
                }
                foreach (List<TableRow> group in groups.Values)
                {
                    group.Sort((a, b) => orders[a].CompareTo(orders[b]));
                }
            }
            return groups;
        }

        /// <summary>여러 값을 GroupBy·RequireUniqueKeys와 같은 형식의 복합 키 하나로 만든다.</summary>
        public static string Key(params string[] values)
        {
            return string.Join(KEY_SEPARATOR.ToString(), values);
        }

        /// <summary>열 이름의 열 번호를 반환하며 없으면 -1을 반환한다.</summary>
        internal int ColumnIndex(string column)
        {
            return _columns.TryGetValue(column, out int index) ? index : -1;
        }

        /// <summary>헤더 레코드에서 열 이름과 번호를 읽고 빈 이름·중복 이름을 오류로 기록한다.</summary>
        private void ReadHeader(CsvRecord record)
        {
            for (int i = 0; i < record.Fields.Count; i++)
            {
                string column = record.Fields[i].Trim();
                if (column.Length == 0 || !_columns.TryAdd(column, i))
                {
                    _log.Add(_name, record.Line, column, "비어 있거나 중복된 열 이름입니다.");
                }
            }
        }

        /// <summary>행의 지정 열 값들로 복합 키를 만든다. 값이 하나라도 비어 있으면 null을 반환한다.</summary>
        private string KeyOf(TableRow row, string[] keyColumns)
        {
            var values = new string[keyColumns.Length];
            for (int i = 0; i < keyColumns.Length; i++)
            {
                values[i] = row.GetOptionalString(keyColumns[i], "");
                if (values[i].Length == 0)
                {
                    return null;
                }
            }
            return Key(values);
        }
    }
}
