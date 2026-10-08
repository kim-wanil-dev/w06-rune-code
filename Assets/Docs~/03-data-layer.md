# 03. 데이터 계층

게임 수치는 전부 `Assets/RuneCode/Resources/RuneCode/` 아래 JSON에 있고, C# 쪽은 **직렬화 스키마 + 검증기 + 읽기 전용 접근자**만 제공한다. ScriptableObject는 쓰지 않는다. JSON은 `JsonUtility`로 읽으므로 필드명이 C# private 필드명(`_maxHp` 등)과 1:1로 대응한다.

## 3.1 자산 목록

| 파일 | 역할 | 읽는 코드 | 적재 시점 |
|---|---|---|---|
| `strings.ko.json` | 한국어 문자열 사전 | `GameData.Load` | 전역 1회 |
| `balance.json` | 플레이어·제한·전투·경제·시뮬레이션 밸런스와 마법 그래프 설정(`_spell`) | `BalanceData.FromJson` | 전역 1회 |
| `enemies.json` | 적 6종(체력·경감 포함) | `EnemyCatalog.FromJson` | **시뮬레이션 인스턴스마다** |
| `sector1.json` | 타일 크기·플레이어 반경·방 5개(타일/웨이브) | `SectorDefinition.FromJson` | 시뮬레이션 인스턴스마다 |
| `governor.json` | 보스 페이즈·패턴 주기 | `GovernorDefinition.FromJson` | 시뮬레이션 인스턴스마다 |
| `incremental.json` | 시간제 전투(제한시간·유입·스테이지 배율·적 편성·타일) | `IncrementalDefinition.FromJson` | 시뮬레이션 인스턴스마다 |
| `UIFont.asset` | TMP 글꼴(한국어 아틀라스) | `RuneCodeApp._font` / `RuneCodeBuild` | 장면 참조 |

## 3.2 GameData — 전역 레지스트리

`Assets/RuneCode/Core/Data/GameData.cs`. static 클래스이며 상태는 2개다.

```csharp
static BalanceData               _balance;  // GameData.Balance (마법 그래프 설정은 GameData.Balance.Spell)
static Dictionary<string,string> _strings;  // GameData.L(key)
```

* `Load()`는 `IsLoaded` 가드로 멱등하다. `RuneCodeApp.Awake`, `RuneSimulation` 생성자, `SimulationCli.Run`, `RuneCodeBuild.PrepareScene`이 각자 호출하지만 실제 I/O는 한 번만 일어난다.
* `ReadResource(path)`는 `Resources.Load<TextAsset>("RuneCode/" + path)`이고 자산이 없으면 `FormatException`이다. 선택적 자산 개념이 없다.
* `L(key)`는 누락 키를 그대로 반환한다(예외 아님). 노드 이름(`node.<id>`), 검증 문구(`graph.<코드>`), 비용 공식 단어(`formula.*`)가 이 사전을 쓴다.
* 모든 검증이 끝난 뒤에 필드를 대입하므로 **검증 실패 시 적재 완료로 보이지 않는다.** 2026-10-08 리팩토링으로 룬 카탈로그(`runes.json`, `RuneData.cs`)와 시작 마법 템플릿(`spells/*.json`)은 제거되었다.
* 전역 상태를 비우는 API가 없다. 에디터에서 JSON을 수정하면 도메인 리로드 또는 재생 재시작이 필요하다.

## 3.3 SpellSettings — 마법 그래프 설정

`balance.json`의 `_spell` 섹션(`BalanceData.Spell`). 규칙 11절의 값을 모두 모은 읽기 전용 설정이다. 노드 종류·포트 수는 `Core/Graph/SpellNodes.cs`의 코드 상수이고, 숫자는 전부 여기서 읽는다.

| 분류 | 값 |
|---|---|
| 토큰 | 기본 위력 10, 수명 3초, 틱당 처리 노드 32 |
| 마나 | 마나풀 100 / 초당 8 (EN 성장 단계만큼 +10 / +1, D1), 적재량 10~100 기본 40 단계 5 |
| 비용 | 바닥값 1, 투사체 2/발, 증폭 계수 0.4, 가산 3(+10), 분열 0.15(×0.7), 분배 2, 합류 2, 분기 1 |
| 파라미터 | 증폭 1.1~3.0 기본 2.0, 분배 10~90% 기본 50%, 분기 기준값 0~10000 기본 100 |
| 머무는 시간 | 투사체 0.25, 그 밖 0.05, 적중 0, 최소 0.01 |
| 합류 | 최대 대기 1.0초 |
| 투사체 | 속도 12타일/s, 사거리 15타일(D3), 반경 6px(D8), 부채꼴 10° / 최대 90°, 묶음 판정 0.1px / 1° |
| 상한 | 활성 토큰 64, 활성 투사체 200 |
| 표시 | 소멸 표시 1초 |

`FromJson`의 `ValidateSpell`은 모든 값이 유한한 0 이상인지, 바닥값이 1 이상인지, 범위 최소·기본·최대 순서와 머무는 시간 최소값을 지키는지 검사한다.

## 3.4 BalanceData — 밸런스 묶음

`Core/Data/BalanceData.cs`. 6개 섹션을 가진 컨테이너다(리팩토링으로 `Adaptation`, `Ram` 섹션과 옛 실행 상한 다섯 개를 제거했다).

| 섹션 | 대표 값(현재 JSON) | 쓰는 곳 |
|---|---|---|
| `Player` | HP 100, 이동 220, (EN 100·회복 15/s는 더 이상 쓰지 않고 마나풀은 `Spell`), 대시 140px·0.18s·쿨 1s, 피격 무적 0.5s | `RuneSimulation`, `RuneCodeApp`의 영구 능력치 계산 |
| `Limits` | 프레임당 최대 5스텝, 노드 256 / 엣지 1024(저장 검증용 기술 상한, D9), 적 300 | 앱 누산기, 편집 상한, 적 유입 |
| `Combat` | 화염 3dps · 0.5s 간격 · 3s, 냉기 30% 감속 · 2.5s · 3중첩 시 빙결 1s + 면역 4s, EMP 2s, 이지스 120° · 70% 감소, 릴레이 반경 200 · 20% 감소 | 냉기 감속 계산과 릴레이 반경 표시만 남음(피해 경로에서는 D2로 제거) |
| `Economy` | (용량 값은 더 이상 쓰지 않음) 능력치 단계 +10, 사망 보존 0.7, 오브 흡수 반경 160, 성장 기본가 12/15/18 · 배율 1.22 · 상한 100/100/18단계, 전투시간 +5초 | 강화 가격, 정산, 저장 검증 |
| `Sim` | 틱 60, 다중 배치 오프셋 40, 도크 좌표·더미 HP 100·일렬 5기·무리 5×2, 버스트 표시 시간, 피격 플래시, 피해 숫자 수명, 지팡이 오프셋 | 시뮬레이션 상수, 도크 시나리오 배치 |

`FromJson`은 3단으로 검증한다.

1. 섹션 존재와 핵심 불변식 — `MaxHp > 0`, `MaxEnergy > 0`, **`Sim.TickRate`가 60이 아니면 거부**.
2. 실수값이 모두 유한하고 0 이상. 이어서 `ValidateSpell`(3.3).
3. 범위·관계 — 비율 값이 1을 넘으면 거부(`ElementCap`, `AegisReduction`, `DeathRetention` 등), 상한 정수가 0 이하면 거부, 성장 배율은 1~2 사이, `GetGrowthCost(kind, 0)`을 실제로 호출해 양수인지 확인, 비용 배열 원소가 모두 양수인지.

`GetGrowthCost(kind, level)`은 `ceil(기본가 × 1.22^level)`을 `int.MaxValue`로 포화시켜 반환하고 단계 상한에 도달하면 `-1`이다. UI는 이 `-1`을 "최대"로 표시한다.

`PlayerBalance`에는 `Hp`, `Speed`, `Energy`, `DashSeconds`, `HurtInvulnerability` 같은 **별칭 프로퍼티**가 있다. 같은 백킹 필드를 두 이름으로 노출해 과거 호출부를 유지하는 장치다.

## 3.5 MissionData — 적·맵·보스·시간제

`Core/Mission/MissionData.cs` 한 파일에 7개 타입이 들어 있다.

### EnemyDefinition / EnemyCatalog

적 6종 고정이다(`FromJson`이 개수가 6이 아니면 거부). 마법 피해는 `max(0, 위력 − 경감)`이다(규칙 8절, D6 값). 도크 더미는 scout 정의에 더미 플래그를 붙인 것이며 경감 0·무한 체력이다. 현재 값:

| ID | HP | 경감 | 속도 | 반경 | 피해 | 공격 간격 | 보상 | 행동 |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| `enemy.scout` | 60 | 12 | 140 | 13 | 8 | 1.0 | 1 | 접근 후 접촉 피해 |
| `enemy.sentry` | 60 | 12 | 0 | 17 | 8 | 2.0 | 3 | 제자리 예고 후 3연사 |
| `enemy.hunter` | 60 | 12 | 110 | 17 | 15 | 1.4 | 3 | 사거리에서 예고 후 돌진 |
| `enemy.aegis` | 200 | 40 | 70 | 20 | 12 | 1.0 | 4 | 접근 후 접촉 피해 (전면 감소는 D2로 제거) |
| `enemy.relay` | 60 | 12 | 0 | 22 | 0 | 0 | 8 | 전진만 함 (오라 감소는 D2로 제거) |
| `boss.governor` | 800 | 40 | 60 | 33 | 10 | 3.0 | 60 | 3페이즈 방사·조준·장판·증원, 페이즈 전환 시 패치 무적 |

### SectorDefinition

`tileSize 32`, `playerRadius 12`, 방 5개(`R1`~`R4`, `BOSS`). `FromJson`은 모든 방을 `MissionMap`으로 파싱해 타일 규격을 확인하고, 웨이브의 적 ID와 스폰 포인트 문자가 실제로 존재하는지 검사한다.

현재 시간제 전투에서 **실제로 쓰이는 값은 `TileSize`, `PlayerRadius`, `Rooms[0].Tiles`뿐이다.** `Rooms[0].Tiles`는 시험 도크 맵으로 쓰이고, 나머지 방과 웨이브 정의는 과거 방 진행형 설계의 잔존 데이터로 검증만 통과한다. 마찬가지로 `MissionSpawn` / `MissionWave` / `MissionRoom`과 `MissionStage.Terminal`은 코드에 남아 있지만 현재 흐름에서는 진입하지 않는다.

### GovernorDefinition

페이즈 문턱 0.67 / 0.34, 패치 2.5초, 방사 8발·3초 주기, 조준 2.4초, 장판 4초 주기(예고 1초, 수명 3초, 반경 68, 피해 12), 증원 12초마다 4기, 3페이즈 배율 1.3, 선호 거리 190. `FromJson`은 2페이즈 문턱이 3페이즈 문턱보다 크고 주기가 양수인지 확인한다.

보스는 `incremental.json`의 적 편성에 들어 있지 않다. 현재는 `RuneSimulation.SpawnEnemy("boss.governor", …)`나 디버그 소환으로만 등장하므로 **구현은 완전하지만 기본 진행에서는 휴면 상태**다.

### IncrementalDefinition

시간제 전투 전용 설정. 제한시간 30초 기본, 플레이어 (220, 352)에서 시작, 적은 `x = 1180`의 `y` 100~604 구간에서 유입, 유입 간격 1.6초(최소 0.35초), 스테이지당 간격 −8% / HP +15% / 피해 +10%, 전투 내 후반 가속 35%, 고정형 적의 전진 속도 55, **적응 비활성(`isAdaptationEnabled: false`)**.

적 편성은 스테이지 해금과 가중치로 구성된다.

| 적 | 첫 등장 스테이지 | 가중치 | 보상(오버라이드) |
|---|---:|---:|---:|
| `enemy.scout` | 1 | 7 | 2 |
| `enemy.hunter` | 2 | 2 | 4 |
| `enemy.aegis` | 3 | 1.5 | 5 |
| `enemy.sentry` | 4 | 1 | 4 |
| `enemy.relay` | 5 | 0.5 | 8 |

`FromJson`은 숫자 유한성·관계(최소 간격 ≤ 기본 간격, 후반 가속 < 1)뿐 아니라 **공간 유효성까지 확인**한다. 타일을 `MissionMap`으로 만들어 플레이어 시작 좌표가 벽이 아닌지, 각 적 반경으로 유입 구간 양 끝에 설 수 있는지 검사하고, 1스테이지에 등장하는 적이 하나라도 있는지 확인한다.

### MissionMap

ASCII 타일 맵. **40칸 × 22줄 고정**이고 테두리는 전부 벽 문자여야 하며 `P`(플레이어 시작) 문자가 필수다. 벽도 바닥도 아닌 문자는 이름 붙은 좌표로 등록된다(`D` 문, `T` 터미널, 숫자 스폰 포인트).

제공 연산:

| 메서드 | 내용 |
|---|---|
| `IsWall(pos)` | 좌표가 벽 또는 맵 외부인지 |
| `CanOccupy(pos, radius)` | 원–사각형 정확 충돌로 반경을 가진 개체가 설 수 있는지 |
| `Move(origin, displacement, radius)` | 이동을 타일 1/4 크기 조각으로 나눠 진행하고, 막히면 수평·수직으로 분해해 벽을 따라 미끄러짐 |
| `NearestFree(target, radius)` | 목표가 벽이면 전체 타일을 훑어 가장 가까운 점유 가능한 타일 중심을 반환(점멸·스폰 보정용) |

생성자가 타일 배열을 복제하므로 맵은 외부 배열 변경에 영향받지 않는다. 플레이어·적·투사체·볼트가 모두 같은 `Move` / `CanOccupy`를 쓰기 때문에 **충돌 규칙이 한 곳에만 존재한다.**
