# Godot 전환 G17: 유니티 main 브랜치의 야전(남코 삼국지2식 부대 전투, Step 15-0~15-8)에서 데이터·씬·그림만 골라 옮긴다.
#
#   git worktree add --detach <폴더> origin/main
#   python tools/godot_port/port_field.py <폴더>
#
# convert_data.py / convert_scenes.py 는 전체를 덮어쓰므로(godot/data 는 이제 직접 고치는 원본) 야전 것만 더한다:
# - Assets/Scenes/FieldBattleScene.unity             -> godot/data/Scenes/FieldBattleScene.scene.json
# - Assets/ScriptableObjects/Field/FieldRules.asset  -> godot/data/Field/FieldRules.json
# - Assets/Sprites/Field/*.png                       -> godot/sprites/Field/, godot/data/sprites.json 에 항목 추가
# - godot/data/project.json 의 씬 순서 끝에 FieldBattleScene
import io, json, os, shutil, sys

sys.path.insert(0, os.path.dirname(__file__))
import convert_data as cd

GODOT_ROOT = cd.ROOT
SRC = os.path.abspath(sys.argv[1])
cd.ROOT = SRC
cd.ASSETS = os.path.join(SRC, 'Assets')
import convert_scenes as cs


def main():
    out_data = os.path.join(GODOT_ROOT, 'godot', 'data')
    out_sprites = os.path.join(GODOT_ROOT, 'godot', 'sprites')
    conv = cs.SceneConverter()

    scene = conv.convert(os.path.join(cd.ASSETS, 'Scenes', 'FieldBattleScene.unity'), 'scene')
    cd.write_json(os.path.join(out_data, 'Scenes', 'FieldBattleScene.scene.json'), scene)
    print('씬: 오브젝트 %d' % len(scene['objects']))

    cd.write_json(os.path.join(out_data, 'Field', 'FieldRules.json'),
                  conv.asset(os.path.join(cd.ASSETS, 'ScriptableObjects', 'Field', 'FieldRules.asset')))

    index_path = os.path.join(out_data, 'sprites.json')
    index = json.load(io.open(index_path, encoding='utf-8'))
    src = os.path.join(cd.ASSETS, 'Sprites', 'Field')
    count = 0
    for name in sorted(os.listdir(src)):
        if not name.endswith('.png'):
            continue
        os.makedirs(os.path.join(out_sprites, 'Field'), exist_ok=True)
        shutil.copyfile(os.path.join(src, name), os.path.join(out_sprites, 'Field', name))
        rel = 'Sprites/Field/' + name
        w, h, ppu, pivot, border, repeat = cd.sprite_info(rel)
        entry = {'w': w, 'h': h, 'ppu': ppu, 'pivot': [round(pivot[0], 6), round(pivot[1], 6)]}
        if any(border):
            entry['border'] = border
        if repeat:
            entry['repeat'] = True
        index[cd.res_path(rel)] = entry
        count += 1
    cd.write_json(index_path, dict(sorted(index.items())))
    print('그림 %d장' % count)

    project_path = os.path.join(out_data, 'project.json')
    project = json.load(io.open(project_path, encoding='utf-8'))
    if 'FieldBattleScene' not in project['scenes']:
        project['scenes'].append('FieldBattleScene')
        cd.write_json(project_path, project)
    print('씬 순서 %s' % project['scenes'])
    if conv.missing:
        print('찾지 못한 참조(비워 둠): %s' % ', '.join(sorted(conv.missing)))
    return 0


if __name__ == '__main__':
    sys.exit(main())
