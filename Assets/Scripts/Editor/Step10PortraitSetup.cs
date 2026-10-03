using System.IO;
using System.Text;
using Samkuk.Data;
using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 10-5: 장수 선택 카드 초상화 연결. <c>Assets/Sprites/Heroes/&lt;장수 에셋 이름&gt;.png</c>
    /// (예: Hero_LiuBei.png)를 찾아 <see cref="HeroData.portrait"/> 에 넣는다.
    /// 이미 지정된 초상화는 덮어쓰지 않고, 그림이 없는 장수는 기존 실루엣 + 색으로 보인다.
    /// 규격과 생성 프롬프트는 docs/HERO_PORTRAITS.md 참고.
    /// </summary>
    public static class Step10PortraitSetup
    {
        public const string PortraitDir = "Assets/Sprites/Heroes";
        const string HeroDir = "Assets/ScriptableObjects/Heroes";

        [MenuItem("Samkuk/Step 10-5 - Link Hero Portraits")]
        public static void Run()
        {
            Directory.CreateDirectory(PortraitDir);
            AssetDatabase.Refresh();

            var linked = new StringBuilder();
            var missing = new StringBuilder();
            int linkedCount = 0, missingCount = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:HeroData", new[] { HeroDir }))
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroData>(AssetDatabase.GUIDToAssetPath(guid));
                if (hero == null) continue;

                string expected = $"{PortraitDir}/{hero.name}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(expected);
                if (sprite == null)
                {
                    missing.Append($"\n  - {hero.displayName}: {expected}");
                    missingCount++;
                    continue;
                }

                // 직접 지정한 초상화는 그대로 둔다
                if (hero.portrait == null)
                {
                    hero.portrait = sprite;
                    EditorUtility.SetDirty(hero);
                }
                linked.Append($"\n  - {hero.displayName}: {expected}");
                linkedCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Samkuk] Step 10-5 완료: 초상화 연결 {linkedCount}명, 없음 {missingCount}명" +
                      (linkedCount > 0 ? $"\n연결됨:{linked}" : "") +
                      (missingCount > 0 ? $"\n그림 없음(기존 실루엣 사용):{missing}" : ""));
        }
    }

    /// <summary>
    /// Assets/Sprites/Heroes 에 넣은 PNG를 UI용 스프라이트로 가져온다 (선명하게, 밉맵 없음, 투명 배경 유지).
    /// 그림을 넣기만 하면 되도록 가져오기 설정을 자동으로 맞춘다.
    /// </summary>
    class HeroPortraitImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Step10PortraitSetup.PortraitDir + "/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
