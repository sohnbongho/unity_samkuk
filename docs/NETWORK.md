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

## 다음 단계 후보

- 협동 모드(Step 15-3~): 방 UI(만들기/코드 참가/준비/장수 선택/시작) → 전투 씬을 스냅샷 렌더러로. proto 는 `coop-design.md` 3절 기준으로 확정 중.
- 로그인 응답에 닉네임, 게임 접속 응답에 저장 데이터(서버 저장) — proto 추가는 서버 쪽에 요청.
- 서버 주소를 설정 파일/타이틀 UI 로.
