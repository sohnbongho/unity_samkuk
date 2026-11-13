#!/bin/bash
# Godot 전환(ADR 0004): 보류 테스트가 "원래 실패하던 것"인지 가리는 도구.
# 유니티 저장소의 옛 커밋(테스트가 마지막으로 고쳐진 때)의 스크립트와 테스트를 지금의 장면 모형(godot/core/Compat) 위에 올려 돌려 본다.
# 같은 실행 환경에서 "그때 코드로는 통과, 지금 코드로는 실패" 면 그 뒤의 코드 변경으로 깨진 테스트다.
#   tools/godot_port/old_snapshot.sh <커밋> <작업 폴더> <테스트 파일 이름(확장자 없이)>...
# 작업 폴더에 core/, tests/ 를 만든다(저장소는 건드리지 않는다). 빌드 오류가 나면 그 파일은 손으로 빼야 한다.
set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
COMMIT="$1"; WORK="$2"; shift 2
rm -rf "$WORK"; mkdir -p "$WORK/src" "$WORK/core" "$WORK/tests"
git -C "$ROOT" archive "$COMMIT" Assets/Scripts Assets/Tests/PlayMode | tar -x -C "$WORK/src"
# 지금의 호환 층과 데이터 읽기 (엔진 자리)
cp -r "$ROOT/godot/core/Compat" "$WORK/core/Compat"
mkdir -p "$WORK/core/Data"
cp "$ROOT/godot/core/Data/GameData.cs" "$ROOT/godot/core/Data/SceneFile.cs" "$WORK/core/Data/"
cp "$ROOT/godot/core/Samkuk.Core.csproj" "$WORK/core/"
# 그때의 런타임 스크립트 (에디터 스크립트 제외)
(cd "$WORK/src/Assets/Scripts" && find . -name '*.cs' -not -path './Editor/*') | while read -r f; do
  mkdir -p "$WORK/core/$(dirname "$f")"
  sed -f "$ROOT/tools/godot_port/usings.sed" "$WORK/src/Assets/Scripts/$f" > "$WORK/core/$f"
done
# 테스트: 지금의 도우미 + 그때의 테스트 파일
cp "$ROOT/godot/tests/Samkuk.Tests.csproj" "$ROOT/godot/tests/UnityTestSupport.cs" "$ROOT/godot/tests/AssetDatabase.cs" "$WORK/tests/"
for t in "$@"; do
  sed -f "$ROOT/tools/godot_port/usings.sed" "$WORK/src/Assets/Tests/PlayMode/$t.cs" > "$WORK/tests/$t.cs"
done
# 데이터·프로젝트 표시 (AssetDatabase 도우미가 project.godot 을 찾는다)
cp "$ROOT/godot/project.godot" "$WORK/"
cp -r "$ROOT/godot/data" "$WORK/data"
echo "준비됨: $WORK (core $(find "$WORK/core" -name '*.cs' | wc -l) 파일)"
