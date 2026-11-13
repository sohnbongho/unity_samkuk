# Godot 전환(ADR 0004): 미룬 테스트를 전부 풀어 다시 시도하게 한다.
# 기능을 옮긴 뒤에 돌린다:  python tools/godot_port/undefer_tests.py  &&  python tools/godot_port/defer_tests.py
# (defer_tests.py 가 아직 컴파일되지 않는 것만 다시 감싼다. [Ignore] 로 보류한 테스트는 건드리지 않는다.)
import glob, io, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
NL = chr(10)
MARK = '// (미룸) '


def main():
    names = sys.argv[1:]
    blocks = lines_restored = 0
    for path in sorted(glob.glob(os.path.join(ROOT, 'godot', 'tests', '*.cs'))):
        if names and os.path.splitext(os.path.basename(path))[0] not in names:
            continue
        lines = io.open(path, encoding='utf-8').read().split(NL)
        out = []
        depth = 0
        for line in lines:
            if line.startswith('#if GAME_PORT'):
                depth += 1
                blocks += 1
                continue
            if line.startswith('#endif') and depth > 0:
                depth -= 1
                continue
            stripped = line.lstrip(' ')
            if stripped.startswith(MARK):
                line = line[:len(line) - len(stripped)] + stripped[len(MARK):]
                lines_restored += 1
            out.append(line)
        io.open(path, 'w', encoding='utf-8', newline=NL).write(NL.join(out))
    print('푼 블록 %d, 되돌린 줄 %d' % (blocks, lines_restored))


if __name__ == '__main__':
    main()
