# Godot 전환(ADR 0004): 유니티 PlayMode 테스트를 godot/tests 로 옮길 때 쓰는 도구.
# 아직 옮기지 않은 것(노드, 씬, 에셋)에 기대는 테스트 메서드를 `#if GAME_PORT ... #endif` 로 감싸
# 지우지 않고 미뤄 둔다. 해당 기능을 옮기면 감싼 것을 풀어 되살린다(grep GAME_PORT 로 남은 양을 센다).
#
# 사용: python tools/godot_port/defer_tests.py            (godot/tests 를 빌드해 오류가 없어질 때까지 반복)
import io, os, re, subprocess, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
TESTS = os.path.join(ROOT, 'godot', 'tests')
ERR = re.compile(r'^(.*?\.cs)\((\d+),\d+\): error (CS\d+)')
MARK_BEGIN = '#if GAME_PORT   // 미룸: 아직 옮기지 않은 부분에 기댄다'
MARK_END = '#endif'


def build():
    out = subprocess.run(['dotnet', 'build', '-nologo', '-v', 'q'], cwd=TESTS, capture_output=True, text=True, encoding='utf-8', errors='replace').stdout
    errors = {}
    io.open(os.path.join(ROOT, 'godot', 'tests', 'obj', 'defer_log.txt'), 'a', encoding='utf-8').write(out + chr(10))   # 어떤 이름 때문에 미뤘는지 나중에 볼 수 있게 남긴다
    for line in out.splitlines():
        m = ERR.match(line.strip())
        if m and os.path.abspath(m.group(1)).startswith(TESTS):
            errors.setdefault(os.path.abspath(m.group(1)), set()).add(int(m.group(2)) - 1)
    return errors


def indent(line):
    return len(line) - len(line.lstrip(' '))


def member_range(lines, at):
    """오류 줄을 품은 멤버(메서드/필드/안쪽 클래스)의 [시작, 끝] 줄. 못 찾으면 None."""
    for level in (8, 4):
        s = at
        while s >= 0:
            t = lines[s]
            if t.strip() and indent(t) == level and t.strip() not in ('{', '}') and not t.strip().startswith(('#', ')')):
                break
            if t.strip() and indent(t) < level:
                s = -1
                break
            s -= 1
        if s < 0:
            continue
        # 서명 줄 찾기: 속성/주석 줄을 건너 아래로
        sig = s
        while sig < len(lines) and lines[sig].strip().startswith(('[', '//')):
            sig += 1
        # 위로 붙은 속성/주석 포함
        while s > 0 and indent(lines[s - 1]) == level and lines[s - 1].strip().startswith(('[', '//')):
            s -= 1
        # 끝 찾기
        e = sig
        nxt = sig + 1
        while nxt < len(lines) and not lines[nxt].strip():
            nxt += 1
        # 여러 줄 서명(매개변수 줄바꿈)을 지나 여는 중괄호를 찾는다
        probe = sig
        block = False
        while probe < len(lines):
            st = lines[probe].strip()
            if st == '{' and indent(lines[probe]) == level:
                block = True
                break
            if st.endswith(';') and indent(lines[probe]) >= level and probe >= sig:
                break
            if st.endswith('{') and probe == sig:   # 같은 줄에서 여는 경우 (초기화 식 등)
                block = True
                break
            probe += 1
        if block:
            e = probe
            closers = ('}', '};')
            while e < len(lines) and not (indent(lines[e]) == level and lines[e].strip() in closers):
                e += 1
        else:
            e = probe
        if s <= at <= e:
            return s, e
    return None


def main():
    for round_no in range(40):
        errors = build()
        if not errors:
            print('빌드 오류 없음 (%d 회)' % round_no)
            return 0
        total = 0
        for path, rows in errors.items():
            lines = io.open(path, encoding='utf-8').read().split('\n')
            ranges = []
            for at in sorted(rows):
                if lines[at].startswith('using '):
                    ranges.append((at, at))
                    continue
                r = member_range(lines, at)
                if r is None:
                    print('감쌀 범위를 못 찾음: %s:%d  %s' % (path, at + 1, lines[at].strip()))
                    continue
                ranges.append(r)
            # 겹치는 범위 합치기
            merged = []
            for s, e in sorted(set(ranges)):
                if merged and s <= merged[-1][1]:
                    merged[-1] = (merged[-1][0], max(merged[-1][1], e))
                else:
                    merged.append((s, e))
            for s, e in reversed(merged):
                head = ' '.join(lines[s:min(e, s + 4) + 1])
                if re.search(r'\[(SetUp|TearDown|OneTimeSetUp|OneTimeTearDown)\]', head):
                    # 준비/정리 메서드는 통째로 미루면 다른 테스트가 실제 파일을 건드릴 수 있다: 오류 줄만 뺀다
                    for at in sorted(rows):
                        if s <= at <= e and not lines[at].lstrip().startswith('// (미룸)'):
                            lines[at] = lines[at][:indent(lines[at])] + '// (미룸) ' + lines[at].lstrip()
                elif s == e and lines[s].startswith('using '):
                    lines[s] = '// (미룸) ' + lines[s]
                else:
                    lines[e + 1:e + 1] = [MARK_END]
                    lines[s:s] = [MARK_BEGIN]
                total += 1
            io.open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(lines))
        print('%d 회: 파일 %d개, 미룬 곳 %d' % (round_no + 1, len(errors), total))
        if total == 0:
            return 1
    return 1


if __name__ == '__main__':
    sys.exit(main())
