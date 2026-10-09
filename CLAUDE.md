# CLAUDE.md — 삼국지 서바이버 (unity_samkuk)

뱀파이어 서바이버류 로그라이크. Unity 6000.5.6f1, URP 2D, 신규 Input System 전용, PC 대상.
초기 기획은 `PLAN.md`, 이 문서는 **현재 상태 / 남은 작업 / 작업 규칙**을 관리한다.

> **문서 유지 규칙 (반드시 지킬 것)**
> 작업을 마칠 때마다(Step 또는 하위 Step 완료, 설계 변경, 새 규칙/함정 발견 시) 이 파일의
> `진행 상황`, `남은 작업`, `알려진 이슈`, `작업 규칙`을 **같은 커밋에** 함께 갱신한다.
> 사용자 확인 대기 중인 작업은 상태를 `확인 대기`로 적고, 확인 후 커밋하면서 `완료`로 바꾼다.

---

## 진행 상황

마지막 갱신: 2026-10-09 (12-6 성마다 다른 전투 맵 완료 + 강 없는 평야('완' 등)가 휑해서 소품 무리/바닥 얼룩/밀도 상향 완료. 타격감/연출 작업은 병행)

| Step | 내용 | 상태 | 커밋 |
|---|---|---|---|
| 1 | 기반 세팅(폴더, 레이어, 플레이스홀더, 카메라) | 완료 | 2fddde8 |
| 2 | 플레이어 이동, 카메라 추적, 무한 배경 | 완료 | b93aa99 |
| 3 | 적 스폰/추적(풀링, 원형 스폰, 겹침 방지) | 완료 | 2b45b27 |
| 4~5 | 전투 코어, 자동 공격 무기(검/활/회전 도끼) | 완료 | c57d53b |
| 6 | 경험치/레벨업 3지선다 | 완료 | 748552c |
| 7 | 웨이브, 엘리트, 보스(여포), 스테이지 클리어 | 완료 | 5c318e4 |
| 8-1 | 장수 5명, 고유 스킬, 장수 선택 | 완료 | 636ccb0 |
| 8-2 | 무기 10종(+넉백, WeaponFx 풀) | 완료 | b762832 |
| 8-3 | 적 병과(궁병 3종, 적 투사체), 선택 카드 반응형 | 완료 | 0b6c605 |
| 8-4 | 진화 무기 7종/진화 시스템 | 완료 | 78b978e |
| 9-1 | 저장, 영구 강화 로직, 결과 화면, 일시정지 | 완료 | ea433f4 |
| 9-2 | 타이틀 씬, 영구 강화 상점, 기록, 저장 초기화, 씬 흐름 | 완료 | 082e8d4 |
| 10-2 | 효과음(코드 합성 `SfxSynth`, `AudioManager`, `SfxHooks`, 타이틀 효과음 볼륨 버튼) | 완료 | ce823ba |
| 10-1 | 타격감(화면 흔들림 `ScreenShake`, 입자 `BurstFx`, 피격 번쩍임 `DamageFlashView`, 데미지 숫자 단계, `FeedbackHooks`, 타이틀 화면 흔들림 설정) | 완료 | 94e4e26 |
| 10-3 | 중국풍 UI 스킨(`UiTheme` 에셋, `UiSkin` 이름 규칙 스킨, 코드 생성 프레임 스프라이트, 레벨업 카드 종류별 색, 타이틀 구분선) | 완료 | adbad4a |
| 10-4 | 밸런싱(목표 난이도 "보통"): `BalanceModel` 어림 모델 + 밸런스 테스트, 경험치 곡선, 선택지 가중치, 장수 무기 진화 5종, 웨이브/무기 수치 조정 | 완료 | 25f8439 |
| 10-5 | 장수 선택 카드 초상화: `HeroData.portrait` 슬롯, 카드 4:5 레이아웃, 메뉴 `Step 10-5` 연결, 규격/프롬프트 `docs/HERO_PORTRAITS.md` (임시 그림 5장은 코드로 생성) | 완료 | 9607907 |
| 10-6 | 게임 안 장수 4방향 걷기 애니메이션: `HeroData.walkSheet`, `HeroSpriteSet`(4x4 시트를 실행 중 슬라이스), `PlayerAnimator`, 메뉴 `Step 10-6`, 규격 `docs/HERO_WALK_SHEETS.md` (임시 그림 5장은 코드로 생성) | 완료 | 69c35a0 |
| 10-7 | 적 10종 4방향 걷기 애니메이션: `EnemyData.walkSheet`, `Enemy.TickAnimation`(EnemyManager 이동 루프에서 호출, 플레이어를 바라봄), 메뉴 `Step 10-7`, 기병/보스는 말 + 기수 (임시 그림은 코드로 생성) | 완료 | 15be588 |
| 10-8 | 아군(동행 장수): 장수 선택 뒤 나머지 중 2명을 골라 데려감. 주인공을 따라다니며 시작 무기로 자동 공격, 적에게 공격받아 쓰러지면 15초 뒤 부활. `AllyController/AllyManager/AllyConfig`, `IEnemyTarget`(적 목표 확장), `IWeaponStats`/`WeaponFactory`(무기 소유자 일반화), `AllySelectUI`, 메뉴 `Step 10-8`, 설명 `docs/ALLIES.md` | 완료 | 38e097b |
| 12-1 | 내정(삼국지3식) 시작: 성 46곳 `CastleData`/`CastleCatalog`(이름, 한자, 주, 지형, 크기, 전략 지도 위치, 인접 성), 성 배경 그림 46장(코드 생성 `tools/castle_art`, 지형·크기·강·시간대별), 셋업 `Step12CastleSetup`(메뉴 `Step 12-1`), 설명 `docs/CASTLES.md`. 내정 수치/화면은 아직 없음 | 완료 | 247b504 |
| 12-2 | 내정 화면 1번(지도 + 성 화면): 새 씬 `StrategyScene`(빌드 2번), 전략 지도 그림(주별 색 영역, 해안·강·산, 코드 생성 `StrategyMap.png`), 성 마커 46개 + 인접 길, 성 선택 → 정보 상자 → 성 화면(배경 + 인접 성 이동), ESC = 성 → 지도 → 타이틀. `StrategyModel`(규칙) / `StrategyUI`(화면을 코드로 생성), 타이틀 [내정] 버튼, 메뉴 `Step 12-2`. 내정 수치/명령은 아직 없음 | 완료 | 247b504 |
| 12-3 | 해상도 상향: 성 배경 1920x1080 / 전략 지도 2560x1440(그림 재생성), 내정 UI 기준 해상도 2560x1440(`StrategyUI.ReferenceResolution`, 지도 영역 1960x1102), 타이틀 해상도/창 모드 버튼(`DisplaySettings`, `SaveData.displayWidth/displayHeight/windowMode`) | 완료 | 247b504 |
| 12-4 | 시작 성 선택과 출진: 내정을 열면 시작 성을 골라 "내 성"으로 저장(`SaveData.homeCastleId`, ★ 표시, [이 성에서 시작]/[시작 성 변경]), 내 성의 성 화면에서 [출진] → 전투(`GameSession.SortieCastle`, 성 지형 색 `BattleTerrain`), 결과 화면 [내정으로 (M)]. 타이틀 [시작]은 성 없이 시작. 전투 결과의 성 반영은 아직 없음 | 완료 | 13db09a |
| 12-5 | 정복: 내 영토(시작 성+정복한 성)의 성 화면에서 [출진] → 이웃한 적 성 중 공격 대상을 고르는 선택창 → 전투(대상 성 지형) → 승리하면 그 성을 영토로(`Territory`, `SaveData.ownedCastleIds`). 지도에 영토/공격 가능 성 테두리 색과 "보유 성 n / 46", 천하 통일 문구. 결과 화면에 정복/퇴각 문구. 난이도·적 반격·보상은 아직 없음 | 완료 | e449b70 |
| 12-6 | 성마다 다른 전투 맵: 지형별 바닥 타일 6종 + 지형 소품 30종 + 공용(연못, 깃발) 그림(코드 생성 `tools/terrain_art`), `TerrainTheme`/`TerrainThemeCatalog`(`Resources`), 성 아이디 시드로 밀도·소품 비율·바닥 색조가 달라지는 `TerrainMap`, 화면 주변 칸만 만드는 `TerrainPropSpawner`, `InfiniteBackground.ApplyCastle`, 메뉴 `Step 12-6`, 설명 `docs/TERRAIN.md`. 소품과 강은 충돌 없는 장식, 지형 효과/막는 지형은 아직 없음. **카탈로그 스크립트 연결 복구**: `TerrainThemeCatalog` 를 자기 파일로 분리해 `Resources/TerrainThemeCatalog.asset` 의 끊긴 스크립트 연결을 되살림(이게 '완' 맵이 텅 비었던 진짜 원인). **덜 휑하게**: 소품 흩뿌림 x1.4 + 같은 소품 무리 + 코드로 만든 바닥 얼룩(`TerrainDecals`, `TerrainMap.LayoutPatches`, 그리기 순서 `OrderPatch`). **강**: 강이 있는 성/강변 지형/성 없이 시작한 판(평야+강)에 구불구불 흐르는 강(`River_*` 토막 12장, `TerrainMap` 강 계산) | 완료 | 5aebcbd |
| 11 | PC 빌드(친구 공유용 Windows 빌드 환경): 메뉴 `Samkuk > Build`(릴리스/개발용) 또는 `tools/build_windows.ps1` → `Builds/` 에 실행 폴더 + 공유용 zip(README.txt 포함), 릴리스는 DebugOverlay/F1~F4 치트 꺼짐, 타이틀에 버전 표시(`BuildTool`, `docs/BUILD.md`). 최적화(GC/풀링)·아이콘·설치 파일은 아직 | 완료 | 15e5ad2 |

## 남은 작업

1. **Step 10 나머지** (진행하면서 이 목록을 갱신)
   - 장수/적 스프라이트 교체 지점 정리 (10-3 UI 스킨은 완료)
   - 10-4 밸런싱: 1분 스테이지 기준 난이도 곡선, 무기/진화/영구 강화 수치 점검
   - (선택) BGM: 현재는 효과음만 있음. `AudioManager`에 BGM 슬롯/볼륨을 추가하는 작업이 남아 있음
2. **10-4 이후 체감 조정**: 직접 플레이해 보고 "너무 쉬움/어려움" 구간을 알려주면 `BalanceModel` 보고서(메뉴 `Samkuk > Balance Report`)를 보며 수치를 조정한다.
3. **Step 11 나머지**: 친구 테스트에서 나온 문제 반영, 최적화(GC/풀링 점검), 앱 아이콘/회사명(저장 경로가 바뀌므로 주의), 필요 시 모바일 터치. 빌드 방법/주의는 `docs/BUILD.md`.
4. **내정(Step 12) 이어서**: 12-1~12-5(성, 지도, 성 화면, 시작 성, 출진/정복) 다음 — 성별 내정 수치(농업/상업/인구/방어 등)와 명령(개발/징병 등)을 성 화면의 명령 자리(`CastlePanel` 아래 띠 `Note`)에 붙이기, 성 규모/지형에 따른 전투 난이도·지형 효과(느려짐 등), 정복 보상, 적 반격/세력 등. 사용자 방향을 확인한 뒤 진행.
5. **미결정(PLAN.md 8번)**: 모바일 포함 여부, 실제 아트 에셋 사용 여부, 장수/무기 최종 목록.

## 알려진 이슈 / 메모

- 스프라이트(번개, 불길, 궁병, 기병 등)는 코드로 만든 **플레이스홀더**. Step 10에서 교체/다듬기.
- 장수 걷기 시트: `Assets/Sprites/HeroWalk/<장수 에셋 이름>_Walk.png`(4열 x 4행: 열=프레임 0~3, 행=아래/위/왼쪽/오른쪽, 칸 96x96, 배경 투명). 현재 5장은 **코드로 그린 임시 그림**(`tools/hero_art/HeroSprites.cs`, `generate.ps1 -Only walk`). 시트가 있는 장수는 `PlayerController`가 좌우 반전을 하지 않고 `PlayerAnimator`가 방향/프레임을 정하며, 장수 색(`tint`)도 입히지 않는다(`HeroSelectController.Apply`). 시트가 없으면 예전 동작(원 스프라이트 + tint + 반전). Player 프리팹에 `PlayerAnimator`를 붙이는 것은 `Step 10-6`이므로 셋업을 돌려야 한다.
- 전투 맵: 출진한 성(`GameSession.SortieCastle`)의 지형으로 `InfiniteBackground.ApplyCastle` 이 바닥 타일을 바꾸고 `TerrainProps` 루트(배경의 자식이 아님: 배경은 카메라를 따라 움직임)에 소품을 흩뿌린다. 데이터는 `Resources/TerrainThemeCatalog.asset`(씬 연결 없음)이며 **없거나 그림이 비면 기존 색 덮개(`BattleTerrain`)로 대신**한다. (타이틀 [시작] 처럼 성 없이 시작한 판도 평야+강 고정 맵(`TerrainMap.CreateFreeBattle`)을 쓴다. 강은 세계에 60유닛 간격으로 평행하게 흐르는 사인 곡선이고 토막(256x128)을 1.5유닛 간격으로 겹쳐 놓으며(강둑 아래, 물 위, 순서 1/2) 칸 경계에서 중복/끊김이 없다(정수 격자). 소품이/연못은 강 위에 놓이지 않는다. 같은 성은 항상 같은 맵(성 id 시드, 12x12 칸 단위 결정적 배치)이고 성마다 밀도/소품 비율/색조가 다르다. 소품은 Background 정렬 레이어의 장식(충돌 없음, 시작 위치 반경 2.5 비움). 그림은 코드로 만든 **임시 그림**(`tools/terrain_art/generate.ps1`, 직접 그린 그림으로 덮어쓰면 해당 지형은 `-Only` 로 피해서 실행). 밀도/비중은 `Theme_*.asset` 을 직접 고친다(셋업은 기존 값을 덮어쓰지 않음). 설명은 `docs/TERRAIN.md`.
- 빌드: `BuildTool`(메뉴 `Samkuk > Build`, 명령줄 `tools/build_windows.ps1`)이 빌드 설정(타이틀 0, 전투 1, 내정 2) 확인 → 빌드 → README.txt → zip 까지 한다. 결과는 `Builds/`(git 제외). **릴리스 빌드는 `DebugOverlay`(FPS, F1~F4 치트)가 꺼지고**(`Debug.isDebugBuild` 로 구분), 개발용(Development Build)만 켜진다. 에디터가 프로젝트를 열고 있으면 명령줄 빌드는 실행되지 않는다. 저장 경로는 Company/Product Name(`DefaultCompany/Samkuk`)에서 나오므로 이름을 바꾸면 저장 위치가 바뀐다. 이 환경(Claude)에서는 유니티를 실행하지 못해 **빌드는 사용자가 직접 확인**한다. 자세한 사용법은 `docs/BUILD.md`.
- 내정 시작/출진: 내정을 처음 열면 시작 성을 골라 내 성(`SaveData.homeCastleId`)으로 저장하고, 영토(`ownedCastleIds`)의 성 화면에서 [출진] → 이웃한 적 성 선택창에서 공격 대상을 고른다. 대상 성(`GameSession.SortieCastle`, 출발 성은 `SortieOrigin`)이 전투로 전달돼 `InfiniteBackground` 가 성 지형 색(`BattleTerrain`)을 배경 위에 한 겹 덮고, 결과 화면에 [내정으로] 가 생긴다. 승리하면 `ResultController` 가 대상 성을 `Territory.Conquer` 로 영토에 넣는다(정복은 결과 정산 때만 저장, 패배/재도전은 영토 불변, 영토가 늘면 시작 성 변경 불가)(Step 9 셋업이 만드는 버튼이라 **Step 9/Run All 재실행 필요**). 타이틀 [시작] 은 `SortieCastle` 을 비우는 "성 없이 시작"이다. 규칙은 `docs/CASTLES.md`.
- Play 시작 씬: 메뉴 `Samkuk > Play From Title Scene`(`EditorSceneManager.playModeStartScene`)은 에디터를 열 때마다 `Step9TitleSetup.EnsurePlayFromTitle`(`InitializeOnLoadMethod`)이 켜 둔다. 끄고 싶으면 그 메뉴로 끄면 프로젝트별로 기억한다. 예전에는 Step 9-2 셋업을 돌릴 때만 켜져서, 에디터를 다시 열면 풀려 마지막으로 열어 둔 내정 씬에서 Play 가 시작됐다(마지막 성 화면). 빌드는 원래 타이틀(0번)부터 시작한다.
- 씬 이름: 유니티 템플릿 기본 이름이던 `SampleScene` 을 역할에 맞게 `BattleScene`(서바이버 전투 한 판)으로 바꿨다. `.meta` 와 함께 옮겨 GUID 는 그대로이며, 코드 상수는 `GameManager.BattleSceneName`, 에디터 셋업은 `Step9TitleSetup.BattleScenePath`. 이전 이름이 남은 곳은 초기 기획 `PLAN.md` 뿐(당시 상태를 적은 문서라 그대로 둠).
- 내정 화면: 씬 `StrategyScene`(타이틀 0, 게임 1, 내정 2 순서로 `Step12StrategySetup.RegisterBuildScenes` 가 등록)에는 카메라/이벤트 시스템/캔버스(+`StrategyUI`)만 있고, **화면 전체는 `StrategyUI` 가 실행 중에 코드로 만든다**(성이 바뀌어도 씬 재구성 불필요). 규칙은 `StrategyModel`(선택/입장/인접 이동, 마지막 성은 `StrategySession` 에 기억). 지도 그림 `Assets/Sprites/Strategy/StrategyMap.png`(2560x1440)는 코드로 만든 **임시 그림**(`tools/castle_art/MapArt.cs`, `generate.ps1 -Only Map`)이며 성 마커는 `mapPosition`(0~1)을 지도 영역(1960x1102)에 곱해 놓는다. 타이틀의 [내정] 버튼은 Step 9-2 를 **다시 실행해야** 생기고(없어도 동작), 버튼 배치가 바뀌었다. 런타임 UI라 스킨은 `UiSkin.StyleX` 를 직접 호출한다.
- 내정 성: 기준표는 `tools/castle_art/castles.json` 한 곳(46곳, `links` 는 `"A-B"` 양방향)이고 그림 생성기와 `Step12CastleSetup` 이 같이 읽는다. 성 목록은 삼국지3의 도시 수(46)에 맞춘 **후한 말 지리 기준 근사**이며 원작 목록과 다를 수 있다. 배경(`Assets/Sprites/Castles/Castle_<id>.png`, 1920x1080 16:9)은 코드로 만든 **임시 그림**이고, 직접 그린 그림으로 덮어쓰면 그 성은 `generate.ps1 -Only`에서 피한다. 셋업은 멱등(기존 성 값은 덮어쓰지 않고 빠진 배경/연결만 채움)이라 값을 바꿀 땐 에셋을 직접 고친다. `CastleData` 가 Sprite 를 직접 참조해 카탈로그 로드 시 46장이 함께 로드된다(압축 후 약 50MB) — 내정 화면에서 부담이 되면 지연 로드로 바꿀 것. 규칙/방법은 `docs/CASTLES.md`.
- 아군: 수치는 `AllyConfig` 한 곳(정원 2, 체력 70, 공격력 x0.6, 받는 피해 x0.7, 부활 15초, 주인공 레벨 3당 무기 +1). 적은 `EnemyManager`가 주인공과 살아 있는 아군 중 **가장 가까운 쪽**을 목표로 고르고(`IEnemyTarget`), 접촉/궁병 화살도 그 대상에 적용된다. 무기는 `Weapon.Owner`가 `Transform`+`IWeaponStats`라 아군도 같은 무기를 쓴다(이펙트/투사체 풀은 주인공의 `WeaponController`를 공유). 아군은 스킬을 쓰지 않는다. `HeroSelectController`는 아군 UI/매니저를 실행 중에 찾으므로 Step 8을 다시 돌려도 연결이 유지된다. 규칙/흐름은 `docs/ALLIES.md`.
- 적 걷기 시트: `Assets/Sprites/EnemyWalk/<적 에셋 이름>_Walk.png`(장수와 같은 4x4 규격, `HeroSpriteSet` 재사용). 시트가 있는 적은 그림 색 그대로(`Enemy.BaseColor`가 흰색)이고 `SetFacing` 좌우 반전 대신 `TickAnimation`으로 플레이어를 바라본다. 시트가 없으면 예전 동작(단색 스프라이트 + tint + 반전). 크기는 `walkPixelsPerUnit` x `scale`. 임시 그림은 `tools/hero_art/EnemySprites.cs`, `generate.ps1 -Only enemy`. 규격/목록은 `docs/HERO_WALK_SHEETS.md`.
- 장수 초상화: 현재 5장은 **코드로 그린 임시 그림**(`tools/hero_art/HeroArt.cs`, `generate.ps1`로 재생성, 직접 그린 그림으로 덮어쓰면 생성 스크립트는 쓰지 않는다). 교체용 그림은 사용자가 준비해 `Assets/Sprites/Heroes/<장수 에셋 이름>.png`(512x640, 세로 4:5, 투명 배경)에 넣고 메뉴 `Step 10-5`로 연결한다. 가져오기 설정은 `HeroPortraitImporter`가 자동으로 맞춘다. 그림이 없는 장수는 기존 실루엣 + `tint`로 보이고(`HeroSelectUI.ShowPortrait`), 이미 연결된 초상화는 셋업이 덮어쓰지 않는다. 카드 레이아웃을 키웠으므로(높이 640→700) 적용하려면 `Step 8`(또는 Run All)을 다시 실행해야 한다.
- 장수 전용 시작 무기(쌍고검, 청룡언월도 등)는 진화 대상이 아니다. 필요하면 `EvolutionData` 추가.
- 게임 씬은 한 판 1분 스테이지 기준이라 진화 필요 레벨을 5로 낮춰 둠(`Evo_*.asset`의 Required Level).
- **밸런스 수치는 어림 모델 기준**: `BalanceModel`은 처치율/무기별 동시 타격 수/평균 빌드를 가정한 근사라서 "방향"과 "의도한 범위 이탈 여부"만 알려 준다. 최종 체감은 직접 플레이로 확인(특히 마지막 웨이브 난이도, 진화 도달 시점).
- 밸런스 기준: 60초에 약 11레벨(경험치 곡선 `PlayerExperience.BaseRequired/RequiredStep` = 7/6), 웨이브 난이도(압박비) 약 2.1 → 2.0 → 1.3 → 1.0 (적 수를 처음보다 30% 줄여 한때의 1.5 → 1.4 → 0.9 → 0.7보다 쉬움. 난이도를 되돌리려면 적 체력을 키우는 방법이 있음), 진화는 무기 Lv.3 + 짝 패시브(집중 육성 약 70%, 무작위 약 15%), 레벨업 선택지 가중치 = 보유 무기 강화 4 : 패시브 2 : 새 무기 1(`UpgradeCatalog`).
- **[임시] 타격감 집중 기간의 적 수**: 웨이브 스폰 속도/동시 최대 수를 직전 값의 30%로 줄임(0.63/1.26/초, 최대 17/25/34/46). 경험치 곡선과 밸런스 테스트(레벨 속도, 압박비, 진화 도달)는 **일부러 손대지 않아** 이 값에서는 맞지 않는다 — 타격감 작업이 끝나면 적 수를 되돌리거나 곡선/테스트를 다시 맞출 것. 직전 값(30% 감소 상태): 2.1/4.2초, 최대 56/84/112/154. 그 이전 단계: 처음보다 30% 줄임(황건적 습격 3 → 2.1/초·최대 80 → 56, 이후 웨이브 6 → 4.2/초 등). 처치 수가 줄어 경험치 곡선을 10/8 → 7/6으로 낮춰 레벨 속도를 유지. 값은 `Step7Setup`(웨이브) + `Step10BalanceSetup.Overrides`(기존 스테이지 에셋).
- 화살 속도: **아군 화살(Arrow 계열 무기 8종)은 빠르게**(12~18, 한때 줄였다가 사용자 요청으로 복원), **적 궁병 화살은 아주 느리게**(2/2.5/3, 플레이어 이동 속도 4의 80% 이하. 도달 거리를 지키려고 수명 4.5초). "화살이 빠르다"는 말은 처음에 적 화살을 가리켰는데 아군 화살로 오해해 두 번 헛수정했다 — 속도 관련 요청은 아군/적 어느 쪽인지 먼저 확인한다. 값은 셋업 기본값 + `Step10BalanceSetup.Overrides` 표, 검사는 `BalanceTests.ArrowWeapons_AreFast_...`(아군 ≥ 10, 도달 거리 ≥ 탐색 거리)와 `EnemyArrows_AreSlowEnoughToDodge_...`(속도 ≤ 이동 속도×0.8, 도달 거리 ≥ 사거리×1.15).
- UI 스킨 스프라이트(`Assets/Sprites/UI/*.png`)는 코드로 만든 **임시 아트**. 직접 만든 PNG로 교체해도 셋업이 덮어쓰지 않는다(`Resources/UiTheme.asset` 슬롯만 채움). 모양을 다시 생성하려면 해당 PNG를 지우고 `Step 10-3` 실행.
- 폰트는 아직 OS 한글 폰트(맑은 고딕). 붓글씨체 등을 쓰려면 ttf를 `UiTheme.font` 슬롯에 지정(코드 수정 불필요).
- 타격감 수치(흔들림 세기, 입자 개수, 번쩍임 강도)는 `FeedbackHooks`에 상수로 있음. 느낌이 과하거나 부족하면 거기서 조정. 입자는 코드로 만든 4x4 흰 사각형 스프라이트(에셋 없음).
- 효과음은 코드로 합성한 **임시 소리**(`SfxSynth`). 실제 음원은 `AudioManager`의 `overrides` 슬롯에 클립을 넣어 교체(코드 수정 불필요).
- 효과음 볼륨은 `SaveData.sfxVolume`(기본 0.7)에 저장, 타이틀 왼쪽 위 버튼으로 끔/작게/보통/크게 순환. 게임 중 볼륨 조절 UI는 아직 없음.
- git의 `LF will be replaced by CRLF` 경고는 무시해도 된다.

---

## 프로젝트 구조

- 코드: `Assets/Scripts/<영역>/` — Core, Player, Enemy, Weapon, Skill, Hero, Stage, Pickup, Upgrade, Meta, Audio, Feedback, Balance, Strategy, World, UI, Data, Editor
  - 네임스페이스는 `Samkuk.*` (예: `Samkuk.Weapons`, `Samkuk.Enemies`, `Samkuk.Meta`, `Samkuk.UI`)
  - 런타임은 `Samkuk.Runtime.asmdef`, 에디터 스크립트는 `Editor/` 폴더
- 데이터: `Assets/ScriptableObjects/<종류>/` — Weapons, Enemies, Passives, Heroes, Skills, Evolutions, Meta, Stage, Castles, 그리고 `UpgradeCatalog`, `HeroCatalog`, `MetaCatalog`, `CastleCatalog`
- 씬: `Assets/Scenes/TitleScene.unity`(빌드 0번) → `BattleScene.unity`(전투, 1번) → `StrategyScene.unity`(내정, 2번)
- 테스트: `Assets/Tests/PlayMode/*.cs` (PlayMode, `Samkuk.Tests.PlayMode.asmdef`)
- 저장: `Application.persistentDataPath/save.json` (`SaveSystem`)

### 핵심 구조 요약
- 무기: `WeaponData`(type 별로 `Weapon` 서브클래스) → `WeaponController.AddWeapon`이 type으로 클래스를 고른다. 새 `WeaponType`을 추가하면 **AddWeapon의 switch와 테스트의 기대 매핑**도 추가.
- 적: `EnemyManager`가 이동/접촉 피해/궁병 사격을 일괄 처리, 공간 그리드로 범위 질의(`OverlapCircle`, `FindNearest`). 그리드가 낡으면 전수 검색으로 대체(신규 스폰 누락 방지).
- 레벨업: `UpgradeGenerator`가 선택지 생성(진화 카드 최우선). 진화는 `WeaponController.Evolve`.
- 밸런스: `Samkuk.Balance.BalanceModel`(순수 계산) → `BalanceTests`가 범위를 고정한다. 수치를 바꾸면 이 테스트가 "의도한 흐름에서 벗어났는가"를 알려 준다. 기존 에셋은 셋업이 덮어쓰지 않으므로 값을 바꿀 때는 **셋업의 기본값 + `Step10BalanceSetup.Overrides` 표**를 함께 고친다(웨이브는 Step 7 셋업이 매번 다시 쓴다). 진화 필요 레벨은 `BalanceModel.EvolutionRequiredLevel` 한 곳.
- 능력치: `PlayerStats` 계층 = 패시브 × 장수 × 영구 강화(`ApplyMeta`) × 버프.
- 한 판의 끝: `GameManager.Finished` → `ResultController`가 골드 계산/저장 → 결과 화면. 일시정지는 `PauseController`(ESC).
- 타격감: `FeedbackHooks`(GameSystems)가 이벤트→연출을 연결. 화면 흔들림은 카메라의 `ScreenShake`가 `Offset`만 계산하고 `CameraFollow`가 추적 위치(basePos)에 더해 적용(카메라 위치를 직접 흔들면 추적 보간에 섞여 누적됨). 흔들림은 `SaveData.screenShake`로 끌 수 있음(타이틀 버튼). 처치 비중은 적 maxHp로 구분(보스 300+, 정예 60+).
- 소리: `AudioManager`는 처음 `Play`할 때 스스로 만들어지는 싱글턴(씬에 둘 필요 없음, `DontDestroyOnLoad`). 같은 소리는 소리별 최소 간격으로 제한하고 AudioSource 8개를 돌려 쓴다. 게임 이벤트→소리 연결은 `SfxHooks`(GameSystems), 무기 공격음은 각 무기가 `PlayAttackSound()`, UI 버튼은 각 UI의 `Bind`에서 클릭음. 새 효과음은 `SfxId` + `SfxSynth` 레시피를 추가하고, 새 무기 종류는 `SfxMap.ForWeapon`에도 추가.
- UI 테마: 색/프레임/폰트는 `Resources/UiTheme.asset`(`UiTheme.Get()`) 한 곳. `UiSkin`(HUD·타이틀 캔버스에 부착)이 **이름 규칙**으로 입힌다 — `Card#`/`Hero#`+Button=카드, 그 밖의 Button=버튼, `Box`=패널, `HpBar/ExpBar/BossBar/SkillHud`=얇은 프레임, 이름이 `Title`인 Text=금색. **새 UI를 만들 때 이 이름을 따르면 자동으로 스킨이 입혀진다.** 런타임에 만드는 UI는 `UiSkin.StyleCard/StyleButton`을 직접 호출. 레벨업 카드 색은 `UiSkin.CardTint(kind)`.
- UI는 uGUI 레거시 `Text` + `UiFont`(한글 폰트). 뷰는 인터페이스(`ILevelUpView`, `IResultView`, `IPauseView`...)로 분리해 테스트에서 가짜 뷰를 쓴다.

### 디버그 키 (게임 씬)
F2 무기 레벨업, F3 경험치 지급, F4 스테이지 15초 건너뛰기, ESC 일시정지, R 재시작, T 타이틀(결과 화면).

---

## 작업 규칙

### 셋업 / 에셋
- 씬 배치와 프리팹/SO 생성은 에디터 스크립트로 자동화한다. 메뉴 `Samkuk > Run All Setup (Step 2-9 + 타이틀)`(10 효과음 + 타격감 + 10-3 테마 + 10-4 밸런스, 12-1 내정 성 포함) 한 번이면 전체 구성. 새 Step을 만들면 `SetupAll.cs`에도 추가한다.
- 셋업은 **멱등**이어야 한다: 이미 있는 에셋은 덮어쓰지 않아 사용자가 조정한 수치를 보존하고, 카탈로그는 **누적 방식**(없는 항목만 추가)으로 채운다.
- 셋업에서 `EditorSceneManager.OpenScene/NewScene` **이후에** 에셋을 로드한다 (씬 전환이 로드된 에셋 참조를 무효화할 수 있음).
- 진화 셋업(`Step8EvolutionSetup`)은 장수 시작 무기가 만들어진 **뒤**(Step 8 이후)에 실행해야 한다 (Run All 순서 참고).
- 셋업 순서 주의: Step 7을 단독 재실행하면 웨이브가 초기화되어 8-3의 궁병 편성이 빠진다 → 항상 `Run All`을 쓰거나 8-3을 이어서 실행.
- **ScriptableObject/MonoBehaviour 클래스는 파일 이름과 같은 이름으로 한 파일에 하나씩** 둔다. 다른 클래스와 한 파일에 넣으면(예전 `TerrainThemeCatalog` 가 `TerrainTheme.cs` 안에 있었음) 에셋의 `m_Script` 가 `{fileID: 0}` 으로 저장돼 "referenced script is missing" 경고와 함께 `Resources.Load` 가 null 을 돌려주고, 기능이 조용히 꺼진다(그때 전투 맵 소품/강이 전부 안 나와 기존 색 덮개로 대신 동작했다). 새 클래스를 만들 때 `.meta`(guid) 도 함께 만든다.
- 데이터 파일(`*.asset`, `*.unity`, `*.prefab`)은 Unity가 만든 것이므로 손으로 편집하지 말고 셋업 스크립트로 바꾼다.

### 코딩
- 주변 코드의 주석 밀도/이름/관용구를 따른다. 주석은 한국어, "왜"를 설명한다.
- `Update` 안의 할당을 피하고(풀링, 재사용 리스트), 무기/적/투사체는 풀링한다.
- 월드에 고정되어야 하는 이펙트/장판/충격파 링은 플레이어의 자식으로 두지 않는다.
- 필요한 대상이 없으면 쿨다운을 소모하지 않고 대기하는 무기 규칙을 유지한다.
- `Enemy.Damaged`는 `Alive=false`가 되기 **전에** 발생한다(마지막 타격 구분은 `Hp > 0`으로).
- 시간 정지(`timeScale = 0`) 중 동작하면 안 되는 것(스킬, 무기)과 되어야 하는 것(UI 확인 타이머는 `unscaledDeltaTime`)을 구분한다.

### 테스트 / 검증
- 새 기능에는 PlayMode 테스트를 추가한다. 저장 관련 테스트는 `SaveSystem.PathOverride`로 임시 경로를 써서 실제 `save.json`을 건드리지 않는다.
- 이 환경에서는 Unity를 직접 실행하지 못한다. 대신 Unity 번들 Roslyn(`csc.dll`)으로 **에디터 밖에서 컴파일 검사**를 한다
  (런타임 / 에디터 / 테스트 어셈블리 각각, 응답 파일은 `Temp/ci/*.rsp`에 있음 — `Temp/`는 로컬 전용이라 없으면 다시 만들어야 한다).
- 컴파일 통과 ≠ 동작 확인이다. 에디터 검증(셋업 실행, Play, Test Runner)은 **사용자가** 하고, 결과를 알려주면 이어서 진행한다. 검증하지 않은 것을 "동작한다"고 보고하지 않는다.

### 커밋 / 보고
- Step(또는 하위 Step)마다 커밋하되, **사용자가 확인한 뒤** 커밋한다.
- 커밋 메시지: `Step N-M: 제목` + 변경 요점 불릿 + 맨 끝에 `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- Step 완료 보고에는 ① 만든 것 ② 에디터에서 할 일(셋업 메뉴, Play 확인 항목, Test Runner) ③ 다음 Step 안내를 포함한다.
- 이 문서의 `진행 상황`/`남은 작업`을 같은 커밋에서 갱신한다.
