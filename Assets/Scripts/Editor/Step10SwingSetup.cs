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
    /// Step 10-9: 무기 휘두르기(베기·찌르기 계열).
    ///  1) <c>Assets/Sprites/Weapons/&lt;무기 에셋 이름&gt;_Held.png</c> (예: Weapon_Sword_Held.png)를
    ///     베기(Slash)/찌르기(Thrust) 무기의 <see cref="WeaponData.heldSprite"/> 에 연결한다. 이미 지정된 그림은 덮어쓰지 않는다.
    ///  2) 휘두르기 각도/시간이 0 인 무기에 무기별 값을 채운다(쌍고검은 짧고 빠르게, 언월도는 넓고 묵직하게).
    ///  3) 장비의 장팔사모가 예전 화살(Arrow) 형이면 찌르기(Thrust) 형으로 한 번 바꾼다(사용자 요청: 던지는 화살이 아니라 찌르는 창).
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
            // 찌르기(각도는 쓰지 않음, 시간만)
            ("Weapon_Thrust", 1f, 0.22f),             // 창 찌르기
            ("Weapon_SerpentSpear", 1f, 0.20f),       // 장팔사모: 날카롭게
            ("Weapon_Evo_DragonSpear", 1f, 0.26f),    // 용담창
        };

        /// <summary>
        /// 검기가 무기인 베기(유비 계열): 반원은 작아도 멀리 날아가며 지나치는 적에게 피해. (에셋, 비거리 유닛, 크기 배율, 피해 비율).
        /// 값이 0 인 에셋만 채운다.
        /// </summary>
        static readonly (string asset, float travel, float scale, float damageRatio)[] Trails =
        {
            ("Weapon_TwinSwords", 7f, 0.7f, 0.5f),       // 쌍고검: 작은 검기가 멀리
            ("Weapon_Evo_TwinDragons", 9f, 0.9f, 0.7f),  // 쌍룡자웅검: 더 크고 더 멀리
            ("Weapon_SerpentSpear", 6f, 1.2f, 0.5f),     // 장팔사모: 뾰족한 검기가 멀리
        };

        /// <summary>장팔사모 찌르기형 값. 밸런스 모델(찌르기 = 피해 x 2.5 / 쿨다운)에서 다른 장수 시작 무기와 비슷한 화력.</summary>
        const float SerpentDamage = 10f, SerpentCooldown = 1.1f, SerpentRange = 3.6f, SerpentSize = 0.35f, SerpentKnockback = 3f, SerpentDuration = 0.15f;

        [MenuItem("Samkuk/Step 10-9 - Link Held Weapon Sprites")]
        public static void Run()
        {
            Directory.CreateDirectory(SpriteDir);
            AssetDatabase.Refresh();

            var linked = new StringBuilder();
            var missing = new StringBuilder();
            int linkedCount = 0, missingCount = 0, tuned = 0;

            bool migrated = MigrateSerpentSpear();

            foreach (string guid in AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponDir }))
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
                if (weapon == null || (weapon.type != WeaponType.Slash && weapon.type != WeaponType.Thrust)) continue;

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
                foreach (var (asset, travel, scale, ratio) in Trails)
                {
                    if (asset != weapon.name) continue;
                    if (weapon.trailTravel <= 0f) { weapon.trailTravel = travel; dirty = true; }
                    if (weapon.trailScale <= 0f) { weapon.trailScale = scale; dirty = true; }
                    if (weapon.trailDamageRatio <= 0f) { weapon.trailDamageRatio = ratio; dirty = true; }
                }
                if (dirty) EditorUtility.SetDirty(weapon);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Samkuk] Step 10-9 완료: 무기 그림 연결 {linkedCount}종, 없음 {missingCount}종, 휘두르기 값 채움 {tuned}종" +
                      (migrated ? "\n장팔사모를 화살형 → 찌르기형으로 바꿈" : "") +
                      (linkedCount > 0 ? $"\n연결됨:{linked}" : "") +
                      (missingCount > 0 ? $"\n그림 없음(호 잔상만 보임, tools/hero_art/generate.ps1 -Only weapon):{missing}" : ""));
        }

        /// <summary>
        /// 장팔사모가 예전 화살(Arrow)형이면 찌르기(Thrust)형으로 바꾼다. 한 번 바뀌면 다시 실행해도 건드리지 않는다.
        /// 진화형 비룡사모는 "날아가는 창"이라 화살형 그대로 둔다.
        /// </summary>
        static bool MigrateSerpentSpear()
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDir}/Weapon_SerpentSpear.asset");
            if (w == null || w.type != WeaponType.Arrow) return false;

            w.type = WeaponType.Thrust;
            w.description = "장팔사모를 길게 내질러 일직선의 적을 꿰뚫고, 뾰족한 검기가 멀리 날아간다.";
            w.damage = SerpentDamage; w.cooldown = SerpentCooldown; w.range = SerpentRange; w.size = SerpentSize;
            w.knockback = SerpentKnockback; w.duration = SerpentDuration;
            w.sprite = null; // 예전 화살 그림은 더 쓰지 않는다 (검기는 코드로 만든다)
            EditorUtility.SetDirty(w);
            return true;
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
