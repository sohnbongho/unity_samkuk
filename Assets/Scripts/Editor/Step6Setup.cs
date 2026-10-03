using System.IO;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Pickups;
using Samkuk.Player;
using Samkuk.Upgrades;
using Samkuk.UI;
using Samkuk.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Samkuk.EditorTools
{
    /// <summary>Step 6: 경험치 보석, 패시브/카탈로그, 레벨업 UI, 경험치 바를 구성한다.</summary>
    public static class Step6Setup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        const string GemPrefabPath = "Assets/Prefabs/ExpGem.prefab";
        const string GemSpritePath = "Assets/Sprites/ExpGem.png";
        const string WeaponDir = "Assets/ScriptableObjects/Weapons";
        const string PassiveDir = "Assets/ScriptableObjects/Passives";
        const string CatalogPath = "Assets/ScriptableObjects/UpgradeCatalog.asset";

        static readonly string[] WeaponNames = { "Weapon_Bow", "Weapon_Sword", "Weapon_Axe" };
        static readonly string[] PassiveNames =
        {
            "Passive_Strategy", "Passive_Haste", "Passive_Horse", "Passive_Armor",
            "Passive_Rations", "Passive_Virtue", "Passive_Wisdom"
        };

        [MenuItem("Samkuk/Step 6 - Setup Experience & Level-Up")]
        public static void Run()
        {
            Directory.CreateDirectory(PassiveDir);
            AssetDatabase.Refresh();

            CreateGemPrefab();
            CreatePassives();
            FillWeaponDescriptions();
            CreateCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SetupPlayerPrefab();
            BuildScene();
            Debug.Log("[Samkuk] Step 6 setup 완료 (Play 후 F3: 경험치 +10)");
        }

        // ───────────────────────── 에셋 ─────────────────────────

        static void CreateGemPrefab()
        {
            var root = new GameObject("ExpGem") { layer = LayerMask.NameToLayer(GameLayers.Pickup) };
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GemSpritePath);
            sr.sortingLayerName = GameLayers.Sorting.Pickup;
            root.AddComponent<ExpGem>();
            PrefabUtility.SaveAsPrefabAsset(root, GemPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void CreatePassives()
        {
            CreatePassive("Passive_Strategy", "병법서", "모든 무기의 공격력이 15% 증가합니다.", PassiveType.Damage, 0.15f);
            CreatePassive("Passive_Haste", "신속", "무기 쿨다운이 8% 감소합니다.", PassiveType.Cooldown, 0.08f);
            CreatePassive("Passive_Horse", "적토마", "이동 속도가 10% 증가합니다.", PassiveType.MoveSpeed, 0.10f);
            CreatePassive("Passive_Armor", "철갑", "최대 체력이 20 증가하고 그만큼 회복합니다.", PassiveType.MaxHp, 20f);
            CreatePassive("Passive_Rations", "군량", "초당 체력이 0.5 회복됩니다.", PassiveType.Regen, 0.5f);
            CreatePassive("Passive_Virtue", "인덕", "경험치 획득 범위가 30% 증가합니다.", PassiveType.PickupRadius, 0.30f);
            CreatePassive("Passive_Wisdom", "지략", "경험치 획득량이 15% 증가합니다.", PassiveType.ExpGain, 0.15f);
        }

        static void CreatePassive(string assetName, string display, string desc, PassiveType type, float value)
        {
            string path = $"{PassiveDir}/{assetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<PassiveData>(path) != null) return; // 사용자가 조정한 값 유지

            var data = ScriptableObject.CreateInstance<PassiveData>();
            data.displayName = display;
            data.description = desc;
            data.type = type;
            data.valuePerLevel = value;
            data.maxLevel = 5;
            AssetDatabase.CreateAsset(data, path);
        }

        static void FillWeaponDescriptions()
        {
            SetDescriptionIfEmpty("Weapon_Bow", "가장 가까운 적에게 화살을 쏩니다.");
            SetDescriptionIfEmpty("Weapon_Sword", "좌우로 검을 휘둘러 주변 적을 벱니다.");
            SetDescriptionIfEmpty("Weapon_Axe", "플레이어 주위를 도는 도끼가 적을 베어냅니다.");
        }

        static void SetDescriptionIfEmpty(string assetName, string desc)
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDir}/{assetName}.asset");
            if (data == null || !string.IsNullOrEmpty(data.description)) return;
            data.description = desc;
            EditorUtility.SetDirty(data);
        }

        static void CreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<UpgradeCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.weapons.Clear();
            foreach (var n in WeaponNames)
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDir}/{n}.asset");
                if (w != null) catalog.weapons.Add(w);
                else Debug.LogWarning($"[Samkuk] {n} 을(를) 찾을 수 없습니다. Step 5 를 먼저 실행하세요.");
            }

            catalog.passives.Clear();
            foreach (var n in PassiveNames)
            {
                var p = AssetDatabase.LoadAssetAtPath<PassiveData>($"{PassiveDir}/{n}.asset");
                if (p != null) catalog.passives.Add(p);
            }
            EditorUtility.SetDirty(catalog);
        }

        static void SetupPlayerPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            if (root.GetComponent<PlayerStats>() == null) root.AddComponent<PlayerStats>();
            if (root.GetComponent<PlayerExperience>() == null) root.AddComponent<PlayerExperience>();

            // 시작 무기는 활 하나
            var wc = root.GetComponent<WeaponController>();
            var bow = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDir}/Weapon_Bow.asset");
            if (wc != null && bow != null)
            {
                var so = new SerializedObject(wc);
                var list = so.FindProperty("startingWeapons");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = bow;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[Samkuk] Player에 WeaponController가 없거나 활 데이터가 없습니다. Step 5 를 먼저 실행하세요.");
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ───────────────────────── 씬 ─────────────────────────

        static void BuildScene()
        {
            // 주의: OpenScene(Single)은 로드된 에셋 참조를 무효화하므로 에셋 로드는 씬을 연 뒤에 한다.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            var systems = GameObject.Find("GameSystems");
            var hud = GameObject.Find("HUD");
            if (player == null || systems == null || hud == null)
            {
                Debug.LogError("[Samkuk] Player / GameSystems / HUD 가 씬에 없습니다. Step 2~4 를 먼저 실행하세요.");
                return;
            }

            var gemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GemPrefabPath)?.GetComponent<ExpGem>();
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(CatalogPath);
            var experience = player.GetComponent<PlayerExperience>();
            var stats = player.GetComponent<PlayerStats>();
            var health = player.GetComponent<PlayerHealth>();
            var weapons = player.GetComponent<WeaponController>();
            if (gemPrefab == null || catalog == null || experience == null || stats == null || health == null || weapons == null)
            {
                Debug.LogError($"[Samkuk] 참조 로드 실패 gem={gemPrefab} catalog={catalog} exp={experience} stats={stats} health={health} weapons={weapons}");
                return;
            }

            // HUD: 경험치 바 + 레벨업 패널 (카드 클릭을 위해 GraphicRaycaster 필요)
            if (hud.GetComponent<GraphicRaycaster>() == null) hud.AddComponent<GraphicRaycaster>();
            DestroyChild(hud.transform, "ExpBar");
            DestroyChild(hud.transform, "LevelUpPanel");
            var existingUi = hud.GetComponent<LevelUpUI>();
            if (existingUi != null) Object.DestroyImmediate(existingUi);

            BuildExpBar(hud.transform, out var expFill, out var levelLabel);
            BuildLevelUpPanel(hud.transform, out var panel, out var cards);

            var levelUpUi = hud.AddComponent<LevelUpUI>();
            var uiSo = new SerializedObject(levelUpUi);
            uiSo.FindProperty("panel").objectReferenceValue = panel;
            var cardsProp = uiSo.FindProperty("cards");
            cardsProp.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                var el = cardsProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("button").objectReferenceValue = cards[i].button;
                el.FindPropertyRelative("title").objectReferenceValue = cards[i].title;
                el.FindPropertyRelative("description").objectReferenceValue = cards[i].description;
                el.FindPropertyRelative("hotkey").objectReferenceValue = cards[i].hotkey;
            }
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            var hudCtrl = hud.GetComponent<HudController>();
            var hudSo = new SerializedObject(hudCtrl);
            hudSo.FindProperty("playerExperience").objectReferenceValue = experience;
            hudSo.FindProperty("expFill").objectReferenceValue = expFill;
            hudSo.FindProperty("levelLabel").objectReferenceValue = levelLabel;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // EventSystem (새 Input System 용)
            var oldEs = Object.FindAnyObjectByType<EventSystem>();
            if (oldEs != null) Object.DestroyImmediate(oldEs.gameObject);
            var esGo = new GameObject("EventSystem", typeof(EventSystem));
            var module = esGo.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();

            // GameSystems: 경험치 매니저, 레벨업 컨트롤러
            var oldGem = systems.GetComponent<ExpGemManager>();
            if (oldGem != null) Object.DestroyImmediate(oldGem);
            var gemMgr = systems.AddComponent<ExpGemManager>();
            var gemSo = new SerializedObject(gemMgr);
            gemSo.FindProperty("gemPrefab").objectReferenceValue = gemPrefab;
            gemSo.FindProperty("target").objectReferenceValue = player.transform;
            gemSo.FindProperty("experience").objectReferenceValue = experience;
            gemSo.ApplyModifiedPropertiesWithoutUndo();

            var oldLv = systems.GetComponent<LevelUpController>();
            if (oldLv != null) Object.DestroyImmediate(oldLv);
            var lv = systems.AddComponent<LevelUpController>();
            var lvSo = new SerializedObject(lv);
            lvSo.FindProperty("experience").objectReferenceValue = experience;
            lvSo.FindProperty("weapons").objectReferenceValue = weapons;
            lvSo.FindProperty("stats").objectReferenceValue = stats;
            lvSo.FindProperty("health").objectReferenceValue = health;
            lvSo.FindProperty("catalog").objectReferenceValue = catalog;
            lvSo.FindProperty("ui").objectReferenceValue = levelUpUi;
            lvSo.ApplyModifiedPropertiesWithoutUndo();

            if (gemMgr.GemPrefab == null || lv.Catalog == null)
                Debug.LogError("[Samkuk] ExpGemManager/LevelUpController 참조 연결 실패");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void DestroyChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        static void BuildExpBar(Transform hud, out RectTransform fill, out Text label)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var bar = NewRect("ExpBar", hud);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, 24f);
            bar.anchoredPosition = Vector2.zero;
            bar.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

            fill = NewRect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
            fill.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.8f, 1f);

            label = NewText("Label", bar, font, 18, TextAnchor.MiddleCenter, "Lv.1");
            Stretch(label.rectTransform);
        }

        static void BuildLevelUpPanel(Transform hud, out GameObject panelGo, out UpgradeCardView[] cards)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var panel = NewRect("LevelUpPanel", hud);
            Stretch(panel);
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var title = NewText("Title", panel, font, 64, TextAnchor.MiddleCenter, "레벨 업!");
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.82f);
            title.rectTransform.sizeDelta = new Vector2(900f, 110f);

            var sub = NewText("Hint", panel, font, 26, TextAnchor.MiddleCenter, "카드를 클릭하거나 1 / 2 / 3 키로 선택");
            sub.rectTransform.anchorMin = sub.rectTransform.anchorMax = new Vector2(0.5f, 0.73f);
            sub.rectTransform.sizeDelta = new Vector2(900f, 50f);

            cards = new UpgradeCardView[3];
            float[] xs = { -440f, 0f, 440f };
            for (int i = 0; i < 3; i++)
                cards[i] = BuildCard(panel, font, i, xs[i]);

            panel.gameObject.SetActive(false);
            panelGo = panel.gameObject;
        }

        static UpgradeCardView BuildCard(RectTransform parent, Font font, int index, float x)
        {
            var rt = NewRect($"Card{index + 1}", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.45f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 320f);
            rt.anchoredPosition = new Vector2(x, 0f);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.17f, 0.2f, 0.3f, 1f);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            var hotkey = NewText("Hotkey", rt, font, 30, TextAnchor.UpperLeft, $"[{index + 1}]");
            hotkey.color = new Color(1f, 0.9f, 0.5f);
            hotkey.rectTransform.anchorMin = new Vector2(0f, 1f);
            hotkey.rectTransform.anchorMax = new Vector2(0f, 1f);
            hotkey.rectTransform.pivot = new Vector2(0f, 1f);
            hotkey.rectTransform.sizeDelta = new Vector2(100f, 44f);
            hotkey.rectTransform.anchoredPosition = new Vector2(14f, -10f);

            var title = NewText("Title", rt, font, 32, TextAnchor.MiddleCenter, "제목");
            title.rectTransform.anchorMin = new Vector2(0f, 0.6f);
            title.rectTransform.anchorMax = new Vector2(1f, 0.88f);
            title.rectTransform.offsetMin = new Vector2(16f, 0f);
            title.rectTransform.offsetMax = new Vector2(-16f, 0f);
            title.color = new Color(1f, 0.95f, 0.8f);

            var desc = NewText("Description", rt, font, 24, TextAnchor.UpperCenter, "설명");
            desc.rectTransform.anchorMin = new Vector2(0f, 0.06f);
            desc.rectTransform.anchorMax = new Vector2(1f, 0.58f);
            desc.rectTransform.offsetMin = new Vector2(22f, 0f);
            desc.rectTransform.offsetMax = new Vector2(-22f, 0f);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Truncate;

            return new UpgradeCardView { button = btn, title = title, description = desc, hotkey = hotkey };
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
