# Windows 테스트 빌드를 명령줄로 만든다 (친구에게 나눠 줄 zip 까지). Windows PowerShell 5.1 에서 실행.
#   powershell -File tools\build_windows.ps1            # 친구 공유용 (릴리스: 디버그 오버레이/치트 꺼짐)
#   powershell -File tools\build_windows.ps1 -Dev       # 개발용 (Development Build: F1~F4, FPS 표시 켜짐)
#   powershell -File tools\build_windows.ps1 -Unity "D:\Unity\6000.5.6f1\Editor\Unity.exe"   # 유니티 위치를 직접 지정
# 유니티 에디터가 이 프로젝트를 열고 있으면 실행할 수 없다 → 에디터를 닫거나 메뉴 Samkuk > Build 를 쓸 것.
# 결과: Builds\SamkukSurvivor_v<버전>\ (폴더)와 Builds\SamkukSurvivor_v<버전>_<날짜>.zip, 로그 Builds\build.log
param([switch]$Dev, [string]$Unity = "")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

# 1. 유니티 실행 파일: 직접 지정 > 환경 변수 UNITY_PATH > Unity Hub 기본 위치(프로젝트가 쓰는 버전)
$versionLine = Select-String -Path (Join-Path $root "ProjectSettings\ProjectVersion.txt") -Pattern "m_EditorVersion:\s*(\S+)" | Select-Object -First 1
$version = $versionLine.Matches[0].Groups[1].Value
if (-not $Unity) { $Unity = $env:UNITY_PATH }
if (-not $Unity) { $Unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe" }
if (-not (Test-Path $Unity)) {
    Write-Error "유니티 $version 을 찾지 못했습니다: $Unity`n -Unity 로 Unity.exe 경로를 지정하거나 환경 변수 UNITY_PATH 를 설정하세요."
}

# 2. 에디터가 프로젝트를 열고 있으면 배치 모드 빌드가 실패한다 (잠금 파일을 에디터가 쥐고 있음)
$lock = Join-Path $root "Temp\UnityLockfile"
if (Test-Path $lock) {
    try { $fs = [IO.File]::Open($lock, "Open", "ReadWrite", "None"); $fs.Close() }
    catch { Write-Error "유니티 에디터가 이 프로젝트를 열고 있습니다. 에디터를 닫고 다시 실행하거나, 에디터 메뉴 Samkuk > Build 를 쓰세요." }
}

# 3. 배치 모드 빌드
$method = if ($Dev) { "Samkuk.EditorTools.BuildTool.BuildWindowsDevelopmentCli" } else { "Samkuk.EditorTools.BuildTool.BuildWindowsReleaseCli" }
$builds = Join-Path $root "Builds"
New-Item -ItemType Directory -Force $builds | Out-Null
$log = Join-Path $builds "build.log"
Write-Output "유니티 $version 으로 빌드합니다 ($(if ($Dev) { '개발용' } else { '친구 공유용' })). 처음에는 몇 분 걸립니다. 로그: $log"

$proc = Start-Process -FilePath $Unity -ArgumentList @("-batchmode", "-nographics", "-quit", "-projectPath", "`"$root`"", "-executeMethod", $method, "-logFile", "`"$log`"") -Wait -PassThru
if ($proc.ExitCode -ne 0) {
    Write-Output "---- 로그에서 오류 줄 ----"
    Select-String -Path $log -Pattern "error CS|\[Samkuk\] 빌드|Error building|BuildFailed|Build Failed" | Select-Object -Last 15 | ForEach-Object { $_.Line }
    Write-Error "빌드 실패 (종료 코드 $($proc.ExitCode)). 전체 로그: $log"
}

# 4. 결과
$zip = Get-ChildItem $builds -Filter "SamkukSurvivor_v*.zip" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Output ""
Write-Output "빌드 완료"
Write-Output ("  친구에게 보낼 파일: {0}  ({1:0.0} MB)" -f $zip.FullName, ($zip.Length / 1MB))
