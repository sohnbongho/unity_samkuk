# Godot 전환 G3 (ADR 0004): 유니티 프리팹(.prefab)과 씬(.unity)을 장면 모형이 읽는 JSON 으로 옮긴다.
#
#   python tools/godot_port/convert_scenes.py
#
# - Assets/Prefabs/*.prefab  ->  godot/data/Prefabs/<이름>.scene.json
# - Assets/Scenes/*.unity    ->  godot/data/Scenes/<이름>.scene.json
# - ProjectSettings          ->  godot/data/project.json (레이어·정렬 레이어 이름, 씬 순서)
#
# 씬/프리팹 JSON: {"kind", "name", "roots": [id...], "objects": [{id, name, layer, tag, active, components: [{id, class, data}]}],
#                 "prefabInstances": [{id, prefab, parent, mods: [{target, path, value}], stripped: {씬 id: 프리팹 id}}]}
#   class 는 내장 컴포넌트면 유니티 클래스 이름(Transform, SpriteRenderer ...), 스크립트면 전체 이름(Samkuk.Enemies.Enemy, UnityEngine.UI.Text).
#   data 는 유니티가 저장한 필드 그대로(m_ 접두사 포함)이고, 참조만 바꾼다:
#     같은 파일 안의 객체 {"$id": "123"} / 프리팹 {"$prefab": "Enemy"} / 데이터 {"$ref": ...} / 그림 {"$sprite": ...}, {"$texture": ...}
#
# convert_data.py 와 같이 유니티 원본이 있는 동안만 쓰는 도구이고, 다시 돌리면 덮어쓴다.
import io, os, re, sys

import yaml

sys.path.insert(0, os.path.dirname(__file__))
import convert_data as cd

DOC = re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?[ \t]*\r?$', re.M)
CLASS_NAMES = {1: 'GameObject', 4: 'Transform', 224: 'RectTransform', 212: 'SpriteRenderer', 20: 'Camera', 50: 'Rigidbody2D', 58: 'CircleCollider2D',
               223: 'Canvas', 222: 'CanvasRenderer', 81: 'AudioListener', 114: 'MonoBehaviour', 1001: 'PrefabInstance', 225: 'CanvasGroup',
               108: 'Light', 33: 'MeshFilter', 23: 'MeshRenderer', 82: 'AudioSource', 102: 'TextMesh'}
SKIP_CLASSES = {29, 104, 157, 196, 1660057539, 222}   # 씬 설정(오클루전, 렌더 설정, 라이트맵, 내비), 씬 루트 목록(따로 읽음), CanvasRenderer


def split_docs(text):
    marks = list(DOC.finditer(text))
    docs = []
    for i, m in enumerate(marks):
        end = marks[i + 1].start() if i + 1 < len(marks) else len(text)
        body = yaml.safe_load(text[m.end():end]) or {}
        key = next(iter(body)) if body else None
        docs.append((int(m.group(1)), m.group(2), bool(m.group(3)), body.get(key) if key else {}))
    return docs


class SceneConverter(cd.Converter):
    def __init__(self):
        super().__init__()
        tags = cd.load_unity_yaml(os.path.join(cd.ROOT, 'ProjectSettings', 'TagManager.asset'))['TagManager']
        self.layers = [l or '' for l in tags['layers']]
        self.sorting = {int(s['uniqueID']): s['name'] for s in tags['m_SortingLayers']}
        self.sorting_names = [s['name'] for s in tags['m_SortingLayers']]

    def ref(self, node):
        file_id, guid = node.get('fileID', 0), node.get('guid')
        if not file_id:
            return None
        if not guid:
            return {'$id': str(file_id)}
        rel = self.guids.get(guid)
        if rel is not None and rel.endswith('.prefab'):
            return {'$prefab': os.path.splitext(os.path.basename(rel))[0], 'id': str(file_id)}
        if rel is None:
            return None   # 패키지·내장 리소스(기본 글꼴, 기본 재질, 입력 설정): 장면 모형이 기본값을 쓴다
        return super().ref(node)

    def component(self, class_id, doc_id, body):
        if class_id == 114:
            ident = str(body.get('m_EditorClassIdentifier') or '')
            name = ident.split('::', 1)[1] if '::' in ident else ''
            if not name:
                script = self.guids.get((body.get('m_Script') or {}).get('guid'))
                name = os.path.splitext(os.path.basename(script))[0] if script else ''
            cls = name
        else:
            cls = CLASS_NAMES.get(class_id, 'Unity%d' % class_id)
        data = {}
        for key, v in body.items():
            if key in ('m_ObjectHideFlags', 'm_CorrespondingSourceObject', 'm_PrefabInstance', 'm_PrefabAsset', 'm_GameObject', 'm_Script',
                       'm_EditorClassIdentifier', 'm_EditorHideFlags', 'm_Name', 'serializedVersion'):
                continue
            data[key] = self.value(v)
        # 글자 설정 묶음을 펼친다 (장면 모형의 Text 는 값들을 바로 든다)
        font = data.pop('m_FontData', None)
        if isinstance(font, dict):
            rename = {'m_RichText': 'supportRichText', 'm_BestFit': 'resizeTextForBestFit', 'm_MinSize': 'resizeTextMinSize', 'm_MaxSize': 'resizeTextMaxSize'}
            for k, v in font.items():
                data[rename.get(k, k)] = v
        if 'm_SortingLayerID' in data:
            data['sortingLayerName'] = self.sorting.get(int(data['m_SortingLayerID']), 'Default')
        return {'id': doc_id, 'class': cls, 'data': data}

    def convert(self, path, kind):
        docs = split_docs(io.open(path, encoding='utf-8').read())
        by_id = {doc_id: (class_id, stripped, body) for class_id, doc_id, stripped, body in docs}
        objects, instances, root_order, stripped_of = [], {}, None, {}
        for class_id, doc_id, stripped, body in docs:
            if class_id == 1660057539:
                root_order = [str(r['fileID']) for r in body.get('m_Roots', [])]
            if class_id == 1001:
                mod = body.get('m_Modification', {})
                source = self.guids.get((body.get('m_SourcePrefab') or {}).get('guid'))
                parent = (mod.get('m_TransformParent') or {}).get('fileID', 0)
                mods = []
                for m in mod.get('m_Modifications', []):
                    ref = self.value(m.get('objectReference') or {'fileID': 0})
                    mods.append({'target': str(m['target']['fileID']), 'path': m['propertyPath'], 'value': ref if ref is not None else m.get('value')})
                instances[doc_id] = {'id': doc_id, 'prefab': os.path.splitext(os.path.basename(source))[0] if source else None,
                                     'parent': str(parent) if parent else None, 'mods': mods, 'stripped': {}}
            if stripped:
                inst = str((body.get('m_PrefabInstance') or {}).get('fileID', 0))
                stripped_of[doc_id] = (inst, str((body.get('m_CorrespondingSourceObject') or {}).get('fileID', 0)))
        for doc_id, (inst, source) in stripped_of.items():
            if inst in instances:
                instances[inst]['stripped'][doc_id] = source

        for class_id, doc_id, stripped, body in docs:
            if class_id != 1 or stripped:
                continue
            comps = []
            for c in body.get('m_Component', []):
                cid = str(c['component']['fileID'])
                if cid not in by_id:
                    continue
                ccls, cstripped, cbody = by_id[cid]
                if cstripped or ccls in SKIP_CLASSES:
                    continue
                comps.append(self.component(ccls, cid, cbody))
            objects.append({'id': doc_id, 'name': body.get('m_Name', ''), 'layer': body.get('m_Layer', 0), 'tag': body.get('m_TagString', 'Untagged'),
                            'active': bool(body.get('m_IsActive', 1)), 'components': comps})

        # 루트 순서: 씬은 SceneRoots 가 준 순서(트랜스폼/프리팹 인스턴스 id), 프리팹은 부모 없는 것
        transform_owner = {}
        for o in objects:
            for c in o['components']:
                if c['class'] in ('Transform', 'RectTransform'):
                    transform_owner[c['id']] = o['id']
        roots = []
        if root_order:
            for r in root_order:
                roots.append(transform_owner.get(r, r))   # 프리팹 인스턴스는 그 id 그대로
        else:
            for o in objects:
                t = next((c for c in o['components'] if c['class'] in ('Transform', 'RectTransform')), None)
                if t is not None and not t['data'].get('m_Father'):
                    roots.append(o['id'])
        return {'kind': kind, 'name': os.path.splitext(os.path.basename(path))[0], 'roots': roots, 'objects': objects,
                'prefabInstances': list(instances.values())}


def main():
    conv = SceneConverter()
    count = 0
    for folder, kind, out in (('Prefabs', 'prefab', 'Prefabs'), ('Scenes', 'scene', 'Scenes')):
        src = os.path.join(cd.ASSETS, folder)
        for name in sorted(os.listdir(src)):
            if not name.endswith(('.prefab', '.unity')):
                continue
            result = conv.convert(os.path.join(src, name), kind)
            cd.write_json(os.path.join(cd.OUT_DATA, out, os.path.splitext(name)[0] + '.scene.json'), result)
            count += 1
            print('%s: 오브젝트 %d, 프리팹 인스턴스 %d' % (name, len(result['objects']), len(result['prefabInstances'])))

    build = cd.load_unity_yaml(os.path.join(cd.ROOT, 'ProjectSettings', 'EditorBuildSettings.asset'))['EditorBuildSettings']
    scenes = [os.path.splitext(os.path.basename(s['path']))[0] for s in build.get('m_Scenes', []) if s.get('enabled', 1)]
    cd.write_json(os.path.join(cd.OUT_DATA, 'project.json'), {'layers': conv.layers, 'sortingLayers': conv.sorting_names, 'scenes': scenes})
    print('씬·프리팹 %d건, 씬 순서 %s' % (count, scenes))
    if conv.missing:
        print('찾지 못한 참조(비워 둠): %d건' % len(conv.missing))
    return 0


if __name__ == '__main__':
    sys.exit(main())
