#!/bin/bash
# Godot 전환 G11: Windows 릴리스 빌드를 내보내고 공유용 zip 을 만든다.
#   tools/godot_port/build_windows.sh
# 결과: Builds/godot/Samkuk.exe (+ Samkuk.pck, data_Samkuk.Game_windows_x86_64/), Builds/Samkuk-godot-win64.zip
# 내보내기 템플릿은 godot/export_presets.cfg 의 custom_template 경로(D:/01-AgentLegion/tools/godot/templates/...)를 쓴다.
# 릴리스 빌드에서는 FPS 표시와 F1~F4 치트(DebugOverlay)가 꺼진다.
set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
GODOT="${GODOT:-/d/01-AgentLegion/tools/godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe}"
OUT="$ROOT/Builds/godot"
rm -rf "$OUT"
mkdir -p "$OUT"
cd "$ROOT/godot"
"$GODOT" --headless --path . --export-release "Windows Desktop" ../Builds/godot/Samkuk.exe > "$ROOT/Builds/godot_export.log" 2>&1 || { tail -20 "$ROOT/Builds/godot_export.log"; exit 1; }
cat > "$OUT/README.txt" <<'TXT'
삼국지 서바이버 (Godot 판)

실행: Samkuk.exe 를 더블클릭합니다. 폴더 안의 파일을 함께 두어야 합니다.
조작: WASD/방향키 이동, Space 스킬, 1~3 선택, ESC 일시정지.
처음 화면의 로그인 창에서 [오프라인으로 계속] 을 누르면 혼자 할 수 있습니다.
저장 위치: %APPDATA%\Godot\app_userdata\Samkuk\save.json
TXT
cd "$ROOT/Builds"
rm -f Samkuk-godot-win64.zip
python -c "import shutil; shutil.make_archive('Samkuk-godot-win64', 'zip', 'godot')"
ls -la "$OUT" Samkuk-godot-win64.zip | grep -v '^total'
