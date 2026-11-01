using Samkuk.Core;
using Samkuk.Field;
using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Assets/Sprites/Field 의 PNG 를 야전 그림으로 가져온다 (규격 <see cref="FieldArtSpec"/>):
    /// 바닥 타일(Tile_*)은 피벗이 가운데인 단일 스프라이트, 부대 시트(Unit_*, 15-8 부터 걷기 시트 규격 4x4)는 실행 중에 자르므로 Default 텍스처.
    /// 둘 다 도트 규격(PPU 32, Point 필터, 압축/밉맵 없음).
    /// </summary>
    class FieldSpriteImporter : AssetPostprocessor
    {
        // 가져오기 설정을 바꿀 때마다 올린다
        public override uint GetVersion() => 2;   // 15-8: 부대 시트 192x192

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(FieldArtSpec.Dir + "/")) return;

            var importer = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool tile = file.StartsWith("Tile_");

            importer.textureType = tile ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 512;
            importer.isReadable = false;

            if (!tile) return;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelArt.PPU;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
