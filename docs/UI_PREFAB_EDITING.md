# UI 프리팹 편집 안내

화면 UI의 기본 색상과 이미지는 프리팹의 Image, TMP 컴포넌트에서 편집한다. 런타임 코드가 룬 분류별 색상을 덮어쓰지 않으며, 버튼 상호작용 색상은 Unity Button의 Color Tint가 처리한다. 기존 이미지 리소스는 보존했다.

## 적용된 스프라이트

색상이 포함된 새 Corner 이미지 경로는 `Assets/RuneCode/Art/UI/`이다. 모두 128×128이며 Image Type은 Sliced, Fill Center는 활성화했다. 기존 흰색 이미지는 `Assets/RuneCode/Art/`에 보존했다.

| 대상 | Source Image | 9-slice Border |
| --- | --- | --- |
| 룬 팔레트·빠른 검색 행 | Rune_FourCorners_분류_128 / Unavailable / Default | 네 방향 16px |
| 강화 트리 노드 | Tree_FourCorners_상태_128 | 네 방향 16px |
| TabButton의 Normal / Selected | Button_TopCorners_Navy_128 | 네 방향 16px |
| 일반 버튼·목록 조작 버튼 | 기존 프리팹 설정 유지 | 기존 설정 유지 |
| UiPanel 및 화면의 패널·입력창 배경 | Panel_White_Square_128 | 네 방향 8px |

새 Corner 이미지는 배경색과 회색 테두리를 이미지 자체에 포함한다. 해당 Image Color의 RGB는 흰색으로 두며 투명 클릭 영역의 Alpha는 0을 유지한다. 탭의 Normal / Selected는 원본과 같은 `#1A2130` 남색 이미지를 공유하고, 현재 탭은 기존 SelectionMarker로 구분한다. 일반 버튼·패널·HUD·화면 배치는 이번 Corner 교체에서 변경하지 않았다. 파일별 색상과 생성 프롬프트는 [UI_BAKED_CORNER_SPRITES.md](UI_BAKED_CORNER_SPRITES.md)를 참고한다.

## 편집 대상

별도 요소 경로: `Assets/RuneCode/Resources/RuneCode/UI/Elements/`

| 프리팹 | 편집 대상 |
| --- | --- |
| TabButton | 작업실 탭과 룬 팔레트 분류 탭. Normal / Selected 표시 및 SelectionMarker |
| RunePaletteRow | 룬 팔레트의 공통 행. Available / Unavailable 배경과 Label |
| RunePaletteShapeRow / RunePaletteBehaviorRow / RunePaletteModifierRow / RunePaletteFlowRow / RunePaletteMethodRow | 형태 / 행동 / Modifier / 흐름 / 메서드별 팔레트 행 Variant |
| QuickPaletteRow | 빠른 룬 검색 결과 행 |
| QuickPaletteShapeRow / QuickPaletteBehaviorRow / QuickPaletteModifierRow / QuickPaletteFlowRow / QuickPaletteMethodRow | 빠른 검색의 분류별 행 Variant |
| SpellListRow | 저장한 마법 목록 행 |
| ValidationRow / ValidationErrorRow / ValidationWarningRow / ValidationSuccessRow | 검증 안내, 오류, 경고, 성공 행 |
| InspectorHeading / InspectorTitle / InspectorNote / InspectorHint | 런타임 생성 인스펙터의 머리글, 노드 이름, 설명, 미선택 안내 |
| InspectorButton / InspectorDeleteButton | 인스펙터 조작 버튼과 삭제 버튼 |
| UiText / UiInput / UiScrollList | 공통 생성 텍스트, 입력창, 스크롤 목록 원본 |
| MissionTimer | 일반 TimerLabel / Warning 표시. 타이머 문구는 두 텍스트에 동일하게 갱신됨 |
| UpgradeTreeConnection | 양 끝 노드 중 미학습 노드가 있는 Locked / 양쪽을 모두 배운 Satisfied 연결선 |
| GraphNodeLabel | 그래프 노드의 라벨 원본 |
| GraphVisualStyle | 그래프 배경, 격자, 노드, 연결선, 포트, 선택 표시의 색상 템플릿 |
| PopupShade / UiHitArea | 화면 생성 도구에서 쓰는 팝업 차단 배경 / 투명 입력 영역 |

강화 노드 원본은 `Assets/RuneCode/Prefabs/UI/UpgradeTreeNode.prefab`이다. Locked / Available / Partial / Max 자식의 Image를 각각 편집한다. 루트 Image는 Source Image 없이 Alpha 0인 투명 클릭 영역으로 유지한다. 표시용 이미지를 루트에 넣지 말고 각 상태 자식에 넣는다. 효과 아이콘은 기존 Icon / SpriteIcon과 강화 트리 SO의 아이콘 연결을 사용한다.

강화 노드는 회색 테두리와 채도를 높인 배경색을 포함한 `Tree_FourCorners_상태_Vivid_128.png`를 사용한다. 상태 자식의 Image Color는 흰색이다. UiStateVisual의 States 순서는 Locked(0), Available(1), Partial(2), Max(3)이며 Selectable은 루트 Button을 참조한다. 비용 부족 전용 상태는 제거했다. 비용이 부족해도 개방 여부와 학습 레벨에 맞는 모습을 유지하며 구매는 차단하고 툴팁에 필요한 비용을 표시한다.

연결선은 양 끝 노드가 모두 1레벨 이상일 때만 Satisfied로 바뀐다. 선행 노드를 배우기만 했거나 디버그 모드로 선행 조건을 무시할 수 있어도, 다음 노드를 배우기 전에는 기본 Locked 표시를 유지한다. MAX까지 강화할 필요는 없다.

화면 전체 배치는 기존 `Assets/RuneCode/Resources/RuneCode/UI/WorkshopScreen.prefab`, `MissionScreen.prefab` 및 각 Popup 프리팹에서 편집한다. 공통 일반 버튼과 패널은 기존 `Assets/RuneCode/Prefabs/UI/UiButton.prefab`, `UiPanel.prefab`을 사용한다.

## 작업실 탐색과 설정 팝업

작업실 탭은 **룬 설계 / 강화 트리** 두 개만 사용한다. 시간제 전투 진입 버튼과 설정 버튼은 `UiButton` 일반 버튼이며, `TabButton`의 선택 상태를 사용하지 않는다.

| 편집 대상 | 프리팹과 위치 |
| --- | --- |
| 룬 설계·강화 트리 탭 | `WorkshopScreen.prefab` → `Page/HeaderArea/BtnArea/editorTabButton`, `treeTabButton` |
| 설정 열기 버튼 | `WorkshopScreen.prefab` → `Page/HeaderArea/BtnArea/settingsButton` |
| 시간제 전투 진입 버튼 | `WorkshopScreen.prefab` → `Page/HeaderArea/stageButton` |
| 스테이지 선택의 뒤로가기 버튼 | `WorkshopScreen.prefab` → `Page/HeaderArea/BtnArea/BackButton` |
| 설정 창 배치·배경·닫기 | `SettingsPopup.prefab` → `Card`, `Card/Content`, `Card/CloseButton` |
| 설정 창의 검은 딤 배경 | `SettingsPopup.prefab` → `PopupShade`의 Image Color `(0, 0, 0, 0.7)` |

`SettingsPopup.prefab`은 `Assets/RuneCode/Resources/RuneCode/UI/`에 있다. 기존 화면 흔들림, 히트스톱, 저장 초기화와 디버그 설정 참조를 이 팝업으로 옮겼다. 배경은 입력을 차단하며 닫기 버튼 또는 Esc로 닫는다. 딤 색상은 이 팝업 인스턴스의 프리팹 Override이므로 공용 `PopupShade.prefab`을 바꿀 필요가 없다.

스테이지 선택 중에는 두 탭과 전투 진입 버튼을 숨기고 뒤로가기를 표시한다. 뒤로가기 또는 Esc는 진입 직전에 보던 룬 설계·강화 트리로 돌아간다. 설정 팝업을 열거나 닫아도 현재 화면은 유지한다. 이동할 버튼을 직접 배치한 뒤에는 `WorkshopScreen` 컴포넌트의 Tab Buttons(룬 설계, 강화 트리 순서), Deploy Button, Settings Button, Back Button 참조를 유지한다. 팝업의 Panel과 Close Button 참조도 유지한다.

## 상태 편집 방법

1. 원본 프리팹을 열고 해당 상태 자식을 선택한다.
2. Image의 Source Image에 원하는 Sprite를 연결한다. 9-slice 이미지는 Image Type을 Sliced로 설정한다.
3. 새 Corner 이미지는 Image Color를 흰색으로 두고 Source Image로 배경색을 선택한다. Image Color를 추가 변경하면 테두리에도 같은 색이 곱해진다. 글자 색상은 TMP의 Vertex Color에서 정한다.
4. 다른 상태의 자식도 각각 편집한다. 프리팹의 기본 활성 상태를 바꿔 미리 볼 수 있다. 실행 중에는 UiStateVisual이 상태에 맞는 자식 하나만 켠다.

TabButton의 클릭 연결, 팔레트 문구와 사용 가능 여부, 강화 트리 상태 판단은 기존 화면 코드가 담당한다. Button의 Transition은 **Color Tint**이며 상태 색상은 Sprite 색상과 Image Color에 곱해진다. 기존 상호작용 설정을 유지했다.

| Button 설정 | RGB 배율 | Alpha |
| --- | --- | --- |
| Normal Color | 1.00 | 1.00 |
| Highlighted Color (Hover) | 1.22 | 1.00 |
| Pressed Color | 0.72 | 1.00 |
| Selected Color (키보드·게임패드 포커스) | 1.12 | 1.00 |
| Disabled Color | 0.45 | 0.70 |

Color Multiplier는 1, Fade Duration은 0.08초이다. 상태별 배경 자식이 있는 버튼은 UiStateVisual의 **Selectable**에 해당 Button을 연결한다. Show 호출 시 활성 자식의 Graphic을 Target Graphic으로 연결하므로, 그 자식에 기본 색상과 이미지를 설정한다. 루트의 투명 Image는 클릭 영역이므로 유지한다. TabButton의 Selected 자식은 현재 열린 탭을 표시하며, Button Selected Color의 포커스 표시와 별개이다.

## 룬 분류마다 다르게 꾸미기

`RunePaletteShapeRow.prefab` 등 원하는 분류 Variant를 열고 `Available`, `Unavailable`의 Source Image와 `Label`의 Vertex Color를 편집한다. 공통 `RunePaletteRow`를 편집하면 개별 Variant에서 Override하지 않은 설정이 함께 바뀐다. 특정 분류만 다르게 하려면 해당 Variant에서 값을 변경한다. 빠른 검색은 `QuickPalette…Row` Variant를 별도로 편집한다. 각 Variant에는 색상이 포함된 FourCorners 이미지를 적용했다. 파일·색상표는 [UI_BAKED_CORNER_SPRITES.md](UI_BAKED_CORNER_SPRITES.md)를 참고한다.

프리팹 연결을 바꾸려면 `WorkshopScreen.prefab`의 `SpellEditorPanel` 컴포넌트에서 **Category Row Prefabs** 배열을 편집한다. 빠른 검색은 `QuickPalettePopup.prefab`의 같은 배열에서 변경한다.

| Category | 분류 | 기본 팔레트 Prefab |
| --- | --- | --- |
| shape | 형태 | RunePaletteShapeRow |
| behavior | 행동 | RunePaletteBehaviorRow |
| modifier | Modifier | RunePaletteModifierRow |
| flow | 흐름 | RunePaletteFlowRow |
| method | 메서드 | RunePaletteMethodRow |

배열의 Category에는 위 ID를, Prefab에는 사용할 UiRow 프리팹을 연결한다. 연결되지 않은 분류나 비어 있는 Prefab은 기존 Rune Row Prefab(빠른 검색은 Row Prefab)을 사용한다. 중복 Category가 있으면 위쪽의 유효한 연결을 사용한다. '전체' 탭에서도 각 룬의 실제 분류에 맞는 Variant를 사용한다.

속성은 현재 팔레트 행이 아닌 노드 파라미터의 드롭다운으로 선택하므로 이 배열에 기본 연결을 추가하지 않았다. 색상 변경은 프리팹에서만 하며 런타임 코드는 분류에 맞는 행 원본만 선택한다.

## 팔레트 2열 배치와 문구

룬 팔레트와 빠른 검색 목록은 Content의 Grid Layout Group을 사용한다. Constraint는 Fixed Column Count / 2, Spacing은 (6, 6), 셀 높이는 52이다. 현재 셀 너비는 작업실 80, 빠른 검색 244이다. 팔레트 폭을 직접 바꾸면 해당 Grid의 Cell Size X도 `(콘텐츠 너비 - 가로 간격) / 2`로 조정한다. Content Size Fitter는 Horizontal Unconstrained / Vertical Preferred Size를 유지한다.

일반 룬은 이름만 한 줄로 표시하고 잠긴 경우에만 둘째 줄에 `미해금`을 추가한다. 해금된 룬에는 빈 줄이나 개행을 붙이지 않으며 RAM과 강화 트리 안내는 버튼에 표시하지 않는다. Modifier는 첫 줄에 이름·등급, 둘째 줄에 `보유 남은 수/전체 보유 수`를 표시한다. Label의 Vertical Alignment는 Middle이며 위·아래 Margin은 0으로, 한 줄과 두 줄 모두 버튼의 세로 가운데에 정렬한다. 기존 보유량 필터와 클릭·드래그 동작은 유지한다. 빠른 검색은 기존처럼 해금된 룬만 표시한다.

행 원본의 Label은 자동 줄바꿈을 꺼 두 줄 문구가 세 줄로 늘어나지 않게 했다. 긴 이름은 Auto Size로 글자를 줄이며 그래도 넘치면 Ellipsis로 표시한다. 문구는 SpellPalettePresenter, 두 줄 높이는 같은 클래스의 ROW_HEIGHT에서 관리한다.

## 그래프 노드의 색상과 텍스트

그래프 노드의 분류 색상은 `GraphVisualStyle.prefab`의 Category Row Prefabs에 연결된 팔레트 행 프리팹에서 읽는다. 해당 행 Button의 Target Graphic에 지정된 Image Color와 읽기 가능한 Sprite의 중앙 픽셀 색을 곱해 상단 띠·분류 아이콘·선택 테두리 색상으로 사용한다. 새 Rune_FourCorners 이미지의 Texture Importer는 이 조회를 위해 Read/Write Enabled를 켰다. 교체할 룬 이미지도 중앙 픽셀을 배경색으로 두고 Read/Write Enabled를 켜야 한다. 형태·행동·Modifier·흐름·메서드는 팔레트와 같은 Variant를 참조하므로 새 이미지로 교체해도 분류 색상이 유지된다. 시전 코어처럼 팔레트에 없는 분류는 기존 NodeAccent 템플릿 색상을 사용한다.

분류 띠는 노드 상단에 높이 8px로 표시하며 GraphMeshPainter의 NODE_ACCENT_HEIGHT에서 조정한다. 제목과 입력 포트 이름의 왼쪽 여백은 GraphNodeLabels의 LABEL_PADDING_X(24px), 출력 포트 이름의 오른쪽 여백은 LABEL_PADDING_RIGHT(14px)이다. 출력 이름은 연결점과 겹치지 않도록 오른쪽 여백을 유지한다.

제목·설명과 포트 행은 별도 TMP 라벨로 표시한다. 포트 행의 중심 Y는 GraphNodeLayout의 실제 포트 좌표를 사용하며, 확대·축소와 노드 이동에도 점과 이름이 함께 움직인다. 첫 포트는 노드 상단에서 52px, 이후는 23px 간격이며 제목·설명을 위한 공간을 포함해 노드 높이를 계산한다. 지원 중단 출력은 기존 연결이 있을 때만 표시하고 취소선을 유지한다. 글자 색상과 글꼴 자산은 기존 GraphNodeLabel 프리팹에서 편집한다.

## 코드가 계속 담당하는 항목

- 강화 노드 위치·선행 관계·효과 아이콘은 Upgrade Tree SO, 연결선 길이·각도는 해당 노드 위치에 따라 결정된다.
- 반복 목록과 인스펙터는 프리팹을 사용하되 행 높이, 표시 문자열, 입력값과 클릭 동작은 코드가 갱신한다.
- 그래프의 노드·포트·연결선 모양은 메시로 그린다. GraphVisualStyle의 자식 Image는 색상 템플릿이며 직접 화면에 표시하지 않는다. 이 템플릿에 Sprite를 넣어도 메시 모양은 바뀌지 않는다. 자식 순서와 GraphVisualStyle의 Templates 참조 배열은 유지한다.
- 적·마법·투사체·월드 이펙트 및 전투 피해 숫자의 색상 처리는 전투 그래픽이므로 유지한다.

새 요소는 Resources 경로로도 로드하므로 Elements 폴더와 프리팹 파일 이름을 변경하려면 해당 로드 경로도 수정해야 한다. 기존 사용자 편집 프리팹을 다시 생성하지 않고 원본을 직접 편집하는 방식을 권장한다.
