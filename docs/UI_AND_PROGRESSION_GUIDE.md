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
| `UI/Title` | `TitleScreen.cs` | 일반/디버그 시작 버튼 입력을 Presenter로 전달한다. |
| `UI/Workshop` | `DeployPanel.cs` | 스테이지를 선택하고 출격 전 검증을 거쳐 미션을 시작한다. |
| `UI/Workshop` | `DockPanel.cs` | 작업실 시험 도크의 버튼, 입력, 적응 정보와 지표를 표시한다. 시뮬레이션 실행은 `DockRun`이 담당한다. |
| `UI/Workshop` | `SettingsPanel.cs` | 화면 피드백 설정, 저장 초기화 확인, 디버그 도구를 운영한다. |
| `UI/Workshop` | `UpgradeTreePanel.cs` | ScriptableObject 노드 연결망, 포인터 설명 패널, 비용·선행 조건·구매 상태를 갱신한다. |
| `UI/Workshop` | `UpgradeTreeNodeView.cs` | 아이콘 전용 사각형 노드의 잠금·완료 표시, 클릭 구매와 포인터 오버를 전달한다. |
| `UI/Workshop` | `UpgradeTreeNodeIconGraphic.cs` | 효과 종류와 룬 분류에 맞는 벡터 아이콘을 uGUI 메시로 그린다. |
| `UI/Workshop` | `UpgradeTreeCanvasInput.cs` | 우클릭 드래그로 연결망을 이동하고 마우스 휠로 포인터 중심 확대·축소를 수행한다. |
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
| `Progression/UpgradeTreeDefinition.cs` | 트리 노드의 효과·비용·좌표·Sprite·선행 조건을 ScriptableObject 형식으로 정의하고 구조를 검증한다. |
| `Editor/UpgradeTreeAssetBuilder.cs` | 최초 트리 자산의 기본 노드를 생성하고, 기존 자산을 배치/비배치 목록으로 한 번 이전한다. 같은 버전 자산의 편집값은 보존한다. |
| `Core/Data/BalanceData.cs` | 전력·체력·이동·경제 등 게임 기본 밸런스를 제공한다. 트리의 구매 비용과 레벨 증가량은 ScriptableObject가 원본이다. |
| `Core/Mission/MissionData.cs` | 인크리멘탈 기본 전투 시간 등 미션 기본값을 읽는다. |
| `Core/Graph/RuneData.cs` | `runes.json`에서 룬 정의를 읽고 시작/트리 해금 분류와 연결 데이터를 검사한다. |
| `Core/Data/GameData.cs` | 공용 밸런스·현지화·룬·마법 템플릿 리소스를 로드하고 조회한다. |
| `Features/Workshop/DockRun.cs` | 작업실 시험 도크 시뮬레이션을 실행한다. `DockPanel`은 이 상태를 보여준다. |
| `Features/Mission/MissionRun.cs` | 실제 미션 진행과 전투 상태를 관리한다. `MissionScreen`과 HUD가 결과를 표시한다. |
| `Core/Simulation/RuneSimulation.cs` | 미션·시험 도크에 전달된 트리 능력치로 체력·전력·이동·마법 피해를 적용하고, 처치 스크랩 보너스를 오브에 반영한다. |
| `Boot/RuneCodeApp.cs`, `Boot/AppScreen.cs` | 앱 시작과 화면 전환 수명주기를 관리한다. |

## 공용 Prefab 편집

| Prefab | 쓰임새 | 편집 기준 |
|---|---|---|
| `Assets/RuneCode/Prefabs/UI/UiPanel.prefab` | 각 화면의 배경·카드·모달 바탕 | 이미지 컴포넌트와 공통 장식은 Prefab에서 편집한다. 패널 크기와 위치는 화면 Layout 빌더가 정한다. |
| `Assets/RuneCode/Prefabs/UI/UiButton.prefab` | 화면의 공통 버튼과 목록 행의 버튼 | 자식 구조·공통 장식은 Prefab에서 편집한다. 버튼 문구·강조색·크기는 호출하는 코드가 설정한다. |
| `Assets/RuneCode/Prefabs/UI/UiRow.prefab` | 강화·해금 및 편집기 반복 목록의 공통 한 줄 | 행의 배경·라벨 구조를 한 곳에서 편집한다. 행 높이·문구·색·클릭 동작은 `UiRow.Configure`가 런타임에 설정한다. |
| `Assets/RuneCode/Prefabs/UI/UpgradeTreeNode.prefab` | 트리의 스탯·룬 해금 노드 | 공통 사각형 크기·윤곽·아이콘 배치를 한 곳에서 편집한다. 이름·설명·레벨·비용은 노드에 마우스를 올렸을 때 정보 패널에 표시된다. |
| `Assets/RuneCode/Prefabs/UI/SpellParameterRow.prefab` | 그래프 노드 인스펙터의 입력 행 | 숫자·텍스트·선택 컨트롤이 포함된 복합 행이다. 기존 Prefab을 보존하며 없을 때만 `SpellEditorLayout`이 기본 구조를 생성한다. |

`UiFactory`는 패널과 버튼의 RectTransform, 이미지 색, 텍스트, 버튼 색을 화면별 입력값으로 설정한다. 따라서 공통 배경 이미지·테두리·자식 구조는 Prefab에서 편집하고, 호출 때마다 덮어써지는 색·문구·크기 값은 `UiTheme` 또는 해당 Layout/뷰 로직에서 바꾼다. 모든 화면이 같이 바뀌어야 할 색은 `UiTheme`을 우선 수정한다.

## 화면 배치와 새 UI 추가

씬 UI는 Editor의 Layout 빌더가 만든다. 각 요소의 좌표·크기·부모 관계는 해당 빌더의 `BuildScene`, `Build` 또는 패널 구성 메서드에 있다. 저장된 씬만 직접 고치면 다음 생성 시 변경이 사라질 수 있으므로 Layout 빌더를 수정한다.

1. 화면을 고른다: Title은 `TitleLayout`, 작업실은 `WorkshopLayout`, 미션은 `MissionLayout`을 수정한다. 그래프 편집기 내부는 `SpellEditorLayout`을 수정한다.
2. 공통 모양은 `UiPanel`, `UiButton`, `UiRow` Prefab에서 편집한다. 화면별 배치와 여백은 Layout 빌더에서 조정한다.
3. 기존 뷰 컴포넌트가 새 오브젝트를 사용해야 하면 Layout 빌더에서 만들고 `LayoutUtility.SetReference` 또는 `SetReferences`로 `[SerializeField]` 참조를 연결한다. 배열 순서는 뷰 코드가 읽는 순서와 맞춘다.
4. 새 UI 동작은 화면 뷰 스크립트에 연결하고, 비용·저장·전투 규칙 변경은 해당 Session/Core/Save/Features 책임 코드에 요청한다.
5. Unity에서 `Rune Code > Build Scenes`를 실행한다. 이 메뉴는 Boot 준비 후 Title·Workshop·Mission 씬을 다시 만들고 빌드 씬 목록을 갱신한다. 생성된 Prefab은 없을 때 기본 형태를 만들며, `SpellParameterRow.prefab`은 기존 자산을 보존한다.

새 화면을 추가할 때는 `UI/{화면}`에 뷰 스크립트, `UI/Editor/Layouts`에 생성기를 둔다. 동일한 디자인의 반복 항목이면 먼저 `UiRow` 또는 기존 공용 Prefab을 재사용하고, 다른 수명·편집 책임을 가진 복합 구성일 때만 별도 Prefab/뷰 컴포넌트를 만든다.

## 강화 트리 노드 추가·수정

기반 강화와 전투시간 강화 구매 화면은 제거했다. 강화와 룬 해금은 `Assets/RuneCode/Resources/RuneCode/UpgradeTree.asset`에서 함께 관리한다. 시작 시 사용할 수 있는 기본 룬은 구형·사각·부채꼴·발사와 일반 속성(`element.neutral`)이다. 일반 속성은 트리 구매 대상이 아니며, 나머지 플레이어 룬은 각각 하나의 `RuneUnlock` 노드 데이터로 관리한다. 내부 문법 토큰은 트리 구매 대상이 아니다.

SO의 **배치할 노드**(`_nodes`)에는 현재 지정된 12개만 있다. 스탯 8개(메인보드, RAM, 파워, CPU, GPU, 스크랩, HP, 이동속도)와 해금 4개(화염, 얼음, 폭발, 잔류)다. **배치하지 않을 노드**(`_unplacedNodes`)에는 나머지 룬 해금 17개가 있다. 이 목록의 노드는 선행 노드가 없고 트리 화면에 그려지거나 일반 모드에서 구매되지 않는다. 디버그 모드에서는 기존처럼 양쪽 목록의 룬을 모두 사용할 수 있다.

`UpgradeTree.asset`의 노드에서 비용과 증가량은 `_levels`의 각 원소에 입력한다. 스탯 강화는 1~3레벨, 룬 해금은 1레벨만 사용한다. `_cost`는 스크랩 가격이고 `_amount`는 해당 레벨의 증가량이다. `_prerequisites`는 배치 목록의 메인보드만 비우고 다른 배치 노드는 선행 노드 하나와 요구 레벨 1로 설정한다. 비배치 목록의 노드는 선행 조건을 비워 둔다. `_requiredStage`는 현재 모든 노드에서 1이며 스테이지 잠금에는 사용하지 않는다. `_position`은 배치 시 사용할 트리 캔버스의 좌상단 기준 좌표이고 `_icon`은 교체할 Sprite 참조다.

새 룬을 추가할 때는 `Assets/RuneCode/Resources/RuneCode/tables/runes.json`에서 시작 형태 4개와 일반 속성 `element.neutral`을 `start`, 그 외 플레이어 룬은 `tree` 또는 `bench`로 설정한다. Core/Internal 구현 룬도 `start`로 두되 노드를 만들지 않는다. 새 `tree`/`bench` 룬의 `RuneUnlock` 노드를 **배치하지 않을 노드**에 추가하고 `_runeId`와 비용을 지정한다. `UpgradeTreeAssetBuilder.EnsureAsset`은 SO에 없는 트리 해금 룬을 비용·기본 아이콘이 설정된 비배치 노드로 보충한다. 실제 트리에 열 때는 그 노드를 **배치할 노드**로 옮기고 `_position`과 배치된 부모 노드 하나의 ID(요구 레벨 1)를 설정한다. 반대로 배치를 해제할 때는 비배치 목록으로 옮기고 `_prerequisites`를 비운다. `UpgradeTreeDefinition.Validate`가 두 목록의 중복 ID와 룬 누락, 배치 노드의 선행 연결, 비배치 노드의 선행 조건 없음, 레벨 범위를 검증한다.

현재 트리의 기본 강화 노드와 효과 종류는 메인보드(전력 회복), RAM 용량, 파워(최대 전력), CPU(캐스팅 속도), GPU(피해), 스크랩 획득량, 기본 HP, 이동 속도다. 새 효과 종류를 추가하려면 `UpgradeEffectType`에 값을 추가하고 실제 적용은 `Session/RuneCodeSession.cs` 및 해당 Core/Features 능력치 계산에 연결한다. `UpgradeTreeNodeIconGraphic.cs`는 Sprite를 지정하지 않았을 때 효과별 임시 아이콘을 그린다. 노드의 `_icon`에 Sprite를 지정하면 그 이미지가 대신 표시된다.

트리 능력치는 레벨별 `_amount`의 합으로 계산한다. 메인보드의 회복량, 파워의 최대 전력, RAM 용량, HP는 기본값에 더한다. GPU의 피해, 이동속도, 스크랩 획득량은 각각 `1 + 합계` 배율이다. GPU는 적에게 들어가는 마법 피해(화염 지속 피해 포함)에 적용하고 회복·보호량에는 적용하지 않는다. 이동속도는 일반 이동에 적용하고 대시는 기존 거리·시간을 쓴다. 스크랩 증가는 처치 기본 보상과 처치 드롭의 스크랩 오브에 적용되며, 소수 보너스는 다음 드롭으로 이월된다. 스테이지 클리어 보상에는 적용하지 않는다. CPU는 현재 구매와 저장만 되며 캐스팅 속도에는 연결되지 않는다.

기본 비용·증가량은 첫 트리 구성용 초깃값이며, 실제 밸런스는 SO의 `_levels`에서 조정한다. CPU는 현재 비용과 레벨만 기록하고 효과량은 0으로 둔다. 초기 스크랩이 없어도 시작 마법을 사용할 수 있도록 메인보드 첫 레벨과 화염 해금 비용은 0으로 설정했다. Builder는 레이아웃 버전 5의 기존 29개 노드를 버전 6에서 배치 12개/비배치 17개로 한 번 분리하며, 기존 비용·효과량·아이콘을 보존한다. 이후 카탈로그에 추가된 `tree`/`bench` 해금 룬은 비배치 목록에 자동 보충한다. 현재 SO는 개발 쪽에 추가된 `behavior.beam`을 포함해 배치 12개/비배치 18개다. 이후에는 `Rune Code > Build Scenes`를 실행해도 버전이 같은 자산의 노드 값을 덮어쓰지 않는다.

### Inspector에서 트리 편집하기

1. Unity Project 창에서 `Assets/RuneCode/Resources/RuneCode/UpgradeTree.asset`을 선택한다. **배치할 노드** 또는 **배치하지 않을 노드**에서 수정할 `_id`를 확인한다. 같은 `_id`는 저장 레벨과 연결선의 기준이므로 기존 노드의 ID를 바꾸면 이전 진행이 이어지지 않는다.
2. 가격과 강화량은 `_levels`의 각 `_cost`, `_amount`에서 레벨별로 수정한다. 스탯은 최대 세 원소, 해금은 한 원소만 둔다. RAM 증가량은 정수로 지정한다. 퍼센트 효과인 GPU·이동속도·스크랩 획득량은 `0.1`이 10% 상승을 뜻한다. CPU는 효과 구현 전이므로 `_amount`를 0으로 둔다.
3. 새 스탯 노드를 만들려면 기존 스탯 노드를 복제해 고유한 `_id`, 표시용 `_titleKey`·`_descriptionKey`, `_effectType`, `_position`을 지정한다. 같은 효과 종류라면 능력치에 해당 종류의 구매 레벨 효과가 합산된다. 새 효과 종류는 enum, 세션 수치 계산, 전투 적용과 팝업의 상승량 단위도 함께 구현해야 한다.
4. 새 해금 노드는 `_effectType = RuneUnlock`, `_runeId = runes.json`의 플레이어 룬 ID, `_levels` 한 개로 설정한다. 플레이어용 `tree` 룬마다 두 목록 중 한 곳에 해금 노드 하나가 필요하다.
5. **배치할 노드**의 루트인 메인보드 외에는 `_prerequisites`에 배치된 노드 ID 하나와 `_requiredLevel = 1`을 입력한다. **배치하지 않을 노드**는 `_prerequisites`를 비운다. `_position`은 배치된 노드의 왼쪽 위 좌표다. 연결선은 이 데이터로 자동 생성되므로 Prefab 안에서 선을 직접 추가하지 않는다. 현재 검증 규칙은 모든 노드의 `_requiredStage = 1`을 요구한다.
6. 다른 그림을 쓰려면 원하는 텍스처를 `Sprite (2D and UI)`로 가져와 노드의 `_icon` 슬롯에 할당한다. 비워두면 `UpgradeTreeNodeIconGraphic`의 임시 벡터 아이콘이 표시된다. 정사각형 테두리와 아이콘 크기는 `UpgradeTreeNode.prefab`에서 함께 편집한다.

성장 화면은 트리 노드만 표시하며 아이콘 클릭이 `RuneCodeSession.BuyUpgradeNode`를 호출한다. 노드 색은 선행 잠금 회색, 구매 가능 기본색, 스크랩 부족 주황색, 부분 강화 보라색, MAX 초록색이다. 아이콘 호버 상자는 해당 노드 위에 이름·설명·다음 상승량·스크랩 비용을 표시하고, 스탯 강화 노드에만 현재 레벨을 추가한다. 룬 노드는 상승량 대신 룬 해금을 표시한다. CPU는 실제 캐스팅 속도 효과가 아직 없어 상승량 자리에 `효과 추후 적용`을 표시한다. MAX 노드에는 다음 상승량과 비용 대신 최대 레벨 상태를 표시한다. 디버그 모드의 비용은 0 스크랩으로 나온다. 마우스 우클릭 드래그로 트리를 이동하고 휠로 확대·축소한다. 화면 여백과 팝업 배치는 `WorkshopLayout.BuildUpgradeTree` 및 `EnsureUpgradeTreeLayout`에서 관리한다.

## 디버그 시작과 초기화

타이틀에서 `일반 모드 시작`을 누르면 기존 일반 저장을, `디버그 모드 시작`을 누르면 별도의 메모리 진행을 연다. 디버그 진행에서는 기본 룬 5개와 두 목록의 모든 룬 해금 노드를 처음부터 사용할 수 있다. 비배치 노드는 디버그에서도 트리 화면에는 보이지 않는다. 스탯 강화는 0레벨에서 시작하며 배치된 어느 노드든 부모 구매나 스크랩 없이 최대 3레벨까지 올릴 수 있다. 노드 비용은 디버그에서 0으로 표시된다.

설정 탭의 `강화 트리 초기화`는 디버그 스탯 레벨만 0으로 돌리고 룬 해금과 마법 보관함은 유지한다. `디버그 진행 초기화`는 이번 디버그 진행 전체를 새로 시작한다. 디버그에서의 구매, 마법 편집, 미션 정산, 설정 변경은 일반 저장 파일에 쓰지 않는다. 일반 모드의 저장·구매 규칙은 그대로 유지된다.

## 연결 흐름

```text
UI/Workshop/UpgradeTreePanel ── UpgradeTreeNodeView ── UpgradeTreeNodeIconGraphic
  └─ UpgradeTreeCanvasInput (우클릭 팬·휠 줌)
  └─ RuneCodeSession → 비용 확인·재화 지불·강화/해금·저장
       ├─ UpgradeTreeDefinition (Resources/RuneCode/UpgradeTree.asset)
       ├─ PlayerSave → 진행 상태
       ├─ BalanceData → 강화 전 기본 능력치
       ├─ MissionRun/DockRun → RuneSimulation 전투 능력치
       └─ RuneData/GameData → 룬 정의와 표시 데이터
```

새 UI 스크립트나 필드를 추가한 뒤에는 Layout 빌더에서 오브젝트·Prefab을 만들고 참조를 연결한다. 기능 확인은 Unity 컴파일, 씬 재생성, Inspector의 Missing Script/Missing Reference 확인 순으로 진행한다. 코드만 읽고 실제 씬 연결이 완료됐다고 판단하지 않는다.
