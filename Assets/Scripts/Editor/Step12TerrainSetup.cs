using System;
using System.IO;
using System.Text;
using Samkuk.Data;
using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 12-6: 전투 맵의 지형 테마 만들기. <c>tools/terrain_art/terrain.json</c>(기준표)을 읽어
    /// <c>Assets/ScriptableObjects/Terrain/Theme_&lt;지형&gt;.asset</c> 6개와 <c>Assets/Resources/TerrainThemeCatalog.asset</c> 을 채우고,
    /// <c>Assets/Sprites/Terrain/</c> 의 바닥 타일과 소품 그림을 연결한다.
    /// 멱등: 이미 있는 테마의 값(밀도, 소품 비중 등)은 덮어쓰지 않고, 빠진 소품/그림만 채운다.
    /// 그림은 <c>tools/terrain_art/generate.ps1</c> 로 만든다. 규칙은 docs/TERRAIN.md.
    /// </summary>
    public static class Step12TerrainSetup
    {
        public const string SpriteDir = "Assets/Sprites/Terrain";
        const string ThemeDir = "Assets/ScriptableObjects/Terrain";
        const string CatalogPath = "Assets/Resources/TerrainThemeCatalog.asset";
        const string TablePath = "tools/terrain_art/terrain.json";

        [Serializable]
        class PropRow
        {
            public string file, kind;
            public float weight = 1f, scaleMin = 1f, scaleMax = 1f;
        }

        [Serializable]
        class GroundRow { public string file; }

        [Serializable]
        class RiverRow
        {
            public string file;
            public float width = 3.6f;
        }

        [Serializable]
        class ThemeRow
        {
            public string terrain;
            public float density;
            public GroundRow ground;
            public RiverRow river;
            public PropRow[] props;
        }

        [Serializable]
        class Table
        {
            public PropRow[] shared;
            public ThemeRow[] themes;
        }

        [MenuItem("Samkuk/Step 12-6 - Terrain Themes (전투 맵 지형)")]
        public static void Run()
        {
            string tableFile = Path.Combine(Directory.GetParent(Application.dataPath).FullName, TablePath);
            if (!File.Exists(tableFile))
            {
                Debug.LogError($"[Samkuk] Step 12-6: 기준표가 없습니다 ({TablePath})");
                return;
            }
            var table = JsonUtility.FromJson<Table>(File.ReadAllText(tableFile, Encoding.UTF8));

            Directory.CreateDirectory(ThemeDir);
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            AssetDatabase.Refresh(); // 새 PNG 를 스프라이트로 가져온 뒤에 연결한다

            var catalog = AssetDatabase.LoadAssetAtPath<TerrainThemeCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<TerrainThemeCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var missing = new StringBuilder();
            int created = 0, linked = 0;

            foreach (var row in table.themes)
            {
                string path = $"{ThemeDir}/Theme_{row.terrain}.asset";
                var theme = AssetDatabase.LoadAssetAtPath<TerrainTheme>(path);
                if (theme == null)
                {
                    theme = ScriptableObject.CreateInstance<TerrainTheme>();
                    theme.terrain = (CastleTerrain)Enum.Parse(typeof(CastleTerrain), row.terrain);
                    theme.propsPerChunk = row.density;
                    AssetDatabase.CreateAsset(theme, path);
                    created++;
                }

                // 직접 지정한 그림은 그대로 둔다
                if (theme.groundTile == null)
                {
                    theme.groundTile = LoadSprite(row.ground.file, missing);
                    if (theme.groundTile != null) linked++;
                }

                foreach (var p in row.props)
                {
                    var prop = theme.props.Find(x => x.name == p.file);
                    if (prop == null)
                    {
                        prop = new TerrainProp { name = p.file, weight = p.weight, scaleMin = p.scaleMin, scaleMax = p.scaleMax };
                        theme.props.Add(prop);
                    }
                    if (prop.sprite == null)
                    {
                        prop.sprite = LoadSprite(p.file, missing);
                        if (prop.sprite != null) linked++;
                    }
                }

                // 강: 그림이 아직 연결되지 않았을 때만 연결하고 폭도 기준표 값으로 정한다 (직접 조정한 값은 보존)
                if (row.river != null && theme.riverWater == null)
                {
                    theme.riverWater = LoadSprite($"{row.river.file}_Water", missing);
                    theme.riverBank = LoadSprite($"{row.river.file}_Bank", missing);
                    theme.riverWidth = row.river.width;
                    if (theme.riverWater != null) linked += 2;
                }

                if (!catalog.themes.Contains(theme)) catalog.themes.Add(theme);
                EditorUtility.SetDirty(theme);
            }

            foreach (var s in table.shared)
            {
                var target = s.file == "Prop_Pond" ? catalog.pond : (s.file == "Prop_Banner" ? catalog.banner : null);
                if (target == null) continue;
                if (target.sprite == null)
                {
                    target.name = s.file;
                    target.scaleMin = s.scaleMin;
                    target.scaleMax = s.scaleMax;
                    target.sprite = LoadSprite(s.file, missing);
                    if (target.sprite != null) linked++;
                }
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Samkuk] Step 12-6 완료: 지형 테마 {catalog.themes.Count}개(새로 {created}개), 그림 연결 {linked}개" +
                      (missing.Length > 0 ? $"\n그림 없음 (tools/terrain_art/generate.ps1 실행 필요):{missing}" : ""));
        }

        static Sprite LoadSprite(string file, StringBuilder missing)
        {
            string path = $"{SpriteDir}/{file}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) missing.Append($"\n  - {path}");
            return sprite;
        }
    }

    /// <summary>
    /// Assets/Sprites/Terrain 의 PNG 를 맵용 스프라이트로 가져온다.
    ///   Ground_*  : 바닥 타일 (Repeat, FullRect, 4유닛 = 256px @ PPU 64)
    ///   Prop_Pond, River_* : 연못과 강 토막 (피벗 가운데)
    ///   그 밖의 Prop_* : 소품 (피벗 = 바닥에 닿는 점, 아래에서 6px 위)
    /// 그림을 넣기만 하면 되도록 가져오기 설정을 자동으로 맞춘다.
    /// </summary>
    class TerrainSpriteImporter : AssetPostprocessor
    {
        const float FootPixels = 6f;   // 생성기가 소품 바닥을 이만큼 띄워 그린다

        // 가져오기 설정을 바꿀 때마다 올린다: 이미 가져온 그림도 새 설정으로 다시 가져오게 한다
        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Step12TerrainSetup.SpriteDir + "/")) return;

            var importer = (TextureImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool ground = name.StartsWith("Ground_");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = ground ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = ground ? SpriteMeshType.FullRect : SpriteMeshType.Tight;   // 타일링(Tiled)은 FullRect 여야 한다
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = ground || name == "Prop_Pond" || name.StartsWith("River_") ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, FootPixels / Math.Max(1f, PngHeight(assetPath)));
            importer.SetTextureSettings(settings);
        }

        /// <summary>PNG 머리글에서 높이를 읽는다 (임포터가 원본 크기를 알려 주지 않는 시점이라).</summary>
        static float PngHeight(string assetPath)
        {
            try
            {
                using (var fs = File.OpenRead(assetPath))
                {
                    var header = new byte[24];
                    if (fs.Read(header, 0, 24) < 24) return 64f;
                    return (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23]; // IHDR 높이(빅엔디안)
                }
            }
            catch (IOException) { return 64f; }
        }
    }
}
