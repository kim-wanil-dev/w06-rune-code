using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>Displays remaining cooldown as a radial image fill and a short center label.</summary>
    public sealed class CooldownRadialView : MonoBehaviour
    {
        [Header("Radial cooldown")]
        [SerializeField] private Image _fill;
        [SerializeField] private TextMeshProUGUI _remaining;

        /// <summary>Updates the radial fill and remaining-time label from the current and total cooldown.</summary>
        public void SetCooldown(double remaining, double duration)
        {
            float total = Mathf.Max(0f, (float)duration);
            float current = Mathf.Clamp((float)remaining, 0f, total);
            _fill.fillAmount = total > 0f ? current / total : 0f;
            _remaining.text = current > 0.005f ? current.ToString("0.0") : "준비";
        }
    }
}
