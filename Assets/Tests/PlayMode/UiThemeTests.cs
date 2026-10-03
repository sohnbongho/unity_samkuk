using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Samkuk.Data;
using Samkuk.Meta;
using Samkuk.UI;
using Samkuk.Upgrades;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Samkuk.Tests
{
    public class UiThemeTests
    {
        const string ThemePath = "Assets/Resources/UiTheme.asset";

        string savePath;
        readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            savePath = Path.Combine(Application.temporaryCachePath, $"test_theme_save_{Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = savePath;
            SaveSystem.ResetCache();
        }

        [TearDown]
        public void TearDown()
        {
            UiTheme.ResetCache();
            SaveSystem.Delete();
            SaveSystem.PathOverride = null;
            SaveSystem.ResetCache();
            foreach (var o in toDestroy) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            toDestroy.Clear();
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        Sprite MakeSprite(string name, int border)
        {
            var tex = new Texture2D(16, 16);
            toDestroy.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = name;
            toDestroy.Add(sprite);
            return sprite;
        }

        /// <summary>스프라이트를 갖춘 테스트용 테마를 만들어 현재 테마로 쓴다.</summary>
        UiTheme UseTestTheme(bool withSprites = true)
        {
            var t = ScriptableObject.CreateInstance<UiTheme>();
            toDestroy.Add(t);
            if (withSprites)
            {
                t.fill = MakeSprite("fill", 4);
                t.frame = MakeSprite("frame", 6);
                t.frameThin = MakeSprite("frameThin", 3);
            }
            UiTheme.Use(t);
            return t;
        }

        GameObject NewGo(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, components.Length > 0 ? components : new[] { typeof(RectTransform) });
            go.transform.SetParent(parent, false);
            return go;
        }

        Image NewImage(string name, Transform parent, bool button = false)
        {
            var go = NewGo(name, parent, typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = new Color(0.1f, 0.2f, 0.3f, 1f); // 스킨이 바꾸기 전의 임의의 색
            if (button) go.AddComponent<Button>().targetGraphic = img;
            return img;
        }

        Text NewText(string name, Transform parent)
        {
            var go = NewGo(name, parent, typeof(RectTransform), typeof(Text));
            var t = go.GetComponent<Text>();
            t.color = Color.white;
            return t;
        }

        static Transform FrameOf(Component c) => c.transform.Find(UiSkin.FrameName);

        static int FrameCount(Transform t)
        {
            int n = 0;
            foreach (Transform c in t) if (c.name == UiSkin.FrameName) n++;
            return n;
        }

        // ───────────────────────── 테마 ─────────────────────────

        [Test]
        public void Theme_Get_NeverReturnsNull_AndHasUsableDefaults()
        {
            UiTheme.Use(null);
            var t = UiTheme.Get();
            Assert.IsNotNull(t);
            Assert.Greater(t.gold.a, 0f);
            Assert.Greater(t.panel.a, 0f);
            Assert.AreNotEqual(t.newWeapon, t.weaponUp);
            Assert.AreNotEqual(t.passive, t.heal);
            Assert.Greater(t.evolve.r, t.evolve.b, "진화색은 금빛 계열");
            Assert.AreSame(t, UiTheme.Get(), "한 번 불러온 테마는 캐시");
        }

        [Test]
        public void Theme_Use_OverridesCurrentTheme()
        {
            var custom = UseTestTheme();
            Assert.AreSame(custom, UiTheme.Get());
        }

        [Test]
        public void ThemeAsset_ExistsWithAllSprites_AndSliceBorders()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            Assert.IsNotNull(theme, "UiTheme.asset 이 없습니다. Samkuk > Step 10-3 를 먼저 실행하세요.");

            Assert.IsNotNull(theme.fill, "fill");
            Assert.IsNotNull(theme.frame, "frame");
            Assert.IsNotNull(theme.frameThin, "frameThin");
            Assert.IsNotNull(theme.divider, "divider");

            foreach (var s in new[] { theme.fill, theme.frame, theme.frameThin })
            {
                var b = s.border;
                Assert.Greater(b.x, 0f, $"{s.name}: 9-슬라이스 Border 필요");
                Assert.AreEqual(b.x, b.y); Assert.AreEqual(b.x, b.z); Assert.AreEqual(b.x, b.w);
                Assert.Less(b.x * 2f, s.rect.width, $"{s.name}: 가운데가 남아야 늘어날 수 있음");
            }
            Assert.Greater(theme.frame.border.x, theme.frameThin.border.x, "패널 프레임이 버튼 프레임보다 두꺼움");
            Assert.AreEqual(100f, theme.frame.pixelsPerUnit, "캔버스 기본 단위(100)와 같아 테두리가 픽셀 그대로 표시됨");
        }

        [Test]
        public void ThemeAsset_LoadsFromResources_ForRuntimeUse()
        {
            UiTheme.ResetCache();
            var t = UiTheme.Get();
            Assert.IsNotNull(t.frame, "Resources/UiTheme.asset 을 런타임에 불러올 수 있어야 함 (Step 10-3)");
            Assert.AreEqual(ThemePath, AssetDatabase.GetAssetPath(t));
        }

        // ───────────────────────── 이름 규칙 ─────────────────────────

        [Test]
        public void IsCardName_MatchesCardAndHeroWithNumber_Only()
        {
            Assert.IsTrue(UiSkin.IsCardName("Card1"));
            Assert.IsTrue(UiSkin.IsCardName("Card3"));
            Assert.IsTrue(UiSkin.IsCardName("Hero5"));
            Assert.IsFalse(UiSkin.IsCardName("HeroSelectPanel"));
            Assert.IsFalse(UiSkin.IsCardName("Heroes"));
            Assert.IsFalse(UiSkin.IsCardName("Card"));
            Assert.IsFalse(UiSkin.IsCardName("StartButton"));
            Assert.IsFalse(UiSkin.IsCardName(""));
            Assert.IsFalse(UiSkin.IsCardName(null));
        }

        [Test]
        public void CardTint_DiffersPerKind_AndMatchesTheme()
        {
            var t = UseTestTheme();
            var seen = new HashSet<Color>();
            foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind)))
                Assert.IsTrue(seen.Add(UiSkin.CardTint(kind)), $"{kind}: 다른 종류와 같은 색");

            Assert.AreEqual(t.evolve, UiSkin.CardTint(UpgradeKind.Evolve));
            Assert.AreEqual(t.passive, UiSkin.CardTint(UpgradeKind.Passive));
        }

        // ───────────────────────── 스킨 적용 ─────────────────────────

        [Test]
        public void Apply_StylesEachRole_AndLeavesOthersAlone()
        {
            var t = UseTestTheme();
            var root = NewGo("Canvas", null);
            toDestroy.Add(root);

            var box = NewImage("Box", root.transform);
            var card = NewImage("Card1", root.transform, button: true);
            var hero = NewImage("Hero2", root.transform, button: true);
            var start = NewImage("StartButton", root.transform, button: true);
            var quit = NewImage("QuitButton", root.transform, button: true);
            var plain = NewImage("계속하기  [ESC]", root.transform, button: true);
            var hpBar = NewImage("HpBar", root.transform);
            var dim = NewImage("HeroSelectPanel", root.transform);
            var fill = NewImage("Fill", hpBar.transform);
            var title = NewText("Title", root.transform);
            var cardTitle = NewText("Title", card.transform);

            Color hpBefore = hpBar.color, dimBefore = dim.color, fillBefore = fill.color;
            UiSkin.Apply(root.transform);

            // 패널
            Assert.AreSame(t.fill, box.sprite);
            Assert.AreEqual(Image.Type.Sliced, box.type);
            Assert.AreEqual(t.panel, box.color);
            Assert.AreSame(t.frame, FrameOf(box).GetComponent<Image>().sprite);

            // 카드
            Assert.AreEqual(t.card, card.color);
            Assert.AreEqual(t.card, hero.color);
            Assert.AreSame(t.frame, FrameOf(card).GetComponent<Image>().sprite, "카드는 두꺼운 프레임");
            Assert.IsNotNull(FrameOf(hero));

            // 버튼: 이름에 따라 색이 다르고 얇은 프레임
            Assert.AreEqual(t.primaryButton, start.color);
            Assert.AreEqual(t.mutedButton, quit.color);
            Assert.AreEqual(t.button, plain.color, "이름이 레이블 텍스트인 버튼도 버튼으로 인식");
            Assert.AreSame(t.frameThin, FrameOf(start).GetComponent<Image>().sprite);

            // 바: 색은 그대로, 얇은 프레임만
            Assert.AreEqual(hpBefore, hpBar.color);
            Assert.AreSame(t.frameThin, FrameOf(hpBar).GetComponent<Image>().sprite);
            Assert.IsNull(FrameOf(fill), "채움 이미지는 건드리지 않음");
            Assert.AreEqual(fillBefore, fill.color);

            // 규칙에 없는 이미지(전체 화면 어둠 등)는 그대로
            Assert.AreEqual(dimBefore, dim.color);
            Assert.IsNull(FrameOf(dim));

            // 제목: 금색 + 그림자, 카드 안의 제목은 그대로
            Assert.AreEqual(t.gold, title.color);
            Assert.IsNotNull(title.GetComponent<Shadow>());
            Assert.AreEqual(Color.white, cardTitle.color);
            Assert.IsNull(cardTitle.GetComponent<Shadow>());
        }

        [Test]
        public void Apply_IsIdempotent_AndFramesNeverBlockInputOrLayout()
        {
            UseTestTheme();
            var root = NewGo("Canvas", null);
            toDestroy.Add(root);
            var rowGo = NewGo("Rows", root.transform, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var card = NewImage("Card1", rowGo.transform, button: true);
            var title = NewText("Title", root.transform);

            UiSkin.Apply(root.transform);
            UiSkin.Apply(root.transform);
            UiSkin.Apply(root.transform);

            Assert.AreEqual(1, FrameCount(card.transform), "여러 번 적용해도 프레임은 하나");
            Assert.AreEqual(1, title.GetComponents<Shadow>().Length, "그림자도 중복되지 않음");

            var frame = FrameOf(card);
            Assert.IsFalse(frame.GetComponent<Image>().raycastTarget, "프레임이 클릭을 가로막지 않음");
            Assert.IsTrue(frame.GetComponent<LayoutElement>().ignoreLayout, "레이아웃 그룹 안에서도 자리를 차지하지 않음");
            Assert.AreEqual(card.transform.childCount - 1, frame.GetSiblingIndex(), "프레임은 맨 위에 그려짐");
        }

        [Test]
        public void Apply_WithoutSprites_StillColors_ButAddsNoFrames()
        {
            var t = UseTestTheme(withSprites: false);
            var root = NewGo("Canvas", null);
            toDestroy.Add(root);
            var card = NewImage("Card1", root.transform, button: true);
            var title = NewText("Title", root.transform);

            Assert.DoesNotThrow(() => UiSkin.Apply(root.transform));

            Assert.AreEqual(t.card, card.color, "스프라이트가 없어도 색은 적용");
            Assert.AreEqual(0, FrameCount(card.transform), "스프라이트가 없으면 프레임을 만들지 않음");
            Assert.IsNull(card.sprite);
            Assert.AreEqual(t.gold, title.color);
        }

        [Test]
        public void StyleButton_WithRecolorFalse_KeepsCurrentColor()
        {
            UseTestTheme();
            var root = NewGo("Canvas", null);
            toDestroy.Add(root);
            var img = NewImage("Buy", root.transform, button: true);
            var original = img.color;

            UiSkin.StyleButton(img.GetComponent<Button>(), recolor: false);

            Assert.AreEqual(original, img.color);
            Assert.IsNotNull(FrameOf(img), "프레임은 입힘");
        }

        [UnityTest]
        public IEnumerator SkinComponent_AppliesOnAwake_IncludingInactiveChildren()
        {
            var t = UseTestTheme();
            var root = NewGo("Canvas", null);
            toDestroy.Add(root);
            root.SetActive(false);
            var panel = NewGo("Panel", root.transform);
            var card = NewImage("Card1", panel.transform, button: true);
            panel.SetActive(false); // 숨겨진 화면(레벨업 패널 등)도 미리 입혀져야 한다

            root.AddComponent<UiSkin>();
            root.SetActive(true);
            yield return null;

            Assert.AreEqual(t.card, card.color);
            Assert.IsNotNull(FrameOf(card));
        }

        // ───────────────────────── 상점 행 ─────────────────────────

        [UnityTest]
        public IEnumerator ShopRows_AreSkinned_AndButtonsUseThemeColors()
        {
            var t = UseTestTheme();

            var cheap = ScriptableObject.CreateInstance<MetaUpgradeData>();
            cheap.name = "cheap"; cheap.displayName = "싼 강화"; cheap.baseCost = 50; cheap.maxLevel = 3;
            var pricey = ScriptableObject.CreateInstance<MetaUpgradeData>();
            pricey.name = "pricey"; pricey.displayName = "비싼 강화"; pricey.baseCost = 900; pricey.maxLevel = 3;
            var catalog = ScriptableObject.CreateInstance<MetaCatalog>();
            catalog.upgrades.Add(cheap); catalog.upgrades.Add(pricey);
            toDestroy.Add(cheap); toDestroy.Add(pricey); toDestroy.Add(catalog);
            SaveSystem.Current.gold = 100;
            SaveSystem.SaveCurrent();

            var go = NewGo("Shop", null, typeof(RectTransform), typeof(MetaShopUI));
            toDestroy.Add(go);
            var rows = NewGo("Rows", go.transform, typeof(RectTransform), typeof(VerticalLayoutGroup));
            var so = new SerializedObject(go.GetComponent<MetaShopUI>());
            so.FindProperty("container").objectReferenceValue = rows.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.GetComponent<MetaShopUI>().Bind(new MetaShop(catalog));
            yield return null;

            Assert.AreEqual(2, rows.transform.childCount);
            foreach (Transform row in rows.transform)
            {
                var rowImage = row.GetComponent<Image>();
                Assert.AreEqual(t.card, rowImage.color, "행은 카드 바탕");
                Assert.IsNotNull(FrameOf(rowImage), "행에 프레임");
                Assert.AreEqual(1, FrameCount(row), "프레임은 하나");
            }

            var buttons = rows.GetComponentsInChildren<Button>();
            Assert.AreEqual(t.positive, ((Image)buttons[0].targetGraphic).color, "살 수 있는 강화");
            Assert.AreEqual(t.disabled, ((Image)buttons[1].targetGraphic).color, "골드가 모자란 강화");
            Assert.IsNotNull(FrameOf(buttons[0].targetGraphic), "구매 버튼에도 프레임");
        }
    }
}
