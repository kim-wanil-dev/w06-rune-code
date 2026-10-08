# 02. 부트스트래핑

Unity 6000.3.22f1 / Windows x64 기준이다. 런타임 진입점은 장면에 놓인 `RuneCodeApp` 하나뿐이며, 그 외의 모든 오브젝트(캔버스, 이벤트 시스템, 패널, 라벨, 경기장)는 코드가 런타임에 생성한다. 프리팹은 쓰지 않는다.

## 2.1 장면 구성

`Assets/Scenes/RuneCodePoC.unity`에는 GameObject가 두 개뿐이다.

| GameObject | 컴포넌트 | 비고 |
|---|---|---|
| `RuneCode Camera` | `Camera` | `MainCamera` 태그, Orthographic, 단색 클리어(어두운 남색), z = −10 |
| `RuneCode PoC` | `RuneCodeApp` | 직렬화 필드 `_font`에 `Resources/RuneCode/UIFont.asset`(TMP) 연결 |

이 장면은 `RuneCodeBuild.PrepareScene()`(메뉴 `Rune Code > Prepare PoC Scene`)이 만들고, `EditorBuildSettings`에 `SampleScene`과 함께 등록되어 있다. 이미 열려 있거나 파일이 존재하면 **새로 만들지 않고 기존 장면을 그대로 활성화**한다. 열린 장면에 저장되지 않은 변경이 있으면 `InvalidOperationException`을 던져 사용자 작업을 덮어쓰지 않는다.

UI가 전부 절차적이므로 장면 파일에는 Missing Script·Missing Reference가 생길 여지가 거의 없고, 반대로 Inspector에서 조정할 수 있는 값도 글꼴 참조 하나뿐이다.

## 2.2 `RuneCodeApp.Awake()` 순서

```
Awake()
 1  GameData.Load()                         ← 전역 JSON 적재 + 스키마 검증 (실패 시 FormatException)
 2  if (SimulationCli.TryRunCommandLine())  ← 명령행에 --sim 이 있으면
        enabled = false; return;              헤드리스 실행 후 Application.Quit
 3  Application.targetFrameRate = Balance.Sim.TickRate      (= 60)
 4  _save = SaveStore.Load()                ← v2 읽기 / v1 이전 / 손상 시 백업 후 신규
 5  _incremental = RuneSimulation.LoadIncrementalDefinition()   ← 기본 제한시간·스테이지 표시용
 6  _statusMessage = SaveStore.LastWarning  ← 저장 복구·쓰기 실패 경고를 첫 화면에 노출
 7  _debugEnabled = 명령행 "-debug" 또는 absoluteURL "debug=1"
 8  _nextId = _save.Library.Count + 1
 9  _editingGraph  = _save.FindGraph(_save.ActiveSpellId).Clone()
    _previousGraph = _editingGraph.Clone()   ← 되돌리기 기준 스냅샷
10  _screen = AppScreen.Title
11  _view = gameObject.AddComponent<RuneCodeView>()
12  _font ??= TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 36)
    _view.Font = _font
13  Recompile()                             ← 편집 그래프 컴파일, 결과를 도크 로드아웃에 반영
14  StartDock(_dockScenario /* "dummy_single" */)
15  _view.Initialize(this)                  ← 캔버스·이벤트 시스템 생성 후 Refresh()
16  LocalTelemetry.Record(0, "session", "start")
```

순서상 중요한 점.

* **1번이 2번보다 먼저**다. CLI 경로도 `GameData`를 필요로 하고, `CompileIssue`가 메시지를 만들 때 `GameData.L`을 쓰기 때문이다.
* **2번에서 분기**하면 `enabled = false`로 `Update`가 돌지 않는다. `TryRunCommandLine`은 내부에서 `Application.Quit`을 호출하지만 Unity의 종료는 프레임 끝에 일어나므로 이 가드가 필요하다.
* **9번은 `Clone()`**이다. 편집 대상은 항상 저장 사본이고, `SaveGraph()`가 `PlayerSave.StoreGraph`로 다시 복사해 넣는다. 편집 중 저장 객체가 직접 변형되는 일은 없다.
* **11번은 `AddComponent`**다. 뷰는 같은 GameObject에 붙으므로 앱과 수명이 일치하고 별도 연결 작업이 없다.
* **13·14번이 15번보다 먼저**다. `RuneCodeView.Refresh()`가 `_app.CompileResult`와 `_app.Dock`을 즉시 읽기 때문에 뷰 초기화 전에 둘 다 준비돼 있어야 한다. (`BuildDock()`에는 `_app.Dock == null`이면 `StartDock`을 호출하는 보호 코드가 한 줄 더 있다.)

## 2.3 `GameData.Load()` 내부

```
Resources/RuneCode/strings.ko.json  → LocalizationData → Dictionary<string,string> (중복 키 금지)
Resources/RuneCode/runes.json       → RuneCatalog.FromJson      (카테고리·해금·포트·파라미터 검증)
Resources/RuneCode/balance.json     → BalanceData.FromJson      (범위·양수·유한값 검증)
Resources/RuneCode/spells/firebolt.json
Resources/RuneCode/spells/shockwave.json   → ShareCodec.Deserialize
Resources/RuneCode/spells/triplefire.json
   └─ 각 시작 마법을 GraphCompiler.Compile(..., capacity: int.MaxValue) 로 검증
```

세 시작 마법 중 하나라도 컴파일에 실패하면 `FormatException`으로 부팅이 중단된다. 데이터와 컴파일러 규칙이 어긋난 채로 출시되는 상황을 부팅 시점에 잡는 장치다.

필드 대입 순서도 의도적이다. `_balance` → `_spells` → `_runes` 순으로 마지막에 `_runes`를 넣는데, `IsLoaded`가 `_runes != null`이므로 **검증이 모두 끝난 뒤에만 적재 완료로 보이게** 된다. 중간에 예외가 나면 `IsLoaded`는 계속 false다.

문자열 조회 `GameData.L(key)`는 누락 키를 예외로 만들지 않고 키 자체를 반환한다. 화면에 원문 키가 보이면 문자열 누락이라는 뜻이다.

## 2.4 저장 복원 경로

`SaveStore.Load()`:

```
runecode.save.v2.json 존재?  ── 예 ─▶ 그 파일
        └─ 아니오 ─▶ runecode.save.v1.json  (둘 다 없으면 PlayerSave.CreateNew())
                 │
          JsonUtility.FromJson<PlayerSave>
                 │
          Migrate(save)
            Version == 2 → 그대로
            Version == 1 → MigrateToIncremental()  (A 슬롯 마법을 단일 활성 설계로, C 슬롯 환불)
            그 외        → FormatException
                 │
          Validate(out _)   ← 성장 단계 범위, 보관함·슬롯 구조, 룬 ID 존재, 처치 기록
            성공 → 반환
            실패 → LastWarning = "save.recovered", 원본을 *.invalid-backup 으로 복사
                   → PlayerSave.CreateNew()
```

`IOException`/`UnauthorizedAccessException`은 `"save.unavailable"` 경고로, `FormatException`/`ArgumentException`은 추가로 `.invalid-backup` 복사까지 수행한다. **원본 v1 파일은 수정하지 않는다.** 어떤 경로에서도 `Load()`는 null을 반환하지 않으므로 `Awake` 이후 `_save`는 항상 유효하다.

쓰기는 `SaveStore.Write()`가 `*.tmp`에 먼저 기록하고 `File.Replace`로 교체하며 직전 정상본을 `.backup`으로 남긴다. 실패는 예외가 아니라 `false` + `LastWarning`으로 전달된다.

## 2.5 화면 진입과 첫 프레임

`Awake` 종료 시점의 상태는 `AppScreen.Title`이다. `RuneCodeView.Refresh()`가 타이틀 페이지를 만들고, `시작` 버튼이 `RuneCodeApp.StartWorkshop()`을 호출해 `AppScreen.Workshop` / `_tab = "editor"`로 들어간다. 이때부터 편집 탭 조건이 만족되어 `Update`의 도크 루프가 돌기 시작한다.

`RuneCodeView.Initialize`가 만드는 것:

```
RuneCodeApp (GameObject)
 └─ RuneCodeCanvas            Canvas(ScreenSpaceOverlay) + CanvasScaler(1280×720, Expand) + GraphicRaycaster
     └─ LogicalScreen         1280×720 고정 논리 해상도 (중앙 정렬)
         └─ Panel(_page)      화면/탭마다 파괴·재생성되는 페이지 루트
 └─ RuneCodeEventSystem       EventSystem.current 가 없을 때만 생성
                              EventSystem + InputSystemUIInputModule
```

화면 전환은 `_page`를 `Destroy`하고 다시 쌓는 방식이다. 그래서 `Refresh()`는 맨 앞에서 캐시된 위젯 참조(`_graph`, `_dockArena`, `_inspector` 등)를 전부 null로 지우고 다시 채운다. 반면 `RefreshGraph()`는 페이지를 재생성하지 않고 라벨·지표·인스펙터만 갱신하는 경량 경로다.

## 2.6 헤드리스 CLI 부팅

`SimulationCli.TryRunCommandLine()`은 `--sim`이 있을 때만 true를 반환한다.

```
--spell <이름|경로>   기본 firebolt   파일이 있으면 그 JSON, 없으면 Resources/RuneCode/spells/<이름>
--scenario <이름>     기본 dummy_line  "incremental" 이면 시간제 전투 모드
--ticks <n>           기본 600        1 ~ 1,000,000
--seed <n>            기본 1
--stage <n>           기본 1
--duration <초>       기본 0 → JSON 기본값 사용
--capacity-level <n>  기본 0
--energy-level <n>    기본 0
--output <경로>       기본 {persistentDataPath}/runecode.sim.json
```

실행 흐름: 그래프 적재 → 인수 범위 검증 → 모든 룬 해금 조건으로 컴파일 → `RuneSimulation` 생성 → 틱 루프 → `SimulationReport`를 JSON으로 직렬화해 파일과 콘솔에 출력 → `Application.Quit(0)`. 예외는 `Debug.LogError` 후 `Quit(1)`이다.

시나리오가 `incremental`이면 `CreateAutomaticInput`이 가장 가까운 생존 적을 조준하고 사거리 안에서만 시전하는 입력을 만든다. 그 외 도크 시나리오는 `(0,0)` 이동 + `(1,0)` 조준 + 매 틱 시전이라는 고정 입력을 쓴다. 두 경우 모두 사용자 저장 파일을 건드리지 않는다.

래퍼는 `tools/sim.ps1`이며 `-batchmode -nographics`로 빌드된 실행 파일을 호출한다. 그러므로 **CLI는 Windows 빌드가 있어야 동작한다.**

## 2.7 에디터 빌드 경로

`RuneCodeBuild`(`Assembly-CSharp-Editor`)의 메뉴 두 개.

* **`Rune Code > Prepare PoC Scene`**
  TMP Essential Resources가 없으면 먼저 임포트 → `GameData.Load()`로 데이터 유효성 확인 → `PrepareFont()` → 장면 확보. `PrepareFont()`는 기존 `UIFont.asset`이 있으면 `Resources/RuneCode` 아래 모든 `TextAsset` 본문 + `−–` 문자를 `TryAddCharacters`로 추가하고, 없으면 시스템 글꼴 `Malgun Gothic`에서 ASCII + 전체 데이터 문자 + 기호를 담은 TMP 자산을 새로 만든다. 한국어 문자열이 늘어나도 아틀라스가 따라오게 하는 장치다.
* **`Rune Code > Build Windows PoC`**
  `PrepareScene()` → `BuildPipeline.BuildPlayer`(StandaloneWindows64, `DetailedBuildReport`) → `Builds/RuneCodePoC-ManualMods/RuneCodePoC.exe`. 요약을 `build-report.txt`로 기록하고 실패 시 예외를 던진다. 빌드 장면 목록은 `RuneCodePoC.unity` 단 하나다.
