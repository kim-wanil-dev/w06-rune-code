# Rune Code M0–M6 진행 기록

| 단계 | 구현 | 실제 검증 |
|---|---|---|
| M0 | Unity 기존 프로젝트·입력·빌드 환경 확인, Core와 UI를 asmdef로 분리 | Editor 연결·컴파일 확인, Core의 UI 역참조 제한 |
| M1 | 19종 룬·데이터 검사·E1~E10/W1~W4·컴파일·RC1·템플릿 3종 완료 | O1~O7 수치·오류·경고, RC1 왕복 확인 |
| M2 | 60Hz·시드 RNG·18종 룬 효과·예약 실행·충돌·상태이상 완료 | S1~S6 및 형태·전격·유도·확장·순간이동·보호막 실행 확인 |
| M3 | uGUI 편집·포트 연결·선택·검색·Undo/Redo·보관함·도크 완료 | 실제 Play 화면, 다중 3발/RAM5/6, 잘못된 연결 차단, 복사·저장 재로드 확인 |
| M4 | ASCII 방·적 5종·웨이브·터미널·입력·HUD·결과·디버그 완료 | R1 일반 전투, 전투 중 편집 차단, 모든 방 진행·터미널 확인 |
| M5 | 버전 1 저장·성장·해금·정산·적응·거버너 완료 | S7/S8, 두 패치 문턱, 보상121, 70% 정산, 용량3단계 구매 재로드 확인 |
| M6 | 도형 피드백·튜토리얼·텔레메트리·한국어 README·Windows x64 배포 완료 | 실제 Title/Workshop/Mission 화면, Player 빌드·실행 시작·CLI 결과 확인 |

테스트 작성 및 Git 작업은 AGENTS.md에 따라 제외한다. 완료 여부는 구현과 실제 실행 검증을 구분하여 갱신한다.

## 실제 검증 기록 — 2026-10-07

Unity 6000.3.22f1의 연결된 Editor에서 공개 API를 일회성 평가하고 Play 화면을 캡처했다. 아래 수치는 저장한 테스트 코드가 아닌 실제 게임 구현에서 측정한 결과다. float JSON 설정으로 인한 약 1e-7 오차는 허용했다.

| 검사 | 관측 결과 |
|---|---|
| O1 | RAM2 / EN10 / CD0.31 / 개체1 |
| O2 | RAM3 / EN16.4 / CD0.34 / 개체3 |
| O3 | RAM3 / EN30 / CD0.34 / 개체3 |
| O4 | RAM5 / EN58.4 / CD0.40 / 개체6 |
| O5 | RAM2 / EN10 / CD0.31 / 개체1 |
| O6, O7 | 원소 중복 E6 / 버스트 관통 ok + W2 |
| S1 | 파이어 볼트 1회: 직접10 + 화상9 = 19, 더미HP81 |
| S2 | 냉기 3번째 명중 시 빙결, 이후 빙결 해제·면역 진입 및 면역 만료 확인 |
| S3 | 일렬 더미HP90 / 91 / 92 / 100 / 100 |
| S4, S5 | 지연 볼트 틱30 / 반복 볼트 틱0·9·18 |
| S6 | 노이즈+다중, 동일 시드1·동일 입력1000틱: 두 해시40150eec; 시드2는 b80593a4 |
| S7, S8 | fire0.60 / bolt0.30 / 피해배율0.28; 6초 무사용 후 fire0.54 |
| 성능 표본 | 더미300체 + 시전, 600틱: Editor C# 47.15ms, 최대 마법 개체3. Node 또는 화면 FPS 측정은 아님 |
| 추가 룬 효과 | 볼트10, 버스트14, 오비트 반복피해18, 존 반복피해16; 전격 연쇄10/5·EMP, 유도명중10, 확장반경70→105, 순간이동160, 보호막20→3초후0, 조건 else 분기 확인 |
| 편집 시나리오 | 파이어 볼트에 다중 부착: 발사3개·장착합계5/6. Undo/Redo 왕복, 내부 연결 포함 복사, 유일ID, 파일 저장 재로드 확인 |
| 안전 경계 | 전투 중 편집 거부. 터미널 편집 성공. 공유RAM7/6 장착이면 다음 방 거부, 복구 후 R2 진입 |
| R1 일반 전투 | 시작 A/B 마법과 고정 입력으로 7.4초에 터미널, HP100, 스카우트6처치·6조각. 디버그 무적/처치 미사용 |
| 전체 콘텐츠 진행 | 정상 보상·방 전환 경로를 디버그 처치로 진행: 방별 누적6/18/35/61, 보스 이후121. 실제 전체 전투 플레이 시간 측정은 아님 |
| 사망 정산 | R1 보상6 이후 R2에서 실제 피격 사망: 지급4(70% 반올림) |
| 보스 패치 | 증폭 버스트로 HP534.392에서 P2, HP269.792에서 P3. 각각 raw/burst가0.6/0.3 고정, 패치 중 1초간 피해0 확인, 이후 처치 가능 |
| 진행 루프 | 실제 앱 Result 정산121→Workshop→용량20/30/45구매→재로드: 용량12, 잔액26, 클리어·노이즈 해금 유지 |
| 저장·공유 | 버전1 JSON 검증·왕복, 시작 보관함 RC1 왕복 동일. QA 진행 변경은 검증 전 로컬 저장으로 복구 |
| 화면 | Builds/title.png, workshop.png, mission.png 실제 Editor 캡처. 글꼴·CanvasRenderer·그래프 마스크·도크 표식·HUD 배치 수정 |
| Windows 빌드 | 최종 Succeeded / 에러0 / 경고1 / 105,119,503bytes / 9.850초(증분 빌드). 첫 빌드는34.713초. 경고는 기존 Pipeline 패키지의 Player 원격 제어 설정 부재이며, 게임 실행과 무관하다. 이를 위해 원격 제어를 활성화하지 않았다 |
| Player CLI | 실제 EXE의 firebolt/dummy_line/600/seed1: 피해287.5, DPS28.75, EN290, 최대개체3, 실행87, 해시80408256. Editor 결과와 동일 |
| Player 결정성 | triplefire/adapt_loop/1000/seed1을 EXE에서 두 번 실행: 해시82f92a3f 동일, 피해126.6095757, 최대개체11 |
| Player 실행 시작 | 그래픽 Player를 별도 숨김 프로세스로 실행해 Input System·Mono·D3D12 초기화 및 게임 예외/Missing 로그 부재 확인 후 해당 프로세스 종료. D3D12 정보 큐 조회 진단은 있었으며 초기화는 계속됨. 전체 마우스 플레이 검증은 아님 |
| 폴리시 검증 | 로컬 기록600건 입력 후 링 버퍼500건 유지. 최종 HUD에서8개 태그·0%·최대 학습 막대를 확인하고 지원되는 글꼴 문자로 표시. 프레임 최대 스텝은 balance.json의5를 참조 |

## 인도 시 구분할 항목

- 필요한 Inspector 연결은 RuneCodePoC 장면과 UIFont 자산에 완료했다. 기존 SampleScene은 보존했다.
- AGENTS.md에 따라 테스트 코드·테스트 자산·테스트 프레임워크 추가, Git CLI·커밋·push는 수행하지 않았다. Git 변경 상태는 제공된 Diff로 별도 확인해야 한다.
- PRD 웹 스택 및 정적 웹 호스팅 대신 기존 Unity 프로젝트의 Windows x64 실행 빌드를 사용한다. 결정 근거는 DECISIONS.md에 기록했다.
- 사람의 첫 설계 3분·R1 5분·전체 섹터8~12분 지표, 노드60개 드래그50fps·전투150개60fps, 코드 커버리지는 측정하지 않았다. 콘텐츠의 실제 플레이 시간은 별도 플레이테스트가 필요하다.

## 인크리멘탈 전환 — 2026-10-07 (완료)

기존 M0–M6 표와 이전 검증 수치는 이전 게임 방식의 기록이다. 새 방식은 단일 마법·시간제 오른쪽 적 유입·RAM 재화·독립 용량/에너지/시간 성장으로 변경한다.

- Core: 시간제 종료와 100%/70% 정산, 단일 전투 마법, 단계별 편성·난이도, 적·예약·실행 상한 구현.
- App/UI: 자동 전투, 출격 단계 선택, 단일 그래프 편집·공유, 별도3종 성장, 시간·처치·RAM HUD, 결과·재도전 연결.
- Save: v1을 보존하는 v2 이전, 기존 설계·성장·설정 보존, 슬롯 비용 환불.
- Editor 실제 컴파일: 오류0. 기본 firebolt/스테이지1/seed1: 1800틱(30초), 처치19/생성23, 정산38 RAM, 종료 후 남은 적4, 실행 생략0. 동일 입력 재실행 해시2f6f26c6 일치.
- 스테이지2 기본 마법은29.5초 사망, 획득34→24 RAM 정산. 이후 단계에는 설계 강화가 필요하다.

### 실제 검증

| 항목 | 관측 결과 |
|---|---|
| 시간 종료 | 스테이지1에서 생존 적4가 남아 있어도 정확히30초에 종료. 종료 후 추가 Step으로 시간·해시 변경 없음, 마법 개체·적 투사체 제거 |
| 적 유입·단일 시전 | 첫 적 x1180, 플레이어 x220. 두 번째 슬롯 시전 거부. 일반 스테이지 적응 비활성화 |
| 강화 전후 | 기본 마법/스테이지2는29.5초 사망. 같은 그래프에 증폭 노드 연결+에너지1단계:30초 완주, 처치16·34 RAM·HP29.6 |
| 실제 앱 정산·재도전 | Result에서38 RAM, 최고완료1·다음2·노이즈 해금. 재도전은 스테이지1/시간0으로 재시작하고 보상 중복 지급 없음 |
| 성장·저장 | 시작 가격12/15/18, 구매 후 용량8·EN110·회복16/s·시간35초. RAM 부족 구매 거부 및3개 성장 단계 저장 재로드 성공 |
| 그래프 편집 | 증폭 배치·연결, Undo2회로 원상 복구, Redo2회로 연결 복구. RC1 가져오기 후 마법ID 유지. 마법 추가·복제·삭제 거부 |
| 편집 잠금 | 실제 Mission 상태에서 배치·삭제·이름 변경·다른 그래프 적용 모두 무효 |
| HUD 갱신 | 노드 배치·Undo 시 머리글 RAM3/6→2/6 즉시 반영. 한국어·시간·처치·보상·출격 단계·성장 카드 실제 Play 캡처 확인 |
| 저장 이전 | v1의4개 설계와 기존 성장·해금·설정을 보존하여 활성1개로 이전. 별도 v1 슬롯3 자료에서120 RAM 환불 확인. 검증 후 v2는 이전 상태와 JSON 동일하게 복구. v1 파일 SHA256 86FB2CF4A1CFF68252FE9BD04B3D2C1667A10B5F64FBC8D83BAC3DA59CF9850D 전후 동일 |
| 실행 상한 | 6중첩×반복6회는 E8로 거부,5중첩은 허용하여1800틱 실행 중 생략0. 400체 생성 요청에도 생존 적300 상한 유지. 시간 성장18단계·최대120초 상한 유지 |
| 자산 연결 | 활성 PoC 장면 Missing Script0, 글꼴 연결 존재, 장면 dirty=false, RuneCode 자산·폴더 누락 meta0, Resources JSON 구문 검사 통과 |
| Windows 빌드 | Succeeded / 오류0 / 경고1 / 105,148,071bytes / 11.246초. 경고는 기존 Pipeline의 Player 원격 제어 설정 부재이며 별도 원격 서버를 활성화하지 않음 |
| 실제 Player CLI | firebolt/incremental/stage1/seed1을2회 실행: 해시2f6f26c6,1800틱·19처치·38 RAM·실행 생략0. Editor와 해시 동일 |
| 강화된 Player CLI | 증폭 파이어 볼트/스테이지2/30초/용량1·에너지1:1800틱·16처치·34 RAM·해시2dae12cb·실행 생략0 |
| 기존 도크 보존 | firebolt/dummy_line/600:피해287.5·DPS28.75·EN290·최대개체3·노드실행87, Editor와 Player 일치 |
| Player 실행 시작 | 별도 숨김 그래픽 프로세스에서 Input System·Mono·D3D12 초기화 확인. 게임 예외·Missing 로그 없음. D3D12 정보 큐 조회 진단은 기존과 동일. 확인용 해당 프로세스 종료 |

### 인도 내용과 남은 항목

- 새 배포: `Builds/RuneCodePoC-Incremental-Windows.zip`. 압축 해제 후 `RuneCodePoC.exe` 실행. 이전 빌드와 ZIP 보존.
- 화면: `Builds/incremental-workshop.png`, `incremental-mission.png`, `incremental-result.png`, `incremental-upgrades.png`, `incremental-deploy.png`.
- 주요 변경: Core의 BalanceData/MissionData/GraphCompiler/RuneSimulation/SimulationTypes, Game의 RuneCodeApp/SimulationCli/RuneArenaGraphic, UI의 RuneCodeView, Save의 PlayerSave/SaveStore, Editor의 RuneCodeBuild.
- 설정·자료 변경: balance.json, strings.ko.json, 새 incremental.json 및 meta, 기존 UIFont 자산, tools/sim.ps1, README.md, DECISIONS.md, 본 진행 기록. 필요한 Inspector 연결 완료.
- 장기 스테이지·다양한 설계의 밸런스와 실제 화면 FPS/사람의 장시간 플레이는 미검증. 자동 전투는 기본 동작으로 적용했다.
- Git 변경 상태는 미확인. Git CLI·커밋·push 및 테스트 코드·자산 추가는 수행하지 않았다.


- 최종 배포 검사: ZIP190항목/38.71MiB, CRC 오류 없음, 필수 EXE·런타임·Core·자료 포함, DoNotShip 제외, ZIP 안 EXE 해시 일치. 이전 빌드·ZIP 및 기존 저장 보존 확인. SHA256: b6431bc432d928d76c35fbb28cb844d4482157aef267fd76c1e27d2d2ceed695.
- 추가 시간 강화 실행: 실제 Player의 증폭 마법·스테이지1·35초 설정은2100틱에 정상 완주.

## 수동 이동·조준 및 수식 커스텀 — 2026-10-07

이번 사용자 요청이 앞선 자동 조작 기록을 대체한다. 단일 마법·시간제 전투·RAM 정산·별도3종 성장은 유지한다.

- App: WASD 이동·마우스 조준으로 전환. 전투 영역의 포인터 방향으로 연속 시전하며 Space 대시와 Esc 일시정지를 연결했다. 자동 적 선택을 제거했다.
- Core/데이터: 사거리·속도 수식 추가, 확장 수식의 크기 조절. 노드마다0.5~3배·0.1단계·기본1.5배를 저장한다. 기존 비용·확장 효과·형태당3개 속성/수식 제한을 보존했다.
- UI: 노드 인스펙터의 숫자 입력과 −/+ 조절, 효과 설명, 형태의 최종 거리·반경·속도 표시. Undo/Redo 후 선택 파라미터도 현재 그래프 값으로 갱신한다.

### 실제 검증

| 항목 | 관측 결과 |
|---|---|
| 컴파일·연결 | Unity 컴파일 실패=false, PoC 장면 Missing Script0·글꼴 연결 존재·dirty=false |
| 실제 입력 경로 | Play 모드 Input System의 W+D 및 마우스 위치 이벤트를 생산 App.Update에 전달. 한 틱에 x+2.5927/y−2.5927 이동, 적이 오른쪽에 있어도 마우스의 왼쪽 위 방향으로 조준, 클릭 없이 마법 실행5노드 확인 |
| 일시정지·대시 | 일시정지 중 App.Update 호출 후 미션 시간 불변. Core의 이동+대시 입력 한 틱은 x+12.963·대시 쿨다운1초. Space 키의 프레임 전환을 포함한 사람 조작은 별도 플레이 확인 대상 |
| 독립 배율 | 속도2배:960px/s·0.7초·거리672px. 사거리2배:480px/s·2.8초·거리1344px. 이전 확장 노드는 반경9px로 기본1.5배 유지 |
| 실제 마법 개체 | 크기2배·속도2배 볼트의 개체 반경12px, 한 틱 이동16px 관측 |
| 네 형태 | 세 수식2배로 볼트 반경12/속도960/거리1344, 버스트 반경140/생성거리240, 오비트 반경16/궤도208/회전480°/s, 존 반경180/생성거리360. 버스트·존 속도에는W2 |
| 값·연결 경계 | 배율0.4·3.1·NaN은E9로 거부. 네 번째 속성/수식 연결은E5로 거부 |
| 편집·저장·공유 | 실제 + 버튼으로 사거리2→2.1, Undo로2 및 입력 표시 복구, Redo로2.1. 저장 재로드와RC1 왕복에서 배율 보존 |
| 기존 도크 | Editor firebolt/dummy_line/600/seed1:피해287.5·DPS28.75·EN290·최대개체3·실행87·해시e2fe49c3 |
| 화면 | 실제 Play 캡처 manualmods-editor.png에서 세 노드 연결·사거리 입력·−/+·한국어 설명 확인. manualmods-mission.png에서 수동 조작 안내·전투 HUD 확인 |

### 변경 파일과 남은 확인

- 소스: RuneCodeApp.cs, RuneData.cs, CompiledSpell.cs, GraphCompiler.cs, RuneCodeView.cs, RuneCodeBuild.cs.
- 데이터·자료: runes.json, strings.ko.json, 기존 UIFont.asset, tools/sim.ps1, README.md, DECISIONS.md, 본 기록. 새 Inspector 연결 작업은 없다.
- 수식 배율·에너지 비용의 장기 밸런스, 사람의 Space/마우스 연속 조작 및 화면 FPS는 미검증이다. 실제 입력 이벤트 경로와 Core 실행 결과는 위 관측 범위에서 확인했다.
- Git 변경 상태는 미확인. Git CLI·커밋·push 및 테스트 코드·Unity 테스트 자산 추가는 수행하지 않았다.

### 배포 검증

- 새 Windows x64 빌드: Succeeded / 오류0 / 경고1 / 105,155,131bytes / 10.866초. 경고는 기존 Pipeline의 Player 원격 제어 설정 부재이며, 게임 기능을 위해 해당 서버를 활성화하지 않았다.
- 실제 EXE의 커스텀 볼트/도크600틱: 피해193.5·EN292.008·개체최대1·실행115·해시81548248, Editor와 동일. 이전 파이어 볼트 회귀 결과도 피해287.5·해시e2fe49c3으로 동일.
- 커스텀 볼트/스테이지1의 비교용 자동 조준 CLI를2회 실행:1764틱 사망·11처치·22획득→15정산 RAM·해시d1c84752 동일·실행 생략0. 이 설계의 기본 에너지·피해 조건에서 완주를 보장하지 않으며 실제 수동 조작 플레이 결과는 아니다.
- 숨김 그래픽 Player의 Input System·Mono·D3D12 초기화 및 게임 예외/Missing 로그 부재를 확인했다. 기존 D3D12 정보 큐 조회 진단은 동일하다. 검증용으로 시작한 해당 프로세스만 종료했다.
- 배포는 `Builds/RuneCodePoC-ManualMods-Windows.zip`. `README.md`, `VERIFICATION.md`, 커스텀 설계 예시와 화면을 포함하며 이전 빌드를 보존한다.
- 검증 후 v2 저장은 시작 시 자료와 JSON 동일, v1은 바이트 동일로 확인했다. 기존22994 RAM·용량10·에너지6·최고완료1·선택2와 설계를 보존했다. v1 SHA256:86fb2cf4a1cff68252fe9bd04b3d2c1667a10b5f64fbc8d83bac3da59cf9850d.

- 최종 배포 검사: ZIP185항목/38.51MiB, CRC 오류 없음, 필수 EXE·런타임·Core·자료·화면 포함, DoNotShip 제외, ZIP 안 EXE 해시 일치. SHA256:17d00aa5c16e1954e5e784750c5b137a034c8bc3db15816a36423eb2f606198a.

## 구조 분리·테이블화 준비 — 2026-10-08

목표: UI와 마법 문법 엔진 분리, 테이블 기반 데이터 준비, 구조 재점검.

| 단계 | 구현 | 검증 |
|---|---|---|
| 상수 통합 | 포트·방향·종류·카테고리·형태·마법 타입 상수를 `SpellGrammar`로 모으고 UI·Presentation·편집 계층과 이벤트 포트 ID의 하드코딩 교체 | 컴파일 |
| 조회 이동 | `CompiledSpell.FindAction`, `SpellAction.Branches` 추가. 패널의 트리 재귀 제거 | 순수 하네스에서 템플릿5종 조회 확인 |
| 진단 | `CompileIssue`를 코드·상세·원인으로 바꾸고 문구는 `CompileIssueText`(Unity 계층)가 현지화 | 컴파일 |
| 자동 연결 | `SpellAutoConnect`(문법)로 이동, UI는 거리 정책만 담당. 컴파일러 규칙과 다르던 Inline Magic 효과 대상 누락 해소 | 컴파일 |
| 테이블 기반 | `RuneCode.Tables`(순수): CSV 파서·`DataTable`·`TableRow`·`TableErrorLog`·`ITableSource`. 규칙 문서 `docs/DATA_TABLES.md` | 순수 빌드, 손상 CSV에서 12건을 `파일:행:열`로 일괄 보고 |
| 룬 테이블 | `runes.json` → 6개 CSV 변환 후 삭제. `RuneCatalog.FromTables` | CSV 재조립 비교 0건, C# 로더 결과와 원본 비교 0건(float32) |
| 한도 분리 | `GrammarLimits`. 컴파일러는 `BalanceData` 대신 이 값만 받음 | 컴파일 |
| 직렬화 분리 | `SpellGraph` 순수화, `SpellGraphData`(JSON 키 동일) DTO, `SpellGraphValidator`, `ShareCodec`을 Core/Data로 이동, `PlayerSave._library`는 DTO + 직렬화 콜백, `Clone`은 깊은 복사 | 컴파일, 순수 하네스에서 Clone 결과 컴파일 동일 |
| 어셈블리 | `RuneCode.Grammar`(Core/Graph, `noEngineReferences`) ← `RuneCode.Core` | 문법 DLL 단독 빌드 후 나머지 빌드(경계 위반 4건 발견·수정), Unity 없이 템플릿5종 컴파일 성공 |

### 남은 확인 (Unity 필요)

- Unity 가져오기 후 새 `.meta` 생성, asmdef 3개 인식, Missing Script 없음.
- 기존 v2 저장 파일 로드·저장 후 `_library` JSON 키와 값 유지, 공유 코드 내보내기·가져오기 왕복.
- 도크 firebolt/dummy_line/600/seed1의 피해·EN·개체·실행 수가 이전 기록(287.5·290·3·87)과 같은지. 상태 해시는 같은 날 `onFirstHitOrExpire` 상태 필드 추가로 형식이 바뀌어 이전 값과 비교하지 않는다.
- 테스트 코드는 작성하지 않았다. 검증용 하네스는 저장소 밖 임시 폴더에서만 실행했다.
