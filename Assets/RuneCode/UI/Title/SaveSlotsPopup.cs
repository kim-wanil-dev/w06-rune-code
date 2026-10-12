using System;

using UnityEngine;

using TMPro;
using UnityEngine.UI;

namespace RuneCode
{
    /// <summary>타이틀에서 3개 저장 슬롯을 조회·선택·생성·삭제하는 팝업이다.</summary>
    public sealed class SaveSlotsPopup : UiPopup
    {
        private const int SLOT_COUNT = 3;

        [Header("팝업 구성")]
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private GameObject _panelPrefab;
        [SerializeField] private GameObject _buttonPrefab;

        private readonly TextMeshProUGUI[] _slotLabels = new TextMeshProUGUI[SLOT_COUNT];
        private readonly Button[] _selectButtons = new Button[SLOT_COUNT];
        private readonly Button[] _deleteButtons = new Button[SLOT_COUNT];
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _hintLabel;
        private RuneCodeSession _session;
        private UIManager _ui;
        private Action _onSlotChanged;
        private bool _isSettingsPicker;
        private bool _isBuilt;

        /// <summary>세션과 UI 관리자를 연결하고 저장 슬롯 표시를 갱신한다.</summary>
        public void Configure(RuneCodeSession session, UIManager ui, bool isSettingsPicker = false, Action onSlotChanged = null)
        {
            _session = session;
            _ui = ui;
            _isSettingsPicker = isSettingsPicker;
            _onSlotChanged = onSlotChanged;
            BuildIfNeeded();
            Refresh();
        }

        /// <summary>사용 모드와 저장 파일 유효성에 맞춰 제목, 슬롯 문구와 선택·삭제 버튼을 갱신한다.</summary>
        private void Refresh()
        {
            _titleLabel.text = GameData.L(_isSettingsPicker ? "save.loadSlot" : "save.manage");
            _hintLabel.text = GameData.L(_isSettingsPicker ? "save.loadSlotHint" : "save.slotHint");
            for (int index = 0; index < SLOT_COUNT; index++)
            {
                int slot = index + 1;
                bool hasFile = SaveStore.HasSlot(slot);
                bool hasSave = SaveStore.TryLoadSlot(slot, out PlayerSave save);
                string slotName = GameData.L("save.slot") + " " + slot;
                if (hasSave)
                {
                    _slotLabels[index].text = slotName + " · " + GameData.L("save.stage") + " " + save.SelectedStage
                        + " · " + GameData.L("save.highestStage") + " " + save.HighestClearedStage
                        + " · RAM " + save.Currency;
                    bool isActive = SaveStore.ActiveSlot == slot;
                    string buttonKey = isActive && !_isSettingsPicker ? "save.selected" : _isSettingsPicker ? "save.load" : "save.select";
                    SetButtonLabel(_selectButtons[index], GameData.L(buttonKey));
                    _selectButtons[index].interactable = !isActive || _isSettingsPicker;
                }
                else
                {
                    _slotLabels[index].text = slotName + " · " + GameData.L(hasFile ? "save.slotReadError" : "save.slotEmpty");
                    string buttonKey = hasFile ? "save.slotReadError" : _isSettingsPicker ? "save.slotEmpty" : "save.newGame";
                    SetButtonLabel(_selectButtons[index], GameData.L(buttonKey));
                    _selectButtons[index].interactable = !_isSettingsPicker && !hasFile;
                }
                _deleteButtons[index].gameObject.SetActive(!_isSettingsPicker && hasFile);
                _deleteButtons[index].interactable = hasFile;
            }
        }

        /// <summary>팝업 UI를 최초 표시 때 공용 패널·버튼·글꼴 자산으로 만든다.</summary>
        private void BuildIfNeeded()
        {
            if (_isBuilt) return;
            UiFactory factory = new UiFactory(_font, _panelPrefab, _buttonPrefab);
            Image shade = factory.Panel(transform, 0, 0, UiTheme.SCREEN_WIDTH, UiTheme.SCREEN_HEIGHT, "Shade").GetComponent<Image>();
            shade.color = new Color(0.025f, 0.055f, 0.09f, 0.86f);
            shade.raycastTarget = true;

            RectTransform panel = factory.Panel(transform, 230, 78, 820, 564, "SaveSlotsPanel");
            _titleLabel = factory.Text(panel, 32, 22, 756, 42, GameData.L("save.manage"), 28, FontStyles.Bold, "Title");
            _hintLabel = factory.Text(panel, 32, 68, 756, 28, GameData.L("save.slotHint"), 15, FontStyles.Normal, "Hint");

            for (int index = 0; index < SLOT_COUNT; index++)
            {
                int slot = index + 1;
                float rowY = 112 + index * 108;
                RectTransform row = factory.Panel(panel, 32, rowY, 756, 90, "SlotRow" + slot);
                _slotLabels[index] = factory.Text(row, 18, 13, 488, 64, string.Empty, 16, FontStyles.Normal, "SlotLabel");
                _selectButtons[index] = factory.Button(row, 516, 22, 104, 46, string.Empty,
                    () => SelectOrCreateSlot(slot), 14, "SelectSlot" + slot);
                _deleteButtons[index] = factory.Button(row, 632, 22, 106, 46, GameData.L("save.delete"),
                    () => ConfirmDeleteSlot(slot), 14, "DeleteSlot" + slot);
            }

            factory.Button(panel, 650, 488, 138, 48, GameData.L("ui.close"), Close, 16, "CloseButton");
            _isBuilt = true;
        }

        /// <summary>설정 선택 모드에서는 슬롯 전환을 확인하고, 타이틀에서는 기존 슬롯 선택이나 빈 슬롯 생성을 처리한다.</summary>
        private void SelectOrCreateSlot(int slot)
        {
            if (_isSettingsPicker)
            {
                string confirmKey = slot == SaveStore.ActiveSlot ? "save.loadConfirm" : "save.switchConfirm";
                _ui.Confirm(GameData.L("save.load"), GameData.L(confirmKey), GameData.L("save.load"), () => LoadSelectedSlot(slot));
                return;
            }

            bool isSuccessful = SaveStore.HasSlot(slot)
                ? _session.SelectSaveSlot(slot)
                : _session.CreateSaveSlot(slot);
            if (!isSuccessful)
            {
                _ui.Alert(GameData.L("save.manage"), GameData.L(SaveStore.LastWarning == null ? "save.slotReadError" : "save.unavailable"));
                Refresh();
                return;
            }
            Refresh();
        }

        /// <summary>확인된 저장 슬롯을 활성 슬롯으로 전환하고 팝업을 닫은 뒤 설정 표시 갱신을 알린다.</summary>
        private void LoadSelectedSlot(int slot)
        {
            bool isLoaded = slot == SaveStore.ActiveSlot
                ? _session.LoadActiveSave()
                : _session.SelectSaveSlot(slot);
            if (!isLoaded)
            {
                _ui.Alert(GameData.L("save.load"), GameData.L("save.unavailable"));
                Refresh();
                return;
            }

            Close();
            _onSlotChanged?.Invoke();
        }

        /// <summary>지정 슬롯 삭제를 확인받고 삭제 후 목록을 갱신한다.</summary>
        private void ConfirmDeleteSlot(int slot)
        {
            _ui.Confirm(GameData.L("save.delete"), GameData.L("save.deleteConfirm"), GameData.L("save.delete"), () =>
            {
                if (!_session.DeleteSaveSlot(slot))
                {
                    _ui.Alert(GameData.L("save.delete"), GameData.L("save.unavailable"));
                    return;
                }
                Refresh();
            });
        }

        /// <summary>슬롯 버튼의 TMP 레이블 문구를 갱신한다.</summary>
        private static void SetButtonLabel(Button button, string value)
        {
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = value;
        }
    }
}
