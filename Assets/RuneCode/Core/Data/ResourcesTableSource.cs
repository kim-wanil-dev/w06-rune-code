using UnityEngine;

using RuneCode.Tables;

namespace RuneCode
{
    /// <summary>Resources 폴더 아래 지정 경로의 CSV TextAsset을 테이블 소스로 제공하는 Unity 구현이다.</summary>
    public sealed class ResourcesTableSource : ITableSource
    {
        private readonly string _folder;

        /// <summary>Resources 기준 테이블 폴더 경로로 소스를 만든다. 예: "RuneCode/Tables".</summary>
        public ResourcesTableSource(string folder)
        {
            _folder = folder;
        }

        /// <summary>폴더의 테이블 이름 TextAsset을 읽어 텍스트를 반환하고, 없으면 false를 반환한다.</summary>
        public bool TryRead(string tableName, out string text)
        {
            TextAsset asset = Resources.Load<TextAsset>(_folder + "/" + tableName);
            text = asset != null ? asset.text : null;
            return asset != null;
        }
    }
}
