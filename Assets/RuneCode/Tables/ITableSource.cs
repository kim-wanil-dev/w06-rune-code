namespace RuneCode.Tables
{
    /// <summary>
    /// 테이블 이름으로 CSV 텍스트를 가져오는 창구다. Unity는 Resources TextAsset, 도구·CLI는 파일 시스템 구현을 사용한다.
    /// 테이블 이름에는 확장자를 붙이지 않는다. 예: "runes".
    /// </summary>
    public interface ITableSource
    {
        /// <summary>테이블 이름의 CSV 텍스트를 읽어 반환하고, 테이블이 없으면 false를 반환한다.</summary>
        bool TryRead(string tableName, out string text);
    }
}
