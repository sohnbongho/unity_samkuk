# CLAUDE.md — 삼국지 서바이버 (unity_samkuk)

뱀파이어 서바이버류 로그라이크. Unity 6000.5.6f1, URP 2D, 신규 Input System 전용, PC 대상.
초기 기획은 `PLAN.md`, 이 문서는 **현재 상태 / 남은 작업 / 작업 규칙**을 관리한다.

> **문서 유지 규칙 (반드시 지킬 것)**
> 작업을 마칠 때마다(Step 또는 하위 Step 완료, 설계 변경, 새 규칙/함정 발견 시) 이 파일의
> `진행 상황`, `남은 작업`, `알려진 이슈`, `작업 규칙`을 **같은 커밋에** 함께 갱신한다.
> 사용자 확인 대기 중인 작업은 상태를 `확인 대기`로 적고, 확인 후 커밋하면서 `완료`로 바꾼다.

---

## 진행 상황

마지막 갱신: 2026-10-03 (Step 9-2 완료, 다음: Step 10)

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
| 9-2 | 타이틀 씬, 영구 강화 상점, 기록, 저장 초기화, 씬 흐름 | 완료 | Step 9-2 커밋(`git log`에서 "Step 9-2" 검색) |
| 10 | 연출·밸런스 | 대기 | - |
| 11 | PC 빌드 | 대기 | - |

## 남은 작업

1. **Step 10 연출·밸런스** (세부 계획, 진행하면서 이 목록을 갱신)
   - 피격/처치 이펙트(파티클), 화면 흔들림, 데미지 숫자 점검
   - 사운드/BGM 훅(AudioManager, 설정값 저장), 에셋 없이도 동작해야 함
   - 중국풍 UI 스킨(색/프레임), 장수/적 스프라이트 교체 지점 정리
   - 밸런싱: 1분 스테이지 기준 난이도 곡선, 무기/진화/영구 강화 수치 점검
2. **Step 11 빌드**: 빌드 설정(타이틀→게임 순서는 9-2에서 등록됨), 최적화(GC/풀링 점검), 실행 파일 출력, 필요 시 모바일 터치.
3. **미결정(PLAN.md 8번)**: 모바일 포함 여부, 실제 아트 에셋 사용 여부, 장수/무기 최종 목록.

## 알려진 이슈 / 메모

- 스프라이트(번개, 불길, 궁병, 기병 등)는 코드로 만든 **플레이스홀더**. Step 10에서 교체/다듬기.
- 장수 전용 시작 무기(쌍고검, 청룡언월도 등)는 진화 대상이 아니다. 필요하면 `EvolutionData` 추가.
- 게임 씬은 한 판 1분 스테이지 기준이라 진화 필요 레벨을 5로 낮춰 둠(`Evo_*.asset`의 Required Level).
- git의 `LF will be replaced by CRLF` 경고는 무시해도 된다.

---

## 프로젝트 구조

- 코드: `Assets/Scripts/<영역>/` — Core, Player, Enemy, Weapon, Skill, Hero, Stage, Pickup, Upgrade, Meta, UI, Data, Editor
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
- 능력치: `PlayerStats` 계층 = 패시브 × 장수 × 영구 강화(`ApplyMeta`) × 버프.
- 한 판의 끝: `GameManager.Finished` → `ResultController`가 골드 계산/저장 → 결과 화면. 일시정지는 `PauseController`(ESC).
- UI는 uGUI 레거시 `Text` + `UiFont`(한글 폰트). 뷰는 인터페이스(`ILevelUpView`, `IResultView`, `IPauseView`...)로 분리해 테스트에서 가짜 뷰를 쓴다.

### 디버그 키 (게임 씬)
F2 무기 레벨업, F3 경험치 지급, F4 스테이지 15초 건너뛰기, ESC 일시정지, R 재시작, T 타이틀(결과 화면).

---

## 작업 규칙

### 셋업 / 에셋
- 씬 배치와 프리팹/SO 생성은 에디터 스크립트로 자동화한다. 메뉴 `Samkuk > Run All Setup (Step 2-9 + 타이틀)` 한 번이면 전체 구성. 새 Step을 만들면 `SetupAll.cs`에도 추가한다.
- 셋업은 **멱등**이어야 한다: 이미 있는 에셋은 덮어쓰지 않아 사용자가 조정한 수치를 보존하고, 카탈로그는 **누적 방식**(없는 항목만 추가)으로 채운다.
- 셋업에서 `EditorSceneManager.OpenScene/NewScene` **이후에** 에셋을 로드한다 (씬 전환이 로드된 에셋 참조를 무효화할 수 있음).
- 셋업 순서 주의: Step 7을 단독 재실행하면 웨이브가 초기화되어 8-3의 궁병 편성이 빠진다 → 항상 `Run All`을 쓰거나 8-3을 이어서 실행.
- 데이터 파일(`*.asset`, `*.unity`, `*.prefab`)은 Unity가 만든 것이므로 손으로 편집하지 말고 셋업 스크립트로 바꾼다.

### 코딩
- 주변 코드의 주석 밀도/이름/관용구를 따른다. 주석은 한국어, "왜"를 설명한다.
- `Update` 안의 할당을 피하고(풀링, 재사용 리스트), 무기/적/투사체는 풀링한다.
- 월드에 고정되어야 하는 이펙트/장판/충격파 링은 플레이어의 자식으로 두지 않는다.
- 필요한 대상이 없으면 쿨다운을 소모하지 않고 대기하는 무기 규칙을 유지한다.
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
