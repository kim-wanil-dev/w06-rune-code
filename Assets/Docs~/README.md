# Rune Code 시스템 구조 문서

> 2026-10-08: 마법 그래프를 토큰 모델로 리팩토링했다. 04가 새 구조를 설명한다. 01·02·03·05·06·08에 남은 옛 컴파일 모델·룬·적응 설명은 `Refactoring/마법 그래프 리팩토링 계획.md`와 04를 우선한다.

이 폴더는 `Assets/RuneCode` 아래 C# 스크립트 24개(프로젝트 코드 22개 + Unity 템플릿 Readme 2개)를 읽고 정리한 구조 문서다. 게임 규칙 설명이 아니라 **코드가 어떤 계층으로 나뉘고 서로 어떻게 연결되며 어떤 순서로 부팅되는지**를 다룬다.

플레이 규칙·조작·강화 가격 등 사용자 관점 설명은 루트 `README.md`, 작업 기록은 `docs/PROGRESS.md`, 결정 기록은 `docs/DECISIONS.md`에 있다.

## 문서 목록

| 문서 | 내용 |
|---|---|
| [01-architecture.md](01-architecture.md) | 계층 구조, 어셈블리 경계, 의존 방향, 앱 계층과 시뮬레이션 계층의 분리 지점 |
| [02-bootstrap.md](02-bootstrap.md) | 부트스트래핑 전 과정 — 장면 준비, `Awake`, 데이터 적재, 저장 복원, 화면 생성, CLI 분기, 빌드 |
| [03-data-layer.md](03-data-layer.md) | `Resources` JSON과 적재·검증 코드(`GameData`, `BalanceData`·`SpellSettings`, `MissionData`) |
| [04-graph-and-compiler.md](04-graph-and-compiler.md) | 편집 그래프 → 실행 그래프(`SpellProgram`) → 토큰 런타임(`SpellRuntime`), 검증 코드 G1~G6/W1, 흐름 표시 |
| [05-simulation.md](05-simulation.md) | 60Hz 결정적 시뮬레이션의 틱 순서, 적 AI, 맵 충돌, 상태 해시 (마법 실행은 04) |
| [06-presentation.md](06-presentation.md) | `RuneCodeApp` 상태 기계, 절차적 uGUI 뷰, 그래프 캔버스, 경기장 메시 렌더러 |
| [07-save-and-tools.md](07-save-and-tools.md) | 저장 파일 v3와 v2·v1 이전, 로컬 텔레메트리, 헤드리스 시뮬레이션 CLI, 에디터 빌드 메뉴 |
| [08-file-reference.md](08-file-reference.md) | 파일별 책임 표, 공개 API 요약, 룬 카탈로그, 주요 호출 지도 |

## 한 장 요약

```
                 Unity Editor / Player
                          |
          [ Assembly-CSharp-Editor ]  RuneCodeBuild  (장면·글꼴 준비, Windows 빌드)
                          |
          [ Assembly-CSharp ]
            Game/  RuneCodeApp (상태 기계·고정 스텝 구동)  SimulationCli  LocalTelemetry
            UI/    RuneCodeView  RuneGraphCanvas  SpellFlowLayer  RuneGraphOverlay  GraphLayout  RuneMesh  RunePaletteDrag
            Save/  PlayerSave  SaveStore
                          |   (단방향 참조)
          [ RuneCode.Core (asmdef) ]
            Data/        GameData  BalanceData(SpellSettings)
            Graph/       SpellGraph  SpellNodes  SpellProgram  ShareCodec
            Mission/     MissionData (적·섹터·보스·인크리멘탈·MissionMap)
            Simulation/  RuneSimulation  SimulationTypes  SpellRuntime  SpellRuntimeTypes
                          |
          Resources/RuneCode/*.json   (밸런스·마법 설정·적·맵·문자열)
```

핵심 분리점은 하나다. **`RuneCode.Core`는 Unity 입력·UI·시간을 전혀 모른다.** 시뮬레이션은 `Step(SimulationInput)`을 호출받아 정확히 1/60초를 진행하는 순수 함수형 루프이고, Unity의 가변 프레임 시간을 고정 틱으로 변환하는 책임은 전부 `RuneCodeApp.Update`의 누산기(accumulator)에 있다. 덕분에 같은 입력·시드는 에디터·빌드·헤드리스 CLI에서 동일한 상태 해시를 만든다.
