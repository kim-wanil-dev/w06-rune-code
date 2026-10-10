namespace RuneCode
{
    /// <summary>출격 탭 Presenter다. 해금 범위 안의 스테이지 선택과 출격 준비 검증 후 미션 화면 전환을 처리한다.</summary>
    public sealed class DeployPresenter
    {
        private readonly RuneCodeSession _session;
        private readonly DeployPanel _view;

        /// <summary>세션과 출격 View를 받아 View 이벤트를 연결한다.</summary>
        public DeployPresenter(RuneCodeSession session, DeployPanel view)
        {
            _session = session;
            _view = view;
            _view.Bind();
            _view.StageStepClicked += ChangeStage;
            _view.LaunchClicked += Launch;
        }

        /// <summary>해금 최고 단계, 선택 스테이지, 제한시간과 현재 마법 문구를 다시 표시한다.</summary>
        public void Refresh()
        {
            if (_session.IsPuzzleBattle)
            {
                PuzzleBattleDefinition puzzle = _session.Puzzle;
                _view.SetContent(GameData.L("ui.puzzle") + " · P" + _session.SelectedPuzzle + " / " + GameData.Puzzles.Count,
                    "P" + _session.SelectedPuzzle + ": " + puzzle.Name,
                    GameData.L("puzzle.mana") + " " + puzzle.MaxEnergy.ToString("0.#") + " · "
                    + GameData.L("puzzle.regen") + " " + puzzle.EnergyRegen.ToString("0.#") + "/s\n"
                    + (puzzle.TimeLimit > 0 ? puzzle.TimeLimit.ToString("0.#") + "s" : GameData.L("puzzle.noTimeLimit"))
                    + " · " + GameData.L("ui.kills") + " " + puzzle.KillTarget + " / " + puzzle.EnemyCount,
                    GameData.L("ui.singleSpell") + " " + _session.Spells.SpellName + " · RAM " + _session.EquippedRam + "/" + puzzle.Capacity);
                _view.SetPuzzleMode(true);
                return;
            }
            _view.SetPuzzleMode(false);
            _view.SetContent(
                GameData.L("ui.highestStage") + "  " + _session.HighestClearedStage,
                GameData.L("ui.stage") + " " + _session.SelectedStage,
                GameData.L("ui.duration") + "  " + _session.BattleDuration.ToString("0") + "s",
                GameData.L("ui.singleSpell") + "  " + _session.Spells.SpellName + "  ·  " + GameData.L("ui.capacity") + " "
                    + _session.EquippedRam + "/" + _session.Capacity);
        }

        /// <summary>해금 범위 안에서 선택 스테이지를 이동하고 표시를 갱신한다.</summary>
        private void ChangeStage(int direction)
        {
            if (_session.IsPuzzleBattle) _session.SelectPuzzle(_session.SelectedPuzzle + direction);
            else _session.SelectStage(_session.SelectedStage + direction);
            Refresh();
        }

        /// <summary>마법을 저장·검증해 준비가 되면 미션 화면 전환을 요청한다.</summary>
        private void Launch()
        {
            if (_session.TryPrepareMission(out _)) _session.RequestScreen(AppScreen.Mission);
        }
    }
}
