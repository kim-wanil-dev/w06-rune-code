# 색상이 포함된 Corner UI 이미지

저장 위치: `Assets/RuneCode/Art/UI/`. 원본 `Button_8px.png`와 기존 흰색 Sprite는 보존했다.

| 파일 | 중앙색 | 적용 대상 |
| --- | --- | --- |
| Button_TopCorners_Navy_128.png | #1A2130 | TabButton Normal / Selected |
| Rune_FourCorners_Shape_128.png | #3E6672 | RunePalette / QuickPalette 행 |
| Tree_FourCorners_Locked_128.png | #090E13 | UpgradeTreeNode 상태 배경 |
| Tree_FourCorners_Max_128.png | #0B291F | UpgradeTreeNode 상태 배경 |
| Tree_FourCorners_Available_128.png | #0E1B26 | UpgradeTreeNode 상태 배경 |
| Tree_FourCorners_Partial_128.png | #1C132B | UpgradeTreeNode 상태 배경 |
| Tree_FourCorners_InsufficientScrap_128.png | #2B1B11 | UpgradeTreeNode 상태 배경 |
| Rune_FourCorners_Unavailable_128.png | #2B3039 | RunePalette / QuickPalette 행 |
| Rune_FourCorners_Default_128.png | #344653 | RunePalette / QuickPalette 행 |
| Rune_FourCorners_Flow_128.png | #435F86 | RunePalette / QuickPalette 행 |
| Rune_FourCorners_Method_128.png | #497368 | RunePalette / QuickPalette 행 |
| Rune_FourCorners_Modifier_128.png | #6D5688 | RunePalette / QuickPalette 행 |
| Rune_FourCorners_Behavior_128.png | #8A6B43 | RunePalette / QuickPalette 행 |

모두 128×128 RGBA PNG, Sprite Single / Full Rect, 9-slice Border 16px, Sliced / Fill Center이다. 중앙은 불투명하며 잘린 모서리 바깥만 투명하다. 바깥 테두리는 원본의 회색 계열 #56627F, 안쪽 선은 #242B39를 기준으로 정리했다. 이미지의 Image Color는 RGB 흰색이며 원래 Alpha는 유지했다. 탭의 선택 표시는 기존 SelectionMarker를 사용한다. Button의 Color Tint와 클릭·드래그·목록 배치는 유지했다.

룬 Sprite는 그래프 분류색 조회를 위해 Read/Write Enabled이다. GraphVisualStyle은 팔레트 Image Color와 Sprite 중앙 픽셀을 곱하므로 이미지가 흰색 Tint로 바뀌어도 그래프 강조색이 유지된다. 자세한 프리팹 연결은 [UI_PREFAB_EDITING.md](UI_PREFAB_EDITING.md)를 참고한다.

## 생성 및 검증

내장 image_gen 도구의 편집 모드를 사용했다. 최초 투명 배경 생성본은 중앙까지 투명하게 처리되어 제외했다. 채택한 불투명 생성본을 .NET 이미지 처리로 128×128로 축소하고 검정 외부 매트만 투명하게 정리했다. 생성 과정의 색 편차는 지정 팔레트에 가까운 색을 양자화해 보정했으며, 모든 최종 중앙색과 Alpha 255를 확인했다. 원본 생성물은 Codex generated_images에 남겨 두었다.

Unity Editor API로 Sprite와 프리팹을 연결했고 13종 크기·Border, 75개 Corner 배경의 흰색 Tint, Button 참조 및 Missing Reference 검사를 통과했다. 실제 프리팹 렌더와 그래프 분류색 유지도 확인했다. 컴파일 오류·경고는 0개이며 Play 모드 조작은 미검증이다.

## 사용한 프롬프트

각 파일에 사용한 최종 프롬프트이다. TopCorners는 원본 PNG를, 첫 FourCorners는 원본 PNG를 참고했다. 나머지 FourCorners는 첫 FourCorners 생성본과 원본 PNG를 함께 참고했다.

### Button_TopCorners_Navy_128.png

```text
Use case: precise-object-edit. Edit target: the attached navy game UI sprite. ONE standalone SOLID FILLED square 1:1 UI button skin for Unity, final128x128. CRITICAL: this is an opaque filled tile, NOT a hollow frame, NOT a transparent center. Match reference minimal flat pixel-sharp 2D construction: outer desaturated blue-gray rim #56627F (2px thick at128), gap of center fill (2px), inset dark gray-blue line #242B39 (1px), solid center fill. Do not thicken or add any other bands. Shape fills canvas edge-to-edge no margin. No bevel, no gradient, no shadow, no glow, no texture, no icon, no text, no mockup, no contact sheet. Chamfer cut length12px at128. Entire interior including all inset area must be absolutely flat uniformly opaque central color. Use PURE BLACK #000000 ONLY in tiny outside cutout triangles as technical matte; absolutely no black or transparent area inside the frame. Do not make a cutout frame. Cut ONLY TOP LEFT and TOP RIGHT at45degrees, both bottom corners square90degrees. CENTRAL FILL EXACTLY #1A2130, filled edge-to-edge inside thin frame. Bake color, never tint all layers.
```

### Rune_FourCorners_Shape_128.png

```text
Use case: precise-object-edit. Edit target: the attached navy game UI sprite. ONE standalone SOLID FILLED square 1:1 UI button skin for Unity, final128x128. CRITICAL: this is an opaque filled tile, NOT a hollow frame, NOT a transparent center. Match reference minimal flat pixel-sharp 2D construction: outer desaturated blue-gray rim #56627F (2px thick at128), gap of center fill (2px), inset dark gray-blue line #242B39 (1px), solid center fill. Do not thicken or add any other bands. Shape fills canvas edge-to-edge no margin. No bevel, no gradient, no shadow, no glow, no texture, no icon, no text, no mockup, no contact sheet. Chamfer cut length12px at128. Entire interior including all inset area must be absolutely flat uniformly opaque central color. Use PURE BLACK #000000 ONLY in tiny outside cutout triangles as technical matte; absolutely no black or transparent area inside the frame. Do not make a cutout frame. Cut ALL FOUR corners equally at45degrees. CENTRAL FILL EXACTLY #3E6672, filled edge-to-edge inside thin frame. Bake color, never tint all layers.
```

### Tree_FourCorners_Locked_128.png

```text
Use case: precise-object-edit. Edit target Image1: a flat solid-filled four-chamfer game UI sprite. Style reference Image2: original navy sprite. CHANGE ONLY the flat interior fill color of Image1 to EXACT opaque #090E13. Preserve Image1's all FOUR equally clipped corner geometry, border thickness, nested frame spacing and blue-gray rim colors; arrange frame as Image2 (bright outer blue-gray #56627F, inset darker blue-gray #242B39 hairline). Do not color-multiply the frame. Keep a very thin frame relative to128-square, no extra bevels. CRITICAL filled tile, never hollow: entire interior completely opaque uniform #090E13, no gradients or lighting or texture at all. ONE square1:1 standalone sprite, fills canvas edge-to-edge, no padding; intended final128x128. Use pure BLACK #000000 ONLY outside four tiny cut corner triangles as removable technical matte. Do not put any black/transparent area inside. No text, no icon, no shadow, no contact sheet.
```

### Tree_FourCorners_Max_128.png

```text
Use case: precise-object-edit. Edit target Image1: a flat solid-filled four-chamfer game UI sprite. Style reference Image2: original navy sprite. CHANGE ONLY the flat interior fill color of Image1 to EXACT opaque #0B291F. Preserve Image1's all FOUR equally clipped corner geometry, border thickness, nested frame spacing and blue-gray rim colors; arrange frame as Image2 (bright outer blue-gray #56627F, inset darker blue-gray #242B39 hairline). Do not color-multiply the frame. Keep a very thin frame relative to128-square, no extra bevels. CRITICAL filled tile, never hollow: entire interior completely opaque uniform #0B291F, no gradients or lighting or texture at all. ONE square1:1 standalone sprite, fills canvas edge-to-edge, no padding; intended final128x128. Use pure BLACK #000000 ONLY outside four tiny cut corner triangles as removable technical matte. Do not put any black/transparent area inside. No text, no icon, no shadow, no contact sheet.
```

### Tree_FourCorners_Available_128.png

```text
Use case: precise-object-edit. Edit target Image1: a flat solid-filled four-chamfer game UI sprite. Style reference Image2: original navy sprite. CHANGE ONLY the flat interior fill color of Image1 to EXACT opaque #0E1B26. Preserve Image1's all FOUR equally clipped corner geometry, border thickness, nested frame spacing and blue-gray rim colors; arrange frame as Image2 (bright outer blue-gray #56627F, inset darker blue-gray #242B39 hairline). Do not color-multiply the frame. Keep a very thin frame relative to128-square, no extra bevels. CRITICAL filled tile, never hollow: entire interior completely opaque uniform #0E1B26, no gradients or lighting or texture at all. ONE square1:1 standalone sprite, fills canvas edge-to-edge, no padding; intended final128x128. Use pure BLACK #000000 ONLY outside four tiny cut corner triangles as removable technical matte. Do not put any black/transparent area inside. No text, no icon, no shadow, no contact sheet.
```

### Tree_FourCorners_Partial_128.png

```text
Use case: precise-object-edit. Edit target Image1: a flat solid-filled four-chamfer game UI sprite. Style reference Image2: original navy sprite. CHANGE ONLY the flat interior fill color of Image1 to EXACT opaque #1C132B. Preserve Image1's all FOUR equally clipped corner geometry, border thickness, nested frame spacing and blue-gray rim colors; arrange frame as Image2 (bright outer blue-gray #56627F, inset darker blue-gray #242B39 hairline). Do not color-multiply the frame. Keep a very thin frame relative to128-square, no extra bevels. CRITICAL filled tile, never hollow: entire interior completely opaque uniform #1C132B, no gradients or lighting or texture at all. ONE square1:1 standalone sprite, fills canvas edge-to-edge, no padding; intended final128x128. Use pure BLACK #000000 ONLY outside four tiny cut corner triangles as removable technical matte. Do not put any black/transparent area inside. No text, no icon, no shadow, no contact sheet.
```

### Tree_FourCorners_InsufficientScrap_128.png

```text
Use case: precise-object-edit. Edit target Image1: a flat solid-filled four-chamfer game UI sprite. Style reference Image2: original navy sprite. CHANGE ONLY the flat interior fill color of Image1 to EXACT opaque #2B1B11. Preserve Image1's all FOUR equally clipped corner geometry, border thickness, nested frame spacing and blue-gray rim colors; arrange frame as Image2 (bright outer blue-gray #56627F, inset darker blue-gray #242B39 hairline). Do not color-multiply the frame. Keep a very thin frame relative to128-square, no extra bevels. CRITICAL filled tile, never hollow: entire interior completely opaque uniform #2B1B11, no gradients or lighting or texture at all. ONE square1:1 standalone sprite, fills canvas edge-to-edge, no padding; intended final128x128. Use pure BLACK #000000 ONLY outside four tiny cut corner triangles as removable technical matte. Do not put any black/transparent area inside. No text, no icon, no shadow, no contact sheet.
```

### Rune_FourCorners_Unavailable_128.png

```text
Use case: precise-object-edit. Edit target Image1: a flat solid-filled four-chamfer game UI sprite. Style reference Image2: original navy sprite. CHANGE ONLY the flat interior fill color of Image1 to EXACT opaque #2B3039. Preserve Image1's all FOUR equally clipped corner geometry, border thickness, nested frame spacing and blue-gray rim colors; arrange frame as Image2 (bright outer blue-gray #56627F, inset darker blue-gray #242B39 hairline). Do not color-multiply the frame. Keep a very thin frame relative to128-square, no extra bevels. CRITICAL filled tile, never hollow: entire interior completely opaque uniform #2B3039, no gradients or lighting or texture at all. ONE square1:1 standalone sprite, fills canvas edge-to-edge, no padding; intended final128x128. Use pure BLACK #000000 ONLY outside four tiny cut corner triangles as removable technical matte. Do not put any black/transparent area inside. No text, no icon, no shadow, no contact sheet.
```

### Rune_FourCorners_Default_128.png

```text
Use case: precise-object-edit. Edit target Image1: a flat solid-filled four-chamfer game UI sprite. Style reference Image2: original navy sprite. CHANGE ONLY the flat interior fill color of Image1 to EXACT opaque #344653. Preserve Image1's all FOUR equally clipped corner geometry, border thickness, nested frame spacing and blue-gray rim colors; arrange frame as Image2 (bright outer blue-gray #56627F, inset darker blue-gray #242B39 hairline). Do not color-multiply the frame. Keep a very thin frame relative to128-square, no extra bevels. CRITICAL filled tile, never hollow: entire interior completely opaque uniform #344653, no gradients or lighting or texture at all. ONE square1:1 standalone sprite, fills canvas edge-to-edge, no padding; intended final128x128. Use pure BLACK #000000 ONLY outside four tiny cut corner triangles as removable technical matte. Do not put any black/transparent area inside. No text, no icon, no shadow, no contact sheet.
```

### Rune_FourCorners_Flow_128.png

```text
Use case: precise-object-edit. Edit target Image1: a flat solid-filled four-chamfer game UI sprite. Style reference Image2: original navy sprite. CHANGE ONLY the flat interior fill color of Image1 to EXACT opaque #435F86. Preserve Image1's all FOUR equally clipped corner geometry, border thickness, nested frame spacing and blue-gray rim colors; arrange frame as Image2 (bright outer blue-gray #56627F, inset darker blue-gray #242B39 hairline). Do not color-multiply the frame. Keep a very thin frame relative to128-square, no extra bevels. CRITICAL filled tile, never hollow: entire interior completely opaque uniform #435F86, no gradients or lighting or texture at all. ONE square1:1 standalone sprite, fills canvas edge-to-edge, no padding; intended final128x128. Use pure BLACK #000000 ONLY outside four tiny cut corner triangles as removable technical matte. Do not put any black/transparent area inside. No text, no icon, no shadow, no contact sheet.
```

### Rune_FourCorners_Method_128.png

```text
Use case: precise-object-edit. Edit target Image1: a flat solid-filled four-chamfer game UI sprite. Style reference Image2: original navy sprite. CHANGE ONLY the flat interior fill color of Image1 to EXACT opaque #497368. Preserve Image1's all FOUR equally clipped corner geometry, border thickness, nested frame spacing and blue-gray rim colors; arrange frame as Image2 (bright outer blue-gray #56627F, inset darker blue-gray #242B39 hairline). Do not color-multiply the frame. Keep a very thin frame relative to128-square, no extra bevels. CRITICAL filled tile, never hollow: entire interior completely opaque uniform #497368, no gradients or lighting or texture at all. ONE square1:1 standalone sprite, fills canvas edge-to-edge, no padding; intended final128x128. Use pure BLACK #000000 ONLY outside four tiny cut corner triangles as removable technical matte. Do not put any black/transparent area inside. No text, no icon, no shadow, no contact sheet.
```

### Rune_FourCorners_Modifier_128.png

```text
Use case: precise-object-edit. Edit target Image1: a flat solid-filled four-chamfer game UI sprite. Style reference Image2: original navy sprite. CHANGE ONLY the flat interior fill color of Image1 to EXACT opaque #6D5688. Preserve Image1's all FOUR equally clipped corner geometry, border thickness, nested frame spacing and blue-gray rim colors; arrange frame as Image2 (bright outer blue-gray #56627F, inset darker blue-gray #242B39 hairline). Do not color-multiply the frame. Keep a very thin frame relative to128-square, no extra bevels. CRITICAL filled tile, never hollow: entire interior completely opaque uniform #6D5688, no gradients or lighting or texture at all. ONE square1:1 standalone sprite, fills canvas edge-to-edge, no padding; intended final128x128. Use pure BLACK #000000 ONLY outside four tiny cut corner triangles as removable technical matte. Do not put any black/transparent area inside. No text, no icon, no shadow, no contact sheet.
```

### Rune_FourCorners_Behavior_128.png

```text
Use case: precise-object-edit. Edit target Image1: a flat solid-filled four-chamfer game UI sprite. Style reference Image2: original navy sprite. CHANGE ONLY the flat interior fill color of Image1 to EXACT opaque #8A6B43. Preserve Image1's all FOUR equally clipped corner geometry, border thickness, nested frame spacing and blue-gray rim colors; arrange frame as Image2 (bright outer blue-gray #56627F, inset darker blue-gray #242B39 hairline). Do not color-multiply the frame. Keep a very thin frame relative to128-square, no extra bevels. CRITICAL filled tile, never hollow: entire interior completely opaque uniform #8A6B43, no gradients or lighting or texture at all. ONE square1:1 standalone sprite, fills canvas edge-to-edge, no padding; intended final128x128. Use pure BLACK #000000 ONLY outside four tiny cut corner triangles as removable technical matte. Do not put any black/transparent area inside. No text, no icon, no shadow, no contact sheet.
```

