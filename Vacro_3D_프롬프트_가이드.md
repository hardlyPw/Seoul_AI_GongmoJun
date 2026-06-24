# Vacro 3D 프롬프트 가이드

게임 프리팹에 바로 적용 가능한 3D 에셋을 Vacro에서 뽑기 위한 프롬프트 작성 공식입니다.

워크플로우: **텍스트 → 2D 이미지 → 3D 변환** (Vacro 이미지→3D 파이프라인 기준)

---

## 프롬프트 골격 — 10블록 구조

```
[1. 오브젝트 정의 + 스타일 부정 키워드]
[2. 뷰 방향 + 형태]
[3. 비율/스케일 명시]
[4. 핵심 디자인 디테일]
[5. 참조 브랜드/장르]
[6. 색상 팔레트]
[7. 재질 + FLAT BASE COLORS 강제]
[8. 바닥 슬랩/플랫폼 제거]
[9. 메시 스펙]
[10. 이미지→3D 합성 라인]
```

각 블록을 빠짐없이 채우는 게 핵심입니다. 특히 **7번 (재질 평탄화)** 과 **8번 (바닥 제거)**, **10번 (이미지→3D 합성)** 은 게임 임포트 호환성과 직결되니 절대 생략 금지.

---

## 블록별 작성 가이드

### 블록 1 — 오브젝트 정의 + 부정 키워드

```
A single stylized 3D [오브젝트명], modern realistic [장르] design 
(NOT chibi, NOT clay, NOT blob shape, NOT toy-like)
```

- 부정 키워드는 점토/만화 톤 회피용. 다른 톤이 필요하면 부정 키워드만 교체.
- 예: 실사 사진풍 회피 시 `(NOT photorealistic, NOT detailed, NOT realistic textures)`

### 블록 2 — 뷰 방향 + 형태

```
facing +Z front view, [형태 — RECTANGULAR / CYLINDRICAL / 
DOME-SHAPED] with [구조 디테일 — flat vertical walls and sharp 
90-degree corners]
```

- 형태 키워드는 대문자로 박아서 Vacro가 흐트러지지 않게.
- 모서리 처리 (sharp / soft rounded / chamfered) 도 같이.

### 블록 3 — 비율/스케일

```
STRICTLY [개수/구조] only, total proportions [WIDER THAN TALL 
/ TALLER THAN WIDE / EQUAL], approximately [X] units wide, 
[Y] units tall, [Z] units deep
```

- "STRICTLY" 로 강제 (2층 방지, 다중 객체 방지 등).
- units 숫자는 Unity 1유닛 기준. 우리 프로젝트는 1 lane = 1 unit.

### 블록 4 — 핵심 디자인 디테일

```
[요소 1 with 설명], [요소 2 with 설명], [요소 3 with 설명]
```

- 3~5개로 압축. 너무 많으면 산만해짐.
- 위치 명시 (centered, on top, flanking, at the front).

### 블록 5 — 참조 브랜드/장르

```
clean modern minimalist design inspired by [브랜드/장소]
```

- 실존 레퍼런스 1~2개로 시각 컨셉 고정.
- 예: `Mega Coffee / Blue Bottle / IKEA / Apple Store / Parisian bistro`

### 블록 6 — 색상 팔레트

```
[메인 컬러] primary color with [서브 컬러] [부위] and 
[악센트 컬러] [부위]
```

- 최대 3색까지. 더 많으면 통일감 깨짐.

### 블록 7 — 재질 + FLAT BASE COLORS (필수)

```
smooth flat painted material with FLAT BASE COLORS ONLY (no baked 
AO, no baked shadows, no painted highlights, no reflections, no 
[재질별 텍스처 — wood grain / metal sheen / fabric weave])
```

- **이 블록이 v3의 핵심.** Unity 게임 엔진 라이팅과 충돌 방지.
- 베이크된 그림자/하이라이트가 텍스처에 박혀 나오면 씬 조명과 어색해짐.

### 블록 8 — 바닥/플랫폼 제거 (필수)

```
the [오브젝트] base MUST start exactly at ground level Y=0 with 
NO [바닥 종류 — concrete pad / display platform / floor pad / 
saucer / coaster / rug] beneath
```

- 종류를 구체적으로 다 나열해야 함. `NO ground` 한 줄로는 안 잡힘.

### 블록 9 — 메시 스펙

```
single mesh, symmetrical [X-axis / vertical axis], origin at 
[base center bottom / footprint center bottom], NO ground plane, 
NO shadow disc, NO surrounding props, transparent background, 
low-poly game-ready topology under [N] triangles, ready for Unity 
import
```

- 폴리 한계: 건물 5000 / 가구 2500 / 소품 1500 (아래 표 참조).

### 블록 10 — 이미지→3D 합성 라인 (필수)

```
orthographic front view composition, object perfectly centered in 
frame, full unobstructed silhouette visible with no parts cropped 
or occluded, even flat studio lighting with NO directional shadow 
and NO strong highlights, clean separation from background, product 
shot composition
```

- 이미지→3D 변환 정확도를 결정. 매번 그대로 붙여 넣기.

---

## 빈칸 채우기 템플릿 (복붙용)

```
A single stylized 3D ____________, modern realistic ____________ 
design (NOT chibi, NOT clay, NOT blob shape, NOT toy-like), facing 
+Z front view, ____________-shaped with ____________, STRICTLY 
____________ only, total proportions ____________ (____________ 
should exceed ____________), approximately ___ units wide, ___ 
units tall, ___ units deep, ____________, ____________, 
____________, clean modern minimalist design inspired by 
____________ and ____________, ____________ primary color with 
____________ ____________ and ____________ ____________, smooth 
flat painted material with FLAT BASE COLORS ONLY (no baked AO, 
no baked shadows, no painted highlights, no reflections, no 
____________), the ____________ base MUST start exactly at ground 
level Y=0 with NO ____________ and NO ____________ beneath, simple 
____________ form only, NO ____________, NO ____________, single 
mesh, symmetrical X-axis, origin at base center bottom, NO ground 
plane, NO shadow disc, NO surrounding props, transparent background,
low-poly game-ready topology under ____ triangles, orthographic 
front view composition, object perfectly centered in frame, full 
unobstructed silhouette visible with no parts cropped or occluded, 
even flat studio lighting with NO directional shadow and NO strong 
highlights, clean separation from background, product shot 
composition, ready for Unity import
```

---

## 실전 예시 — 가로등 (`StreetLamp`)

```
A single stylized 3D Korean city street lamp, modern realistic 
urban furniture design (NOT chibi, NOT clay, NOT blob shape, NOT 
toy-like), facing +Z front view, CYLINDRICAL with vertical pole 
and rectangular lamp head on top with sharp 90-degree corners, 
STRICTLY one single pole only, total proportions TALLER THAN WIDE 
(height should exceed width by 8 to 1), approximately 0.3 units 
wide, 3 units tall, 0.3 units deep, thin straight cylindrical pole 
with subtle base flange, rectangular lamp housing at the top tilted 
slightly downward, simple horizontal arm connecting pole to lamp 
housing, clean modern minimalist design inspired by Seoul Gangnam 
street lamps and modern urban LED fixtures, dark charcoal gray 
primary color with white lamp panel surface and silver metal trim 
accents, smooth flat painted material with FLAT BASE COLORS ONLY 
(no baked AO, no baked shadows, no painted highlights, no 
reflections, no metal sheen), the pole base MUST start exactly at 
ground level Y=0 with NO concrete pad and NO mounting platform 
beneath, simple architectural form only, NO ornaments, NO banners, 
single mesh, symmetrical X-axis, origin at base center bottom, NO 
ground plane, NO shadow disc, NO surrounding props, transparent 
background, low-poly game-ready topology under 1500 triangles, 
orthographic front view composition, object perfectly centered in 
frame, full unobstructed silhouette visible with no parts cropped 
or occluded, even flat studio lighting with NO directional shadow 
and NO strong highlights, clean separation from background, product 
shot composition, ready for Unity import
```

---

## 카테고리별 폴리 / 스케일 가이드

| 카테고리 | 폴리 한계 | Unity 유닛 기준 |
|---|---|---|
| 건물 (cafe, 빌딩) | 5000 | 가로 3~6, 높이 3~6 |
| 큰 가구 (카운터, 진열장) | 2500 | 가로 1.5~2.5, 높이 1 |
| 작은 가구 (테이블, 의자) | 2000~3000 | 가로 0.5~1, 높이 0.5~0.8 |
| 인테리어 소품 (메뉴판, 화분) | 1500 | 가로 0.3~0.5 |
| 픽업 아이템 (컵, 코인) | 1500 | 가로 0.1~0.3 |
| 캐릭터 | 8000 | 높이 1.8 |
| 도시 소품 (가로등, 표지판) | 1500 | 높이 2~3 |

> 우리 프로젝트 기준: `LaneManager.LaneSpacing = 1` 이므로 1 lane = 1 Unity unit.
> 카페처럼 lane 3개 폭이 필요한 기믹은 가로 ≈ 3 units.

---

## Vacro 워크플로우

### 1. 텍스트→2D 생성
- 위 템플릿으로 프롬프트 작성
- 4장 중 가장 깨끗한 정면 정사영 이미지 선택

### 2. 2D 선택 체크리스트
- [ ] 완전 정면 (3/4 각도 X)
- [ ] 좌우 대칭
- [ ] 비율 요청대로 (가로/세로)
- [ ] 인테리어/디테일 보임
- [ ] 바닥 슬랩 없음 (있으면 다른 후보 선택)

### 3. 3D 변환 (200 크레딧)
- 선택한 이미지에서 "3D 생성하기" 클릭
- 1~3분 대기

### 4. 변환 결과 확인 체크리스트
- [ ] 정면 잘 나옴 (간판/입구/창문 다 보임)
- [ ] 뒷면 — 비어있거나 구멍 안 났음
- [ ] 측면 — 평평한 벽 형태
- [ ] 윗면 — 지붕/꼭대기 살아있음
- [ ] 바닥 — 회색 슬랩 같이 모델링됐는지 확인 (있으면 Blender/ProBuilder로 제거 필요)

### 5. 다운로드
- 포맷: `.fbx` 권장 (URP 머티리얼 추출 용이)
- `.glb` 도 가능하지만 머티리얼 변환 추가 작업 필요

---

## Unity 임포트 가이드

```
1. 새 폴더: Project-Seoul/Assets/Models/[카테고리]/
2. .fbx 드래그해서 임포트
3. 임포트된 모델 클릭 → Inspector
   - Model 탭: Scale Factor 확인 (1이면 OK, 0.01 같으면 100으로 조정)
   - Materials 탭: "Extract Materials" 클릭 → 같은 폴더에 저장
   - 추출된 머티리얼 → Shader를 URP/Lit로 변경
4. 적용할 prefab 열기 (예: CafeGimmick.prefab)
5. 교체할 자식 GameObject 선택
   - MeshFilter의 Mesh 필드에 → 임포트한 메시 드래그
   - MeshRenderer의 Materials 필드에 → 변환한 URP 머티리얼 드래그
6. Scene 뷰에서 크기/위치 확인
   - 어색하면 localScale, localPosition 조정
```

---

## 자주 발생하는 문제 & 대처

| 증상 | 원인 | 대처 |
|---|---|---|
| 빌드에서 magenta로 보임 | URP 셰이더 변종 스트립 | Project Settings → Graphics → Always Included Shaders에 추가 |
| 너무 크거나 작게 임포트됨 | Vacro 단위 (cm) vs Unity (m) | Inspector → Model → Scale Factor 조정 |
| 머티리얼이 분홍색 | URP 미적용 | Materials → Extract → Shader URP/Lit 변경 |
| 바닥에 회색 슬랩 따라옴 | "NO ground" 지시 무시 | Blender로 슬랩 메시 분리/삭제 OR 임포트 후 BlockWall localPosition.y 조정으로 가림 |
| 캐릭터가 T-pose 아님 | 포즈 미지정 | 프롬프트에 `strict T-POSE with arms horizontal and legs straight` 명시 |
| 측면이 비어 있음 | 정면 view만 생성 | 측면/상단 view 별도 생성 후 multi-view로 3D 변환 시도 |

---

## 핵심 원칙 요약

1. **부정 키워드를 명시적으로 박아라** — "NOT chibi", "NO clay" 같은 식
2. **FLAT BASE COLORS** — 베이크된 라이팅 절대 금지
3. **바닥 슬랩 제거를 구체적으로 명시** — 종류를 나열 (concrete pad, platform, rug...)
4. **이미지→3D 합성 라인은 매번 붙여 넣기** — 정면 정사영 + 균일 조명
5. **Unity 유닛 단위로 스케일 지정** — 우리 게임 1 lane = 1 unit
6. **참조 브랜드 1~2개로 시각 컨셉 고정** — 추상적 형용사보다 효과적

---

문의: 프로젝트 Slack `#dev-art` 채널 또는 GitHub Issue.
