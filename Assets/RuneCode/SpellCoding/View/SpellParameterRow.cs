using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// 인스펙터 파라미터 한 줄이다. 라벨과 종류별 입력 컨트롤(숫자 입력, 증감 버튼, 선택 버튼, Spell Call 대상 버튼)을 가지며
    /// Configure 계열 메서드로 파라미터 종류에 맞는 모드만 표시한다.
    /// </summary>
    public sealed class SpellParameterRow : MonoBehaviour
    {
        [Header("파라미터 행 구성")]
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TMP_InputField _input;
        [SerializeField] private Button _minusButton;
        [SerializeField] private Button _plusButton;
        [SerializeField] private Button _optionButton;
        [SerializeField] private TextMeshProUGUI _optionLabel;
        [SerializeField] private Button _spellButton;
        [SerializeField] private TextMeshProUGUI _spellLabel;

        public TMP_InputField Input => _input;
        public Button MinusButton => _minusButton;
        public Button PlusButton => _plusButton;
        public Button OptionButton => _optionButton;
        public Button SpellButton => _spellButton;

        /// <summary>숫자 파라미터 행을 구성한다. 수식 배율이면 입력 필드를 좁히고 감소·증가 버튼을 함께 표시한다.</summary>
        public void ConfigureNumber(string labelText, string valueText, bool isModifierScale)
        {
            _label.text = labelText;
            RectTransform inputRect = (RectTransform)_input.transform;
            inputRect.anchoredPosition = new Vector2(isModifierScale ? 42f : 0f, -24f);
            inputRect.sizeDelta = new Vector2(isModifierScale ? 132f : 222f, 30f);
            _input.gameObject.SetActive(true);
            _input.text = valueText;
            _minusButton.gameObject.SetActive(isModifierScale);
            _plusButton.gameObject.SetActive(isModifierScale);
            _optionButton.gameObject.SetActive(false);
            _spellButton.gameObject.SetActive(false);
            SetHeight(64f);
        }

        /// <summary>문자열 파라미터 행을 입력 필드 모드로 구성한다.</summary>
        public void ConfigureText(string labelText, string valueText)
        {
            _label.text = labelText;
            RectTransform inputRect = (RectTransform)_input.transform;
            inputRect.anchoredPosition = new Vector2(0f, -24f);
            inputRect.sizeDelta = new Vector2(222f, 30f);
            _input.gameObject.SetActive(true);
            _input.text = valueText;
            _minusButton.gameObject.SetActive(false);
            _plusButton.gameObject.SetActive(false);
            _optionButton.gameObject.SetActive(false);
            _spellButton.gameObject.SetActive(false);
            SetHeight(64f);
        }

        /// <summary>Trigger·Magic 같은 열거 파라미터 행을 현재 값 라벨의 선택 버튼 모드로 구성한다.</summary>
        public void ConfigureEnum(string labelText, string optionLabel)
        {
            _label.text = labelText;
            _input.gameObject.SetActive(false);
            _minusButton.gameObject.SetActive(false);
            _plusButton.gameObject.SetActive(false);
            _optionButton.gameObject.SetActive(true);
            _optionLabel.text = optionLabel;
            _spellButton.gameObject.SetActive(false);
            SetHeight(64f);
        }

        /// <summary>Spell Call 대상 선택 행을 보관함 이름과 ID를 보여 주는 버튼 모드로 구성한다.</summary>
        public void ConfigureSpell(string labelText, string buttonLabel)
        {
            _label.text = labelText;
            _input.gameObject.SetActive(false);
            _minusButton.gameObject.SetActive(false);
            _plusButton.gameObject.SetActive(false);
            _optionButton.gameObject.SetActive(false);
            _spellButton.gameObject.SetActive(true);
            _spellLabel.text = buttonLabel;
            SetHeight(76f);
        }

        /// <summary>파라미터 종류에 맞는 행 높이를 적용한다. 라벨 위 24픽셀에 컨트롤이 놓인다.</summary>
        private void SetHeight(float height)
        {
            RectTransform rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(224f, height);
        }
    }
}
