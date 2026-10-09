using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.World;
using UnityEngine;

namespace Samkuk.Tests
{
    /// <summary>겹친 소품 반투명(Step 14-6): 플레이어 앞에서 몸을 덮는 서 있는 소품만 반투명.</summary>
    public class Hd2dPropFadeTests
    {
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            MapStore.Disabled = true;
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
            Hd2dSettings.LightingOverride = false;
            Hd2dSettings.ShadowsOverride = false;
        }

        [TearDown]
        public void TearDown()
        {
            MapStore.Disabled = false;
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
            Hd2dSettings.ResetOverrides();
            foreach (var o in toDestroy) if (o != null) Object.DestroyImmediate(o);
            toDestroy.Clear();
        }

        // ───────────────────────── 규칙 ─────────────────────────

        [Test]
        public void Rule_FadesOnlyPropsInFrontThatCoverTheBody()
        {
            var feet = new Vector2(10f, 10f);
            var covering = new Rect(9f, 9.2f, 2f, 2.5f);     // 발 9.7 (플레이어보다 아래 = 앞), 몸을 덮음
            Assert.IsTrue(PropFadeRule.ShouldFade(9.7f, covering, feet));
            Assert.IsFalse(PropFadeRule.ShouldFade(10.3f, covering, feet), "플레이어 뒤에 그려지는 소품은 가리지 않는다");
            var aside = new Rect(12f, 9f, 1f, 2f);
            Assert.IsFalse(PropFadeRule.ShouldFade(9f, aside, feet), "옆에 있으면 겹치지 않는다");
            var below = new Rect(9.5f, 7f, 1f, 2.5f);      // 위 끝 9.5 < 발 10
            Assert.IsFalse(PropFadeRule.ShouldFade(7f, below, feet), "몸보다 아래에서 끝나면 겹치지 않는다");
        }

        [Test]
        public void Rule_Step_MovesSmoothly()
        {
            float a = 1f;
            a = PropFadeRule.Step(a, true, 0.05f);
            Assert.Less(a, 1f);
            Assert.Greater(a, PropFadeRule.FadedAlpha, "한 틱에 확 떨어지지 않는다");
            for (int i = 0; i < 60; i++) a = PropFadeRule.Step(a, true, 0.05f);
            Assert.AreEqual(PropFadeRule.FadedAlpha, a, 1e-4f);
            for (int i = 0; i < 60; i++) a = PropFadeRule.Step(a, false, 0.05f);
            Assert.AreEqual(1f, a, 1e-4f);
        }

        // ───────────────────────── 스포너 ─────────────────────────

        Sprite MakeSprite()
        {
            var tex = new Texture2D(16, 32, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(tex, new Rect(0, 0, 16, 32), new Vector2(0.5f, 0.05f), 16f);   // 1x2 유닛, 피벗 발
            toDestroy.Add(tex);
            toDestroy.Add(sprite);
            return sprite;
        }

        TerrainPropSpawner MakeSpawner(Vector2 treeAt, bool allowFx, out TerrainProp tree)
        {
            var c = ScriptableObject.CreateInstance<TerrainThemeCatalog>();
            var t = ScriptableObject.CreateInstance<TerrainTheme>();
            t.terrain = CastleTerrain.Plain;
            t.propsPerChunk = 0f;
            t.groundTile = MakeSprite();
            tree = new TerrainProp { name = "Tree", sprite = MakeSprite(), standing = true, blockRadius = 0.3f };
            t.props.Add(tree);
            c.themes.Add(t);
            toDestroy.Add(t);
            toDestroy.Add(c);
            TerrainThemeCatalog.Use(c);
            var castle = ScriptableObject.CreateInstance<CastleData>();
            castle.id = "FadeTest"; castle.terrain = CastleTerrain.Plain;
            toDestroy.Add(castle);
            var map = TerrainMap.Create(castle, c);
            map.SetCustomChunk(TerrainMap.ChunkOf(treeAt), new List<PropPlacement>
            {
                new PropPlacement { prop = tree, position = treeAt, scale = 1f, stretchY = 1f, order = TerrainMap.OrderPropBase },
            });

            var follow = new GameObject("Follow");
            toDestroy.Add(follow);
            var go = new GameObject("TerrainProps");
            toDestroy.Add(go);
            var spawner = go.AddComponent<TerrainPropSpawner>();
            spawner.Initialize(map, follow.transform, null, allowFx);
            return spawner;
        }

        SpriteRenderer FindTree(TerrainPropSpawner spawner, TerrainProp tree)
        {
            foreach (var sr in spawner.GetComponentsInChildren<SpriteRenderer>())
                if (sr.sprite == tree.sprite) return sr;
            return null;
        }

        [Test]
        public void Spawner_FadesTree_WhenPlayerStandsBehindIt()
        {
            var spawner = MakeSpawner(new Vector2(3f, 3f), true, out var tree);
            var player = new GameObject("Player");
            toDestroy.Add(player);
            spawner.FadeTarget = player.transform;
            var treeSr = FindTree(spawner, tree);

            player.transform.position = new Vector3(3.1f, 3.4f, 0f);   // 나무보다 조금 위(뒤), 나무 그림(1x2) 안
            for (int i = 0; i < 30; i++) spawner.TickFade(0.1f);
            Assert.AreEqual(PropFadeRule.FadedAlpha, treeSr.color.a, 1e-3f, "나무가 반투명");
            Assert.AreEqual(1, spawner.FadedCount);

            player.transform.position = new Vector3(3f, 2.5f, 0f);     // 나무보다 아래(앞)
            for (int i = 0; i < 30; i++) spawner.TickFade(0.1f);
            Assert.AreEqual(1f, treeSr.color.a, 1e-3f, "플레이어가 앞이면 되돌린다");
            Assert.AreEqual(0, spawner.FadedCount);

            player.transform.position = new Vector3(8f, 3.4f, 0f);     // 멀리
            for (int i = 0; i < 30; i++) spawner.TickFade(0.1f);
            Assert.AreEqual(1f, treeSr.color.a, 1e-3f);
        }

        [Test]
        public void Spawner_NoFade_InMapEditor()
        {
            var spawner = MakeSpawner(new Vector2(3f, 3f), false, out var tree);
            var player = new GameObject("Player");
            toDestroy.Add(player);
            spawner.FadeTarget = player.transform;
            player.transform.position = new Vector3(3f, 3.4f, 0f);
            for (int i = 0; i < 30; i++) spawner.TickFade(0.1f);
            Assert.AreEqual(1f, FindTree(spawner, tree).color.a, 1e-3f, "맵 편집기에서는 반투명하지 않다");
        }

        [Test]
        public void Spawner_RestoresAlpha_WhenPropIsReused()
        {
            var spawner = MakeSpawner(new Vector2(3f, 3f), true, out var tree);
            var player = new GameObject("Player");
            toDestroy.Add(player);
            spawner.FadeTarget = player.transform;
            player.transform.position = new Vector3(3f, 3.4f, 0f);
            for (int i = 0; i < 30; i++) spawner.TickFade(0.1f);
            Assert.Less(FindTree(spawner, tree).color.a, 1f);

            spawner.RebuildChunk(TerrainMap.ChunkOf(new Vector2(3f, 3f)));   // 풀로 보냈다가 다시 꺼낸다
            Assert.AreEqual(1f, FindTree(spawner, tree).color.a, 1e-3f, "다시 놓인 소품은 불투명으로 시작한다");
        }
    }
}
