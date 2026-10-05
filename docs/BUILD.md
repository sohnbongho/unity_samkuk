# Windows 빌드 (친구들과 나눠 보기)

친구에게 보내서 확인받을 수 있는 **Windows 64비트 테스트 빌드**를 한 번에 만드는 방법입니다. 결과는 폴더와 **zip 한 개**이고, 친구는 압축을 풀고 `SamkukSurvivor.exe` 만 실행하면 됩니다(설치, 별도 런타임 필요 없음).

## 만드는 법

### 방법 A: 에디터 메뉴 (보통은 이쪽)

1. 코드/씬을 바꿨다면 먼저 메뉴 **Samkuk > Run All Setup** (씬이 최신이어야 합니다. 타이틀/전투/내정 씬이 모두 빌드에 들어갑니다).
2. 메뉴 **Samkuk > Build > Windows - 친구 공유용 (릴리스)** 를 실행합니다. 처음에는 몇 분 걸립니다(셰이더/코드 컴파일).
3. 끝나면 탐색기가 열리고 `Builds/` 아래에 두 가지가 생깁니다.
   - `Builds/SamkukSurvivor_v1.0/` : 실행 폴더 (내 컴퓨터에서 바로 실행해 확인)
   - `Builds/SamkukSurvivor_v1.0_<날짜_시각>.zip` : **친구에게 보낼 파일**

### 방법 B: 명령줄 (에디터를 닫은 상태)

```
powershell -File tools\build_windows.ps1          # 친구 공유용
powershell -File tools\build_windows.ps1 -Dev     # 개발용
```

- 유니티 에디터가 이 프로젝트를 **열고 있으면 실행되지 않습니다**(안내 메시지가 나옵니다). 에디터를 닫거나 방법 A를 쓰세요.
- 유니티는 `ProjectSettings/ProjectVersion.txt` 의 버전을 Unity Hub 기본 위치에서 찾습니다. 다른 곳에 설치했다면 `-Unity "…\Unity.exe"` 또는 환경 변수 `UNITY_PATH` 로 알려 주세요.
- 로그는 `Builds/build.log` 입니다. 실패하면 오류 줄을 화면에 보여 줍니다.

## 두 가지 빌드

| | 친구 공유용 (릴리스) | 개발용 |
|---|---|---|
| 메뉴 | Windows - 친구 공유용 | Windows - 개발용 (디버그 키 포함) |
| 디버그 오버레이 (FPS, 적 수) | 꺼짐 | 켜짐 |
| F1~F4 치트 (적 +100, 무기 레벨업, 경험치, 시간) | **꺼짐** | 켜짐 |
| 타이틀 버전 표기 | `v1.0` | `v1.0 (dev)` |
| 용도 | 친구에게 나눠 주는 빌드 | 내가 빌드로 직접 확인할 때, 진화/후반 구간을 빠르게 보고 싶은 친구 |

폴더/zip 이름 끝에 `_Dev`/`_dev` 가 붙어서 구분됩니다.

## 친구에게 알려 줄 것

zip 안에 **`README.txt`** 가 들어 있어 친구는 그것만 읽으면 됩니다(실행 방법, 조작법, 모드 설명, 저장 위치, 피드백 방법). 따로 설명할 것은 하나입니다.

- **"Windows의 PC 보호" 창**: 서명하지 않은 개인 빌드라 처음 실행할 때 뜹니다. **[추가 정보] → [실행]**.

피드백을 받을 때는 "어느 버전(타이틀 오른쪽 아래 `v…`)", "어떤 화면/장수/성에서 무엇을 했는지", 그리고 로그 파일(`%USERPROFILE%\AppData\LocalLow\DefaultCompany\Samkuk\Player.log`)을 같이 받으면 원인을 찾기 쉽습니다.

## 버전 올리기

**Edit > Project Settings > Player > Version** 을 바꾸면 폴더/zip 이름, 타이틀 표기, README 에 모두 반영됩니다. 친구에게 새 빌드를 보낼 때마다 올리면 "어느 빌드를 말하는지" 헷갈리지 않습니다.

## 저장 데이터

저장은 `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Samkuk\save.json` 입니다(에디터에서 플레이할 때와 **같은 파일**이라, 내 컴퓨터에서 빌드를 실행하면 에디터 진행 상황이 그대로 보입니다). 친구 컴퓨터는 별개이므로 서로 섞이지 않습니다.
이 경로는 Player 설정의 **Company Name / Product Name**(`DefaultCompany`/`Samkuk`)에서 나오므로, 이름을 바꾸면 **저장 위치도 바뀌어** 기존 저장이 안 보이게 됩니다. 바꿀 때는 파일을 옮기세요.

## 알아 둘 점

- **한글 글꼴**: UI 글꼴은 Windows 의 "맑은 고딕"을 씁니다. 일반적인 Windows 10/11 에는 모두 들어 있어 문제없지만, 글자가 네모로 보인다는 말이 나오면 이 점을 의심하세요(`UiTheme.font` 슬롯에 글꼴 파일을 넣으면 해결).
- **Unity 스플래시 로고**: Unity Personal 라이선스에서는 끌 수 없습니다.
- **화면 설정**: 기본은 1920x1080 테두리 없는 전체화면입니다. 해상도/창 모드는 타이틀 왼쪽 위 버튼에서 바꿀 수 있고 빌드에서만 실제로 적용됩니다.
- **빌드가 `Builds/` 아래에 쌓이는 것**: `.gitignore` 로 제외되어 있어 커밋되지 않습니다(필요 없으면 폴더째 지워도 됩니다).
- **아직 안 한 것(Step 11 나머지)**: GC/풀링 점검 등 최적화, 앱 아이콘, 설치 파일, 모바일. 친구 테스트에서 느림/끊김이 나오면 그때 프로파일링합니다.

## 처음 빌드하면 바뀌는 설정 파일

처음 Windows 빌드를 하면 유니티가 URP(렌더 파이프라인) 관련 설정을 채워 넣습니다: `ProjectSettings/ProjectSettings.asset`(URP 전역 설정을 preloadedAssets 에 등록), `Assets/Settings/*.asset`(셰이더 프리필터, 볼륨 기본값). 빌드가 이 값을 쓰므로 **커밋해 두는 것이 좋습니다**(다른 컴퓨터에서도 같은 빌드가 나옵니다).
반대로 `ProjectSettings/UnityConnectSettings.asset` 의 `m_Enabled` 처럼 개인 에디터 설정이 함께 바뀌어 있을 수 있으니 커밋 전에 확인하세요.
