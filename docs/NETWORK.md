# 서버 로그인 (Step 15-1)

타이틀에 들어오면 로그인 창이 먼저 뜨고, 로그인 서버 → 게임 서버 순서로 접속한 뒤 플레이 내내 연결을 유지한다. 서버는 별도 프로젝트 `D:\01-AgentLegion\csharpServer`(C#, protobuf)이고 이 문서는 **Unity 클라이언트 쪽**을 설명한다. 용어는 `CONTEXT.md` "네트워크".

## 흐름

| 단계 | 클라 → 서버 | 서버 → 클라 | 다음 |
|---|---|---|---|
| 1 | 9100 접속 | `ConnectedResponse{index}` (첫 프레임) | 인사를 받은 뒤 보내기 시작 |
| 2 | `LoginRequest{user_id, password_hash}` | `LoginResponse{success, error_code, auth_token}` | 성공이면 로그인 소켓을 닫고 3으로. 토큰 유효 60초 |
| 3 | 9101 접속 | `ConnectedResponse` | |
| 4 | `GameConnectRequest{auth_token}` | `GameConnectResponse{success, error_code}` | 성공 = 접속 완료 |
| 5 | `KeepAliveRequest{}` 3초마다 | | 서버는 10초 동안 못 받으면 끊음 |

- 비밀번호는 **평문을 보내지 않는다**: `SHA256(UTF8(평문))` 32바이트(`PasswordHash.Client`). 서버가 PBKDF2 로 다시 해시해 검증.
- 오류 코드 문구: 로그인 1 인증 실패 / 2 밴 / 3 서버 오류 / 4 횟수 제한, 게임 1 토큰 무효 / 2 서버 오류 (`LoginFlow.LoginErrorText/GameErrorText`).
- 인증 전에 허용되지 않은 메시지를 보내면 서버가 즉시 끊는다. 로그인 서버 허용: LoginRequest·KeepAliveRequest, 게임 서버 허용: GameConnectRequest·KeepAliveRequest.
- 연결/응답을 15초(`NetSettings.ResponseTimeout`) 안에 못 받으면 실패로 본다(첫 로그인은 서버 DB 첫 연결 + PBKDF2 로 수 초 걸릴 수 있음).
- 테스트 계정: `user_00001` ~ `user_10000`, 비밀번호 `Test1234!`.

## 프레임

`[2바이트 little-endian ushort = 바디 길이(헤더 제외)][protobuf MessageWrapper 바이트]`. 바디 길이 0 은 불가, 상한 8190. 한 수신에 여러 프레임이 붙거나 한 프레임이 쪼개질 수 있어 `FrameParser` 가 누적해 자른다(서버 `ReceiveParser` 와 같은 규칙). `MessageWrapper.message_size` 필드는 서버가 쓰지 않아 0 으로 둔다.

## 구조 (`Assets/Scripts/Net/`)

- `NetSettings` — 주소(127.0.0.1, 9100/9101)와 상수 한 곳.
- `FrameParser` — 순수 파서. `Push` 로 바이트를 쌓고 `TryPop` 으로 바디 조각을 꺼낸다.
- `NetFrames` — `MessageWrapper` ↔ 프레임 바이트.
- `INetLink` / `TcpLink` — 연결 하나. `TcpLink` 는 비동기 접속 + 백그라운드 수신 스레드가 프레임을 `ConcurrentQueue` 에 넣고, 메인 스레드가 `TryReceive` 로 꺼낸다(유니티 API 를 쓰지 않음). 보내기는 메인 스레드에서 바로.
- `LoginFlow` — **순수 상태 기계**(`NetPhase`: Offline → ConnectingLogin → LoggingIn → ConnectingGame → Authenticating → Online / Failed). 연결은 `INetLink` 공장으로만 만들고 시간은 `Tick(dt)` 로 받아 서버 없이 테스트한다. 받은 메시지를 먼저 처리하고 끊김을 본다(서버가 응답 직후 끊어도 응답을 놓치지 않게).
- `NetClient` — 하나뿐인 `MonoBehaviour`(DontDestroyOnLoad). 매 프레임 `Tick(unscaledDeltaTime)`(일시정지 중에도 연결 유지), `PhaseChanged` 이벤트(단계마다 콘솔에 `[Net] 단계: 문구` 로그). Awake 에서 `Application.runInBackground = true`: 창이 뒤로 가면 Update 가 멈춰 로그인 흐름이 그 자리에 서고 연결 유지도 끊기기 때문(첫 테스트에서 "로그인 중" 에 멈춘 원인 — 서버 콘솔을 보러 창을 바꾼 사이 메인 스레드 펌프가 멈춤). 에디터의 일시정지 버튼은 여전히 멈추므로 10초 넘게 누르면 서버가 끊는다. `LinkFactory` 를 바꾸면 가짜 연결(테스트). `OfflineChosen` 은 이번 실행에서 오프라인을 골랐다는 표시(저장 안 함).
- `PasswordHash` — SHA256.
- `Proto/Message.cs` — protoc 가 만든 메시지 클래스 **복사본**(아래 "프로토콜 변경").
- `Assets/Plugins/Protobuf/Google.Protobuf.dll` — 3.27.3 netstandard2.0.
- `UI/LoginPanel` — 타이틀 로그인 창. `TitleController.Start` 가 `LoginPanel.Attach` 를 불러 코드로 만든다(씬 재구성 불필요). 캔버스 맨 위의 어두운 덮개가 뒤의 버튼을 막는다. Enter = 로그인. 성공하면 사라지고 아이디를 `SaveData.lastUserId` 에 저장(비밀번호는 저장 안 함). [오프라인으로 계속] 은 `NetClient.OfflineChosen`. 이미 접속돼 있거나 오프라인을 골랐으면 뜨지 않는다(`LoginPanel.ShouldShow`). 끊기면 다음에 타이틀로 올 때 다시 뜬다.

## 프로토콜 변경

스키마 원본은 **`csharpServer/Scripts/message.proto` 한 곳**이다. 필드/메시지가 필요하면 서버 쪽에 요청하고, 서버 쪽이 다시 생성한 `SocketLibrary/DTO/Message.cs` 를 `Assets/Scripts/Net/Proto/Message.cs` 에 **그대로 복사**한다(머리의 두 줄 주석만 유지). 이 파일은 직접 고치지 않는다.

## 확인 방법

1. 서버 실행: csharpServer 프로젝트에서 로그인 서버(9100)·게임 서버(9101)를 띄운다(DB 시드 포함). 주소가 다르면 `NetSettings`.
2. Play(타이틀): 로그인 창이 뜨고 아이디 `user_00001` / 비밀번호 `Test1234!` 로 [로그인] → "접속 완료" 뒤 창이 사라진다. 서버 콘솔에서 로그인/게임 접속/3초마다 KeepAlive 를 확인. 틀린 비밀번호 → "아이디 또는 비밀번호가 틀렸습니다", 서버 꺼짐 → "연결 실패" 또는 15초 뒤 "로그인 서버에 연결할 수 없습니다". [오프라인으로 계속] → 창 없이 플레이, 전투 뒤 타이틀로 돌아와도 다시 묻지 않음.
3. 유니티 콘솔: `Google.Protobuf.dll` 임포트 뒤 "System.Runtime.CompilerServices.Unsafe 를 찾을 수 없음" 류의 오류가 나면 NuGet 캐시(`%USERPROFILE%\.nuget\packages\system.runtime.compilerservices.unsafe\6.0.0\lib\netstandard2.0\`)의 DLL 을 `Assets/Plugins/Protobuf/` 에 추가한다(Unity 의 .NET Standard 2.1 프로필이 System.Memory/System.Buffers 는 이미 제공하므로 그 둘은 넣지 않는다).
4. Test Runner: `NetLoginTests`(프레임, 해시, 흐름, NetClient, 로그인 창).

## 서버 데이터 내보내기 (Step 15-2)

협동 모드(2~4인)는 **서버가 전투 전체를 시뮬레이션**하고 유니티는 렌더러+입력 장치가 된다(설계: `csharpServer/Docs/coop-design.md`).
그래서 서버도 같은 게임 데이터가 필요한데, **원본은 유니티 에셋**이고 서버는 유니티가 내보낸 JSON 을 읽는다.

- 메뉴 **`Samkuk > Export Server Data`**(`ServerDataExporter`) → `D:\01-AgentLegion\csharpServer\GameServer\Data\` 에
  `enemies.json`, `weapons.json`, `passives.json`, `evolutions.json`, `skills.json`, `heroes.json`, `stages.json`, `upgrade_catalog.json`, `balance.json`, `export_info.json`.
- 규칙: id 는 **에셋 파일명**(`Enemy_Soldier`, `Weapon_Bow`...), 참조도 파일명, 열거형은 이름 문자열(`"Archer"`, `"Slash"`), 키는 camelCase, 들여쓰기 2칸, 실수는 소수 5자리.
  색/스프라이트/시트 같은 표시용 값은 빼고, 참조가 빈 진화는 건너뛴다.
- `balance.json` 은 전투 씬(`GameSystems` 의 EnemyManager/EnemySpawner/ExpGemManager/LevelUpController, Player 의 PlayerHealth/PlayerController)에
  **직렬화된 현재 값**을 `SerializedObject` 로 읽는다. 전투 씬이 열려 있지 않으면 추가 모드로 잠깐 열었다가 저장 없이 닫는다(작업 중 씬 무관).
  코드 상수(`PlayerController.BodyRadius`, `PlayerExperience.BaseRequired/RequiredStep`, `UpgradeGenerator.HealRatio`, `Enemy.KnockbackDecay/MaxKnockbackSpeed/ShotWarningSeconds`, `PlayerHealth.RegenTickSeconds`)도 함께 적는다.
  스폰 링 반지름은 오프라인이 실제 창 비율로 계산하지만 서버는 고정값이 필요하므로 **도트 화면 기본 반높이 5.625 × 16:9** 로 계산한다(`cameraHalfHeight/cameraAspect/spawnMargin` 도 같이 적음).
  적의 실제 충돌 반지름은 `colliderRadius × scale` 이라 `radius` 로 한 번 더 적는다(`Enemy.Radius` 와 같은 식).
- 값을 바꿀 땐 에셋/씬을 고치고 **다시 내보낸다**(JSON 은 손으로 고치지 않는다). 서버 데이터 로더는 csharpServer 쪽.

## 협동 방 (Step 15-3)

협동 모드는 로그인 뒤 **같은 게임 서버 연결(9101)** 로 방에 모인다. 프로토는 확정본(`message.proto` 60~72번: Room*, BattleStartNoti).
**방은 맵 위치(성) 단위의 월드**다: 같은 성으로 들어온 사람끼리 한 방(최대 4명)에 모이고, 그 성의 방이 없으면 서버가 만들며 첫 입장자가 방장이다(사용자 결정: 초대 코드 입력이 불편).
초대 코드 방식(`RoomCreate/RoomJoin`)은 서버와 `CoopFlow` 에 남아 있지만 UI 는 쓰지 않는다.

- 타이틀 오른쪽 아래 **[협동]** 버튼(`CoopButton`, 씬 로드 때 코드로 붙음, 접속돼 있을 때만 활성) → 방 창 `RoomPanel`(코드 생성, 캔버스 맨 위 덮개).
- 흐름: 장수 고르기 → 성 고르기([<] [>], 처음은 내정의 내 성) → [입장](`RoomEnterRequest{world_id = 성 에셋 이름, hero_id}`) → 대기방(`RoomStateNoti` 로 구성원 4칸 갱신)
  → 구성원은 [준비](`RoomReadyRequest`), 방장은 전원 준비되면 [시작](`RoomStartRequest`) → `BattleStartNoti` → 전투 씬(15-4) → `BattleEndNoti` 뒤 같은 방의 대기 상태로.
- 전투 중인 성에 들어오면 거절되지 않고 **대기 멤버**로 들어간다(`RoomInfo.in_battle`, 그 멤버의 `in_battle=false`): 스냅샷은 받지 않고 `BattleEndNoti` 와 그 뒤의 `RoomStateNoti` 를 받아 다음 판부터 참여. 창에는 "전투 진행 중, 다음 판을 기다리는 중".
- 방장은 준비 없이 시작할 수 있다. 서버가 방을 만들 때 스테이지 기본값(stages.json 첫 항목)을 넣어 주므로 클라는 비어 있을 때만 `CoopSettings.DefaultStageId` 를 보내고, 전투 배경 성(`castle_id`)은 들어온 월드로 맞춰 둔다(`RoomSetStageRequest{stage_id, castle_id}`, 방장만, 이미 맞으면 안 보냄). 장수를 바꾸면 `RoomSetHeroRequest`.
- 성 목록은 `Resources/CastleCatalog.asset`(`CastleCatalog.Load()/FindAsset()`): 타이틀에 참조가 없어 Resources 로 옮겼다(GUID 그대로라 내정/맵 편집기 씬의 참조는 유지). 전투 씬은 `BattleStartNoti.castle_id` 로 `GameSession.SortieCastle` 을 정해 오프라인과 같은 성 지형 배경을 쓴다(서버 1차는 개활지라 소품은 장식).
- 구조(`Assets/Scripts/Coop/`): `RoomModel`(서버 RoomInfo 를 담는 순수 상태: 내 자리/방장/시작 가능 판정, 오류 코드 문구) → `CoopFlow`(순수 상태 기계 `CoopPhase` None/Creating/Joining/InRoom/Starting/Battle, 요청·응답·타임아웃)
  → `CoopSession`(정적, NetClient 의 `MessageReceived`/`Ticked`/`PhaseChanged` 에 붙어 Flow 하나를 씬 너머로 유지, 접속이 끊기면 방 상태를 지움) → `RoomPanel`(그리기만).
- `LoginFlow` 는 접속 완료 뒤 자기 일이 아닌 메시지를 `Unhandled` 로 넘기고 `Send` 는 접속 완료 상태에서만 보낸다. `NetClient` 가 이를 `MessageReceived`/`Send`/`Ticked` 로 공개한다.
- 장수 목록은 `Resources/HeroCatalog.asset`(`HeroCatalog.Load()`): 타이틀 씬에 참조가 없어 Resources 로 옮겼다(GUID 그대로라 전투 씬의 참조는 유지).
- 서버 규칙(csharpServer): 방/전투 응답은 룸 스레드가 보내 최대 50 ms 뒤에 온다. 대기방에서는 KeepAlive 가 여전히 필요하고(LoginFlow 가 계속 보냄) 전투 중에는 어떤 메시지든 활동으로 친다. 접속이 끊기면 서버가 그 사람을 방에서 뺀다.
- 테스트 `CoopRoomTests`(가짜 전송/가짜 연결). 실제 서버로는 혼자서도 확인할 수 있다: 성 고르고 [입장] → 방장 → [시작](혼자면 바로 시작 가능) → 전투 씬.

## 협동 전투 (Step 15-4) — 꼭두각시 모드

방에서 `BattleStartNoti` 가 오면 `RoomPanel` 이 전투 씬(`BattleScene`)을 열고, 씬 로드 직후 `CoopBattleDriver` 가 스스로 붙는다(`SceneManager.sceneLoaded`, `CoopSession.InBattle` 일 때만).
드라이버는 **오프라인 규칙 컴포넌트를 끄고**(HeroSelectController, PlayerController/PlayerAnimator, WeaponController, SkillController, EnemyManager/EnemySpawner/EnemyProjectileSystem,
ExpGemManager, LevelUpController, StageController, GameManager, PauseController, AllyManager) 서버가 보내는 상태로 같은 프리팹·HUD·연출을 움직인다.

- 입력: WASD/방향키/패드 스틱 + 스페이스(스킬, 1차에선 서버가 무시) → `BattleInputRequest{seq, move, skill}` 를 서버 틱(20 Hz)마다.
- 스냅샷(10 Hz): `CoopBattleModel`(순수)이 플레이어/적/젬의 **보간 트랙**(`MotionTrack`: 지금 자리에서 새 위치까지 스냅샷 간격 동안 직선 이동, 6유닛 넘게 튀면 즉시)과
  `StageState` 를 담는다. 분할 조각(players 비어 있음)은 상태를 덮어쓰지 않고 이벤트만. 스냅샷에서 0.5초 빠진 적/끌려가던 젬은 숨긴다(관심 영역 밖).
- 적: `Enemy` 프리팹 꼭두각시 풀(`EnemyPuppetPool`). `Enemy.Init` 으로 그림만 쓰고 `Teleport`/`TickAnimation`(가장 가까운 산 플레이어 쪽을 봄)/`ShowPuppetFlags`(기절·돌진 예고·사격 예고 색)/
  `ShowHit`(Damaged 이벤트 → 데미지 숫자·불꽃·소리는 기존 구독자)/`ShowDeath`(Died 이벤트 → 입자·흔들림·소리). 종류는 `EnemySpawn.enemy_id` 로, 못 받았으면 스테이지의 가장 약한 적으로 임시.
- 플레이어: 내 자리는 씬의 Player 오브젝트(위치를 서버 값으로, 걷기는 `PuppetWalker` 가 서버 facing/이동 플래그로; 내 입력이 있으면 그 방향을 먼저 보여 체감 지연을 줄임),
  다른 자리는 Player 를 복제해 동작 컴포넌트를 끈 것. 쓰러지면 반투명.
- 무기: `WeaponFire.motion` 으로 `SwingPuppet` 이 SlashWeapon 과 같은 휘두르기 그림(들고 있는 무기 + 반원 검기, 찌르기는 뾰족 검기), `LoadoutChanged` 로 `OrbitPuppet`(회전 도끼 칼날,
  각도 = 서버와 같은 식 `경과초 × 회전속도 + i × 360/n`, 필드 없음), 투사체는 `ProjectileSpawn` 의 방향·속도로 외삽(`ProjectilePuppetPool`), `AreaFx` 는 코드로 만든 원판(`CoopSprites.Disc`)을 `WeaponFx.PlayDisc`.
- HUD: `HudController.SetHealth/SetExp`, `StageHudView.SetTimerSeconds/SetWave/ShowBanner/ShowBoss/HideBoss` (서버 값). 카운트다운 3·2·1, 다른 사람 레벨업 선택 중 "OO 선택 중...".
- 레벨업: `LevelUpChoiceNoti` → 기존 `LevelUpUI`(`ILevelUpView`)에 `UpgradeOption` 으로 보여 주고 고르면 `UpgradeSelectRequest{choice_id, index}`. 시간이 지나면(서버 자동 선택) 창을 닫는다. 로컬 timeScale 은 건드리지 않는다(서버가 방 전체를 멈춤).
- 끝: `BattleEndNoti` → `CoopResultPanel`(참가자별 처치/레벨/골드, 서버 계산값; 내 골드는 저장에 더함) → [방으로 돌아가기] → 타이틀로 가면 `TitleController.Start` 가 같은 방의 `RoomPanel` 을 다시 연다. 접속이 끊기면 같은 창에 "연결이 끊겼습니다".
- 1차에서 없는 것: 지형 충돌(서버가 개활지라 나무를 통과해 보임), 장수 스킬, 아군, 일시정지(ESC 없음), 중간에 나가기, 재접속.

## 다음 단계 후보

- 협동 2차: 중간 나가기(ESC → 방으로), 장수 스킬·아군·지형 충돌(서버), 재접속, 스테이지/성 선택 UI, 서버 저장(메타 강화 배율).
- 로그인 응답에 닉네임, 게임 접속 응답에 저장 데이터(서버 저장) — proto 추가는 서버 쪽에 요청.
- 서버 주소를 설정 파일/타이틀 UI 로.
