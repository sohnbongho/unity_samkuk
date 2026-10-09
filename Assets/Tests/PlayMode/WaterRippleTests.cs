using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Feedback;
using Samkuk.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    /// <summary>물에 들어가면 발밑 물결 + 첨벙 (지형 이동의 연출).</summary>
    public class WaterRippleTests
    {
        readonly List<Object> toDestroy = new List<Object>();
        readonly List<GameObject> gos = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            MapStore.Disabled = true;
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
            GameSession.SortieCastle = null;
        }

        [TearDown]
        public void TearDown()
        {
            MapStore.Disabled = false;
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
            GameSession.SortieCastle = null;
            foreach (var g in gos) if (g != null) Object.Destroy(g);
            gos.Clear();
            foreach (var o in toDestroy) if (o != null) Object.Destroy(o);
            toDestroy.Clear();
        }

        Sprite MakeSprite(string name)
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            sprite.name = name;
            toDestroy.Add(tex);
            toDestroy.Add(sprite);
            return sprite;
        }

        /// <summary>소품 없는 평야 테마 + 연못. 연못을 원점에 놓아 "물 속"을 만든다.</summary>
        TerrainMap MakeMapWithPond(Vector2 pondAt)
        {
            var c = ScriptableObject.CreateInstance<TerrainThemeCatalog>();
            var t = ScriptableObject.CreateInstance<TerrainTheme>();
            t.terrain = CastleTerrain.Plain;
            t.propsPerChunk = 0f;
            t.groundTile = MakeSprite("Ground_Plain");
            c.themes.Add(t);
            c.pond.sprite = MakeSprite("Prop_Pond");
            c.pond.slowRadius = 1.4f;
            c.pond.slowFactor = 0.5f;
            toDestroy.Add(t);
            toDestroy.Add(c);
            TerrainThemeCatalog.Use(c);

            var castle = ScriptableObject.CreateInstance<CastleData>();
            castle.id = "Ripple"; castle.displayName = "Ripple"; castle.terrain = CastleTerrain.Plain; castle.hasWater = false;
            toDestroy.Add(castle);

            var map = TerrainMap.Create(castle, c);
            var chunk = TerrainMap.ChunkOf(pondAt);
            map.SetCustomChunk(chunk, new List<PropPlacement>
            {
                new PropPlacement { prop = c.pond, position = pondAt, scale = 2f, stretchY = 1f, order = TerrainMap.OrderPond },   // 반지름 2.8
            });
            return map;
        }

        WaterRippleFx MakeFx(TerrainMap map)
        {
            var go = new GameObject("RippleFxTest");
            gos.Add(go);
            var fx = go.AddComponent<WaterRippleFx>();
            fx.Initialize(new TerrainCollision(map), null);
            return fx;
        }

        [UnityTest]
        public IEnumerator Wader_InWater_MakesRipplesAndOneSplash_ThenStopsOnLand()
        {
            var fx = MakeFx(MakeMapWithPond(new Vector2(20f, 20f)));
            var wader = new GameObject("Wader");
            gos.Add(wader);
            wader.transform.position = new Vector3(20f, 20f, 0f);   // 연못 한가운데
            fx.Track(wader.transform, makesSound: false);

            yield return null;
            Assert.AreEqual(0, fx.SplashCount, "처음부터 물 안에 있었으면 '들어간' 것이 아니다");

            // 물 안에서 걷는다
            for (int i = 0; i < 30; i++)
            {
                wader.transform.position += new Vector3(0.05f, 0f, 0f);
                yield return null;
            }
            Assert.Greater(fx.ActiveCount, 0, "물 안에서 걸으면 발밑에 물결이 생긴다");
            int madeInWater = fx.CreatedCount;
            Assert.Greater(madeInWater, 1);

            // 마른 땅으로 나간다 (연못 반지름 2.8 밖)
            wader.transform.position = new Vector3(30f, 20f, 0f);
            yield return null;
            int afterLeaving = fx.CreatedCount;
            for (int i = 0; i < 40; i++)
            {
                wader.transform.position += new Vector3(0.05f, 0f, 0f);
                yield return null;
            }
            Assert.AreEqual(afterLeaving, fx.CreatedCount, "마른 땅에서는 새 물결을 만들지 않는다 (풀만 재사용)");
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(0, fx.ActiveCount, "남은 물결은 사라진다");

            // 다시 들어가면 첨벙
            wader.transform.position = new Vector3(20f, 20f, 0f);
            yield return null;
            Assert.AreEqual(1, fx.SplashCount, "물에 들어가는 순간 한 번");
            yield return null;
            Assert.AreEqual(1, fx.SplashCount, "물 안에 머무는 동안 다시 첨벙하지 않는다");
        }

        [UnityTest]
        public IEnumerator Wader_OnLand_MakesNothing()
        {
            var fx = MakeFx(MakeMapWithPond(new Vector2(20f, 20f)));
            var wader = new GameObject("Wader");
            gos.Add(wader);
            wader.transform.position = Vector3.zero;
            fx.Track(wader.transform, makesSound: false);

            for (int i = 0; i < 30; i++)
            {
                wader.transform.position += new Vector3(0.05f, 0f, 0f);
                yield return null;
            }
            Assert.AreEqual(0, fx.CreatedCount);
            Assert.AreEqual(0, fx.SplashCount);
        }

        [UnityTest]
        public IEnumerator Ripples_AreCappedAndPooled()
        {
            var fx = MakeFx(MakeMapWithPond(new Vector2(20f, 20f)));
            for (int i = 0; i < 6; i++)
            {
                var w = new GameObject($"Wader{i}");
                gos.Add(w);
                w.transform.position = new Vector3(20f + i * 0.2f, 20f, 0f);
                fx.Track(w.transform, makesSound: false);
            }
            for (int i = 0; i < 120; i++)
            {
                foreach (var g in gos) if (g.name.StartsWith("Wader")) g.transform.position += new Vector3(0f, (i % 2 == 0 ? 0.3f : -0.3f), 0f);
                yield return null;
            }
            Assert.LessOrEqual(fx.CreatedCount, WaterRippleFx.MaxRipples, "상한을 넘겨 만들지 않는다");
            Assert.LessOrEqual(fx.ActiveCount, fx.CreatedCount);
            Assert.Less(fx.CreatedCount, 90, "짧게 사라진 물결을 다시 쓴다 (매번 새로 만들지 않는다)");
        }

        [UnityTest]
        public IEnumerator Background_AttachesRipples_WithTerrainMap()
        {
            MakeMapWithPond(new Vector2(20f, 20f));   // 카탈로그를 Use 로 올려 둔다
            var castle = ScriptableObject.CreateInstance<CastleData>();
            castle.id = "Bg"; castle.displayName = "Bg"; castle.terrain = CastleTerrain.Plain;
            toDestroy.Add(castle);
            GameSession.SortieCastle = castle;

            var camGo = new GameObject("TestCam", typeof(Camera));
            gos.Add(camGo);
            var bgGo = new GameObject("TestBackground", typeof(SpriteRenderer));
            gos.Add(bgGo);
            bgGo.GetComponent<SpriteRenderer>().sprite = MakeSprite("Default");
            var bg = bgGo.AddComponent<InfiniteBackground>();
            yield return null;

            Assert.IsNotNull(bg.Ripples, "지형 맵이 적용되면 물결 연출도 붙는다");
            Assert.AreSame(bg.Props.gameObject, bg.Ripples.gameObject, "소품 루트와 함께 살고 함께 사라진다");

            bg.ResetToDefault();
            Assert.IsNull(bg.Ripples);
        }

        [Test]
        public void RingSprite_IsGeneratedOnce()
        {
            var a = WaterRippleFx.RingSprite;
            Assert.IsNotNull(a);
            Assert.AreSame(a, WaterRippleFx.RingSprite);
            Assert.AreEqual(1f, a.bounds.size.x, 0.01f, "지름 1유닛");
        }
    }
}
