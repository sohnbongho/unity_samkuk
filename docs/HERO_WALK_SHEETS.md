# 장수 걷기 스프라이트 시트 (게임 안 4방향 애니메이션)

게임 중 플레이어는 고른 장수의 **걷기 시트**로 4방향(아래/위/왼쪽/오른쪽) 걷기 애니메이션을 재생합니다.
시트가 없는 장수는 예전처럼 기본 스프라이트 + 장수 색 + 좌우 반전으로 보입니다.

## 시트 규격

한 장의 PNG 안에 **4열 x 4행** 으로 칸을 나란히 둡니다. 칸은 모두 같은 크기의 정사각형이고, 시트 전체도 정사각형입니다.

| | 열 0 | 열 1 | 열 2 | 열 3 |
|---|---|---|---|---|
| **행 0: 아래(정면)** | 서 있기 | 한쪽 발 앞 | 서 있기 | 반대쪽 발 앞 |
| **행 1: 위(뒷모습)** | 〃 | 〃 | 〃 | 〃 |
| **행 2: 왼쪽** | 〃 | 〃 | 〃 | 〃 |
| **행 3: 오른쪽** | 〃 | 〃 | 〃 | 〃 |

- 열 0은 **멈춰 있을 때** 쓰는 자세입니다. 걷는 동안에는 1 → 2 → 3 → 0 순서로 돕니다(초당 약 9프레임).
- 칸 크기는 자유(권장 96x96, 시트 384x384). 칸 크기 = 시트 크기 / 4 로 자동 계산됩니다.
- 배경 투명 PNG. 캐릭터는 칸 가운데, 발은 칸 아래쪽(약 90%)에 두면 그림자와 몸의 중심이 맞습니다.
- 왼쪽 행은 오른쪽 행을 좌우 반전해도 됩니다(지금 그림이 그렇게 만들어져 있음).
- 한 칸이 월드에서 얼마나 큰지는 장수 에셋의 `Walk Pixels Per Unit` (기본 96 = 한 칸이 1유닛, 플레이어 충돌 반경 0.4)로 정합니다. 칸을 크게 그렸다면 이 값도 같이 키우세요.

## 넣는 법

1. 파일 이름 `<장수 에셋 이름>_Walk.png` 로 `Assets/Sprites/HeroWalk/` 에 넣는다. (Unity가 선명하게, 투명 유지로 자동 가져옴)

| 장수 | 파일 이름 |
|---|---|
| 유비 | `Hero_LiuBei_Walk.png` |
| 관우 | `Hero_GuanYu_Walk.png` |
| 장비 | `Hero_ZhangFei_Walk.png` |
| 조조 | `Hero_CaoCao_Walk.png` |
| 여포 | `Hero_LvBu_Walk.png` |

2. 메뉴 **Samkuk > Step 10-6 - Link Hero Walk Sheets** 를 실행한다. (Player 프리팹에 `PlayerAnimator` 추가 + 장수 에셋에 시트 연결)
3. ▶ Play → 장수를 고르고 WASD 로 움직여 확인한다.

이미 연결된 시트를 **같은 이름의 PNG로 덮어쓰면** 메뉴를 다시 실행하지 않아도 바뀝니다.
다른 파일 이름을 쓰려면 장수 에셋(`Assets/ScriptableObjects/Heroes/Hero_*.asset`)의 `Walk Sheet` 칸에 직접 끌어 넣으세요.

## 지금 들어 있는 그림 (임시)

5장 모두 **코드로 그린 임시 그림**(머리가 큰 2등신 평면 벡터풍)입니다. `tools/hero_art/HeroSprites.cs` 가 그리며
`powershell -File tools\hero_art\generate.ps1 -Only walk` 로 다시 만들 수 있습니다. 직접 그린 그림으로 덮어쓴 뒤에는 이 스크립트를 실행하지 마세요.

## 적 걷기 시트

적도 같은 규격(4열 x 4행, 칸 96x96, 투명 PNG)을 씁니다. 파일은 `Assets/Sprites/EnemyWalk/<적 에셋 이름>_Walk.png` 에 넣고
메뉴 **Samkuk > Step 10-7 - Link Enemy Walk Sheets** 로 `EnemyData.walkSheet` 에 연결합니다.

| 적 | 파일 이름 |
|---|---|
| 황건적 병사 | `Enemy_Soldier_Walk.png` |
| 황건적 척후 | `Enemy_Scout_Walk.png` |
| 황건 궁병 | `Enemy_YellowArcher_Walk.png` |
| 황건 장수 | `Enemy_YellowTurbanGeneral_Walk.png` |
| 동탁군 보병 | `Enemy_DongzhuoInfantry_Walk.png` |
| 동탁군 노수 | `Enemy_DongzhuoCrossbow_Walk.png` |
| 여포군 정예 | `Enemy_LvbuElite_Walk.png` |
| 여포군 신궁 | `Enemy_LvbuArcher_Walk.png` |
| 서량 기병 | `Enemy_XiliangCavalry_Walk.png` |
| 보스 여포 | `Boss_Lvbu_Walk.png` |

- 적은 **이동 방향이 아니라 플레이어를 바라봅니다**(그래서 물러나며 쏘는 궁병도 정면). 쫓아가는 동안만 프레임이 돌고, 플레이어 앞에서 멈추면 서 있는 자세입니다.
- 걷기 시트가 있으면 그림 색 그대로 보이고(`tint` 는 사망 입자 색에만 쓰임), 기절(푸른색)/돌진 예고(붉은색)/사격 예고(노란색) 깜빡임은 그림 위에 색을 곱해 그대로 표시됩니다.
- 크기는 `Walk Pixels Per Unit`(클수록 작게)과 적의 `Scale` 을 곱한 값입니다. 처음 연결할 때 `Step10EnemyWalkSetup` 표의 값으로 정해지며 이후에는 직접 바꿔도 덮어쓰지 않습니다.
- 임시 그림은 `powershell -File tools\hero_art\generate.ps1 -Only enemy` 로 다시 만들 수 있습니다 (`tools/hero_art/EnemySprites.cs`).
- 성능: 적마다 `Update` 를 돌리지 않고 `EnemyManager` 의 이동 루프 안에서 `Enemy.TickAnimation` 을 부르며, 스프라이트는 바뀔 때만 교체합니다.

## 동작 방식 (참고)

- `HeroSpriteSet` 이 시트를 실행 중에 4x4로 잘라 `Sprite` 16개를 만든다(에디터 슬라이스 불필요, 텍스처당 한 번만).
- `PlayerAnimator` 가 이동 입력의 큰 축으로 방향을 고르고(대각선은 떨림 방지로 1.2배 이상 차이 나야 전환), 움직이는 동안만 프레임을 돌린다. 일시정지(`timeScale 0`) 중에는 멈춘다.
- 장수를 고르면 `HeroSelectController.Apply` 가 `PlayerAnimator.SetHero` 를 호출한다. 걷기 시트가 있으면 장수 색(`tint`)을 입히지 않는다.
