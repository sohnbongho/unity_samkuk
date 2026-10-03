# 장수 그림을 코드로 다시 그린다. Windows PowerShell 5.1 (System.Drawing 내장)에서 실행.
#   powershell -File tools\hero_art\generate.ps1            # 초상화 + 걷기 시트 모두
#   powershell -File tools\hero_art\generate.ps1 -Only walk # 걷기 시트만 (portrait 이면 초상화만)
# 결과: Assets\Sprites\Heroes\Hero_*.png (초상화), Assets\Sprites\HeroWalk\Hero_*_Walk.png (걷기 시트)
# 같은 이름의 파일은 덮어쓴다. 직접 그린 그림으로 바꿨다면 해당 종류는 실행하지 말 것.
param([ValidateSet("all", "portrait", "walk")][string]$Only = "all")

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# HeroSprites.cs 는 HeroArt 의 partial 이라 한 소스로 이어 붙인다 (using 줄은 첫 파일에만 둔다)
$second = [IO.File]::ReadAllText("$PSScriptRoot\HeroSprites.cs") -replace '(?m)^using .*;\r?\n', ''
$src = [IO.File]::ReadAllText("$PSScriptRoot\HeroArt.cs") + "`n" + $second
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing

if ($Only -ne "walk") {
    $out = Join-Path $root "Assets\Sprites\Heroes"
    $sheet = Join-Path $env:TEMP "hero_portraits_sheet.png"
    [HeroArt]::Generate($out, $sheet)
    Write-Output "초상화 완료: $out (미리보기: $sheet)"
}
if ($Only -ne "portrait") {
    $out = Join-Path $root "Assets\Sprites\HeroWalk"
    $preview = Join-Path $env:TEMP "hero_walk_preview.png"
    [HeroArt]::GenerateWalkSheets($out, $preview)
    Write-Output "걷기 시트 완료: $out (미리보기: $preview)"
}
