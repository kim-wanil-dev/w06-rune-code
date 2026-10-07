using UnityEngine;

namespace RuneCode
{
    /// <summary>시뮬레이션의 AI 적응 학습값을 HUD 막대 또는 도크 요약 문자열로 만든다.</summary>
    public static class AdaptationText
    {
        /// <summary>bars가 true면 전체 태그의 학습값과 고정 상태를 막대로, false면 학습된 태그 수치만 한 줄로 반환한다.</summary>
        public static string Format(RuneSimulation sim, bool bars)
        {
            string text = bars ? GameData.L("ui.adaptation") + "\n" : "";
            foreach (string tag in sim.Adaptation.Tags)
            {
                double value = sim.Adaptation.GetValue(tag);
                if (bars)
                {
                    bool isElement = tag == "fire" || tag == "ice" || tag == "arc" || tag == "raw";
                    float cap = isElement ? GameData.Balance.Adaptation.ElementCap : GameData.Balance.Adaptation.FormCap;
                    int filled = Mathf.Clamp(Mathf.RoundToInt((float)value / Mathf.Max(0.001f, cap) * 8), 0, 8);
                    string locked = sim.Adaptation.IsLocked(tag) ? "<color=#FFC96B>*</color>" : "";
                    text += "<color=#DDEBFA>" + GameData.L("tag." + tag) + "<pos=62>" + (value * 100).ToString("0") + "%</color>" + locked +
                        "<pos=112><color=#344455>" + new string('■', 8) + "</color><pos=112><color=#BFB2FF>" + new string('■', filled) + "</color>\n";
                }
                else if (value > 0) text += GameData.L("tag." + tag) + " " + (value * 100).ToString("0") + "%  ";
            }
            return text;
        }
    }
}
