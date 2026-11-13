# Godot 전환(ADR 0004): godot/tests 를 돌려 실패한 테스트를 원인별로 묶어 보여 준다.
#   python tools/godot_port/fails.py [--list] [이름에 든 글자]
# 결과 원본은 godot/tests/obj/results.trx.
import io, os, re, subprocess, sys
import xml.etree.ElementTree as ET

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
TESTS = os.path.join(ROOT, 'godot', 'tests')
NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    listing = '--list' in sys.argv
    trx = os.path.join(TESTS, 'obj', 'results.trx')
    cmd = ['dotnet', 'test', '-nologo', '-v', 'q', '--blame-hang-timeout', '120s', '--logger', 'trx;LogFileName=' + trx]
    out = subprocess.run(cmd, cwd=TESTS, capture_output=True, text=True, encoding='utf-8', errors='replace').stdout
    tail = [l for l in out.splitlines() if '통과:' in l or 'error' in l]
    print(tail[-1].strip() if tail else out[-400:])
    if not os.path.exists(trx):
        return 1
    root = ET.parse(trx).getroot()
    classes = {}
    for ut in root.iterfind('.//t:UnitTest', NS):
        tm = ut.find('t:TestMethod', NS)
        classes[ut.get('id')] = tm.get('className').split('.')[-1]
    groups = {}
    for r in root.iterfind('.//t:UnitTestResult', NS):
        if r.get('outcome') != 'Failed':
            continue
        name = classes.get(r.get('testId'), '?') + '.' + r.get('testName')
        if args and not any(a in name for a in args):
            continue
        msg = r.find('.//t:Message', NS)
        stack = r.find('.//t:StackTrace', NS)
        text = (msg.text or '').strip() if msg is not None else ''
        first = text.split(chr(10))[0].strip()[:110]
        where = ''
        if stack is not None and stack.text:
            m = re.search(r'at (Samkuk[.][^(]+)[(].*?:line (\d+)', stack.text)
            if m:
                where = m.group(1).split('.')[-2] + '.' + m.group(1).split('.')[-1] + ':' + m.group(2)
        key = re.sub(r'[0-9.]+', '#', first) if not listing else name
        groups.setdefault(key, []).append((name, first, where, text))
    for key, items in sorted(groups.items(), key=lambda kv: -len(kv[1])):
        if listing:
            n, first, where, text = items[0]
            print('%s | %s | %s' % (n, where, ' / '.join(x.strip() for x in text.split(chr(10))[:4])[:230]))
        else:
            print('%3d  %s   예: %s @ %s' % (len(items), key, items[0][0], items[0][2]))
    return 0


if __name__ == '__main__':
    sys.exit(main())
