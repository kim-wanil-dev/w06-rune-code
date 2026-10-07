# 폐도시 재가동 PoC

Unity 6.3 (`6000.3.22f1`), URP 2D, Windows 데스크탑용 작업장 시뮬레이션입니다.

## 실행

- 시작 씬, 로봇 프리팹, 데이터 에셋 20개, 기본 PNG 34개가 생성되고 연결되어 있습니다.
- Unity에서 `Assets/_RobotWorkshop/Scenes/RobotWorkshop_PoC.unity`를 열고 Game 뷰를 `Full HD (1920x1080)`로 선택한 뒤 Play를 누릅니다. 추가 Inspector 연결은 필요 없습니다.
- 생성 자산이 없는 경우에만 `Robot Workshop > Create PoC Assets`를 실행합니다. 기존 자산은 유지합니다.
- Windows Development Player는 `Robot Workshop > Build Windows Development Player`로 빌드합니다. 결과는 `Build/RobotWorkshop_PoC/RobotWorkshop_PoC.exe`입니다.
- 첫 개체는 철을 자동 채집해 정비소로 운반합니다. 정비소에 쌓인 재료는 `자원 환전`을 눌러야 공통 재화가 됩니다.
- `운영`, `조립 · 뽑기`, `시설 · 데이터` 탭에서 개체 선택, 작업 지정, 파츠 관리, 뽑기, 시설 구매를 합니다.
- 이름 변경을 누른 뒤 키보드로 입력하고 Enter를 누르면 저장됩니다. Esc는 취소합니다.
- 화면 상단의 좌우 버튼으로 작업장을 이동합니다. 앱은 창이 비활성인 동안에도 시뮬레이션을 진행합니다.

## 조정 위치

- 전체 시간·적재·지능·코어 성장·저장 주기: `Assets/_RobotWorkshop/Data/WorkshopGameConfig.asset`
- 등급 배율·뽑기 가중치·표시 색: `Assets/_RobotWorkshop/Data/GradeTable.asset`
- 파츠 능력치·레벨 성장·채집 효율·뽑기 가중치: `Assets/_RobotWorkshop/Data/Part_*.asset`
- 자원 무게·환전 가치·XP·채집 시간: `Assets/_RobotWorkshop/Data/Resource_*.asset`
- 부위별 뽑기 가격과 기본 파츠 세트: `Assets/_RobotWorkshop/Data/GachaConfig.asset`
- 자원함·오른쪽 작업 구역 비용과 효과: `Assets/_RobotWorkshop/Data/Facility_*.asset`
- 보너스 잔해의 간격·대기 수·자원 풀·출현 위치: `Assets/_RobotWorkshop/Data/BonusSpawnConfig.asset`
- 공급원 위치·초기량·최대량·재공급: 시작 씬의 `Source_Iron`, `Source_Copper`, `Source_Electronics` Inspector
- 정비소와 화면 위치: 시작 씬의 `ServiceBay`, `WorkshopRuntime` Inspector

## 이미지 교체

`Assets/_RobotWorkshop/Art/`의 PNG는 Sprite, Single, Full Rect, PPU 32, 투명 배경, Mipmap Off, Point 필터로 가져옵니다. 로봇 부위는 각각 64×64 전체 캔버스, 시설 단계는 128×96, 자원·잔해는 64×64, 바닥은 64×32, UI 아이콘은 32×32입니다. 로봇 부위와 시설 이미지는 하단 중앙 피벗이고, UI 아이콘은 중앙 피벗입니다.

같은 PNG를 같은 경로와 이름으로 바꾸면 기존 `.meta`와 Sprite 참조가 유지됩니다. 새 PNG를 추가할 때는 같은 임포트 규격으로 설정하고, 관련 `PartDefinition`, `ResourceDefinition`, `FacilityDefinition`의 Sprite 필드에 연결합니다. 이미지는 외형에만 영향을 주며 작업 판정·적재 무게·지면 기준점을 바꾸지 않습니다.

## 저장과 초기화

- 저장 파일은 `%USERPROFILE%\AppData\LocalLow\RobotWorkshop\RobotWorkshop_PoC\RobotWorkshop\save.json`입니다.
- 자동 저장은 설정 주기와 종료 시 실행됩니다. 종료 후 오프라인 생산은 계산하지 않습니다.
- Editor와 Development Player의 `시설 · 데이터` 탭에 개발 패널이 있습니다. 저장 초기화는 이 패널의 버튼으로만 합니다.

## 검증과 범위

- Unity 6.3의 열린 에디터에서 씬과 자산을 생성하고 컴파일 및 Play 모드 실행을 확인했습니다. 현재 Console 오류와 경고는 0개입니다.
- 실제 자동 채집으로 철 10개가 입고되고, 환전 전 재화 0에서 환전 후 재화 10으로 바뀌는 것을 확인했습니다. 시설 구매, 공급원 잠금 해제, 카메라 이동, 뽑기, 저장 재로드, 초기화도 기존 UI 버튼을 통해 확인했습니다. 시설·뽑기 점검에는 기존 개발 재화 지급을 사용했습니다.
- 씬·프리팹·데이터의 Missing Script 및 Missing Reference와 런타임 필수 참조 누락은 0개입니다. 한글 표시, 픽셀 아트 피벗, 화면 아래 제작 버튼, 초기화 후 로봇 표시도 확인했습니다.
- Windows Player 빌드와 실행은 이번 작업에서 수행하지 않았습니다. 필요하면 위 빌드 메뉴를 사용합니다.
- PoC 제외: 대규모 로봇 군집, 비행·대형 인양 부품, 경로 최적화, 복잡한 적 제작, 구역 추가 구매, 자동 환전, 오프라인 생산, 랜덤 추가 옵션, 부품 강화·코어 진화, 투명창·클릭 통과·트레이 기능.
