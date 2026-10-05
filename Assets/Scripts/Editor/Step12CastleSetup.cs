using System;
using System.IO;
using System.Text;
using Samkuk.Data;
using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 12-1: 내정용 성 46곳 만들기. <c>tools/castle_art/castles.json</c>(기준표)을 읽어
    /// <c>Assets/ScriptableObjects/Castles/Castle_&lt;id&gt;.asset</c> 과 <c>CastleCatalog</c> 를 채우고,
    /// <c>Assets/Sprites/Castles/Castle_&lt;id&gt;.png</c>(배경)를 각 성에 연결한다.
    /// 멱등: 이미 있는 성의 값은 덮어쓰지 않고(사용자가 조정한 수치 보존), 빠진 배경/인접 성만 채운다.
    /// 배경 그림은 <c>tools/castle_art/generate.ps1</c> 로 만든다. 규칙은 docs/CASTLES.md.
    /// </summary>
    public static class Step12CastleSetup
    {
        public const string BackgroundDir = "Assets/Sprites/Castles";
        const string CastleDir = "Assets/ScriptableObjects/Castles";
        const string CatalogPath = "Assets/ScriptableObjects/CastleCatalog.asset";
        const string TablePath = "tools/castle_art/castles.json";

        [Serializable]
        class Row
        {
            public string id, name, hanja, region, terrain;
            public int size;
            public bool water;
            public float x, y;
        }

        [Serializable]
        class Table
        {
            public Row[] castles;
            public string[] links;
        }

        [MenuItem("Samkuk/Step 12-1 - Castles (내정 성 + 배경)")]
        public static void Run()
        {
            string tableFile = Path.Combine(Directory.GetParent(Application.dataPath).FullName, TablePath);
            if (!File.Exists(tableFile))
            {
                Debug.LogError($"[Samkuk] Step 12-1: 기준표가 없습니다 ({TablePath})");
                return;
            }
            var table = JsonUtility.FromJson<Table>(File.ReadAllText(tableFile, Encoding.UTF8));

            Directory.CreateDirectory(CastleDir);
            AssetDatabase.Refresh(); // 새로 생긴 배경 PNG 를 스프라이트로 가져온 뒤에 연결한다

            var catalog = AssetDatabase.LoadAssetAtPath<CastleCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CastleCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            int created = 0, linked = 0, noBackground = 0;
            var missing = new StringBuilder();
            foreach (var row in table.castles)
            {
                string path = $"{CastleDir}/Castle_{row.id}.asset";
                var castle = AssetDatabase.LoadAssetAtPath<CastleData>(path);
                if (castle == null)
                {
                    castle = ScriptableObject.CreateInstance<CastleData>();
                    castle.id = row.id;
                    castle.displayName = row.name;
                    castle.hanja = row.hanja;
                    castle.region = (CastleRegion)Enum.Parse(typeof(CastleRegion), row.region);
                    castle.terrain = (CastleTerrain)Enum.Parse(typeof(CastleTerrain), row.terrain);
                    castle.size = (CastleSize)row.size;
                    castle.hasWater = row.water;
                    castle.mapPosition = new Vector2(row.x, row.y);
                    AssetDatabase.CreateAsset(castle, path);
                    created++;
                }

                // 직접 지정한 배경은 그대로 둔다
                if (castle.background == null)
                {
                    string bg = $"{BackgroundDir}/Castle_{row.id}.png";
                    castle.background = AssetDatabase.LoadAssetAtPath<Sprite>(bg);
                    if (castle.background == null) { noBackground++; missing.Append($"\n  - {row.name}: {bg}"); }
                    else linked++;
                }

                if (!catalog.castles.Contains(castle)) catalog.castles.Add(castle);
                EditorUtility.SetDirty(castle);
            }

            // 인접 성은 양방향으로, 없는 연결만 추가한다 (직접 뺀 연결은 되살리지 않으려면 기준표에서도 지울 것)
            foreach (string link in table.links)
            {
                string[] ab = link.Split('-');
                var a = catalog.Find(ab[0]);
                var b = catalog.Find(ab[1]);
                if (a == null || b == null) { Debug.LogWarning($"[Samkuk] Step 12-1: 알 수 없는 연결 {link}"); continue; }
                if (!a.neighbors.Contains(b)) a.neighbors.Add(b);
                if (!b.neighbors.Contains(a)) b.neighbors.Add(a);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Samkuk] Step 12-1 완료: 성 {catalog.castles.Count}곳(새로 {created}곳), 배경 연결 {linked}곳" +
                      (noBackground > 0 ? $"\n배경 없음 {noBackground}곳 (tools/castle_art/generate.ps1 실행 필요):{missing}" : ""));
        }
    }

    /// <summary>
    /// Assets/Sprites/Castles(성 배경)와 Assets/Sprites/Strategy(전략 지도)의 PNG 를 UI/배경용 스프라이트로 가져온다 (밉맵 없음, 압축: 46장을 한꺼번에 들고 있어도 부담이 적게).
    /// 그림을 넣기만 하면 되도록 가져오기 설정을 자동으로 맞춘다.
    /// </summary>
    class CastleBackgroundImporter : AssetPostprocessor
    {
        // 가져오기 설정을 바꿀 때마다 올린다: 이미 가져온 그림도 새 설정으로 다시 가져오게 한다
        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Step12CastleSetup.BackgroundDir + "/") && !assetPath.StartsWith(Step12StrategySetup.MapDir + "/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096; // 2048 이면 2560 폭의 전략 지도가 줄어들어 흐려진다
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
