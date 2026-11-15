#!/bin/bash
# Godot 전환 G11: Windows 릴리스 빌드를 내보내고 공유용 zip 을 만든다.
#   tools/godot_port/build_windows.sh          릴리스 (친구에게 보내는 것)
#   tools/godot_port/build_windows.sh --dev    개발용: 타이틀의 [맵 편집기](전투 테스트)와 FPS 표시·F1~F4 치트가 켜진다
# 결과: Builds/godot/Samkuk.exe (+ Samkuk.pck, data_Samkuk.Game_windows_x86_64/), Builds/Samkuk-godot-win64.zip
# 내보내기 템플릿은 godot/export_presets.cfg 의 custom_template 경로(D:/01-AgentLegion/tools/godot/templates/...)를 쓴다.
# 경로가 다른 PC 에서는 환경 변수 GODOT(콘솔 실행 파일), GODOT_TEMPLATES(템플릿 폴더)로 바꾼다.
# 릴리스 빌드에서는 FPS 표시와 F1~F4 치트(DebugOverlay)가 꺼진다.
set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
GODOT="${GODOT:-/d/01-AgentLegion/tools/godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe}"
OUT="$ROOT/Builds/godot"
MODE=--export-release
[ "$1" = "--dev" ] && MODE=--export-debug
rm -rf "$OUT"
mkdir -p "$OUT"
cd "$ROOT/godot"
# 내보내며 도는 dotnet 빌드가 컴파일 서버를 남기면 그 서버가 로그 파일 손잡이를 쥐어 Godot 콘솔 실행 파일이 끝나지 않는다.
export UseSharedCompilation=false MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0
# 다른 PC: GODOT_TEMPLATES 에 템플릿 폴더(windows_*_x86_64.exe 가 든 곳, 예: C:/agntJobs/tools/godot/templates/templates)를
# 넣으면 내보내는 동안만 export_presets.cfg 의 custom_template 경로를 그쪽으로 바꾸고 끝나면 되돌린다.
if [ -n "$GODOT_TEMPLATES" ]; then
  cp export_presets.cfg "$ROOT/Builds/export_presets.cfg.orig"
  trap 'cp "$ROOT/Builds/export_presets.cfg.orig" "$ROOT/godot/export_presets.cfg"; rm -f "$ROOT/Builds/export_presets.cfg.orig"' EXIT
  sed -i "s#^\(custom_template/[a-z]*\)=\".*/\(windows_[a-z]*_x86_64\.exe\)\"#\1=\"${GODOT_TEMPLATES%/}/\2\"#" export_presets.cfg
fi
"$GODOT" --headless --path . $MODE "Windows Desktop" ../Builds/godot/Samkuk.exe > "$ROOT/Builds/godot_export.log" 2>&1 || { tail -20 "$ROOT/Builds/godot_export.log"; exit 1; }
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
