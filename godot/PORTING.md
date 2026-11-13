# Godot 전환 작업 문서

Unity 프로젝트(저장소 루트의 `Assets/`)를 Godot 4.7.2 .NET 으로 옮기는 작업의 현재 상태와 방법. 결정과 이유는 `docs/adr/0004-godot-conversion.md`.
진행표는 루트 `CLAUDE.md` 의 "Godot 전환" 표가 원본이고, 이 문서는 **구조, 옮기는 방법, 확인이 필요한 것**을 적는다.

## 구조 한눈에

```
유니티 런타임 스크립트 (using 만 바꿔 그대로)          godot/core  (Samkuk.Core, 엔진 참조 없음)
        │  GameObject/MonoBehaviour/Transform ...       ├─ Compat/   장면 모형 + 호환 타입
        ▼                                               ├─ <영역>/   Player, Enemy, Weapon, UI, World ... (유니티와 같은 폴더)
   장면 모형 (SceneRuntime.Tick 으로 한 프레임)          └─ Data/     GameData(데이터 JSON), SceneFile/Prefabs(씬·프리팹 JSON)
        │  SpriteRenderer, Light, Mesh, Text, Image ...  = "무엇을 어디에 그릴지" 데이터
        ▼
   화면 층 (매 프레임 읽어서 비춘다)                     godot/game  (Samkuk.Game, Godot 노드)
        WorldMirror  → Sprite3D, 빛, 메시, 카메라        GameRoot.cs  입력 → Tick → 비추기
        UiMirror     → Label, NinePatchRect, LineEdit    View/        WorldMirror, UiMirror
        AudioOut     → AudioStreamPlayer                 Data/        GodotDataSource, Textures
```

- **게임 규칙은 전부 `godot/core`** 에 있고 엔진 없이 `dotnet test` 로 돈다. `godot/game` 은 보여 주고 입력을 전하는 일만 한다.
- **장면 모형**(`Samkuk.Compat`)은 유니티와 같은 이름·같은 수명 주기(Awake → OnEnable → Start → FixedUpdate/Update/LateUpdate → OnDisable → OnDestroy)의
  작은 오브젝트 모델이다. 그리지 않는다: 그림·빛·메시·UI 컴포넌트는 값만 들고, 화면 층이 그 값을 Godot 노드로 옮긴다.
- **씬과 프리팹**은 유니티의 `.unity`/`.prefab` 을 JSON 으로 옮긴 것(`godot/data/Scenes|Prefabs/*.scene.json`)이고 `SceneFile` 이 읽어 오브젝트를 만든다.
  씬 전환은 `SceneManager.LoadScene(이름)` → 장면 모형을 비우고 그 씬 파일을 읽는다.
- 좌표: 로직은 x 오른쪽, y 북쪽, 땅 z = 0, 위 = -z. Godot 은 `(x, -z, -y)` (Y 가 위, 북쪽 = -Z, 카메라는 남쪽에서 본다). 변환은 `WorldMirror.Map` 한 곳.

## 폴더

| 경로 | 프로젝트 | 내용 |
|---|---|---|
| `godot/core/` | `Samkuk.Core` (net8.0) | 순수 로직 + 장면 모형. 폴더는 유니티 `Assets/Scripts/<영역>/` 과 같다 |
| `godot/game/`, `godot/scenes/Main.tscn` | `Samkuk.Game` (Godot.NET.Sdk) | 화면 층. 씬 파일은 `Main.tscn` 하나(`GameRoot`)뿐이고 나머지는 실행 중에 비춘다 |
| `godot/tests/` | `Samkuk.Tests` (NUnit) | 유니티 PlayMode 테스트 42개 파일 + `GameDataTests` |
| `godot/exporter/` | `Samkuk.Exporter` (콘솔) | 서버 데이터 내보내기 (유니티 메뉴 `Samkuk > Export Server Data` 의 자리) |
| `godot/data/` | | 데이터 JSON 132건, 씬 4개·프리팹 5개, `project.json`(레이어·씬 순서), `sprites.json`(그림 색인) |
| `godot/sprites/` | | 그림 146장 |
| `tools/godot_port/` | | 옮길 때 쓰는 도구 |

`core/` 와 `tests/` 에는 `.gdignore` 가 있어 Godot 이 훑지 않는다.

## 실행

```bash
cd godot
dotnet test tests/Samkuk.Tests.csproj                 # 테스트 (엔진 없이)
dotnet build Samkuk.Game.csproj                       # 게임 C# 빌드
GODOT=/d/01-AgentLegion/tools/godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe
"$GODOT" --headless --path . --import                 # 리소스 임포트
"$GODOT" --headless --path . -- --smoke               # 120 프레임 돌리고 상태를 찍고 끝낸다
"$GODOT" --path . --resolution 1280x720 -- --scene=BattleScene "--press=30:digit2;60:enter" --keys=d --frames=420 --shot=C:/임시/battle.png
```

마지막 줄이 **자동 점검**이다: 전투 씬에서 30번째 프레임에 2번 장수를 고르고 60번째에 확인, d 키를 누른 채 420 프레임을 돌린 뒤 화면을 PNG 로 저장한다
(창이 잠깐 뜬다). 명령줄 옵션은 `GameRoot.cs` 머리말. 에디터로 열려면 `Godot_v4.7.2-stable_mono_win64.exe` 로 `godot/project.godot` 을 가져온다.

## 옮기는 방법

- **스크립트**: `tools/godot_port/copy_core.sh <Assets/Scripts 기준 경로>...` 가 파일을 `godot/core` 로 복사하며 `using` 을 바꾼다(`usings.sed`).
  이미 있는 파일은 건드리지 않는다. 빌드 오류는 `python tools/godot_port/errs.py core` 로 본다.
- **호환 층에 없는 유니티 기능이 필요하면** `Compat/` 에 같은 이름·같은 결과로 더한다. 그리는 것이 아니라 값을 드는 컴포넌트로 만들고,
  화면에 보여야 하면 `WorldMirror`/`UiMirror` 가 그 값을 읽게 한다.
- **씬·프리팹·데이터**: `convert_data.py`(데이터·그림), `convert_scenes.py`(씬·프리팹·프로젝트 설정). 유니티 원본에서 다시 만드는 도구라 **돌리면 덮어쓴다**.
- **파일 고치기**: 셸 인용 문제(백슬래시가 벗겨짐)를 피하려고 `patch.py <치환목록.json>` 을 쓴다.
- **테스트**: `copy_tests.sh <이름>...`(덮어쓴다) → `defer_tests.py`(컴파일 안 되는 테스트를 `#if GAME_PORT` 로 미룸) → `fails.py`(실패를 원인별로) →
  `hold_tests.py`(남은 실패를 이유와 함께 `[Ignore]`). 기능을 옮긴 뒤에는 `undefer_tests.py && defer_tests.py` 로 미룬 것을 다시 시도한다.

### 유니티에서 달라진 것

- 엔진에 묶인 것은 값만 남겼다: `Light2D`, 픽셀 퍼펙트 카메라, 가짜 드리운 그림자(`CastShadow`), 2D 정렬은 **값은 있지만 Godot 에서 그리지 않는다**(틸트 한 가지뿐).
  후처리 볼륨·틸트 시프트·렌더러 번호도 값만 있고 화면 층이 읽는다(G4).
- `BattleTiltCamera.OnBeginCamera`: 컬링 범위를 넓히던 코드를 뺐다(Godot 이 그림의 실제 범위로 컬링한다).
- `MapTestReturn`, `DebugOverlay`: `OnGUI` 로 그리던 글자를 뺐다(화면 층이 따로 그린다, G6).
- `Mathf.PerlinNoise`: 유니티와 값이 같지 않다(연출용이라 범위·연속성만 맞춤).
- 프레임 순서: 컴포넌트는 만든 순서대로 돈다(유니티는 스크립트 실행 순서를 따로 정하지 않으면 보장이 없었다).
- 테스트의 `[UnityTest]`: `yield return null` 한 번이 정확히 1/60초 한 프레임이다. 실제 시간이 흐르지 않아 빨리 끝난다.

## 데이터 (G2)

- **형식**: 데이터 한 건이 JSON 파일 하나(`godot/data/<종류>/<이름>.json`). `$type`(클래스 전체 이름) + `name` + 필드.
  다른 데이터는 `{"$ref": "Weapon_Bow"}`, 그림은 `{"$sprite": "res://sprites/...", w, h, ppu, pivot}` / `{"$texture": ...}`, 빈 참조는 `null`.
  열거형은 숫자, 참/거짓은 `0`/`1`(유니티에서 온 그대로) 또는 `true`/`false`.
- **읽기**: `GameData.Load(IDataSource)` 가 전부 읽어 데이터 클래스로 만든다. 게임은 `GodotDataSource`(res://data), 테스트는 `DirectoryDataSource`.
  `GameData.Get<T>(이름)`/`All<T>()`, `Resources.Load<T>(이름)` 도 이 창고에서 찾는다. 문제는 `GameData.Problems` 에 쌓이고 `GameDataTests` 가 비어 있는지 본다.
- **값 고치기**: 이제 `godot/data/*.json` 을 직접 고친다(변환 도구를 다시 돌리면 덮어쓴다).

## 테스트 현황

`dotnet test`: **통과 594, 보류 25, 실패 0**. 유니티 테스트 42개 파일을 전부 옮겼고 Godot 판에서 더한 테스트(`GameDataTests`, `ServerDataExporterTests`)가 있다.
아직 컴파일되지 않아 `#if GAME_PORT` 로 미룬 블록이 46개 남았다(대부분 UI 를 직접 누르거나 에디터 전용 API 를 쓰는 테스트: Title, UiTheme, 상점, 빌드 설정).

보류 25개는 **전부 유니티 원본에서도 맞지 않는 테스트**다(`[Ignore]` 에 이유가 적혀 있다). 옮기면서 깨진 것은 없다.

### 원본 데이터·문구와 어긋난 테스트 (6) — 값이 정해져 있어 유니티에서도 맞지 않는다

| 테스트 | 내용 |
|---|---|
| `BalanceTests.Levels_AtClear_ArePacedForAOneMinuteStage`, `Pressure_FollowsTheIntendedDifficultyCurve`, `Pressure_IsPlayableForEveryHero_AtEveryWave` | `CLAUDE.md` 에 적힌 대로: 타격감 집중 기간에 적 수를 줄여 둬 밸런스 범위를 벗어나 있다. 적 수를 되돌리거나 곡선을 다시 맞출 때 되살린다 |
| `EvolutionTests.CatalogAsset_HasWellFormedEvolutions` | 장팔사모는 찌르기가 됐지만 진화형 비룡사모는 화살형 그대로(Step 10-9 결정)인데 테스트는 "진화해도 같은 계열"을 기대한다 |
| `TerrainTests.RealCatalog_HasAllTerrainsWithArt` | 바닥 타일은 Step 14-3 에서 128x128 이 됐는데 테스트는 256 을 기대한다 |
| `StrategyTests.Ui_TargetPanel_ShowsMessage_WhenNoTargets` | 화면 문구는 "공격할 수 있는 성이 없습니다" 인데 테스트는 "공격할 수 없습니다" 를 찾는다 |

### 보류 테스트 판정 (19) — 유니티 원본 코드와 비교한 결과: 원래 실패하던 테스트

처음에는 "옮기면서 달라진 것인지" 알 수 없어 확인 필요로 두었던 19개다. 유니티 에디터의 Test Runner 는 명령줄로 돌리지 못했다
(`-batchmode -runTests` 가 "Play From Title Scene" 설정 때문에 타이틀 씬에서 멈춘다. 그 설정과 Unity 원본은 건드리지 않았다). 대신 두 가지로 가렸다.

1. **유니티의 Mono 로 실행** (`tools/godot_port/mono_check.sh`): 옮긴 로직과 테스트를 .NET Framework 용으로 빌드해 유니티에 들어 있는 `MonoBleedingEdge/bin/mono.exe` 로 돌린다.
   유니티와 같은 런타임이라 실수 계산(Mono 는 float 연산을 배정밀도로 한다)과 `System.Random` 수열이 유니티와 같다. 결과: 612개 중 585 통과, 실패 27 = 보류 25개 전부 + 2개.
   → 19개는 .NET 8 과 Mono 의 차이 때문에 실패하는 것이 아니다. (`System.Random` 수열은 두 런타임이 같다는 것도 따로 확인했다 — 자동 생성 맵은 유니티와 같게 나온다.)
2. **테스트가 쓰인 커밋의 코드로 실행** (`tools/godot_port/old_snapshot.sh`): 그 테스트가 마지막으로 고쳐진 커밋의 스크립트를 지금의 장면 모형 위에 올려 같은 테스트를 돌린다.
   결과: 19개 모두 그때 코드로도 실패한다. → 나중의 코드 변경으로 깨진 것도 아니다. 처음부터 맞지 않았다.

로직 파일은 원본과 줄 단위로 같다(`diff` 0줄; `TerrainHeights.cs`, `TerrainMap.cs` 만 1~2줄 다름).

| 테스트 | 판정 | 근거 |
|---|---|---|
| `TerrainHeightTests.Map_HeightAt_UsesCustomHeights_AndRampInterpolates` | 원래 실패 (확정) | y = 5.99 는 셀의 99.5% 지점이라 높이 0.995. 기대는 1 ± 0.001 |
| `CoopBattleTests.CoopBattleModel_Expire_HidesUnseenEnemies_KeepsFloorGems` | 원래 실패 (확정) | 코드는 "마지막으로 본 때"(0초)부터 0.5초, 테스트는 "빠진 스냅샷"(1초)부터 0.5초를 기대 |
| `NetLoginTests.LoginFlow_HappyPath_LoginServer_ThenGameServer_ThenKeepAlive` | 원래 실패 (확정) | 접속 완료가 된 틱의 0.1초가 더해져 2.9초 틱에서 딱 3.0 이 된다 |
| `TiltViewTests.Preset_Up_IsPerpendicularToForward_AndRisesFromGround` | 원래 실패 (확정) | cos(90°) 가 정확히 0 이 아닌데 정확 비교한다 (Mono 1.2e-8, .NET -4.4e-8) |
| `TerrainCollisionTests.Resolve_RemovesInwardComponent_KeepsTangent`, `Resolve_HeadOn_PlayerStops_EnemyDeflects` | 원래 실패 (확정) | 경계에 딱 붙인 위치에서의 정확 비교·방향 기대가 코드와 다르다 |
| `MapEditorTests.EraseCircle_RemovesOnlyInsideAndRespectsFilter`, `FindNearest_PrefersPropsOverPatches_AndMissesFarAway` | 원래 실패 (확정) | "먼 곳"으로 고른 자리에 자동 생성 물건(얼룩 포함)이 있다 |
| `TerrainHeightTests.Auto_MountainsHaveHills_PlainsHaveFew_AndRiversStayFlat`, `Auto_StarterHill_IsNearTheStart_WithARamp_AndOffTheRiver` | 원래 실패 (확정) | 시드로 정해지는 결과가 기대와 다르다 (언덕 셀 7, 기대 8 등) |
| `BalanceTests.Evolution_IsReachableWithinOneStage_ForFocusedPlay_ButNotGuaranteed` | 원래 실패 (확정) | 실제 카탈로그로 도는 확률 시험: Mono 에서 6번 돌려 6번 모두 기준(55%) 미달 |
| `Hd2dLightingTests.Spawner_NoLights_WhenLightingDisabled` | 원래 실패 (거의 확실) | 소품 2 를 기대하지만 이웃 칸의 자동 생성 얼룩까지 세어 31 |
| `TerrainTests.Spawner_ReusedPoolObjects_ResetRotationAndScale` | 원래 실패 (거의 확실) | 풀에서 돌려 쓴 그림에 회전이 남는다 |
| `WeaponSwingTests.Slash_HeldSprite_ShowsWhileSwinging_AndHidesAfter`, `Thrust_HeldSpear_MovesForwardAlongTheThrust_ThenHides`, `Halberd_HeldSprite_LungesWhenThrusting_AndTurnsWhenSweeping`, `Halberd_AlternatesThrustAndSweep` | 원래 실패 (거의 확실) | 코드는 한 Update 안에서 "그림 갱신 → 휘두르기 시작" 순서라 무기 그림·타격이 다음 프레임에 나온다. 테스트는 한 프레임 뒤를 본다 |
| `WeaponExpansionTests.Thrust_KnocksEnemiesAway` | 원래 실패 (거의 확실) | 넉백 8 이 0.2 밑으로 줄려면 0.46초가 필요한데 찌르기가 들어간 뒤 남는 시간은 0.4초쯤 |
| `WaterRippleTests.Wader_InWater_MakesRipplesAndOneSplash_ThenStopsOnLand` | 원래 실패 (추정) | 물결이 하나씩 차례로 생겨 "만든 수 2 이상"이 안 된다. 유니티의 실제 프레임 시간에 따라 달라질 수 있어 가장 약한 판정 |

"확정"은 엔진과 무관한 계산이라 유니티에서도 같은 결과가 나온다. "거의 확실"·"추정"은 게임 오브젝트의 프레임 순서를 장면 모형이 흉내 낸 결과라, 유니티 에디터에서 한 번 돌려 보면 확정된다.
이 25개는 테스트의 기대를 코드에 맞게 고치거나(대부분) 코드를 테스트에 맞게 고칠지(Expire, 연결 유지 간격) 정해 정리해야 한다.

Mono 에서만 더 실패한 2개: `TitleTests.SceneNames_MatchTheSceneAssets`(작업 폴더에 Assets 가 없어서 — 도구의 한계), `TiltViewTests.View_StandScale_IsTheSameEverywhere_UnderOrthographicTilt`
(벡터 정확 비교가 Mono 의 배정밀도 계산에서 어긋난다 — .NET 8 에서는 통과하지만 **유니티에서는 실패할 수 있는 테스트**다).

## 내보내기 (G11)

```bash
tools/godot_port/build_windows.sh     # Builds/godot/Samkuk.exe + Builds/Samkuk-godot-win64.zip
```

- 내보내기 템플릿: 공식 GitHub 릴리스의 `Godot_v4.7.2-stable_mono_export_templates.tpz`(SHA512 확인)를 `D:/01-AgentLegion/tools/godot/templates/` 에 풀어 두었고,
  `godot/export_presets.cfg` 의 `custom_template` 이 그 경로를 가리킨다(다른 PC 에서는 경로를 고치거나 Godot 에디터로 템플릿을 설치한다).
- 데이터 JSON 은 리소스가 아니라서 `include_filter="*.json"` 으로 꾸러미에 넣는다. 게임은 `GodotDataSource` 로 꾸러미 안에서 읽는다.
- 릴리스 빌드는 `Debug.isDebugBuild` 가 거짓이라 FPS 표시와 F1~F4 치트가 꺼진다. 결과물은 `Samkuk.exe`, `Samkuk.pck`, `data_Samkuk.Game_windows_x86_64/`(.NET) 세 가지가 함께 있어야 한다.
- 내보낸 실행 파일로 전투가 도는 것까지 확인했다(데이터 132건, 60 FPS).

## 유니티와 기능 대조 (G11)

확인 방법: **테스트** = 옮긴 PlayMode 테스트 통과, **화면** = 창을 띄워 찍은 화면을 내가 봄, **서버** = csharpServer 를 로컬에서 띄워 접속.

| 기능 (유니티 Step) | 규칙 | 화면 | 비고 |
|---|---|---|---|
| 이동·카메라·무한 배경 (2) | 테스트 | 화면 | |
| 적 스폰·추적·병과·궁병 화살 (3, 8-3) | 테스트 | 화면 | 적 194마리 60 FPS (RTX 3080) |
| 무기 10종·진화·휘두르기·찌르기·극 (4~5, 8-2, 8-4, 10-9) | 테스트 | 일부 | 베기 검기·스킬·진화 카드는 봄. 무기 종류별 이펙트를 하나씩 보지는 못함 |
| 경험치·레벨업·장수별 무기 목록 (6, 8-5) | 테스트 | 화면 | |
| 웨이브·정예·보스·클리어 (7) | 테스트 | 화면 | 보스 출현 문구·체력 막대·클리어 결과 |
| 장수 5명·스킬·아군 (8-1, 10-8) | 테스트 | 화면 | |
| 저장·영구 강화·결과·일시정지·타이틀 (9) | 테스트 | 화면 | 저장 위치는 `user://save.json` |
| 효과음 (10-2) | 테스트 | — | 소리 출력은 연결만 했고 들어 보지 못함 |
| 타격감: 흔들림·입자·번쩍임·데미지 숫자 (10-1) | 테스트 | 일부 | 입자·숫자는 봄. 화면 흔들림·피격 번쩍임은 정지 화면으로는 알 수 없음 |
| UI 스킨 (10-3) | 테스트 일부 | 화면 | UiTheme 테스트 일부는 아직 미룸 |
| 초상화·걷기 애니메이션 (10-5~10-7) | 테스트 | 화면 | |
| 내정: 지도·성 화면·시작 성·출진·정복 (12-1~12-5) | 테스트 | 화면 | 출진 → 클리어 → "정복 성공" → 지도에 보유 성 반영 |
| 전투 맵·지형 이동·물결 (12-6, 12-7) | 테스트 | 화면 | |
| 맵 편집기 (13) | 테스트 | 화면 | 그림 고르기·놓기, 선택 후 끌어 옮기기, 지우개, 높이 올리기(덮개·벽 선), 저장 → 다시 열기까지 화면으로 확인. 회전·크기·복제·실행 취소·전투 테스트는 테스트만 |
| 조명·후처리·틸트 시프트·그림자 (14) | 테스트 | 화면 | 2D 조명·픽셀 퍼펙트 카메라·가짜 그림자는 옮기지 않음(결정) |
| 겹친 소품 반투명 (14-6) | 테스트 | 화면 | |
| 2.5D 틸트·언덕·절벽·흙길 (16) | 테스트 | 화면 | F10 으로 끄는 탑다운 경로는 그리지 않음(결정) |
| 로그인·협동 방·협동 전투 (15) | 테스트 | 서버 | 1인으로 로그인 → 방 → 시작 → 서버 전투. 2인 이상은 미확인 |
| 서버 데이터 내보내기 (15-2) | 테스트 | — | `dotnet run --project godot/exporter` (기본 출력 `csharpServer/GameServer/Data`, 인자로 다른 폴더). `godot/data` 에서 만든 9개 파일이 서버의 현재 파일과 글자 하나까지 같다(`export_info.json` 만 다름) |
| Windows 빌드 (11) | — | 화면 | `tools/godot_port/build_windows.sh` |
| 디버그 키 F1~F10 | — | 화면 | F7(도트 격자)·F10(틸트 끄기)은 값만 바뀌고 화면은 그대로 |

## 눈으로 확인할 것 (사용자)

테스트와 정지 화면으로는 알 수 없는 것만 모은다. 수치는 **유니티 원본에 맞추는 쪽**으로 정해 두었다(사용자 결정 2026-11-13).

1. **밝기·색**: 빛 규칙을 유니티 셰이더(`PixelSprite`)와 같게 했다 — 색 x (환경광 + 햇빛 x 그림자 + 점광원), 면 방향은 보지 않는다. 값도 원본 그대로(`LightingPreset`, `TiltPreset.SunShare 0.6 / AmbientShare 0.45`, 선형 색 공간, 톤 매핑 없음, 딱 떨어지는 그림자 2048).
   남은 차이: 점광원(횃불, 플레이어 빛)의 거리에 따른 감쇠 곡선이 엔진마다 달라 번지는 넓이가 조금 다를 수 있다.
2. **틸트 시프트**: 유니티 셰이더와 같은 식·같은 13탭·같은 값(`TiltShiftPreset`: 띠 0.18, 1080p 기준 6px). 비네트도 URP 와 같은 식이다. 블룸은 문턱 1·세기 0.7 을 Godot 글로우로 옮겼는데 번지는 모양(scatter)은 같지 않다.
3. **글자**: 같은 글꼴(맑은 고딕)·같은 크기로 그리지만 **줄 높이와 글자 폭은 유니티에서 재 보지 못해 맞추지 못했다**. 장수 카드 설명과 협동 창의 안내 문장이 유니티보다 한 줄 더 넘치는 곳이 있다. 굵은 글씨는 외곽선으로 흉내 낸다. 유니티 화면과 나란히 보고 어긋나면 `UiMirror` 의 줄 간격·크기 배율을 맞춘다.
4. **서 있는 그림**: 이제 그늘을 받는다(나무 그림자 속에서 어두워진다). 절벽 옆면은 임시 그림이다.
5. **조작감과 소리**: 이동, 무기 타이밍, 화면 흔들림, 효과음 크기와 겹침. 소리는 한 번도 들어 보지 못했다.
6. **성능**: RTX 3080 에서 적 194마리 60 FPS. 내장 그래픽에서는 재지 못했다(그림 한 장이 노드 하나라 적이 아주 많으면 `MultiMesh` 로 묶어야 할 수 있다).
7. **맵 편집기**: 왼쪽 패널의 "지우개 반경" 글자가 두 줄로 나와 조금 잘린다.
