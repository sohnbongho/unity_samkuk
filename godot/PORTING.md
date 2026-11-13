# Godot 전환 작업 문서

Unity 프로젝트(저장소 루트의 `Assets/`)를 Godot 4.7.2 .NET 으로 옮기는 작업의 현재 상태와 방법. 결정과 이유는 `docs/adr/0004-godot-conversion.md`.
진행표는 루트 `CLAUDE.md` 의 "Godot 전환" 표가 원본이고, 이 문서는 **어떻게 옮기는지**와 **확인이 필요한 것**을 적는다.

## 폴더

| 경로 | 프로젝트 | 내용 |
|---|---|---|
| `godot/core/` | `Samkuk.Core` (net8.0) | 엔진 참조 없는 순수 로직. 폴더는 유니티 `Assets/Scripts/<영역>/` 과 같게 둔다. `Compat/` 가 호환 층 |
| `godot/game/`, `godot/scenes/` | `Samkuk.Game` (Godot.NET.Sdk) | 노드, 화면, 입력, 소리. `Boot.cs` 가 순수 로직에 로그·저장 폴더를 꽂는다 |
| `godot/tests/` | `Samkuk.Tests` (NUnit) | 엔진 없이 도는 테스트 |
| `tools/godot_port/` | | 옮길 때 쓰는 도구 |

`core/` 와 `tests/` 에는 `.gdignore` 가 있어 Godot 이 훑지 않는다(자기 프로젝트에서 컴파일).

## 실행

```bash
cd godot
dotnet test tests/Samkuk.Tests.csproj                 # 순수 로직 테스트
dotnet build Samkuk.Game.csproj                       # 게임 C# 빌드
GODOT=/d/01-AgentLegion/tools/godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe
"$GODOT" --headless --path . --import                 # 리소스 임포트
"$GODOT" --headless --path . -- --smoke               # 실행해 연결만 확인하고 종료
```

에디터로 열려면 `Godot_v4.7.2-stable_mono_win64.exe` 를 실행해 `godot/project.godot` 을 가져온다.

## 옮기는 방법

- **순수 로직**: `tools/godot_port/copy_core.sh <Assets/Scripts 기준 경로>...` 가 파일을 `godot/core` 로 복사하며 `using UnityEngine;` 을
  `using Samkuk.Compat;` 로 바꾼다(이미 있는 파일은 건드리지 않는다). 그 뒤 `dotnet build` 오류를 고친다. 엔진에 닿는 멤버(메시 넣기, 머티리얼 값 넣기,
  카메라 회전)는 지우고 화면 층에서 다시 만든다.
- **호환 층에 없는 것이 필요하면** `Compat/` 에 유니티와 같은 이름·같은 결과로 더한다. 그림은 그리지 않는다:
  `Sprite`/`Texture2D` 는 파일 경로 또는 픽셀 배열만 들고 화면 층이 Godot 텍스처로 바꾼다.
- **테스트**: `copy_tests.sh <이름>...` 로 복사한 뒤 `python tools/godot_port/defer_tests.py` 를 돌리면 컴파일되지 않는 테스트 메서드를
  `#if GAME_PORT ... #endif` 로 감싼다(준비/정리 메서드는 오류 줄만 `// (미룸)` 으로). 해당 기능을 옮기면 감싼 것을 풀어 되살린다.
  남은 양은 `grep -c GAME_PORT godot/tests/*.cs`. **주의**: `copy_tests.sh` 는 덮어쓰므로 이미 손본 테스트 파일에는 쓰지 않는다.
- **유니티에서 달라진 것**
  - `TerrainHeight.ZAt`: 틸트가 꺼졌는지 보지 않는다(틸트 한 가지뿐).
  - `TiltPreset`: 회전(`Quaternion`) 멤버가 없다. `CameraPosition` + `Forward`/`Up` 으로 화면 층이 만든다.
  - `PostFxPreset`, `TiltShiftPreset`: 수치만. 넣는 코드는 화면 층.
  - `SfxSynth.Generate` 는 샘플(float[])만 돌려준다. 재생용 스트림은 화면 층.
  - `ExperienceCurve`: `PlayerExperience` 안에 있던 경험치 곡선 상수를 따로 뺐다.
  - `DisplaySettings.ApplyAction`: 기본은 아무것도 안 한다. 게임이 창 설정을 꽂는다.
  - `Mathf.PerlinNoise`: 유니티와 값이 같지 않다(연출용이라 범위·연속성만 맞춤).
  - `UpgradeGenerator` 는 아직 안 옮겼다(`WeaponController`/`PlayerStats` 에 기댄다, G3).

## 테스트 현황 (G1)

`dotnet test`: **통과 183, 보류 24, 실패 0**. 유니티 테스트 42개 파일 중 30개를 옮겼고 그 안의 테스트 표시 478개 가운데 271개는 미뤄 두었다
(게임 오브젝트·컨트롤러·씬·에셋에 기댄다). 아직 안 옮긴 12개 파일(전부 게임 오브젝트 테스트): Ally, Build, Combat, EnemyRole, Enemy, Feedback, Hero,
PlayerMovement, Title, UiTheme, WaterRipple, WeaponExpansion.

보류 24개는 두 종류다(`[Ignore]` 에 이유가 적혀 있다).

### G2 뒤에 되살릴 것 (14)

데이터 에셋이 있어야 도는 테스트: `BalanceTests` 13개, `TerrainTests.RealCatalog_HasAllTerrainsWithArt`.

### 확인 필요 (10) — 유니티 Test Runner 에서 통과하던 테스트인가

옮긴 코드에서 실패한다. 로직은 원본과 같으므로 **유니티에서도 실패하고 있었을 가능성이 높지만 확인하지 못했다**
(이 환경에서 유니티를 실행하지 못하고, Test Runner 결과 파일도 남아 있지 않다). 사용자가 유니티 Test Runner 에서 아래 10개의 결과를 알려 주면
"원래 실패하던 테스트(테스트를 고친다)" 인지 "옮기면서 달라진 것(호환 층을 고친다)" 인지 가른다.

| 테스트 | 증상 | 추정 |
|---|---|---|
| `TerrainHeightTests.Map_HeightAt_UsesCustomHeights_AndRampInterpolates` | 기대 1 ± 0.001, 실제 0.995 | y = 5.99 는 셀의 99.5% 지점이라 계산상 0.995 가 맞다. 테스트 허용 오차 문제로 보임 |
| `CoopBattleTests.CoopBattleModel_Expire_HidesUnseenEnemies_KeepsFloorGems` | 1.3초에 적 2 가 이미 숨겨짐 | 코드는 "마지막으로 본 때"(0초)부터 0.5초, 테스트는 "빠진 스냅샷"(1초)부터 0.5초를 기대 |
| `NetLoginTests.LoginFlow_HappyPath_LoginServer_ThenGameServer_ThenKeepAlive` | 2.9초 틱 뒤 연결 유지가 이미 1번 | 접속 완료가 된 틱의 0.1초가 더해져 딱 3.0 이 됨 |
| `TiltViewTests.Preset_Up_IsPerpendicularToForward_AndRisesFromGround` | (0, -0.00000004, 1) ≠ (0, 0, 1) | cos(90°) 가 실수 계산에서 정확히 0 이 아님. 정확 비교 문제 |
| `TerrainCollisionTests.Resolve_RemovesInwardComponent_KeepsTangent` | (-3, 2) 와 아주 조금 다름 | 정확 비교 문제 (경계에 딱 붙은 위치) |
| `TerrainCollisionTests.Resolve_HeadOn_PlayerStops_EnemyDeflects` | 접선 방향이 반대 | 미확인 |
| `MapEditorTests.EraseCircle_RemovesOnlyInsideAndRespectsFilter` | 먼 곳(100,100)에서 5개가 지워짐 | 그 자리에 자동 생성 소품이 있다. 유니티에서 비어 있었다면 **자동 생성 결과가 런타임마다 다르다는 뜻** |
| `MapEditorTests.FindNearest_PrefersPropsOverPatches_AndMissesFarAway` | (900,900)에서 뭔가 잡힘 | 위와 같음 |
| `TerrainHeightTests.Auto_MountainsHaveHills_PlainsHaveFew_AndRiversStayFlat` | 조건 하나가 거짓 | 위와 같음 (시드에 따른 결과) |
| `TerrainHeightTests.Auto_StarterHill_IsNearTheStart_WithARamp_AndOffTheRiver` | 높은 셀 8 기대, 7 | 위와 같음 |

아래 넷이 유니티에서 통과했다면 Godot 판의 자동 생성 맵이 유니티와 다르게 나온다는 뜻이다(시드 → `System.Random` 수열 또는 실수 반올림 차이).
맵 편집기로 고친 칸은 저장 파일 그대로지만 자동 생성 칸의 소품·언덕 위치는 달라진다. 맞추려면 난수를 직접 구현한 것으로 바꾼다.

## 다음 단계에서 정할 것

- **G2 데이터 형식**: 처음 계획은 Godot 커스텀 Resource(`.tres`)였다. 순수 로직이 엔진을 모르게 되면서 `.tres` 는 데이터 클래스를 Godot 쪽에 한 벌 더
  만들어 옮겨 담아야 한다. **JSON(`godot/data/*.json`) → 순수 로직 데이터 클래스** 가 더 단순하고 테스트에서도 그대로 읽힌다(서버 내보내기 JSON 과도 가깝다).
  대신 Godot 인스펙터에서 값을 고칠 수 없고 텍스트로 고친다. 추천은 JSON.
- **G3 규칙 컴포넌트**: `PlayerStats`, `WeaponController`, `EnemyManager` 같은 규칙이 든 MonoBehaviour 를 순수 로직 클래스 + 얇은 노드로 나눌지,
  노드로 그대로 옮길지. 나누면 미룬 테스트 대부분이 `dotnet test` 로 돌아온다.
