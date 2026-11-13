# Godot 전환(ADR 0004): 옮긴 파일을 고치는 작은 도우미. 셸 인용 문제를 피하려고 치환 목록을 JSON 파일로 받는다.
#   python tools/godot_port/patch.py <치환목록.json>
# 치환목록: [{"file": "godot/core/...", "old": "...", "new": "...", "count": 1(생략 가능, 0 = 전부)}, ...]
# old 가 파일에 없으면 멈춘다(조용히 넘어가지 않는다).
import io, json, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))


def main():
    edits = json.load(io.open(sys.argv[1], encoding='utf-8'))
    for e in edits:
        path = os.path.join(ROOT, e['file'])
        text = io.open(path, encoding='utf-8').read()
        if e['old'] not in text:
            print('없음: %s :: %s' % (e['file'], e['old'][:80]))
            return 1
        count = e.get('count', 1)
        text = text.replace(e['old'], e['new']) if count == 0 else text.replace(e['old'], e['new'], count)
        io.open(path, 'w', encoding='utf-8', newline=chr(10)).write(text)
    print('고친 곳 %d' % len(edits))
    return 0


if __name__ == '__main__':
    sys.exit(main())
