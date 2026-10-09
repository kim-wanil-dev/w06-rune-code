using System;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>
    /// RC1 공유 코드 팝업 View다. 현재 마법의 코드를 보여 주고 클립보드 복사·붙여넣기를 처리하며, 가져오기 클릭 시 입력 코드를 알린다.
    /// </summary>
    public sealed class SharePopup : UiPopup
    {
        [Header("공유 코드")]
        [SerializeField] private TMP_InputField _codeField;
        [SerializeField] private Button _exportButton;
        [SerializeField] private Button _pasteButton;
        [SerializeField] private Button _importButton;

        private Action<string> _onImport;
        private bool _isBound;

        /// <summary>표시할 공유 코드와 가져오기 동작을 설정한다. 가져오면 입력 코드를 넘기고 팝업을 닫는다.</summary>
        public void Configure(string code, Action<string> onImport)
        {
            if (!_isBound)
            {
                _exportButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = _codeField.text);
                _pasteButton.onClick.AddListener(() => _codeField.text = GUIUtility.systemCopyBuffer);
                _importButton.onClick.AddListener(Import);
                _isBound = true;
            }
            _codeField.text = code;
            _onImport = onImport;
        }

        /// <summary>입력 코드로 가져오기 동작을 실행하고 팝업을 닫는다.</summary>
        private void Import()
        {
            Action<string> onImport = _onImport;
            string code = _codeField.text;
            Close();
            onImport?.Invoke(code);
        }
    }
}
