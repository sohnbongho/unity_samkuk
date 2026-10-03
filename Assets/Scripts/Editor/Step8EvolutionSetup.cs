using System.IO;
using Samkuk.Balance;
using Samkuk.Data;
using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>Step 8-4: 진화 무기 7종과 진화 조합(무기 + 패시브)을 만들고 카탈로그에 등록한다.</summary>
    public static class Step8EvolutionSetup
    {
        const string SpriteDir = "Assets/Sprites";
        const string WeaponDir = "Assets/ScriptableObjects/Weapons";
        const string PassiveDir = "Assets/ScriptableObjects/Passives";
        const string EvolutionDir = "Assets/ScriptableObjects/Evolutions";
        const string CatalogPath = "Assets/ScriptableObjects/UpgradeCatalog.asset";

        /// <summary>(기본 무기 에셋, 필요 패시브 에셋, 진화 무기 에셋, 진화 조합 에셋 이름)</summary>
        static readonly (string baseWeapon, string passive, string evolved, string evolution)[] Pairs =
        {
            ("Weapon_Sword",     "Passive_Horse",    "Weapon_Evo_Zanmato",       "Evo_Sword"),
            ("Weapon_Bow",       "Passive_Haste",    "Weapon_Evo_Repeater",      "Evo_Bow"),
            ("Weapon_Axe",       "Passive_Armor",    "Weapon_Evo_HeavenAxe",     "Evo_Axe"),
            ("Weapon_FireZone",  "Passive_Wisdom",   "Weapon_Evo_RedCliff",      "Evo_FireZone"),
            ("Weapon_Lightning", "Passive_Strategy", "Weapon_Evo_HeavenThunder", "Evo_Lightning"),
            ("Weapon_Thrust",    "Passive_Rations",  "Weapon_Evo_DragonSpear",   "Evo_Thrust"),
            ("Weapon_Nova",      "Passive_Virtue",   "Weapon_Evo_HeavenDrum",    "Evo_Nova"),
            // 장수 시작 무기: 시작 무기가 곧 주력이므로 진화 가능해야 한다 (진화 도달 가능성 점검 결과)
            ("Weapon_TwinSwords",   "Passive_Virtue",   "Weapon_Evo_TwinDragons",   "Evo_TwinSwords"),
            ("Weapon_GreenDragon",  "Passive_Horse",    "Weapon_Evo_MoonDragon",    "Evo_GreenDragon"),
            ("Weapon_SerpentSpear", "Passive_Armor",    "Weapon_Evo_FlyingSpear",   "Evo_SerpentSpear"),
            ("Weapon_YitianSword",  "Passive_Strategy", "Weapon_Evo_OverlordSword", "Evo_YitianSword"),
            ("Weapon_SkyPiercer",   "Passive_Haste",    "Weapon_Evo_PeerlessHalberd", "Evo_SkyPiercer"),
        };

        [MenuItem("Samkuk/Step 8-4 - Setup Weapon Evolutions")]
        public static void Run()
        {
            Directory.CreateDirectory(EvolutionDir);
            AssetDatabase.Refresh();

            CreateEvolvedWeapons();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            CreateEvolutions();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AddToCatalog();
            AssetDatabase.SaveAssets();
            Debug.Log("[Samkuk] Step 8-4 setup 완료 (무기 Lv.5 + 짝 패시브 → 진화)");
        }

        // ───────────────────────── 진화 무기 ─────────────────────────

        static Sprite Sprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");

        static void CreateEvolvedWeapons()
        {
            Create("Weapon_Evo_Zanmato", w =>
            {
                w.displayName = "참마도"; w.type = WeaponType.Slash; w.sprite = Sprite("Slash");
                w.description = "말도 단칼에 베는 대도. 좌우를 동시에 크게 베고 적을 날려 보낸다.";
                w.tint = new Color(1f, 0.85f, 0.35f);
                w.damage = 40f; w.cooldown = 1.0f; w.range = 3.8f; w.count = 2; w.duration = 0.22f; w.knockback = 6f;
                w.levelsPerExtraCount = 0;
            });
            Create("Weapon_Evo_Repeater", w =>
            {
                w.displayName = "연노"; w.type = WeaponType.Arrow; w.sprite = Sprite("Arrow");
                w.description = "한 번에 두 발씩 쉴 새 없이 쏘아대는 연발 쇠뇌.";
                w.tint = new Color(1f, 0.85f, 0.4f);
                w.damage = 14f; w.cooldown = 0.35f; w.range = 11f; w.count = 2;
                w.projectileSpeed = 18f; w.pierce = 2; w.duration = 1.2f; w.size = 0.25f; w.levelsPerExtraCount = 3;
            });
            Create("Weapon_Evo_HeavenAxe", w =>
            {
                w.displayName = "파천부"; w.type = WeaponType.Orbit; w.sprite = Sprite("Blade");
                w.description = "하늘을 쪼개는 거대한 도끼 네 자루가 빠르게 맴돈다.";
                w.tint = new Color(1f, 0.8f, 0.3f);
                w.damage = 14f; w.range = 2.4f; w.count = 4; w.size = 0.65f; w.rotateSpeed = 260f; w.tickInterval = 0.2f;
                w.levelsPerExtraCount = 3;
            });
            Create("Weapon_Evo_RedCliff", w =>
            {
                w.displayName = "적벽대화"; w.type = WeaponType.FireZone; w.sprite = Sprite("Fire");
                w.description = "적벽을 태운 불바다. 넓은 불길 셋이 오래 타오른다.";
                w.tint = new Color(1f, 0.6f, 0.4f);
                w.damage = 9f; w.cooldown = 2.5f; w.range = 9f; w.size = 2.0f; w.count = 3; w.duration = 4f; w.tickInterval = 0.3f;
                w.levelsPerExtraCount = 3;
            });
            Create("Weapon_Evo_HeavenThunder", w =>
            {
                w.displayName = "천뢰"; w.type = WeaponType.Lightning; w.sprite = Sprite("Bolt");
                w.description = "하늘이 내리는 벼락. 셋이 동시에 떨어져 넓게 쓸어버린다.";
                w.tint = new Color(0.8f, 0.9f, 1f);
                w.damage = 40f; w.cooldown = 1.2f; w.range = 10f; w.size = 1.3f; w.count = 3; w.duration = 0.25f;
                w.levelsPerExtraCount = 3;
            });
            Create("Weapon_Evo_DragonSpear", w =>
            {
                w.displayName = "용담창"; w.type = WeaponType.Thrust; w.sprite = Sprite("Arrow");
                w.description = "용의 쓸개처럼 날카로운 창. 세 방향으로 동시에 내지른다.";
                w.tint = new Color(1f, 0.9f, 0.5f);
                w.damage = 20f; w.cooldown = 0.9f; w.range = 4.5f; w.size = 0.55f; w.count = 3; w.duration = 0.18f;
                w.knockback = 8f; w.levelsPerExtraCount = 0;
            });
            // ── 장수 시작 무기 진화 ──
            Create("Weapon_Evo_TwinDragons", w =>
            {
                w.displayName = "쌍룡자웅검"; w.type = WeaponType.Slash; w.sprite = Sprite("Slash");
                w.description = "두 자루 검이 용이 되어 좌우를 크게 휩쓴다.";
                w.tint = new Color(0.6f, 1f, 0.7f);
                w.damage = 26f; w.cooldown = 0.85f; w.range = 3.2f; w.count = 2; w.duration = 0.2f; w.knockback = 4f;
                w.levelsPerExtraCount = 0;
            });
            Create("Weapon_Evo_MoonDragon", w =>
            {
                w.displayName = "청룡참월도"; w.type = WeaponType.Slash; w.sprite = Sprite("Slash");
                w.description = "달까지 벨 기세의 거대한 청룡도. 사방의 적을 베어 날린다.";
                w.tint = new Color(0.45f, 1f, 0.7f);
                w.damage = 42f; w.cooldown = 1.1f; w.range = 4.4f; w.count = 2; w.duration = 0.24f; w.knockback = 8f;
                w.levelsPerExtraCount = 0;
            });
            Create("Weapon_Evo_FlyingSpear", w =>
            {
                w.displayName = "비룡사모"; w.type = WeaponType.Arrow; w.sprite = Sprite("Arrow");
                w.description = "하늘을 나는 용처럼 적진을 꿰뚫는 창을 연달아 던진다.";
                w.tint = new Color(0.85f, 0.85f, 1f);
                w.damage = 30f; w.cooldown = 0.8f; w.range = 10f; w.count = 2;
                w.projectileSpeed = 16f; w.pierce = 8; w.duration = 1.2f; w.size = 0.4f; w.levelsPerExtraCount = 3;
            });
            Create("Weapon_Evo_OverlordSword", w =>
            {
                w.displayName = "패왕의천검"; w.type = WeaponType.Arrow; w.sprite = Sprite("Arrow");
                w.description = "검기를 사방으로 쏟아내는 패왕의 검.";
                w.tint = new Color(0.55f, 0.75f, 1f);
                w.damage = 16f; w.cooldown = 0.7f; w.range = 10f; w.count = 4;
                w.projectileSpeed = 14f; w.pierce = 3; w.duration = 1.5f; w.size = 0.35f; w.levelsPerExtraCount = 3;
            });
            Create("Weapon_Evo_PeerlessHalberd", w =>
            {
                w.displayName = "천하무쌍극"; w.type = WeaponType.Orbit; w.sprite = Sprite("Blade");
                w.description = "천하에 둘도 없는 방천화극이 네 자루로 불어나 거세게 맴돈다.";
                w.tint = new Color(1f, 0.55f, 0.4f);
                w.damage = 18f; w.range = 2.4f; w.count = 4; w.size = 0.7f; w.rotateSpeed = 280f; w.tickInterval = 0.25f;
                w.levelsPerExtraCount = 3;
            });
            Create("Weapon_Evo_HeavenDrum", w =>
            {
                w.displayName = "천고"; w.type = WeaponType.Nova; w.sprite = Sprite("Slash");
                w.description = "하늘을 울리는 거대한 북소리. 더 넓고 강한 충격파가 퍼진다.";
                w.tint = new Color(1f, 0.9f, 0.6f);
                w.damage = 36f; w.cooldown = 2.6f; w.range = 6.5f; w.duration = 0.6f; w.knockback = 12f;
                w.levelsPerExtraCount = 0;
            });
        }

        static void Create(string name, System.Action<WeaponData> init)
        {
            string path = $"{WeaponDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<WeaponData>(path) != null) return; // 사용자가 조정한 값 유지

            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.maxLevel = 8;
            init(w);
            AssetDatabase.CreateAsset(w, path);
        }

        // ───────────────────────── 진화 조합 ─────────────────────────

        static void CreateEvolutions()
        {
            foreach (var (baseName, passiveName, evolvedName, evoName) in Pairs)
            {
                var baseWeapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDir}/{baseName}.asset");
                var passive = AssetDatabase.LoadAssetAtPath<PassiveData>($"{PassiveDir}/{passiveName}.asset");
                var evolved = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDir}/{evolvedName}.asset");
                if (baseWeapon == null || passive == null || evolved == null)
                {
                    Debug.LogWarning($"[Samkuk] 진화 조합 {evoName} 을(를) 만들 수 없습니다 (base={baseWeapon}, passive={passive}, evolved={evolved}). Step 6 / 8-2 를 먼저 실행하세요.");
                    continue;
                }

                string path = $"{EvolutionDir}/{evoName}.asset";
                var evo = AssetDatabase.LoadAssetAtPath<EvolutionData>(path);
                if (evo == null)
                {
                    evo = ScriptableObject.CreateInstance<EvolutionData>();
                    evo.requiredLevel = BalanceModel.EvolutionRequiredLevel; // 1분 스테이지에서 도달 가능한 레벨
                    evo.requiredPassiveLevel = 1;
                    AssetDatabase.CreateAsset(evo, path);
                }

                // 참조는 항상 맞춰 둔다 (필요 레벨은 사용자가 조정한 값을 유지)
                evo.baseWeapon = baseWeapon;
                evo.requiredPassive = passive;
                evo.evolvedWeapon = evolved;
                EditorUtility.SetDirty(evo);
            }
        }

        static void AddToCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("[Samkuk] UpgradeCatalog 가 없습니다. Step 6 을 먼저 실행하세요.");
                return;
            }

            catalog.evolutions.RemoveAll(e => e == null);
            foreach (var (_, _, _, evoName) in Pairs)
            {
                var evo = AssetDatabase.LoadAssetAtPath<EvolutionData>($"{EvolutionDir}/{evoName}.asset");
                if (evo != null && !catalog.evolutions.Contains(evo)) catalog.evolutions.Add(evo);
            }
            EditorUtility.SetDirty(catalog);
        }
    }
}
