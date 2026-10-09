using System.IO;
using System.Text;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Weapons;
using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 10-9: 무기 휘두르기(베기 계열).
    ///  1) <c>Assets/Sprites/Weapons/&lt;무기 에셋 이름&gt;_Held.png</c> (예: Weapon_Sword_Held.png)를
    ///     베기(Slash) 무기의 <see cref="WeaponData.heldSprite"/> 에 연결한다. 이미 지정된 그림은 덮어쓰지 않는다.
    ///  2) 휘두르기 각도/시간이 0 인 베기 무기에 무기별 값을 채운다(쌍고검은 짧고 빠르게, 언월도는 넓고 묵직하게).
    /// 그림은 tools/hero_art/generate.ps1 -Only weapon 이 만든다(도트 규격 PPU 32, 손잡이 아래·날 위, 피벗 아래 가운데).
    /// 규칙/조작은 docs/WEAPON_SWING.md.
    /// </summary>
    public static class Step10SwingSetup
    {
        public const string SpriteDir = "Assets/Sprites/Weapons";
        const string WeaponDir = "Assets/ScriptableObjects/Weapons";

        /// <summary>무기별 휘두르기 값(각도, 초). 값이 0 인 에셋만 채우므로 이후 조정은 에셋을 직접 고친다.</summary>
        static readonly (string asset, float arc, float duration)[] Swings =
        {
            ("Weapon_Sword", 120f, 0.25f),           // 검 베기: 기준
            ("Weapon_TwinSwords", 100f, 0.18f),      // 쌍고검: 짧고 빠르게
            ("Weapon_GreenDragon", 150f, 0.32f),     // 청룡언월도: 넓고 묵직하게
            ("Weapon_Evo_Zanmato", 160f, 0.35f),     // 참마도: 가장 크게
            ("Weapon_Evo_TwinDragons", 110f, 0.20f), // 쌍룡자웅검
            ("Weapon_Evo_MoonDragon", 170f, 0.32f),  // 청룡참월도
        };

        [MenuItem("Samkuk/Step 10-9 - Link Held Weapon Sprites")]
        public static void Run()
        {
            Directory.CreateDirectory(SpriteDir);
            AssetDatabase.Refresh();

            var linked = new StringBuilder();
            var missing = new StringBuilder();
            int linkedCount = 0, missingCount = 0, tuned = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponDir }))
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
                if (weapon == null || weapon.type != WeaponType.Slash) continue;

                bool dirty = false;
                string expected = $"{SpriteDir}/{weapon.name}_Held.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(expected);
                if (sprite == null)
                {
                    missing.Append($"\n  - {weapon.displayName}: {expected}");
                    missingCount++;
                }
                else
                {
                    if (weapon.heldSprite == null) { weapon.heldSprite = sprite; dirty = true; }
                    linked.Append($"\n  - {weapon.displayName}: {expected}");
                    linkedCount++;
                }

                foreach (var (asset, arc, duration) in Swings)
                {
                    if (asset != weapon.name) continue;
                    if (weapon.swingArcDegrees <= 0f) { weapon.swingArcDegrees = arc; dirty = true; tuned++; }
                    if (weapon.swingDuration <= 0f) { weapon.swingDuration = duration; dirty = true; }
                }
                if (dirty) EditorUtility.SetDirty(weapon);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Samkuk] Step 10-9 완료: 무기 그림 연결 {linkedCount}종, 없음 {missingCount}종, 휘두르기 값 채움 {tuned}종" +
                      (linkedCount > 0 ? $"\n연결됨:{linked}" : "") +
                      (missingCount > 0 ? $"\n그림 없음(호 잔상만 보임, tools/hero_art/generate.ps1 -Only weapon):{missing}" : ""));
        }
    }

    /// <summary>
    /// Assets/Sprites/Weapons 의 PNG 를 들고 휘두르는 무기 스프라이트로 가져온다:
    /// 도트 규격(PPU <see cref="PixelArt.PPU"/>, Point 필터, 압축 없음), 피벗은 손잡이 끝(아래 가운데).
    /// </summary>
    class HeldWeaponImporter : AssetPostprocessor
    {
        // 가져오기 설정을 바꿀 때마다 올린다
        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Step10SwingSetup.SpriteDir + "/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelArt.PPU;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 256;
            importer.isReadable = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
