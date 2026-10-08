namespace RuneCode
{
    /// <summary>문법 엔진의 컴파일 진단을 현지화된 사용자 문구로 만든다.</summary>
    public static class CompileIssueText
    {
        /// <summary>
        /// 진단 코드의 현지화 문구에 상세값과 하위 원인 문구를 이어 붙여 반환한다.
        /// 예: "다른 마법 오류 화염구: 시작 조건이 없습니다".
        /// </summary>
        public static string Format(CompileIssue issue)
        {
            string text = GameData.L("issue." + issue.Code);
            if (issue.Detail != null)
            {
                text += " " + issue.Detail;
            }
            if (issue.Cause != null)
            {
                text += ": " + Format(issue.Cause);
            }
            return text;
        }
    }
}
