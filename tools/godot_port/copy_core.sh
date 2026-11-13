#!/bin/bash
# Godot 전환(ADR 0004): 유니티 소스에서 순수 로직 파일을 godot/core 로 복사하고 using 만 바꾼다.
# 처음 옮길 때 한 번 쓰는 도구다. 이미 있는 파일은 덮어쓰지 않는다(옮긴 뒤 고친 내용을 지키려고).
# 사용: tools/godot_port/copy_core.sh <Assets/Scripts 기준 상대 경로>...
set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SRC="$ROOT/Assets/Scripts"
DST="$ROOT/godot/core"
for rel in "$@"; do
  for f in $(cd "$SRC" && find $rel -name '*.cs'); do
    out="$DST/$f"
    if [ -e "$out" ]; then continue; fi
    mkdir -p "$(dirname "$out")"
    sed -e 's/^using UnityEngine;$/using Samkuk.Compat;/' -e 's/\r$//' "$SRC/$f" > "$out"
    echo "copied $f"
  done
done
