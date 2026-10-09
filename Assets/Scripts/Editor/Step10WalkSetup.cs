using System.IO;
using System.Text;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Player;
using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 10-6: 게임 안 장수 걷기 애니메이션.
    ///  1) Player 프리팹에 <see cref="PlayerAnimator"/> 를 붙인다.
    ///  2) <c>Assets/Sprites/HeroWalk/&lt;장수 에셋 이름&gt;_Walk.png</c> (예: Hero_LiuBei_Walk.png)를
    ///     <see cref="HeroData.walkSheet"/> 에 연결한다. 이미 지정된 시트는 덮어쓰지 않는다.
    /// 시트 규격(4열 x 4행)은 <see cref="HeroSpriteSet"/> 와 docs/HERO_WALK_SHEETS.md 참고.
    /// </summary>
    public static class Step10WalkSetup
    {
        public const string SheetDir = "Assets/Sprites/HeroWalk";
        const string HeroDir = "Assets/ScriptableObjects/Heroes";
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

        [MenuItem("Samkuk/Step 10-6 - Link Hero Walk Sheets")]
        public static void Run()
        {
            Directory.CreateDirectory(SheetDir);
            AssetDatabase.Refresh();

            bool prefabOk = EnsureAnimatorOnPlayerPrefab();
            LinkSheets(out int linked, out string linkedList, out int missing, out string missingList);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Samkuk] Step 10-6 완료: Player 프리팹 PlayerAnimator {(prefabOk ? "OK" : "프리팹 없음 - Step 2 먼저")}, " +
                      $"걷기 시트 연결 {linked}명, 없음 {missing}명" +
                      (linked > 0 ? $"\n연결됨:{linkedList}" : "") +
                      (missing > 0 ? $"\n시트 없음(기본 스프라이트 사용):{missingList}" : ""));
        }

        static bool EnsureAnimatorOnPlayerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null) return false;

            var contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (contents.GetComponent<PlayerAnimator>() == null)
                {
                    contents.AddComponent<PlayerAnimator>();
                    PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return true;
        }

        static void LinkSheets(out int linked, out string linkedList, out int missing, out string missingList)
        {
            var ok = new StringBuilder();
            var none = new StringBuilder();
            linked = 0;
            missing = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:HeroData", new[] { HeroDir }))
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroData>(AssetDatabase.GUIDToAssetPath(guid));
                if (hero == null) continue;

                string expected = $"{SheetDir}/{hero.name}_Walk.png";
                var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(expected);
                if (sheet == null)
                {
                    none.Append($"\n  - {hero.displayName}: {expected}");
                    missing++;
                    continue;
                }

                // 직접 지정한 시트는 그대로 둔다
                if (hero.walkSheet == null)
                {
                    hero.walkSheet = sheet;
                    if (PixelArt.IsPixelWalkSheet(sheet.width)) hero.walkPixelsPerUnit = PixelArt.PPU;   // 도트 규격(칸 48)
                    EditorUtility.SetDirty(hero);
                }
                ok.Append($"\n  - {hero.displayName}: {expected}");
                linked++;
            }

            linkedList = ok.ToString();
            missingList = none.ToString();
        }
    }

    /// <summary>
    /// Assets/Sprites/HeroWalk, Assets/Sprites/EnemyWalk 의 PNG를 걷기 시트용 텍스처로 가져온다.
    /// 실행 중에 직접 자르므로 스프라이트 슬라이스는 쓰지 않고, 선명하게(밉맵/압축 없음, 도트가 번지지 않게 Point 필터) 투명 배경을 유지한다.
    /// </summary>
    class HeroWalkImporter : AssetPostprocessor
    {
        // 가져오기 설정을 바꿀 때마다 올린다 (2: 도트 규격 Point 필터)
        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Step10WalkSetup.SheetDir + "/") && !assetPath.StartsWith(Step10EnemyWalkSetup.SheetDir + "/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.isReadable = false;
        }
    }
}
