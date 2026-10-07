# CRD — 룬 코드 (RUNE CODE, 가제)

> **버전** v0.1 (2026-10-07) · **독자** Codex 에이전트(구현), 기획자(검토)
> **범위** MVP = 버티컬 슬라이스(섹터 1개 + 보스 1체). 이 문서에 없는 기능은 만들지 않는다.
> **한 줄 요약** 고대 룬을 코드 블록처럼 노드로 연결해 나만의 마법을 설계하고, 모든 것을 학습한 AI와의 실전에서 시험하고 성장시키는 2D 탑다운 액션.

---

## 1. 에이전트 작업 규칙 (가장 먼저 읽을 것)

1. §9의 마일스톤을 순서대로 진행한다. 현재 마일스톤 밖의 기능은 구현하지 말고 `docs/BACKLOG.md`에 한 줄로 적는다.
2. 명세가 모호하면 **§2.2 핵심 경험에 가장 부합하는 가장 단순한 해석**을 택해 구현하고, 결정을 `docs/DECISIONS.md`에 한 줄 기록한 뒤 계속 진행한다. 작업을 멈추고 질문하지 않는다.
3. 모든 커밋은 `npm run check`(typecheck + lint + test)를 통과해야 한다. 마일스톤 단위로 커밋/PR을 나눈다.
4. `src/core`는 순수 로직이다. DOM·Canvas·React import 금지, `Math.random`·`Date.now` 금지(시드 RNG와 tick만 사용). 전투·적 AI·미션 진행 같은 **게임 규칙은 모두 core**에 두고, `game/`은 입력과 렌더링만 맡는다.
5. 모든 수치는 `src/data/*.json`에서 읽는다(코드 하드코딩 금지). 이 문서의 수치는 **초기값**이며 튜닝 대상이다.
6. 허용 의존성: `react` `react-dom` `zustand` `@xyflow/react` `zod` / dev: `vite` `typescript` `vitest` `eslint` `prettier` `tsx`(및 각 타입·플러그인 패키지). M0에서 일괄 설치하고 lockfile을 커밋한다. 추가가 필요하면 사유를 DECISIONS.md에 남긴다.
7. 외부 에셋 금지. 모든 그래픽은 도형·텍스트 플레이스홀더로 만든다.
8. 사용자에게 보이는 문자열은 `src/data/strings.ko.json`에 둔다.
9. core는 테스트 먼저 작성한다. §10의 오라클 예시는 그대로 테스트로 옮긴다.

---

## 2. 제품 개요

### 2.1 시놉시스

- 약 50년 뒤(2076년경), AI가 인류를 지배한다. AI는 인류의 기존 데이터를 전부 학습해 **기존의 공격 방식이 통하지 않는다**.
- 주인공은 우연히 **고대 마법 원리서**를 얻고, 그 원리를 현대 기술에 접목해 **RAM이 박힌 지팡이(Wand)**를 만든다. 룬(마법 원리)을 코드 블록처럼 연결하면 마법이 실행된다.
- AI를 쓰러뜨리면 **RAM 조각**을 얻고, 이것으로 지팡이를 강화하고 마법을 확장한다.
- 톤: 사이버펑크 폐허 + 네온 룬 문양·회로. (MVP는 도형 플레이스홀더)

### 2.2 핵심 경험 — 모든 판단의 기준

1. **나만의 마법을 쓰는 신선함**: 모든 마법은 플레이어가 노드로 만든 그래프다. 프리셋도 편집 가능한 템플릿일 뿐이다.
2. **마법을 성장시키는 재미**: 성장은 단순 수치 상승이 아니라 ① 용량(RAM) ② 어휘(새 룬) ③ 이해(적응을 읽고 재설계)로 체감된다.

### 2.3 설계 원칙

- **설계 → 시험 → 실전 루프는 10초 이내**: 에디터에 도킹된 시험 도크에서 즉시 시험한다.
- **읽히는 마법**: 발동 시 실행된 노드가 순서대로 하이라이트된다(디버거 감각).
- **제약이 창의성을 만든다**: RAM(크기)·에너지(비용)·적응(반복 억제)이 서로 다른 방향에서 설계를 압박한다.
- **적응은 벌이 아니라 초대**: 같은 해법을 반복하면 약해지지만, 상태는 항상 보이고 대안은 항상 있다.

---

## 3. 범위

**MVP 포함**: 작업실(에디터·벤치·출격), 섹터 1(전투방 4 + 터미널 + 보스방), 룬 19종, 적 5종 + 보스 1체, AI 적응, 저장.

**MVP 제외**: 멀티플레이, 절차적 맵, 오디오, 컷신(텍스트 배너 정도만), 모바일·게임패드, 온라인 공유 서버, 원소 반응, 빔·벽·소환 형태, 차지·패시브 코어, 룬 숙련, RAM 모듈 아이템화, 업적·랭킹.

---

## 4. 기술 스택·아키텍처

- **플랫폼(가정)**: 웹 브라우저, 데스크톱, 키보드+마우스. 엔진이 따로 정해지지 않아 에이전트가 직접 검증하기 쉬운 웹/TypeScript로 가정한다. 다른 엔진으로 옮길 경우 §5·§6·§8이 그대로 명세 역할을 한다.
- **스택**: TypeScript(strict) · Vite · React + Zustand(UI 상태) · `@xyflow/react`(노드 에디터) · Canvas 2D(게임·시험 도크 렌더, 라이브러리 없이 직접 구현) · zod(데이터·세이브 검증) · Vitest.
- **시뮬레이션**: 60Hz 고정 타임스텝, 렌더와 완전 분리, 결정적(시드 RNG `mulberry32`). 게임 루프는 프레임당 최대 5스텝.
- **화면**: 논리 해상도 1280×720(레터박스 스케일), 타일 32px, 방 1개 = 40×22 타일 고정 카메라(스크롤 없음). 상하 여백은 HUD.
- **의존 방향**: `ui`, `game` → `core`. 역방향은 린트로 차단한다.
- **명령어**: `npm run dev | build | test | lint | typecheck | check | sim`

```
src/
  core/        순수 로직 (UI·렌더 의존 금지)
    runes/     룬 정의 로더·레지스트리
    graph/     그래프 모델·검증·컴파일·공유 코드
    sim/       고정 스텝 시뮬, 엔티티, 전투, 상태이상, 적 AI
    adapt/     AI 적응
    mission/   방·웨이브·드롭·진행 상태
    rng.ts
  data/        runes.json enemies.json balance.json strings.ko.json
               spells/*.json  stages/*.json  bosses/*.json
  game/        입력, Canvas 렌더러, 게임 루프 (core를 구동하고 그린다)
  ui/          editor/ bench/ hud/ 앱 셸
  save/        localStorage 저장·마이그레이션
tools/         sim-cli.ts   헤드리스 시뮬레이션 (검증·밸런싱)
docs/          CRD.md  DECISIONS.md  BACKLOG.md
```

---

## 5. 게임 규칙 명세

### 5.1 마법 그래프 모델

마법(Spell) = 노드(룬)와 엣지로 이루어진 그래프. 포트는 두 종류다: `exec`(실행 흐름), `mod`(수식·속성 부착).

| 계열 | 룬 | 입력 포트 | 출력 포트 |
|---|---|---|---|
| Core | 시전 | – | `exec` |
| Form | 볼트·버스트·오비트·존 | `exec`(1), `mod`(최대 3) | `onHit`, `onExpire` (둘 다 exec) |
| Element / Modifier | 속성·수식 | – | `mod` (여러 Form에 연결 가능) |
| Flow | 지연·반복 | `exec` | `next` / `body` |
| Flow | 조건 | `exec` | `then`, `else` |
| Action | 점멸·방벽 | `exec` | `next` |

- `exec` 입력은 엣지를 1개만 받는다. `exec` 출력은 여러 엣지에 연결할 수 있으며 이는 **병렬 실행**이다.
- `mod` 출력은 여러 Form에 연결할 수 있다. 룬 1개를 재사용해 RAM을 아끼는 것이 "최적화"의 재미 요소다.
- Core는 마법당 정확히 1개, 삭제 불가, RAM 0. 새 마법을 만들면 자동 생성된다.
- 배치된 모든 노드(연결 여부와 무관)가 RAM을 점유한다.
- 연결 시도 단계에서 규칙 위반을 차단하되(E2·E3·E5), 가져오기 데이터 검증을 위해 컴파일에서도 같은 코드로 검출한다.

**검증 코드** — 에러(E)는 장착·시전 불가, 경고(W)는 허용.

| 코드 | 내용 |
|---|---|
| E1 | Core 없음 또는 2개 이상 |
| E2 | `exec` 입력에 엣지 2개 이상 |
| E3 | 포트 종류 불일치 |
| E4 | 순환 |
| E5 | Form의 수식 슬롯(3) 초과 |
| E6 | 한 Form에 속성 룬 2개 이상 |
| E7 | RAM 용량 초과 (장착 불가) |
| E8 | energyCost가 최대 에너지 초과 (시전 불가) |
| E9 | 파라미터 범위 위반 |
| E10 | 미해금 룬 포함 (열람·편집 가능, 장착 불가) |
| W1 | Core에서 도달할 수 없는 노드 (RAM은 점유) |
| W2 | 효과 없는 수식 (예: 버스트에 관통) |
| W3 | 대상이 필요한 조건 노드가 `onHit` 하위 경로 밖에 있음 (항상 false) |
| W4 | 어디에도 연결되지 않은 속성·수식 |

### 5.2 컴파일 (그래프 → CompiledSpell)

`compile(graph, { runes, balance, unlocked })` → `{ ok, errors, warnings, spell? }`. 타입은 §8 참조.

- `ramUsed` = 배치된 모든 노드의 RAM 합. `signature` = 노드 좌표를 제외한 정규화 그래프의 안정 해시.
- 수식은 데이터에 정의된 곱/합 규칙으로 Form 스탯에 적용한다. 같은 수식을 한 Form에 두 번 부착할 수 없다.
- **에너지 비용은 최악의 경우를 시전 시 선지불하며 환불하지 않는다.**
  - `spawn` = `own + count × ( min(maxHits, 3) × cost(onHit) + cost(onExpire) )`
  - `own` = `form.energy × Π(수식 energyMult) + 속성 energy`
  - `maxHits`: 볼트는 `1 + 관통 수`, 그 외 Form은 3. `count`: 다중 등으로 늘어난 개체 수.
  - `delay` = cost(then) · `repeat` = times × cost(body) · `if` = max(then, else) · `blink`/`shield` = 자체 에너지 + cost(next)
- `cooldown` = `0.25 + 0.03 × ramUsed` 초.
- `worstCaseEntities`도 같은 구조로 계산한다.
- 의도: 큰 마법일수록 쿨다운·에너지가 늘어 "RAM만 크면 무조건 강한" 상황을 막는다. 에너지가 최대치를 넘는 마법은 시전 자체가 불가(E8)하므로 폭주 마법의 자연스러운 상한이 된다.

### 5.3 런타임 실행

- **시전**: 슬롯 입력 시 `cooldown ≤ 0` 이고 `energy ≥ energyCost`이면 비용을 차감하고 쿨다운을 시작한다. 입력을 누르고 있으면 쿨다운마다 연사한다. 시퀀스(`delay`·`repeat`)는 비동기로 계속 진행되며 쿨다운을 막지 않는다.
- **컨텍스트**: `{ origin, dir, owner, target?, fromEvent }`. 최초 컨텍스트 = 완드 끝 / 조준 방향 / target 없음 / fromEvent=false.

| Action | 동작 |
|---|---|
| `spawn` bolt | origin에서 `count`발 발사(다중이면 부채꼴). 벽에 닿으면 소멸, 적에 닿으면 `onHit` 후 소멸(관통이면 계속 진행) |
| `spawn` burst | `origin + dir × offset`(fromEvent이면 origin)에서 즉시 범위 피해 1회. `onHit`은 가까운 순 최대 3체, 직후 `onExpire` 호출 |
| `spawn` orbit | 앵커 주위를 회전. 앵커 = 시전자(Core 유래) 또는 이벤트 위치에 고정(이벤트 유래) |
| `spawn` zone | 위치에 지속 장판 생성. `onHit`은 대상별 최초 접촉 시 |
| `delay` | `seconds` 뒤 `then` 실행(컨텍스트 스냅샷 유지) |
| `repeat` | `interval`초 간격으로 `times`회 `body` 실행 |
| `if` | 조건 평가 후 `then`/`else` 실행. 대상이 필요한 조건인데 target이 없으면 false |
| `blink` | Core 유래: 조준 방향으로 `distance`만큼 이동(벽에서 정지). 이벤트 유래: 이벤트 위치로 이동(불가 위치면 가장 가까운 이동 가능 지점). 이후 `next`를 갱신된 origin으로 실행 |
| `shield` | 시전자에게 보호막 부여(수치는 룬 데이터), 이후 `next` 실행 |

- **이벤트**: `onHit`(엔티티당 최대 3회) · `onExpire`(수명 종료, 적중 후 소멸, 버스트 직후 등 엔티티가 사라질 때). 자식 컨텍스트 = origin: 이벤트 위치 / dir: 이벤트 시점 진행 방향 / target: 피격 적(onHit) 또는 없음(onExpire) / fromEvent=true.
- **속성은 상속되지 않는다.** 자식 Form은 자기 속성 룬이 필요하다(단, `mod` 출력 팬아웃으로 룬 1개를 공유할 수 있다).
- 실행된 노드는 `onNodeExecuted(nodeId, tick)` 이벤트로 방출한다(에디터 하이라이트·텔레메트리용).
- 안전장치: 동시 존재 스펠 엔티티가 `limits.maxLiveSpellEntities`(128)를 넘으면 신규 생성을 무시한다.

### 5.4 전투 규칙

- **피해 파이프라인**: `기본 피해 × 수식 배율 × adaptMult(§5.5) × 방어 보정` → 적용 → 상태이상 부여 → 피해 숫자 표시.
- **상태이상**(데이터 정의): `burn` 초당 3(0.5s 틱, 3s, 재부여 시 갱신) · `chill` 스택당 이속 −30%(2.5s, 적중마다 갱신, 최대 3스택) · `freeze` chill 3스택 도달 시 1.0s 정지 후 4s 빙결 면역 · `emp` 적 실드 2s 무력화.
- **이지스 방패**: 전방 120° 안에서 들어오는 `bolt` 피해 −70%. burst·orbit·zone은 영향 없음. `emp` 동안 비활성.
- **릴레이**: 반경 200px 아군의 받는 피해 −20%. 릴레이 생존 중 적응 학습량 ×1.5(전역).
- **플레이어**: HP 100 · 이동 220px/s · 대시(Space) 140px/0.18s 무적, 쿨다운 1.0s · 피격 후 무적 0.5s · 에너지 100(초당 15 회복).

### 5.5 AI 적응 시스템

세계관 규칙("AI는 모든 데이터를 학습했다")을 게임 규칙으로 구현한다. 미션마다 `AdaptationNet`(네트워크 공유 학습값)이 0에서 시작한다.

- **태그**: 속성 `fire ice arc raw` + 형태 `bolt burst orbit zone`. 플레이어 마법의 피해 1회는 자신의 속성 태그와 형태 태그를 각각 학습시킨다.
- **학습**: `net[속성] += 0.02 × w`, `net[형태] += 0.01 × w`
  - `w` = 직접 적중 1.0 / 지속 피해·존 틱 0.25. 여기에 노이즈 수식이 붙은 개체는 ×0.3, 릴레이 생존 중에는 ×1.5.
- **상한**: 속성 0.60, 형태 0.30.
- **감소**: 해당 태그를 4초간 쓰지 않으면 이후 초당 0.03씩 감소(최소 0).
- **효과**: `adaptMult = (1 − net[속성]) × (1 − net[형태])`.
- **표시** [Must]: HUD 우측에 8개 태그의 학습도 막대(0이면 숨김), 학습도 0.3 이상인 태그로 맞은 적 위에 내성 아이콘, 결과 화면에 "AI 학습 리포트"(상위 3개 태그와 대안 제안 문구).
- **보스 패치**: §6.4. **시험 도크**: 적응 ON/OFF 토글·값 표시·리셋.
- 의도: 같은 마법을 반복하면 약해져 재설계를 유도하지만, 노이즈 룬과 다른 속성·형태 조합이라는 출구가 항상 있다.

### 5.6 성장·경제

- 통화는 **RAM 조각**(이하 조각). 적 처치 시 오브가 떨어지고 플레이어 160px 이내에서 자동 흡수된다. 방 클리어 시 남은 오브는 자동 회수된다.
- 정산: 클리어 100%, 사망 70%(반올림). 섹터는 반복 플레이 가능.
- **성장 축 3개**: ① RAM 용량 ② 어휘(룬 해금) ③ 슬롯·기초 능력(스펠 슬롯, 최대 에너지, 최대 HP).
- **RAM 용량 규칙**: `ram.mode = "shared"`(기본)는 장착한 모든 마법의 `ramUsed` 합 ≤ 지팡이 용량. `"perSpell"`은 마법마다 ≤ 용량. 보관함에만 있는 마법은 용량을 쓰지 않는다.
- **시작 상태**: 용량 6, 슬롯 2개(A·B), 시작 룬 7종(Core 포함), 시작 마법 2종 장착 + 보관함 템플릿 1종.
- **목표 경제**: 섹터 1 첫 클리어 보상(약 120)으로 '용량 3단계(95)' 또는 '용량 2단계 + 룬 2종(약 110)' 같은 **선택**이 생겨야 한다.

---

## 6. 콘텐츠 명세

### 6.1 룬 19종

| id | 이름 | RAM | 에너지 | 효과·수치 (초기값) | 해금 |
|---|---|---|---|---|---|
| `core.cast` | 시전 | 0 | 0 | 루트. 시전 시 실행 시작 | 시작 |
| `form.bolt` | 볼트 | 1 | 8 | 투사체. 피해 10 · 속도 480px/s · 반경 6 · 수명 1.4s | 시작 |
| `form.burst` | 버스트 | 1 | 10 | 즉시 원형 범위. 피해 14 · 반경 70 · offset 120px | 시작 |
| `form.orbit` | 오비트 | 2 | 14 | 회전 구체 2개. 접촉 피해 6(대상별 0.4s 간격) · 궤도 반경 52 · 240°/s · 수명 4s | 벤치 45 |
| `form.zone` | 존 | 2 | 16 | 지속 장판. 틱 피해 4(0.5s) · 반경 90 · 수명 4s · offset 180px | 벤치 35 |
| `elem.fire` | 화염 | 1 | +2 | 피해 속성 fire, `burn` 부여 | 시작 |
| `elem.ice` | 냉기 | 1 | +2 | 피해 속성 ice, `chill` 부여 | 벤치 25 |
| `elem.arc` | 전격 | 1 | +2 | 피해 속성 arc, 반경 110 내 다른 적 2체에 50% 연쇄, `emp` 부여 | 벤치 40 |
| `mod.amplify` | 증폭 | 1 | ×1.4 | 피해 ×1.5 | 시작 |
| `mod.multi` | 다중 | 1 | ×1.8 | 개체 3개(볼트=부채꼴 ±12°, 버스트·존=삼각 배치, 오비트=구체 4개), 개체당 피해 ×0.7 | 시작 |
| `mod.pierce` | 관통 | 1 | ×1.2 | 볼트 전용. +2 관통, 관통마다 피해 −10% | 벤치 30 |
| `mod.homing` | 유도 | 1 | ×1.2 | 볼트 전용. 선회 140°/s, 탐지 300px | 벤치 50 |
| `mod.expand` | 확장 | 1 | ×1.2 | 크기·반경 ×1.5, 피해 ×0.85 | 벤치 30 |
| `mod.noise` | 노이즈 | 1 | ×1.0 | 시전마다 속성이 보유 속성 중 무작위로 바뀜. 이 개체의 학습 증가량 ×0.3 | 보스 보상 |
| `flow.delay` | 지연 | 1 | 0 | 0.1~3.0s (기본 0.5) | 시작 |
| `flow.repeat` | 반복 | 1 | ×N | 2~6회 · 간격 0.1~1.0s (기본 3회/0.15s) | 벤치 40 |
| `flow.if` | 조건 | 1 | max | 조건 택1: 대상 HP < % / 대상 상태 보유 / 대상 거리 < px / 자신 HP < % | 벤치 60 |
| `act.blink` | 점멸 | 1 | 6 | 이동 거리 140~220 (기본 160) | 벤치 45 |
| `act.shield` | 방벽 | 1 | 8 | 보호막 20, 3s | 벤치 45 |

시작 룬 7종: `core.cast` `form.bolt` `form.burst` `elem.fire` `mod.amplify` `mod.multi` `flow.delay`.
시작 마법(`src/data/spells/`): **파이어 볼트**(Core→볼트[화염], 장착 A) · **충격파**(Core→버스트[증폭], 장착 B) · **삼연 화염**(Core→볼트[화염·다중], 보관함 템플릿).

### 6.2 적

| id | 이름 | HP | 이동 | 공격·특성 | 보상(조각) |
|---|---|---|---|---|---|
| `enemy.scout` | 스카우트 | 20 | 140 | 접촉 8(1s 쿨). 4~6체 무리 | 1 |
| `enemy.sentry` | 센트리 | 40 | 고정 | 2.0s마다 0.5s 예고 후 3연탄(탄당 8) | 3 |
| `enemy.hunter` | 헌터 | 60 | 110 | 거리 160 이내에서 0.3s 예고 후 돌진(280px/s, 0.25s, 피해 15) | 3 |
| `enemy.aegis` | 이지스 | 70 | 70 | 방패 밀치기 12. 전방 방패(§5.4) | 4 |
| `enemy.relay` | 릴레이 | 90 | 고정 | 공격 없음. 오라(§5.4). 우선 처치 대상 | 8 |
| `boss.governor` | 거버너 | 800 | 60 | §6.4 | 60 |

### 6.3 섹터 1 — 폐허가 된 데이터센터 지구

진행: `R1 → 터미널 → R2 → 터미널 → R3 → 터미널 → R4 → 터미널 → 보스`

| 방 | 구성 | 의도 |
|---|---|---|
| R1 | 스카우트 6 (3+3 웨이브) | 기본 조작. 볼트+화염으로 충분 |
| R2 | 헌터 3 + 센트리 1 | 이동·회피 |
| R3 | 이지스 2 + 스카우트 6 (3+3) + 센트리 1 | 볼트가 막힘 → 버스트·존·전격 등 재설계 유도 |
| R4 | 릴레이 1 + 헌터 2 + 이지스 2 + 스카우트 4 | 릴레이 우선 처치, 적응 가속 체험 |
| 보스 | 거버너 + 증원 | §6.4 |

- 보상 합계(증원 제외) 121조각.
- **터미널**(안전지대): HP 30% 회복, 에너지 충전, **에디터를 모달로 열 수 있다**. 전투 중에는 에디터를 열 수 없다.
- 방 데이터는 ASCII 타일맵: `#` 벽 · `.` 바닥 · `P` 플레이어 시작 · `D` 문 · `T` 터미널 · `1`~`9` 스폰 포인트(웨이브 데이터가 참조).

### 6.4 보스 — 거버너 (HP 800)

- 3페이즈(HP 100~67% / 67~34% / 34~0%). 패턴은 `src/data/bosses/governor.json`으로 정의한다.
  - P1: 8방향 탄막(3s 주기) + 12s마다 스카우트 4체 증원
  - P2: 조준 3연탄 + 바닥 위험 장판(1s 예고) 추가
  - P3: 이동·발사 속도 ×1.3, 모든 패턴 혼합
- **패치 연출(핵심)**: 67%·34%에서 2.5초 무적 + "PATCH 적용 중" 배너 → 그때까지 **가장 많은 피해를 준 속성 1개와 형태 1개**의 학습값을 상한으로 올리고 보스전 동안 고정(감소 없음).
- 의도: 한 가지 빌드로 밀어붙이면 막히고, 장착 마법을 서로 다른 속성·형태로 구성한 플레이어가 유리해야 한다. 이것이 "AI는 이미 학습했다"는 세계관의 클라이맥스다.
- 보상: 조각 60 + `mod.noise` 해금(첫 클리어 한정).

### 6.5 벤치 카탈로그

| 항목 | 단계 | 비용(조각) |
|---|---|---|
| RAM 용량 +2 (6 → 최대 24) | 9 | 20 · 30 · 45 · 65 · 90 · 120 · 160 · 210 · 270 |
| 스펠 슬롯 C (Q키) | 1 | 120 |
| 최대 에너지 +10 | 5 | 30 · 40 · 55 · 70 · 90 |
| 최대 HP +10 | 5 | 30 · 40 · 55 · 70 · 90 |
| 룬 해금 | 11종 | §6.1 표의 "벤치" 비용 |

---

## 7. UI/UX

### 7.1 화면 흐름

타이틀 → **작업실**(탭: 에디터 / 벤치 / 출격) → 미션 → 결과(획득 조각, AI 학습 리포트) → 작업실. 세이브 초기화 버튼을 설정에 둔다.

### 7.2 에디터

- **상단 바**: RAM(장착 합계/용량, 마법별 분해 표시) · 에너지 비용 · 쿨다운 · 예상 DPS [Should: 도크 시뮬 기반].
- **슬롯 탭** A·B·C(미해금 슬롯은 잠금) · **보관함**(저장·불러오기·복제·삭제·이름 변경, 최대 30개) · **공유 코드**(복사/붙여넣기, `RC1.` + base64url(JSON), 가져올 때 zod+컴파일 검증) · **▶ 시험** 버튼.
- **좌측 팔레트**: 계열 탭 + 검색. 미해금 룬은 실루엣과 해금 조건을 보여준다(어휘 확장의 동기).
- **중앙 캔버스**: 이동·줌, 드래그 배치, 포트 드래그 연결(유효 포트 하이라이트, 무효는 붉게), 다중 선택, 복사·붙여넣기·복제, Undo/Redo(50단계), 자동 정렬.
- **우측 인스펙터**: 선택 노드의 파라미터 + 에러/경고 목록(클릭 시 해당 노드로 포커스).
- 장착 중인 마법 편집 시 RAM 초과가 되는 노드 배치는 막는다(노드 흔들림 + 메시지). 보관함 전용 마법은 초과 가능(장착만 불가).
- 노드 표시: 계열별 색 + 고유 도형(색각 이상 대응), RAM 배지, 호버 시 계산된 스탯 툴팁.
- [Should] 빈 곳 더블클릭 → 빠른 검색 배치 / 수식·속성을 Form 근처에 놓으면 자동 부착.

### 7.3 시험 도크

- 에디터에 도킹된 샌드박스 캔버스(독립된 core 시뮬 인스턴스). 편집하면 **자동 재컴파일**(디바운스 200ms)되어 유효하면 즉시 시험할 수 있다.
- 시나리오: `dummy_single`(HP 100, 무반격) · `dummy_line`(5체 일렬) · `dummy_swarm`(10체 이동) · `aegis`(방패) · `adapt_loop`(무한 리스폰 + 적응 ON).
- 조작: 마우스 조준·클릭 시전, 1·2·3 = 슬롯 A/B/C, 자동 발사 토글, 리셋(R), 속도 0.5×/1×/2×.
- 표시: 피해 숫자, 누적 피해, 5초 롤링 DPS, 에너지 사용량, 엔티티 최대치, 적응값. **실행된 노드가 에디터에서 200ms 하이라이트**된다 [Must].
- [Should] 터미널에서 열면 현재 미션의 적응값을 불러와 시험할 수 있다.

### 7.4 HUD

좌상단 HP·에너지 바 · 하단 중앙 슬롯 A/B/C(아이콘·쿨다운·에너지 비용) · 우상단 AI 학습도 패널 · 조각 획득 플로팅 텍스트 · 보스 HP 바.

### 7.5 조작

WASD 이동 · 마우스 조준 · 좌클릭 슬롯 A · 우클릭 슬롯 B · Q 슬롯 C · Space 대시 · E 터미널 상호작용 · Esc 일시정지. (키 리바인딩은 Could)

### 7.6 피드백 [Must, 경량]

속성별 색·모양, 투사체 잔상, 적중 플래시, 피해 숫자, 상태이상 아이콘, 처치 시 조각 오브, 에디터 노드 실행 하이라이트. 히트스톱·화면 흔들림은 [Should]이며 설정에서 끌 수 있어야 한다.

---

## 8. 데이터 스키마 (참조용 — 의미는 유지하되 세부는 다듬어도 된다)

```ts
type RuneCategory = 'core' | 'form' | 'element' | 'modifier' | 'flow' | 'action';
type PortKind = 'exec' | 'mod';
type Element = 'fire' | 'ice' | 'arc' | 'raw';
type FormKind = 'bolt' | 'burst' | 'orbit' | 'zone';

interface PortDef { id: string; kind: PortKind; dir: 'in' | 'out'; max?: number }
type ParamDef =
  | { id: string; kind: 'number'; min: number; max: number; step: number; default: number }
  | { id: string; kind: 'enum'; options: string[]; default: string };

interface RuneDef {
  id: string;                        // 'form.bolt'
  category: RuneCategory;
  ram: number;                       // RAM 점유
  energy: number;                    // 기본 에너지
  energyMult?: number;               // 수식·반복용 배율
  ports: PortDef[];
  params?: ParamDef[];
  stats: Record<string, number>;     // 피해·속도·반경 등 (튜닝 대상)
  tags: string[];                    // 적응 태그: 'fire', 'bolt' ...
  unlock: { type: 'start' } | { type: 'bench'; cost: number } | { type: 'reward'; source: string };
}

interface GraphNode { id: string; runeId: string; pos: { x: number; y: number }; params?: Record<string, number | string> }
interface GraphEdge { id: string; from: { node: string; port: string }; to: { node: string; port: string } }
interface SpellGraph { id: string; name: string; version: 1; nodes: GraphNode[]; edges: GraphEdge[] }

type Condition =
  | { type: 'targetHpBelow'; pct: number }
  | { type: 'targetHasStatus'; status: 'burn' | 'chill' | 'freeze' | 'emp' }
  | { type: 'targetDistanceBelow'; px: number }
  | { type: 'selfHpBelow'; pct: number };

type Action =
  | { kind: 'spawn'; nodeId: string; form: FormKind; element: Element; noise: boolean; count: number;
      stats: Record<string, number>; mods: string[]; onHit: Action[]; onExpire: Action[] }
  | { kind: 'delay'; nodeId: string; seconds: number; then: Action[] }
  | { kind: 'repeat'; nodeId: string; times: number; interval: number; body: Action[] }
  | { kind: 'if'; nodeId: string; cond: Condition; then: Action[]; else: Action[] }
  | { kind: 'blink' | 'shield'; nodeId: string; params: Record<string, number>; next: Action[] };

interface Issue { code: string; nodeId?: string; message: string }
interface CompiledSpell {
  signature: string; ramUsed: number; energyCost: number; cooldown: number;
  worstCaseEntities: number; tags: string[]; root: Action[];
}
interface CompileResult { ok: boolean; errors: Issue[]; warnings: Issue[]; spell?: CompiledSpell }

interface SaveDataV1 {                // localStorage 키: 'runecode.save.v1'
  version: 1;
  wand: { capacityLevel: number; slotCount: number; energyLevel: number; hpLevel: number };
  currency: { ram: number };
  unlockedRunes: string[];
  library: SpellGraph[];              // 최대 30
  loadout: (string | null)[];         // 슬롯별 library id
  progress: { sector1Cleared: boolean; killCounts: Record<string, number> };
  settings: { screenShake: boolean; hitStop: boolean };
}
```

`balance.json` 최상위 키: `player` · `limits`(`maxLiveSpellEntities`, `hitTriggerCap`) · `ram`(`mode`) · `adaptation` · `economy`.

---

## 9. 마일스톤 & 완료 조건

각 마일스톤은 독립 PR로 병합 가능해야 하며 이전 마일스톤의 테스트를 깨뜨리지 않는다.

### M0 — 부트스트랩
- 산출물: Vite+TS+React 골격, §4 폴더 구조, npm scripts, ESLint(core 격리·`Math.random`/`Date.now` 금지), Prettier, Vitest, `AGENTS.md`(§1 규칙 + 명령어), `docs/DECISIONS.md`·`BACKLOG.md`.
- [ ] `npm run check` 통과
- [ ] `npm run dev`로 빈 화면 표시
- [ ] core에서 ui/game을 import하면 린트가 실패함을 확인

### M1 — Core: 룬·그래프·컴파일
- 산출물: §6.1 룬 데이터(JSON) + zod 로더, 그래프 모델·검증(E1~E10, W1~W4), `compile`, signature, 공유 코드 인코더/디코더, 시작 마법 JSON 3종.
- [ ] 단위 테스트 30개 이상 (모든 에러·경고 코드를 각 1개 이상 포함)
- [ ] §10 컴파일 오라클 O1~O7 통과
- [ ] 모든 `src/data` JSON이 스키마 검증 테스트를 통과

### M2 — Core: 시뮬레이션 & 스펠 런타임
- 산출물: 60Hz 고정 스텝 시뮬, 시드 RNG, 엔티티(플레이어·더미·투사체·버스트·오비트·존), 충돌, 상태이상, 피해 파이프라인, 스케줄러(delay/repeat), 트리거(onHit/onExpire), 노드 실행 이벤트, `tools/sim-cli.ts`.
- [ ] §10 시뮬 오라클 S1~S6 통과
- [ ] Core를 제외한 룬 18종 각각에 효과 테스트 1개 이상
- [ ] 동일 시드 결정성 테스트
- [ ] 성능: 엔티티 300개 × 600틱이 Node에서 2초 미만
- [ ] `npm run sim -- --spell src/data/spells/firebolt.json --scenario dummy_line --ticks 600 --seed 1`이 JSON 결과(총 피해, DPS, 최대 엔티티 수, 노드 실행 수)를 출력

### M3 — 에디터 & 시험 도크
- 산출물: §7.2~7.3. Zustand 스토어(그래프 편집·Undo/Redo·보관함), React Flow 에디터, 인스펙터, 검증 패널, 도크(Canvas), 공유 코드 UI.
- [ ] 수동 시나리오 통과: "시작 → 파이어 볼트 열기 → 다중 추가 → ▶ 시험 → 볼트 3발 발사, RAM 합계 5/6 표시"
- [ ] 유효하지 않은 연결 차단 / 장착 마법의 RAM 초과 배치 차단
- [ ] 노드 60개에서 드래그 50fps 이상
- [ ] 스토어 단위 테스트(Undo/Redo, 복사·붙여넣기, 가져오기 검증)

### M4 — 미션 씬
- 산출물: 플레이어 이동·대시·시전 입력, 방 로더(ASCII), 적 5종 AI(core), 웨이브·문·터미널(에디터 모달), 조각 오브, HUD, 사망/클리어 → 결과 화면, 디버그 패널(`?debug=1`: 조각 지급·전 룬 해금·무적·적 소환).
- [ ] R1~R4 + 터미널을 끝까지 플레이 가능(보스방은 플레이스홀더)
- [ ] 적 AI 단위 테스트(스카우트 접근·접촉, 센트리 예고→발사, 헌터 돌진, 이지스 방패 감쇠, 릴레이 오라)
- [ ] 전투 중 에디터 열기 불가
- [ ] 엔티티 150개에서 60fps (데스크톱 기준)

### M5 — 성장·적응·보스
- 산출물: 저장/불러오기(버전+마이그레이션 스텁, zod 검증), 벤치(§6.5), 룬 해금, 용량·슬롯·능력치 업그레이드, 정산(100%/70%), AI 적응(§5.5)+HUD+리포트, 거버너(패치 연출).
- [ ] 작업실 → 미션 → 결과 → 벤치 → 미션 루프가 새로고침 후에도 유지
- [ ] §10 적응 오라클 S7·S8 통과
- [ ] 보스 패치 테스트(67%·34%에서 상위 태그가 상한으로 고정)
- [ ] 데이터 기준 섹터 1 전 처치 보상 합계 ≥ 100

### M6 — 폴리시 & 플레이테스트 빌드
- 산출물: §7.6 피드백, 튜토리얼(3단계: 볼트 배치 → 화염 부착 → 시험), 로컬 텔레메트리(링버퍼 500건, 콘솔에서 JSON 덤프), 밸런스 패스(sim-cli), `npm run build`(정적 호스팅 가능), README(플레이·개발 방법).
- [ ] 처음 보는 사람이 3분 안에 첫 커스텀 마법을 시험
- [ ] 빌드 산출물이 정적 서버에서 동작
- [ ] `npm run check` 통과

### MVP 전체 수락 기준
1. 새 세이브에서 5분 안에 템플릿을 수정(또는 새로 설계)한 마법으로 R1을 클리어할 수 있다.
2. 섹터 1(보스 포함)을 8~12분에 클리어할 수 있다.
3. 첫 클리어 후 '용량 3단계' 또는 '용량 2단계 + 룬 2종'을 구매할 수 있다.
4. 같은 속성·형태를 연속 사용하면 10초 안에 해당 학습도가 0.3 이상으로 HUD에 표시된다.
5. 새로고침 후에도 진행이 유지되고 `npm run check`가 통과한다.

---

## 10. 테스트·품질 기준

### 10.1 컴파일 오라클 (§6.1 초기값 기준, 실수 비교는 `toBeCloseTo`)

| ID | 그래프 | 기대 결과 |
|---|---|---|
| O1 | Core → 볼트[화염] | ram 2 · energy 10 · cooldown 0.31 · entities 1 |
| O2 | Core → 볼트[화염, 다중] | ram 3 · energy 16.4 · cooldown 0.34 · entities 3 |
| O3 | Core → 반복(3) → 볼트[화염] | ram 3 · energy 30 · cooldown 0.34 · entities 3 |
| O4 | Core → 볼트[화염, 다중] —onHit→ 버스트[증폭] | ram 5 · energy 58.4 · cooldown 0.40 · entities 6 |
| O5 | Core → 지연(0.5) → 버스트 | ram 2 · energy 10 · cooldown 0.31 · entities 1 |
| O6 | Core → 볼트[화염, 냉기] | 에러 E6 |
| O7 | Core → 버스트[관통] | ok + 경고 W2 |

### 10.2 시뮬 오라클 (시드 1, 더미 HP 100, 적응 OFF, 반격 없음)

| ID | 상황 | 기대 결과 |
|---|---|---|
| S1 | 파이어 볼트 1회 명중 | 즉시 10 + `burn` 총 9(0.5s 틱 6회 × 1.5) → 3.0s 후 누적 19 |
| S2 | 냉기 볼트 3회 연속 명중 | 3번째 명중 시 `freeze` 1.0s, 이후 4s 빙결 면역 |
| S3 | 볼트[관통]가 일직선 5체를 통과 | 앞 3체만 피해 10 / 9 / 8 |
| S4 | Core → 지연(0.5) → 볼트 | 시전 후 30틱(±1)에 투사체 생성 |
| S5 | Core → 반복(3, 0.15s) → 볼트 | 틱 0·9·18에 생성 |
| S6 | 동일 시드·동일 입력 | 1,000틱 후 상태 해시 동일 |
| S7 | 화염 볼트 30회 직접 명중(w=1) | net[fire]=0.60, net[bolt]=0.30 → 피해 배율 0.28 (10 → 2.8) |
| S8 | S7 직후 6초 무사용 | net[fire]=0.54 (4초 유예 후 2초 × 0.03 감소) |

### 10.3 품질 기준

- core 라인 커버리지 85% 이상. 모든 데이터 JSON은 zod 검증 테스트로 강제한다.
- core에서 `Math.random`·`Date.now` 사용 0건(린트로 강제).
- 성능: 시뮬 1틱 평균 4ms 미만(엔티티 300, Node). 정상 플레이 중 콘솔 에러·경고 0.
- 접근성: 속성은 색 + 도형을 함께 사용한다.

---

## 11. 열린 질문·리스크·후속

**열린 질문 (사람이 결정 — 에이전트는 기본값으로 먼저 구현)**
1. 엔진/플랫폼: 기본은 웹(TS). Unity·Godot으로 확정되면 §4만 교체하고 §5·§6·§8을 명세로 쓴다.
2. RAM 용량 방식: 기본은 전체 공유(`ram.mode`). 마법별 방식과 플레이테스트로 비교한다.
3. 에너지: 기본은 최악 선지불·환불 없음. 체감이 가혹하면 미사용분 환불을 검토한다.
4. 진행 구조: 기본은 섹터 반복 + 영구 성장. 로그라이크식 런 리셋은 채택하지 않았다.
5. 적응 강도: 기본값은 §5.5. 체감이 가혹하면 상한부터 낮춘다.
6. 아트·사운드 방향.

**리스크와 대응**
- 노드 에디터 진입장벽 → 템플릿 마법, 3단계 튜토리얼, 자동 부착, 친절한 에러 문구
- 마법 밸런스 폭주 → 에너지 최악 선지불, 엔티티 하드캡, sim-cli 회귀 점검
- 적응이 불쾌하게 느껴짐 → 상태 상시 표시, 노이즈 룬, 상한 조정
- 범위 확장 → 마일스톤 게이트, BACKLOG 분리

**후속 확장(MVP 이후)**: 원소 반응(화염+냉기 등), 차지·패시브 코어, 빔·벽·소환 형태, 룬 숙련, RAM 모듈 아이템화(특성), 중력·시간 계열 룬, 절차적 섹터, 고대서 수집·내러티브, 공유 코드 갤러리, 게임패드·키 리바인딩.

**플레이테스트 지표 (사람이 측정, 에이전트 작업 대상 아님)**: 첫 커스텀 마법까지 3분 이내 · 섹터 1 중 인당 편집 5회 이상 · 인당 서로 다른 마법 시그니처 장착 4종 이상 · "내가 만든 마법 같다" 5점 척도 4.0 이상 · 사망 후 재도전 의향 70% 이상.

---

## 부록. Codex 첫 지시 프롬프트 예시

이 파일을 `docs/CRD.md`로 저장한 뒤:

```
docs/CRD.md를 끝까지 읽고 §1 규칙에 따라 M0만 수행해줘.
완료 조건을 모두 확인한 뒤, 변경 요약과 DECISIONS.md에 기록한 가정을 보고하고 멈춰.
```

이후 마일스톤은 "M1 진행해줘. 동일 규칙 적용."처럼 한 단계씩 지시한다.
