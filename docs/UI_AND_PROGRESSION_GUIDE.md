# UI·성장·룬 해금 기능 편집 안내

이 문서는 UI 스크립트와 공용 UI Prefab의 위치, 화면 배치 수정 방법, 성장·룬 해금 기능의 데이터 흐름을 안내한다. UI 표현과 배치는 `Assets/RuneCode/UI`에서 관리하고, 세이브·비용·전투 상태는 기존 책임 폴더에 둔다.

## 폴더 구조와 스크립트 역할

| 위치 | 스크립트 | 역할 |
|---|---|---|
| `UI/Shared` | `AdaptationText.cs` | 적응 학습값을 HUD 막대 또는 요약 문구로 변환한다. |
| `UI/Shared` | `RuneArenaGraphic.cs` | 시뮬레이션 상태를 도크·전투 경기장 UI로 그린다. |
| `UI/Shared` | `RuneMesh.cs` | 노드, 연결선, 원형 효과 등 커스텀 UI 메시를 만든다. |
| `UI/Shared` | `UiFactory.cs` | 공용 Prefab을 사용해 좌표 기반 uGUI 요소를 만들고 목록·입력 요소를 구성한다. |
| `UI/Shared` | `UiRow.cs` | 공용 목록 행의 문구, 강조색, 높이, 클릭 동작을 설정한다. |
| `UI/Shared` | `UiTheme.cs` | 공통 기준 해상도와 UI 색상 팔레트를 보관한다. |
| `UI/Title` | `TitleScreen.cs` | 시작 버튼 입력을 세션의 작업실 전환 요청으로 전달한다. |
| `UI/Workshop` | `BenchPanel.cs` | 강화 카드와 룬 해금 목록을 갱신하고 구매 요청을 보낸다. |
| `UI/Workshop` | `DeployPanel.cs` | 스테이지를 선택하고 출격 전 검증을 거쳐 미션을 시작한다. |
| `UI/Workshop` | `DockPanel.cs` | 작업실 시험 도크의 버튼, 입력, 적응 정보와 지표를 표시한다. 시뮬레이션 실행은 `DockRun`이 담당한다. |
| `UI/Workshop` | `SettingsPanel.cs` | 화면 피드백 설정, 저장 초기화 확인, 디버그 도구를 운영한다. |
| `UI/Workshop` | `UpgradeCardView.cs` | 강화 카드의 제목·수치·설명·구매 버튼을 표시한다. |
| `UI/Workshop` | `WorkshopScreen.cs` | 작업실 탭·헤더·상태줄을 운영하고 편집 호스트 역할을 제공한다. |
| `UI/Mission` | `MissionHud.cs` | 체력·에너지·진행도·남은 시간과 일시정지·디버그 조작을 표시한다. |
| `UI/Mission` | `MissionScreen.cs` | 미션 진입, 조준 입력, HUD 갱신, 결과 전환을 연결한다. |
| `UI/Mission` | `PausePanel.cs` | 계속하기, 피드백 설정, 후퇴 정산을 제공하는 일시정지 모달이다. |
| `UI/Mission` | `ResultPanel.cs` | 전투 정산과 재도전·작업실 복귀를 표시한다. |
| `UI/SpellEditor` | `RuneGraphCanvas.cs` | 마법 그래프 노드와 연결을 그리고 선택·드래그 상호작용을 처리한다. |
| `UI/SpellEditor` | `RunePaletteDrag.cs` | 팔레트의 룬을 그래프에 드래그하는 입력을 처리한다. |
| `UI/SpellEditor` | `SpellEditorPanel.cs` | 팔레트·그래프·인스펙터·모달을 구성하고 편집 명령을 편집 세션에 전달한다. |
| `UI/SpellEditor` | `SpellParameterRow.cs` | 노드 인스펙터의 숫자·문자·선택·Spell Call 입력 행을 표시한다. |
| `UI/Editor/Layouts` | `LayoutUtility.cs` | 공용 글꼴·Prefab 준비, 직렬화 필드 연결, 씬 저장을 돕는다. |
| `UI/Editor/Layouts` | `TitleLayout.cs` | Title 씬의 오브젝트 배치와 `TitleScreen` 참조를 만든다. |
| `UI/Editor/Layouts` | `WorkshopLayout.cs` | 작업실 헤더·탭·패널 배치와 화면 참조를 만든다. |
| `UI/Editor/Layouts` | `SpellEditorLayout.cs` | 비주얼 편집기와 내부 모달·목록을 만들고 참조를 연결한다. |
| `UI/Editor/Layouts` | `MissionLayout.cs` | 경기장·HUD·일시정지·결과 UI 배치와 참조를 만든다. |

### UI 외부의 상태·데이터 스크립트

UI에서 요청을 전달하는 대상으로, UI 폴더로 옮기지 않는다. 세이브와 게임 규칙을 소유하는 위치를 유지해야 화면 표시 코드와 진행 상태의 변경 이유가 분리된다.

| 위치 | 스크립트 | 역할 |
|---|---|---|
| `Session/RuneCodeSession.cs` | 비용 조회, 구매 가능 여부, 재화 지불, 강화·해금 적용, 저장과 편집 세션 갱신을 관리한다. |
| `Session/SessionSpellPolicy.cs` | 세이브의 해금·RAM·에너지 값을 바탕으로 편집 가능 여부와 컴파일 문맥을 제공한다. |
| `Session/SessionSpellStorage.cs` | 마법 편집기의 보관함 읽기·쓰기를 현재 `PlayerSave`에 연결한다. |
| `Save/PlayerSave.cs` | 강화 단계, 보유 재화, 해금 룬 ID 등 저장 상태를 보관하고 변경한다. |
| `Core/Data/BalanceData.cs` | 강화 비용 증가율, 기본 비용, 성장 상한과 효과 증가량을 제공한다. |
| `Core/Mission/MissionData.cs` | 인크리멘탈 기본 전투 시간 등 미션 기본값을 읽는다. |
| `Core/Graph/RuneData.cs` | CSV에서 룬 정의를 읽고 해금 방식·비용과 연결 데이터의 유효성을 검사한다. |
| `Core/Data/GameData.cs` | 공용 밸런스·현지화·룬 등 리소스 데이터를 로드하고 조회한다. |
| `Features/Workshop/DockRun.cs` | 작업실 시험 도크 시뮬레이션을 실행한다. `DockPanel`은 이 상태를 보여준다. |
| `Features/Mission/MissionRun.cs` | 실제 미션 진행과 전투 상태를 관리한다. `MissionScreen`과 HUD가 결과를 표시한다. |
| `Boot/RuneCodeApp.cs`, `Boot/AppScreen.cs` | 앱 시작과 화면 전환 수명주기를 관리한다. |

## 공용 Prefab 편집

| Prefab | 쓰임새 | 편집 기준 |
|---|---|---|
| `Assets/RuneCode/Prefabs/UI/UiPanel.prefab` | 각 화면의 배경·카드·모달 바탕 | 이미지 컴포넌트와 공통 장식은 Prefab에서 편집한다. 패널 크기와 위치는 화면 Layout 빌더가 정한다. |
| `Assets/RuneCode/Prefabs/UI/UiButton.prefab` | 화면의 공통 버튼과 목록 행의 버튼 | 자식 구조·공통 장식은 Prefab에서 편집한다. 버튼 문구·강조색·크기는 호출하는 코드가 설정한다. |
| `Assets/RuneCode/Prefabs/UI/UiRow.prefab` | 강화·해금 및 편집기 반복 목록의 공통 한 줄 | 행의 배경·라벨 구조를 한 곳에서 편집한다. 행 높이·문구·색·클릭 동작은 `UiRow.Configure`가 런타임에 설정한다. |
| `Assets/RuneCode/Prefabs/UI/UpgradeCard.prefab` | 벤치의 용량·에너지·전투 시간 강화 카드 3장 | 카드 내부 요소를 한 번 편집하면 세 카드가 같은 디자인을 사용한다. 각 카드 값과 구매 동작은 `UpgradeCardView` 및 `BenchPanel`이 채운다. |
| `Assets/RuneCode/Prefabs/UI/SpellParameterRow.prefab` | 그래프 노드 인스펙터의 입력 행 | 숫자·텍스트·선택 컨트롤이 포함된 복합 행이다. 기존 Prefab을 보존하며 없을 때만 `SpellEditorLayout`이 기본 구조를 생성한다. |

`UiFactory`는 패널과 버튼의 RectTransform, 이미지 색, 텍스트, 버튼 색을 화면별 입력값으로 설정한다. 따라서 공통 배경 이미지·테두리·자식 구조는 Prefab에서 편집하고, 호출 때마다 덮어써지는 색·문구·크기 값은 `UiTheme` 또는 해당 Layout/뷰 로직에서 바꾼다. 모든 화면이 같이 바뀌어야 할 색은 `UiTheme`을 우선 수정한다.

## 화면 배치와 새 UI 추가

씬 UI는 Editor의 Layout 빌더가 만든다. 각 요소의 좌표·크기·부모 관계는 해당 빌더의 `BuildScene`, `Build` 또는 패널 구성 메서드에 있다. 저장된 씬만 직접 고치면 다음 생성 시 변경이 사라질 수 있으므로 Layout 빌더를 수정한다.

1. 화면을 고른다: Title은 `TitleLayout`, 작업실은 `WorkshopLayout`, 미션은 `MissionLayout`을 수정한다. 그래프 편집기 내부는 `SpellEditorLayout`을 수정한다.
2. 공통 모양은 `UiPanel`, `UiButton`, `UiRow`, `UpgradeCard` Prefab에서 편집한다. 화면별 배치와 여백은 Layout 빌더에서 조정한다.
3. 기존 뷰 컴포넌트가 새 오브젝트를 사용해야 하면 Layout 빌더에서 만들고 `LayoutUtility.SetReference` 또는 `SetReferences`로 `[SerializeField]` 참조를 연결한다. 배열 순서는 뷰 코드가 읽는 순서와 맞춘다.
4. 새 UI 동작은 화면 뷰 스크립트에 연결하고, 비용·저장·전투 규칙 변경은 해당 Session/Core/Save/Features 책임 코드에 요청한다.
5. Unity에서 `Rune Code > Build Scenes`를 실행한다. 이 메뉴는 Boot 준비 후 Title·Workshop·Mission 씬을 다시 만들고 빌드 씬 목록을 갱신한다. 생성된 Prefab은 없을 때 기본 형태를 만들며, `SpellParameterRow.prefab`은 기존 자산을 보존한다.

새 화면을 추가할 때는 `UI/{화면}`에 뷰 스크립트, `UI/Editor/Layouts`에 생성기를 둔다. 동일한 디자인의 반복 항목이면 먼저 `UiRow` 또는 기존 공용 Prefab을 재사용하고, 다른 수명·편집 책임을 가진 복합 구성일 때만 별도 Prefab/뷰 컴포넌트를 만든다.

## 강화 항목 추가·수정

현재 화면은 `capacity`, `energy`, `duration` 세 종류를 보여준다. 값을 바꾸는 경우에는 다음 역할을 함께 확인한다.

1. `UI/Workshop/BenchPanel.cs`의 `_upgradeTypes`와 `RefreshCard`에서 표시 순서와 각 항목의 현재 값 계산을 수정한다. 새 값이 필요하면 `RuneCodeSession`의 읽기 전용 프로퍼티를 사용한다.
2. `Session/RuneCodeSession.cs`에서 `GetUpgradeCost`와 `BuyUpgrade`가 새 종류를 인식하는지 확인한다. 여기서 가격 검증, 재화 지불과 저장 흐름을 유지한다.
3. `Save/PlayerSave.cs`의 단계 필드·`Upgrade`·검증·저장 호환성을 함께 수정한다. 저장 구조에 새 필드를 추가하면 버전 이전 로직과 기존 세이브 검증도 검토한다.
4. `Core/Data/BalanceData.cs`와 `Resources/RuneCode/balance.json`에서 새 비용·상한·증가량을 정의한다. 기존 세 종류의 비용 계산은 `EconomyBalance.GetGrowthCost`가 맡는다. 전투 시간의 기본값은 `Core/Mission/MissionData.cs`가 읽는 `Resources/RuneCode/incremental.json`에서 관리한다.
5. `Resources/RuneCode/strings.ko.json`의 `ui.{종류}`, `ui.{종류}Description` 등 표시 키를 추가하거나 수정한다. UI 문구를 코드에 직접 넣지 않는다.
6. 모양만 바꾸려면 `UpgradeCard.prefab`을 편집하고, 카드 내부 요소/값 바인딩을 바꾸려면 `UpgradeCardView.cs`와 `BenchPanel.cs`를 수정한다.

기존 강화 효과를 조정하는 작업은 저장 단계와 게임 규칙의 값 계산이 함께 맞는지 확인한다. UI 카드만 수정하면 실제 강화 효과나 가격은 바뀌지 않는다.

## 룬 해금 추가·수정

룬 정의의 원본은 `Assets/RuneCode/Resources/RuneCode/Tables/runes.csv`다. `unlockType`은 `start`, `bench`, `reward` 중 하나를 사용하며, 벤치에서 구매하는 항목은 `bench`와 양수 `unlockCost`를 설정한다. 룬의 포트·파라미터·옵션·스탯·태그는 각각 `rune_ports.csv`, `rune_params.csv`, `rune_param_options.csv`, `rune_stats.csv`, `rune_tags.csv`에서 정의한다.

`BenchPanel`은 `start`가 아닌 룬을 목록에 표시하고, `RuneCodeSession`은 `bench` 해금만 가격을 반환해 구매를 허용한다. 보상형(`reward`) 해금은 미션 보상 흐름에서 저장에 추가해야 하며, 벤치 버튼을 추가하는 것으로는 보상 해금이 되지 않는다. CSV 편집 규칙과 열 설명은 [데이터 테이블 규칙](DATA_TABLES.md)을 따른다.

## 연결 흐름

```text
UI/Workshop/BenchPanel
  ├─ UpgradeCardView → 카드 표시 및 클릭 전달
  └─ RuneCodeSession → 비용 확인·재화 지불·강화/해금·저장
       ├─ PlayerSave → 진행 상태
       ├─ BalanceData → 강화 비용과 수치
       └─ RuneData/GameData → 룬 정의와 표시 데이터
```

새 UI 스크립트나 필드를 추가한 뒤에는 Layout 빌더에서 오브젝트·Prefab을 만들고 참조를 연결한다. 기능 확인은 Unity 컴파일, 씬 재생성, Inspector의 Missing Script/Missing Reference 확인 순으로 진행한다. 코드만 읽고 실제 씬 연결이 완료됐다고 판단하지 않는다.
