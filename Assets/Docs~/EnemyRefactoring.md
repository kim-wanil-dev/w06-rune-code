# 적 상태 정의 및 시뮬레이션 구조 리팩토링

## 리팩토링 대상

- Enemy이동 : 현재 enum에 따라서 이동 위치를 RuneSimulation내에서 계산 중이다. Move의 최종 처리는 simulation에서 처리하지만, 이동 위치를 선정하는 기능은 다형화된 클래스에서 처리하게 한다.
- SimulationEnemy 객체 하나가 하나의  이동을 처리하는 객체를 보유하는 형태가 아닌, 값만 계산해서 리턴해주는 중앙 객체(또는 static)이 있어야한다.
- 상태이상 : simulation이 상태이상을 하나의 데이터로 취급하여 시작과 만료를 직접 관리한다. 상태이상 데이터는 적 상태 데이터의 id를 보유하고 상태이상 처리 등에서 이용한다.
- 플레이어의 공격 타입을 구조화 하지는 않는다. 이후의 단계이다.

---

## 1. 현재 구조 진단

현재 코드(`RuneSimulation.cs`, `SimulationTypes.cs`) 기준이다. 적 동작 전반은 `ENEMY_SPAWN_DAMAGE.md`에 정리되어 있다.

### 1.1 이동

- 적 종류는 enum이 아니라 `SimulationEnemy.Kind`(= `EnemyDefinition.Id` 문자열)이다. `AdvanceEnemies`(`RuneSimulation.cs:620`)가 `"enemy.relay"`, `"enemy.sentry"`, `"enemy.hunter"`, `"boss.governor"` 문자열 비교 분기로 종류를 나눈다.
- 한 분기 안에 **이동 목표 계산**, **공격 타이머 전이**(예고·돌진·사격), **최종 이동 적용**(`MoveEnemy`)이 섞여 있다. 새 이동 방식을 추가하려면 이 분기 사슬을 직접 고쳐야 한다.
- 종류별 실제 이동 규칙:

| 종류 | 이동 규칙 |
|---|---|
| 스카우트, 이지스 | 매 틱 플레이어 방향으로 `speed` |
| 센트리 | 미션: `stationaryAdvanceSpeed`로 전진 / 시험 도크: 방향만 회전 |
| 릴레이 | 미션: `stationaryAdvanceSpeed`로 전진 / 시험 도크: 아무것도 안 함 |
| 헌터 | 돌진 중: 고정 방향 `dashSpeed` / 예고 중·예고 시작 틱·돌진 시작 틱: 정지 / 그 외: 추적 `speed` |
| 거버너 | 패치 중: 정지 / `preferredDistance`보다 멀면 추적(3페이즈 ×1.3) / 가까우면 방향만 회전 |

### 1.2 상태 이상

- 화염·냉기·빙결·EMP의 상태가 `SimulationEnemy`의 필드 9개로 흩어져 있다(`_burnUntil`, `_burnNext`, `_burnForm`, `_burnNoise`, `_chillUntil`, `_chillStacks`, `_freezeUntil`, `_freezeImmuneUntil`, `_empUntil`).
- 시작은 `SimulationEnemy.Burn/Chill/Emp`, 만료는 `Observe(time)`의 관측 시각 비교와 냉기 스택 초기화, 화염 틱은 `RuneSimulation.AdvanceStatuses`로 **세 곳에 나뉘어** 있다.
- 상태 조회 지점: `Evaluate`(조건 룬 `targetHasStatus`), `ApplyDamage`(이지스 EMP), `AdvanceEnemies`(빙결 시 행동 정지), `MoveEnemy`(냉기 감속), `RunEnemyShots`(빙결 시 연사 취소), `RuneArenaGraphic.DrawEnemy`(아이콘 표시).

---

## 2. 리팩토링 방향성

### 2.1 원칙

1. **판단과 적용의 분리**: 이동 목표는 다형화된 이동 클래스가 *값으로* 계산하고, 벽 충돌·냉기 감속·위치 갱신은 시뮬레이션이 적용한다.
2. **적은 이동 객체를 소유하지 않는다**: `SimulationEnemy`는 이동 종류(enum 값)만 가진다. 이동 클래스는 상태 없는 공유 인스턴스이고 중앙 static 클래스가 종류별로 찾아 호출한다.
3. **상태 이상은 시뮬레이션이 소유하는 데이터다**: 상태 이상 1건 = 데이터 1개(대상 적 ID 포함). 부여·틱·만료·제거를 시뮬레이션이 한곳에서 처리하고, 적 객체는 상태 이상 필드를 갖지 않는다.
4. **동작 보존**: 플레이 규칙, 수치, 틱 단위 판정 순서를 바꾸지 않는다. 같은 시드·입력이면 피해량·처치 수·스폰 수가 리팩토링 전과 같아야 한다.
5. **결정성과 할당**: 매 틱 할당을 늘리지 않는다. 이동 판단의 입력·출력은 struct로 전달하고, 상태 이상 처리 순서는 기존 순서(적 목록 순서)를 따른다.

### 2.2 범위: Enemy 단계까지만

이번 리팩토링은 **적 쪽 구조**만 정리한다. 플레이어 공격과 맞닿은 부분은 현재 형태를 그대로 두고, 다음 단계(플레이어 공격 타입 구조화)에서 다룬다.

| 구분 | 포함 (이번 단계) | 제외 (이후 단계) |
|---|---|---|
| 이동 | 종류별 이동 목표 계산의 다형화, 중앙 static 조회 | 이동 종류의 데이터(JSON) 이전 |
| 상태 이상 | 화염·냉기·빙결·EMP의 데이터화, 시뮬레이션의 부여·틱·만료·조회 | 속성 문자열(`fire/ice/arc`)에서 상태 이상을 고르는 규칙 |
| 적 공격 | 이동 분리에 필요한 처리 순서 정리만 | 접촉·연사·예고·돌진·보스 패턴의 구조화 |
| 피해 처리 | 상태 조회 경로 교체만 | `ApplyDamage`의 이지스 방패·릴레이 오라 판정, 마법 형태별 적중 판정 |
| 보스 | 이동 판단 분리 | 패치(페이즈 전환 무적), 증원, 장판 |
| 데이터 | 변경 없음 | `enemies.json` 스키마 변경 |

이지스 방패는 `form == "bolt" && !isDot`, 상태 부여는 `element == "fire"` 같은 **플레이어 공격 타입 문자열**에 의존한다. 이 부분을 지금 구조화하면 플레이어 공격 타입까지 함께 건드려야 하므로 이번 단계에서 제외한다.

---

## 3. 목표 구조

### 3.1 이동

새 파일 `Core/Simulation/EnemyMovement.cs` 하나에 둔다(`RuneCode.Core` 어셈블리, 모두 `internal`).

```csharp
internal enum EnemyMovementType { Chase, Turret, Anchor, DashChase, KeepDistance }

internal enum EnemyMoveMode { Stay, Face, Move }

/// 이동 판단에 필요한 값. 시뮬레이션이 이동 전에 한 번 계산해 전달한다.
internal readonly struct EnemyMoveContext
{
    // Offset(플레이어 - 적), Direction(정규화), Time, IsMission,
    // AdvanceSpeed(incremental.stationaryAdvanceSpeed),
    // PreferredDistance(governor), SpeedMultiplier(보스 3페이즈 배율, 그 외 1)
}

/// 이동 판단 결과. 최종 이동은 시뮬레이션이 적용한다.
internal readonly struct EnemyMoveDecision
{
    // Mode(Stay/Face/Move), Direction, Speed(감속 전 속도)
}

internal abstract class EnemyMovement
{
    public abstract EnemyMoveDecision Decide(SimulationEnemy enemy, in EnemyMoveContext context);
}

internal static class EnemyMovements
{
    // 종류별 상태 없는 공유 인스턴스 배열
    public static EnemyMovementType Resolve(string enemyId);              // 생성 시 1회
    public static EnemyMoveDecision Decide(SimulationEnemy enemy, in EnemyMoveContext context);
}
```

이동 종류와 구현:

| 이동 종류 | 대상 | 판단 규칙 |
|---|---|---|
| `Chase` | 스카우트, 이지스 | `Move(Direction, definition.Speed)` |
| `Turret` | 센트리 | 미션: `Move(Direction, AdvanceSpeed)` / 도크: `Face(Direction)` |
| `Anchor` | 릴레이 | 미션: `Move(Direction, AdvanceSpeed)` / 도크: `Stay` |
| `DashChase` | 헌터 | `Time < DashUntil`: `Move(AttackDirection, DashSpeed)` / `WarningUntil > 0`: `Stay` / 사거리 안이고 `Time >= AttackAt`: `Stay` / 그 외: `Move(Direction, Speed)` |
| `KeepDistance` | 거버너 | 거리 > `PreferredDistance`: `Move(Direction, Speed × SpeedMultiplier)` / 그 외: `Face(Direction)` |

- `Turret`과 `Anchor`는 같은 클래스에 도크 동작 플래그만 다르게 준 두 인스턴스로 만든다.
- `SimulationEnemy`에는 `EnemyMovementType MovementType`(internal 읽기 전용, 생성자 인자) 하나만 추가한다. 이동 클래스는 적의 공격 타이머(`AttackAt`, `WarningUntil`, `DashUntil`, `AttackDirection`)를 **읽기만** 하고 바꾸지 않는다.
- 종류 ID와 이동 종류의 대응은 `EnemyMovements.Resolve` 한곳에만 둔다. `SpawnEnemy`에서 한 번 결정해 적에 저장한다.

### 3.2 `AdvanceEnemies` 처리 순서

이동을 분리해도 기존과 같은 결과가 나오도록 적 하나의 틱 처리를 다음 순서로 고정한다.

```
1. 죽었거나 더미이거나 빙결이면 건너뜀            (기존과 동일)
2. 이동 전 Offset / Direction 계산                (기존과 동일, 1회)
3. 보스면 CheckBossPatch, 패치 중이면 건너뜀       (기존과 동일)
4. decision = EnemyMovements.Decide(enemy, context)
5. ApplyMove(enemy, decision)                      Stay: 없음 / Face: 방향만 / Move: MoveEnemy(감속·벽 충돌)
6. 공격 갱신 (보스: AdvanceBoss, 그 외: AdvanceEnemyAttack. 기존 코드에서 이동 줄만 제거)
   - 접촉·예고 시작 판정은 2의 이동 전 Offset/Direction을 사용
   - 접촉 거리와 발사 위치는 이동 후 위치를 사용
```

헌터는 기존 분기(돌진 → 예고 종료 전이 → 예고 시작 → 추적)의 결과가 같아지도록, 6단계도 **이동 전 상태 기준**으로 판정한다. 기존 코드에서도 예고 시작 판정은 이동 전 `offset`을, 접촉 판정은 이동 후 위치를 쓰므로 순서만 정리하면 결과가 같다.

| 헌터 상황 | 기존 | 리팩토링 후 (4~6단계) |
|---|---|---|
| 돌진 중 | 돌진 이동 → 접촉 | `Move(돌진)` → 접촉 |
| 예고 종료 틱 | 이동 없음, 돌진 시작 | `Stay` → 돌진 시작 |
| 예고 중 | 이동 없음 | `Stay` → 없음 |
| 사거리 안·공격 가능 | 이동 없음, 예고 시작 | `Stay` → 예고 시작 |
| 그 외 | 추적 이동 | `Move(추적)` → 없음 |

### 3.3 상태 이상

새 파일 `Core/Simulation/EnemyStatus.cs`에 데이터 타입만 둔다.

```csharp
public enum EnemyStatusType { Burn, Chill, Freeze, Emp }

public sealed class EnemyStatusEffect
{
    // 읽기: EnemyId, Type, Until
    //       Stacks(냉기), NextTickTime·SourceForm·IsNoise(화염), ImmuneUntil(빙결)
    // 변경: internal setter. 시뮬레이션만 변경한다.
}
```

`RuneSimulation`에 추가하는 상태와 메서드:

| 구성 | 역할 |
|---|---|
| `List<EnemyStatusEffect> _enemyStatuses` | 소유 목록. 상태 해시 기록 순서 |
| `Dictionary<int, EnemyStatusEffect> _statusIndex` | 적 ID·종류로 조회 (키 = `적 ID × 4 + 종류`, 적 300 × 매 틱 조회 대비, 키 할당 없음) |
| `double _statusTime` | 상태 판정 기준 시각. 기존 `Observe(Time)` 관측 시각과 같은 시점에 갱신 |
| `ApplyStatus(enemy, element, form, noise)` | `ApplyDamage`의 상태 부여 줄을 옮김. 내부에서 `ApplyBurn/ApplyChill`, EMP는 직접 갱신 |
| `AdvanceStatuses()` | `_statusTime` 갱신 → **적 목록 순서로** 화염 틱 처리 → 만료 레코드 제거 |
| `RemoveEnemyStatuses(int enemyId)` | `CleanupEnemies`에서 제거된 적의 상태 이상 삭제 |
| `HasEnemyStatus(SimulationEnemy, EnemyStatusType)` | 현재 활성 여부 (public, 표시·조건 룬용) |
| `GetChillStacks(SimulationEnemy)` | 냉기 스택 (public, 감속·표시용) |

종류별 규칙은 기존 비교식을 그대로 옮긴다.

| 종류 | 부여 | 활성 | 레코드 제거 |
|---|---|---|---|
| Burn | 레코드가 없거나 `time >= Until`이면 `NextTickTime = time + interval`. 항상 `Until = time + duration`, 형태·노이즈 갱신 | `_statusTime < Until` | 남은 틱이 없고(`NextTickTime > Until + ε`) `time >= Until` |
| Chill | 스택 +1(최대 3), `Until = time + duration`. 3스택이고 빙결 면역이 아니면 Freeze 부여 후 Chill 레코드 제거 | 스택 > 0 | `time >= Until` |
| Freeze | `Until = time + freeze`, `ImmuneUntil = Until + immunity` | `_statusTime < Until` (면역은 `< ImmuneUntil`) | `time >= ImmuneUntil` |
| Emp | `Until = time + duration` (덮어씀) | `_statusTime < Until` | `time >= Until` |

- 화염 틱은 `_enemyStatuses` 순서가 아니라 **적 목록 순서**로 처리한다. 화염 피해로 릴레이가 먼저 죽는지, 적응 학습 누적 순서가 어떤지가 기존과 같아야 하기 때문이다.
- `SimulationEnemy`에서 상태 이상 필드와 `Burn/Chill/Emp`, `IsBurning`, `ChillStacks`, `IsFrozen`, `IsFreezeImmune`, `IsEmp`, `HasStatus`, `BurnNext/BurnUntil/BurnForm/BurnNoise`를 제거한다. `Observe`는 예고·패치·피격 플래시 판정용으로 남기되 냉기 스택 초기화는 뺀다.
- 경고(`WarningUntil`), 보스 패치(`PatchUntil`), 피격 플래시는 상태 이상이 아니라 공격 예고·보스 페이즈·표시 상태이므로 적 객체에 남긴다.

### 3.4 호출부 교체

| 위치 | 기존 | 변경 |
|---|---|---|
| `Evaluate` | `Target.HasStatus(status)` | 문자열(`burn/chill/freeze/emp`)을 `EnemyStatusType`으로 바꿔 `HasEnemyStatus` |
| `ApplyDamage` | `enemy.IsEmp`, 상태 부여 줄 | `HasEnemyStatus(enemy, Emp)`, `ApplyStatus(...)` |
| `AdvanceEnemies` | `enemy.IsFrozen` | `HasEnemyStatus(enemy, Freeze)` |
| `MoveEnemy` | `enemy.ChillStacks` | `GetChillStacks(enemy)` |
| `RunEnemyShots` | `shot.Owner.IsFrozen` | `HasEnemyStatus(shot.Owner, Freeze)` |
| `CleanupEnemies`, `ResetBench` | — | `RemoveEnemyStatuses`, 목록·인덱스 초기화 |
| `StateHash` | 적 `WriteState`에 상태 필드 포함 | 적 `WriteState`에서 제외, `_enemyStatuses`를 순서대로 기록 |
| `RuneArenaGraphic.DrawEnemy` | `enemy.IsBurning/ChillStacks/IsFrozen/IsEmp` | `sim.HasEnemyStatus(...)`, `sim.GetChillStacks(...)` |

---

## 4. 실행 계획

각 단계는 독립적으로 컴파일·검증한 뒤 다음 단계로 넘어간다.

### 0단계. 기준값 확보 (코드 변경 없음)

1. `Rune Code > Build Windows PoC`로 빌드한다.
2. `tools/sim.ps1`로 아래 조합의 결과 JSON을 저장한다.
   - 시험 도크: `dummy_single`, `dummy_line`, `dummy_swarm`, `aegis`, `adapt_loop` × `firebolt`, `triplefire`, `shockwave`, `magicmissile`, `barrier` (`-Ticks 1800`)
   - 시간제 전투: `-Scenario incremental -Stage 1/2/3/5/10 -Ticks 3600`
   - 냉기·전격 확인용: 얼음·전격 속성 마법을 공유 코드 파일로 저장해 `-Spell <파일 경로>`로 위 조합 일부를 실행
3. 같은 명령을 두 번 실행해 `_stateHash`가 같은지(결정성) 확인한다.

### 1단계. 상태 이상 데이터화

1. `EnemyStatus.cs` 추가 (`EnemyStatusType`, `EnemyStatusEffect`).
2. `RuneSimulation`에 목록·인덱스·`_statusTime`과 부여·진행·제거·조회 메서드 추가.
3. 3.4절의 호출부를 교체한다.
4. `SimulationEnemy`에서 상태 이상 필드와 메서드를 제거하고 `WriteState`를 정리한다.
5. 0단계 조합을 다시 실행해 비교한다(5절).

### 2단계. 이동 판단 분리

1. `EnemyMovement.cs` 추가 (enum, context·decision struct, 추상 클래스, 구현 4종, `EnemyMovements`).
2. `SimulationEnemy`에 `MovementType` 추가, `SpawnEnemy`에서 `EnemyMovements.Resolve`로 설정.
3. `AdvanceEnemies`/`AdvanceBoss`를 3.2절 순서로 바꾸고 이동 코드를 `ApplyMove` 하나로 모은다.
4. 0단계 조합을 다시 실행해 비교한다.

### 3단계. 문서 정리

1. `ENEMY_SPAWN_DAMAGE.md`의 2.2절(행동 패턴), 5.4절(상태 이상), 7절(코드 위치)을 새 구조에 맞게 고친다.
2. 이 문서의 상태를 "완료"로 바꾸고 남은 문제를 7절에 기록한다.

---

## 5. 검증 기준

| 항목 | 기준 |
|---|---|
| 컴파일 | Unity 콘솔 오류·경고 없음 |
| 결과 동일성 | 0단계 결과와 `_totalDamage`, `_dps`, `_kills`, `_spawnedEnemies`, `_earnedRam`, `_settledRam`, `_result`, `_remainingTime`, `_nodeExecutionCount`, `_peakSpellEntities`, `_droppedExecutions`가 모두 같음 |
| 결정성 | 변경 후 같은 명령 2회 실행 시 `_stateHash` 동일 |
| 상태 해시 | 기록 형식이 바뀌므로 0단계와 `_stateHash` 값이 다른 것은 정상 |
| 플레이 확인 | 미션에서 헌터 예고·돌진, 센트리 3연사, 이지스 정면 방패(EMP 시 해제), 화염·냉기·빙결·EMP 아이콘, 빙결 중 정지·연사 취소, 디버그 보스 스폰 후 페이즈 전환·패치 |

`_elapsedMilliseconds`는 실행 환경에 따라 달라지므로 비교하지 않는다. 결과가 다르면 다음 단계로 넘어가지 않고 원인을 먼저 찾는다.

---

## 6. 위험 요소와 결정 필요 사항

### 6.1 위험 요소

| 위험 | 대응 |
|---|---|
| 화염 마지막 틱과 만료 시점의 부동소수 경계 | 기존 비교식(`time >= until`, `+0.000001`)을 그대로 옮기고, 마지막 틱 처리 후에만 레코드를 제거 |
| 관측 시각 차이 | 기존 `Observe`는 적마다 관측 시각을 가졌고 `RunScheduled`·공격 입력 단계에서는 이전 틱 시각으로 판정된다. `_statusTime`을 같은 시점(`AdvanceStatuses` 시작)에 갱신해 같은 판정을 유지 |
| 화염 처리 순서 | 적 목록 순서로 처리 (3.3절) |
| 헌터 정지 조건 중복 | 이동 판단과 공격 갱신이 같은 "사거리 안·공격 가능" 조건을 각각 읽는다. 두 곳 모두 이동 전 Offset을 쓰도록 맞춘다 |
| 조회 비용 | 인덱스 딕셔너리로 조회. 매 틱 새 할당이 생기지 않도록 레코드는 부여 시에만 생성 |

### 6.2 결정 필요 사항

1. **죽은 대상의 상태 조건**: 지연 실행(`delay`, `repeat`)이 이미 죽은 적을 대상으로 `targetHasStatus`를 평가하면, 기존에는 적 객체에 남은 사망 시점의 상태가 그대로 보인다. 리팩토링 후에는 사망 정리 때 레코드가 삭제되어 `false`가 된다.
   - 권장: `false`로 처리한다. 죽은 대상에게 상태가 남아 있다고 보는 것은 의도된 규칙이 아니라 기존 구조의 부산물로 보인다. 다만 이 경우만 기존과 결과가 달라질 수 있으므로 확인이 필요하다.
   - 기존 동작을 유지하려면 대상이 죽을 때 그 적의 상태를 스냅샷으로 남겨야 해 구조가 복잡해진다.
2. **이동 종류의 위치**: 이번 단계는 `EnemyMovements.Resolve`의 코드 대응표로 둔다(`enemies.json` 스키마 변경 없음). 데이터로 옮기는 것은 이후 단계로 미룬다.

---

## 7. 리팩토링의 의의와 한계

### 7.1 의의

- **이동 확장 비용 감소**: 새 이동 방식은 `EnemyMovement` 구현 하나와 대응표 한 줄로 추가된다. `AdvanceEnemies`의 문자열 분기를 고치지 않아도 된다.
- **판단과 적용의 경계 명확화**: 이동 클래스는 값만 반환하고 상태를 바꾸지 않는다. 벽 충돌·감속·위치 갱신은 시뮬레이션 한곳에서만 일어나므로 이동 버그의 위치를 좁히기 쉽다.
- **적 객체 경량화**: 적은 이동 객체나 상태 이상 필드를 갖지 않는다. 적 300기 기준으로도 이동 클래스 인스턴스는 종류 수만큼만 존재한다.
- **상태 이상 수명의 단일화**: 부여·틱·만료·제거가 시뮬레이션 한곳에 모인다. 새 상태 이상을 추가할 때 `SimulationEnemy`에 필드를 늘리지 않고 종류와 처리 규칙만 추가하면 된다.
- **상태 이상의 가시성**: 상태 이상이 목록 데이터가 되어 디버그 표시, 상태 해시, 이후의 저장·재현에 그대로 쓸 수 있다.
- **다음 단계 준비**: 적 쪽 구조가 정리되어, 플레이어 공격 타입을 구조화할 때 바꿔야 할 접점이 `ApplyDamage`의 피해 보정과 `ApplyStatus` 입력으로 좁혀진다.

### 7.2 한계

- **이동만 다형화된다**: 접촉 주기, 센트리 연사, 헌터 예고·돌진 전이, 보스 패턴 같은 공격 로직은 여전히 `RuneSimulation`의 종류별 분기에 남는다. 헌터처럼 이동과 공격 상태가 얽힌 적은 "사거리 안·공격 가능" 조건이 이동 판단과 공격 갱신에 중복된다.
- **적 종류 분기가 완전히 사라지지 않는다**: `ApplyDamage`의 이지스 방패(`form == "bolt" && !isDot`)와 릴레이 오라는 적 특성이지만 플레이어 공격 형태에 의존하므로 문자열 비교로 남는다. 이를 정리하려면 플레이어 공격 타입 구조화가 먼저 필요하다.
- **상태 이상 부여 규칙은 그대로다**: 어떤 공격이 어떤 상태 이상을 거는지는 여전히 `element` 문자열(`fire/ice/arc`)로 결정된다. 데이터화된 것은 상태 이상의 수명 관리이지 부여 조건이 아니다.
- **빙결은 특수 처리로 남는다**: 빙결의 "모든 행동 정지"는 일반화된 행동 제한 규칙이 아니라 `AdvanceEnemies`·`RunEnemyShots`의 조회로 처리된다.
- **이동 종류가 코드에 있다**: 종류 ID와 이동 종류의 대응을 기획 데이터에서 바꿀 수 없다.
- **상태 해시가 바뀐다**: 기록 형식이 달라져 리팩토링 전에 저장한 `_stateHash` 값과는 비교할 수 없다. 동일성은 결과 수치로 확인한다.
- **경계 동작 차이 가능성**: 죽은 대상의 상태 조건(6.2절)은 결정에 따라 기존과 결과가 달라질 수 있다.

---

## 8. 진행 기록

### 1단계. 상태 이상 데이터화 — 완료

변경 파일:

| 파일 | 내용 |
|---|---|
| `Core/Simulation/EnemyStatus.cs` (신규) | `EnemyStatusType`, `EnemyStatusEffect` |
| `Core/Simulation/SimulationTypes.cs` | `SimulationEnemy`에서 상태 이상 필드·프로퍼티·메서드 제거, `Observe`의 냉기 초기화 제거, `WriteState`에서 상태 이상 제외 |
| `Core/Simulation/RuneSimulation.cs` | 상태 이상 목록·인덱스·판정 시각, 부여(`ApplyStatus/ApplyBurn/ApplyChill`), 진행·만료(`AdvanceStatuses/IsStatusExpired`), 조회(`HasEnemyStatus/GetChillStacks`), 사망·초기화 정리, 상태 해시 기록, 호출부 교체 |
| `Presentation/RuneArenaGraphic.cs` | 이지스 방패·상태 아이콘 표시를 `sim.HasEnemyStatus/GetChillStacks`로 교체 |

계획 대비 조정:

- 조회 인덱스 키는 튜플 대신 `적 ID × 4 + 종류` 정수를 쓴다(키 할당·박싱 없음).
- 사용처가 없는 `StartTime`과 공개 `EnemyStatuses` 목록은 추가하지 않았다.
- 6.2절 1번(죽은 대상의 상태 조건)은 권장안대로 `false`로 처리했다. 사망 정리 시 해당 적의 상태 이상 레코드를 삭제한다.

검증 결과:

- 컴파일: 프로젝트 csproj 복사본으로 `dotnet msbuild` 오류·경고 없음. 임시 복사 프로젝트에서 `RuneCodeBuild.BuildWindows` batchmode 빌드 성공(오류 0, 경고 1은 URP 파이프라인 설정 경고로 무관).
- 결과 동일성: 마법 17종(기존 5종 + 화염·얼음·전격 × 발사·폭발·궤도·잔류) × 시나리오 10종(시험 도크 5종 1800틱, 시간제 전투 스테이지 1/2/3/5/10, 60초) = 170건에서 `_stateHash`·`_elapsedMilliseconds` 외 모든 필드가 리팩토링 전과 동일.
- 결정성: 리팩토링 전 2회, 리팩토링 후 2회 실행 각각 170건 `_stateHash`까지 동일. 리팩토링 전후 해시 값은 기록 형식 변경으로 달라짐(정상).

미검증·남은 차이:

- 에디터 플레이로 상태 아이콘(화염·냉기·빙결·EMP)과 이지스 방패 표시를 직접 확인하지 않았다.
- 보스(거버너)와 조건 룬 `targetHasStatus`는 위 시뮬레이션 조합에 포함되지 않았다.
- 보스 페이즈 전환이 공격 입력·예약 실행 단계에서 일어나면, 기존에는 보스의 관측 시각이 그 자리에서 갱신되어 같은 단계의 이후 상태 판정에 반영됐다. 지금은 판정 시각이 다음 `AdvanceStatuses`에서 갱신된다. 같은 틱에 보스의 상태 이상 만료가 겹칠 때만 1틱 차이가 생길 수 있다.
- 신규 `EnemyStatus.cs`의 `.meta`는 Unity 에디터가 프로젝트를 다시 읽을 때 생성된다.

### 2단계. 이동 판단 분리 — 완료

변경 파일:

| 파일 | 내용 |
|---|---|
| `Core/Simulation/EnemyMovement.cs` (신규) | `EnemyMovementType`, `EnemyMoveMode`, `EnemyMoveContext`, `EnemyMoveDecision`, `EnemyMovement`와 구현 4종(`ChaseMovement`, `StationaryAdvanceMovement`, `DashChaseMovement`, `KeepDistanceMovement`), 중앙 static `EnemyMovements` |
| `Core/Simulation/SimulationTypes.cs` | `SimulationEnemy`에 `MovementType` 추가(생성자 인자) |
| `Core/Simulation/RuneSimulation.cs` | `SpawnEnemy`에서 `EnemyMovements.Resolve`로 이동 종류 설정. `AdvanceEnemies`를 3.2절 순서로 재구성하고 이동 적용을 `ApplyMove` 하나로 통합. 보스 외 공격 갱신을 `AdvanceEnemyAttack`으로 분리. `AdvanceBoss`에서 페이즈 확인·이동 제거 |

계획 대비 조정:

- 이동 관련 타입은 시뮬레이션 내부에서만 쓰이므로 모두 `internal`로 두었다.
- `MoveEnemy` 호출은 이제 `ApplyMove`에서만 일어난다.

검증 결과:

- 컴파일: `dotnet msbuild` 오류·경고 없음, 임시 복사 프로젝트 batchmode 플레이어 빌드 성공(오류 0).
- 결과 동일성: 1단계와 같은 170건에서 **`_stateHash`까지 1단계 결과와 동일**. 상태 해시에는 모든 적의 위치·방향·공격 타이머가 포함되므로 이동·공격 처리 결과가 틱 단위로 같음을 뜻한다. 리팩토링 전 기준값과도 해시 외 모든 필드가 동일.

미검증:

- 거버너는 시뮬레이션 조합에 없어 `KeepDistanceMovement`와 페이즈 전환 후 이동은 코드 검토로만 확인했다. 에디터에서 디버그 스폰으로 확인이 필요하다.
- 신규 `EnemyMovement.cs`의 `.meta`는 Unity 에디터가 프로젝트를 다시 읽을 때 생성된다.

### 3단계. 문서 정리 — 완료

- `ENEMY_SPAWN_DAMAGE.md`: 2.2절에 이동 판단·적용·공격 갱신 순서와 이동 종류 표, 5.4절에 상태 이상 데이터 구조·판정 시각·제거 시점, 4장 틱 순서와 7장 코드 위치를 새 구조로 갱신. 바뀐 코드 줄 번호 갱신.
- `docs/PROGRESS.md`에 1~3단계 진행 기록 추가.
