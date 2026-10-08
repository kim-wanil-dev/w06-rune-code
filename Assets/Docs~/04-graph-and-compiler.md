# 04. 마법 그래프 · 실행 그래프 · 토큰 런타임

> 2026-10-08 리팩토링으로 옛 "컴파일된 실행 트리" 모델(`GraphCompiler`, `CompiledSpell`, E1~E10/W1~W4, RC1 공유 코드)을 제거하고 토큰 모델로 바꿨다. 규칙의 기준은 `Refactoring/마법 그래프 구현 규칙.md`, 결정과 구현 해석은 `Refactoring/마법 그래프 리팩토링 계획.md`(D1~D9, I1~I11)에 있다. 파일 이름은 링크 보존을 위해 그대로 둔다.

마법은 세 단계로 표현된다.

```
 편집 (UI)                  읽기 전용 색인                       실행 (Core/Simulation)
 ──────────                 ─────────────                        ──────────────────────
 SpellGraph  ──▶ SpellProgram.Build ──▶ SpellProgram ──▶ RuneSimulation.SetProgram ──▶ SpellRuntime
  Nodes[] (종류 ID, 파라미터, 좌표)      Errors G1~G6 / Warnings W1          토큰 · 합류 대기 · 발사 요청 · 투사체
  Edges[] (포트 "0"/"1")                 Next[노드, 출력 포트] O(1)
                                         도달성 · 파라미터 · Signature
 ShareCodec: SpellGraph ⇄ JSON (복제·Undo 비교·저장 검증 공통 경로)
```

그래프는 하나이고 읽기 전용이다. 시전할 때마다 늘어나는 것은 **토큰**이며, 토큰마다 그래프를 복제하지 않는다.

## 4.1 SpellGraph — 편집 모델 (`Core/Graph/SpellGraph.cs`)

직렬화 스키마는 옛 모델과 같다(`_id`, `_name`, `_version`, `_nodes`, `_edges`, `_runeId`, `_x`, `_y`, `_params`, `_fromNode`, `_fromPort`, `_toNode`, `_toPort`). 값의 의미만 바뀌었다.

| 필드 | 의미 |
|---|---|
| `GraphNode.RuneId` | 노드 종류 ID: `cast`, `projectile`, `amplify`, `add`, `split`, `fork`, `join`, `branch`, `onHit` |
| `GraphNode.Params` | 시전 `aim`(`cursor`/`nearest`)·`load`, 증폭 `ratio`, 분배 `share`(퍼센트), 분기 `condition`·`compare`(`ge`/`le`)·`threshold` |
| `GraphNode.X/Y` | 편집기 좌표. 규칙에 쓰지 않는다 |
| `GraphEdge.FromPort/ToPort` | 포트 번호 문자열 `"0"`, `"1"` |

* `SpellGraph.Create(id, name)`은 기본 그래프 `core(cast) → projectile`을 만든다.
* `RemoveNode`는 시전 노드(`SpellNodes.CAST_ID`)를 지우지 않고, 지운 노드에 붙은 엣지를 함께 지운다.
* 연결 의미 규칙(출력 포트당 엣지 1개, 시전·적중 입력 금지)은 편집 연산(`RuneCodeApp.Connect`)과 `SpellProgram` 검증이 맡는다.

## 4.2 SpellNodes — 노드 종류 표 (`Core/Graph/SpellNodes.cs`)

규칙 5.1의 구조 값(포트 수)은 코드 상수, 숫자 값(비용 계수·머무는 시간·파라미터 범위)은 `balance.json`의 `_spell` 섹션(`SpellSettings`)에서 읽는다.

| 종류 | 입력/출력 | 비용 (`GetCost`, 도착 순간 토큰 값) | 머무는 시간 |
|---|---|---|---|
| Cast | 0 / 1 | 0 (마나풀에서 적재량을 뺀다) | DefaultDwell 0.05 |
| Projectile | 1 / 1 | max(바닥값, 2 × 개수) | 0.25 |
| Amplify | 1 / 1 | max(바닥값, 위력 × (배율 − 1) × 0.4) | 0.05 |
| Add | 1 / 1 | max(바닥값, 3) | 0.05 |
| Split | 1 / 1 | max(바닥값, 위력 × 0.15) | 0.05 |
| Fork | 1 / 2 | max(바닥값, 2) | 0.05 |
| Join | 2 / 1 | max(바닥값, 2) | 0.05 (합쳐진 뒤) |
| Branch | 1 / 2 | max(바닥값, 1) | 0.05 |
| OnHit | 0 / 1 | 0 | 0 |

`GetCostFormula`는 편집기에 보여 줄 공식 문자열(예: `위력×(2.0−1)×0.4 (≥1.0)`)을 만든다.

## 4.3 SpellProgram — 읽기 전용 실행 그래프 (`Core/Graph/SpellProgram.cs`)

`SpellProgram.Build(graph, settings)`는 항상 객체를 반환한다. 노드는 그래프 순서대로 0..N−1 색인이 되고, `TryGetNext(node, outPort, out target, out inPort)`가 배열 하나로 다음 노드를 찾는다.

| 코드 | 의미 | 구분 |
|---|---|---|
| G1 | 시전 노드가 정확히 1개가 아님 | 오류 |
| G2 | 적중 노드가 2개 이상 | 오류 |
| G3 | 시전·적중 노드로 들어오는 엣지 | 오류 |
| G4 | 같은 출력 포트에 엣지 2개 이상 | 오류 |
| G5 | 알 수 없는 종류, 끊긴 엣지, 잘못된 포트, 중복 노드 ID | 오류 |
| G6 | 파라미터 키·범위·옵션 오류 | 오류 |
| W1 | 시전에서 도달할 수 있는 투사체 노드가 없음 | 경고 |

* 오류가 있으면 `IsValid`가 false이고 시전·출격을 막는다. 편집 연산이 오류를 만들지 않으므로 오류는 손상된 저장에서만 나와야 한다.
* 도달 가능 = 시전 또는 적중 노드에서 출력 엣지를 따라 갈 수 있는 노드. 도달 불가 노드는 편집기에서 회색이다. 역방향·자기 연결(루프)은 허용한다.
* `Signature`는 노드 ID·종류·파라미터·엣지의 정렬 문자열을 FNV-1a 64비트로 접은 값이다(좌표·이름·배열 순서 제외). 텔레메트리, CLI 리포트, 상태 해시가 쓴다.

## 4.4 SpellRuntime — 토큰 실행 (`Core/Simulation/SpellRuntime.cs`, `SpellRuntimeTypes.cs`)

### 틱 안의 위치 (`RuneSimulation.Step`, 규칙 4.2)

```
Player.Advance            마나풀 회복
TryCast()                 시전 입력이 눌린 틱에만. 새 토큰은 같은 틱에 처리된다 (I1)
AdvanceEnemies, RunEnemyShots
AdvanceProjectiles        이동·충돌. 적중 토큰은 목록에 넣기만 하고 다음 틱부터 처리 (I2)
ProcessTokens             생성 순서대로. 분배 복제는 원본 직후 같은 틱에 [진행 루프]부터 (I3)
FlushFireRequests         이번 틱 발사 요청을 묶어 부채꼴로 투사체 생성. 다음 틱부터 이동
```

### 시전 (`TryCast`)

마나풀 < 적재량이면 `NoMana`, 활성 토큰이 64개면 `Overload`이며 둘 다 차감하지 않는다. 성공하면 적재량만큼 마나풀을 빼고 토큰(마나 = 적재량, 위력 10, 개수 1, 원점 = 캐릭터, 남은 대기 = 시전 노드 머무는 시간)을 만든다.

### 토큰 처리 (규칙 4.3)

경과 시간이 수명(3초)을 넘으면 `Expired`로 소멸한다. 합류 대기 중이면 대기 시간을 늘리고 1초가 지나면 혼자 출력 0으로 나간다. 그 밖에는 남은 대기 시간을 줄이고 0 이하(허용 오차 1e-9)인 동안 최대 32개 노드까지 진행한다. 남은 시간은 다음 노드로 넘긴다.

### 도착 처리 (규칙 4.4, 5.2)

비용을 계산해 `마나 < 비용`이면 `Unpayable`로 소멸한다(같으면 지불). 지불하면 통과 횟수를 올리고 노드 효과를 적용한다.

* 투사체: 발사 요청을 만든다. 출력 엣지가 없으면 **종착 발현** — 남은 마나를 개수로 나눠 각 발이 운반하고 토큰은 끝난다.
* 증폭 `위력 ×= 배율`, 가산 `위력 += 10`, 분열 `개수 += 1, 위력 ×= 0.7`.
* 분배: 남은 마나를 비율로 나눠 원본은 출력 0, 복제는 출력 1. 토큰 상한이면 뒤쪽 몫은 사라진다.
* 합류: 반대쪽 포트 대기열(FIFO) 맨 앞 토큰이 도착 토큰을 흡수(마나·위력·개수 합산). 없으면 대기를 시작한다.
* 분기: 대상 거리·대상 체력 %·마나·위력·세대·통과 횟수를 기준값과 비교해 참이면 출력 0, 거짓이면 출력 1.

### 발사 묶음과 투사체 (규칙 6)

* 같은 틱의 요청 중 위치(0.1px)·방향(1°)이 같고 같은 토큰이 아닌 요청을 한 묶음으로 모아 M발을 10° 간격 부채꼴로 쏜다. 퍼짐이 90°를 넘으면 90°/M 간격이다.
* 캐릭터 원점은 발사 순간 플레이어 위치와 시전 노드 조준 모드(커서 / 가장 가까운 적), 지점 원점은 적중 위치와 맞은 투사체 방향을 쓴다.
* 투사체는 직선 등속(12타일/s), 사거리 15타일, 반경 6px(D3, D8). 벽·사거리에서 소멸하면 운반 마나는 사라진다. 적은 이동 선분에서 가장 먼저 닿은 하나만 맞고 관통하지 않는다.
* 적중: `RuneSimulation.OnProjectileHit`이 `max(0, 위력 − 경감)` 피해를 준다(보스 패치 중 무적, D2). 운반 마나가 있고 적중 노드가 있으면 적중 토큰(개수 1, 세대 +1, 맞은 적 무시)을 만든다.

### 바꾸면 안 되는 규칙 (규칙 7.3)

비용 바닥값 1, 종착 발현에서만 마나가 투사체로 이동, 적중 노드 최대 1개, 비관통, 적중 토큰 개수 1, 적중 토큰은 맞은 적 무시, 분배 복제 같은 틱 처리, 편집 진입 시 실행 상태 삭제. 앱은 그래프가 바뀌는 모든 편집(Undo/Redo 포함)에서 도크의 `ClearSpellState()`를 부른다(D5).

## 4.5 표시용 읽기 모델

| 공개 값 | 쓰는 곳 |
|---|---|
| `Tokens`, `Projectiles` | 흐름 레이어(`UI/SpellFlowLayer`), 경기장(`RuneArenaGraphic`) |
| `GetLastPaidCost/Tick(node)` | 노드 아래 `−3.0` 표시 |
| `EndEvents` | 지불 불가(빨강, 필요/보유)·수명 초과(회색) 약 1초 표시 |
| `LastCastResult/Tick`, `LastOverloadTick` | HUD·도크의 '마나 부족'·'과부하' |

편집 화면(`RuneGraphCanvas`)과 전투 오버레이(`RuneGraphOverlay`, G 키)는 같은 `SpellFlowLayer`를 자식으로 붙이고, 노드 배치와 엣지 곡선은 `GraphLayout`의 같은 함수를 쓴다.

## 4.6 ShareCodec

`Serialize`/`Deserialize`(크기·ID·숫자 검증)만 남았다. RC1 공유 코드(`Encode`, `TryDecode`)는 규칙 13절(그래프 공유 범위 밖)에 따라 제거했다. `SpellGraph.Clone`, `PlayerSave.Validate`, `RuneCodeApp.ChangedGraph`의 변경 비교가 이 경로를 쓴다.
