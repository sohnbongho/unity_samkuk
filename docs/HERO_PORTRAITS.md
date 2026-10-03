# 장수 초상화 규격 (장수 선택 카드용)

장수 선택 화면의 카드 5장에 들어가는 그림입니다. 그림이 없는 장수는 지금처럼 실루엣 + 색으로 보이므로,
**한 명씩 넣어도 됩니다.**

## 지금 들어 있는 그림 (임시)

`Assets/Sprites/Heroes/` 의 5장은 **코드로 그린 임시 그림**(평면 벡터풍)입니다. `tools/hero_art/HeroArt.cs` 가 그리며,
`powershell -File tools\hero_art\generate.ps1` 로 다시 만들 수 있습니다(Windows 내장 GDI+만 사용, 설치 불필요).
**직접 그린 그림이나 AI로 만든 그림으로 바꿀 때는 같은 이름의 PNG를 덮어쓰면 됩니다.** (덮어쓴 뒤에는 생성 스크립트를 실행하지 마세요.)

## 넣는 법

1. 아래 이름으로 PNG를 `Assets/Sprites/Heroes/` 에 넣는다. (Unity가 자동으로 UI용 스프라이트로 가져온다)
2. 메뉴 **Samkuk > Step 10-5 - Link Hero Portraits** 를 실행한다. (장수 에셋에 초상화가 연결된다)
3. ▶ Play → 장수 선택 화면에서 확인한다.

| 장수 | 파일 이름 |
|---|---|
| 유비 | `Hero_LiuBei.png` |
| 관우 | `Hero_GuanYu.png` |
| 장비 | `Hero_ZhangFei.png` |
| 조조 | `Hero_CaoCao.png` |
| 여포 | `Hero_LvBu.png` |

이미 연결된 그림을 **같은 이름의 PNG로 덮어쓰면** 메뉴를 다시 실행할 필요 없이 바뀝니다.
다른 파일 이름을 쓰고 싶으면 장수 에셋(`Assets/ScriptableObjects/Heroes/Hero_*.asset`)의 `Portrait` 칸에 직접 끌어 넣으면 됩니다.

## 규격

| 항목 | 값 |
|---|---|
| 비율 | **세로 4:5** |
| 크기 | **512 x 640 px** 권장 (최대 1024 x 1280) |
| 형식 | PNG, **배경 투명** (카드 바탕색이 비쳐 보임) |
| 구도 | 가슴 위(바스트 샷), 얼굴이 위쪽 1/3 지점, 시선은 정면 또는 살짝 비스듬히 |
| 여백 | 머리 위와 좌우에 약간의 여백 (카드 안에서 잘리지 않도록. 투구 깃이나 무기가 가장자리에 닿지 않게) |
| 화면 표시 크기 | 카드 안에서 약 176 x 220 (1920x1080 기준). 작게 보이므로 **실루엣과 색이 한눈에 구분**되는 쪽이 좋음 |

5명이 한 화면에 나란히 보이므로 **화풍, 선 굵기, 채도, 조명 방향을 통일**하는 것이 가장 중요합니다.
장수별 구분은 색으로 하면 좋습니다(카드 안의 실루엣 색과 같은 계열이면 자연스럽습니다).

| 장수 | 대표색 | 인상 |
|---|---|---|
| 유비 | 연두 / 옥색 | 온화하고 인자한 군주. 쌍고검 |
| 관우 | 짙은 녹색 | 긴 수염, 위엄 있는 무신. 청룡언월도 |
| 장비 | 청회색 / 남색 | 부리부리한 눈, 덥수룩한 수염, 거구. 장팔사모 |
| 조조 | 파랑 | 날카롭고 영리한 눈빛, 절제된 위엄. 의천검 |
| 여포 | 주황 / 붉은 기운 | 꿩 깃 투구, 압도적인 기세. 방천화극 |

## AI 이미지 생성용 프롬프트 (예시)

공통 접두어(화풍 통일용)를 모든 장수 앞에 똑같이 붙여 주세요.

```
공통: Three Kingdoms warlord character portrait, bust shot, front-facing, 2D game illustration,
bold ink outlines with painterly cel shading, rich saturated colors, dramatic rim lighting,
transparent background, centered, vertical 4:5 composition, no text, no watermark
```

| 장수 | 이어 붙일 문장 |
|---|---|
| 유비 | `Liu Bei, gentle and benevolent lord, kind eyes, light jade-green robes with gold trim, elegant long ears, twin swords on his back` |
| 관우 | `Guan Yu, majestic war god, extremely long flowing beard, red-tinged face, deep green robes and armor, holding Green Dragon Crescent Blade` |
| 장비 | `Zhang Fei, fierce giant general, wide glaring eyes, thick wild black beard, steel-blue armor, holding a serpent spear` |
| 조조 | `Cao Cao, cunning ambitious warlord, sharp intelligent eyes, thin mustache, dark blue lamellar armor with a cape, calm commanding presence` |
| 여포 | `Lu Bu, unrivaled warrior, pheasant-feather helmet, red cloak, orange and crimson armor, overwhelming aura, holding Sky Piercer halberd` |

팁: 투명 배경이 잘 안 나오면 단색(예: 연회색) 배경으로 뽑아 배경 제거 도구로 지운 뒤 PNG로 저장하세요.
