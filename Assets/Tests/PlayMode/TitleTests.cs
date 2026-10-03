using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Meta;
using Samkuk.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Samkuk.Tests
{
    public class TitleTests
    {
        const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        string savePath;
        readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            // 실제 저장 파일을 건드리지 않도록 임시 경로를 쓴다
            savePath = Path.Combine(Application.temporaryCachePath, $"test_title_save_{Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = savePath;
            SaveSystem.ResetCache();
        }

        [TearDown]
        public void TearDown()
        {
            Audio.AudioManager.DestroyInstance();
            Time.timeScale = 1f;
            SaveSystem.Delete();
            SaveSystem.PathOverride = null;
            SaveSystem.ResetCache();
            if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
            foreach (var o in toDestroy) if (o != null) UnityEngine.Object.Destroy(o);
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        MetaUpgradeData MakeMeta(string name, MetaStat stat, float perLevel = 0.1f, int maxLevel = 3,
            int baseCost = 100, float growth = 2f)
        {
            var u = ScriptableObject.CreateInstance<MetaUpgradeData>();
            u.name = name; u.displayName = name + "강화"; u.description = name + " 설명";
            u.stat = stat; u.valuePerLevel = perLevel; u.maxLevel = maxLevel;
            u.baseCost = baseCost; u.costGrowth = growth;
            toDestroy.Add(u);
            return u;
        }

        MetaCatalog MakeCatalog(params MetaUpgradeData[] upgrades)
        {
            var c = ScriptableObject.CreateInstance<MetaCatalog>();
            c.upgrades.AddRange(upgrades);
            toDestroy.Add(c);
            return c;
        }

        void GiveGold(int gold)
        {
            SaveSystem.Current.gold = gold;
            SaveSystem.SaveCurrent();
        }

        class TitleParts
        {
            public GameObject root;
            public TitleController controller;
            public GameObject main, shopPanel, recordsPanel;
            public Button start, shop, records, reset, quit, shopClose, recordsClose;
            public MetaShopUI shopUi;
            public Text gold, resetLabel, recordsText, shopGold;
            public Button sound;
            public Text soundLabel;
            public Button shake;
            public Text shakeLabel;
            public RectTransform rows;
        }

        Button NewButton(Transform parent, string name, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Button>().targetGraphic = go.GetComponent<Image>();
            var lab = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lab.transform.SetParent(go.transform, false);
            label = lab.GetComponent<Text>();
            return go.GetComponent<Button>();
        }

        Text NewText(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Text>();
        }

        /// <summary>타이틀 씬과 같은 구성의 UI를 코드로 만든다 (Awake 전에 참조를 모두 연결하기 위해 비활성으로 구성).</summary>
        TitleParts BuildTitle(MetaCatalog catalog, Action<string> sceneLoader = null, Action quit = null)
        {
            var p = new TitleParts();
            p.root = new GameObject("TestTitle", typeof(RectTransform));
            toDestroy.Add(p.root);
            p.root.SetActive(false);
            var rt = p.root.transform;

            p.main = new GameObject("Main", typeof(RectTransform)); p.main.transform.SetParent(rt, false);
            p.shopPanel = new GameObject("ShopPanel", typeof(RectTransform)); p.shopPanel.transform.SetParent(rt, false);
            p.recordsPanel = new GameObject("RecordsPanel", typeof(RectTransform)); p.recordsPanel.transform.SetParent(rt, false);

            p.start = NewButton(p.main.transform, "Start", out _);
            p.shop = NewButton(p.main.transform, "Shop", out _);
            p.records = NewButton(p.main.transform, "Records", out _);
            p.reset = NewButton(p.main.transform, "Reset", out p.resetLabel);
            p.quit = NewButton(p.main.transform, "Quit", out _);
            p.sound = NewButton(p.main.transform, "Sound", out p.soundLabel);
            p.shake = NewButton(p.main.transform, "Shake", out p.shakeLabel);
            p.gold = NewText(p.main.transform, "Gold");

            p.shopClose = NewButton(p.shopPanel.transform, "Close", out _);
            p.shopGold = NewText(p.shopPanel.transform, "ShopGold");
            var rowsGo = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rowsGo.transform.SetParent(p.shopPanel.transform, false);
            p.rows = (RectTransform)rowsGo.transform;
            p.shopUi = p.shopPanel.AddComponent<MetaShopUI>();
            var uiSo = new SerializedObject(p.shopUi);
            uiSo.FindProperty("container").objectReferenceValue = p.rows;
            uiSo.FindProperty("goldLabel").objectReferenceValue = p.shopGold;
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            p.recordsClose = NewButton(p.recordsPanel.transform, "Close", out _);
            p.recordsText = NewText(p.recordsPanel.transform, "Text");

            p.controller = p.root.AddComponent<TitleController>();
            if (sceneLoader != null) p.controller.SceneLoader = sceneLoader;
            if (quit != null) p.controller.QuitAction = quit;

            var so = new SerializedObject(p.controller);
            so.FindProperty("mainPanel").objectReferenceValue = p.main;
            so.FindProperty("shopPanel").objectReferenceValue = p.shopPanel;
            so.FindProperty("recordsPanel").objectReferenceValue = p.recordsPanel;
            so.FindProperty("startButton").objectReferenceValue = p.start;
            so.FindProperty("shopButton").objectReferenceValue = p.shop;
            so.FindProperty("recordsButton").objectReferenceValue = p.records;
            so.FindProperty("resetButton").objectReferenceValue = p.reset;
            so.FindProperty("quitButton").objectReferenceValue = p.quit;
            so.FindProperty("shopCloseButton").objectReferenceValue = p.shopClose;
            so.FindProperty("recordsCloseButton").objectReferenceValue = p.recordsClose;
            so.FindProperty("shopUi").objectReferenceValue = p.shopUi;
            so.FindProperty("recordsText").objectReferenceValue = p.recordsText;
            so.FindProperty("goldLabel").objectReferenceValue = p.gold;
            so.FindProperty("resetLabel").objectReferenceValue = p.resetLabel;
            so.FindProperty("soundButton").objectReferenceValue = p.sound;
            so.FindProperty("soundLabel").objectReferenceValue = p.soundLabel;
            so.FindProperty("shakeButton").objectReferenceValue = p.shake;
            so.FindProperty("shakeLabel").objectReferenceValue = p.shakeLabel;
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();

            p.root.SetActive(true); // Awake / Start
            return p;
        }

        // ───────────────────────── 효과 문구 / 기록 문구 ─────────────────────────

        [Test]
        public void EffectText_FormatsEachStatKind()
        {
            Assert.AreEqual("+30", MetaProgression.EffectText(MakeMeta("hp", MetaStat.MaxHp, 10f), 3));
            Assert.AreEqual("+0.45/초", MetaProgression.EffectText(MakeMeta("rg", MetaStat.Regen, 0.15f), 3));
            Assert.AreEqual("+12%", MetaProgression.EffectText(MakeMeta("dm", MetaStat.Damage, 0.04f), 3));
            Assert.AreEqual("+0%", MetaProgression.EffectText(MakeMeta("dm2", MetaStat.Damage, 0.04f), 0));
            Assert.AreEqual("+0%", MetaProgression.EffectText(MakeMeta("dm3", MetaStat.Damage, 0.04f), -2), "음수 레벨은 0으로 취급");
        }

        [Test]
        public void Records_ShowsStatsAndWinRate()
        {
            var save = new SaveData
            {
                gold = 530, totalRuns = 8, clears = 2, bestSeconds = 65.4f, bestKills = 321, lastHero = "여포"
            };
            save.SetUpgradeLevel("a", 2);
            save.SetUpgradeLevel("b", 3);

            string text = RecordsFormatter.Format(save);

            StringAssert.Contains("8회", text);
            StringAssert.Contains("2회", text);
            StringAssert.Contains("25%", text, "클리어 2 / 총 8 = 25%");
            StringAssert.Contains("01:05", text);
            StringAssert.Contains("321", text);
            StringAssert.Contains("여포", text);
            StringAssert.Contains("Lv.5", text, "강화 레벨 합계");
            StringAssert.Contains("530", text);
        }

        [Test]
        public void Records_FreshSave_HasNoDivisionByZero_AndShowsDash()
        {
            string text = null;
            Assert.DoesNotThrow(() => text = RecordsFormatter.Format(new SaveData()));

            StringAssert.Contains("0%", text);
            StringAssert.Contains("00:00", text);
            StringAssert.Contains("-", text, "마지막 장수가 없으면 대시");
        }

        // ───────────────────────── 상점 모델 ─────────────────────────

        [Test]
        public void Shop_ListsEntriesWithLevelCostAndEffects()
        {
            var hp = MakeMeta("hp", MetaStat.MaxHp, 10f, maxLevel: 3, baseCost: 100, growth: 2f);
            var dmg = MakeMeta("dmg", MetaStat.Damage, 0.05f, maxLevel: 2);
            SaveSystem.Current.SetUpgradeLevel("hp", 1);
            GiveGold(250);

            var shop = new MetaShop(MakeCatalog(hp, dmg));

            Assert.AreEqual(2, shop.Entries.Count);
            Assert.AreEqual(250, shop.Gold);

            var e = shop.Entries[0];
            Assert.AreSame(hp, e.Upgrade);
            Assert.AreEqual(1, e.Level);
            Assert.AreEqual(3, e.MaxLevel);
            Assert.AreEqual(200, e.Cost, "1레벨 → 2레벨 비용");
            Assert.IsFalse(e.IsMax);
            Assert.IsTrue(e.CanBuy);
            Assert.AreEqual("+10", e.EffectText);
            Assert.AreEqual("+20", e.NextEffectText);

            Assert.AreEqual(0, shop.Entries[1].Level);
            Assert.AreEqual(100, shop.Entries[1].Cost);
        }

        [Test]
        public void Shop_Purchase_SpendsGold_Saves_AndNotifies()
        {
            var hp = MakeMeta("hp", MetaStat.MaxHp, 10f, baseCost: 100, growth: 2f);
            GiveGold(350);
            var shop = new MetaShop(MakeCatalog(hp));
            int changed = 0;
            shop.Changed += () => changed++;

            Assert.IsTrue(shop.Purchase(hp));    // 100
            Assert.IsTrue(shop.Purchase(hp));    // 200

            Assert.AreEqual(2, changed);
            Assert.AreEqual(50, shop.Gold);
            Assert.AreEqual(2, shop.Entries[0].Level);
            Assert.IsFalse(shop.Entries[0].CanBuy, "다음 비용 400 > 50");

            SaveSystem.ResetCache();
            var saved = SaveSystem.Load();
            Assert.AreEqual(50, saved.gold, "구매 즉시 파일에 저장");
            Assert.AreEqual(2, saved.GetUpgradeLevel("hp"));
        }

        [Test]
        public void Shop_Purchase_FailsWithoutGold_OrAtMax_WithoutChangingAnything()
        {
            var hp = MakeMeta("hp", MetaStat.MaxHp, 10f, maxLevel: 1, baseCost: 100);
            GiveGold(99);
            var shop = new MetaShop(MakeCatalog(hp));
            int changed = 0;
            shop.Changed += () => changed++;

            Assert.IsFalse(shop.Purchase(hp), "골드 부족");
            Assert.AreEqual(99, shop.Gold);
            Assert.AreEqual(0, changed, "실패는 Changed 를 보내지 않음");

            GiveGold(500);
            shop.Refresh();
            Assert.IsTrue(shop.Purchase(hp));
            Assert.IsTrue(shop.Entries[0].IsMax);
            Assert.AreEqual("", shop.Entries[0].NextEffectText);
            Assert.AreEqual(-1, shop.Entries[0].Cost);

            Assert.IsFalse(shop.Purchase(hp), "최대 레벨");
            Assert.AreEqual(400, shop.Gold);
        }

        [Test]
        public void Shop_NullCatalog_IsEmpty_AndSkipsNullUpgrades()
        {
            Assert.AreEqual(0, new MetaShop(null).Entries.Count);

            var hp = MakeMeta("hp", MetaStat.MaxHp);
            var withNull = MakeCatalog(hp, null);
            Assert.AreEqual(1, new MetaShop(withNull).Entries.Count);
        }

        // ───────────────────────── 상점 UI ─────────────────────────

        [UnityTest]
        public IEnumerator ShopUI_BuildsOneRowPerUpgrade_AndReflectsAffordability()
        {
            var cheap = MakeMeta("cheap", MetaStat.MaxHp, baseCost: 50);
            var pricey = MakeMeta("pricey", MetaStat.Damage, baseCost: 500);
            GiveGold(100);
            var p = BuildTitle(MakeCatalog(cheap, pricey));
            yield return null;

            p.controller.OpenShop();
            var buttons = p.rows.GetComponentsInChildren<Button>();

            Assert.AreEqual(2, p.shopUi.RowCount);
            Assert.AreEqual(2, buttons.Length);
            Assert.IsTrue(buttons[0].interactable, "100G 로 50G 강화는 살 수 있음");
            Assert.IsFalse(buttons[1].interactable, "500G 강화는 살 수 없음");
            StringAssert.Contains("50G", buttons[0].GetComponentInChildren<Text>().text);
            StringAssert.Contains("100", p.shopGold.text, "보유 골드 표시");
        }

        [UnityTest]
        public IEnumerator ShopUI_ClickingBuy_PurchasesAndRefreshesEverything()
        {
            var hp = MakeMeta("hp", MetaStat.MaxHp, 10f, maxLevel: 2, baseCost: 100, growth: 2f);
            GiveGold(350);
            var p = BuildTitle(MakeCatalog(hp));
            yield return null;
            p.controller.OpenShop();

            var button = p.rows.GetComponentInChildren<Button>();
            var label = button.GetComponentInChildren<Text>();

            button.onClick.Invoke(); // 100G
            Assert.AreEqual(250, SaveSystem.Current.gold);
            StringAssert.Contains("250", p.shopGold.text, "상점 골드 즉시 갱신");
            StringAssert.Contains("250", p.gold.text, "타이틀 골드도 갱신");
            StringAssert.Contains("200G", label.text, "다음 비용 표시");
            var info = p.rows.GetComponentInChildren<Text>().text;
            StringAssert.Contains("Lv.1 / 2", info);

            button.onClick.Invoke(); // 200G
            Assert.AreEqual(50, SaveSystem.Current.gold);
            Assert.AreEqual("MAX", label.text);
            Assert.IsFalse(button.interactable, "최대 레벨이면 비활성");
            StringAssert.Contains("Lv.2 / 2", p.rows.GetComponentInChildren<Text>().text);

            button.onClick.Invoke(); // 최대 레벨 — 아무 일도 없어야 함
            Assert.AreEqual(50, SaveSystem.Current.gold);
        }

        [UnityTest]
        public IEnumerator ShopUI_Bind_Twice_DoesNotDuplicateRows()
        {
            var hp = MakeMeta("hp", MetaStat.MaxHp);
            var dmg = MakeMeta("dmg", MetaStat.Damage);
            var p = BuildTitle(MakeCatalog(hp, dmg));
            yield return null;

            p.shopUi.Bind(new MetaShop(MakeCatalog(hp, dmg)));
            yield return null; // 이전 행 Destroy 반영

            Assert.AreEqual(2, p.shopUi.RowCount);
            Assert.AreEqual(2, p.rows.GetComponentsInChildren<Button>().Length, "다시 연결해도 행이 중복되지 않음");
        }

        // ───────────────────────── 타이틀 컨트롤러 ─────────────────────────

        [UnityTest]
        public IEnumerator Title_StartsOnMainPanel_AndAllReferencesAreWired()
        {
            GiveGold(77);
            var p = BuildTitle(MakeCatalog(MakeMeta("hp", MetaStat.MaxHp)));
            yield return null;

            CollectionAssert.IsEmpty(p.controller.MissingReferences());
            Assert.IsTrue(p.controller.IsMainVisible);
            Assert.IsFalse(p.controller.IsShopVisible);
            Assert.IsFalse(p.controller.IsRecordsVisible);
            StringAssert.Contains("77", p.gold.text);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Title_ButtonsSwitchPanels_AndCloseReturnsToMain()
        {
            var p = BuildTitle(MakeCatalog(MakeMeta("hp", MetaStat.MaxHp)));
            yield return null;

            p.shop.onClick.Invoke();
            Assert.IsTrue(p.controller.IsShopVisible);
            Assert.IsFalse(p.controller.IsMainVisible);
            p.shopClose.onClick.Invoke();
            Assert.IsTrue(p.controller.IsMainVisible);
            Assert.IsFalse(p.controller.IsShopVisible);

            SaveSystem.Current.bestKills = 42;
            p.records.onClick.Invoke();
            Assert.IsTrue(p.controller.IsRecordsVisible);
            Assert.IsFalse(p.controller.IsMainVisible);
            StringAssert.Contains("42", p.recordsText.text, "기록 화면은 열 때마다 최신 저장 값을 보여줌");
            p.recordsClose.onClick.Invoke();
            Assert.IsTrue(p.controller.IsMainVisible);
            Assert.IsFalse(p.controller.IsRecordsVisible);
        }

        [UnityTest]
        public IEnumerator Title_StartLoadsGameScene_QuitCallsQuitAction()
        {
            string loaded = null;
            int quits = 0;
            var p = BuildTitle(MakeCatalog(), s => loaded = s, () => quits++);
            yield return null;

            p.start.onClick.Invoke();
            Assert.AreEqual(GameManager.GameSceneName, loaded);
            Assert.AreEqual("SampleScene", loaded);

            p.quit.onClick.Invoke();
            Assert.AreEqual(1, quits);
        }

        [UnityTest]
        public IEnumerator Title_Reset_RequiresSecondClick_ThenWipesSave()
        {
            var hp = MakeMeta("hp", MetaStat.MaxHp, baseCost: 10);
            SaveSystem.Current.SetUpgradeLevel("hp", 2);
            SaveSystem.Current.totalRuns = 9;
            GiveGold(999);
            var p = BuildTitle(MakeCatalog(hp));
            yield return null;
            string idle = p.resetLabel.text;

            p.reset.onClick.Invoke();
            Assert.IsTrue(p.controller.ResetArmed, "첫 클릭은 확인 대기");
            Assert.AreNotEqual(idle, p.resetLabel.text, "확인 문구로 바뀜");
            Assert.AreEqual(999, SaveSystem.Current.gold, "아직 지워지지 않음");
            Assert.IsTrue(File.Exists(savePath));

            p.reset.onClick.Invoke();
            Assert.IsFalse(p.controller.ResetArmed);
            Assert.AreEqual(idle, p.resetLabel.text, "문구 복원");
            Assert.IsFalse(File.Exists(savePath), "저장 파일 삭제");
            Assert.AreEqual(0, SaveSystem.Current.gold);
            Assert.AreEqual(0, SaveSystem.Current.totalRuns);
            Assert.AreEqual(0, p.controller.Shop.Entries[0].Level, "상점도 초기화된 상태를 반영");
            StringAssert.Contains("0", p.gold.text);
        }

        [UnityTest]
        public IEnumerator Title_ResetConfirmation_ExpiresAfterTimeout()
        {
            GiveGold(500);
            var p = BuildTitle(MakeCatalog(MakeMeta("hp", MetaStat.MaxHp)));
            p.controller.ResetConfirmSeconds = 0.2f;
            yield return null;
            string idle = p.resetLabel.text;

            p.reset.onClick.Invoke();
            Assert.IsTrue(p.controller.ResetArmed);

            yield return new WaitForSecondsRealtime(0.5f);

            Assert.IsFalse(p.controller.ResetArmed, "시간이 지나면 확인 대기 해제");
            Assert.AreEqual(idle, p.resetLabel.text);

            p.reset.onClick.Invoke(); // 다시 첫 클릭부터
            Assert.AreEqual(500, SaveSystem.Current.gold, "시간 초과 후의 클릭은 지우지 않음");
        }

        [UnityTest]
        public IEnumerator Title_LeavingMainPanel_CancelsPendingReset()
        {
            GiveGold(500);
            var p = BuildTitle(MakeCatalog(MakeMeta("hp", MetaStat.MaxHp)));
            yield return null;

            p.reset.onClick.Invoke();
            Assert.IsTrue(p.controller.ResetArmed);
            p.controller.OpenShop();
            p.controller.ShowMain();

            Assert.IsFalse(p.controller.ResetArmed, "화면을 오가면 확인 대기가 풀림");
            Assert.AreEqual(500, SaveSystem.Current.gold);
        }

        [UnityTest]
        public IEnumerator Title_MissingReferences_ReportsWhatIsNotWired()
        {
            var go = new GameObject("EmptyTitle");
            toDestroy.Add(go);
            go.SetActive(false);
            var controller = go.AddComponent<TitleController>();
            yield return null;

            var missing = controller.MissingReferences();

            CollectionAssert.Contains(missing, "startButton");
            CollectionAssert.Contains(missing, "catalog");
            Assert.AreEqual(19, missing.Count);
        }

        [UnityTest]
        public IEnumerator Title_SoundButton_CyclesVolume_ShowsLabel_AndSaves()
        {
            SaveSystem.Current.sfxVolume = 0.7f;
            var p = BuildTitle(MakeCatalog());
            yield return null;
            StringAssert.Contains("보통", p.soundLabel.text);

            p.sound.onClick.Invoke();
            Assert.AreEqual(1f, SaveSystem.Current.sfxVolume, 0.001f);
            StringAssert.Contains("크게", p.soundLabel.text);

            p.sound.onClick.Invoke();
            Assert.AreEqual(0f, SaveSystem.Current.sfxVolume, 0.001f, "끝에서 처음(끔)으로");
            StringAssert.Contains("끔", p.soundLabel.text);

            SaveSystem.ResetCache();
            Assert.AreEqual(0f, SaveSystem.Load().sfxVolume, 0.001f, "설정이 파일에 저장됨");

            p.sound.onClick.Invoke();
            Assert.AreEqual(0.35f, SaveSystem.Current.sfxVolume, 0.001f);
            StringAssert.Contains("작게", p.soundLabel.text);
            Audio.AudioManager.DestroyInstance();
        }

        [UnityTest]
        public IEnumerator Title_ShakeButton_TogglesSetting_ShowsLabel_AndSaves()
        {
            var p = BuildTitle(MakeCatalog());
            yield return null;
            Assert.IsTrue(SaveSystem.Current.screenShake, "기본값은 켬");
            StringAssert.Contains("켬", p.shakeLabel.text);

            p.shake.onClick.Invoke();
            Assert.IsFalse(SaveSystem.Current.screenShake);
            StringAssert.Contains("끔", p.shakeLabel.text);

            SaveSystem.ResetCache();
            Assert.IsFalse(SaveSystem.Load().screenShake, "설정이 파일에 저장됨");

            p.shake.onClick.Invoke();
            Assert.IsTrue(SaveSystem.Current.screenShake);
            StringAssert.Contains("켬", p.shakeLabel.text);
            Audio.AudioManager.DestroyInstance();
        }

        // ───────────────────────── 실제 씬 / 빌드 설정 ─────────────────────────

        [Test]
        public void BuildSettings_ListTitleFirst_ThenGame_BothEnabled()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.GreaterOrEqual(scenes.Length, 2, "Samkuk > Step 9-2 를 먼저 실행하세요 (빌드 설정에 씬 등록)");

            Assert.AreEqual(TitleScenePath, scenes[0].path, "타이틀 씬이 0번");
            Assert.AreEqual(GameScenePath, scenes[1].path, "게임 씬이 1번");
            Assert.IsTrue(scenes[0].enabled);
            Assert.IsTrue(scenes[1].enabled);
        }

        [Test]
        public void SceneNames_MatchTheSceneAssets()
        {
            Assert.AreEqual("TitleScene", GameManager.TitleSceneName);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(TitleScenePath), GameManager.TitleSceneName);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(GameScenePath), GameManager.GameSceneName);
            Assert.IsTrue(File.Exists(TitleScenePath), "TitleScene.unity 가 없습니다. Step 9-2 를 실행하세요.");
        }
    }
}
