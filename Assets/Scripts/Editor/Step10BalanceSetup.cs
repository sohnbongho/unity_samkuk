using System.Collections.Generic;
using Samkuk.Balance;
using Samkuk.Data;
using Samkuk.Meta;
using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 10-4: 밸런스. 기준값을 기존 에셋에 적용하고(셋업은 기존 에셋을 덮어쓰지 않으므로),
    /// 현재 데이터로 계산한 밸런스 보고서를 콘솔에 출력한다.
    /// 적용 대상: 모든 진화 조합의 필요 레벨(BalanceModel.EvolutionRequiredLevel).
    /// 경험치 곡선은 PlayerExperience, 선택지 가중치는 UpgradeCatalog, 웨이브는 Step 7 셋업에서 관리한다.
    /// </summary>
    public static class Step10BalanceSetup
    {
        const string EvolutionDir = "Assets/ScriptableObjects/Evolutions";
        const string StagePath = "Assets/ScriptableObjects/Stage/Stage_YellowTurban.asset";
        const string HeroCatalogPath = "Assets/ScriptableObjects/HeroCatalog.asset";
        const string UpgradeCatalogPath = "Assets/ScriptableObjects/UpgradeCatalog.asset";
        const string MetaCatalogPath = "Assets/ScriptableObjects/MetaCatalog.asset";

        /// <summary>
        /// 기존 에셋에 덮어쓰는 기준값 (에셋 이름, 필드, 값). 셋업은 기존 에셋을 건드리지 않으므로 수치를 바꿀 때는
        /// 여기와 해당 셋업의 기본값을 함께 고친다. 근거는 BalanceModel 보고서(장수 시작 무기 화력 편차, 진화 위력 비율).
        /// </summary>
        static readonly (string asset, string field, float value)[] Overrides =
        {
            ("Weapons/Weapon_YitianSword", "damage", 10f),        // 조조 시작 무기: 화력 16 → 20
            ("Weapons/Weapon_SkyPiercer", "damage", 7f),          // 여포 시작 무기: 화력 36 → 28
            ("Weapons/Weapon_Evo_DragonSpear", "damage", 20f),    // 진화 위력 5.2배 → 3.5배
            ("Weapons/Weapon_Evo_HeavenDrum", "damage", 36f),     // 진화 위력 1.7배 → 2.5배
            ("Weapons/Weapon_Evo_HeavenDrum", "cooldown", 2.6f),
            // 스테이지: 타격감 확인용으로 적 수를 이전 값(2.1/4.2, 56/84/112/154)의 30%로 줄임 (웨이브 순서 0~3)
            ("Stage/Stage_YellowTurban", "waves.Array.data[0].spawnPerSecond", 0.63f),
            ("Stage/Stage_YellowTurban", "waves.Array.data[0].maxAlive", 17f),
            ("Stage/Stage_YellowTurban", "waves.Array.data[1].spawnPerSecond", 1.26f),
            ("Stage/Stage_YellowTurban", "waves.Array.data[1].maxAlive", 25f),
            ("Stage/Stage_YellowTurban", "waves.Array.data[2].spawnPerSecond", 1.26f),
            ("Stage/Stage_YellowTurban", "waves.Array.data[2].maxAlive", 34f),
            ("Stage/Stage_YellowTurban", "waves.Array.data[3].spawnPerSecond", 1.26f),
            ("Stage/Stage_YellowTurban", "waves.Array.data[3].maxAlive", 46f),
            // 아군 화살(Arrow 계열)은 빠르게 (속도 12~18). 한때 줄였다가 원래 값으로 복원 — 느린 쪽은 적 궁병 화살이다
            ("Weapons/Weapon_Bow", "projectileSpeed", 12f),
            ("Weapons/Weapon_Bow", "duration", 1.5f),
            ("Weapons/Weapon_YitianSword", "projectileSpeed", 12f),
            ("Weapons/Weapon_YitianSword", "duration", 1.5f),
            ("Weapons/Weapon_Crossbow", "projectileSpeed", 18f),
            ("Weapons/Weapon_Crossbow", "duration", 1.2f),
            ("Weapons/Weapon_Knives", "projectileSpeed", 16f),
            ("Weapons/Weapon_Knives", "duration", 1.2f),
            ("Weapons/Weapon_Evo_Repeater", "projectileSpeed", 18f),
            ("Weapons/Weapon_Evo_Repeater", "duration", 1.2f),
            ("Weapons/Weapon_Evo_FlyingSpear", "projectileSpeed", 16f),
            ("Weapons/Weapon_Evo_FlyingSpear", "duration", 1.2f),
            ("Weapons/Weapon_Evo_OverlordSword", "projectileSpeed", 14f),
            ("Weapons/Weapon_Evo_OverlordSword", "duration", 1.5f),
            // 적 궁병 화살: 플레이어 이동 속도(4)보다 훨씬 느리게 (달리면 쉽게 피할 수 있도록).
            // 느려진 만큼 수명을 늘려 사격 거리(사거리의 1.15배)까지 닿게 한다
            ("Enemies/Enemy_YellowArcher", "projectileSpeed", 2f),
            ("Enemies/Enemy_YellowArcher", "projectileLifetime", 4.5f),
            ("Enemies/Enemy_DongzhuoCrossbow", "projectileSpeed", 2.5f),
            ("Enemies/Enemy_DongzhuoCrossbow", "projectileLifetime", 4.5f),
            ("Enemies/Enemy_LvbuArcher", "projectileSpeed", 3f),
            ("Enemies/Enemy_LvbuArcher", "projectileLifetime", 4.5f),
        };

        [MenuItem("Samkuk/Step 10-4 - Apply Balance & Report")]
        public static void Run()
        {
            int changed = ApplyEvolutionLevels();
            if (changed > 0) Debug.Log($"[Samkuk] 진화 필요 레벨을 Lv.{BalanceModel.EvolutionRequiredLevel}로 맞춤 ({changed}개)");

            int overridden = ApplyOverrides();
            if (overridden > 0) Debug.Log($"[Samkuk] 밸런스 기준값 적용 ({overridden}개 필드)");
            AssetDatabase.SaveAssets();

            PrintReport();
        }

        [MenuItem("Samkuk/Balance Report (read-only)")]
        public static void PrintReport()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(StagePath);
            var heroes = AssetDatabase.LoadAssetAtPath<HeroCatalog>(HeroCatalogPath);
            var upgrades = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(UpgradeCatalogPath);
            var meta = AssetDatabase.LoadAssetAtPath<MetaCatalog>(MetaCatalogPath);
            if (stage == null || heroes == null || upgrades == null || meta == null)
            {
                Debug.LogWarning("[Samkuk] 밸런스 보고서에 필요한 에셋이 없습니다. Run All Setup 을 먼저 실행하세요.");
                return;
            }

            var heroWeapons = new List<WeaponData>();
            foreach (var h in heroes.heroes)
                if (h != null && h.startingWeapon != null) heroWeapons.Add(h.startingWeapon);

            Debug.Log(BalanceModel.Report(stage, heroWeapons, upgrades, meta));
        }

        /// <summary>Overrides 표를 적용한다. 값이 바뀐 필드 개수를 돌려준다 (에셋이 없으면 건너뜀).</summary>
        static int ApplyOverrides()
        {
            int changed = 0;
            foreach (var (asset, field, value) in Overrides)
            {
                var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"Assets/ScriptableObjects/{asset}.asset");
                if (obj == null) continue;

                var so = new SerializedObject(obj);
                var prop = so.FindProperty(field);
                bool isFloat = prop != null && prop.propertyType == SerializedPropertyType.Float;
                bool isInt = prop != null && prop.propertyType == SerializedPropertyType.Integer;
                if (!isFloat && !isInt)
                {
                    Debug.LogWarning($"[Samkuk] 밸런스 기준값 적용 실패: {asset}.{field}");
                    continue;
                }

                if (isFloat)
                {
                    if (Mathf.Approximately(prop.floatValue, value)) continue;
                    prop.floatValue = value;
                }
                else
                {
                    int target = Mathf.RoundToInt(value);
                    if (prop.intValue == target) continue;
                    prop.intValue = target;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(obj);
                changed++;
            }
            return changed;
        }

        /// <summary>모든 진화 조합의 필요 레벨을 기준값으로 맞춘다. 바뀐 개수를 돌려준다.</summary>
        static int ApplyEvolutionLevels()
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:EvolutionData", new[] { EvolutionDir }))
            {
                var evo = AssetDatabase.LoadAssetAtPath<EvolutionData>(AssetDatabase.GUIDToAssetPath(guid));
                if (evo == null || evo.requiredLevel == BalanceModel.EvolutionRequiredLevel) continue;

                evo.requiredLevel = BalanceModel.EvolutionRequiredLevel;
                EditorUtility.SetDirty(evo);
                changed++;
            }
            return changed;
        }
    }
}
