# 06. 앱 계층과 표현 계층

`Game/`과 `UI/`는 하나의 상태 기계(`RuneCodeApp`)와 그 상태를 그리는 절차적 uGUI(`RuneCodeView` 및 두 커스텀 `MaskableGraphic`)로 구성된다.

```
RuneCodeApp (MonoBehaviour, 상태 기계 + 고정 스텝 구동)
 │
 ├─ RuneCodeView (같은 GameObject, 절차적 화면 전체)
 │    ├─ RuneGraphCanvas   : 그래프 편집 (MaskableGraphic + 포인터 핸들러 7종)
 │    │    └─ RunePaletteDrag : 팔레트 → 캔버스 드래그 배치
 │    ├─ RuneArenaGraphic  : 도크 경기장   (Func<RuneSimulation> 주입)
 │    └─ RuneArenaGraphic  : 미션 경기장   (Func<RuneSimulation> 주입)
 │         └─ RuneMesh      : Rect/Line/Polygon/Ring + 계열·속성 색상
 │
 ├─ PlayerSave / SaveStore  : 진행 저장
 ├─ LocalTelemetry          : 링 버퍼 기록
 └─ RuneSimulation ×2       : _mission, _dock
```

## 6.1 RuneCodeApp 상태 기계

### 화면과 탭

```csharp
enum AppScreen { Title, Workshop, Mission, Result }
```

| 화면 | 진입 | 이탈 |
|---|---|---|
| `Title` | `Awake` | `StartWorkshop()` |
| `Workshop` | `StartWorkshop`, `ReturnWorkshop` | `Deploy()` |
| `Mission` | `Deploy()` | `FinishMission()`(시간 종료·사망), `AbandonMission()` |
| `Result` | 정산 완료 | `ReturnWorkshop()`, `RetryStage()` |

`Workshop`의 탭은 문자열 `_tab`로 관리한다: `editor`(기본) / `bench` / `deploy` / `settings`.

```
      Title ──StartWorkshop──▶ Workshop ──Deploy──▶ Mission
                                  ▲                   │
                                  │                   │ 시간 종료·사망 → FinishMission
                     ReturnWorkshop                   │ 일시정지 → 중단 → AbandonMission
                                  │                   ▼
                                  └────────────────  Result ──RetryStage──▶ Mission
```

### 편집 잠금

```csharp
private bool CanEdit() => _screen == AppScreen.Workshop && _tab == "editor";
```

`AddRune`, `Connect`, `RemoveNode`, `RemoveEdge`, `SetNodeNumber`, `SetNodeText`, `Undo`, `Redo`, `PasteNodes`, `AutoArrange`, `RenameSpell`, `ImportGraph`, `ApplyGraph`가 모두 이 가드를 먼저 통과한다. 전투 중 그래프 변경이 구조적으로 불가능하다.

### 파생 능력치

저장된 성장 단계와 밸런스를 조합한 읽기 전용 프로퍼티다. 미션 생성·컴파일·UI 표시가 전부 이 값을 쓴다.

| 프로퍼티 | 계산 |
|---|---|
| `Capacity` | `Economy.BaseCapacity + CapacityLevel × Economy.CapacityStep` |
| `MaxEnergy` | `Player.MaxEnergy + EnergyLevel × Economy.StatStep` |
| `MaxHp` | `Player.MaxHp + HpLevel × Economy.StatStep` |
| `EnergyRegen` | `Player.EnergyRegen + EnergyLevel × Economy.EnergyRegenStep` |
| `BattleDuration` | `incremental.BaseDuration + DurationLevel × Economy.DurationStep` |
| `EquippedRam` | 장착 마법들의 노드 RAM 합(편집 사본 반영) |

`MaxHp`만 예외다. `GetUpgradeCost` / `Upgrade`에 `hp` 항목이 없어 **현재는 구매할 수 없고**, `_hpLevel`은 v1 저장에서 이전된 값만 가진다. 구매 가능한 성장은 `capacity`, `energy`, `duration` 셋과 벤치의 룬 해금이다.

### Update 구조

```
Update()
 A  그래프 변경 디바운스
      _graphDirty && unscaledTime >= _compileAt
        → Recompile() + SaveGraph() + _view.RefreshGraph()
 B  전역 단축키 (입력 필드 포커스 시 무시)
      Esc        : 터미널 편집 종료 또는 미션 일시정지
      Ctrl+Z / Y : Undo / Redo
      F8         : LocalTelemetry.DumpToConsole
 C  미션 루프 (Mission && !일시정지 && !터미널 편집)
      WASD → 이동 벡터, Space → 대시 요구 플래그 누적
      포인터가 경기장 안이면 그 좌표를 조준으로, 동시에 CastA = true (연속 시전)
      _missionAccumulator = min(누산 + unscaledDeltaTime, 1/60 × MaxFrameSteps)
      while (누산 >= 1/60 && steps < MaxFrameSteps) { Step(...); 누산 -= 1/60; }
      단계 변화 시 _view.Refresh(), 노드 텔레메트리 수집
      Dead/Cleared → FinishMission()
    (아니면 누산기와 대시 플래그를 0으로 리셋)
 D  도크 루프 (Workshop+editor 탭 또는 터미널 편집)
      포인터 → 조준, 자동 시전 또는 좌클릭 → 시전
      R → ResetDock()
      누산에 _dockSpeed(0.5/1/2×) 를 곱해 동일 while 루프
    (아니면 누산기 0)
```

* **디바운스**: `ChangedGraph()`가 `_compileAt = unscaledTime + 0.2f`를 세팅한다. 드래그 중 매 프레임 컴파일하지 않고 손을 뗀 뒤 0.2초에 한 번만 컴파일·저장·갱신한다.
* **누산기 상한**: 프레임이 길게 튀어도 `1/60 × MaxFrameSteps`(5틱)까지만 몰아서 처리한다. 탭을 떠난 사이의 시간은 누산기 리셋으로 버린다.
* **`unscaledDeltaTime`**: `Time.timeScale`에 영향받지 않는다.
* 미션 입력에서 `CastA`는 **포인터가 경기장 안에 있는 동안 계속 true**다. 실제 발사 제한은 시뮬레이션의 쿨다운·에너지가 맡는다.

### 그래프 편집 이력

```
BeginGraphEdit()  → _previousGraph = 현재 사본 (드래그 시작 시)
ChangedGraph()    → 직렬화 비교로 실제 변경이면 _undo 에 push (상한 50), _redo 클리어
                    _graphDirty = true, _compileAt 갱신
Undo()/Redo()     → 스택 교환 + Recompile + SaveGraph + RefreshGraph
```

변경 판정이 `ShareCodec.Serialize` 문자열 비교이므로 좌표만 바뀐 이동도 이력에 남고, 값이 같은 재설정은 남지 않는다.

복사/붙여넣기는 `_copiedNodes` / `_copiedEdges`에 **사본**을 담는다. `core.cast`는 복사 대상에서 제외되고, 붙여넣기는 새 ID를 발급하며 (+30, +30) 오프셋을 주고, 노드·엣지 상한과 장착 RAM 검사를 통과해야 반영된다.

`AutoArrange`는 실행 계열(core/form/flow/action)을 상단 4열 격자에, 속성·수식을 하단 격자에 배치한다.

### ID 발급

```csharp
private string NewId(string prefix) => prefix + "-" + _nextId++ + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
```

순번과 GUID 조각을 합쳐 저장 사이에도 충돌하지 않는 노드·엣지 ID를 만든다.

### 전투 시작과 정산

```
Deploy()
  정산 안 된 미션이 있으면 거부
  SaveGraph() + Recompile()
  컴파일 실패 또는 EquippedRam > Capacity → "editor.invalidEquip"
  _mission = new RuneSimulation(seed 1, isMission: true, MaxHp, MaxEnergy,
                                SelectedStage, BattleDuration, EnergyRegen)
  SetLoadout([spell]) + SetUnlockedElements(해금 속성)
  화면 전환, 누산기·텔레메트리·대시 플래그 초기화
  LocalTelemetry.Record(0, "mission.start", "스테이지:서명")

FinishMission()      (_settled 가드로 1회만)
  cleared = Stage == Cleared
  fragments = SettlementFragments     (사망이면 70% 반올림)
  cleared → RecordStageClear(stage)   (다음 스테이지 해금 + 선택)
  Settle(fragments, cleared)          (첫 완주 시 mod.noise 해금)
  종류별 처치 수 기록 → SaveStore.Write
  결과 문자열 구성, AppScreen.Result, 텔레메트리 기록

AbandonMission()
  EarnedFragments × DeathRetention 반올림으로 사망과 동일 비율 정산
```

미션 시드는 항상 1이다. 스테이지·시간·능력치가 같으면 전투 전개가 재현된다.

### 강화 구매

```
GetUpgradeCost(kind)
  "capacity" / "energy" / "duration" → Economy.GetGrowthCost(kind, 현재 단계)   (상한이면 -1)
  그 외(룬 ID)                        → bench 해금 대상이고 미해금이면 UnlockCost, 아니면 -1

BuyUpgrade(kind)
  Workshop 화면에서만
  비용 < 0 → "bench.maxed"
  Spend 실패 → "bench.insufficient"
  kind 에 점이 있으면 Unlock(룬), 없으면 Upgrade(성장)
  저장 → Recompile() → StartDock(현재 시나리오) → 텔레메트리 → Refresh
```

구매 후 도크를 재시작하는 이유는 능력치 변화(최대 EN, 회복량)를 도크 시뮬레이션에 반영해야 하기 때문이다.

### 레거시 호환 표면

과거 다중 마법 보관함·슬롯·터미널 설계의 공개 메서드가 남아 있고, 전부 단일 마법 규칙을 강제하는 형태로 바뀌었다.

| 메서드 | 현재 동작 |
|---|---|
| `NewSpell` / `NewGraph` / `DuplicateSpell` / `DuplicateGraph` / `DeleteSpell` / `DeleteGraph` / `Unequip` | `"editor.singleSpell"` 안내만 표시 |
| `InteractTerminal` / `OpenTerminalEditor` / `CloseTerminal` / `CloseTerminalEditor` / `ContinueMission` | 빈 구현 |
| `Equip(slot)` | `slot == 0`만 허용 |
| `SelectSpell(id)` | `id == ActiveSpellId`만 허용 |
| `RunIsAutomatic` | 항상 false (수동 조작 전투) |
| `SelectLibrary` / `EquipCurrent` / `StartMission` / `RenameGraph` / `ExportGraph` / `Upgrade` / `UnlockRune` / `SetDockAdaptation` | 신규 메서드로 위임하는 별칭 |

`RuneSimulation.InteractTerminal()`도 항상 false를 반환해 시뮬레이션 쪽에서 같은 규칙을 지킨다.

### 디버그 도구

`-debug` 실행 인수 또는 WebGL `absoluteURL`에 `debug=1`이 있을 때만 `_debugEnabled`가 켜지고, 각 디버그 메서드는 자체적으로 이 플래그를 다시 확인한다. 조각 500 지급, 전체 룬 해금, 미션 무적 토글, 적 소환.

## 6.2 RuneCodeView — 절차적 uGUI

`UI/RuneCodeView.cs` (818줄, 프로젝트 최대 파일). 프리팹 없이 모든 위젯을 코드로 만든다.

### 위젯 팩토리

| 메서드 | 생성물 |
|---|---|
| `Rect` | 좌상단 기준 `RectTransform`(앵커·피봇 (0,1)) |
| `Panel` | `Image` 배경, 기본적으로 `raycastTarget = false` |
| `Text` | `TextMeshProUGUI` (글꼴·크기·색·스타일) |
| `Button` | 패널 + `Image`(레이캐스트 on) + `Button` + 중앙 정렬 라벨, 색상 블록 설정 |
| `Input` | 패널 + `TMP_InputField` + 마스킹된 뷰포트 + 본문/플레이스홀더 라벨 |
| `ScrollList` | `ScrollRect` + `Mask` 뷰포트 + `VerticalLayoutGroup` + `ContentSizeFitter` 콘텐츠 |
| `Layout` | 목록 항목의 고정 높이(`LayoutElement`) |
| `OpenModal` / `CloseModal` | 전면 차단 오버레이 + 중앙 카드, `_isModalOpen`으로 경기장 입력 차단 |

좌표계는 전부 1280×720 논리 해상도의 좌상단 기준 픽셀이다. `CanvasScaler`가 `ScaleWithScreenSize` + `Expand`로 실제 해상도에 맞춘다.

### 페이지 빌더

| 메서드 | 화면 |
|---|---|
| `BuildTitle` | 타이틀 — 소개 문구 + `시작` |
| `BuildHeader` | 작업실 상단 — 탭 4개 + 보유 RAM / 사용량·용량 |
| `BuildEditor` | 룬 팔레트(검색·계열 필터) + 그래프 뷰포트 + 인스펙터 + 하단 도크 + 튜토리얼 줄 |
| `BuildInspector` | 선택 노드의 파라미터 편집 + 형태의 실제 사거리·반경·속도 + 검증 메시지 목록 |
| `BuildDock` | 시험 도크 — 시나리오 순환, 시험, 리셋, 자동 시전, 적응, 배속, 지표, 적응 표시 |
| `BuildBench` | 강화 카드 3장(용량·에너지·전투시간) + 룬 해금 목록 |
| `BuildDeploy` | 스테이지 선택 ±, 제한시간, 현재 마법 요약, 출격 |
| `BuildSettings` | 화면 흔들림·히트스톱 토글, 진행 초기화(확인 모달), 디버그 패널 |
| `BuildMission` | 경기장 + HP/EN 바 + 스테이지·처치·조각 + 남은 시간 + 마법 쿨다운·비용 + 일시정지 |
| `BuildResult` | 정산 결과 + 최고 스테이지 + `마법 강화` / `같은 스테이지 재도전` |
| `OpenPause` / `OpenRename` / `OpenShare` / `OpenQuickPalette` / `OpenTutorialHelp` | 모달 |
| `BuildDebug` | 디버그 버튼 4개(설정 화면과 미션 모달에서 재사용) |

`Refresh()`는 `_page`를 파괴하고 처음부터 다시 쌓는다. 그래서 맨 앞에서 모든 캐시 참조를 null로 비운다. `RefreshGraph()`는 페이지를 건드리지 않고 노드 라벨·지표·제목·인스펙터만 갱신하는 경량 경로다. 둘의 구분이 지켜지지 않으면 편집 중 포커스가 날아가므로, 그래프 편집 계열 호출은 전부 `RefreshGraph`를 쓴다.

### Update의 역할

1. 상태 메시지 갱신.
2. 도크 지표 문자열(피해·DPS·에너지·최대 개체) 갱신. 도크 인스턴스가 교체되면 카운터를 리셋한다.
3. **노드 실행 하이라이트** — `Dock.NodeExecutionCount` 증가분만큼 `NodeEvents` 뒤쪽을 읽어 12틱 이내 이벤트의 노드를 `RuneGraphCanvas.Highlight`로 넘긴다. 같은 틱에 여러 번 실행된 노드도 누락되지 않는다.
4. 튜토리얼 문구와 적응 표시 갱신.
5. 미션 HUD 갱신(`UpdateMissionHud`).
6. 그래프 단축키 — Del/Backspace 삭제, Ctrl+C/V 복사·붙여넣기, Ctrl+D 복제, Ctrl+S 저장, `1` 도크 시전. 모달이 열려 있거나 입력 필드에 포커스가 있으면 전부 무시한다.

### 인스펙터의 두 가지 수치 표시

`FindAction(spell.Root, nodeId)`로 **컴파일된 명령**을 찾아 보여준다. 즉 인스펙터와 툴팁의 피해·에너지·사거리는 룬 정의의 기본값이 아니라 **부착된 수식·속성이 모두 반영된 실제 값**이다. 컴파일 실패 상태에서는 룬 정의 기본값으로 떨어진다.

수식 노드는 숫자 입력과 함께 `−`/`+` 버튼이 붙어 `ParameterDefinition.Step`만큼 조절하며 `Min`/`Max`로 클램프한다.

검증 메시지는 각 항목이 버튼이고, 누르면 `RuneGraphCanvas.FocusNode(nodeId)`로 해당 노드를 화면 중앙에 가져오며 선택한다.

### 튜토리얼

`UpdateTutorial`이 그래프 상태를 직접 읽어 3단계를 판정한다 — `form.bolt`가 exec로 연결됨 → `elem.fire`가 어딘가로 연결됨 → 도크에서 실제 실행이 발생함. 세 조건이 다 차면 `AdvanceTutorial(3)`으로 저장한다. 별도 튜토리얼 상태 기계를 두지 않고 그래프와 실행 카운터에서 역산하는 방식이다.

## 6.3 RuneGraphCanvas — 그래프 편집

`UI/RuneGraphCanvas.cs`. `MaskableGraphic`을 상속해 노드·엣지·포트를 단일 메시로 그리고, 노드 텍스트만 자식 `TextMeshProUGUI`로 둔다(노드 ID → 라벨 딕셔너리로 재사용·정리).

### 좌표계

```
그래프 좌표(node.X, node.Y, y 아래 방향)
   ↓  × _zoom, + _pan,  y 부호 반전
RectTransform 로컬 좌표(좌상단 기준)
   ↓  RectTransformUtility.ScreenPointToLocalPointInRectangle
화면 좌표
```

`_zoom`은 0.45~1.5, `FitGraph()`가 첫 표시 때 모든 노드가 들어오도록 초기 배율·이동을 맞춘다. `OnScroll`은 마우스 지점을 고정점으로 삼아 확대한다.

노드 사각형 높이는 `39 + max(입력 포트 수, 출력 포트 수) × 23`으로 **룬 정의에서 파생**된다. 포트 위치도 같은 규칙으로 계산되므로 룬에 포트를 추가하면 레이아웃이 자동으로 따라온다.

### 입력 매핑

| 입력 | 동작 |
|---|---|
| 좌클릭 포트 | 연결 시작. 드래그 중 호환 포트는 녹색, 비호환은 적색으로 표시 |
| 좌클릭 노드 | 선택(Shift로 토글/추가) + 드래그 이동. `BeginGraphEdit()`로 이력 기준 저장 |
| 좌클릭 빈 곳 | 영역 선택 시작 / 더블클릭이면 빠른 검색 모달 |
| 중간 버튼 드래그 | 화면 이동 |
| 우클릭 | 엣지 근처(8px)면 엣지 삭제, 아니면 화면 이동 |
| 휠 | 확대·축소 |
| 포인터 이동 | 툴팁 대상 전달 |
| 드롭(팔레트) | `RunePaletteDrag` → `TryGetGraphPoint` → `PlaceRune` |

`OnPointerUp`에서 연결을 확정한다. 출력에서 시작했으면 `(source → target)`, 입력에서 시작했으면 방향을 뒤집어 `_app.Connect`를 호출한다.

### 배치 보조

* `SuggestPlacement(runeId)` — 팔레트 클릭 배치용. 속성·수식이면 기존 형태 노드 근처를 우선하고, 결과 좌표를 보이는 영역 안으로 클램프한다.
* `PlaceRune(runeId, position)` — 배치 후 속성·수식이면 **175px 안의 가장 가까운 형태 노드에 자동으로 `mod` 연결**을 시도한다. 연결 적합성 판정은 `_app.Connect`(즉 컴파일러)가 한다.

### 실행 하이라이트

`Highlight(nodeId)`가 `_executedUntil[nodeId] = unscaledTime + 0.2f`를 기록하고, `OnPopulateMesh`에서 해당 노드의 테두리·배경을 밝게 그린다. `Update`는 하이라이트가 남아 있을 때만 메시를 더럽혀 불필요한 재구성을 피한다.

## 6.4 RuneArenaGraphic — 경기장 렌더러

`Game/RuneArenaGraphic.cs`. 시뮬레이션에 **직접 참조를 갖지 않고** `Func<RuneSimulation>`을 주입받는다. 도크/미션 두 인스턴스가 같은 컴포넌트 타입을 공유하고, 시뮬레이션이 교체돼도 람다가 최신 인스턴스를 돌려준다.

```csharp
void Initialize(Func<RuneSimulation> simulation, TMP_FontAsset font,
                Func<bool> screenShake = null, Func<bool> hitStop = null)
```

### 좌표 변환

월드는 `1280 × 704`(40×22 타일 × 32px) 고정이다. `CalculateScale()`이 가로세로 비율을 유지하는 배율과 좌상단 원점을 구하고, 화면 흔들림이 켜져 있으면 원점에 사인/코사인 오프셋을 더한다. `Point(SimVector)`가 시뮬레이션 좌표를 로컬 좌표로 바꾸고(`y` 부호 반전), `TryGetPointer(screen, out SimVector)`가 역변환 + 경계 판정을 한다.

### 그리기 순서 (`OnPopulateMesh`)

```
배경 → 타일(벽/바닥, 도크에서는 문·터미널 마커) → (미션) 유입 흐름 안내
→ 마법 개체 → 적 탄환·장판 → RAM 오브 → 적 → 플레이어
```

형태·속성에 따라 도형이 달라진다. 존·버스트는 채움 다각형 + 링(냉기 6각, 화염 12분할), 볼트는 꼬리선 + 방향 회전 다각형(화염 3각, 냉기 6각, 전격 4각, 무속성 16각). 적은 종류별 변 수와 색(scout 3각, sentry/hunter 4각, aegis 6각, relay 8각, 보스 6각)으로 구분하고 체력 바, 공격 예고 링, 릴레이 오라, 이지스 방패 호, 보스 패치 링, 화염·냉기·EMP 상태 아이콘, **적응 내성 표시**를 덧붙인다. 내성 표시는 적이 마지막으로 받은 속성·형태의 학습값이 `Adaptation.ResistanceThreshold`를 넘었을 때만 켜진다.

### 피해 숫자

`Update`에서 `sim.DamageNumbers`를 읽어 `TextMeshProUGUI` 라벨을 **풀링**해 재사용한다(최대 64개, 40틱 이내). 경과 틱에 따라 위로 올리고 알파를 낮추며, 남는 라벨은 비활성화한다.

### 흔들림과 히트스톱

누적 피해가 증가하거나 플레이어 HP가 감소한 프레임에 `_shakeUntil`(0.08초)과 `_hitStopUntil`(0.025초)을 세팅한다. 히트스톱 구간에는 **메시 갱신을 건너뛴다**(시뮬레이션은 계속 돈다 — 순수 표현 효과다). 두 효과 모두 저장 설정 람다가 false면 적용되지 않는다. 관찰 대상 시뮬레이션이 교체되면 비교 기준값을 다시 잡아 리셋 순간에 가짜 흔들림이 나지 않게 한다.

## 6.5 RuneMesh / RunePaletteDrag

`UI/RuneMesh.cs`는 `internal static` 헬퍼다. `VertexHelper`에 사각형·선분·다각형·링을 추가하는 4개 메서드와, 룬 계열 색(`CategoryColor`)·피해 속성 색(`ElementColor`)을 돌려주는 2개 메서드로 구성된다. 그래프 캔버스와 경기장이 같은 프리미티브를 공유하므로 스프라이트 자산이 필요 없다.

`UI/RunePaletteDrag.cs`는 30줄짜리 드래그 핸들러다. `OnBeginDrag`/`OnDrag`는 Unity 이벤트 시스템이 드래그를 인식하도록 하는 빈 구현이고, 실제 로직은 `OnEndDrag` 하나다 — 해금된 룬이고 드롭 지점이 그래프 안이면 `RuneGraphCanvas.PlaceRune`을 호출한다.
