# 흰색 UI 버튼·패널 스프라이트

이 문서는 보존된 흰색 스프라이트의 생성 기록이다. 현재 룬·탭·강화 노드는 색상이 포함된 이미지로 교체했다. 최신 적용 파일과 편집 방법은 [UI_BAKED_CORNER_SPRITES.md](UI_BAKED_CORNER_SPRITES.md)를 참고한다.

저장 위치: `Assets/RuneCode/Art/`

| 파일 | 잘린 모서리 |
| --- | --- |
| Button_White_Diagonal_128.png | 왼쪽 위 / 오른쪽 아래 |
| Button_White_FourCorners_128.png | 네 모서리 모두 |
| Button_White_TopCorners_128.png | 상단 두 모서리 |
| Panel_White_Square_128.png | 잘리지 않은 네 직각 모서리 |

모두 128×128 RGBA PNG이며 중앙은 불투명한 흰색, 테두리는 중립 회색이다. 버튼의 잘린 모서리 바깥은 투명하며 패널은 네 모서리까지 불투명하다. RGB가 중립이므로 Unity Image의 Color로 색을 입혀 재사용할 수 있다. 원본 이미지는 보존했고, 생성한 이미지와 색상을 기존 UI 프리팹에 적용했다.

## Unity 설정

Sprite (2D and UI) / Single / Full Rect로 가져왔다. Mip Map 없음, 압축 없음, Bilinear, Clamp를 사용한다. 9-slice Border는 버튼은 네 방향 모두 16px, 패널은 8px이다. Image는 Sliced / Fill Center로 설정했다. 프리팹에서 Image Color로 기본 색상, Button의 Color Tint로 상호작용 색상을 변경한다. 대상 프리팹과 상태 설정은 [UI_PREFAB_EDITING.md](UI_PREFAB_EDITING.md)를 참고한다.

## 적용된 룬 분류 색상

다음 색상은 현재 RunePalette 및 QuickPalette 분류별 Sprite의 중앙색이다. 해당 Image Color는 흰색이며, 분류 이미지를 교체해서 배경색을 변경한다.

| 분류 | 배경 Color | 분위기 |
| --- | --- | --- |
| 형태 (shape) | #3E6672 | 차분한 청록 |
| 행동 (behavior) | #8A6B43 | 황동·앰버 |
| Modifier (modifier) | #6D5688 | 탁한 보라 |
| 흐름 (flow) | #435F86 | 차분한 청색 |
| 메서드 (method) | #497368 | 회녹색 |

글자는 #F0EEE6, 사용할 수 없는 행의 배경은 #2B3039이다. 비활성 버튼은 Disabled Color가 배경을 추가로 어둡게 한다. 배경 Color의 Alpha는 1(255)로 둔다. 분류를 색상만으로 구분하지 않고 이름·아이콘도 함께 유지한다.

## 생성 기록

내장 image_gen 도구로 원본 PNG를 편집했다. 최종 생성본을 128×128로 축소하고 검정 외부 매트를 투명하게 정리했으며, 중립 명암과 불투명 중앙을 확인했다. 최초 투명 배경 생성본은 중앙도 투명해져 제외했다.

최종 공통 프롬프트:

```text
Edit the referenced source UI button image into a flat white tintable Unity sprite. CRITICAL: WHITE SOLID FILLED CENTER, not a hollow frame. Pure opaque white center extending all the way to a thin gray border. Match source geometry and minimal flat 2D double-line border. No bevel effect, no gradients, no blur, no shadows, no texture, no glow. Entire image achromatic grayscale. Border: medium gray outer line 2px and light gray inner line 1px at final128x128. Flat white central fill with uniform intensity. Square 1:1 canvas, shape fills canvas edge-to-edge no margin. Use PURE BLACK #000000 only outside the small corner cutouts, as a technical matte to remove afterwards. Absolutely no black in the central fill; entire center is opaque white #FFFFFF. No labels, icons, text or contact sheet. Exact final size target128x128, cut lengths12pixels. Preserve flat pixel-sharp geometric style of source. 
```

각 이미지에 추가한 모서리 조건:

- Diagonal: Clip only TOP LEFT and BOTTOM RIGHT corners at45degrees. TOP RIGHT and BOTTOM LEFT must be square.
- FourCorners: Clip ALL FOUR corners equally at45degrees. Symmetric square with four small clipped corners.
- TopCorners: Clip only TOP LEFT and TOP RIGHT corners at45degrees. BOTH BOTTOM corners remain perfectly square90degrees.

직각 패널은 내장 image_gen 편집 모드에서 Diagonal 이미지를 참고해 생성했다. 불투명 배경으로 생성하고 최종 이미지를 128×128로 축소·중립 회색으로 정리했다. 사용한 프롬프트:

```text
Precise UI sprite edit: match the referenced white filled square Unity UI button's flat white center and neutral gray thin double inset frame, but remove ALL chamfered/cut corners. ALL FOUR corners must be sharp square 90-degree corners, on both outer and inner frames. A perfectly ordinary square rectangular PANEL skin, not a button mockup. The entire center MUST be opaque flat pure white #FFFFFF, NOT transparent, with uniform thin medium gray outer edge and light gray inner hairline. No clipped corners, no rounded corners, no background margin, no black matte, no shadows, no gradients, no texture, no lighting, no text or icons. Square1:1 canvas filled edge-to-edge by the rectangular panel. Final size128x128. Match border weight to reference (2-3px outer edge plus1px inset line at128). All RGB achromatic. A flat functional 9-slice texture.
```
