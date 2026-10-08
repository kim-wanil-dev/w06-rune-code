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

## 마법 리메이크 최소 PoC — 2026-10-07

사용자 요청에 따라 PRD/PLAN의 전체 기능 중 T-06 행동·방어를 제외하고 연결 실행을 검증할 최소 범위를 구현했다. 이전 기록의 포인터 자동 시전을 클릭·홀드 입력으로 대체한다. 전체 P0~P7 완료를 의미하지 않으며 지원/보류 범위는 PLAN 첫 절에 기록했다.

### 구현 결과

- Core/Graph: 그래프 v2, 사건 포트, 트리거·제약 노드, 입력 파라미터, 유한 횟수·범위·사건 공급 검사, 단계 지불과 최악 예상 EN. v1 그래프와 RC1을 유지하고 새 설계는 RC2를 사용한다.
- Core/Simulation: 기존 고정 틱 실행을 확장했다. `RuneSimulation.Magic.cs`에 시전 계보·유지 취소·형태 관측·유한 사건 큐를 연결했다. 여섯 기본 형태, 사망/시간 경쟁, 다음 형태/시전 전체 강화, 실제 장벽 이동/적 투사체 차단과 영역 감속·흡입을 구현했다.
- Game/UI: 좌클릭 즉시 시전, 선택적 홀드 반복, 정지 차징, 홀드 유지. 영역 이탈·포커스 상실·종료 시 정리한다. 팔레트·포트·스크롤 인스펙터·실패 표시·사건 기록과 여섯 형태의 그림을 연결했다. 기존 이동·대시는 보존했다.
- Resources: 여섯 형태와 트리거/제약 정의, 세 강화 프로필, 한국어 설명, `poc_impact/mark/link/charge` 4개 템플릿 및 meta. PoC 버튼은 저장하지 않는 도크 미리보기로 기존 활성 설계를 보존한다.
- Save: v3 저장, v2/v1 읽기·이전. 기존 원본 파일을 덮어쓰지 않는다. 미리보기는 설계·튜토리얼 진행을 저장하지 않으며 일반 전투 출격을 차단한다.
- 빌드/자료: 기존 BuildWindows 및 sim.ps1의 새 출력 폴더 `Builds/RuneCodePoC-MagicRemake`; README/PLAN/본 진행 기록. 기존 장면과 Prefab 구조를 재사용하며 새 Inspector 수동 연결은 없다.

### 실제 검증

테스트 코드·테스트 자산을 추가하지 않고 기존 CLI와 일회성 Unity eval로 관측했다.

| 항목 | 관측 결과 |
|---|---|
| 컴파일·자산 연결 | Unity scriptCompilationFailed=false, PoC 장면 Missing Script 0, 글꼴 참조 존재, 장면 dirty=false. 네 신규 템플릿 컴파일 오류/경고 0 |
| 자원 교환 | 최대 EN100 단일 시전: Vector10 + Domain14 + 추가50 = 74 EN. 시작 EN80에서는 성공하여6 남음. EN69에서는 Vector10만 소비하고 성공 단계의64는 차감하지 않음 |
| 강화 범위 | 다음 형태는 Vector 피해50/반경21, 후속 Domain12/75. 전체 범위는 같은 Vector 이후 Domain30/225. 동일 강화가 후속마다 재곱해지지 않음 |
| HP·상태 제약 | HP100에서는 후속 실패/EN10, HP30에서는 Domain 피해18/총 EN24. 동결 없음은 실패/EN10, 동결 있음은 상태 소비 후 Domain 피해24/총 EN24. 반사 호출은 검증의 초기 HP/동결 설정에만 사용하고 그래프 실행은 기존 Step 경로로 확인 |
| 표식 경쟁·정리 | 빔이 적3명에 주입. 사망 시 각 가시1회, 이후300틱에도 시간 분기 중복 없음. 정상 시간 경로도 각 표식에서1회. Reset 시 개체·사건 기록 정리 |
| 네 단일 시전 | 최대 EN100, 입력 더미300틱: impact EN74/피해300, mark EN64/피해168, link EN28/피해100, charge 준비비 포함 EN26/피해220. link의 장벽은 해당 시점에 아직 수명 중 |
| 차징·유지 | 최소 미달/이동 취소는 준비 EN2만 소비·개체0. 완전 차징은 놓기 전 개체0, 놓은 뒤3배 가시 생성. 유지 해제 시 해당 계보 개체0 및 취소 기록 |
| 생산 입력 경로 | 실제 Input System MouseState를 큐에 넣고 InputSystem.Update → App.Update로 전달. 포인터만 있을 때0, 클릭 때 Vector10, 즉시 홀드 동안 Root1회. 실제 차징 홀드/해제는 준비2→가시14 EN. 영역 이탈 후 다시 진입한 홀드는 준비2만 소비·개체0으로 취소 유지 |
| 저장·공유 | 메모리의 v2→v3 이전 후 설계·재화 보존과 Validate 성공. RC1/RC2 왕복 문자열 동일·버전 일치. 현재 작업공간 UI 저장의 보유 RAM43 보존. 이전 기록의22994 저장 검증과 구분 |
| 화면 | `Logs/magic-trigger.png`에서 사건 연결·제약 노드·8개 트리거 파라미터와 도크 표시 확인. 긴 이름은 말줄임, 인스펙터는 스크롤로 접근 |
| 입력 설정 복구 | 검증용 임시 InputSettings의 배경/포커스 옵션을 기본값으로 복구. 저장된 설정 자산 없음, 프로젝트 입력 설정 파일 변경 없음. Play 종료 확인 |

### 완료 범위와 남은 확인

- T-06 및 PLAN의 보류 기능은 미구현이다. 일반 FirstOf/AllOfGroup, 자유 구간·중첩 강화, 무기 Aura·결계 핵·환경 반응 등 전체 PRD 확장은 후속 작업이다.
- 실제 사람의 장시간 클릭/홀드·회피 플레이, 프레임 FPS/p95, 성장·해금·전투 밸런스는 미검증이다. Input System 이벤트 경로 및 고정 틱 관측을 사람의 조작 검증으로 해석하지 않는다.
- Git 변경 상태는 미확인. Git CLI·커밋·push를 실행하지 않았다.

### 배포 검증

- Windows x64 빌드: Succeeded, 오류0, 경고1, 105,211,467bytes. 최종 증분 빌드는2.932초. 경고는 기존 Pipeline의 RuntimePipelineConfig 부재로 Player 원격 제어가 비활성화된다는 내용이다.
- 첫 동기 eval 빌드 중 Pipeline의5초 요청 제한으로 도구 오류2건이 기록되어 detached job으로 재빌드했다. 최종 빌드 결과 오류0을 확인했다. 입력 검증용 임시 InputSettings 교체 직후 Play 종료에 Input System 내부 Assertion1건이 있었으며 기본 설정 복구·다음 Play/Stop 재실행에서는 추가 오류0/경고0이다. 게임 소스 예외는 없었다.
- 실제 Windows Player와 Editor의 같은 CLI 입력/seed1/dummy_line/600틱 결과가 아래5개 모두 피해·EN·상태 해시 일치했다. 이는 자동 비교 입력이며 단일 클릭 결과와 구분한다.

| 템플릿 | 피해 | EN | 최대 개체 | 실행 노드 | 해시 | 실행 생략 |
|---|---:|---:|---:|---:|---|---:|
| poc_impact | 300 | 299 | 4 | 304 | 53e7722e | 0 |
| poc_mark | 300 | 290 | 13 | 421 | 672ecbc4 | 0 |
| poc_link | 300 | 224 | 4 | 78 | e7edcfb0 | 0 |
| poc_charge | 430 | 106 | 3 | 16 | 17cac6fe | 0 |
| firebolt | 287.5 | 290 | 3 | 87 | dfa7b371 | 0 |

- poc_mark Player 재실행의 해시672ecbc4 동일. 기존 firebolt의 피해·EN·최대 개체·실행 횟수는 이전 기록과 동일하다. 상태 해시는 신규 모듈 상태를 포함하도록 확장되어 이전 해시와 비교하지 않는다.
- 숨김 그래픽 Player의 Mono·Input System·D3D12 초기화 및 게임 예외/Missing/글꼴 경고 부재를 확인했다. 기존 D3D12 정보 큐 진단은 동일하다. 검증을 위해 시작한 해당 Player만 종료했다.
- 최종 재생/종료와 그래픽 Player 확인 전후 v1/v2/v3 저장의 SHA256 모두 동일했다. 이전 빌드·ZIP을 보존했다. 실행 파일은 `Builds/RuneCodePoC-MagicRemake/RuneCodePoC.exe`, 메트릭은 `Builds/magic-*.json`, 화면은 `Logs/magic-trigger.png`다.

### 주요 작업 파일

- Core/Data: RuneData.cs, GameData.cs. Core/Graph: SpellGraph.cs, CompiledSpell.cs, GraphCompiler.cs, ShareCodec.cs.
- Core/Simulation: RuneSimulation.cs, 신규 RuneSimulation.Magic.cs와 meta, SimulationTypes.cs.
- Game/UI: RuneCodeApp.cs, SimulationCli.cs, RuneArenaGraphic.cs, RuneCodeView.cs, RuneMesh.cs.
- Save/Editor/도구: PlayerSave.cs, SaveStore.cs, RuneCodeBuild.cs, tools/sim.ps1.
- 자산·자료: runes.json, strings.ko.json, 신규4종 spells JSON과 meta, 기존 UIFont.asset, README.md, PLAN_MAGIC_REMAKE.md, PROGRESS.md.

## 원래 프로젝트에 리메이크 적용 — 2026-10-08

- 적용 대상: `C:/Users/JUNGLE/Documents/GitHub/w06-rune-code`.
- 원본 Editor가 닫힌 상태에서 파일 내용을 비교했다. 신규10개(소스/JSON5개와 meta5개), 기존 변경23개로 총33개를 적용했다. 기존 파일의 줄바꿈과 meta/GUID를 보존했다. RuneGraphCanvas.cs의 사건 포트 색상 변경도 포함했다.
- 적용 전 기존 파일과 사용자 저장 백업: `C:/Users/JUNGLE/Documents/GitHub/w06-rune-code-backups/magic-remake-20261008-010942`. `before`에 기존 파일, `userdata-before`에 저장 파일, `manifest.json`에 신규/변경 경로와 해시를 기록했다.
- 적용 대상 외215개 파일의 해시를 전후 비교하여 동일함을 확인했다. 기존 장면, Prefab, Packages, ProjectSettings와 기존 빌드는 보존했다. Git 변경 상태는 미확인이며 Git CLI·커밋·push를 실행하지 않았다.
- 원본 Unity6000.3.22f1에서 실제 컴파일 실패=false, Missing Script0, 앱/글꼴 참조 연결, 장면 dirty=false. 네 PoC 템플릿 오류/경고0. Play/Stop 후 Console 오류/경고0.
- 원본 작업실에서 네 PoC 도크 미리보기와 사건 실행을 확인했다. 닫은 뒤 기존 설계 문자열과 보유 RAM이 동일했다. v1/v2/v3 저장 SHA256도 검증 전후 동일했다. 수동 Inspector 작업은 없다.
- 원본 Editor의600틱 CLI에서 네 PoC 및 firebolt의 피해·EN·상태 해시가 이전 검증과 모두 일치했다. 원본 경로의 Player poc_mark도 피해300/EN290/해시672ecbc4/실행 생략0으로 일치했다.
- 검증한 Windows 배포 폴더를 원본의 `Builds/RuneCodePoC-MagicRemake`에 복사했다.179개 파일의 해시 일치를 확인했으며 이번 적용에서 원본 빌드를 다시 생성하지 않았다. 사람의 장시간 조작·밸런스·성능 확인 범위는 이전 PoC 기록과 같다.
