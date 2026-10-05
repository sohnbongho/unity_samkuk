# 성 배경 그림 46장과 전략 지도 그림을 코드로 다시 그린다. Windows PowerShell 5.1 (System.Drawing 내장)에서 실행.
#   powershell -File tools\castle_art\generate.ps1                  # 전부 (성 46장 + 전략 지도)
#   powershell -File tools\castle_art\generate.ps1 -Only Luoyang    # 한 곳만 (성 id)
#   powershell -File tools\castle_art\generate.ps1 -Only Map        # 전략 지도만
# 기준표: tools\castle_art\castles.json (id, 이름, 주, 지형, 크기, 강 유무, 지도 위치)
# 결과: Assets\Sprites\Castles\Castle_<id>.png (1280x720), Assets\Sprites\Strategy\StrategyMap.png (1600x900),
#       모아보기: %TEMP%\castle_backgrounds_sheet.png
# 같은 이름의 파일은 덮어쓴다. 직접 그린 그림으로 바꿨다면 해당 그림은 -Only 로 피해서 실행할 것.
param([string]$Only = "")

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# CastleArt.cs, MapArt.cs 는 한 소스로 이어 붙인다 (using 줄은 첫 파일에만 둔다)
$src = [IO.File]::ReadAllText("$PSScriptRoot\CastleArt.cs", [Text.Encoding]::UTF8) + "`n" +
    ([IO.File]::ReadAllText("$PSScriptRoot\MapArt.cs", [Text.Encoding]::UTF8) -replace '(?m)^using .*;\r?\n', '')
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing

$json = [IO.File]::ReadAllText("$PSScriptRoot\castles.json", [Text.Encoding]::UTF8) | ConvertFrom-Json

if ($Only -eq "" -or $Only -ne "Map") {
    $out = Join-Path $root "Assets\Sprites\Castles"
    New-Item -ItemType Directory -Force $out | Out-Null
    $files = @()
    foreach ($c in $json.castles) {
        $file = "Castle_$($c.id).png"
        $files += $file
        if ($Only -ne "" -and $c.id -ne $Only) { continue }
        [CastleArt]::Draw((Join-Path $out $file), $c.id, $c.name, $c.hanja, $c.terrain, [int]$c.size, [bool]$c.water)
        Write-Output "그림: $file"
    }
    $sheet = Join-Path $env:TEMP "castle_backgrounds_sheet.png"
    [CastleArt]::Sheet($out, [string[]]$files, $sheet, 8)
    Write-Output "완료: $out (모아보기: $sheet)"
}

if ($Only -eq "" -or $Only -eq "Map") {
    $mapDir = Join-Path $root "Assets\Sprites\Strategy"
    New-Item -ItemType Directory -Force $mapDir | Out-Null
    $regions = [string[]]($json.castles | ForEach-Object { $_.region })
    $xs = [single[]]($json.castles | ForEach-Object { [single]$_.x })
    $ys = [single[]]($json.castles | ForEach-Object { [single]$_.y })
    [MapArt]::Draw((Join-Path $mapDir "StrategyMap.png"), $regions, $xs, $ys)
    Write-Output "전략 지도 완료: $mapDir\StrategyMap.png"
}
