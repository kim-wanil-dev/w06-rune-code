namespace RuneCode
{
    /// <summary>Modifier 이름과 안내 문구를 등급별 TMP rich text 색상으로 표시한다.</summary>
    public static class ModifierGradeText
    {
        private const string C_COLOR = "#C1CAD6";
        private const string B_COLOR = "#62C6FF";
        private const string A_COLOR = "#C18BFF";
        private const string S_COLOR = "#FFD166";

        /// <summary>텍스트를 지정 등급 색으로 감싸고 알 수 없는 등급이면 원문을 반환한다.</summary>
        public static string Colorize(string text, string grade)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string color = ColorForGrade(grade);
            return color == null ? text : "<color=" + color + ">" + text + "</color>";
        }

        /// <summary>Modifier 이름과 등급을 묶어 등급 색의 표시 문구로 반환한다.</summary>
        public static string FormatName(string runeName, string grade)
        {
            string label = string.IsNullOrEmpty(grade) ? runeName : runeName + " (" + grade + ")";
            return Colorize(label, grade);
        }

        /// <summary>C·B·A·S 등급에 대응하는 색상 코드를 반환하고 알 수 없는 등급은 null을 반환한다.</summary>
        private static string ColorForGrade(string grade)
        {
            if (string.IsNullOrEmpty(grade)) return null;
            switch (grade.ToUpperInvariant())
            {
                case "C": return C_COLOR;
                case "B": return B_COLOR;
                case "A": return A_COLOR;
                case "S": return S_COLOR;
                default: return null;
            }
        }
    }
}
