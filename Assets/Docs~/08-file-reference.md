# 08. 파일 참조와 연결 지도

> 2026-10-08 마법 그래프 토큰 모델 리팩토링 기준. 줄 수는 그 시점 값이다.

## 8.1 파일별 책임

| 파일 | 줄 | 어셈블리 | 담은 타입 | 책임 |
|---|---:|---|---|---|
| `Core/Data/GameData.cs` | 64 | Core | `LocalizedEntry`, `LocalizationData`, `GameData` | 전역 JSON 1회 적재(문자열·밸런스), 문자열 조회 |
| `Core/Data/BalanceData.cs` | 398 | Core | `PlayerBalance`, `LimitBalance`, `CombatBalance`, `EconomyBalance`, `SimulationBalance`, `SpellSettings`, `BalanceData` | 밸런스 섹션과 마법 그래프 설정, 범위 검증, 성장 가격 |
| `Core/Graph/SpellGraph.cs` | 215 | Core | `NodeParameter`, `GraphNode`, `GraphEdge`, `SpellGraph` | 편집 그래프 모델(직렬화 스키마), 기본 그래프 |
| `Core/Graph/SpellNodes.cs` | 337 | Core | `SpellNodeKind`, `AimMode`, `BranchCondition`, `SpellNodes` | 노드 9종 표: 포트 수, 머무는 시간, 파라미터, 비용·공식 |
| `Core/Graph/SpellProgram.cs` | 471 | Core | `SpellProgramIssue`, `SpellProgram` | 읽기 전용 실행 그래프, G1~G6/W1 검증, 도달성, 서명 |
| `Core/Graph/ShareCodec.cs` | 66 | Core | `ShareCodec` | JSON 직렬화·검증(복제·저장 검증) |
| `Core/Mission/MissionData.cs` | 344 | Core | `EnemyDefinition`(+경감), `EnemyCatalog`, `MissionSpawn`, `MissionWave`, `MissionRoom`, `SectorDefinition`, `GovernorDefinition`, `IncrementalEnemyEntry`, `IncrementalDefinition`, `MissionMap` | 적·맵·보스·시간제 설정과 타일 충돌 |
| `Core/Simulation/RuneSimulation.cs` | 509 | Core | `RuneSimulation` (+ `DamageSample`, `PendingEnemyShot`) | 60Hz 틱 루프, 적 AI, 피해 콜백, 상태 해시 |
| `Core/Simulation/SimulationTypes.cs` | 315 | Core | `SimVector`, `SimulationInput`, `MissionStage`, `SimulationPlayer`, `SimulationEnemy`, `SimulationProjectile`, `FragmentOrb`, `DamageNumber`, `NodeExecutionEvent` | 시뮬레이션 상태 모델 |
| `Core/Simulation/SpellRuntime.cs` | 713 | Core | `SpellRuntime` | 시전, 토큰 처리, 합류·분배·분기, 발사 묶음, 투사체, 적중 토큰 |
| `Core/Simulation/SpellRuntimeTypes.cs` | 222 | Core | `TokenEndReason`, `CastResult`, `SpellToken`, `SpellProjectile`, `FireRequest`, `TokenEndEvent` | 런타임 상태 타입 |
| `Game/RuneCodeApp.cs` | 646 | Assembly-CSharp | `AppScreen`, `RuneCodeApp` | 진입점, 상태 기계, 고정 스텝, 편집 연산, 클릭 시전, G 토글, 성장·정산 |
| `Game/RuneArenaGraphic.cs` | 242 | Assembly-CSharp | `RuneArenaGraphic` | 경기장 메시(투사체 세대 색·운반 마나 링) + 피해 숫자 |
| `Game/SimulationCli.cs` | 151 | Assembly-CSharp | `SimulationCli`, `SimulationReport` | 헤드리스 실행과 지표 리포트 |
| `Game/LocalTelemetry.cs` | 60 | Assembly-CSharp | `LocalTelemetry`, `TelemetryEntry`, `TelemetryDump` | 500건 로컬 링 버퍼 |
| `UI/RuneCodeView.cs` | 843 | Assembly-CSharp | `RuneCodeView` | 절차적 uGUI 화면·모달·HUD·인스펙터·팔레트 |
| `UI/RuneGraphCanvas.cs` | 692 | Assembly-CSharp | `RuneGraphCanvas` | 그래프 편집 상호작용과 렌더링, 흐름 레이어 연결 |
| `UI/GraphLayout.cs` | 101 | Assembly-CSharp | `GraphLayout` | 노드 크기·포트 위치·엣지 곡선 공용 계산 |
| `UI/SpellFlowLayer.cs` | 390 | Assembly-CSharp | `SpellFlowLayer` | 토큰 흐름·×N 배지·지불 비용·소멸 표시 레이어 |
| `UI/RuneGraphOverlay.cs` | 183 | Assembly-CSharp | `RuneGraphOverlay` | 전투 중 축소 그래프 오버레이 |
| `UI/RuneMesh.cs` | 101 | Assembly-CSharp | `RuneMesh` | 메시 프리미티브와 색상(노드 종류 색 포함) |
| `UI/RunePaletteDrag.cs` | 30 | Assembly-CSharp | `RunePaletteDrag` | 팔레트 → 캔버스 드래그 배치 |
| `Save/PlayerSave.cs` | 272 | Assembly-CSharp | `PlayerSave`, `KillRecord` | 저장 모델 v3, 검증, v1→v2→v3 이전 |
| `Save/SaveStore.cs` | 73 | Assembly-CSharp | `SaveStore` | v3 원자적 쓰기, 이전 파일 읽기, 백업, 복구 |
| `Editor/RuneCodeBuild.cs` | 114 | Assembly-CSharp-Editor | `RuneCodeBuild` | 장면·글꼴 준비, Windows 빌드 |

삭제된 파일: `GraphCompiler.cs`, `CompiledSpell.cs`, `RuneData.cs`, `AdaptationNet.cs`, `runes.json`, `spells/*.json`.

## 8.2 호출 지도

### 부팅

```
RuneCodeApp.Awake
 ├─ GameData.Load ─────────▶ BalanceData.FromJson (+ ValidateSpell)
 ├─ SimulationCli.TryRunCommandLine ─▶ SimulationCli.Run ─▶ SpellProgram.Build / RuneSimulation / SimulationReport
 ├─ SaveStore.Load ────────▶ PlayerSave 이전(v1→v2→v3) / Validate ─▶ ShareCodec 왕복, SpellNodes.TryParse
 ├─ PlayerSave.FindGraph ─▶ SpellGraph.Clone ─▶ ShareCodec
 ├─ RebuildProgram ───────▶ SpellProgram.Build ─▶ RuneSimulation.SetProgram (도크)
 ├─ StartDock ────────────▶ new RuneSimulation + ResetBench + SetProgram
 └─ RuneCodeView.Initialize ─▶ Refresh ─▶ Build*  (RuneGraphCanvas + SpellFlowLayer / RuneArenaGraphic 생성)
```

### 편집 1회

```
RuneGraphCanvas.OnPointerUp (출력 → 입력으로 정규화)
 └─ RuneCodeApp.Connect
     ├─ 종류·포트 검증 (시전·적중 입력 금지)
     ├─ 같은 출력 포트의 기존 엣지 제거 → SpellGraph.AddEdge
     ├─ RuneCodeApp.ChangedGraph → _undo push, 도크 ClearSpellState, _graphDirty
     └─ RuneCodeView.RefreshGraph

(0.2초 뒤) RuneCodeApp.Update
 ├─ RebuildProgram ─▶ SpellProgram.Build ─▶ (서명이 바뀌면) RuneSimulation.SetProgram
 ├─ SaveGraph ─▶ PlayerSave.StoreGraph ─▶ SaveStore.Write
 └─ RuneCodeView.RefreshGraph ─▶ 라벨·지표·인스펙터·검증 목록
```

### 전투 1틱

```
RuneCodeApp.Update (클릭한 프레임에만 CastA = true)
 └─ RuneSimulation.Step(SimulationInput)
     ├─ SimulationPlayer.Aim / Advance(마나풀 회복) / StartDash / Move
     ├─ TryCast ─▶ SpellRuntime.TryCast ─▶ SimulationPlayer.TrySpendEnergy
     ├─ AdvanceEnemies / RunEnemyShots
     ├─ SpellRuntime.AdvanceProjectiles ─▶ OnProjectileHit ─▶ SimulationEnemy.TakeSpellDamage
     │                                    └─ 적중 토큰 생성
     ├─ SpellRuntime.ProcessTokens ─▶ Arrive(비용·효과) ─▶ OnNodePaid ─▶ NodeExecutionEvent
     ├─ SpellRuntime.FlushFireRequests ─▶ SpellProjectile 생성
     ├─ AdvanceHostileProjectiles ─▶ HurtPlayer
     ├─ CleanupEnemies ─▶ FragmentOrb / 처치 집계
     └─ CollectNearbyOrbs, TrimFeedback(+ TrimEvents)

 (렌더) RuneArenaGraphic / RuneGraphOverlay / SpellFlowLayer ─▶ RuneMesh.*   ← 읽기 전용 조회
 (텔레메트리) CaptureNodeTelemetry ─▶ LocalTelemetry.Record
```

### 정산

```
RuneSimulation.Step → Stage = Cleared / Dead
 └─ RuneCodeApp.FinishMission
     ├─ PlayerSave.RecordStageClear / Settle / RecordKills
     ├─ SaveStore.Write
     ├─ LocalTelemetry.Record
     └─ RuneCodeView.Refresh ─▶ BuildResult
```

## 8.3 계층 간 공개 표면

| 대상 | 호출 가능한 것 |
|---|---|
| `GameData` | `Load()`, `IsLoaded`, `Balance`, `L(key)` |
| `BalanceData` | 섹션 프로퍼티, `Spell`(SpellSettings), `EconomyBalance.GetGrowthCost` |
| `SpellGraph` | `AddNode`/`RemoveNode`/`AddEdge`/`RemoveEdge`, `FindNode`, `Rename`, `SetIdentity`, `Clone`, `Create` |
| `SpellNodes` | `TryParse`, `GetId`, 포트 수, `GetDwell`, `GetCost`, `GetCostFormula`, 파라미터 키·범위·옵션 |
| `SpellProgram` | `Build`, `IsValid`, `Errors`, `Warnings`, `IndexOf`, `GetNodeId`, `GetKind`, `TryGetNext`, `IsReachable`, 파라미터 조회, `Signature` |
| `ShareCodec` | `Serialize`, `Deserialize` |
| `SpellRuntime` | 읽기 전용: `Program`, `Tokens`, `Projectiles`, `EndEvents`, `ActiveTokenCount`, `LastCastResult/Tick`, `LastOverloadTick`, `GetLastPaidCost/Tick` |
| `RuneSimulation` | 생성자, `Step`, `TryCast`, `SetProgram`, `ClearSpellState`, `SetDebugInvulnerable`, `ResetBench`, `SpawnEnemy`, `DebugSpawn`, `DebugDefeatRoom`, `StateHash`, `LoadIncrementalDefinition`, `Spell` + 읽기 전용 상태 프로퍼티 |
| `MissionMap` | `Tiles`, `TileSize`, `Width`, `Height`, `PlayerStart`, `HasPoint`, `GetPoint`, `IsWall`, `CanOccupy`, `Move`, `NearestFree` |

`SimulationPlayer`, `SimulationEnemy`, `SpellToken`, `SpellProjectile`의 상태 변경자는 전부 `internal`이다. 어셈블리 경계가 "뷰는 읽기만" 규칙을 컴파일 타임에 보장한다.

## 8.4 코드 규약 (AGENTS.md 적용 결과)

| 항목 | 적용 |
|---|---|
| 필드 | private은 `_camelCase`. `[SerializeField] public`은 없음. 외부 노출은 읽기 전용 프로퍼티 또는 `internal set` |
| 상수 | `SCREAMING_SNAKE_CASE` (`TICK_RATE`, `MAX_JSON_LENGTH`, `CAST_ID`, `NODE_WIDTH`, `WAIT_EPSILON` …) |
| 직렬화 | `[Serializable]` + `[Header("역할")]`. JSON 필드명이 private 필드명과 일치 |
| 주석 | Unity Lifecycle/Callback을 제외한 모든 메서드에 한국어 XML `summary` |
| using 순서 | `System` → `UnityEngine` → Unity 패키지 → 프로젝트 순, 그룹 사이 빈 줄 |
| 패턴 회피 | Singleton·Event Bus·Interface·추상 계층을 쓰지 않는다. 통신은 직접 호출, `Func<T>`·`Action<T>` 주입 |
| 결정성 | 토큰은 생성 순서, 합류 대기는 FIFO, 노드 조회는 색인 배열. 틱 안에서 LINQ를 쓰지 않는다 |

## 8.5 알아두면 좋은 지점

* **그래프 좌표는 실행에 무관하다.** `Signature`가 좌표와 이름을 제외하므로 노드를 옮겨도 같은 마법이고 도크 실행 상태도 유지된다.
* **노드 ID는 저장 형식 그대로다.** 포트는 `"0"`/`"1"` 문자열이며 `SpellProgram`이 정수로 바꾼다.
* **비용 바닥값 1이 루프를 끝낸다.** 분열 자기 루프도 마나가 바닥나면 지불 불가로 소멸한다(규칙 V8).
* **편집하면 도크 실행 상태가 지워진다.** `ChangedGraph`, Undo/Redo, 프로그램 교체가 모두 `ClearSpellState`/`SetProgram`을 부른다(규칙 9절, D5).
* **충돌 판정이 `MissionMap` 한 곳에만 있다.** 플레이어·적·투사체·탄환이 같은 `Move`/`CanOccupy`를 쓴다.
* **휴면 코드가 남아 있다.** 적 상태이상(화염·냉기·전격) 필드와 냉기 감속 계산, `CombatBalance`의 이지스·릴레이 값, `EconomyBalance`의 용량 비용, `PlayerSave._capacityLevel`, `MissionStage.Terminal`, `sector1.json`의 방·웨이브. 현재 진행 경로에서는 값이 쌓이지 않거나 쓰이지 않는다.
* **UI는 전부 코드 생성이다.** 프리팹이 없으므로 Missing Reference 위험이 낮은 대신, 레이아웃 수정은 코드 수정이다.
