#!/bin/bash
# Godot 전환(ADR 0004): 유니티 PlayMode 테스트를 godot/tests 로 복사하며 using 을 바꾼다.
# 주의: 이미 옮긴 파일을 덮어쓴다(미룬 표시가 사라진다) — 복사 뒤 defer_tests.py 를 다시 돌린다.
# 사용: tools/godot_port/copy_tests.sh <테스트 이름(확장자 없이)>...
set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
for b in "$@"; do
  sed -e 's/\r$//' \
      -e 's/^using UnityEngine;$/using Samkuk.Compat;/' \
      -e '/^using UnityEngine\.TestTools;$/d' -e '/^using UnityEditor;$/d' \
      -e 's/UnityEngine\.Object/Samkuk.Compat.Object/g' \
      -e 's/UnityEngine\.Random/Samkuk.Compat.Random/g' \
      "$ROOT/Assets/Tests/PlayMode/$b.cs" > "$ROOT/godot/tests/$b.cs"
done
