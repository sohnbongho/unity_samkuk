using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Samkuk.Compat;

namespace Samkuk.Data
{
    /// <summary>데이터 JSON 을 읽는 곳. 테스트는 폴더에서, 게임은 Godot 리소스(res://data)에서 읽는다.</summary>
    public interface IDataSource
    {
        /// <summary>모든 JSON 의 상대 경로 (예: "Weapons/Weapon_Bow.json", 구분자는 /).</summary>
        IEnumerable<string> List();
        /// <summary>없으면 null.</summary>
        string Read(string relativePath);
    }

    /// <summary>디스크 폴더에서 읽는다 (테스트, 도구).</summary>
    public sealed class DirectoryDataSource : IDataSource
    {
        readonly string root;
        public DirectoryDataSource(string root) { this.root = root; }

        public IEnumerable<string> List()
        {
            if (!System.IO.Directory.Exists(root)) yield break;
            foreach (string file in System.IO.Directory.GetFiles(root, "*.json", System.IO.SearchOption.AllDirectories))
                yield return System.IO.Path.GetRelativePath(root, file).Replace('\\', '/');
        }

        public string Read(string relativePath)
        {
            string file = System.IO.Path.Combine(root, relativePath);
            return System.IO.File.Exists(file) ? System.IO.File.ReadAllText(file) : null;
        }
    }

    /// <summary>
    /// 게임 데이터 창고 (Godot 전환 G2, ADR 0004). 유니티 때의 ScriptableObject 에셋이 JSON 한 건씩이 되었고(<c>godot/data</c>),
    /// 여기서 한 번에 읽어 데이터 클래스(<see cref="WeaponData"/> 등)로 만든다. 이름 = 파일 이름 = 예전 에셋 이름.
    ///
    /// JSON 규칙: <c>$type</c>(클래스 전체 이름) + <c>name</c> + 필드. 다른 데이터는 <c>{"$ref": 이름}</c>,
    /// 그림은 <c>{"$sprite": 경로, w, h, ppu, pivot}</c> / <c>{"$texture": 경로, w, h}</c>, 빈 참조는 null.
    /// 클래스에 없는 항목은 무시하고 JSON 에 없는 필드는 클래스의 기본값을 둔다(필드를 더해도 예전 파일이 읽힌다).
    /// 읽고 나면 <see cref="Resources.Load{T}"/> 도 이 창고에서 찾는다.
    /// </summary>
    public static class GameData
    {
        public const string MapsFolder = "Maps/";

        static readonly Dictionary<string, ScriptableObject> byName = new Dictionary<string, ScriptableObject>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static IDataSource source;

        public static bool Loaded => source != null;
        /// <summary>씬·프리팹 파일을 읽는 동안 같은 파일 안의 객체/프리팹 참조를 풀어 주는 곳 (<see cref="SceneFile"/> 이 꽂는다).</summary>
        internal static Func<JsonElement, Type, string, object> ObjectResolver;
        /// <summary>데이터 폴더의 글 파일 하나 (씬·프리팹·맵). 없으면 null.</summary>
        public static string ReadText(string relativePath) => source?.Read(relativePath);
        public static int Count => byName.Count;
        /// <summary>읽다가 생긴 문제 (모르는 형식, 없는 참조). 비어 있어야 정상이며 테스트가 확인한다.</summary>
        public static readonly List<string> Problems = new List<string>();

        /// <summary>모든 데이터를 읽는다 (이전 내용은 버린다). 두 번에 나눠 읽어 서로 참조해도 순서와 무관하다.</summary>
        public static void Load(IDataSource from)
        {
            Clear();
            source = from;
            var pending = new List<(ScriptableObject target, JsonDocument doc)>();
            foreach (string path in from.List())
            {
                if (path.StartsWith(MapsFolder, StringComparison.OrdinalIgnoreCase)) continue;   // 맵 파일은 MapStore 가 읽는다
                string text = from.Read(path);
                if (string.IsNullOrWhiteSpace(text)) continue;
                JsonDocument doc;
                try { doc = JsonDocument.Parse(text); }
                catch (JsonException e) { Problems.Add($"{path}: JSON 오류 {e.Message}"); continue; }

                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("$type", out var typeName)) continue;
                var type = typeof(GameData).Assembly.GetType(typeName.GetString() ?? "");
                if (type == null || !typeof(ScriptableObject).IsAssignableFrom(type))
                {
                    Problems.Add($"{path}: 모르는 형식 {typeName.GetString()}");   // 아직 옮기지 않은 클래스 (예: UiTheme)
                    continue;
                }
                var target = (ScriptableObject)Activator.CreateInstance(type);
                target.name = root.TryGetProperty("name", out var n) ? n.GetString() : System.IO.Path.GetFileNameWithoutExtension(path);
                if (byName.ContainsKey(target.name)) Problems.Add($"{path}: 이름이 겹친다 {target.name}");
                byName[target.name] = target;
                pending.Add((target, doc));
            }

            foreach (var (target, doc) in pending)
            {
                Fill(target, doc.RootElement, target.name);
                doc.Dispose();
            }

            Resources.Loader = LoadResource;
            LoadProject(from);
            LoadSpriteIndex(from);
        }

        static void LoadSpriteIndex(IDataSource from)
        {
            string text = from.Read("sprites.json");
            if (text == null) return;
            using var doc = JsonDocument.Parse(text);
            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                if (sprites.ContainsKey(entry.Name)) continue;
                using var one = JsonDocument.Parse("{\"$sprite\":" + JsonSerializer.Serialize(entry.Name) + "," + entry.Value.GetRawText().TrimStart().Substring(1));
                ToSprite(one.RootElement, entry.Name);
            }
        }

        /// <summary>그림 파일 경로(res://sprites/...)의 스프라이트 손잡이. 색인에 없으면 null.</summary>
        public static Sprite SpriteAt(string resPath) => resPath != null && sprites.TryGetValue(resPath, out var s) ? s : null;

        /// <summary>프로젝트 설정(레이어 이름, 정렬 레이어, 씬 순서)을 읽고 씬 파일을 씬 전환에 등록한다.</summary>
        static void LoadProject(IDataSource from)
        {
            string text = from.Read("project.json");
            if (text == null) return;
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("layers", out var layers))
            {
                LayerMask.Names.Clear();
                foreach (var l in layers.EnumerateArray()) LayerMask.Names.Add(l.GetString() ?? "");
            }
            if (root.TryGetProperty("sortingLayers", out var sorting))
            {
                SortingLayer.Names.Clear();
                foreach (var s in sorting.EnumerateArray()) SortingLayer.Names.Add(s.GetString());
            }
            if (root.TryGetProperty("scenes", out var scenes))
                foreach (var s in scenes.EnumerateArray())
                {
                    string name = s.GetString();
                    SceneManager.Register(name, () =>
                    {
                        string json = ReadText("Scenes/" + name + Prefabs.Extension);
                        if (json != null) SceneFile.LoadScene(json);
                        else Debug.LogWarning($"[Samkuk] 씬 파일이 없습니다: {name}");
                    });
                }
        }

        public static void Clear()
        {
            Prefabs.Clear();
            byName.Clear();
            sprites.Clear();
            textures.Clear();
            Problems.Clear();
            source = null;
            if (Resources.Loader == LoadResource) Resources.Loader = null;
        }

        /// <summary>이름으로 찾는다. 없거나 형식이 다르면 null.</summary>
        public static T Get<T>(string name) where T : ScriptableObject
            => name != null && byName.TryGetValue(name, out var found) ? found as T : null;

        /// <summary>한 형식의 모든 데이터 (이름 순).</summary>
        public static List<T> All<T>() where T : ScriptableObject
        {
            var list = new List<T>();
            foreach (var o in byName.Values) if (o is T t) list.Add(t);
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
        }

        // Resources.Load 창구: 데이터는 이름으로, 글 파일(맵)은 경로로
        static object LoadResource(Type type, string name)
        {
            if (type == typeof(TextAsset))
            {
                string text = source?.Read(name + ".json");
                return text != null ? new TextAsset(text) : null;
            }
            return byName.TryGetValue(name, out var found) && type.IsInstanceOfType(found) ? found : null;
        }

        static void Fill(object target, JsonElement obj, string where)
        {
            var type = target.GetType();
            foreach (var prop in obj.EnumerateObject())
            {
                if (prop.Name.StartsWith("$") || prop.Name == "name" && target is ScriptableObject) continue;
                // 이름 그대로, 없으면 유니티 내장 표기(m_NormalColor → normalColor)로. 직렬화 표시가 붙은 private 필드도 채운다
                var field = SceneFile.FindField(type, prop.Name);
                if (field == null || field.IsInitOnly || field.IsLiteral) continue;
                if (!field.IsPublic && !field.IsDefined(typeof(SerializeFieldAttribute))) continue;
                try { field.SetValue(target, Convert(prop.Value, field.FieldType, where + "." + prop.Name)); }
                catch (Exception e) { Problems.Add($"{where}.{prop.Name}: {e.Message}"); }
            }
        }

        internal static object Convert(JsonElement v, Type type, string where)
        {
            if (v.ValueKind == JsonValueKind.Null)
                return type.IsValueType ? Activator.CreateInstance(type) : null;

            // 장면 객체(게임 오브젝트, 컴포넌트)나 인터페이스를 가리키는 필드: 씬 파일 안의 참조
            if (v.ValueKind == JsonValueKind.Object && (v.TryGetProperty("$id", out _) || v.TryGetProperty("$prefab", out _)))
                return ObjectResolver != null ? ObjectResolver(v, type, where) : null;
            if (typeof(Compat.Object).IsAssignableFrom(type) && !typeof(ScriptableObject).IsAssignableFrom(type) && type != typeof(Sprite) && type != typeof(Texture2D))
                return null;   // 옮기지 않은 종류의 참조 (재질, 글꼴): 기본값을 쓴다

            if (type == typeof(string)) return v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText();
            if (type == typeof(bool)) return v.ValueKind == JsonValueKind.Number ? v.GetDouble() != 0 : v.GetBoolean();   // 유니티 YAML 의 bool 은 0/1
            if (type == typeof(int)) return v.GetInt32();
            if (type == typeof(float)) return v.GetSingle();
            if (type == typeof(double)) return v.GetDouble();
            if (type == typeof(byte)) return v.GetByte();
            if (type == typeof(long)) return v.GetInt64();
            if (type.IsEnum)
                return v.ValueKind == JsonValueKind.String ? Enum.Parse(type, v.GetString()) : Enum.ToObject(type, v.GetInt32());

            if (type == typeof(Sprite)) return ToSprite(v, where);
            if (type == typeof(Texture2D)) return ToTexture(v, where);
            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                string name = v.TryGetProperty("$ref", out var r) ? r.GetString() : null;
                if (name != null && byName.TryGetValue(name, out var found) && type.IsInstanceOfType(found)) return found;
                Problems.Add($"{where}: 없는 참조 {name}");
                return null;
            }

            if (type.IsArray)
            {
                var elem = type.GetElementType();
                var array = Array.CreateInstance(elem, v.GetArrayLength());
                int i = 0;
                foreach (var item in v.EnumerateArray()) array.SetValue(Convert(item, elem, $"{where}[{i}]"), i++);
                return array;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elem = type.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(type);
                int i = 0;
                foreach (var item in v.EnumerateArray()) list.Add(Convert(item, elem, $"{where}[{i++}]"));
                return list;
            }

            // 색·좌표·묶음 클래스(웨이브, 소품 등): public 필드를 이름으로 채운다. 구조체는 상자에 담긴 채 채워진다
            object boxed = Activator.CreateInstance(type);
            if (v.ValueKind == JsonValueKind.Object) Fill(boxed, v, where);
            return boxed;
        }

        static Sprite ToSprite(JsonElement v, string where)
        {
            if (!v.TryGetProperty("$sprite", out var p)) { Problems.Add($"{where}: 스프라이트가 아니다"); return null; }
            string path = p.GetString();
            if (sprites.TryGetValue(path, out var cached)) return cached;

            int w = v.TryGetProperty("w", out var jw) ? jw.GetInt32() : 0, h = v.TryGetProperty("h", out var jh) ? jh.GetInt32() : 0;
            float ppu = v.TryGetProperty("ppu", out var jp) ? jp.GetSingle() : 100f;
            var pivot = new Vector2(0.5f, 0.5f);
            if (v.TryGetProperty("pivot", out var jv) && jv.GetArrayLength() == 2) pivot = new Vector2(jv[0].GetSingle(), jv[1].GetSingle());
            var sprite = Sprite.FromFile(path, w, h, ppu, pivot);
            if (v.TryGetProperty("border", out var jb) && jb.GetArrayLength() == 4)
                sprite.border = new Vector4(jb[0].GetSingle(), jb[1].GetSingle(), jb[2].GetSingle(), jb[3].GetSingle());
            sprite.texture.filterMode = FilterMode.Point;   // 파일 그림은 모두 도트 규격 (PixelArt)
            if (v.TryGetProperty("repeat", out var jr) && jr.ValueKind == JsonValueKind.True) sprite.texture.wrapMode = TextureWrapMode.Repeat;   // 바닥 타일
            sprites[path] = sprite;
            return sprite;
        }

        static Texture2D ToTexture(JsonElement v, string where)
        {
            if (!v.TryGetProperty("$texture", out var p)) { Problems.Add($"{where}: 텍스처가 아니다"); return null; }
            string path = p.GetString();
            if (textures.TryGetValue(path, out var cached)) return cached;
            var tex = new Texture2D(path, v.TryGetProperty("w", out var jw) ? jw.GetInt32() : 0, v.TryGetProperty("h", out var jh) ? jh.GetInt32() : 0) { filterMode = FilterMode.Point };
            textures[path] = tex;
            return tex;
        }
    }
}
