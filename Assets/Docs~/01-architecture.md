# 01. 계층 구조와 의존 방향

## 1.1 폴더와 어셈블리

| 경로 | 어셈블리 | 역할 |
|---|---|---|
| `Assets/RuneCode/Core/` | **RuneCode.Core** (`RuneCode.Core.asmdef`) | 게임 규칙 전체. 데이터 적재·검증, 그래프 컴파일, 결정적 시뮬레이션 |
| `Assets/RuneCode/Game/` | Assembly-CSharp | 앱 진입점(`RuneCodeApp`), 헤드리스 CLI, 로컬 기록, 경기장 렌더러 |
| `Assets/RuneCode/UI/` | Assembly-CSharp | 절차적 uGUI 화면, 그래프 편집 캔버스, 메시 헬퍼 |
| `Assets/RuneCode/Save/` | Assembly-CSharp | 저장 데이터 모델과 파일 입출력 |
| `Assets/RuneCode/Editor/` | Assembly-CSharp-Editor | 장면·글꼴 준비와 Windows 빌드 메뉴 |
| `Assets/RuneCode/Resources/RuneCode/` | — | 런타임에 읽는 JSON 설정 7종과 시작 마법 3종, TMP 글꼴 자산 |
| `Assets/TutorialInfo/Scripts/` | Assembly-CSharp(+Editor) | Unity 템플릿 Readme 자산(프로젝트 로직과 무관) |

`RuneCode.Core.asmdef`의 `references`는 비어 있다. 즉 Core는 `Assembly-CSharp`의 어떤 타입도 볼 수 없고, 참조는 항상 한 방향으로만 흐른다.

```
Editor  ──▶ Game ──▶ UI ──▶ Save ──▶ Core ──▶ UnityEngine (JsonUtility / Resources / TextAsset)
   └──────────────────────────────────────▶ Core
```

Core도 `UnityEngine`에는 의존한다. `JsonUtility`로 JSON을 읽고 `Resources.Load<TextAsset>`로 자산을 가져오며 `[SerializeField]`로 직렬화 스키마를 정의하기 때문이다. 다만 `MonoBehaviour`·`Transform`·입력·`Time`은 Core 어디에도 없다.

## 1.2 앱 계층과 시뮬레이션 계층

두 계층의 경계는 `RuneSimulation.Step(SimulationInput)` 한 지점이다.

```
 ┌──────────────── 앱 계층 (Assembly-CSharp) ────────────────┐
 │ 가변 프레임, Unity 입력, uGUI, 파일 저장, 화면 전환        │
 │                                                           │
 │  RuneCodeApp.Update()                                     │
 │    accumulator += Time.unscaledDeltaTime                  │
 │    while (accumulator >= 1/60 && steps < MaxFrameSteps)    │
 │        ├── Keyboard/Mouse → SimulationInput 생성 ─────────┼──┐
 │        └── accumulator -= 1/60                            │  │
 └───────────────────────────────────────────────────────────┘  │
                                                                ▼
 ┌─────────── 시뮬레이션 계층 (RuneCode.Core) ───────────────────┐
 │ 고정 1/60초, double 연산, 자체 RNG, Unity 시간 미사용         │
 │                                                              │
 │  RuneSimulation.Step(input)  →  _tick++                      │
 │    상태: Player / Enemies / SpellEntities / Projectiles /     │
 │          Orbs / DamageNumbers / NodeEvents / AdaptationNet    │
 └──────────────────────────────────────────────────────────────┘
                                                                │
 읽기 전용 조회(IReadOnlyList 프로퍼티)로 앱 계층이 그린다  ◀───┘
```

### 단방향 데이터 흐름

* **입력**: 앱 계층이 `SimulationInput`(이동, 조준, CastA/B/C, Dash, Interact)으로 포장해 넘긴다. 시뮬레이션은 `Keyboard.current`를 직접 읽지 않는다.
* **출력**: 시뮬레이션은 `IReadOnlyList<SimulationEnemy>` 같은 읽기 전용 프로퍼티만 노출한다. 뷰는 매 프레임 이 목록을 읽어 메시를 다시 만든다. 뷰가 시뮬레이션 상태를 직접 쓰는 경로는 없다.
* **상태 변경자**: 외부가 시뮬레이션을 바꿀 수 있는 공개 메서드는 `SetLoadout`, `SetUnlockedElements`, `SetAdaptationEnabled`, `SetDebugInvulnerable`, `TryCast`, `ResetBench`, `SpawnEnemy`, `DebugSpawn`, `DebugDefeatRoom`으로 한정된다.

### 시뮬레이션 인스턴스가 두 개인 이유

`RuneCodeApp`은 `RuneSimulation`을 동시에 두 개 들고 있다.

| 필드 | 생성 조건 | 용도 |
|---|---|---|
| `_dock` | `isMission: false` + `ResetBench(scenario)` | 작업실 편집 탭 하단의 시험 도크. 더미 적, 적응 옵션, 0.5/1/2× 배속 |
| `_mission` | `isMission: true` + 스테이지·제한시간·영구 능력치 | 실제 전투. 오른쪽 적 유입, 제한시간, 사망·클리어 정산 |

둘은 상태를 전혀 공유하지 않는다. 각자 자기 `MissionMap`, `AdaptationNet`, RNG 상태를 가진다. 같은 `Update` 프레임에서 두 누산기(`_missionAccumulator`, `_dockAccumulator`)가 독립적으로 돌고, 활성화되지 않은 쪽의 누산기는 0으로 초기화되어 탭을 떠난 사이의 시간이 몰아서 적용되는 일이 없다.

## 1.3 Core 내부 4계층

Core 자체도 아래에서 위로 쌓인 4개 층이고, 아래 층은 위 층을 모른다.

```
Simulation/   RuneSimulation · SimulationTypes · AdaptationNet
      ▲  컴파일된 마법(CompiledSpell)과 밸런스를 입력으로 받는다
      │
Graph/        SpellGraph · GraphCompiler · CompiledSpell · ShareCodec
      ▲  룬 정의와 제한값을 읽어 그래프를 검증·컴파일한다
      │
Mission/      EnemyCatalog · SectorDefinition · GovernorDefinition
              IncrementalDefinition · MissionMap
      ▲  적·맵 설정. 타일 충돌은 여기 MissionMap이 전담
      │
Data/         GameData (정적 레지스트리) · RuneCatalog · BalanceData
```

* `Data/`는 프로세스 전역 1회 적재다. `GameData.Load()`는 `IsLoaded` 가드로 중복 적재를 막는다.
* `Mission/`의 적·맵 데이터는 `GameData`에 들어가지 않는다. `RuneSimulation` 생성자가 매 인스턴스마다 `Resources`에서 다시 읽는다(§03.4 참고).
* `Graph/`는 시뮬레이션을 모른다. `GraphCompiler`의 출력 `CompiledSpell`은 `SpellAction` 트리라는 불변 자료구조일 뿐이다.
* `Simulation/`은 편집 그래프(`SpellGraph`)를 모른다. 받는 것은 `CompiledSpell`과 노드 ID 문자열뿐이고, 그 ID를 `NodeExecutionEvent`로 되돌려 보내 UI가 하이라이트할 수 있게 한다.

## 1.4 설계상 지켜지는 규칙

1. **단일 데이터 원본**: 조정 가능한 수치는 전부 `Resources/RuneCode/*.json`에 있고 코드에는 상수로 복제되지 않는다. 예외는 의미가 고정된 값뿐이다(`TICK_RATE = 60`, `DEGREES_TO_RADIANS`, 좌표 변환용 `WORLD_WIDTH/HEIGHT`, 히스토리 상한 50, 텔레메트리 상한 500, 공유 코덱 크기 상한).
2. **생성 시 전량 검증**: 모든 `FromJson`은 실패 시 `FormatException`을 던진다. 런타임 중간에 설정이 비정상인 상태로 진행하는 경로가 없으므로 시뮬레이션 내부에 방어적 `null` 검사가 거의 없다.
3. **불변 컴파일 결과**: `CompiledSpell`/`SpellAction`/`SpellStats`는 전부 `readonly` 필드 + 읽기 전용 프로퍼티다. 시뮬레이션이 실행 중 마법 정의를 변형하지 않는다.
4. **결정성**: `RuneSimulation`은 `UnityEngine.Random`을 쓰지 않고 mulberry32 기반 `NextRandom()`을 쓴다. 정렬은 전부 `StringComparer.Ordinal` 또는 거리 비교로 안정화되어 있고, 상태 해시 문자열은 `CultureInfo.InvariantCulture`로 쓴다.
5. **레거시 호환 표면**: `RuneCodeApp`에는 과거 다중 마법·터미널 설계 시절의 메서드가 빈 구현 또는 안내 메시지로 남아 있다(§06.2). 호출부를 깨지 않으면서 단일 마법 규칙을 강제하는 장치다.
