using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Samkuk.Compat;
using Object = Samkuk.Compat.Object;

namespace Samkuk.Data
{
    /// <summary>
    /// 씬·프리팹 파일(<c>godot/data/Scenes|Prefabs/*.scene.json</c>)을 장면 모형의 오브젝트로 만든다 (Godot 전환 G3).
    /// 유니티의 .unity/.prefab 을 <c>tools/godot_port/convert_scenes.py</c> 가 옮긴 것이라 오브젝트·컴포넌트·저장된 값이 원본과 같다.
    /// 필드 이름은 유니티가 저장한 그대로(내장 컴포넌트는 m_ 접두사)이고, 여기서 장면 모형의 필드에 맞춰 넣는다.
    /// </summary>
    public static class SceneFile
    {
        /// <summary>읽다가 생긴 문제 (모르는 컴포넌트, 없는 참조). 옮기지 않기로 한 것은 <see cref="Ignored"/> 로 걸러진다.</summary>
        public static readonly List<string> Problems = new List<string>();

        /// <summary>일부러 만들지 않는 컴포넌트: 에디터 전용 입력 설정.</summary>
        static readonly HashSet<string> Ignored = new HashSet<string>
        {
            "", "UnityEngine.InputSystem.InputActionReference", "UnityEngine.InputSystem.InputActionAsset", "CanvasRenderer",
        };

        static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();
        static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        sealed class Built
        {
            public readonly Dictionary<string, Object> byId = new Dictionary<string, Object>();
            public readonly List<GameObject> roots = new List<GameObject>();
            public readonly List<GameObject> all = new List<GameObject>();
        }

        /// <summary>씬 파일의 오브젝트를 장면에 만든다 (다 만든 뒤 한꺼번에 깨운다).</summary>
        public static List<GameObject> LoadScene(string json)
        {
            var built = Build(json, false);
            // 켜진 것부터 유니티처럼: 모든 오브젝트가 준비된 다음 Awake → OnEnable
            foreach (var go in built.all)
                foreach (var c in go.components.ToArray())
                    if (c is MonoBehaviour mb && go.activeInHierarchy) mb.WakeUp();
            return built.roots;
        }

        /// <summary>프리팹 파일로 원형(장면에 없는 오브젝트)을 만든다. id → 원형 안의 객체 표도 돌려준다 (씬의 프리팹 인스턴스가 고친 값을 찾는 데 쓴다).</summary>
        public static GameObject LoadPrefab(string json, out Dictionary<string, Object> ids)
        {
            var built = Build(json, true);
            ids = built.byId;
            return built.roots.Count > 0 ? built.roots[0] : null;
        }

        static Built Build(string json, bool asPrefab)
        {
            var built = new Built();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var pending = new List<(Component comp, JsonElement data, string where)>();
            var fathers = new List<(Transform parent, JsonElement children)>();

            SceneRuntime.BeginBuild();
            var previousResolver = GameData.ObjectResolver;
            GameData.ObjectResolver = (v, type, where) => Resolve(built, v, type, where);
            try
            {
                // 1) 오브젝트와 컴포넌트 (값은 아직)
                foreach (var o in root.GetProperty("objects").EnumerateArray())
                {
                    string name = o.GetProperty("name").GetString();
                    var go = asPrefab ? GameObject.CreatePrefab(name) : new GameObject(name);
                    go.layer = o.GetProperty("layer").GetInt32();
                    go.tag = o.GetProperty("tag").GetString();
                    go.active = o.GetProperty("active").GetBoolean();
                    built.byId[o.GetProperty("id").GetString()] = go;
                    built.all.Add(go);

                    var claimed = new HashSet<Component>();
                    foreach (var c in o.GetProperty("components").EnumerateArray())
                    {
                        string cls = c.GetProperty("class").GetString();
                        string where = name + "/" + cls;
                        Component comp;
                        if (cls == "Transform" || cls == "RectTransform") comp = go.transform;
                        else
                        {
                            var type = TypeOf(cls);
                            if (type == null)
                            {
                                if (!Ignored.Contains(cls)) Problems.Add($"{where}: 모르는 컴포넌트");
                                continue;
                            }
                            // RequireComponent 로 이미 붙은 것이 있으면 그것을 쓴다
                            comp = null;
                            foreach (var existing in go.components)
                                if (existing.GetType() == type && !claimed.Contains(existing)) { comp = existing; break; }
                            comp ??= go.AddComponent(type);
                        }
                        claimed.Add(comp);
                        built.byId[c.GetProperty("id").GetString()] = comp;
                        pending.Add((comp, c.GetProperty("data"), where));
                    }
                }

                // 2) 프리팹 인스턴스: 원형을 복제하고 고친 값을 얹는다
                var instanceParents = new List<(GameObject go, string parentId)>();
                if (root.TryGetProperty("prefabInstances", out var instances))
                    foreach (var inst in instances.EnumerateArray())
                    {
                        string prefabName = inst.GetProperty("prefab").GetString();
                        var template = Prefabs.Get(prefabName, out var templateIds);
                        if (template == null) { Problems.Add($"프리팹 인스턴스: 원형이 없다 {prefabName}"); continue; }
                        var copy = SceneRuntime.CloneWithMap(template, out var map);
                        copy.name = template.name;   // "(Clone)" 을 붙이지 않는다 (씬에 놓인 인스턴스)
                        built.byId[inst.GetProperty("id").GetString()] = copy;
                        built.all.AddRange(Descendants(copy));

                        foreach (var s in inst.GetProperty("stripped").EnumerateObject())
                            if (templateIds.TryGetValue(s.Value.GetString(), out var source) && map.TryGetValue(source, out var mapped)) built.byId[s.Name] = mapped;
                        foreach (var m in inst.GetProperty("mods").EnumerateArray())
                        {
                            if (!templateIds.TryGetValue(m.GetProperty("target").GetString(), out var source) || !map.TryGetValue(source, out var target)) continue;
                            ApplyPath(target, m.GetProperty("path").GetString(), m.GetProperty("value"), prefabName);
                        }
                        string parentId = inst.GetProperty("parent").ValueKind == JsonValueKind.String ? inst.GetProperty("parent").GetString() : null;
                        instanceParents.Add((copy, parentId));
                    }

                // 3) 부모/자식 (자식 순서는 부모가 적어 둔 순서)
                foreach (var (comp, data, where) in pending)
                    if (comp is Transform t && data.TryGetProperty("m_Children", out var children) && children.ValueKind == JsonValueKind.Array)
                        fathers.Add((t, children));
                foreach (var (parent, children) in fathers)
                    foreach (var child in children.EnumerateArray())
                    {
                        if (child.ValueKind != JsonValueKind.Object || !child.TryGetProperty("$id", out var id)) continue;
                        if (built.byId.TryGetValue(id.GetString(), out var found))
                        {
                            var ct = found as Transform ?? (found as GameObject)?.transform;
                            ct?.SetParent(parent, false);
                        }
                    }
                foreach (var (go, parentId) in instanceParents)
                    if (parentId != null && built.byId.TryGetValue(parentId, out var p) && p is Transform pt && go.transform.parent == null) go.transform.SetParent(pt, false);

                // 4) 값
                foreach (var (comp, data, where) in pending)
                    foreach (var prop in data.EnumerateObject())
                        ApplyField(comp, prop.Name, prop.Value, where);

                // 5) 루트 순서
                foreach (var r in root.GetProperty("roots").EnumerateArray())
                    if (built.byId.TryGetValue(r.GetString(), out var found) && found is GameObject go) built.roots.Add(go);
            }
            finally
            {
                GameData.ObjectResolver = previousResolver;
                SceneRuntime.EndBuild();
            }
            return built;
        }

        static IEnumerable<GameObject> Descendants(GameObject go)
        {
            yield return go;
            for (int i = 0; i < go.transform.childCount; i++)
                foreach (var d in Descendants(go.transform.GetChild(i).gameObject)) yield return d;
        }

        static Type TypeOf(string cls)
        {
            if (typeCache.TryGetValue(cls, out var cached)) return cached;
            Type found = null;
            if (!Ignored.Contains(cls))
            {
                var asm = typeof(SceneFile).Assembly;
                found = asm.GetType(cls);
                if (found == null)
                {
                    // 유니티/패키지 클래스: 같은 이름의 호환 층 컴포넌트
                    string simple = cls.Substring(cls.LastIndexOf('.') + 1);
                    foreach (var t in asm.GetTypes())
                        if (t.Name == simple && t.Namespace != null && t.Namespace.StartsWith("Samkuk.Compat") && typeof(Component).IsAssignableFrom(t)) { found = t; break; }
                }
                if (found != null && !typeof(Component).IsAssignableFrom(found)) found = null;
            }
            typeCache[cls] = found;
            return found;
        }

        // 같은 파일 안의 객체({"$id"}) 또는 프리팹({"$prefab"}) 참조를 필드 형식에 맞춰 준다
        static object Resolve(Built built, JsonElement v, Type type, string where)
        {
            Object found = null;
            if (v.TryGetProperty("$id", out var id)) built.byId.TryGetValue(id.GetString(), out found);
            else if (v.TryGetProperty("$prefab", out var prefab)) found = Prefabs.Get(prefab.GetString(), out _);
            if (found is null) return null;
            if (type.IsInstanceOfType(found)) return found;
            var go = found as GameObject ?? (found as Component)?.gameObject;
            if (go == null) return null;
            if (type == typeof(GameObject)) return go;
            return go.GetComponent(type);   // 프리팹 참조가 컴포넌트 형식(Enemy enemyPrefab)이거나 Transform ↔ GameObject
        }

        static string Normalize(string key)
        {
            switch (key)
            {
                case "orthographic size": return "orthographicSize";
                case "near clip plane": return "nearClipPlane";
                case "far clip plane": return "farClipPlane";
                case "m_BackGroundColor": return "backgroundColor";
            }
            if (key.StartsWith("m_") && key.Length > 2) return char.ToLowerInvariant(key[2]) + key.Substring(3);
            return key;
        }

        internal static FieldInfo FindField(Type type, string key)
        {
            string alt = Normalize(key);
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                var f = t.GetField(key, Fields) ?? (alt != key ? t.GetField(alt, Fields) : null);
                if (f != null) return f;
            }
            return null;
        }

        static void ApplyField(Component comp, string key, JsonElement value, string where)
        {
            if (key == "m_Father" || key == "m_Children" || key == "m_Materials") return;
            if (key == "m_Enabled" && comp is Behaviour b) { b.enabledFlag = value.ValueKind == JsonValueKind.Number ? value.GetDouble() != 0 : value.GetBoolean(); return; }
            var field = FindField(comp.GetType(), key);
            if (field == null || field.IsInitOnly || field.IsLiteral) return;   // 장면 모형이 쓰지 않는 값 (물리 세부 설정 등)
            try { field.SetValue(comp, GameData.Convert(value, field.FieldType, where + "." + key)); }
            catch (Exception e) { Problems.Add($"{where}.{key}: {e.Message}"); }
        }

        // 프리팹 인스턴스가 고친 값: "m_Name", "m_LocalPosition.x", "field", "field.sub"
        static void ApplyPath(Object target, string path, JsonElement value, string where)
        {
            if (target is GameObject go)
            {
                if (path == "m_Name") go.name = value.ToString();
                else if (path == "m_IsActive") go.active = value.ToString() != "0";
                else if (path == "m_Layer" && int.TryParse(value.ToString(), out int layer)) go.layer = layer;
                else if (path == "m_TagString") go.tag = value.ToString();
                return;
            }
            if (path.Contains(".Array.")) { Problems.Add($"{where}: 목록을 고친 값은 옮기지 못했다 ({path})"); return; }
            string[] parts = path.Split('.');
            if (parts[0] == "m_LocalEulerAnglesHint") return;
            var field = FindField(target.GetType(), parts[0]);
            if (field == null) return;
            try
            {
                if (parts.Length == 1) { field.SetValue(target, GameData.Convert(Unquote(value, field.FieldType), field.FieldType, where + "." + path)); return; }
                // 구조체의 한 성분 (x, y, z, w, r, g, b, a)
                object boxed = field.GetValue(target);
                var sub = boxed?.GetType().GetField(parts[1], BindingFlags.Instance | BindingFlags.Public);
                if (sub == null) return;
                sub.SetValue(boxed, GameData.Convert(Unquote(value, sub.FieldType), sub.FieldType, where + "." + path));
                field.SetValue(target, boxed);
            }
            catch (Exception e) { Problems.Add($"{where}.{path}: {e.Message}"); }
        }

        // 고친 값은 유니티가 글자로 저장하기도 한다 ("1", "0.5"): 숫자 필드면 숫자로 다시 읽는다
        static JsonElement Unquote(JsonElement value, Type type)
        {
            if (value.ValueKind != JsonValueKind.String || type == typeof(string)) return value;
            string s = value.GetString();
            if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                using (var d = JsonDocument.Parse(s)) return d.RootElement.Clone();
            return value;
        }
    }

    /// <summary>프리팹 원형 창고: 이름으로 한 번만 만들어 두고 <see cref="Object.Instantiate{T}(T)"/> 로 복제해 쓴다.</summary>
    public static class Prefabs
    {
        public const string Folder = "Prefabs/";
        public const string Extension = ".scene.json";
        static readonly Dictionary<string, (GameObject root, Dictionary<string, Object> ids)> cache = new Dictionary<string, (GameObject, Dictionary<string, Object>)>();

        public static GameObject Get(string name) => Get(name, out _);

        public static GameObject Get(string name, out Dictionary<string, Object> ids)
        {
            ids = null;
            if (string.IsNullOrEmpty(name)) return null;
            if (cache.TryGetValue(name, out var cached) && cached.root != null) { ids = cached.ids; return cached.root; }
            string json = GameData.ReadText(Folder + name + Extension);
            if (json == null) return null;
            var root = SceneFile.LoadPrefab(json, out ids);
            cache[name] = (root, ids);
            return root;
        }

        /// <summary>원형의 컴포넌트 (예: <c>Prefabs.Get&lt;Enemy&gt;("Enemy")</c>).</summary>
        public static T Get<T>(string name) where T : class => Get(name)?.GetComponent<T>();

        public static void Clear() => cache.Clear();
    }
}
