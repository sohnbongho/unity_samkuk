# 전투 맵의 지형 바닥 타일과 소품 그림을 코드로 다시 그린다. Windows PowerShell 5.1 (System.Drawing 내장)에서 실행.
#   powershell -File tools\terrain_art\generate.ps1                  # 전부
#   powershell -File tools\terrain_art\generate.ps1 -Only Jungle     # 한 지형만 (Plain/Steppe/Mountain/River/Jungle/Loess, 공용은 Shared)
# 기준표: tools\terrain_art\terrain.json (파일 이름, 그리는 방식, 색)
# 결과: Assets\Sprites\Terrain\Ground_*.png, Prop_*.png, 모아보기: %TEMP%\terrain_sheet.png
# 같은 이름의 파일은 덮어쓴다. 직접 그린 그림으로 바꿨다면 해당 지형은 -Only 로 피해서 실행할 것.
param([string]$Only = "")

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Add-Type -TypeDefinition ([IO.File]::ReadAllText("$PSScriptRoot\TerrainArt.cs", [Text.Encoding]::UTF8)) -ReferencedAssemblies System.Drawing

$json = [IO.File]::ReadAllText("$PSScriptRoot\terrain.json", [Text.Encoding]::UTF8) | ConvertFrom-Json
$out = Join-Path $root "Assets\Sprites\Terrain"
New-Item -ItemType Directory -Force $out | Out-Null

if ($Only -eq "" -or $Only -eq "Shared") {
    foreach ($p in $json.shared) {
        [TerrainArt]::DrawProp((Join-Path $out "$($p.file).png"), $p.kind, [string[]]$p.colors, 0)
        Write-Output "그림: $($p.file).png"
    }
}

$groundFiles = @(); $propFiles = @()
foreach ($t in $json.themes) {
    $groundFiles += "$($t.ground.file).png"
    $propFiles += , ([string[]]($t.props | ForEach-Object { "$($_.file).png" }))
    if ($Only -ne "" -and $Only -ne $t.terrain) { continue }
    [TerrainArt]::DrawGround((Join-Path $out "$($t.ground.file).png"), $t.ground.base, [string[]]$t.ground.accents, $t.ground.detail)
    Write-Output "그림: $($t.ground.file).png"
    foreach ($p in $t.props) {
        [TerrainArt]::DrawProp((Join-Path $out "$($p.file).png"), $p.kind, [string[]]$p.colors, 0)
        Write-Output "그림: $($p.file).png"
    }
    if ($t.river) {
        [TerrainArt]::DrawProp((Join-Path $out "$($t.river.file)_Water.png"), "riverwater", [string[]]@($t.river.water), 0)
        [TerrainArt]::DrawProp((Join-Path $out "$($t.river.file)_Bank.png"), "riverbank", [string[]]@($t.river.bank), 0)
        Write-Output "그림: $($t.river.file)_Water.png, _Bank.png"
    }
}

$sheet = Join-Path $env:TEMP "terrain_sheet.png"
[TerrainArt]::Sheet($out, [string[]]$groundFiles, [string[][]]$propFiles, $sheet)
Write-Output "완료: $out (모아보기: $sheet)"
