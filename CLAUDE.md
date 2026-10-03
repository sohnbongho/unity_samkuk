# CLAUDE.md — 삼국지 서바이버 (unity_samkuk)

뱀파이어 서바이버류 로그라이크. Unity 6000.5.6f1, URP 2D, 신규 Input System 전용, PC 대상.
초기 기획은 `PLAN.md`, 이 문서는 **현재 상태 / 남은 작업 / 작업 규칙**을 관리한다.

> **문서 유지 규칙 (반드시 지킬 것)**
> 작업을 마칠 때마다(Step 또는 하위 Step 완료, 설계 변경, 새 규칙/함정 발견 시) 이 파일의
> `진행 상황`, `남은 작업`, `알려진 이슈`, `작업 규칙`을 **같은 커밋에** 함께 갱신한다.
> 사용자 확인 대기 중인 작업은 상태를 `확인 대기`로 적고, 확인 후 커밋하면서 `완료`로 바꾼다.

---

## 진행 상황

마지막 갱신: 2026-10-03 (Step 10-4 완료, 다음: Step 11 빌드)

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
| 11 | PC 빌드 | 대기 | - |

## 남은 작업

1. **Step 10 나머지** (진행하면서 이 목록을 갱신)
   - 장수/적 스프라이트 교체 지점 정리 (10-3 UI 스킨은 완료)
   - 10-4 밸런싱: 1분 스테이지 기준 난이도 곡선, 무기/진화/영구 강화 수치 점검
   - (선택) BGM: 현재는 효과음만 있음. `AudioManager`에 BGM 슬롯/볼륨을 추가하는 작업이 남아 있음
2. **10-4 이후 체감 조정**: 직접 플레이해 보고 "너무 쉬움/어려움" 구간을 알려주면 `BalanceModel` 보고서(메뉴 `Samkuk > Balance Report`)를 보며 수치를 조정한다.
3. **Step 11 빌드**: 빌드 설정(타이틀→게임 순서는 9-2에서 등록됨), 최적화(GC/풀링 점검), 실행 파일 출력, 필요 시 모바일 터치.
4. **미결정(PLAN.md 8번)**: 모바일 포함 여부, 실제 아트 에셋 사용 여부, 장수/무기 최종 목록.

## 알려진 이슈 / 메모

- 스프라이트(번개, 불길, 궁병, 기병 등)는 코드로 만든 **플레이스홀더**. Step 10에서 교체/다듬기.
- 장수 전용 시작 무기(쌍고검, 청룡언월도 등)는 진화 대상이 아니다. 필요하면 `EvolutionData` 추가.
- 게임 씬은 한 판 1분 스테이지 기준이라 진화 필요 레벨을 5로 낮춰 둠(`Evo_*.asset`의 Required Level).
- **밸런스 수치는 어림 모델 기준**: `BalanceModel`은 처치율/무기별 동시 타격 수/평균 빌드를 가정한 근사라서 "방향"과 "의도한 범위 이탈 여부"만 알려 준다. 최종 체감은 직접 플레이로 확인(특히 마지막 웨이브 난이도, 진화 도달 시점).
- 밸런스 기준: 60초에 약 11레벨(경험치 곡선 `PlayerExperience.BaseRequired/RequiredStep` = 10/8), 웨이브 난이도(압박비) 약 1.5 → 1.4 → 0.9 → 0.7, 진화는 무기 Lv.3 + 짝 패시브(집중 육성 약 70%, 무작위 약 15%), 레벨업 선택지 가중치 = 보유 무기 강화 4 : 패시브 2 : 새 무기 1(`UpgradeCatalog`).
- UI 스킨 스프라이트(`Assets/Sprites/UI/*.png`)는 코드로 만든 **임시 아트**. 직접 만든 PNG로 교체해도 셋업이 덮어쓰지 않는다(`Resources/UiTheme.asset` 슬롯만 채움). 모양을 다시 생성하려면 해당 PNG를 지우고 `Step 10-3` 실행.
- 폰트는 아직 OS 한글 폰트(맑은 고딕). 붓글씨체 등을 쓰려면 ttf를 `UiTheme.font` 슬롯에 지정(코드 수정 불필요).
- 타격감 수치(흔들림 세기, 입자 개수, 번쩍임 강도)는 `FeedbackHooks`에 상수로 있음. 느낌이 과하거나 부족하면 거기서 조정. 입자는 코드로 만든 4x4 흰 사각형 스프라이트(에셋 없음).
- 효과음은 코드로 합성한 **임시 소리**(`SfxSynth`). 실제 음원은 `AudioManager`의 `overrides` 슬롯에 클립을 넣어 교체(코드 수정 불필요).
- 효과음 볼륨은 `SaveData.sfxVolume`(기본 0.7)에 저장, 타이틀 왼쪽 위 버튼으로 끔/작게/보통/크게 순환. 게임 중 볼륨 조절 UI는 아직 없음.
- git의 `LF will be replaced by CRLF` 경고는 무시해도 된다.

---

## 프로젝트 구조

- 코드: `Assets/Scripts/<영역>/` — Core, Player, Enemy, Weapon, Skill, Hero, Stage, Pickup, Upgrade, Meta, Audio, Feedback, Balance, UI, Data, Editor
  - 네임스페이스는 `Samkuk.*` (예: `Samkuk.Weapons`, `Samkuk.Enemies`, `Samkuk.Meta`, `Samkuk.UI`)
  - 런타임은 `Samkuk.Runtime.asmdef`, 에디터 스크립트는 `Editor/` 폴더
- 데이터: `Assets/ScriptableObjects/<종류>/` — Weapons, Enemies, Passives, Heroes, Skills, Evolutions, Meta, Stage, 그리고 `UpgradeCatalog`, `HeroCatalog`, `MetaCatalog`
- 씬: `Assets/Scenes/TitleScene.unity`(빌드 0번) → `SampleScene.unity`(게임, 1번)
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
- 씬 배치와 프리팹/SO 생성은 에디터 스크립트로 자동화한다. 메뉴 `Samkuk > Run All Setup (Step 2-9 + 타이틀)`(10 효과음 + 타격감 + 10-3 테마 + 10-4 밸런스 포함) 한 번이면 전체 구성. 새 Step을 만들면 `SetupAll.cs`에도 추가한다.
- 셋업은 **멱등**이어야 한다: 이미 있는 에셋은 덮어쓰지 않아 사용자가 조정한 수치를 보존하고, 카탈로그는 **누적 방식**(없는 항목만 추가)으로 채운다.
- 셋업에서 `EditorSceneManager.OpenScene/NewScene` **이후에** 에셋을 로드한다 (씬 전환이 로드된 에셋 참조를 무효화할 수 있음).
- 진화 셋업(`Step8EvolutionSetup`)은 장수 시작 무기가 만들어진 **뒤**(Step 8 이후)에 실행해야 한다 (Run All 순서 참고).
- 셋업 순서 주의: Step 7을 단독 재실행하면 웨이브가 초기화되어 8-3의 궁병 편성이 빠진다 → 항상 `Run All`을 쓰거나 8-3을 이어서 실행.
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
