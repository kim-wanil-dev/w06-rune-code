# 07. 저장 · 텔레메트리 · 도구

## 7.1 PlayerSave — 저장 데이터 모델

`Assets/RuneCode/Save/PlayerSave.cs`. `[Serializable]` 클래스이고 `JsonUtility`로 직렬화된다. 현재 버전은 **3**이다(2026-10-08 마법 그래프 토큰 모델 전환).

| 섹션 | 필드 |
|---|---|
| 버전 | `_version` (= 3) |
| 재화·성장 | `_currency`, `_capacityLevel`(더 이상 쓰지 않음), `_energyLevel`(마나풀 성장), `_hpLevel`, `_durationLevel` |
| 진행 | `_highestClearedStage`, `_selectedStage`, `_sectorCleared` |
| 마법 | `_activeSpellId`, `_library`(`List<SpellGraph>`), `_loadout`(3칸), `_slotCount`(= 1 고정) |
| 기록 | `_killCounts`(`List<KillRecord>`) (룬 해금 목록은 제거) |
| 설정 | `_screenShake`, `_hitStop`, `_tutorialStep` |

### 신규 생성

`CreateNew()`는 기본 그래프 `SpellGraph.Create("spell-1", L("spell.defaultName"))`(시전 → 투사체) 하나를 보관함에 넣어 활성 마법으로 지정하고, 로드아웃을 `[활성 ID, null, null]`로 채운다. 모든 노드 종류를 제한 없이 쓸 수 있으므로 해금 목록이 없다.

### 변경 메서드

| 메서드 | 규칙 |
|---|---|
| `Spend(amount)` | 음수 거부, 잔액 부족이면 false |
| `Settle(fragments, cleared)` | 음수 보상은 0으로, 잔액은 1억으로 포화. 완주면 `_sectorCleared = true` |
| `Upgrade(kind)` | `capacity` / `energy` / `duration`만, 각자 상한 미만일 때 +1. 앱은 `energy`·`duration`만 판매한다(D1) |
| `StoreGraph(graph)` | 같은 ID가 있으면 사본으로 교체, 없으면 추가(`MaxLibrary` 상한) |
| `SetLoadout(slot, id)` | **`slot == 0`이고 활성 마법 ID일 때만** 반영 |
| `RemoveGraph(id)` | 활성 마법은 삭제 불가 |
| `SelectStage` / `RecordStageClear` | 해금 범위(`최고 클리어 + 1`) 안에서만 |
| `RecordKills(enemyId, count)` | 적 ID별 누적(`KillRecord.Add`가 음수를 0으로) |
| `SetTutorialStep(step)` | `max(기존, min(3, step))` — 되돌아가지 않음 |
| `SetFeedback(shake, hitStop)` | 설정 갱신 |

`StoreGraph`가 `graph.Clone()`을 저장하므로 저장 객체와 편집 중인 그래프는 항상 분리된다.

### Validate — 불러오기 검증

실패 시 `error`에 문자열 키를 담고 false를 반환한다.

**`save.invalidValues`** — `_version != 3`, 재화 음수 또는 1억 초과, 성장 단계가 상한 초과, `_highestClearedStage`가 0 미만 또는 100만 이상, `_selectedStage`가 `[1, 최고+1]` 밖, **`_slotCount != 1`**, 튜토리얼 단계가 `[0, 3]` 밖.

**`save.invalidStructure`** —
* 보관함이 비었거나 `MaxLibrary` 초과, 로드아웃 길이가 3이 아님, 리스트가 null
* 보관함 마법의 ID 공백·중복
* 각 마법을 `ShareCodec.Serialize` → `Deserialize`로 왕복시켜 구조 검증 실패, 또는 노드 종류 ID가 `SpellNodes.TryParse`로 해석되지 않음
* 처치 기록의 ID 공백·80자 초과·음수·중복
* 로드아웃이 보관함에 없는 ID를 참조하거나 `_slotCount` 밖 슬롯에 값이 있음
* `_activeSpellId`가 비었거나 보관함에 없거나 **`_loadout[0]`과 다름**

마지막 조건이 단일 마법 불변식을 저장 수준에서 고정한다.

### v1 → v2 이전 (`MigrateToIncremental`)

```
_version != 1 이면 아무 것도 하지 않음
구조 사전 검사 실패 → FormatException  (슬롯 2~3개, 성장 단계 범위, 로드아웃 3칸, 보관함 비어있지 않음, 재화 범위)

_activeSpellId = 로드아웃 A 슬롯이 보관함에 있으면 그 ID, 아니면 보관함 첫 항목
_slotCount == 3 이었다면 재화 += Economy.SlotCost (= 120)    ← C 슬롯 환불
_slotCount = 1
_loadout = [활성 ID, null, null]
_durationLevel = 0
_highestClearedStage = _sectorCleared ? 1 : 0
_selectedStage = 최고 + 1
_version = 2
```

A 외의 설계는 **보관함 안에 보존**되고 로드아웃에서만 빠진다(그 뒤 v3 이전에서 보관함이 기본 그래프로 바뀐다).

### v2 → v3 이전 (`MigrateToGraphTokens`)

```
_version != 2 이면 아무 것도 하지 않음
_library = [SpellGraph.Create("spell-1", 기본 이름)]     ← 옛 룬 그래프는 새 노드와 호환되지 않는다(D4)
_activeSpellId / _loadout = [새 ID, null, null], _slotCount = 1
_tutorialStep = 0                                       ← 새 노드 튜토리얼을 다시 보인다
_version = 3
```

재화·성장 단계·스테이지 기록·처치 기록·설정은 그대로 이전된다. v1 파일은 v1 → v2 → v3 순서로 이전된다.

## 7.2 SaveStore — 파일 입출력

`Assets/RuneCode/Save/SaveStore.cs`. 경로는 `Application.persistentDataPath`이고 파일명은 `runecode.save.v3.json`(이전 `runecode.save.v2.json`, 레거시 `runecode.save.v1.json`). 이전 버전 파일은 읽기만 하고 쓰지 않는다.

```
Load()
  v3 → v2 → v1 순서로 있는 파일을 읽고, 모두 없으면 CreateNew()
  읽기 → Migrate() → Validate()
    성공 → 반환
    검증 실패 → LastWarning = "save.recovered", 원본을 *.invalid-backup 으로 복사, CreateNew()
  예외:
    IO / 접근 거부      → LastWarning = "save.unavailable"
    Format / Argument  → 같은 경고 + *.invalid-backup 복사
  어떤 경로에서도 null 을 반환하지 않는다

Write(save)
  persistentDataPath 디렉터리 보장
  SavePath + ".tmp" 에 전체 기록
  기존 파일이 있으면 File.Replace(tmp, 본파일, 본파일 + ".backup")   ← 원자적 교체 + 직전본 보존
  없으면 File.Move
  IO / 접근 거부 → LastWarning 설정 후 false
```

예외를 삼키지 않고 `LastWarning`으로 전달하며, `RuneCodeApp.Awake`가 이 값을 첫 상태 메시지로 올려 사용자에게 보여준다. 저장 쓰기는 `SaveGraph`, 장착, 구매, 정산, 설정 변경, 튜토리얼 진행, `OnApplicationQuit`에서 호출된다.

같은 폴더에 남는 부산물: `.tmp`(교체 직후 사라짐), `.backup`(직전 정상본), `.invalid-backup`(손상본 보존).

## 7.3 LocalTelemetry

`Game/LocalTelemetry.cs`. 외부 전송이 없는 **로컬 전용 링 버퍼**다.

```csharp
const int MAX_ENTRIES = 500;
static readonly Queue<TelemetryEntry> _entries;   // (tick, kind, detail)
```

가득 차면 가장 오래된 항목을 버린다. `DumpJson()`은 `TelemetryDump`로 감싸 JSON을 만들고 `DumpToConsole()`이 `Debug.Log`로 출력한다. **F8**이 이 덤프를 호출한다.

기록되는 종류:

| kind | detail | 호출 지점 |
|---|---|---|
| `session` | `start` | `Awake` |
| `mission.start` | `스테이지:서명` | `Deploy` |
| `mission.result` | `clear:조각` 또는 `death:조각` | `FinishMission` |
| `mission.node` | 노드 ID | 미션 틱의 `CaptureNodeTelemetry` |
| `dock.node` | 노드 ID | 도크 틱의 `CaptureNodeTelemetry` |
| `bench.cast` | 마법 서명 | `FireDock` |
| `bench.purchase` | `항목:비용` | `BuyUpgrade` |

`CaptureNodeTelemetry`는 `NodeExecutionCount`의 증가분만큼 `NodeEvents` 뒤쪽을 읽는다. `NodeEvents`는 1초 뒤 잘려 나가지만 카운터는 누적이므로, **같은 틱에 여러 번 실행된 노드도 중복 없이 전부 기록**된다.

## 7.4 SimulationCli — 헤드리스 실행

`Game/SimulationCli.cs`. `RuneCodeApp.Awake`에서 `--sim` 유무로 분기한다(§02.6).

`Run(...)`의 흐름:

```
GameData.Load()
ticks 범위 확인 (1 ~ 1,000,000), --cast-interval 은 1 이상
마법 적재: --spell 값이 실제 파일이면 그 JSON, 아니면 기본 그래프 SpellGraph.Create("cli-default")
stage / duration / energyLevel 범위 확인
isTimedBattle = (scenario == "incremental")
maxEnergy = Spell.ManaMax + energyLevel × StatStep,  regen = Spell.ManaRegen + energyLevel × EnergyRegenStep
program = SpellProgram.Build(graph) → 오류가 있으면 graph.<코드> 문구를 합쳐 ArgumentException
RuneSimulation 생성 (시간제면 미션 모드, 아니면 ResetBench(scenario)) → SetProgram(program)
castRange = Spell.ProjectileRangeTiles × Map.TileSize
for tick in 0..ticks:  종료·사망이면 중단
    canCast = tick % castInterval == 0          ← 규칙 4.1: 누를 때마다 한 번
    시간제 → CreateAutomaticInput(sim, castRange, canCast)
    그 외  → SimulationInput(이동 0, 조준 (1,0), CastA = canCast)
JsonUtility.ToJson(new SimulationReport(...), pretty)
```

### 자동 전투 입력

`CreateAutomaticInput`은 가장 가까운 생존 적을 조준하고, `canCast`이면서 거리가 `castRange + 적 반경` 이내일 때만 `CastA`를 켠다. 이동은 하지 않는다.

### SimulationReport

JSON으로 나가는 필드.

| 필드 | 내용 |
|---|---|
| `_ticks`, `_seed`, `_scenario`, `_signature` | 실행 조건과 설계 식별자 |
| `_maxEnergy`, `_energyRegen` | 적용된 능력치 |
| `_totalDamage`, `_dps`, `_energySpent` | 전투 성과 |
| `_peakProjectiles`, `_nodeExecutionCount` | 최대 동시 투사체 수와 노드 지불 횟수 |
| `_elapsedMilliseconds` | 벽시계 실행 시간(성능 관찰용) |
| `_stateHash` | 결정성 비교용 해시 |
| `_stage`, `_duration`, `_remainingTime`, `_result` | 시간제 전투 상태 |
| `_kills`, `_earnedRam`, `_settledRam`, `_spawnedEnemies` | 처치·보상 집계 |

`_settledRam`은 종료·사망 상태에서만 채워진다(진행 중이면 0).

### 래퍼 스크립트

`tools/sim.ps1`은 `Builds/RuneCodePoC-ManualMods/RuneCodePoC.exe`를 `-batchmode -nographics`로 실행하고 로그를 `Logs/rune-sim-player.log`에 남긴 뒤 결과 JSON을 출력한다. 빌드가 없으면 즉시 예외를 던진다. 즉 **CLI는 에디터가 아니라 빌드된 플레이어에서 동작한다.**

```powershell
.\tools\sim.ps1 -Scenario incremental -Ticks 6000 -Stage 1
.\tools\sim.ps1 -Spell C:\path\my-graph.json -Scenario incremental -Ticks 6000 -Stage 2 -Duration 35 -EnergyLevel 1 -CastInterval 20
.\tools\sim.ps1 -Scenario dummy_line -Ticks 600 -Seed 1
```

CLI는 사용자 저장 파일을 읽거나 쓰지 않는다.

## 7.5 RuneCodeBuild — 에디터 도구

`Assets/RuneCode/Editor/RuneCodeBuild.cs` (`Assembly-CSharp-Editor`). 상수 3개로 경로가 고정되어 있다.

```
SCENE_PATH = Assets/Scenes/RuneCodePoC.unity
FONT_PATH  = Assets/RuneCode/Resources/RuneCode/UIFont.asset
BUILD_PATH = Builds/RuneCodePoC-ManualMods/RuneCodePoC.exe
```

### `Rune Code > Prepare PoC Scene`

1. TMP Settings 자산이 없으면 `TMP_PackageResourceImporter.ImportResources` + `AssetDatabase.Refresh`.
2. `GameData.Load()` — 데이터가 깨져 있으면 여기서 멈춘다.
3. `PrepareFont()`.
4. 장면 확보 — 이미 열려 있으면 활성화만(단, **더티 상태면 예외**), 파일이 있으면 추가 로드, 없으면 카메라 + `RuneCodeApp` 두 오브젝트로 새로 만들고 `SerializedObject`로 `_font`를 연결해 저장.

사용자가 편집 중인 장면을 덮어쓰지 않도록 설계된 분기다.

### `PrepareFont()`

| 상황 | 동작 |
|---|---|
| `UIFont.asset`이 이미 있음 | `Resources/RuneCode` 아래 모든 `TextAsset` 본문 + `−–`을 중복 제거해 `TryAddCharacters`로 추가. 아틀라스 텍스처가 자산에 포함돼 있지 않으면 `AddObjectToAsset`으로 붙임 |
| 없음 | 시스템 글꼴 `Malgun Gothic`에서 TMP 자산 생성(없으면 예외). ASCII 95자 + 전체 데이터 문자 + 기호를 추가하고 머티리얼·아틀라스를 서브 자산으로 묶어 저장 |

한국어 문자열이나 룬 이름이 추가돼도 아틀라스가 자동으로 따라오고, 기존 글꼴 자산의 GUID는 유지된다.

### `Rune Code > Build Windows PoC`

`PrepareScene()` → `BuildPlayer`(장면 1개, StandaloneWindows64, `DetailedBuildReport`) → 요약을 `Builds/RuneCodePoC-ManualMods/build-report.txt`에 기록 → 성공이 아니면 예외. 빌드 산출물은 `.gitignore` 규칙을 따른다.
