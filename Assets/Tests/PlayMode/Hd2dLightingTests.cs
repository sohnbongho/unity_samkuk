using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.World;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    /// <summary>HD-2D 조명(Step 14-1): 성 시간대 → 전역광 색조, 소품 점광원, 플레이어 빛, 켜고 끄기.</summary>
    public class Hd2dLightingTests
    {
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            MapStore.Disabled = true;
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
            GameSession.SortieCastle = null;
            Hd2dSettings.LightingOverride = true;
        }

        [TearDown]
        public void TearDown()
        {
            MapStore.Disabled = false;
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
            GameSession.SortieCastle = null;
            Hd2dSettings.ResetOverrides();
            foreach (var o in toDestroy) if (o != null) Object.DestroyImmediate(o);
            toDestroy.Clear();
        }

        // ───────────────────────── 시간대 ─────────────────────────

        /// <summary>
        /// 성 배경 생성기(tools/castle_art, .NET Framework)가 같은 시드로 뽑은 값과 같아야 전투 맵 색조가 성 그림의 하늘과 맞는다.
        /// 기대값은 생성기와 같은 코드를 PowerShell 로 돌려 얻었다 (0 낮, 1 해질녘, 2 새벽).
        /// </summary>
        [TestCase("Luoyang", TimeOfDay.Dusk)]
        [TestCase("Chengdu", TimeOfDay.Day)]
        [TestCase("Xiangping", TimeOfDay.Dawn)]
        [TestCase("Jianye", TimeOfDay.Dusk)]
        [TestCase("Nanhai", TimeOfDay.Day)]
        [TestCase("Shouchun", TimeOfDay.Dawn)]
        [TestCase("Wan", TimeOfDay.Day)]
        public void CastleMood_MatchesCastleArtGenerator(string id, TimeOfDay expected)
        {
            Assert.AreEqual(expected, CastleMood.Of(id));
        }

        [Test]
        public void CastleMood_NoCastleOrFreeBattle_IsDay()
        {
            Assert.AreEqual(TimeOfDay.Day, CastleMood.Of((CastleData)null));
            Assert.AreEqual(TimeOfDay.Day, CastleMood.Of(TerrainMap.FreeBattleCastle));
        }

        // ───────────────────────── 수치표 ─────────────────────────

        [Test]
        public void LightingPreset_EveryCombination_StaysBrightEnough()
        {
            foreach (CastleTerrain terrain in System.Enum.GetValues(typeof(CastleTerrain)))
                foreach (TimeOfDay time in System.Enum.GetValues(typeof(TimeOfDay)))
                {
                    var g = LightingPreset.Global(terrain, time);
                    Assert.GreaterOrEqual(g.color.r, LightingPreset.MinChannel, $"{terrain}/{time} r");
                    Assert.GreaterOrEqual(g.color.g, LightingPreset.MinChannel, $"{terrain}/{time} g");
                    Assert.GreaterOrEqual(g.color.b, LightingPreset.MinChannel, $"{terrain}/{time} b");
                    Assert.LessOrEqual(g.color.maxColorComponent, 1f, $"{terrain}/{time} 과다 노출");
                    Assert.That(g.intensity, Is.InRange(0.8f, 1.1f), $"{terrain}/{time} 세기");
                }
        }

        [Test]
        public void LightingPreset_Dusk_IsWarmerThanDay()
        {
            var day = LightingPreset.Global(CastleTerrain.Plain, TimeOfDay.Day).color;
            var dusk = LightingPreset.Global(CastleTerrain.Plain, TimeOfDay.Dusk).color;
            Assert.Greater(dusk.r - dusk.b, day.r - day.b, "해질녘은 주황빛");
            Assert.Less(dusk.b, day.b);
        }

        [Test]
        public void LightingPreset_Terrains_DifferFromEachOther()
        {
            var plain = LightingPreset.Global(CastleTerrain.Plain, TimeOfDay.Day).color;
            var mountain = LightingPreset.Global(CastleTerrain.Mountain, TimeOfDay.Day).color;
            var loess = LightingPreset.Global(CastleTerrain.Loess, TimeOfDay.Day).color;
            Assert.Less(mountain.r, plain.r, "산악은 서늘하다");
            Assert.Less(loess.b, plain.b, "황토는 따뜻하다");
        }

        // ───────────────────────── 소품 빛 기본값 ─────────────────────────

        [Test]
        public void PropKinds_LightDefaults_BannerAndPondOnly()
        {
            var banner = new TerrainProp(); TerrainPropKinds.ApplyLightDefaults(banner, "banner");
            var pond = new TerrainProp(); TerrainPropKinds.ApplyLightDefaults(pond, "pond");
            var tree = new TerrainProp(); TerrainPropKinds.ApplyLightDefaults(tree, "tree");
            var grass = new TerrainProp(); TerrainPropKinds.ApplyLightDefaults(grass, "tuft");

            Assert.IsTrue(banner.HasLight);
            Assert.Greater(banner.lightFlicker, 0f, "횃불은 일렁인다");
            Assert.Greater(banner.lightColor.r, banner.lightColor.b, "횃불은 따뜻한 색");
            Assert.Greater(banner.lightHeight, 0f, "횃불은 깃대 위에 달린다");
            Assert.IsTrue(pond.HasLight);
            Assert.Greater(pond.lightColor.b, pond.lightColor.r, "연못은 푸른 빛");
            Assert.IsFalse(tree.HasLight);
            Assert.IsFalse(grass.HasLight);
        }

        [Test]
        public void TerrainProp_NewProp_HasNoLight()
        {
            Assert.IsFalse(new TerrainProp().HasLight, "값을 채우기 전(예전 에셋)에는 빛이 없어야 한다");
        }

        // ───────────────────────── 소품 점광원 ─────────────────────────

        Sprite MakeSprite()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.1f), 8f);
            toDestroy.Add(tex);
            toDestroy.Add(sprite);
            return sprite;
        }

        /// <summary>소품 없는 평야 테마. 원점 칸에 빛이 있는 깃발 하나와 빛이 없는 바위 하나를 직접 놓는다.</summary>
        TerrainMap MakeMapWithBanner(out TerrainProp banner)
        {
            var c = ScriptableObject.CreateInstance<TerrainThemeCatalog>();
            var t = ScriptableObject.CreateInstance<TerrainTheme>();
            t.terrain = CastleTerrain.Plain;
            t.propsPerChunk = 0f;
            t.groundTile = MakeSprite();
            var rock = new TerrainProp { name = "Rock", sprite = MakeSprite() };
            t.props.Add(rock);
            c.themes.Add(t);
            c.banner.sprite = MakeSprite();
            TerrainPropKinds.ApplyLightDefaults(c.banner, "banner");
            banner = c.banner;
            toDestroy.Add(t);
            toDestroy.Add(c);
            TerrainThemeCatalog.Use(c);

            var castle = ScriptableObject.CreateInstance<CastleData>();
            castle.id = "LightTest"; castle.displayName = "LightTest"; castle.terrain = CastleTerrain.Plain;
            toDestroy.Add(castle);

            var map = TerrainMap.Create(castle, c);
            map.SetCustomChunk(TerrainMap.ChunkOf(Vector2.zero), new List<PropPlacement>
            {
                new PropPlacement { prop = c.banner, position = new Vector2(3f, 2f), scale = 1.5f, stretchY = 1f, order = TerrainMap.OrderPropBase },
                new PropPlacement { prop = rock, position = new Vector2(6f, 6f), scale = 1f, stretchY = 1f, order = TerrainMap.OrderPropBase },
            });
            return map;
        }

        TerrainPropSpawner MakeSpawner(TerrainMap map)
        {
            var follow = new GameObject("Follow");
            toDestroy.Add(follow);
            var go = new GameObject("TerrainProps");
            toDestroy.Add(go);
            var spawner = go.AddComponent<TerrainPropSpawner>();
            spawner.Initialize(map, follow.transform, null);
            return spawner;
        }

        [Test]
        public void Spawner_MakesPointLight_ForLitProps_Only()
        {
            var map = MakeMapWithBanner(out var banner);
            var spawner = MakeSpawner(map);

            Assert.IsTrue(spawner.LightsEnabled);
            Assert.AreEqual(1, spawner.ActiveLightCount, "깃발 하나만 빛이 있다 (바위는 없음)");

            var lights = spawner.GetComponentsInChildren<Light2D>();
            Assert.AreEqual(1, lights.Length);
            var l = lights[0];
            Assert.AreEqual(Light2D.LightType.Point, l.lightType);
            Assert.AreEqual(banner.lightRadius * 1.5f, l.pointLightOuterRadius, 1e-4f, "반지름은 소품 크기 배율을 곱한다");
            Assert.AreEqual(3f, l.transform.position.x, 1e-4f);
            Assert.AreEqual(2f + banner.lightHeight * 1.5f, l.transform.position.y, 1e-4f, "횃불은 깃대 위");
            Assert.AreEqual(banner.lightColor, l.color);
        }

        [Test]
        public void Spawner_NoLights_WhenLightingDisabled()
        {
            Hd2dSettings.LightingOverride = false;
            var map = MakeMapWithBanner(out _);
            var spawner = MakeSpawner(map);

            Assert.IsFalse(spawner.LightsEnabled);
            Assert.AreEqual(0, spawner.ActiveLightCount);
            Assert.AreEqual(0, spawner.GetComponentsInChildren<Light2D>().Length);
            Assert.AreEqual(2, spawner.ActivePropCount, "소품 자체는 그대로");
        }

        [Test]
        public void Spawner_ToggleLights_RebuildsAndPools()
        {
            var map = MakeMapWithBanner(out _);
            var spawner = MakeSpawner(map);
            Assert.AreEqual(1, spawner.ActiveLightCount);

            spawner.SetLightsEnabled(false);
            Assert.AreEqual(0, spawner.ActiveLightCount);
            Assert.AreEqual(0, spawner.GetComponentsInChildren<Light2D>(false).Length, "꺼진 빛은 비활성(풀)");

            spawner.SetLightsEnabled(true);
            Assert.AreEqual(1, spawner.ActiveLightCount);
            Assert.AreEqual(1, spawner.GetComponentsInChildren<Light2D>(true).Length, "풀에서 다시 꺼내 쓴다 (새로 만들지 않음)");
        }

        [Test]
        public void Spawner_Flicker_StaysWithinRange_AndIsSmooth()
        {
            var map = MakeMapWithBanner(out var banner);
            var spawner = MakeSpawner(map);
            var l = spawner.GetComponentsInChildren<Light2D>()[0];

            float min = banner.lightIntensity * (1f - banner.lightFlicker), max = banner.lightIntensity;
            float prev = -1f, maxStep = 0f;
            for (int i = 0; i < 200; i++)
            {
                spawner.TickFlicker(i * (1f / 60f));
                Assert.That(l.intensity, Is.InRange(min - 1e-4f, max + 1e-4f));
                if (prev >= 0f) maxStep = Mathf.Max(maxStep, Mathf.Abs(l.intensity - prev));
                prev = l.intensity;
            }
            Assert.Less(maxStep, banner.lightIntensity * banner.lightFlicker * 0.5f, "한 프레임에 확 튀지 않는다 (깜빡임이 아니라 일렁임)");
        }

        // ───────────────────────── 전역광 ─────────────────────────

        Light2D MakeGlobalLight()
        {
            var go = new GameObject("Global Light 2D");
            toDestroy.Add(go);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = Color.white;
            light.intensity = 1f;
            return light;
        }

        [UnityTest]
        public IEnumerator BattleLighting_TintsGlobalLight_AndRestoresWhenDestroyed()
        {
            var global = MakeGlobalLight();
            var go = new GameObject("Lighting");
            toDestroy.Add(go);
            var lighting = go.AddComponent<BattleLighting>();
            lighting.Initialize(CastleTerrain.Mountain, TimeOfDay.Dusk, global);

            var expected = LightingPreset.Global(CastleTerrain.Mountain, TimeOfDay.Dusk);
            Assert.IsTrue(lighting.IsApplied);
            Assert.AreEqual(expected.color, global.color);
            Assert.AreEqual(expected.intensity, global.intensity, 1e-5f);
            Assert.IsNotNull(lighting.PlayerGlow, "플레이어 주변 빛");
            Assert.AreEqual(Light2D.LightType.Point, lighting.PlayerGlow.lightType);

            Object.Destroy(go);
            yield return null;

            Assert.AreEqual(Color.white, global.color, "사라지면 전역광을 되돌린다");
            Assert.AreEqual(1f, global.intensity, 1e-5f);
        }

        [Test]
        public void BattleLighting_Disabled_LeavesGlobalLightAlone()
        {
            Hd2dSettings.LightingOverride = false;
            var global = MakeGlobalLight();
            var go = new GameObject("Lighting");
            toDestroy.Add(go);
            var lighting = go.AddComponent<BattleLighting>();
            lighting.Initialize(CastleTerrain.Loess, TimeOfDay.Dawn, global);

            Assert.IsFalse(lighting.IsApplied);
            Assert.AreEqual(Color.white, global.color);
            Assert.IsNull(lighting.PlayerGlow);

            lighting.Apply(true);   // F5 로 켜면 그제야 입힌다
            Assert.IsTrue(lighting.IsApplied);
            Assert.AreNotEqual(Color.white, global.color);
            lighting.Apply(false);
            Assert.AreEqual(Color.white, global.color);
        }

        // ───────────────────────── 배경과의 연결 ─────────────────────────

        InfiniteBackground MakeBackground()
        {
            var go = new GameObject("TestBg", typeof(SpriteRenderer));
            toDestroy.Add(go);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = MakeSprite();
            sr.sortingLayerName = GameLayers.Sorting.Background;
            return go.AddComponent<InfiniteBackground>();
        }

        [Test]
        public void Background_ApplyCastle_UsesCastleTimeOfDay()
        {
            MakeMapWithBanner(out _);   // 카탈로그 등록
            var castle = ScriptableObject.CreateInstance<CastleData>();
            castle.id = "Luoyang"; castle.displayName = "낙양"; castle.terrain = CastleTerrain.Plain;   // 생성기 기준 해질녘
            toDestroy.Add(castle);
            var bg = MakeBackground();

            bg.ApplyCastle(castle);
            Assert.IsNotNull(bg.Lighting);
            Assert.AreEqual(TimeOfDay.Dusk, bg.Lighting.TimeOfDay);
            Assert.AreEqual(CastleTerrain.Plain, bg.Lighting.Terrain);
            Assert.IsTrue(bg.Props.LightsEnabled);
        }

        [Test]
        public void Background_LightingNotAllowed_SkipsLightsEntirely()
        {
            var map = MakeMapWithBanner(out _);
            var bg = MakeBackground();
            bg.LightingAllowed = false;   // 맵 편집기

            bg.ApplyMap(map);
            Assert.IsNull(bg.Lighting);
            Assert.IsFalse(bg.Props.LightsEnabled);
            Assert.AreEqual(0, bg.Props.ActiveLightCount);
        }

        [Test]
        public void Hd2dSettings_Override_WinsOverSave()
        {
            Hd2dSettings.LightingOverride = false;
            Assert.IsFalse(Hd2dSettings.Lighting);
            Hd2dSettings.LightingOverride = true;
            Assert.IsTrue(Hd2dSettings.Lighting);
        }
    }
}
