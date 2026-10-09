using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Player;
using Samkuk.World;
using UnityEngine;

namespace Samkuk.Tests
{
    /// <summary>월드 정렬·서 있는 소품·드리운 그림자(Step 14-4).</summary>
    public class Hd2dWorldSortTests
    {
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            MapStore.Disabled = true;
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
            Hd2dSettings.ShadowsOverride = true;
            Hd2dSettings.LightingOverride = false;
            CastShadow.SetSettings(ShadowPreset.For(TimeOfDay.Day));
        }

        [TearDown]
        public void TearDown()
        {
            MapStore.Disabled = false;
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
            Hd2dSettings.ResetOverrides();
            CastShadow.SetSettings(ShadowPreset.For(TimeOfDay.Day));
            foreach (var o in toDestroy) if (o != null) Object.DestroyImmediate(o);
            toDestroy.Clear();
        }

        // ───────────────────────── 그림자 수학 ─────────────────────────

        [TestCase(TimeOfDay.Day)]
        [TestCase(TimeOfDay.Dusk)]
        [TestCase(TimeOfDay.Dawn)]
        public void Decompose_RebuildsShadowMatrix(TimeOfDay time)
        {
            var s = ShadowPreset.For(time);
            ShadowPreset.Matrix(s, out float a, out float b, out float c, out float d);
            ShadowPreset.Decompose(a, b, c, d, out float outer, out float sx, out float sy, out float inner);

            // R(outer) · diag(sx, sy) · R(inner) 를 몇 점에 적용해 원래 행렬과 비교
            foreach (var p in new[] { new Vector2(0f, 1f), new Vector2(0.3f, 0.7f), new Vector2(-0.5f, 1.4f) })
            {
                Vector2 expected = new Vector2(a * p.x + b * p.y, c * p.x + d * p.y);
                Vector2 q = Rotate(p, inner);
                q = new Vector2(q.x * sx, q.y * sy);
                q = Rotate(q, outer);
                Assert.AreEqual(expected.x, q.x, 1e-4f, $"{time} x");
                Assert.AreEqual(expected.y, q.y, 1e-4f, $"{time} y");
            }
            Assert.Less(sy, 0f, "그림자는 발 아래로 뒤집혀 눕는다");
        }

        static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
        }

        [Test]
        public void ShadowPreset_Dusk_IsLongerAndDarkerThanDay()
        {
            var day = ShadowPreset.For(TimeOfDay.Day);
            var dusk = ShadowPreset.For(TimeOfDay.Dusk);
            Assert.Greater(dusk.length, day.length);
            Assert.Greater(dusk.alpha, day.alpha);
            Assert.Greater(Mathf.Abs(dusk.shearX), Mathf.Abs(day.shearX));
            Assert.Less(ShadowPreset.For(TimeOfDay.Dawn).shearX, 0f, "새벽은 반대쪽");
        }

        // ───────────────────────── 걷기 시트 피벗 ─────────────────────────

        [Test]
        public void WalkSheet_PivotIsAtFeet()
        {
            var tex = new Texture2D(PixelArt.WalkCell * 4, PixelArt.WalkCell * 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            toDestroy.Add(tex);
            var s = HeroSpriteSet.Get(tex, PixelArt.PPU).Get(FacingDir.Down, 0);
            Assert.AreEqual(0.5f, s.pivot.x / s.rect.width, 1e-4f);
            Assert.AreEqual(PixelArt.WalkFootPixels, s.pivot.y, 1e-4f, "발은 아래에서 8px");
            Assert.AreEqual(-PixelArt.WalkFootPixels / (float)PixelArt.PPU, s.bounds.min.y, 1e-4f, "트랜스폼 아래로는 발 두께만큼만 내려간다");
        }

        // ───────────────────────── 드리운 그림자 ─────────────────────────

        Sprite MakeSprite(Vector2 pivot)
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), pivot, 8f);
            toDestroy.Add(tex);
            toDestroy.Add(sprite);
            return sprite;
        }

        SpriteRenderer MakeBody(out GameObject go)
        {
            go = new GameObject("Body", typeof(SpriteRenderer));
            toDestroy.Add(go);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = MakeSprite(new Vector2(0.5f, 0.1f));
            return sr;
        }

        [Test]
        public void CastShadow_FollowsSourceSprite_AndLiesOnGround()
        {
            var body = MakeBody(out var go);
            var shadow = CastShadow.Attach(body);

            Assert.IsNotNull(shadow);
            Assert.AreSame(shadow, CastShadow.Attach(body), "두 번 붙이면 같은 것");
            Assert.AreEqual(body.sprite, shadow.Renderer.sprite);
            Assert.AreEqual(SortingLayer.NameToID(GameLayers.Sorting.Background), shadow.Renderer.sortingLayerID, "그림자는 배경 레이어(캐릭터 아래)");
            Assert.AreEqual(ShadowPreset.SortingOrder, shadow.Renderer.sortingOrder);
            Assert.AreEqual(0f, shadow.Renderer.color.r);
            Assert.AreEqual(ShadowPreset.For(TimeOfDay.Day).alpha, shadow.Renderer.color.a, 1e-4f);

            // 발 위 1유닛 점의 그림자는 발 아래로 눕는다 (x 는 옆으로 밀리고 y 는 음수)
            var s = ShadowPreset.For(TimeOfDay.Day);
            var p = shadow.SpriteNode.TransformPoint(new Vector3(0f, 1f, 0f)) - go.transform.position;
            Assert.AreEqual(s.shearX, p.x, 1e-3f);
            Assert.AreEqual(-s.length, p.y, 1e-3f);

            body.sprite = MakeSprite(new Vector2(0.5f, 0.1f));
            body.flipX = true;
            shadow.Sync();
            Assert.AreEqual(body.sprite, shadow.Renderer.sprite, "스프라이트가 바뀌면 따라온다");
            Assert.IsTrue(shadow.Renderer.flipX);
        }

        [Test]
        public void CastShadow_SettingsChange_ReshapesAllShadows()
        {
            var body = MakeBody(out var go);
            var shadow = CastShadow.Attach(body);
            CastShadow.SetSettings(ShadowPreset.For(TimeOfDay.Dusk));
            shadow.Sync();
            var s = ShadowPreset.For(TimeOfDay.Dusk);
            var p = shadow.SpriteNode.TransformPoint(new Vector3(0f, 1f, 0f)) - go.transform.position;
            Assert.AreEqual(s.shearX, p.x, 1e-3f, "해질녘엔 길게");
            Assert.AreEqual(-s.length, p.y, 1e-3f);
            Assert.AreEqual(s.alpha, shadow.Renderer.color.a, 1e-4f);
        }

        [Test]
        public void CastShadow_Disabled_HidesRenderer()
        {
            var body = MakeBody(out _);
            var shadow = CastShadow.Attach(body);
            Assert.IsTrue(shadow.Renderer.enabled);
            Hd2dSettings.ShadowsOverride = false;
            shadow.Sync();
            Assert.IsFalse(shadow.Renderer.enabled);
            Hd2dSettings.ShadowsOverride = true;
            shadow.Sync();
            Assert.IsTrue(shadow.Renderer.enabled);
        }

        [Test]
        public void CastShadow_Blob_IsFlatEllipse()
        {
            var body = MakeBody(out _);
            var shadow = CastShadow.Attach(body, blob: true);
            Assert.IsTrue(shadow.IsBlob);
            Assert.AreSame(CastShadow.BlobSprite, shadow.Renderer.sprite);
            Assert.Less(shadow.ScaleNode.localScale.y, shadow.ScaleNode.localScale.x, "납작한 타원");
            body.sprite = MakeSprite(new Vector2(0.5f, 0.1f));
            shadow.Sync();
            Assert.AreSame(CastShadow.BlobSprite, shadow.Renderer.sprite, "발밑 그림자는 원본 스프라이트를 따라가지 않는다");
        }

        // ───────────────────────── 서 있는 소품 ─────────────────────────

        [Test]
        public void PropKinds_StandingEqualsBlocking()
        {
            foreach (var kind in new[] { "tree", "pine", "rock", "boulder", "yurt", "boat", "banner", "mound" })
                Assert.IsTrue(TerrainPropKinds.IsStanding(kind), kind);
            foreach (var kind in new[] { "bush", "tuft", "flowers", "fern", "reed", "pond", "riverwater", "riverbank" })
                Assert.IsFalse(TerrainPropKinds.IsStanding(kind), kind);
            Assert.IsFalse(new TerrainProp().standing, "값을 채우기 전(예전 에셋)에는 바닥 장식");
        }

        TerrainMap MakeMap(out TerrainProp tree, out TerrainProp grass)
        {
            var c = ScriptableObject.CreateInstance<TerrainThemeCatalog>();
            var t = ScriptableObject.CreateInstance<TerrainTheme>();
            t.terrain = CastleTerrain.Plain;
            t.propsPerChunk = 0f;
            t.groundTile = MakeSprite(new Vector2(0.5f, 0.5f));
            tree = new TerrainProp { name = "Tree", sprite = MakeSprite(new Vector2(0.5f, 0.05f)), standing = true, blockRadius = 0.3f };
            grass = new TerrainProp { name = "Grass", sprite = MakeSprite(new Vector2(0.5f, 0.05f)) };
            t.props.Add(tree);
            t.props.Add(grass);
            c.themes.Add(t);
            toDestroy.Add(t);
            toDestroy.Add(c);
            TerrainThemeCatalog.Use(c);

            var castle = ScriptableObject.CreateInstance<CastleData>();
            castle.id = "SortTest"; castle.displayName = "SortTest"; castle.terrain = CastleTerrain.Plain;
            toDestroy.Add(castle);
            var map = TerrainMap.Create(castle, c);
            map.SetCustomChunk(TerrainMap.ChunkOf(Vector2.zero), new List<PropPlacement>
            {
                new PropPlacement { prop = tree, position = new Vector2(2f, 3f), scale = 1.2f, stretchY = 1f, rotation = 40f, order = TerrainMap.OrderPropBase + 7 },
                new PropPlacement { prop = grass, position = new Vector2(5f, 5f), scale = 1f, stretchY = 1f, rotation = 25f, order = TerrainMap.OrderPropBase + 3 },
            });
            return map;
        }

        TerrainPropSpawner MakeSpawner(TerrainMap map, bool allowFx = true)
        {
            var follow = new GameObject("Follow");
            toDestroy.Add(follow);
            var refGo = new GameObject("Ref", typeof(SpriteRenderer));
            toDestroy.Add(refGo);
            var reference = refGo.GetComponent<SpriteRenderer>();
            reference.sortingLayerName = GameLayers.Sorting.Background;
            var go = new GameObject("TerrainProps");
            toDestroy.Add(go);
            var spawner = go.AddComponent<TerrainPropSpawner>();
            spawner.Initialize(map, follow.transform, reference, allowFx);
            return spawner;
        }

        static SpriteRenderer FindProp(TerrainPropSpawner spawner, TerrainProp prop)
        {
            foreach (var sr in spawner.GetComponentsInChildren<SpriteRenderer>())
                if (sr.sprite == prop.sprite && sr.gameObject.name == "Prop") return sr;
            return null;
        }

        [Test]
        public void Spawner_StandingProp_UsesWorldSorting_NoRotation_WithShadow()
        {
            var map = MakeMap(out var tree, out var grass);
            var spawner = MakeSpawner(map);

            var treeSr = FindProp(spawner, tree);
            var grassSr = FindProp(spawner, grass);
            Assert.IsNotNull(treeSr);
            Assert.IsNotNull(grassSr);

            Assert.IsTrue(WorldSorting.IsConfigured(treeSr), "서 있는 소품은 월드 정렬(발 y)");
            Assert.AreEqual(0f, treeSr.transform.localRotation.eulerAngles.z, 1e-3f, "서 있는 소품은 눕히지 않는다 (저장된 회전 무시)");
            Assert.IsNotNull(treeSr.transform.Find(CastShadow.NodeName), "서 있는 소품에는 드리운 그림자");
            Assert.IsTrue(treeSr.transform.Find(CastShadow.NodeName).gameObject.activeSelf);

            Assert.IsFalse(WorldSorting.IsConfigured(grassSr), "바닥 장식은 배경 레이어 그대로");
            Assert.AreEqual(SortingLayer.NameToID(GameLayers.Sorting.Background), grassSr.sortingLayerID);
            Assert.AreEqual(TerrainMap.OrderPropBase + 3, grassSr.sortingOrder);
            Assert.AreEqual(25f, grassSr.transform.localRotation.eulerAngles.z, 1e-3f);
            Assert.IsNull(grassSr.transform.Find(CastShadow.NodeName), "바닥 장식에는 그림자 없음");
        }

        [Test]
        public void Spawner_PooledStandingProp_ReusedAsFlat_ResetsSortingAndShadow()
        {
            var map = MakeMap(out var tree, out var grass);
            var spawner = MakeSpawner(map);
            var treeSr = FindProp(spawner, tree);

            // 같은 칸을 풀 하나만 쓰도록 바꿔 서 있는 소품으로 쓰이던 것이 바닥 장식으로 돌려 쓰이게 한다
            map.SetCustomChunk(TerrainMap.ChunkOf(Vector2.zero), new List<PropPlacement>
            {
                new PropPlacement { prop = grass, position = new Vector2(1f, 1f), scale = 1f, stretchY = 1f, rotation = 0f, order = TerrainMap.OrderPropBase },
            });
            spawner.RebuildChunk(TerrainMap.ChunkOf(Vector2.zero));

            var reused = FindProp(spawner, grass);
            Assert.IsNotNull(reused);
            Assert.IsFalse(WorldSorting.IsConfigured(reused), "풀에서 돌려 쓴 것도 바닥 장식 설정으로 돌아간다");
            var shadowNode = reused.transform.Find(CastShadow.NodeName);
            if (shadowNode != null) Assert.IsFalse(shadowNode.gameObject.activeSelf, "남아 있는 그림자는 꺼진다");
        }

        [Test]
        public void Spawner_NoFx_SkipsShadows()
        {
            var map = MakeMap(out var tree, out _);
            var spawner = MakeSpawner(map, allowFx: false);   // 맵 편집기
            var treeSr = FindProp(spawner, tree);
            Assert.IsTrue(WorldSorting.IsConfigured(treeSr), "정렬은 편집기에서도 같다 (가려짐을 보며 놓도록)");
            Assert.IsNull(treeSr.transform.Find(CastShadow.NodeName));
        }

        // ───────────────────────── 맵 편집기 ─────────────────────────

        [Test]
        public void MapEditor_CanRotate_FalseForStandingProps()
        {
            MakeMap(out var tree, out var grass);
            Assert.IsFalse(MapEditModel.CanRotate(new PropPlacement { prop = tree }), "서 있는 소품은 돌지 않는다");
            Assert.IsTrue(MapEditModel.CanRotate(new PropPlacement { prop = grass }), "바닥 장식은 돈다");
            Assert.IsTrue(MapEditModel.CanRotate(new PropPlacement { prop = null }));
        }

        [Test]
        public void WorldSorting_Configure_SetsPivotSortPoint()
        {
            var sr = MakeBody(out _);
            WorldSorting.Configure(sr);
            Assert.IsTrue(WorldSorting.IsConfigured(sr));
            Assert.AreEqual(SpriteSortPoint.Pivot, sr.spriteSortPoint);
            Assert.AreEqual(0, sr.sortingOrder);
            Assert.AreEqual(WorldSorting.LayerId, sr.sortingLayerID);
        }
    }
}
