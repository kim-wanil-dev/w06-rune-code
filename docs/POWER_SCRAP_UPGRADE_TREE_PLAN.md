# 전력·스크랩 업그레이드 트리 계획

상태: M1 완료. M2 이후는 미착수.

## 확정된 목표와 범위

기존 기반 강화 화면과 그 구매 경로를 제거하고, 업그레이드와 룬 해금을 새 하드웨어 트리 하나로 통합한다. 기존 전투시간 강화도 제거한다. 스테이지 자체의 전투 시간·제한시간 규칙은 유지한다.

일반 시작 시 기본 해금 룬은 구형, 사각, 부채꼴, 발사 네 개뿐이다. 그 외 플레이어가 선택할 수 있는 룬은 각각 한 번 구매하는 해금 노드로 둔다. 강화 노드는 1~3레벨, 해금 노드는 1회 구매이며, 선행 노드 하나를 1레벨 이상 구매하면 다음 노드에 접근할 수 있다. 비용과 레벨별 강화량은 ScriptableObject에서 편집한다.

게임 시작 화면에서 일반 모드와 디버그 모드를 고르게 한다. 디버그 모드는 모든 룬을 트리와 무관하게 사용할 수 있게 하고, 강화 노드의 선행 조건을 무시하며 스크랩 비용 없이 구매할 수 있게 한다. 디버그 화면에는 트리 초기화 버튼을 둔다. 디버그 구매 및 초기화는 현재 디버그 세션에만 적용해 일반 저장 진행을 바꾸지 않는 안으로 계획한다.

새 트리의 현재 시작점은 메인보드로 둔다. 보스 스테이지 클리어 뒤에 메인보드 위로 추가할 상위 루트 노드와 그 해금 조건은 이번 범위에 넣지 않는다. CPU는 구매와 레벨 기록만 지원하고 캐스팅 속도 효과는 후속 작업으로 남긴다.

## 트리 구성

메인 줄기는 화면 가운데에서 아래로 이어지고, 나머지 가지는 좌우 대각선으로 뻗는다.

- 메인보드: 전력 회복 속도 강화, 현재 트리의 시작 노드
  - 화염 속성 해금
  - 얼음 속성 해금
  - RAM: 램 용량 증가
    - 파워: 최대 전력 증가
    - CPU: 캐스팅 속도 강화 노드, 현재는 구매와 레벨만 기록
    - GPU: 마법 데미지 증가
      - 폭발 해금
      - 잔류 해금
      - 스크랩 획득량 증가
        - 기본 HP 증가
        - 이동속도 증가

요청한 목록 외의 모든 플레이어용 룬도 해금 노드로 추가한다. 해당 룬들은 종류에 맞는 가지에 배치하고, 각 노드의 위치·선행 노드·가격은 ScriptableObject에서 조정한다. 예외는 마법 편집기에 직접 놓지 않는 구현용 토큰뿐이다. 예를 들어 form.bolt, magic.inline, elem.fire는 내부 처리용 항목이고, 트리의 화염 해금 대상 element.fire와는 다른 ID다. 따라서 element.fire와 element.ice를 포함해 플레이어에게 제공되는 비시작 룬은 모두 해금 노드가 된다. 현재 트리에는 스테이지별 잠금 조건을 추가하지 않는다.

강화 효과 종류는 전력 회복, 최대 전력, RAM 용량, 캐스팅 속도, 데미지, HP, 이동속도, 스크랩 획득량, 룬 해금으로 구성한다. 메인보드와 RAM 같은 하드웨어 노드는 각 스탯 효과를 설명하는 이름으로 표시한다. 각 노드에는 교체 가능한 Sprite 참조를 두고, 초기 아이콘은 임시 도형으로 표시해 나중에 에셋만 바꿀 수 있게 한다.

스크랩 획득량 강화는 우선 적 처치로 얻는 스크랩에 적용한다. 스테이지 클리어 보상은 기존 설정값을 유지한다.

## 구매·저장·효과 규칙

일반 모드에서 노드 구매는 스크랩을 사용한다. 자식 노드는 바로 연결된 선행 노드 중 하나를 1레벨 이상 구매하면 접근 가능하다. 강화 노드는 최대 3레벨, 룬 해금 노드는 1회 구매로 제한한다. 최대 레벨, 구매 가능, 선행 조건 잠금, 스크랩 부족, 일부 강화 상태는 노드의 색·테두리·진행 표시로 서로 구분한다.

새 트리에는 기존 39개 노드의 레벨이나 효과를 승계하지 않는다. 저장 마이그레이션에서 기존 트리 진행과 구형 기반 강화 레벨을 초기화하고, 기존 진행으로 해금됐던 비시작 룬도 새 트리에서 다시 구매하도록 잠근다. 스크랩, 스테이지 진행, 마법 보관함과 설정 같은 나머지 저장 데이터는 보존한다. 저장된 마법 그래프는 보존하되, 잠긴 룬이 포함된 마법은 해당 룬을 새 트리에서 다시 해금하기 전까지 편집·출격 가능 여부를 기존 검증 규칙에 맞게 처리한다.

새 트리의 구매 레벨만 전투 능력치에 적용한다. 전력 회복, 최대 전력, RAM, 데미지, HP, 이동속도, 스크랩 획득량은 일반 게임과 시험 도크에서 같은 세션 능력치 원본을 사용한다. CPU 레벨은 저장하되 실제 캐스팅 속도에는 아직 반영하지 않는다.

## 화면 동작

작업실에서는 기반 강화 탭과 기존 룬 해금 목록을 제거하고 강화 트리 탭만 성장 구매 화면으로 사용한다. 트리 화면 왼쪽에는 강화 가능한 스탯, 현재 레벨·수치, 다음 강화 조작을 표시한다. 왼쪽 스탯 행의 구매 버튼은 트리의 해당 노드 아이콘 클릭과 같은 구매 경로와 상태를 사용해 진행 데이터가 이중으로 생기지 않게 한다.

노드에는 아이콘만 표시한다. 아이콘을 누르면 일반 모드에서 구매 또는 해금을 시도한다. 마우스를 올렸을 때 아이콘 바로 위에 뜨는 상자에는 강화 이름·설명·현재 레벨만 표시하고, 해금 노드에는 레벨을 표시하지 않는다. 가격이나 잠금 사유는 이 상자에 넣지 않는다.

디버그 모드에서는 모든 룬을 사용할 수 있고 트리의 선행 조건과 비용을 무시한다. 노드 레벨은 일반 강화와 동일하게 표시한다. 초기화 버튼은 디버그 모드에서만 보이며, 디버그 세션에서 구매한 강화 레벨을 되돌린다. 우클릭 드래그 이동과 휠 확대·축소는 계속 지원한다.

## 커밋 단위 마일스톤 제안

커밋은 실행하지 않는다. 아래 메시지는 제안만 한다.

### M1 — 용어와 구형 강화 저장 정리

제안 메시지: refactor(progression): retire legacy base upgrades

- [x] 화면의 에너지 표기를 전력으로 바꾸고, 처치·정산 재화와 구매 재화 표기를 스크랩으로 바꾼다. 실제 마법 메모리 용량 표기 RAM은 유지한다.
- [x] 기반 강화 카드·별도 룬 구매 경로·전투시간 강화의 저장 필드, 비용 계산, 구매 처리를 새 트리 적용에 맞춰 폐기한다.
- [x] 룬 팔레트에서 구형 룬 구매 가격을 없애고 강화 트리에서 해금한다는 안내를 표시한다.
- [x] 기존 세이브의 스크랩과 나머지 진행 데이터를 보존하고, 구형 강화 및 기존 트리 구매 효과를 초기화하는 v3 일회성 마이그레이션을 추가한다.
- [x] 스테이지에 정의된 전투 시간 규칙은 유지한다.

완료 기준: 기반 강화/전투시간 강화의 구매 경로가 없고, 새 저장에서는 구형 강화 레벨이 전투에 적용되지 않는다.

### M2 — ScriptableObject 트리 데이터 교체

제안 메시지: feat(progression): define hardware upgrade tree data

- 기존 39개 노드를 새 하드웨어 구조로 교체하고 메인보드 시작 노드, 한 단계 선행 조건, 1~3레벨 강화 및 1회 해금 규칙을 검증한다.
- 네 시작 룬을 제외한 모든 플레이어용 룬마다 해금 노드를 하나씩 둔다.
- 비용, 강화량, 룬 연결, 좌표, 아이콘 참조를 ScriptableObject에서 직접 편집할 수 있게 한다.
- 스테이지별 잠금이나 미래 보스 루트 노드는 추가하지 않는다.

완료 기준: 카탈로그의 플레이어용 비시작 룬이 빠짐없이 해금 노드에 연결되고 데이터 검증이 중복 ID·없는 룬·잘못된 선행 노드를 검출한다.

### M3 — 구매 처리와 능력치 연결

제안 메시지: feat(progression): apply hardware tree upgrades

- 일반 구매의 스크랩 차감, 선행 조건, 최대 레벨, 룬 해금과 저장을 새 노드 데이터에 연결한다.
- 새 트리 레벨에서 미션과 시험 도크의 전력·RAM·데미지·HP·이동속도·스크랩 획득량을 계산한다.
- CPU는 비용 지불 및 레벨 저장만 하며 캐스팅 속도는 바꾸지 않는다.
- 스크랩 획득량은 적 처치 보상에 적용하고 스테이지 클리어 보상은 기존 값으로 둔다.

완료 기준: 새 트리 레벨만 효과에 반영되고, 일반 구매·잠금·최대 레벨·세이브 후 재실행 시 동작이 일치한다.

### M4 — 시작 화면의 모드 선택과 디버그 진행

제안 메시지: feat(debug): add upgrade tree debug start mode

- 타이틀에서 일반/디버그 시작을 선택하게 하고 선택값을 세션에 전달한다.
- 디버그 모드에서는 전체 룬 사용, 선행 조건 무시, 무료 강화와 초기화를 제공한다.
- 디버그 트리 진행은 현재 세션에만 두어 일반 세이브를 덮어쓰지 않는다.

완료 기준: 모드 선택이 첫 화면에서 가능하고, 디버그에서 모든 룬과 강화 노드를 시험할 수 있으며 초기화가 일반 저장 진행에 영향을 주지 않는다.

### M5 — 작업실 성장 화면과 좌측 스탯 패널

제안 메시지: feat(ui): replace base upgrades with hardware tree

- 기반 강화 탭과 중복 룬 목록을 제거하고, 트리를 새 배치로 화면 중앙에 둔다.
- 왼쪽 스탯 목록의 강화 버튼과 트리 노드 클릭을 같은 구매 처리에 연결한다.
- 아이콘은 노드 데이터의 Sprite 참조로 바꿔 끼울 수 있게 하고 임시 아이콘을 표시한다.
- 기존 우클릭 이동과 휠 확대·축소를 유지한다.

완료 기준: 성장 구매는 트리 화면 하나에서 가능하고 왼쪽 스탯 조작과 노드 조작이 동일한 레벨을 변경한다.

### M6 — 노드 상태와 포인터 툴팁

제안 메시지: feat(ui): clarify upgrade node states and tooltips

- 구매 불가, 선행 잠금, 부분 강화, 최대 레벨 상태를 노드 시각으로 구분한다.
- 포인터 툴팁을 노드 바로 위에 띄우고 강화명·설명·강화 노드 레벨만 표시한다.
- 디버그 모드에서만 초기화 조작을 표시한다.

완료 기준: 노드 상태마다 화면 표시가 구별되고, 툴팁 위치와 항목이 요청 내용에 맞는다.

### M7 — 안내 문서와 통합 검증

제안 메시지: docs: document hardware upgrade tree workflow

- docs/UI_AND_PROGRESSION_GUIDE.md에 노드 추가·가격/강화량 변경·아이콘 교체·선행 조건 편집 방법을 기록한다.
- Unity 컴파일과 에셋 참조를 확인하고 일반/디버그 시작, 구매·잠금·해금·세이브 마이그레이션, 트리 입력과 툴팁을 확인한다.
- Missing Script/Reference와 저장 마이그레이션 결과를 확인한다. 테스트 코드는 추가하지 않는다.

완료 기준: 문서만으로 SO에서 강화·해금 노드를 추가·수정할 수 있고, Unity에서 핵심 구매 흐름을 검증한다.

## 수정 대상 기존 파일 목록

### 시작 모드와 화면

- Assets/RuneCode/Boot/RuneCodeApp.cs — 세션 생성을 타이틀 모드 선택 뒤로 옮기고 선택 모드를 전달한다.
- Assets/RuneCode/UI/Title/TitleScreen.cs — 일반/디버그 시작 조작과 참조를 연결한다.
- Assets/RuneCode/UI/Title/TitlePresenter.cs — 모드 선택을 앱 세션 생성·화면 진입으로 전달한다.
- Assets/RuneCode/UI/Editor/Layouts/TitleLayout.cs — 타이틀 모드 선택 UI를 생성한다.
- Assets/RuneCode/Resources/RuneCode/UI/TitleScreen.prefab — 저장된 타이틀 화면의 참조와 레이아웃을 갱신한다.

### 진행 데이터, 세이브, 효과

- Assets/RuneCode/Progression/UpgradeTreeDefinition.cs — 새 효과 종류, Sprite, 노드 규칙과 검증을 추가한다.
- Assets/RuneCode/Editor/UpgradeTreeAssetBuilder.cs — 기존 선형/교차 데이터 대신 새 트리 데이터를 만든다.
- Assets/RuneCode/Resources/RuneCode/UpgradeTree.asset — 기존 39개 노드를 새 노드 구성으로 교체한다.
- Assets/RuneCode/Resources/RuneCode/tables/runes.json — 시작 룬을 네 개로 제한하고 나머지 플레이어용 룬의 트리 해금 상태를 데이터에 반영한다.
- Assets/RuneCode/Session/RuneCodeSession.cs — 새 트리의 구매·레벨·효과·디버그 규칙을 소유한다.
- Assets/RuneCode/Save/PlayerSave.cs — 구형 기반 레벨과 이전 트리 효과 초기화, 네 시작 룬, 새 진행 저장을 처리한다.
- Assets/RuneCode/Save/SaveStore.cs — 기존 세이브를 새 진행 버전으로 한 번 마이그레이션한다.
- Assets/RuneCode/Core/Data/BalanceData.cs — 기반/전투시간 강화 전용 비용·수치 정의와 검증을 정리한다.
- Assets/RuneCode/Resources/RuneCode/balance.json — 제거된 기반/전투시간 강화 값만 정리하고 나머지 경제 설정을 보존한다.
- Assets/RuneCode/Core/Simulation/RuneSimulation.cs — 미션 능력치, 이동·피해·처치 스크랩 보상에 새 트리 효과를 연결한다.
- Assets/RuneCode/Boot/SimulationCli.cs — 제거되는 기반 강화 설정 참조를 정리해 시뮬레이션 CLI를 계속 사용할 수 있게 한다.
- Assets/RuneCode/Features/Mission/MissionRun.cs — 세션의 능력치 값을 미션 시뮬레이션으로 전달한다.
- Assets/RuneCode/Features/Workshop/DockRun.cs — 시험 도크에도 동일한 공통 능력치 계산을 전달한다.

### 작업실 UI와 안내 문서

- Assets/RuneCode/UI/Workshop/WorkshopScreen.cs — 기반 강화 탭과 Bench 참조를 제거하고 새 트리 구성을 연결한다.
- Assets/RuneCode/UI/Workshop/WorkshopPresenter.cs — Bench 구매 갱신 흐름을 제거하고 새 트리 패널 갱신을 연결한다.
- Assets/RuneCode/UI/Workshop/UpgradeTreePresenter.cs — 왼쪽 스탯 강화, 디버그 초기화, 상태별 노드 표시를 처리한다.
- Assets/RuneCode/UI/SpellEditor/SpellPalettePresenter.cs — 트리에서 구매할 룬에 구형 벤치 비용을 표시하지 않고 해금 안내를 연결한다.
- Assets/RuneCode/UI/Workshop/UpgradeTreePanel.cs — 왼쪽 스탯 패널, 노드 입력, 툴팁 배치와 화면 좌표를 연결한다.
- Assets/RuneCode/UI/Workshop/UpgradeTreeNodeView.cs — 잠금·구매 가능·부분 강화·최대 상태를 구분한다.
- Assets/RuneCode/UI/Workshop/UpgradeTreeNodeIconGraphic.cs — 새 효과 종류의 임시 아이콘 표시를 지원한다.
- Assets/RuneCode/UI/Editor/Layouts/WorkshopLayout.cs — 기반 강화 화면을 제거하고 좌측 스탯 패널 및 새 트리 배치를 생성한다.
- Assets/RuneCode/Resources/RuneCode/UI/WorkshopScreen.prefab — 저장된 작업실 화면의 탭·패널 참조를 갱신한다.
- Assets/RuneCode/Prefabs/UI/UpgradeTreeNode.prefab — 노드 아이콘과 시각 상태 참조를 갱신한다.
- Assets/RuneCode/Resources/RuneCode/strings.ko.json — 전력·스크랩·모드 선택·스탯·상태 문구를 반영한다.
- docs/UI_AND_PROGRESSION_GUIDE.md — 트리 편집과 아이콘 교체 절차를 문서화한다.

### 더 이상 사용하지 않는 기존 파일

기반 강화 화면과 전용 카드를 완전히 제거할 경우 다음 소스·에셋 및 대응 .meta를 삭제한다.

- Assets/RuneCode/UI/Workshop/BenchPanel.cs
- Assets/RuneCode/UI/Workshop/BenchPresenter.cs
- Assets/RuneCode/UI/Workshop/UpgradeCardView.cs
- Assets/RuneCode/Prefabs/UI/UpgradeCard.prefab

## 검토할 해석

- “보스 클리어 후 추가할 루트”는 후속 범위이므로 이번에는 메인보드를 즉시 접근 가능한 현재 루트로 둔다.
- 시작 룬 네 개 외의 기존 해금은 새 트리로 다시 획득하게 하고, 세이브의 마법 그래프 자체는 지우지 않는다.
- 내부 문법 토큰 제외는 현재 카탈로그에서 플레이어가 직접 선택하지 않는 구현 항목을 가리킨다. 화염·얼음 트리 노드는 element.fire와 element.ice이며 내부 elem.fire/elem.ice 토큰을 뜻하지 않는다.
- 디버그 모드의 노드 레벨 변경은 일반 세이브에 기록하지 않는 세션 전용 진행으로 둔다.
- 요청된 숫자 비용과 강화량은 이 계획에서 임의로 정하지 않고 새 ScriptableObject 값으로 편집한다.



