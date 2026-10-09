using System;

using UnityEngine.InputSystem;

namespace RuneCode
{
    /// <summary>
    /// 미션 전투 한 판의 시뮬레이션, 고정 스텝 입력, 일시정지, 정산을 책임지는 일반 클래스다.
    /// MissionScreen이 소유하며 세이브 반영은 RuneCodeSession.SettleMission으로 위임한다.
    /// </summary>
    public sealed class MissionRun
    {
        private const double FIXED_STEP = 1.0 / RuneSimulation.TICK_RATE;

        private readonly RuneCodeSession _session;
        private readonly CompiledSpell _spell;
        private readonly string _spellName;
        private readonly RuneSimulation _simulation;

        private double _accumulator;
        private int _telemetryCount;
        private bool _hasPendingDash;
        private bool _isPaused;
        private bool _settled;
        private string _lastResult;

        /// <summary>현재 전투 시뮬레이션을 반환한다. 경기장 그래픽과 HUD가 읽는다.</summary>
        public RuneSimulation Simulation => _simulation;

        /// <summary>시전할 마법 이름을 반환한다.</summary>
        public string SpellName => _spellName;

        /// <summary>시전할 마법의 에너지 비용을 반환한다.</summary>
        public float SpellCost => _spell?.EnergyCost ?? 0f;

        /// <summary>일시정지 상태를 반환한다.</summary>
        public bool IsPaused => _isPaused;

        /// <summary>결과 정산이 끝나 전투가 종료되었는지 반환한다.</summary>
        public bool IsFinished => _settled;

        /// <summary>정산 후 표시할 결과 문구를 반환한다.</summary>
        public string LastResult => _lastResult;

        /// <summary>세션의 능력치와 선택 스테이지로 전투를 만들고 마법과 해금 속성을 연결한 뒤 시작 텔레메트리를 기록한다.</summary>
        public MissionRun(RuneCodeSession session, CompiledSpell spell, string spellName)
        {
            _session = session;
            _spell = spell;
            _spellName = spellName;
            _simulation = new RuneSimulation(1, true, session.MaxHp, session.MaxEnergy, session.SelectedStage, session.EnergyRegen);
            _simulation.SetLoadout(new[] { spell });
            _simulation.SetUnlockedElements(session.GetUnlockedElements());
            _simulation.SetAreaBoxUpright(session.IsAreaBoxUpright);
            LocalTelemetry.Record(0, "mission.start", session.SelectedStage + ":" + spell.Signature);
        }

        /// <summary>미션의 고정 시뮬레이션 갱신을 일시정지하거나 재개한다.</summary>
        public void TogglePause() { _isPaused = !_isPaused; }

        /// <summary>이동·대시 키와 포인터 조준을 모아 MaxFrameSteps 제한 안의 고정 스텝만큼 시뮬레이션을 갱신하고, 노드 텔레메트리 기록과 종료 조건을 확인한다.</summary>
        public void Step(bool isTyping, bool hasPointer, SimVector pointer, float unscaledDeltaTime)
        {
            if (_settled) return;
            if (_isPaused) { _accumulator = 0; _hasPendingDash = false; return; }
            var keyboard = Keyboard.current;
            var movement = SimVector.Zero;
            if (!isTyping && keyboard != null)
            {
                movement = new SimVector((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.sKey.isPressed ? 1 : 0) - (keyboard.wKey.isPressed ? 1 : 0));
                _hasPendingDash |= keyboard.spaceKey.wasPressedThisFrame;
            }
            var maxSteps = GameData.Balance.Limits.MaxFrameSteps;
            _accumulator = Math.Min(_accumulator + unscaledDeltaTime, FIXED_STEP * maxSteps);
            var steps = 0;
            while (_accumulator >= FIXED_STEP && steps++ < maxSteps)
            {
                var aim = hasPointer ? pointer - _simulation.Player.Position : _simulation.Player.AimDirection;
                _simulation.Step(new SimulationInput(movement, aim, hasPointer, false, false, _hasPendingDash));
                _hasPendingDash = false;
                _accumulator -= FIXED_STEP;
            }
            CaptureNodeTelemetry();
            if (_simulation.Completed || _simulation.IsFailed) FinishMission();
        }

        /// <summary>클리어 보상을 포함한 클리어, 사망 또는 보스 제한시간 초과 보상을 한 번만 정산하고 결과 문구와 텔레메트리를 남긴다. 세이브 반영은 세션의 SettleMission이 한다.</summary>
        public void FinishMission()
        {
            if (_settled) return;
            _settled = true;
            var cleared = _simulation.Completed;
            var fragments = _simulation.SettlementFragments;
            _session.SettleMission(_simulation.StageNumber, fragments, cleared, _simulation.KillCounts);
            var result = cleared ? "clear" : _simulation.IsTimedOut ? "timeout" : "death";
            var resultKey = cleared ? "result.stageCleared" : _simulation.IsTimedOut ? "result.timeout" : "result.dead";
            _lastResult = GameData.L(resultKey) + "\n" + GameData.L("result.fragments") + " " + fragments;
            if (cleared) _lastResult += "  ·  " + GameData.L("result.clearReward") + " " + _simulation.ClearReward;
            _isPaused = false;
            LocalTelemetry.Record(_simulation.Tick, "mission.result", result + ":" + fragments);
        }

        /// <summary>전투를 중단하여 사망과 같은 보존 비율로 현재 획득 조각을 한 번만 정산하고 결과 문구를 남긴다.</summary>
        public void AbandonMission()
        {
            if (_settled) return;
            _settled = true;
            var fragments = (int)Math.Round(_simulation.EarnedFragments * GameData.Balance.Economy.DeathRetention, MidpointRounding.AwayFromZero);
            _session.SettleMission(_simulation.StageNumber, fragments, false, _simulation.KillCounts);
            _lastResult = GameData.L("result.retreat") + "\n" + GameData.L("result.fragments") + " " + fragments;
            _isPaused = false;
        }

        /// <summary>디버그 실행에서만 미션 플레이어 무적을 토글한다.</summary>
        public void DebugInvulnerable()
        {
            if (!_session.IsDebugEnabled) return;
            _simulation.SetDebugInvulnerable(!_simulation.DebugInvulnerable);
        }

        /// <summary>디버그 실행에서만 지정한 종류의 적을 미션에 생성한다. 엘리트 지정 시 엘리트 배율로 생성한다.</summary>
        public void DebugSpawn(string kind = "enemy.scout", bool isElite = false)
        {
            if (!_session.IsDebugEnabled) return;
            _simulation.DebugSpawn(kind, isElite);
        }

        /// <summary>새로 실행된 노드만 로컬 링 버퍼에 기록하여 동일 tick 실행도 보존한다.</summary>
        private void CaptureNodeTelemetry()
        {
            var count = _simulation.NodeExecutionCount - _telemetryCount;
            var start = Math.Max(0, _simulation.NodeEvents.Count - count);
            for (var index = start; index < _simulation.NodeEvents.Count; index++)
            {
                var entry = _simulation.NodeEvents[index];
                LocalTelemetry.Record(entry.Tick, "mission.node", entry.NodeId);
            }
            _telemetryCount = _simulation.NodeExecutionCount;
        }
    }
}
