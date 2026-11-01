# 야전(Step 15) 임시 그림을 코드로 다시 그린다. Windows PowerShell 5.1 (System.Drawing 내장)에서 실행.
#   powershell -File tools\field_art\generate.ps1                # 전부 (바닥 타일 6장 + 부대 시트 8장)
#   powershell -File tools\field_art\generate.ps1 -Only tile     # 바닥 타일만 (unit 도 가능)
# 결과: Assets\Sprites\Field\Tile_<지형>.png (48x24 마름모), Unit_<병과|General>_<Player|Enemy>.png (192x48 시트), 모아보기: %TEMP%\field_art_preview.png
# 바닥 색은 tools\terrain_art\terrain.json 의 지형 바닥 색을 그대로 쓴다 (전투 맵과 같은 색감).
# 같은 이름의 파일은 덮어쓴다. 직접 그린 그림으로 바꿨다면 해당 종류는 실행하지 말 것.
param([ValidateSet("all", "tile", "unit")][string]$Only = "all")

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$pixel = [IO.File]::ReadAllText((Join-Path (Split-Path -Parent $PSScriptRoot) "pixel\PixelTools.cs"), [Text.Encoding]::UTF8) -replace '(?m)^using .*;\r?\n', ''
$src = [IO.File]::ReadAllText("$PSScriptRoot\FieldArt.cs", [Text.Encoding]::UTF8) + "`n" + $pixel
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing

$out = Join-Path $root "Assets\Sprites\Field"
New-Item -ItemType Directory -Force $out | Out-Null

$json = [IO.File]::ReadAllText((Join-Path (Split-Path -Parent $PSScriptRoot) "terrain_art\terrain.json"), [Text.Encoding]::UTF8) | ConvertFrom-Json
$tileFiles = @()
foreach ($t in $json.themes) {
    $file = "Tile_$($t.terrain).png"
    $tileFiles += $file
    if ($Only -eq "all" -or $Only -eq "tile") {
        [FieldArt]::DrawTile((Join-Path $out $file), $t.ground.base, [string[]]$t.ground.accents)
        Write-Output "그림: $file"
    }
}

$unitFiles = @()
foreach ($cls in @("Infantry", "Archer", "Cavalry", "General")) {
    foreach ($side in @("Player", "Enemy")) {
        $file = "Unit_${cls}_${side}.png"
        $unitFiles += $file
        if ($Only -eq "all" -or $Only -eq "unit") {
            [FieldArt]::DrawUnitSheet((Join-Path $out $file), $cls, $side)
            Write-Output "그림: $file"
        }
    }
}

$preview = Join-Path $env:TEMP "field_art_preview.png"
[FieldArt]::Preview($out, [string[]]$tileFiles, [string[]]$unitFiles, $preview)
Write-Output "완료: $out (모아보기: $preview)"
