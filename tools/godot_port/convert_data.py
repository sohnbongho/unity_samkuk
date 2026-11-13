# Godot 전환 G2 (ADR 0004): 유니티 데이터 에셋(.asset, YAML)을 JSON 으로, 그림(PNG)을 godot/sprites 로 옮긴다.
#
#   python tools/godot_port/convert_data.py
#
# - Assets/ScriptableObjects/**, Assets/Resources/*.asset  ->  godot/data/<같은 하위 경로>/<이름>.json
# - Assets/Sprites/**/*.png                                ->  godot/sprites/<같은 하위 경로>
# - Assets/Resources/Maps/*.json (맵 편집기로 고친 맵)       ->  godot/data/Maps/
#
# JSON 한 건: {"$type": "Samkuk.Data.WeaponData", "name": "Weapon_Bow", 필드...}
#   다른 데이터 참조  {"$ref": "Weapon_Bow"}        (이름 = 유니티 에셋 파일명)
#   스프라이트        {"$sprite": "res://sprites/...png", "w": 96, "h": 176, "ppu": 32, "pivot": [0.5, 0.03], "border": [l, b, r, t]}
#   텍스처            {"$texture": "res://sprites/...png", "w": 192, "h": 192}
#   빈 참조           null
#
# 유니티 원본이 있는 동안만 쓰는 도구다. **다시 돌리면 godot/data 를 덮어쓴다** — 전환 뒤 값은 JSON 을 직접 고치므로,
# JSON 을 고치기 시작한 뒤에는 돌리지 않는다(돌려야 하면 git diff 로 확인).
import io, json, os, re, shutil, struct, sys

import yaml

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
ASSETS = os.path.join(ROOT, 'Assets')
OUT_DATA = os.path.join(ROOT, 'godot', 'data')
OUT_SPRITES = os.path.join(ROOT, 'godot', 'sprites')

# 스프라이트 정렬(alignment) -> 피벗 비율
ALIGN = {0: (0.5, 0.5), 1: (0, 1), 2: (0.5, 1), 3: (1, 1), 4: (0, 0.5), 5: (1, 0.5), 6: (0, 0), 7: (0.5, 0), 8: (1, 0)}


def load_unity_yaml(path):
    text = io.open(path, encoding='utf-8').read()
    # 유니티 머리말(%YAML, %TAG, --- !u!114 &id)은 표준 파서가 못 읽는다: 첫 문서 본문만 남긴다
    body = re.split(r'^--- !u![^\n]*\n', text, flags=re.M)
    return yaml.safe_load(body[1] if len(body) > 1 else text)


def png_size(path):
    with open(path, 'rb') as f:
        head = f.read(24)
    return struct.unpack('>II', head[16:24])


def build_guid_map():
    """guid -> (종류, Assets 기준 경로). .meta 파일의 guid 줄만 읽는다."""
    guids = {}
    for base, _, files in os.walk(ASSETS):
        for name in files:
            if not name.endswith('.meta'):
                continue
            meta = os.path.join(base, name)
            with io.open(meta, encoding='utf-8', errors='replace') as f:
                for line in f:
                    if line.startswith('guid:'):
                        guids[line.split(':', 1)[1].strip()] = os.path.relpath(meta[:-5], ASSETS).replace('\\', '/')
                        break
    return guids


def sprite_info(rel):
    """그림의 임포트 설정(PPU, 피벗, 9-슬라이스 테두리)과 크기."""
    full = os.path.join(ASSETS, rel)
    w, h = png_size(full)
    meta = load_unity_yaml(full + '.meta') or {}
    imp = meta.get('TextureImporter', {}) or {}
    ppu = imp.get('spritePixelsToUnits', 100)
    align = imp.get('alignment', 0)
    if align == 9:
        p = imp.get('spritePivot', {'x': 0.5, 'y': 0.5})
        pivot = (p['x'], p['y'])
    else:
        pivot = ALIGN.get(align, (0.5, 0.5))
    b = imp.get('spriteBorder', {}) or {}
    border = [b.get('x', 0), b.get('y', 0), b.get('z', 0), b.get('w', 0)]
    return w, h, ppu, pivot, border


def res_path(rel):
    assert rel.startswith('Sprites/'), rel
    return 'res://sprites/' + rel[len('Sprites/'):]


class Converter:
    def __init__(self):
        self.guids = build_guid_map()
        self.missing = set()

    def ref(self, node):
        file_id, guid = node.get('fileID', 0), node.get('guid')
        if not file_id or not guid:
            return None
        rel = self.guids.get(guid)
        if rel is None:
            self.missing.add(guid)
            return None
        if rel.endswith('.asset'):
            return {'$ref': os.path.splitext(os.path.basename(rel))[0]}
        if rel.endswith('.png'):
            w, h, ppu, pivot, border = sprite_info(rel)
            if file_id == 21300000:   # Sprite
                out = {'$sprite': res_path(rel), 'w': w, 'h': h, 'ppu': ppu, 'pivot': [round(pivot[0], 6), round(pivot[1], 6)]}
                if any(border):
                    out['border'] = border
                return out
            return {'$texture': res_path(rel), 'w': w, 'h': h}   # Texture2D (2800000)
        self.missing.add(rel)
        return None

    def value(self, node):
        if isinstance(node, dict):
            if 'fileID' in node:
                return self.ref(node)
            return {k: self.value(v) for k, v in node.items()}
        if isinstance(node, list):
            return [self.value(v) for v in node]
        return node

    def asset(self, path):
        doc = load_unity_yaml(path)
        mb = doc['MonoBehaviour']
        ident = str(mb.get('m_EditorClassIdentifier', ''))
        # "Samkuk.Runtime::Samkuk.Data.WeaponData" 또는 예전 표기 "Samkuk.Runtime:Samkuk.Data:TerrainThemeCatalog"
        if '::' in ident:
            type_name = ident.split('::', 1)[1]
        else:
            parts = ident.split(':')
            type_name = '.'.join(parts[1:])
        out = {'$type': type_name, 'name': mb['m_Name']}
        for key, v in mb.items():
            if key.startswith('m_'):
                continue
            out[key] = self.value(v)
        return out


NL = chr(10)


def dump(obj, level=0):
    """사람이 손으로 고치기 쉬운 JSON: 값이 전부 단순한 묶음(색, 좌표, 참조)과 숫자 목록은 한 줄에 쓴다."""
    pad, inner = '  ' * level, '  ' * (level + 1)
    scalar = lambda v: not isinstance(v, (dict, list))
    if isinstance(obj, dict):
        if not obj:
            return '{}'
        if level > 0 and all(scalar(v) or (isinstance(v, list) and all(scalar(x) for x in v)) for v in obj.values()):
            return '{ ' + ', '.join(json.dumps(k, ensure_ascii=False) + ': ' + json.dumps(v, ensure_ascii=False) for k, v in obj.items()) + ' }'
        return '{' + NL + (',' + NL).join(inner + json.dumps(k, ensure_ascii=False) + ': ' + dump(v, level + 1) for k, v in obj.items()) + NL + pad + '}'
    if isinstance(obj, list):
        if not obj:
            return '[]'
        if all(scalar(v) for v in obj):
            return json.dumps(obj, ensure_ascii=False)
        return '[' + NL + (',' + NL).join(inner + dump(v, level + 1) for v in obj) + NL + pad + ']'
    return json.dumps(obj, ensure_ascii=False)


def write_json(path, obj):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with io.open(path, 'w', encoding='utf-8', newline=NL) as f:
        f.write(dump(obj) + NL)


def main():
    conv = Converter()
    count = 0
    for src_root, sub in ((os.path.join(ASSETS, 'ScriptableObjects'), ''), (os.path.join(ASSETS, 'Resources'), '')):
        for base, _, files in os.walk(src_root):
            for name in sorted(files):
                if not name.endswith('.asset'):
                    continue
                rel = os.path.relpath(os.path.join(base, name), src_root).replace('\\', '/')
                write_json(os.path.join(OUT_DATA, sub, rel[:-6] + '.json'), conv.asset(os.path.join(base, name)))
                count += 1

    maps = os.path.join(ASSETS, 'Resources', 'Maps')
    map_count = 0
    if os.path.isdir(maps):
        for name in sorted(os.listdir(maps)):
            if name.endswith('.json'):
                os.makedirs(os.path.join(OUT_DATA, 'Maps'), exist_ok=True)
                shutil.copyfile(os.path.join(maps, name), os.path.join(OUT_DATA, 'Maps', name))
                map_count += 1

    png_count = 0
    sprites = os.path.join(ASSETS, 'Sprites')
    for base, _, files in os.walk(sprites):
        for name in files:
            if name.endswith('.png'):
                rel = os.path.relpath(os.path.join(base, name), sprites)
                dst = os.path.join(OUT_SPRITES, rel)
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                shutil.copyfile(os.path.join(base, name), dst)
                png_count += 1

    # 그림 색인: 데이터가 가리키지 않는 그림(배경, 투사체 등)도 경로만으로 크기·PPU·피벗을 알 수 있게
    index = {}
    for base, _, files in os.walk(sprites):
        for name in sorted(files):
            if name.endswith('.png'):
                rel = 'Sprites/' + os.path.relpath(os.path.join(base, name), sprites).replace(os.sep, '/')
                w, h, ppu, pivot, border = sprite_info(rel)
                entry = {'w': w, 'h': h, 'ppu': ppu, 'pivot': [round(pivot[0], 6), round(pivot[1], 6)]}
                if any(border):
                    entry['border'] = border
                index[res_path(rel)] = entry
    write_json(os.path.join(OUT_DATA, 'sprites.json'), dict(sorted(index.items())))

    print('데이터 %d건, 맵 %d건, 그림 %d장' % (count, map_count, png_count))
    if conv.missing:
        print('찾지 못한 참조(비워 둠): %s' % ', '.join(sorted(conv.missing)))
    return 0


if __name__ == '__main__':
    sys.exit(main())
