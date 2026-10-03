# 장수 초상화(512x640 PNG)를 다시 그린다. Windows PowerShell 5.1 (System.Drawing 내장)에서 실행.
# 사용: powershell -File tools\hero_art\generate.ps1
# 결과: Assets\Sprites\Heroes\Hero_*.png (같은 이름의 파일은 덮어쓴다). 직접 그린 그림으로 바꿨다면 실행하지 말 것.
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$out = Join-Path $root "Assets\Sprites\Heroes"
$sheet = Join-Path $env:TEMP "hero_portraits_sheet.png"
$src = [IO.File]::ReadAllText("$PSScriptRoot\HeroArt.cs")
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing
[HeroArt]::Generate($out, $sheet)
Write-Output "완료: $out (미리보기: $sheet)"
