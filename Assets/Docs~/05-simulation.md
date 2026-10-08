# 05. 시뮬레이션 계층

`Assets/RuneCode/Core/Simulation/` 3개 파일이 게임 플레이 규칙 전부를 담당한다.

| 파일 | 내용 |
|---|---|
| `RuneSimulation.cs` | 60Hz 고정 틱 루프, 마법 실행, 적 AI, 피해 파이프라인, 정산, 상태 해시 |
| `SimulationTypes.cs` | 벡터·입력·플레이어·적·마법 개체·적 탄환·오브·피해 숫자·노드 이벤트 |
| `SpellRuntime.cs` / `SpellRuntimeTypes.cs` | 토큰 실행, 발사 묶음, 투사체, 적중 토큰 (04 참고) |

## 5.1 결정성 설계

| 장치 | 내용 |
|---|---|
| 고정 시간 | `TICK_RATE = 60`, `STEP_SECONDS = 1/60`. `Time` 프로퍼티는 `_tick × STEP_SECONDS`로 **누적 오차가 없다** |
| 실수 타입 | 시뮬레이션 전체가 `double`. `float`는 데이터 적재와 표시에만 쓴다 |
| 난수 | mulberry32 기반 `NextRandom()`. 상태는 `uint _rngState`, 초기값은 시드. `UnityEngine.Random` 미사용 |
| 정렬 안정화 | 적중 대상은 거리 제곱 비교로 정렬, 컴파일 단계의 실행 순서는 Ordinal 정렬 |
| 문자열 서식 | 상태 해시는 `"R"` 서식 + `CultureInfo.InvariantCulture` |
| Unity 비의존 | `Time.deltaTime`, 입력, `MonoBehaviour`를 쓰지 않는다. 유일한 Unity 의존은 생성 시 `Resources.Load<TextAsset>` |

`StateHash()`는 틱·RNG 상태·스테이지·플레이어·적·마법 런타임(토큰·합류 대기·발사 요청·투사체)·프로그램 서명·적 탄환·오브·대기 중 연사·누적 피해를 한 문자열에 쓰고 FNV-1a 32비트로 접어 8자리 16진수로 반환한다. CLI 리포트가 이 값을 포함하므로 **같은 시드·입력이 같은 해시를 내는지로 회귀를 검증**할 수 있다.

적의 상태 질의(`IsBurning`, `IsFrozen` 등)가 현재 시간을 인자로 받지 않는 대신 `Observe(time)`로 틱 시간을 주입받는 패턴을 쓴다. 덕분에 `SimulationEnemy`는 시뮬레이션을 역참조하지 않으면서도 시간 의존 상태를 노출할 수 있다.

## 5.2 생성과 모드

```csharp
new RuneSimulation(seed = 1, isMission = false, maxHp = 0, maxEnergy = 0,
                   stageNumber = 1, battleDuration = 0, energyRegen = 0)
```

생성자는 `GameData.Load()`를 호출한 뒤 `enemies` / `sector1` / `governor` / `incremental` JSON을 **인스턴스별로** 읽는다. 0으로 넘긴 능력치는 `balance.json` 기본값으로 대체된다. 마지막에 모드에 따라 분기한다.

| 모드 | 조건 | 초기화 |
|---|---|---|
| 시간제 전투 | `isMission == true` | `BeginTimedBattle()` — `MissionStage.Combat`, `incremental.Tiles` 맵, 왼쪽 시작 위치, 첫 적 유입 + 다음 생성 틱 설정 |
| 시험 도크 | `isMission == false` | `ResetBench("dummy_single")` — `MissionStage.Bench`, `sector1.Rooms[0].Tiles` 맵 |

`MissionStage`는 `Bench`, `Combat`, `Terminal`, `Cleared`, `Dead`다. 현재 흐름에서 `Terminal`에는 진입하지 않는다(`InteractTerminal()`이 항상 false).

### 시험 도크 시나리오

`ResetBench(scenario)`는 미션 인스턴스에서 호출하면 `InvalidOperationException`이고, 알 수 없는 이름이면 `ArgumentException`이다. 시드·틱·통계·개체·마법 실행 상태를 전부 초기화한 뒤(프로그램은 유지) 시나리오별로 적을 배치한다.

| 시나리오 | 배치 |
|---|---|
| `dummy_single` | 더미 1기 (경감 0, 무한 체력) |
| `dummy_line` | 일렬 더미 5기 |
| `dummy_swarm` | 이동하는 scout 5×2 (경감 12) |
| `aegis` | 이지스 1기(더미 플래그, 경감 0) |
| `adapt_loop` | 더미 1기, 처치되면 매 틱 즉시 재생성 (적응은 제거됨) |

### 마법 프로그램

`SetProgram(program)`은 읽기 전용 실행 그래프(`SpellProgram`)를 `SpellRuntime`에 넘기고 진행 중인 토큰·투사체·합류 대기를 지운다. `ClearSpellState()`는 프로그램은 두고 실행 상태만 지운다(편집 진입·그래프 변경 시, 규칙 9절). 미션과 도크 모두 마법은 하나다.

## 5.3 틱 순서 — `Step(SimulationInput)`

```
if (Completed || IsDead) return;                 ← 종료 후 틱은 무시

 1  Player.Aim(input.AimDirection)
 2  Player.Advance(dt, Time, energyRegen)        마나풀 회복 / 보호막 만료 / 대시 잔여
 3  MovePlayer(input)                            대시 개시 판정 → 변위 계산 → MissionMap.Move
 4  if (mission) AdvanceEnemyStreaming()         오른쪽 적 유입
 5  if (input.CastA) TryCast()                   앱이 클릭한 틱에만 true로 넘긴다
 6  AdvanceEnemies()                             AI: 접근·접촉·예고·연사·돌진·보스
 7  RunEnemyShots()                              예약된 연사 발사
 8  Spell.AdvanceProjectiles()                   투사체 이동·충돌·적중 토큰 (규칙 4.2-2)
 9  Spell.ProcessTokens()                        토큰 처리 (규칙 4.2-3)
10  Spell.FlushFireRequests()                    발사 요청 묶음 → 투사체 생성 (규칙 4.2-4)
11  AdvanceHostileProjectiles()                  적 탄환·장판 이동과 플레이어 피격
12  CleanupEnemies()                             사망 적 제거 + 처치 통계 + RAM 오브 생성
13  CollectNearbyOrbs()                          흡수 반경 내 오브 회수
14  사망 / adapt_loop 재생성 판정
15  _tick++
16  if (mission && 제한시간 도달) CompleteTimedBattle()
17  TrimFeedback()                               피해 숫자·노드 이벤트·5초 DPS·소멸 표시 정리
```

순서상 의도가 드러나는 지점.

* **5번(시전)이 9번(토큰 처리)보다 먼저**다. 시전한 틱에 새 토큰의 대기 시간이 바로 줄기 시작한다(계획서 I1).
* **8번(투사체)이 9번(토큰)보다 먼저**다. 적중으로 생긴 토큰은 9번에서 건너뛰고 다음 틱부터 처리한다(I2).
* **15번(`_tick++`)이 마지막 근처**다. 틱 안의 모든 로직이 같은 `Time` 값을 본다.
* **16번의 제한시간 검사가 `_tick++` 뒤**다. 그래서 `battleDuration`에 정확히 도달한 틱까지 처리된 다음 종료된다.
* `Step`의 입력 `Interact`는 현재 사용되지 않는다(터미널 흐름 제거의 잔존 필드).

### 플레이어 이동

```
move = |input.Movement| > 1 ? 정규화 : 그대로
if (input.Dash) Player.StartDash(move 또는 조준 방향, dashSeconds, dashCooldown, Time)
변위 = 대시 중이면 (dashDistance / dashSeconds) × min(dt, 남은 대시 시간) × 대시 방향
       아니면       moveSpeed × dt × move
Player.Move(map.Move(위치, 변위, playerRadius))
```

대시는 무적을 함께 부여하고(`invulnerableUntil = max(기존, Time + seconds)`), 이동은 항상 `MissionMap.Move`를 통과하므로 벽 통과가 불가능하다.

## 5.4 마법 실행 모델

토큰 실행·발사 묶음·투사체 규칙은 [04](04-graph-and-compiler.md) 4.4절에 있다. 시뮬레이션은 `SpellRuntime`을 만들 때 두 콜백을 넘긴다.

| 콜백 | 처리 |
|---|---|
| `OnNodePaid(node)` | 지불에 성공한 도착마다 `NodeExecutionEvent`를 남긴다(하이라이트·텔레메트리) |
| `OnProjectileHit(enemy, projectile)` | 살아 있고 보스 패치 중이 아니면 `enemy.TakeSpellDamage(위력)` = `max(0, 위력 − 경감)`. 피해 숫자는 0이어도 표시하고, 피해가 있으면 누적·DPS에 더한 뒤 보스면 페이즈 전환을 검사한다 |

적응 학습, 속성 상태이상(화염·냉기·전격), 이지스 전면 감쇠, 릴레이 오라 감쇠는 결정 D2에 따라 피해 경로에서 제거했다. 적 데이터의 상태이상 필드와 냉기 감속 계산은 남아 있지만 값이 쌓이지 않으므로 효과가 없다.

## 5.5 적 AI

`AdvanceEnemies`는 루프 시작 시 개체 수를 저장해(`initialCount`) **이번 틱에 새로 생성된 적은 같은 틱에 행동하지 않게** 한다. 더미와 빙결 상태는 건너뛴다.

| 적 | 행동 |
|---|---|
| `enemy.scout` 등 기본 | 플레이어 방향으로 `Speed` 이동, 공격 쿨이 지나고 접촉하면 피해 |
| `enemy.sentry` | 미션에서는 `StationaryAdvanceSpeed`로 느리게 전진, 도크에서는 제자리. `WarningSeconds` 예고 후 3연사 발사 |
| `enemy.hunter` | 사거리 안이면 예고 → 돌진(`DashSpeed`, `DashSeconds`) 중 접촉 피해, 그 외에는 추적 |
| `enemy.relay` | 공격하지 않고 전진만. 효과는 피해 파이프라인의 오라 |
| `enemy.aegis` | 기본 추적 + 전면 방패(피해 파이프라인에서 처리) |
| `boss.governor` | `AdvanceBoss` |

이동은 모두 `MoveEnemy`를 거치며 냉기 중첩에 따라 `1 - ChillSlow × stacks`로 감속된다.

### 발사 처리

`ShootFan(적, 방향, 발수, 속도배율)`은 `SpreadDegrees` 안에서 부채꼴로 탄환을 만든다. `ShootBurst`는 첫 발을 즉시 쏘고 나머지 2발을 `BurstInterval` 간격으로 `_pendingEnemyShots`에 예약한다. `RunEnemyShots`는 예약 시점에 **소유 적이 살아 있고 패치 중이 아니며 빙결이 아닐 때만** 발사하고, 그렇지 않으면 예약을 폐기한다. 발사 위치는 예약 시점이 아닌 **발사 시점의 적 위치**다.

### 보스 (`AdvanceBoss`)

```
CheckBossPatch → 패치 중이면 아무 것도 하지 않음
배율 = 3페이즈면 PhaseThreeMultiplier
선호 거리보다 멀면 접근, 가까우면 제자리에서 조준만
공격 시점:
    페이즈 1 또는 3 → RadialCount 발 방사
    페이즈 2 이상   → 조준 3연사
    다음 공격 = 페이즈2면 AimInterval, 그 외 RadialInterval (배율로 단축)
페이즈 2 이상 → HazardInterval 마다 플레이어 위치에 예고 장판 생성
ReinforcementInterval 마다 scout ReinforcementCount 기 소환
접촉 피해 판정
```

`CheckBossPatch`는 HP 비율이 문턱을 넘을 때 페이즈를 올리고 `PatchSeconds` 동안 무적(`IsPatching`)을 준 뒤 무적이 끝나면 다시 피해를 받는다(옛 적응 태그 고정은 D2로 제거). 공격 재개 시점도 패치 종료 + 예고 시간 뒤로 밀린다.

## 5.6 적 유입과 정산 (시간제 전투)

```
SpawnIntervalTicks()
  progress = clamp(Time / battleDuration, 0, 1)
  seconds  = BaseSpawnInterval / (1 + (stage-1) × IntervalStageScale) × (1 - progress × IntraStageRamp)
  return max(1, round(max(MinSpawnInterval, seconds) × 60))
```

스테이지가 올라갈수록, 그리고 한 전투 안에서도 시간이 갈수록 유입이 빨라지되 `MinSpawnInterval` 아래로는 내려가지 않는다.

`SpawnIncomingEnemy`는 현재 스테이지에서 해금된 편성만 가중치 합으로 모아 `NextRandom()`으로 한 종류를 뽑고, `x = SpawnX`의 `y` 구간 안 무작위 위치에 생성한다. `SpawnEnemy`는 스테이지 배율(HP `1 + (stage-1) × HpStageScale`, 피해 동일 방식)과 편성별 보상 오버라이드를 적용하며, 좌표는 `NearestFree`로 보정한다.

정산 경로:

| 상황 | 처리 |
|---|---|
| 제한시간 도달 | `CompleteTimedBattle()` — 남은 오브 전량 회수, `Cleared`, 진행 중 개체·예약 취소 |
| HP 0 | `Dead`, 오브 전량 회수, 개체·예약 취소 |
| 적 처치 | `CleanupEnemies()`가 위치에 `FragmentOrb(보상)` 생성, 종류별 처치 수 누적 |
| 오브 회수 | `OrbAbsorbRadius`(160) 안이면 자동 흡수 |

`SettlementFragments`는 사망 시 `round(수집량 × DeathRetention)`, 그 외에는 수집량 그대로다. `EarnedFragments`는 수집량 + 미회수 오브 합계로 HUD 표시에 쓰인다.

## 5.8 상태 모델 (`SimulationTypes.cs`)

| 타입 | 종류 | 핵심 상태 |
|---|---|---|
| `SimVector` | readonly struct | `X`, `Y`, 길이·정규화·회전·내적·연산자 |
| `SimulationInput` | readonly struct | 이동, 조준, CastA/B/C, Dash, Interact |
| `MissionStage` | enum | `Bench`, `Combat`, `Terminal`, `Cleared`, `Dead` |
| `SimulationPlayer` | class | 위치·조준·HP·EN·보호막·대시·무적·슬롯 쿨다운 3개. 변경자는 모두 `internal` |
| `SimulationEnemy` | class | 위치·방향·HP·공격 예약·화염/냉기/빙결/EMP/EMP면역·플래시·보스 페이즈·패치·증원·장판 시점·최근 피해 태그 |
| `SpellToken` / `SpellProjectile` | class (`SpellRuntimeTypes.cs`) | 토큰: 노드·포트·대기·마나·위력·개수·원점·세대·통과 횟수·합류 대기. 투사체: 위력·운반 마나·세대·무시할 적·위치·방향·이동 거리 |
| `SimulationProjectile` | class | 적 탄환/예고 장판. 위치·방향·속도·피해·반경·수명·예고 시간 |
| `FragmentOrb` | readonly struct | 위치, 수량 |
| `DamageNumber` | readonly struct | 위치, 수치, 속성, 틱 |
| `NodeExecutionEvent` | readonly struct | 노드 ID, 틱 |

접근 제어가 일관적이다. 외부(뷰·앱)는 `public` 프로퍼티로 **읽기만** 하고, 상태를 바꾸는 메서드는 전부 `internal`이라 `RuneCode.Core` 밖에서는 호출할 수 없다. 어셈블리 경계가 캡슐화를 실제로 보장한다.

모든 상태 타입은 `WriteState(StringBuilder)`를 가지며 `StateHash()`가 이를 순서대로 호출한다.

## 5.9 런타임 예산 정리

| 상한 | 값 (`balance.json`) | 초과 시 |
|---|---|---|
| 활성 토큰 `_spell._maxTokens` | 64 | 시전 실패('과부하'), 분배 뒤쪽 몫·적중 토큰 마나 소멸 |
| 활성 투사체 `_spell._maxProjectiles` | 200 | 만들지 못한 발의 마나 소멸('과부하') |
| 토큰 수명 `_spell._tokenLifetime` | 3초 | `Expired` 소멸 |
| 틱당 토큰 처리 노드 `_spell._maxNodesPerFrame` | 32 | 다음 틱에 이어서 진행 |
| `MaxEnemies` | 300 | 유입·소환 중단 |
| `MaxFrameSteps` | 5 | 앱 계층의 한 프레임 최대 틱 수(§06) |

### 피드백 수명 (`TrimFeedback`)

피해 숫자는 `Sim.DamageNumberSeconds × 60`틱, 노드 실행 이벤트는 60틱(1초)까지 유지되고, 5초 롤링 DPS 윈도는 그보다 오래된 샘플을 빼면서 합계를 갱신한다. 세 목록 모두 상한이 있어 장시간 전투에서도 메모리가 증가하지 않는다.
