# 장수/적 그림을 코드로 다시 그린다. Windows PowerShell 5.1 (System.Drawing 내장)에서 실행.
#   powershell -File tools\hero_art\generate.ps1                 # 전부 (초상화 + 장수 걷기 + 적 걷기)
#   powershell -File tools\hero_art\generate.ps1 -Only enemy     # 적 걷기 시트만 (portrait / walk 도 가능)
# 결과: Assets\Sprites\Heroes\Hero_*.png (초상화), Assets\Sprites\HeroWalk\Hero_*_Walk.png (장수 걷기),
#       Assets\Sprites\EnemyWalk\*_Walk.png (적 걷기)
# 같은 이름의 파일은 덮어쓴다. 직접 그린 그림으로 바꿨다면 해당 종류는 실행하지 말 것.
param([ValidateSet("all", "portrait", "walk", "enemy")][string]$Only = "all")

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# HeroSprites.cs, EnemySprites.cs 는 HeroArt 의 partial 이라 한 소스로 이어 붙인다 (using 줄은 첫 파일에만 둔다)
$rest = @("HeroSprites.cs", "EnemySprites.cs") | ForEach-Object {
    [IO.File]::ReadAllText("$PSScriptRoot\$_") -replace '(?m)^using .*;\r?\n', ''
}
$src = [IO.File]::ReadAllText("$PSScriptRoot\HeroArt.cs") + "`n" + ($rest -join "`n")
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing

if ($Only -eq "all" -or $Only -eq "portrait") {
    $out = Join-Path $root "Assets\Sprites\Heroes"
    $sheet = Join-Path $env:TEMP "hero_portraits_sheet.png"
    [HeroArt]::Generate($out, $sheet)
    Write-Output "초상화 완료: $out (미리보기: $sheet)"
}
if ($Only -eq "all" -or $Only -eq "walk") {
    $out = Join-Path $root "Assets\Sprites\HeroWalk"
    $preview = Join-Path $env:TEMP "hero_walk_preview.png"
    [HeroArt]::GenerateWalkSheets($out, $preview)
    Write-Output "장수 걷기 시트 완료: $out (미리보기: $preview)"
}
if ($Only -eq "all" -or $Only -eq "enemy") {
    $out = Join-Path $root "Assets\Sprites\EnemyWalk"
    $preview = Join-Path $env:TEMP "enemy_walk_preview.png"
    [HeroArt]::GenerateEnemySheets($out, $preview)
    Write-Output "적 걷기 시트 완료: $out (미리보기: $preview)"
}
