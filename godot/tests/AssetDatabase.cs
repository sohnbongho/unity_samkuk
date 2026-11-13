using System;
using System.Collections.Generic;
using System.IO;
using Samkuk.Compat;
using Samkuk.Data;

namespace Samkuk.Tests
{
    /// <summary>
    /// 유니티 테스트의 <c>AssetDatabase.LoadAssetAtPath</c> 자리 (Godot 전환 G2). 옮긴 테스트가 예전 에셋 경로를 그대로 쓰도록,
    /// 경로의 파일 이름으로 <see cref="GameData"/>(<c>godot/data</c> 의 JSON)에서 찾아 준다. 그림 경로는 <c>godot/sprites</c> 의 같은 파일.
    /// </summary>
    /// <summary>씬 파일이 있다는 표시 (유니티 SceneAsset 자리).</summary>
    public sealed class SceneAsset { public string name; }

    public static class AssetDatabase
    {
        static string godotRoot;

        /// <summary>테스트 실행 폴더에서 위로 올라가며 찾은 godot/ 폴더.</summary>
        public static string GodotRoot
        {
            get
            {
                if (godotRoot != null) return godotRoot;
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot"))) dir = dir.Parent;
                if (dir == null) throw new InvalidOperationException("godot/project.godot 을 찾지 못했습니다.");
                return godotRoot = dir.FullName;
            }
        }

        /// <summary>실제 데이터(godot/data)를 창고에 올린다. 다른 테스트가 창고를 비웠으면 다시 읽는다.</summary>
        public static void EnsureLoaded()
        {
            if (!GameData.Loaded) GameData.Load(new DirectoryDataSource(Path.Combine(GodotRoot, "data")));
        }

        public static T LoadAssetAtPath<T>(string path) where T : class
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (typeof(T) == typeof(Sprite) || typeof(T) == typeof(Texture2D))
            {
                // "Assets/Sprites/Strategy/StrategyMap.png" -> godot/sprites/Strategy/StrategyMap.png
                int at = path.IndexOf("Sprites/", StringComparison.Ordinal);
                if (at < 0) return null;
                string rel = path.Substring(at + "Sprites/".Length);
                string file = Path.Combine(GodotRoot, "sprites", rel);
                if (!File.Exists(file)) return null;
                ReadPngSize(file, out int w, out int h);
                string res = "res://sprites/" + rel;
                if (typeof(T) == typeof(Texture2D)) return new Texture2D(res, w, h) as T;
                EnsureLoaded();
                return (GameData.SpriteAt(res) ?? Sprite.FromFile(res, w, h, 100f, new Vector2(0.5f, 0.5f))) as T;
            }

            EnsureLoaded();
            if (path.EndsWith(".prefab"))
            {
                // 프리팹: 원형 오브젝트, 또는 그 컴포넌트 (LoadAssetAtPath<Enemy>)
                var prefab = Prefabs.Get(name);
                if (prefab == null) return null;
                return typeof(T) == typeof(GameObject) ? prefab as T : prefab.GetComponent<T>();
            }
            if (path.EndsWith(".unity"))
                return GameData.ReadText("Scenes/" + name + Prefabs.Extension) != null ? new SceneAsset { name = name } as T : null;
            return Resources.Load<T>(name);
        }

        /// <summary>"t:EnemyData" 같은 형식 필터만 지원한다. 돌려주는 값은 이름(유니티에서는 guid)이다.</summary>
        public static string[] FindAssets(string filter, string[] folders = null)
        {
            EnsureLoaded();
            string typeName = filter.StartsWith("t:") ? filter.Substring(2) : filter;
            var names = new List<string>();
            foreach (var o in GameData.All<ScriptableObject>())
                if (o.GetType().Name == typeName) names.Add(o.name);
            return names.ToArray();
        }

        public static string GUIDToAssetPath(string guid) => guid;

        /// <summary>데이터·그림의 예전 에셋 경로 (이름만 맞으면 되는 테스트용).</summary>
        public static string GetAssetPath(Samkuk.Compat.Object asset)
        {
            // Resources 에 있던 카탈로그·테마는 그 경로로
            if (asset is ScriptableObject so)
                return (so is CastleCatalog || so is HeroCatalog || so is TerrainThemeCatalog || so is Samkuk.UI.UiTheme ? "Assets/Resources/" : "Assets/ScriptableObjects/") + so.name + ".asset";
            if (asset is Sprite s && s.path != null) return "Assets/Sprites/" + s.path.Substring("res://sprites/".Length);
            if (asset is Texture2D t && t.path != null) return "Assets/Sprites/" + t.path.Substring("res://sprites/".Length);
            return "";
        }

        static void ReadPngSize(string file, out int width, out int height)
        {
            using var f = File.OpenRead(file);
            var head = new byte[24];
            f.Read(head, 0, 24);
            width = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            height = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
        }
    }
}
