# 데이터 테이블 규칙

게임 데이터를 정규화된 CSV 테이블로 관리하기 위한 규칙이다. 룬(마법 노드) 테이블이 첫 번째 적용 사례이자 **참조 구현**이며, 적·장비·스테이지·보상 테이블도 같은 방식으로 추가한다.

## 구성

| 위치 | 역할 |
|---|---|
| `Assets/RuneCode/Tables/` (`RuneCode.Tables` 어셈블리) | 범용 기반. 순수 C#이며 UnityEngine을 참조하지 않는다(`noEngineReferences`). |
| `Assets/RuneCode/Resources/RuneCode/Tables/*.csv` | 테이블 원본. 이 파일이 유일한 원본이며 직접 편집한다. |
| `Core/Data/ResourcesTableSource.cs` | Unity에서 CSV(TextAsset)를 읽는 `ITableSource` 구현 |
| `Core/Graph/RuneData.cs`의 `RuneCatalog.FromTables` (`RuneCode.Grammar`) | 참조 구현. 테이블 6종을 합쳐 룬 정의를 만든다. |

`RuneCode.Tables`의 공개 API:

- `ITableSource.TryRead(name, out text)`: 테이블 이름(확장자 없음)으로 CSV 텍스트를 가져온다.
- `DataTable.Load(source, name, log)`: 테이블을 읽는다. `Rows`, `RequireColumns`, `RequireUniqueKeys`, `RequireReferences`, `GroupBy`, `DataTable.Key`
- `TableRow`: `GetString/GetInt/GetFloat/GetBool`(필수), `GetOptional...(column, fallback)`(빈 칸이면 기본값), `ReportError(column, message)`
- `TableErrorLog`: 오류를 `파일:행:열 메시지`로 모으고 `ThrowIfAny()`로 한 번에 보고한다. 예외는 `TableLoadException`(`FormatException` 상속)이다.

## 파일 규칙

- 파일명은 `snake_case.csv`, 열 이름은 `camelCase`로 쓴다.
- **1행은 헤더**다.
- **첫 칸이 `#`으로 시작하는 행은 주석**이라 로더가 무시한다. 2행에는 열 설명 행을 둔다(예: `#룬 ID,표시 이름,...`). 엑셀에서 열의 의미를 바로 보기 위해서다.
- 인코딩은 UTF-8이다. 엑셀이 한글을 올바르게 열도록 BOM을 포함해 저장한다(로더는 BOM이 있든 없든 읽는다).
- 따옴표와 쉼표는 RFC 4180을 따른다. 쉼표나 줄바꿈이 들어간 값은 `"..."`로 감싸고, 따옴표 문자는 `""`로 쓴다.

## 키와 관계

- **메인 테이블의 첫 열은 `id`**다. 유일해야 하며, 저장 데이터가 참조하므로 한 번 배포한 ID는 바꾸지 않는다.
- 메인 테이블의 **행 순서가 게임 내 기본 순서**다(별도 `order` 열 없음).
- **목록·중첩 데이터는 자식 테이블로 분리**한다. 한 셀에 `a;b;c`처럼 구분자로 넣지 않는다.
  - 파일명: `{부모}_{자식}.csv` (예: `rune_ports.csv`)
  - 필수 열: `{부모}Id`(부모 참조), `order`(정수, 묶음 안 순서)
  - 자식의 식별 열은 `portId`, `paramId`처럼 의미가 드러나는 이름을 쓴다.
- 1:1 확장 데이터(열이 많고 대부분 비어 있는 수치)는 별도 테이블로 분리해도 된다(예: `rune_stats.csv`, 룬마다 정확히 1행).

## 값 규칙

| 타입 | 형식 |
|---|---|
| 숫자 | 로캘과 무관하게 소수점은 항상 `.` (`1.5`). `1,5`는 오류다. NaN·무한대는 허용하지 않는다. |
| bool | `true` / `false` (대소문자 무관) |
| 빈 칸 | 그 열의 **기본값**. 필수 열(`Get...`)이 비어 있으면 오류다. |
| `0` | 실제 0. 빈 칸과 다르게 취급한다. |
| 표시 문자열 | 새 테이블은 표시 문구 대신 현지화 키(`nameKey` 등)를 쓴다. 룬 테이블의 `name` 열은 기존 구조를 유지한 예외다. |

열의 기본값은 로더 코드(`GetOptional...`의 fallback)와 2행 설명에 함께 적는다(예: `피해 배율(빈칸=1)`).

## 로더 작성 패턴

`RuneCatalog.FromTables`를 그대로 따라 한다.

```csharp
public static EnemyCatalog FromTables(ITableSource source)
{
    var log = new TableErrorLog();
    DataTable enemies = DataTable.Load(source, "enemies", log);
    DataTable drops = DataTable.Load(source, "enemy_drops", log);
    if (!(enemies.RequireColumns("id", "hp", "speed") & drops.RequireColumns("enemyId", "order", "itemId")))
    {
        log.ThrowIfAny();
    }

    HashSet<string> enemyIds = enemies.RequireUniqueKeys("id");       // 키 유일성
    drops.RequireUniqueKeys("enemyId", "order");
    drops.RequireReferences(enemyIds, "enemies", "enemyId");         // 부모 참조
    Dictionary<string, List<TableRow>> dropRows = drops.GroupBy("order", "enemyId");

    var result = new List<EnemyDefinition>();
    foreach (TableRow row in enemies.Rows)
    {
        var enemy = new EnemyDefinition(row, dropRows);              // 생성자에서 row.Get...으로 읽기
        if (enemy.Hp <= 0f) row.ReportError("hp", "0보다 커야 합니다."); // 데이터 규칙 검증
        result.Add(enemy);
    }
    log.ThrowIfAny();                                               // 모든 오류를 한 번에 보고
    return new EnemyCatalog(result);
}
```

원칙:

- 데이터 클래스는 `private readonly` 필드와 읽기 전용 프로퍼티를 쓰고, `internal` 생성자에서 `TableRow`를 받아 채운다.
- 리플렉션 자동 매핑이나 범용 레지스트리는 쓰지 않는다. 테이블마다 `FromTables` 하나로 명시적으로 변환한다.
- 첫 오류에서 멈추지 말고 `ReportError`로 기록한 뒤 끝에서 `ThrowIfAny()`를 호출한다.
- 런타임 중 테이블 데이터를 변경하지 않는다. 변하는 상태는 그 상태를 소유하는 객체가 따로 가진다.

## 새 테이블 추가 절차

1. `Resources/RuneCode/Tables/`에 CSV를 만든다(헤더 + `#` 설명 행).
2. 데이터 클래스와 `FromTables` 로더를 작성한다(위 패턴).
3. 앱 초기화(`GameData.Load` 또는 해당 기능의 로더)에서 `new ResourcesTableSource("RuneCode/Tables")`로 호출한다.
4. 키·참조·값 규칙 검증을 넣는다. 일부러 잘못된 값을 넣어 `파일:행:열` 오류가 나는지 확인한다.
5. 기존 JSON에서 옮기는 경우, 변환 전후 값이 같은지 비교한다(CSV를 다시 조립해 원본과 비교).
6. 이 문서의 "현재 테이블" 표를 갱신한다.

Unity 밖(CLI·도구)에서 읽어야 하면 파일 시스템용 `ITableSource`(폴더 + `{name}.csv` 읽기)를 구현해 같은 `FromTables`를 쓴다.

## 엑셀 편집 주의

- "CSV UTF-8(쉼표로 분리)" 형식으로 저장한다. 일반 "CSV"는 한글이 깨진다.
- 소수점이 쉼표로 표시되는 로캘에서는 저장값도 `1,5`가 될 수 있다. 로더가 오류로 거부하므로 저장 후 확인한다.
- ID처럼 보이는 숫자(`001`)는 엑셀이 앞의 0을 지운다. ID는 문자로 시작하게 짓는다(예: `enemy.scout`).

## 현재 테이블

| 테이블 | 키 | 내용 |
|---|---|---|
| `runes` | `id` | 룬 기본 정보: 이름, 카테고리, 점유 RAM, 에너지 비용·배율, 해금 방식·비용 |
| `rune_stats` | `runeId` (룬마다 1행) | 전투 수치 24종. 빈 칸은 배율 열이면 1, 그 외 0 |
| `rune_ports` | `runeId`+`direction`+`portId` | 연결 포트(문법 구조). `order`가 노드의 포트 표시 순서 |
| `rune_params` | `runeId`+`paramId` | 노드 파라미터 정의(number/enum/text) |
| `rune_param_options` | `runeId`+`paramId`+`order` | enum 파라미터 선택지 |
| `rune_tags` | `runeId`+`order` | 룬 태그 |

`rune_ports`·`rune_params`·`rune_param_options`의 ID와 `runes.category`는 문법 엔진이 해석하는 구조 값이다. 수치 조정이 아니라 문법 변경이므로 코드와 함께 바꾼다.

## 다음 후보 (권장 구조)

아직 JSON으로 남아 있는 데이터다. 옮길 때 아래 구조를 출발점으로 쓴다.

| 대상 (현재 파일) | 권장 테이블 |
|---|---|
| 적 (`enemies.json`) | `enemies.csv`: `id, nameKey, hp, speed, radius, damage, reward, ...` (행 = 적 1종) |
| 스테이지·웨이브 (`sector1.json`, `incremental.json`) | `stages.csv`(`id, nameKey, mapId, ...`), `stage_waves.csv`(`stageId, order, ...`), `stage_spawns.csv`(`stageId, waveOrder, order, enemyId, count, point`). 맵 타일 격자는 표 대신 `maps/{mapId}.txt` |
| 보상 | `rewards.csv`(`id, ...`), `reward_items.csv`(`rewardId, order, itemId, amount, weight`) |
| 장비 | `equipment.csv`(`id, nameKey, slot, ...`), `equipment_stats.csv`(`equipmentId, order, stat, value`) |
| 설정값 (`balance.json`, `governor.json`) | `config.csv`: `group, key, value, description` (모르는 키·빠진 키는 오류) |
| 현지화 (`strings.ko.json`) | `strings.csv`: `key, ko` (언어 추가 시 열 추가) |

플레이어 저장 파일과 공유 코드, 기본 마법 템플릿(`spells/*.json`)은 그래프 구조라 테이블 대상이 아니다.
