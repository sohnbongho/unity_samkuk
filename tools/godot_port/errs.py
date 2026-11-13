# Godot 전환(ADR 0004): 프로젝트를 빌드해 오류를 "파일:줄: 코드 | 원본 줄" 로 짧게 보여 준다.
#   python tools/godot_port/errs.py core|tests|game [최대 줄 수]
import io, os, re, subprocess, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
DIRS = {'core': ('godot/core', None), 'tests': ('godot/tests', None), 'game': ('godot', 'Samkuk.Game.csproj')}
ERR = re.compile(r'^(.*?[.]cs)[(](\d+),\d+[)]: error (CS\d+): (.*?)( [\[]|$)')


def main():
    sub, proj = DIRS[sys.argv[1]]
    limit = int(sys.argv[2]) if len(sys.argv) > 2 else 60
    cwd = os.path.join(ROOT, sub)
    cmd = ['dotnet', 'build', '-nologo', '-v', 'q'] + ([proj] if proj else [])
    out = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, encoding='utf-8', errors='replace').stdout
    seen, rows = set(), []
    for line in out.splitlines():
        m = ERR.match(line.strip())
        if not m:
            continue
        key = (m.group(1), m.group(2), m.group(3))
        if key in seen:
            continue
        seen.add(key)
        path = m.group(1)
        try:
            src = io.open(path, encoding='utf-8').read().split(chr(10))[int(m.group(2)) - 1].strip()
        except Exception:
            src = ''
        rows.append('%s:%s: %s %s | %s' % (os.path.relpath(path, cwd).replace(os.sep, '/'), m.group(2), m.group(3), m.group(4)[:70], src[:150]))
    print('오류 %d' % len(rows))
    for r in sorted(rows)[:limit]:
        print(r)
    return 1 if rows else 0


if __name__ == '__main__':
    sys.exit(main())
