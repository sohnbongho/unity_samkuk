using System.IO;
using Samkuk.Data;
using Samkuk.Heroes;
using Samkuk.Player;
using Samkuk.Skills;
using Samkuk.UI;
using Samkuk.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.EditorTools
{
    /// <summary>Step 8-1: 장수 5명(능력치/시작 무기/고유 스킬), 장수 선택 화면, 스킬 HUD를 구성한다.</summary>
    public static class Step8Setup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        const string SpriteDir = "Assets/Sprites";
        const string WeaponDir = "Assets/ScriptableObjects/Weapons";
        const string SkillDir = "Assets/ScriptableObjects/Skills";
        const string HeroDir = "Assets/ScriptableObjects/Heroes";
        const string CatalogPath = "Assets/ScriptableObjects/HeroCatalog.asset";

        static readonly string[] HeroNames =
            { "Hero_LiuBei", "Hero_GuanYu", "Hero_ZhangFei", "Hero_CaoCao", "Hero_LvBu" };

        [MenuItem("Samkuk/Step 8 - Setup Heroes & Skills")]
        public static void Run()
        {
            Directory.CreateDirectory(SkillDir);
            Directory.CreateDirectory(HeroDir);
            AssetDatabase.Refresh();

            CreateSkills();
            CreateHeroWeapons();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            CreateHeroes();
            CreateCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SetupPlayerPrefab();
            BuildScene();
            Debug.Log("[Samkuk] Step 8 setup 완료 (게임 시작 시 장수 선택, 스페이스바: 스킬)");
        }

        // ───────────────────────── 스킬 ─────────────────────────

        static void CreateSkills()
        {
            var fx = LoadSprite("Slash");

            CreateSkill("Skill_Blessing", s =>
            {
                s.displayName = "인덕의 가호"; s.type = SkillType.Blessing;
                s.description = "체력을 40% 회복하고 경험치를 모두 끌어당긴다.";
                s.cooldown = 25f; s.radius = 3f; s.power = 0.4f; s.effectColor = new Color(0.6f, 1f, 0.6f);
            }, fx);
            CreateSkill("Skill_GreenDragon", s =>
            {
                s.displayName = "청룡참"; s.type = SkillType.GreenDragonSlash;
                s.description = "주변의 적에게 큰 피해를 준다.";
                s.cooldown = 20f; s.radius = 6f; s.damage = 90f; s.effectColor = new Color(0.3f, 1f, 0.5f);
            }, fx);
            CreateSkill("Skill_Roar", s =>
            {
                s.displayName = "일갈"; s.type = SkillType.Roar;
                s.description = "주변의 적을 2.5초간 기절시키고 피해를 준다.";
                s.cooldown = 22f; s.radius = 7f; s.damage = 15f; s.duration = 2.5f; s.effectColor = new Color(0.8f, 0.85f, 1f);
            }, fx);
            CreateSkill("Skill_Scheme", s =>
            {
                s.displayName = "간웅의 책략"; s.type = SkillType.Scheme;
                s.description = "8초간 공격력 +50%, 쿨다운 -30%.";
                s.cooldown = 30f; s.power = 0.5f; s.power2 = 0.3f; s.duration = 8f; s.effectColor = new Color(0.4f, 0.6f, 1f);
            }, fx);
            CreateSkill("Skill_Warrior", s =>
            {
                s.displayName = "무쌍난무"; s.type = SkillType.Warrior;
                s.description = "3초간 무적, 이동속도 +50%, 주변에 지속 피해.";
                s.cooldown = 28f; s.radius = 2.6f; s.damage = 14f; s.power = 0.5f; s.duration = 3f; s.tickInterval = 0.25f;
                s.effectColor = new Color(1f, 0.4f, 0.3f);
            }, fx);
        }

        static void CreateSkill(string name, System.Action<SkillData> init, Sprite fx)
        {
            string path = $"{SkillDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<SkillData>(path) != null) return; // 사용자가 조정한 값 유지

            var s = ScriptableObject.CreateInstance<SkillData>();
            init(s);
            s.effectSprite = fx;
            AssetDatabase.CreateAsset(s, path);
        }

        // ───────────────────────── 장수 전용 무기 ─────────────────────────

        static void CreateHeroWeapons()
        {
            CreateWeapon("Weapon_TwinSwords", w =>
            {
                w.displayName = "쌍고검"; w.type = WeaponType.Slash; w.sprite = LoadSprite("Slash");
                w.description = "좌우로 두 자루 검을 휘두른다.";
                w.tint = new Color(0.7f, 1f, 0.7f); w.damage = 12f; w.cooldown = 1.1f; w.range = 2.4f; w.count = 1; w.duration = 0.18f;
            });
            CreateWeapon("Weapon_GreenDragon", w =>
            {
                w.displayName = "청룡언월도"; w.type = WeaponType.Slash; w.sprite = LoadSprite("Slash");
                w.description = "넓은 범위를 크게 베어낸다.";
                w.tint = new Color(0.4f, 1f, 0.6f); w.damage = 20f; w.cooldown = 1.5f; w.range = 3.3f; w.count = 1; w.duration = 0.2f;
            });
            CreateWeapon("Weapon_SerpentSpear", w =>
            {
                w.displayName = "장팔사모"; w.type = WeaponType.Arrow; w.sprite = LoadSprite("Arrow");
                w.description = "적을 여럿 꿰뚫는 창을 던진다.";
                w.tint = new Color(0.75f, 0.75f, 0.9f); w.damage = 14f; w.cooldown = 1.1f; w.range = 9f; w.count = 1;
                w.projectileSpeed = 14f; w.pierce = 4; w.duration = 1.2f; w.size = 0.35f;
            });
            CreateWeapon("Weapon_YitianSword", w =>
            {
                w.displayName = "의천검"; w.type = WeaponType.Arrow; w.sprite = LoadSprite("Arrow");
                w.description = "검기를 두 갈래로 쏘아 보낸다.";
                w.tint = new Color(0.5f, 0.7f, 1f); w.damage = 8f; w.cooldown = 1.0f; w.range = 9f; w.count = 2;
                w.projectileSpeed = 12f; w.pierce = 1; w.duration = 1.5f; w.size = 0.3f;
            });
            CreateWeapon("Weapon_SkyPiercer", w =>
            {
                w.displayName = "방천화극"; w.type = WeaponType.Orbit; w.sprite = LoadSprite("Blade");
                w.description = "주위를 맴도는 화극이 적을 벤다.";
                w.tint = new Color(1f, 0.5f, 0.4f); w.damage = 9f; w.range = 1.9f; w.count = 2; w.size = 0.5f;
                w.rotateSpeed = 220f; w.tickInterval = 0.25f; w.levelsPerExtraCount = 2;
            });
        }

        static void CreateWeapon(string name, System.Action<WeaponData> init)
        {
            string path = $"{WeaponDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<WeaponData>(path) != null) return;

            var w = ScriptableObject.CreateInstance<WeaponData>();
            init(w);
            AssetDatabase.CreateAsset(w, path);
        }

        static Sprite LoadSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");

        // ───────────────────────── 장수 ─────────────────────────

        static void CreateHeroes()
        {
            CreateHero("Hero_LiuBei", h =>
            {
                h.displayName = "유비"; h.title = "인덕의 군주";
                h.description = "사람을 끌어모으는 덕으로 빠르게 성장한다.";
                h.tint = new Color(0.5f, 0.95f, 0.6f);
                h.maxHpBonus = 20f; h.expMultiplier = 1.2f; h.pickupRadiusMultiplier = 1.3f;
            }, "Weapon_TwinSwords", "Skill_Blessing");

            CreateHero("Hero_GuanYu", h =>
            {
                h.displayName = "관우"; h.title = "미염공";
                h.description = "청룡언월도를 휘두르는 무신. 공격력이 높다.";
                h.tint = new Color(0.2f, 0.6f, 0.3f);
                h.damageMultiplier = 1.15f; h.moveSpeedMultiplier = 0.95f;
            }, "Weapon_GreenDragon", "Skill_GreenDragon");

            CreateHero("Hero_ZhangFei", h =>
            {
                h.displayName = "장비"; h.title = "만인지적";
                h.description = "강인한 체력으로 적진 한가운데서 버틴다.";
                h.tint = new Color(0.4f, 0.4f, 0.6f);
                h.maxHpBonus = 50f; h.damageMultiplier = 1.05f;
            }, "Weapon_SerpentSpear", "Skill_Roar");

            CreateHero("Hero_CaoCao", h =>
            {
                h.displayName = "조조"; h.title = "난세의 간웅";
                h.description = "책략으로 전황을 뒤집는다.";
                h.tint = new Color(0.4f, 0.55f, 1f);
                h.expMultiplier = 1.1f; h.moveSpeedMultiplier = 1.05f;
            }, "Weapon_YitianSword", "Skill_Scheme");

            CreateHero("Hero_LvBu", h =>
            {
                h.displayName = "여포"; h.title = "천하무쌍";
                h.description = "최강의 무력을 지녔지만 몸이 약하다.";
                h.tint = new Color(1f, 0.55f, 0.3f);
                h.maxHpBonus = -20f; h.damageMultiplier = 1.2f; h.moveSpeedMultiplier = 1.1f;
            }, "Weapon_SkyPiercer", "Skill_Warrior");
        }

        static void CreateHero(string name, System.Action<HeroData> init, string weaponName, string skillName)
        {
            string path = $"{HeroDir}/{name}.asset";
            var hero = AssetDatabase.LoadAssetAtPath<HeroData>(path);
            bool isNew = hero == null;
            if (isNew)
            {
                hero = ScriptableObject.CreateInstance<HeroData>();
                init(hero);
                AssetDatabase.CreateAsset(hero, path);
            }

            // 무기/스킬 참조는 항상 맞춰 둔다 (사용자가 능력치를 바꿔도 유지됨)
            hero.startingWeapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDir}/{weaponName}.asset");
            hero.skill = AssetDatabase.LoadAssetAtPath<SkillData>($"{SkillDir}/{skillName}.asset");
            EditorUtility.SetDirty(hero);
        }

        static void CreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HeroCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<HeroCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.heroes.Clear();
            foreach (var n in HeroNames)
            {
                var h = AssetDatabase.LoadAssetAtPath<HeroData>($"{HeroDir}/{n}.asset");
                if (h != null) catalog.heroes.Add(h);
            }
            EditorUtility.SetDirty(catalog);
        }

        // ───────────────────────── 플레이어 프리팹 ─────────────────────────

        static void SetupPlayerPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            if (root.GetComponent<SkillController>() == null) root.AddComponent<SkillController>();

            // 시작 무기는 장수가 정해준다 → 프리팹의 기본 시작 무기는 비운다
            var wc = root.GetComponent<WeaponController>();
            if (wc != null)
            {
                var so = new SerializedObject(wc);
                so.FindProperty("startingWeapons").arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ───────────────────────── 씬 ─────────────────────────

        static void BuildScene()
        {
            // 주의: OpenScene(Single) 이후에 에셋을 로드한다.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            var systems = GameObject.Find("GameSystems");
            var hud = GameObject.Find("HUD");
            if (player == null || systems == null || hud == null)
            {
                Debug.LogError("[Samkuk] Player / GameSystems / HUD 가 씬에 없습니다. Step 2~7 을 먼저 실행하세요.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<HeroCatalog>(CatalogPath);
            var playerSprite = LoadSprite("Player");
            var stats = player.GetComponent<PlayerStats>();
            var health = player.GetComponent<PlayerHealth>();
            var weapons = player.GetComponent<WeaponController>();
            var skills = player.GetComponent<SkillController>();
            if (catalog == null || stats == null || health == null || weapons == null || skills == null)
            {
                Debug.LogError($"[Samkuk] 참조 로드 실패 catalog={catalog} stats={stats} health={health} weapons={weapons} skills={skills}");
                return;
            }

            // HUD 정리 후 재구성
            DestroyChild(hud.transform, "HeroSelectPanel");
            DestroyChild(hud.transform, "SkillHud");
            var oldUi = hud.GetComponent<HeroSelectUI>();
            if (oldUi != null) Object.DestroyImmediate(oldUi);
            var oldView = hud.GetComponent<SkillHudView>();
            if (oldView != null) Object.DestroyImmediate(oldView);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildSkillHud(hud.transform, font, out var skillBox, out var skillMask, out var skillLabel);
            BuildHeroPanel(hud.transform, font, playerSprite, out var panel, out var cards);

            var skillView = hud.AddComponent<SkillHudView>();
            var svSo = new SerializedObject(skillView);
            svSo.FindProperty("skills").objectReferenceValue = skills;
            svSo.FindProperty("label").objectReferenceValue = skillLabel;
            svSo.FindProperty("cooldownMask").objectReferenceValue = skillMask;
            svSo.FindProperty("root").objectReferenceValue = skillBox;
            svSo.ApplyModifiedPropertiesWithoutUndo();

            var heroUi = hud.AddComponent<HeroSelectUI>();
            var uiSo = new SerializedObject(heroUi);
            uiSo.FindProperty("panel").objectReferenceValue = panel;
            var cardsProp = uiSo.FindProperty("cards");
            cardsProp.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                var el = cardsProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("button").objectReferenceValue = cards[i].button;
                el.FindPropertyRelative("hotkey").objectReferenceValue = cards[i].hotkey;
                el.FindPropertyRelative("heroName").objectReferenceValue = cards[i].heroName;
                el.FindPropertyRelative("title").objectReferenceValue = cards[i].title;
                el.FindPropertyRelative("description").objectReferenceValue = cards[i].description;
                el.FindPropertyRelative("portrait").objectReferenceValue = cards[i].portrait;
            }
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            // GameSystems: HeroSelectController
            var old = systems.GetComponent<HeroSelectController>();
            if (old != null) Object.DestroyImmediate(old);
            var ctrl = systems.AddComponent<HeroSelectController>();
            var cSo = new SerializedObject(ctrl);
            cSo.FindProperty("catalog").objectReferenceValue = catalog;
            cSo.FindProperty("stats").objectReferenceValue = stats;
            cSo.FindProperty("health").objectReferenceValue = health;
            cSo.FindProperty("weapons").objectReferenceValue = weapons;
            cSo.FindProperty("skills").objectReferenceValue = skills;
            cSo.FindProperty("ui").objectReferenceValue = heroUi;
            cSo.ApplyModifiedPropertiesWithoutUndo();

            if (ctrl.Catalog == null || ctrl.Catalog.heroes.Count == 0)
                Debug.LogError("[Samkuk] HeroSelectController 참조 연결 실패 또는 장수 목록이 비어 있음");

            // 장수 선택 화면이 가장 위에 그려지도록
            var heroPanel = hud.transform.Find("HeroSelectPanel");
            if (heroPanel != null) heroPanel.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void DestroyChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        static void BuildSkillHud(Transform hud, Font font, out GameObject box, out RectTransform mask, out Text label)
        {
            var rt = NewRect("SkillHud", hud);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(320f, 64f);
            rt.anchoredPosition = new Vector2(20f, 40f);
            rt.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.18f, 0.28f, 0.95f);

            mask = NewRect("CooldownMask", rt);
            mask.anchorMin = Vector2.zero;
            mask.anchorMax = Vector2.one;
            mask.pivot = new Vector2(0.5f, 0f);
            mask.offsetMin = Vector2.zero;
            mask.offsetMax = Vector2.zero;
            mask.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            mask.GetComponent<Image>().raycastTarget = false;

            label = NewText("Label", rt, font, 26, TextAnchor.MiddleCenter, "스킬");
            Stretch(label.rectTransform);

            box = rt.gameObject;
            box.SetActive(false);
        }

        static void BuildHeroPanel(Transform hud, Font font, Sprite portraitSprite, out GameObject panelGo, out HeroCardView[] cards)
        {
            var panel = NewRect("HeroSelectPanel", hud);
            Stretch(panel);
            panel.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.08f, 0.95f);

            var title = NewText("Title", panel, font, 60, TextAnchor.MiddleCenter, "장수를 선택하세요");
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.93f);
            title.rectTransform.sizeDelta = new Vector2(1000f, 100f);

            var hint = NewText("Hint", panel, font, 24, TextAnchor.MiddleCenter, "카드를 클릭하거나 1 ~ 5 키로 선택");
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0.865f);
            hint.rectTransform.sizeDelta = new Vector2(1000f, 40f);

            cards = new HeroCardView[5];
            float[] xs = { -680f, -340f, 0f, 340f, 680f };
            for (int i = 0; i < 5; i++)
                cards[i] = BuildHeroCard(panel, font, portraitSprite, i, xs[i]);

            panel.gameObject.SetActive(false);
            panelGo = panel.gameObject;
        }

        static HeroCardView BuildHeroCard(RectTransform parent, Font font, Sprite portraitSprite, int index, float x)
        {
            var rt = NewRect($"Hero{index + 1}", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.44f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, 640f);
            rt.anchoredPosition = new Vector2(x, 0f);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.17f, 0.2f, 0.3f, 1f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            var hotkey = NewText("Hotkey", rt, font, 28, TextAnchor.UpperLeft, $"[{index + 1}]");
            hotkey.color = new Color(1f, 0.9f, 0.5f);
            hotkey.rectTransform.anchorMin = hotkey.rectTransform.anchorMax = new Vector2(0f, 1f);
            hotkey.rectTransform.pivot = new Vector2(0f, 1f);
            hotkey.rectTransform.sizeDelta = new Vector2(80f, 40f);
            hotkey.rectTransform.anchoredPosition = new Vector2(12f, -8f);

            var portrait = NewRect("Portrait", rt);
            portrait.anchorMin = portrait.anchorMax = new Vector2(0.5f, 1f);
            portrait.pivot = new Vector2(0.5f, 1f);
            portrait.sizeDelta = new Vector2(110f, 110f);
            portrait.anchoredPosition = new Vector2(0f, -48f);
            var portraitImg = portrait.gameObject.AddComponent<Image>();
            portraitImg.sprite = portraitSprite;
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;

            var heroName = NewText("Name", rt, font, 40, TextAnchor.MiddleCenter, "장수");
            heroName.rectTransform.anchorMin = new Vector2(0f, 1f);
            heroName.rectTransform.anchorMax = new Vector2(1f, 1f);
            heroName.rectTransform.pivot = new Vector2(0.5f, 1f);
            heroName.rectTransform.sizeDelta = new Vector2(0f, 50f);
            heroName.rectTransform.anchoredPosition = new Vector2(0f, -170f);

            var title = NewText("Title", rt, font, 22, TextAnchor.MiddleCenter, "별칭");
            title.color = new Color(1f, 0.85f, 0.4f);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(0f, 34f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -222f);

            var desc = NewText("Description", rt, font, 20, TextAnchor.UpperLeft, "설명");
            desc.rectTransform.anchorMin = Vector2.zero;
            desc.rectTransform.anchorMax = Vector2.one;
            desc.rectTransform.offsetMin = new Vector2(18f, 14f);
            desc.rectTransform.offsetMax = new Vector2(-18f, -266f);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Truncate;

            return new HeroCardView
            {
                button = btn, hotkey = hotkey, heroName = heroName, title = title, description = desc, portrait = portraitImg
            };
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Text NewText(string name, Transform parent, Font font, int size, TextAnchor anchor, string value)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = value;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
