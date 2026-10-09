using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    /// <summary>성마다 다른 전투 맵(Step 12-6): 지형 바닥 + 성마다 다르게 흩뿌려지는 소품.</summary>
    public class TerrainTests
    {
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            TerrainThemeCatalog.Disabled = false;
            MapStore.Disabled = true;   // 사용자가 맵 편집기로 고쳐 둔 실제 맵이 이 테스트에 섞이지 않게
            DestroyLeftoverProps();
            TerrainThemeCatalog.Use(null);
            GameSession.SortieCastle = null;
        }

        [TearDown]
        public void TearDown()
        {
            MapStore.Disabled = false;
            TerrainThemeCatalog.Disabled = false;
            TerrainThemeCatalog.Use(null);
            GameSession.SortieCastle = null;
            foreach (var o in toDestroy) if (o != null) Object.Destroy(o);
            toDestroy.Clear();
            DestroyLeftoverProps();
        }

        /// <summary>지연 삭제로 남은 소품 루트가 다음 테스트에 섞이지 않게 즉시 지운다.</summary>
        static void DestroyLeftoverProps()
        {
            foreach (var s in Object.FindObjectsByType<TerrainPropSpawner>())
                Object.DestroyImmediate(s.gameObject);
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        Sprite MakeSprite(Color color)
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[64];
            for (int i = 0; i < px.Length; i++) px[i] = color;
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.1f), 8f);
            toDestroy.Add(tex);
            toDestroy.Add(sprite);
            return sprite;
        }

        TerrainTheme MakeTheme(CastleTerrain terrain, float density = 8f)
        {
            var t = ScriptableObject.CreateInstance<TerrainTheme>();
            t.terrain = terrain;
            t.propsPerChunk = density;
            t.groundTile = MakeSprite(Color.green);
            t.props.Add(new TerrainProp { name = "A", sprite = MakeSprite(Color.red), weight = 3f });
            t.props.Add(new TerrainProp { name = "B", sprite = MakeSprite(Color.blue), weight = 2f });
            t.props.Add(new TerrainProp { name = "C", sprite = MakeSprite(Color.yellow), weight = 1f });
            t.riverWater = MakeSprite(Color.blue);
            t.riverBank = MakeSprite(Color.gray);
            t.riverWidth = 3.5f;
            toDestroy.Add(t);
            return t;
        }

        TerrainThemeCatalog MakeCatalog()
        {
            var c = ScriptableObject.CreateInstance<TerrainThemeCatalog>();
            foreach (CastleTerrain t in System.Enum.GetValues(typeof(CastleTerrain))) c.themes.Add(MakeTheme(t));
            c.pond.sprite = MakeSprite(Color.cyan);
            c.banner.sprite = MakeSprite(Color.magenta);
            toDestroy.Add(c);
            return c;
        }

        CastleData MakeCastle(string id, CastleTerrain terrain = CastleTerrain.Plain, CastleSize size = CastleSize.Small, bool water = false)
        {
            var c = ScriptableObject.CreateInstance<CastleData>();
            c.id = id; c.displayName = id; c.terrain = terrain; c.size = size; c.hasWater = water;
            toDestroy.Add(c);
            return c;
        }

        /// <summary>여러 칸의 배치를 모아 돌려준다.</summary>
        static List<PropPlacement> Gather(TerrainMap map, int range)
        {
            var all = new List<PropPlacement>();
            for (int y = -range; y <= range; y++)
                for (int x = -range; x <= range; x++) all.AddRange(map.Layout(x, y));
            return all;
        }

        // ───────────────────────── 맵 만들기 ─────────────────────────

        [Test]
        public void Create_ReturnsNull_WithoutCastleCatalogOrTheme()
        {
            var catalog = MakeCatalog();
            Assert.IsNull(TerrainMap.Create(null, catalog));
            Assert.IsNull(TerrainMap.Create(MakeCastle("X"), null));

            var empty = ScriptableObject.CreateInstance<TerrainThemeCatalog>();
            toDestroy.Add(empty);
            Assert.IsNull(TerrainMap.Create(MakeCastle("X"), empty), "해당 지형의 테마가 없으면 null (기존 색 덮개 방식으로 대신)");
            Assert.IsNotNull(TerrainMap.Create(MakeCastle("X"), catalog));
        }

        [Test]
        public void Layout_IsDeterministic_ForSameCastleAndChunk()
        {
            var map1 = TerrainMap.Create(MakeCastle("Luoyang"), MakeCatalog());
            var map2 = TerrainMap.Create(MakeCastle("Luoyang"), MakeCatalog());

            var a = map1.Layout(3, -2);
            var b = map2.Layout(3, -2);
            Assert.Greater(a.Count, 0);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].position, b[i].position);
                Assert.AreEqual(a[i].scale, b[i].scale);
                Assert.AreEqual(a[i].prop.name, b[i].prop.name);
            }
        }

        [Test]
        public void Layout_DiffersBetweenChunksAndBetweenCastles()
        {
            var catalog = MakeCatalog();
            var map = TerrainMap.Create(MakeCastle("Luoyang"), catalog);
            var other = TerrainMap.Create(MakeCastle("Changan"), catalog);

            CollectionAssert.AreNotEqual(map.Layout(1, 1).Select(p => p.position).ToList(), map.Layout(2, 1).Select(p => p.position).ToList(), "칸마다 배치가 다르다");
            CollectionAssert.AreNotEqual(map.Layout(1, 1).Select(p => p.position).ToList(), other.Layout(1, 1).Select(p => p.position).ToList(), "성마다 배치가 다르다");
        }

        [Test]
        public void CastlesDiffer_InDensityTintAndPropMix()
        {
            var catalog = MakeCatalog();
            var maps = new List<TerrainMap>();
            foreach (string id in new[] { "Ye", "Jinyang", "Luoyang", "Changan", "Chengdu", "Jianye", "Xuchang", "Wan" })
                maps.Add(TerrainMap.Create(MakeCastle(id), catalog));

            Assert.Greater(maps.Select(m => System.Math.Round(m.DensityMultiplier, 2)).Distinct().Count(), 3, "밀도가 성마다 다르다");
            Assert.Greater(maps.Select(m => m.GroundTint).Distinct().Count(), 3, "바닥 색조가 성마다 다르다");
            foreach (var m in maps)
            {
                Assert.That(m.DensityMultiplier, Is.InRange(0.8f, 1.31f));
                Assert.That(m.GroundTint.r, Is.InRange(0.92f, 1.08f));
            }

            // 어떤 소품이 많은지도 성마다 다르다
            var mixes = maps.Select(m => string.Join(",", Gather(m, 6).GroupBy(p => p.prop.name).OrderBy(g => g.Key).Select(g => g.Count() / 10))).Distinct().Count();
            Assert.Greater(mixes, 2, "소품 비율이 성마다 다르다");
        }

        // ───────────────────────── 배치 규칙 ─────────────────────────

        [Test]
        public void Layout_StaysInsideChunk_AndKeepsStartAreaClear()
        {
            var map = TerrainMap.Create(MakeCastle("Ye"), MakeCatalog());
            for (int y = -4; y <= 4; y++)
                for (int x = -4; x <= 4; x++)
                    foreach (var p in map.Layout(x, y))
                    {
                        Assert.That(p.position.x, Is.InRange(x * TerrainMap.ChunkSize, (x + 1) * TerrainMap.ChunkSize));
                        Assert.That(p.position.y, Is.InRange(y * TerrainMap.ChunkSize, (y + 1) * TerrainMap.ChunkSize));
                        Assert.GreaterOrEqual(p.position.magnitude, TerrainMap.ClearRadius, "플레이어 시작 위치 주변은 비어 있어야 한다");
                    }
        }

        [Test]
        public void Layout_PropsDoNotOverlapEachOther_OrPonds()
        {
            var map = TerrainMap.Create(MakeCastle("Xiangyang", CastleTerrain.River, CastleSize.Large, water: true), MakeCatalog());
            foreach (var chunk in new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, 2), new Vector2Int(5, -3) })
            {
                var items = map.Layout(chunk.x, chunk.y);
                var ponds = items.Where(p => p.prop == map.Catalog.pond).ToList();
                var others = items.Where(p => p.prop != map.Catalog.pond && p.prop != map.RiverBankProp && p.prop != map.RiverWaterProp).ToList();
                for (int i = 0; i < others.Count; i++)
                {
                    for (int j = i + 1; j < others.Count; j++)
                        Assert.GreaterOrEqual(Vector2.Distance(others[i].position, others[j].position), 1.1f, "소품끼리 너무 가깝다");
                    foreach (var pond in ponds)
                        Assert.GreaterOrEqual(Vector2.Distance(others[i].position, pond.position), 2.2f * pond.scale - 0.01f, "소품이 연못 위에 있다");
                }
            }
        }

        [Test]
        public void Layout_OnlyUsesPropsWithSpriteAndWeight()
        {
            var catalog = MakeCatalog();
            var theme = catalog.Get(CastleTerrain.Plain);
            theme.props[0].sprite = null;     // 그림이 없는 소품
            theme.props[1].weight = 0f;       // 비중 0
            var map = TerrainMap.Create(MakeCastle("Ye"), catalog);

            var names = Gather(map, 5).Select(p => p.prop.name).Distinct().ToList();
            CollectionAssert.AreEquivalent(new[] { "C" }, names);
        }

        [Test]
        public void Layout_DensityRoughlyFollowsThemeSetting()
        {
            var catalog = MakeCatalog();
            catalog.Get(CastleTerrain.Plain).propsPerChunk = 2f;
            catalog.Get(CastleTerrain.Jungle).propsPerChunk = 14f;
            var sparse = TerrainMap.Create(MakeCastle("Ye", CastleTerrain.Plain), catalog);
            var dense = TerrainMap.Create(MakeCastle("Ye", CastleTerrain.Jungle), catalog);

            Assert.Greater(Gather(dense, 4).Count, Gather(sparse, 4).Count * 2, "밀도가 높은 지형이 훨씬 빽빽하다");
        }

        [Test]
        public void Layout_IsLively_ClustersAndMoreThanBareScatter()
        {
            var catalog = MakeCatalog();
            var map = TerrainMap.Create(MakeCastle("Wan", CastleTerrain.Plain, CastleSize.Large), catalog);
            var items = Gather(map, 4).Where(p => p.prop != catalog.banner).ToList();
            float perChunk = items.Count / 81f;
            Assert.Greater(perChunk, 8f * map.DensityMultiplier * 1.4f, "테마 밀도보다 훨씬 빽빽해 휑하지 않다");

            // 무리: 같은 소품이 바짝 모여 있는 곳이 있다 (순수 흩뿌림이면 드물다)
            int tight = 0;
            foreach (var a in items)
                if (items.Count(b => b.prop == a.prop && b.position != a.position && Vector2.Distance(a.position, b.position) < 2.4f) >= 3) tight++;
            Assert.Greater(tight, items.Count / 20, "숲, 풀밭 같은 무리가 있다");
        }

        [Test]
        public void Patches_AreSoftTintedGroundDecor_Deterministic_AndOffTheRiver()
        {
            var catalog = MakeCatalog();
            var map = TerrainMap.Create(MakeCastle("Wet", CastleTerrain.Plain, water: true), catalog);
            var a = map.LayoutPatches(2, -1);
            var b = map.LayoutPatches(2, -1);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) { Assert.AreEqual(a[i].position, b[i].position); Assert.AreEqual(a[i].tint, b[i].tint); }

            var all = new List<PropPlacement>();
            for (int y = -4; y <= 4; y++)
                for (int x = -4; x <= 4; x++) all.AddRange(map.LayoutPatches(x, y));
            Assert.Greater(all.Count, 100, "바닥 얼룩이 화면마다 여러 개 있다");
            foreach (var p in all)
            {
                Assert.IsTrue(p.tinted);
                Assert.Less(p.tint.a, 0.3f, "은은한 색 변화일 뿐 바닥을 덮지 않는다");
                Assert.AreEqual(TerrainMap.OrderPatch, p.order);
                Assert.IsNotNull(p.prop.sprite);
                Assert.GreaterOrEqual(p.position.magnitude, TerrainMap.ClearRadius);
                Assert.GreaterOrEqual(map.RiverDistance(p.position), map.RiverWidth * 0.5f, "강물 위에 얼룩이 없다");
            }
            Assert.Less(TerrainMap.OrderPatch, TerrainMap.OrderBank, "얼룩은 강둑보다 아래");
        }

        [Test]
        public void Ponds_OnlyOnWaterOrRiverCastles()
        {
            var catalog = MakeCatalog();
            int Ponds(CastleData c) => Gather(TerrainMap.Create(c, catalog), 8).Count(p => p.prop == catalog.pond);

            Assert.AreEqual(0, Ponds(MakeCastle("Dry", CastleTerrain.Plain)), "강이 없는 평야에는 연못이 없다");
            Assert.Greater(Ponds(MakeCastle("Wet", CastleTerrain.Plain, water: true)), 0, "강이 있는 성에는 연못이 있다");
            Assert.Greater(Ponds(MakeCastle("River", CastleTerrain.River)), 0, "강변 지형에는 연못이 있다");
            Assert.Greater(Ponds(MakeCastle("RiverWet", CastleTerrain.River, water: true)), Ponds(MakeCastle("River", CastleTerrain.River)) / 2);
        }

        [Test]
        public void Banners_OnlyOnLargeCastles_AndMoreOnCapitals()
        {
            var catalog = MakeCatalog();
            int Banners(CastleData c) => Gather(TerrainMap.Create(c, catalog), 8).Count(p => p.prop == catalog.banner);

            Assert.AreEqual(0, Banners(MakeCastle("S", size: CastleSize.Small)));
            Assert.AreEqual(0, Banners(MakeCastle("M", size: CastleSize.Medium)));
            int large = Banners(MakeCastle("L", size: CastleSize.Large));
            int capital = Banners(MakeCastle("L", size: CastleSize.Capital));
            Assert.Greater(large, 0);
            Assert.Greater(capital, large, "도성에 깃발이 더 많다");
        }

        [Test]
        public void Order_PondsAreBelowProps_AndLowerPropsDrawOnTop()
        {
            var catalog = MakeCatalog();
            var map = TerrainMap.Create(MakeCastle("Wet", CastleTerrain.River, water: true), catalog);
            var items = Gather(map, 4);

            Assert.IsTrue(items.Where(p => p.prop == catalog.pond).All(p => p.order == TerrainMap.OrderPond));
            Assert.IsTrue(items.Where(p => p.prop == map.RiverBankProp).All(p => p.order == TerrainMap.OrderBank));
            Assert.IsTrue(items.Where(p => p.prop == map.RiverWaterProp).All(p => p.order == TerrainMap.OrderWater));
            Assert.IsTrue(items.Where(p => p.prop != catalog.pond && p.prop != map.RiverBankProp && p.prop != map.RiverWaterProp).All(p => p.order >= TerrainMap.OrderPropBase));

            var a = map.Layout(1, 1).Where(p => p.prop != catalog.pond && p.prop != map.RiverBankProp && p.prop != map.RiverWaterProp).OrderBy(p => p.position.y).ToList();
            for (int i = 1; i < a.Count; i++)
                Assert.GreaterOrEqual(a[i - 1].order, a[i].order, "아래쪽(y 가 작은) 소품이 위에 그려진다");
        }

        // ───────────────────────── 강 ─────────────────────────

        static List<PropPlacement> RiverPieces(TerrainMap map, int range, bool water = true)
        {
            var all = new List<PropPlacement>();
            var prop = water ? map.RiverWaterProp : map.RiverBankProp;
            for (int y = -range; y <= range; y++)
                for (int x = -range; x <= range; x++) all.AddRange(map.Layout(x, y).Where(p => p.prop == prop));
            return all;
        }

        [Test]
        public void River_OnlyOnWaterCastlesAndRiverTerrain()
        {
            var catalog = MakeCatalog();
            Assert.IsFalse(TerrainMap.Create(MakeCastle("Dry", CastleTerrain.Plain), catalog).HasRiver, "강이 없는 평야에는 강이 없다");
            Assert.IsFalse(TerrainMap.Create(MakeCastle("Dry2", CastleTerrain.Mountain), catalog).HasRiver);
            Assert.IsTrue(TerrainMap.Create(MakeCastle("Wet", CastleTerrain.Plain, water: true), catalog).HasRiver, "강이 있는 성");
            Assert.IsTrue(TerrainMap.Create(MakeCastle("River", CastleTerrain.River), catalog).HasRiver, "강변 지형");

            var dry = TerrainMap.Create(MakeCastle("Dry", CastleTerrain.Plain), catalog);
            Assert.IsEmpty(RiverPieces(dry, 4), "강이 없는 맵에는 강 토막도 없다");
            Assert.IsTrue(float.IsPositiveInfinity(dry.RiverDistance(Vector2.one)));
        }

        [Test]
        public void River_NeedsRiverArtInTheme()
        {
            var catalog = MakeCatalog();
            catalog.Get(CastleTerrain.Plain).riverWater = null; // 그림이 없으면 강도 없다 (오류 없이)
            var map = TerrainMap.Create(MakeCastle("Wet", CastleTerrain.Plain, water: true), catalog);
            Assert.IsFalse(map.HasRiver);
            Assert.DoesNotThrow(() => Gather(map, 2));
        }

        [Test]
        public void River_BankAndWaterCome_InPairsAtTheSamePlaces()
        {
            var map = TerrainMap.Create(MakeCastle("Xiangyang", CastleTerrain.River, water: true), MakeCatalog());
            var water = RiverPieces(map, 5, true);
            var bank = RiverPieces(map, 5, false);

            Assert.Greater(water.Count, 20, "강이 화면 몇 개 분량 이상 흐른다");
            Assert.AreEqual(water.Count, bank.Count);
            CollectionAssert.AreEquivalent(water.Select(p => p.position), bank.Select(p => p.position));
            Assert.IsTrue(bank.All(p => p.stretchY > water[0].stretchY * 0.99f), "강둑이 물보다 넓다");
        }

        [Test]
        public void River_IsContinuous_NoGapsBetweenPieces()
        {
            var map = TerrainMap.Create(MakeCastle("Xiangyang", CastleTerrain.River, water: true), MakeCatalog());
            var pieces = RiverPieces(map, 4);

            // 칸 경계를 넘어서도 토막이 이어진다: 모든 토막 곁에 이웃 토막이 (바깥 테두리를 빼고) 가까이 있다
            int interior = 0;
            foreach (var p in pieces)
            {
                if (Mathf.Abs(p.position.x) > 3.2f * TerrainMap.ChunkSize || Mathf.Abs(p.position.y) > 3.2f * TerrainMap.ChunkSize) continue;
                interior++;
                bool hasNeighbor = pieces.Any(q => !q.position.Equals(p.position) && Vector2.Distance(p.position, q.position) < 2.4f);
                Assert.IsTrue(hasNeighbor, $"강 토막 {p.position} 곁이 끊겨 있다");
            }
            Assert.Greater(interior, 10);
        }

        [Test]
        public void River_PiecesBelongToExactlyOneChunk_NoDuplicates()
        {
            var map = TerrainMap.Create(MakeCastle("Xiangyang", CastleTerrain.River, water: true), MakeCatalog());
            var all = new List<Vector2>();
            for (int y = -6; y <= 6; y++)
                for (int x = -6; x <= 6; x++)
                    foreach (var p in map.Layout(x, y).Where(p => p.prop == map.RiverWaterProp))
                    {
                        Assert.AreEqual(new Vector2Int(x, y), TerrainMap.ChunkOf(p.position), "강 토막은 자기가 속한 칸에서만 만들어진다");
                        all.Add(p.position);
                    }

            Assert.AreEqual(all.Count, all.Distinct().Count(), "같은 자리에 겹친 토막이 없다 (이웃 칸이 중복으로 만들지 않는다)");
        }

        [Test]
        public void River_Flows_AlongItsOwnDirection()
        {
            var map = TerrainMap.Create(MakeCastle("Xiangyang", CastleTerrain.River, water: true), MakeCatalog());
            var pieces = RiverPieces(map, 5);

            // 토막이 눕는 각도는 강의 방향(성마다 다름) 근처다 (굽이 때문에 기울기 만큼 벗어남: 최대 atan(진폭 x 2π / 파장))
            float baseAngle = map.RiverAngleDegrees;
            foreach (var p in pieces)
            {
                float diff = Mathf.Abs(Mathf.DeltaAngle(p.rotation, baseAngle));
                Assert.Less(diff, 55f, "강 토막이 강 방향과 너무 어긋난다");
            }
            Assert.Greater(pieces.Select(p => Mathf.Round(p.rotation)).Distinct().Count(), 5, "굽이치므로 토막 각도가 다양하다");
        }

        [Test]
        public void River_KeepsStartAreaDry_AndPropsOffTheWater()
        {
            var catalog = MakeCatalog();
            foreach (string id in new[] { "Xiangyang", "Jianye", "Wu", "Changsha", "Jiangxia", "FreeBattle", "Shouchun", "Beihai" })
            {
                var map = TerrainMap.Create(MakeCastle(id, CastleTerrain.River, water: true), catalog);
                Assert.Greater(map.RiverDistance(Vector2.zero), map.RiverWidth * 0.5f + 1.5f, $"{id}: 시작 위치가 강물에 닿는다");

                foreach (var p in Gather(map, 3).Where(p => p.prop != map.RiverBankProp && p.prop != map.RiverWaterProp))
                    Assert.GreaterOrEqual(map.RiverDistance(p.position), map.RiverWidth * 0.5f + 0.4f, $"{id}: {p.prop.name} 이(가) 강물 위에 서 있다");
            }
        }

        [Test]
        public void River_DiffersBetweenCastles_AndTerrainsUseTheirOwnWidth()
        {
            var catalog = MakeCatalog();
            catalog.Get(CastleTerrain.River).riverWidth = 6f;
            catalog.Get(CastleTerrain.Mountain).riverWidth = 2f;
            var wide = TerrainMap.Create(MakeCastle("A", CastleTerrain.River), catalog);
            var narrow = TerrainMap.Create(MakeCastle("A", CastleTerrain.Mountain, water: true), catalog);
            Assert.Greater(wide.RiverWidth, narrow.RiverWidth * 2f, "지형 테마의 강 폭이 반영된다");

            var angles = new[] { "Ye", "Jinyang", "Luoyang", "Changan", "Chengdu", "Jianye" }
                .Select(id => System.Math.Round(TerrainMap.Create(MakeCastle(id, CastleTerrain.River), catalog).RiverAngleDegrees)).Distinct().Count();
            Assert.Greater(angles, 3, "성마다 강이 흐르는 방향이 다르다");
        }

        [Test]
        public void River_NearestRiverIsVisibleSoonFromStart()
        {
            var catalog = MakeCatalog();
            foreach (string id in new[] { "Xiangyang", "Jianye", "Wu", "FreeBattle" })
            {
                var map = TerrainMap.Create(MakeCastle(id, CastleTerrain.River), catalog);
                Assert.Less(map.RiverDistance(Vector2.zero), 21f, $"{id}: 강이 시작 위치에서 너무 멀다 (이동하면 곧 보여야 한다)");
            }
        }

        [Test]
        public void FreeBattle_UsesPlainWithARiver()
        {
            var catalog = MakeCatalog();
            var map = TerrainMap.CreateFreeBattle(catalog);

            Assert.IsNotNull(map);
            Assert.AreEqual(CastleTerrain.Plain, map.Castle.terrain);
            Assert.IsTrue(map.HasRiver, "성 없이 시작한 판도 강이 있는 평야 (아무것도 없는 빈 평지가 아니다)");
            Assert.Greater(Gather(map, 2).Count, 20);
            Assert.IsNull(TerrainMap.CreateFreeBattle(null));

            var again = TerrainMap.CreateFreeBattle(catalog);
            CollectionAssert.AreEqual(map.Layout(2, 3).Select(p => p.position), again.Layout(2, 3).Select(p => p.position), "자유 전투 맵은 늘 같다");
        }

        // ───────────────────────── 소품 만들기 (스포너) ─────────────────────────

        TerrainPropSpawner MakeSpawner(TerrainMap map, Transform follow, out SpriteRenderer reference)
        {
            var refGo = new GameObject("RefBg", typeof(SpriteRenderer));
            toDestroy.Add(refGo);
            reference = refGo.GetComponent<SpriteRenderer>();
            reference.sortingLayerName = GameLayers.Sorting.Background;

            var go = new GameObject("TestSpawner");
            toDestroy.Add(go);
            var spawner = go.AddComponent<TerrainPropSpawner>();
            spawner.Initialize(map, follow, reference);
            return spawner;
        }

        Transform MakeFollow(Vector3 position)
        {
            var go = new GameObject("TestFollow");
            toDestroy.Add(go);
            go.transform.position = position;
            return go.transform;
        }

        [Test]
        public void Spawner_CreatesPropsAroundFollowTarget()
        {
            var map = TerrainMap.Create(MakeCastle("Ye"), MakeCatalog());
            var spawner = MakeSpawner(map, MakeFollow(Vector3.zero), out _);

            Assert.GreaterOrEqual(spawner.ActiveChunkCount, 4, "화면 주변 칸이 만들어진다");
            Assert.Greater(spawner.ActivePropCount, 10);
            Assert.AreEqual(spawner.ActivePropCount, spawner.GetComponentsInChildren<SpriteRenderer>().Length, "활성 소품 수 = 실제 스프라이트 수");
        }

        [Test]
        public void Spawner_PatchesGetTheirTint_AndPooledSpritesAreRecolored()
        {
            var map = TerrainMap.Create(MakeCastle("Ye"), MakeCatalog());
            var follow = MakeFollow(Vector3.zero);
            var spawner = MakeSpawner(map, follow, out _);
            var renderers = spawner.GetComponentsInChildren<SpriteRenderer>(false);
            Assert.IsTrue(renderers.Any(r => r.color != Color.white), "얼룩은 색을 입는다");
            Assert.IsTrue(renderers.Any(r => r.color == Color.white), "소품은 원래 색");

            // 멀리 갔다가 돌아와도(풀 재사용) 소품이 얼룩 색을 물려받지 않는다
            follow.position = new Vector3(400f, 0f, 0f);
            spawner.Refresh();
            follow.position = Vector3.zero;
            spawner.Refresh();
            foreach (var r in spawner.GetComponentsInChildren<SpriteRenderer>(false))
                Assert.AreEqual(r.sprite == TerrainDecals.SoftBlob, r.color != Color.white, "얼룩만 색을 입는다");
        }


        [Test]
        public void Spawner_PropsAreDecoration_BehindEverythingElse()
        {
            var map = TerrainMap.Create(MakeCastle("Ye"), MakeCatalog());
            var spawner = MakeSpawner(map, MakeFollow(Vector3.zero), out var reference);

            foreach (var sr in spawner.GetComponentsInChildren<SpriteRenderer>())
            {
                Assert.AreEqual(reference.sortingLayerID, sr.sortingLayerID, "배경 정렬 레이어 (적/플레이어보다 뒤)");
                Assert.GreaterOrEqual(sr.sortingOrder, 1, "배경 바닥(0)보다 위");
                Assert.AreEqual(reference.sharedMaterial, sr.sharedMaterial, "배경과 같은 재질 = 같은 조명");
                Assert.IsNull(sr.GetComponent<Collider2D>(), "충돌이 없는 장식");
            }
        }

        [Test]
        public void Spawner_MovingFar_DespawnsOldChunks_AndReusesPool()
        {
            var map = TerrainMap.Create(MakeCastle("Ye"), MakeCatalog());
            var follow = MakeFollow(Vector3.zero);
            var spawner = MakeSpawner(map, follow, out _);
            int firstChildren = spawner.transform.childCount;
            int firstChunks = spawner.ActiveChunkCount;

            follow.position = new Vector3(600f, 0f, 0f);
            spawner.Refresh();
            Assert.AreEqual(firstChunks, spawner.ActiveChunkCount, "멀리 가도 화면 주변의 칸 수는 같다");
            Assert.LessOrEqual(spawner.transform.childCount, firstChildren * 2 + 30, "옛 소품을 풀에서 다시 써서 오브젝트가 계속 늘지 않는다");   // 새 칸을 먼저 만들고 옛 칸을 치우므로 한때 두 배까지

            for (int i = 1; i <= 20; i++)
            {
                follow.position = new Vector3(600f + i * 20f, i * 15f, 0f);
                spawner.Refresh();
            }
            Assert.LessOrEqual(spawner.ActiveChunkCount, 30, "계속 이동해도 활성 칸이 한없이 늘지 않는다");
            Assert.LessOrEqual(spawner.transform.childCount, 1000, "계속 이동해도 소품 오브젝트 수가 한없이 늘지 않는다 (풀 재사용)");
            foreach (var sr in spawner.GetComponentsInChildren<SpriteRenderer>(false))
                Assert.That(Vector2.Distance(sr.transform.position, follow.position), Is.LessThan(60f), "화면에서 먼 소품은 치워졌다");
        }

        [Test]
        public void Spawner_SameWorldPlace_GetsSamePropsWhenRevisited()
        {
            var map = TerrainMap.Create(MakeCastle("Ye"), MakeCatalog());
            var follow = MakeFollow(Vector3.zero);
            var spawner = MakeSpawner(map, follow, out _);

            List<Vector2> Snapshot() => spawner.GetComponentsInChildren<SpriteRenderer>(false)
                .Select(s => (Vector2)s.transform.position).OrderBy(v => v.x).ThenBy(v => v.y).ToList();
            var before = Snapshot();

            follow.position = new Vector3(300f, 300f, 0f);
            spawner.Refresh();
            follow.position = Vector3.zero;
            spawner.Refresh();

            CollectionAssert.AreEqual(before, Snapshot(), "다시 돌아와도 같은 자리에 같은 소품 (세계가 고정되어 있다)");
        }

        // ───────────────────────── 배경에 적용 ─────────────────────────

        InfiniteBackground MakeBackground(Sprite defaultSprite, out GameObject go)
        {
            go = new GameObject("TestBg", typeof(SpriteRenderer));
            toDestroy.Add(go);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = defaultSprite;
            sr.sortingLayerName = GameLayers.Sorting.Background;
            return go.AddComponent<InfiniteBackground>();
        }

        [UnityTest]
        public IEnumerator Background_AppliesGroundTileTintAndProps_ForSortieCastle()
        {
            var catalog = MakeCatalog();
            TerrainThemeCatalog.Use(catalog);
            var castle = MakeCastle("Jianye", CastleTerrain.River, CastleSize.Large, water: true);
            GameSession.SortieCastle = castle;

            var defaultSprite = MakeSprite(Color.gray);
            var bg = MakeBackground(defaultSprite, out var go);
            var sr = go.GetComponent<SpriteRenderer>();
            var theme = catalog.Get(CastleTerrain.River);

            Assert.AreSame(theme.groundTile, sr.sprite, "지형의 바닥 타일로 바뀐다");
            Assert.AreEqual(TerrainMap.Create(castle, catalog).GroundTint, sr.color, "성마다 다른 색조");
            Assert.IsNotNull(bg.Props, "소품을 만드는 쪽이 생긴다");
            Assert.Greater(bg.Props.ActivePropCount, 0);
            Assert.IsNull(bg.Props.transform.parent, "소품은 배경(카메라를 따라 움직임)의 자식이 아니다");
            Assert.IsNull(go.transform.Find("TerrainOverlay"), "그림이 있으면 색 덮개는 쓰지 않는다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Background_WithoutSortie_UsesFreeBattleMapWithRiver()
        {
            var catalog = MakeCatalog();
            TerrainThemeCatalog.Use(catalog);
            var defaultSprite = MakeSprite(Color.gray);
            var bg = MakeBackground(defaultSprite, out var go);   // SortieCastle = null: 타이틀의 [시작]
            var sr = go.GetComponent<SpriteRenderer>();

            Assert.AreSame(catalog.Get(CastleTerrain.Plain).groundTile, sr.sprite, "성 없이 시작해도 평야 바닥");
            Assert.IsNotNull(bg.Props, "빈 평지가 아니라 소품이 있다");
            Assert.IsTrue(bg.Props.Map.HasRiver, "강이 흐른다");
            Assert.Greater(bg.Props.GetComponentsInChildren<SpriteRenderer>().Length, 10);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Background_ResetToDefault_RestoresPlainBackground_AndRemovesProps()
        {
            var catalog = MakeCatalog();
            TerrainThemeCatalog.Use(catalog);
            var defaultSprite = MakeSprite(Color.gray);
            var bg = MakeBackground(defaultSprite, out var go);
            var sr = go.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(bg.Props);

            bg.ResetToDefault();
            yield return null;

            Assert.AreSame(defaultSprite, sr.sprite, "기본 배경으로 되돌린다");
            Assert.AreEqual(Color.white, sr.color);
            Assert.IsNull(bg.Props);
            Assert.IsNull(GameObject.Find("TerrainProps"), "소품도 치워진다");
        }

        [UnityTest]
        public IEnumerator Background_WithoutThemes_AndWithoutSortie_StaysDefault()
        {
            TerrainThemeCatalog.Disabled = true; // 지형 그림이 아직 없는 상태
            var defaultSprite = MakeSprite(Color.gray);
            var bg = MakeBackground(defaultSprite, out var go);

            Assert.AreSame(defaultSprite, go.GetComponent<SpriteRenderer>().sprite, "카탈로그가 없으면 기존 기본 배경 그대로");
            Assert.IsNull(bg.Props);
            Assert.IsNull(go.transform.Find("TerrainOverlay"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Spawner_PlacesRiverPieces_RotatedAndStretched()
        {
            var catalog = MakeCatalog();
            var map = TerrainMap.Create(MakeCastle("Xiangyang", CastleTerrain.River, water: true), catalog);
            // 강이 지나는 곳 위에서 시작: 시작 위치에서 가장 가까운 강의 한가운데 근처로 간다
            var river = Enumerable.Range(-3, 7).SelectMany(x => Enumerable.Range(-3, 7).SelectMany(y => map.Layout(x, y)))
                .First(p => p.prop == map.RiverWaterProp);
            var spawner = MakeSpawner(map, MakeFollow(new Vector3(river.position.x, river.position.y, 0f)), out _);
            yield return null;

            var water = spawner.GetComponentsInChildren<SpriteRenderer>().Where(s => s.sprite == catalog.Get(CastleTerrain.River).riverWater).ToList();
            Assert.Greater(water.Count, 3, "강 토막이 만들어진다");
            Assert.IsTrue(water.Any(s => !Mathf.Approximately(s.transform.localEulerAngles.z, 0f)), "강 토막이 흐르는 방향으로 눕는다");
            Assert.IsTrue(water.All(s => s.transform.localScale.y > 1.2f), "강 폭만큼 넓어진다");
            Assert.IsTrue(water.All(s => s.sortingOrder == TerrainMap.OrderWater));
        }

        [UnityTest]
        public IEnumerator Spawner_ReusedPoolObjects_ResetRotationAndScale()
        {
            var catalog = MakeCatalog();
            var map = TerrainMap.Create(MakeCastle("Xiangyang", CastleTerrain.River, water: true), catalog);
            var follow = MakeFollow(Vector3.zero);
            var spawner = MakeSpawner(map, follow, out _);

            // 멀리 갔다 오면서 강 토막(회전/늘임)이 풀에서 소품으로 재사용된다. 소품은 회전이 없고 정상 크기여야 한다.
            for (int i = 1; i <= 12; i++)
            {
                follow.position = new Vector3(i * 30f, i * 17f, 0f);
                spawner.Refresh();
            }
            yield return null;

            foreach (var sr in spawner.GetComponentsInChildren<SpriteRenderer>())
            {
                bool isRiver = sr.sprite == catalog.Get(CastleTerrain.River).riverWater || sr.sprite == catalog.Get(CastleTerrain.River).riverBank;
                if (isRiver) continue;
                Assert.AreEqual(0f, Mathf.DeltaAngle(0f, sr.transform.localEulerAngles.z), 0.01f, "소품에 강 토막의 회전이 남아 있다");
                Assert.Less(sr.transform.localScale.y, 2f, "소품에 강 토막의 늘임이 남아 있다");
            }
        }

        [UnityTest]
        public IEnumerator Background_DifferentCastles_GetDifferentMaps()
        {
            var catalog = MakeCatalog();
            TerrainThemeCatalog.Use(catalog);
            var bg = MakeBackground(MakeSprite(Color.gray), out var go);
            var sr = go.GetComponent<SpriteRenderer>();

            bg.ApplyCastle(MakeCastle("Ye", CastleTerrain.Plain));
            var tintA = sr.color;
            var spriteA = sr.sprite;
            bg.ApplyCastle(MakeCastle("Wan", CastleTerrain.Jungle));
            yield return null;

            Assert.AreNotSame(spriteA, sr.sprite, "지형이 다르면 바닥 타일이 다르다");
            Assert.AreNotEqual(tintA, sr.color, "색조도 다르다");
            Assert.AreEqual(1, GameObject.FindObjectsByType<TerrainPropSpawner>().Length, "이전 소품은 치워진다");
        }

        [UnityTest]
        public IEnumerator Background_FallsBackToColorOverlay_WhenNoTerrainThemes()
        {
            TerrainThemeCatalog.Disabled = true; // 셋업 전(지형 그림 없음)과 같은 상태
            var defaultSprite = MakeSprite(Color.gray);
            var bg = MakeBackground(defaultSprite, out var go);

            bg.ApplyCastle(MakeCastle("Wan", CastleTerrain.Mountain));
            var overlay = go.transform.Find("TerrainOverlay");
            Assert.IsNotNull(overlay, "지형 그림이 없으면 기존 색 덮개로 대신한다");
            Assert.AreEqual(BattleTerrain.Overlay(CastleTerrain.Mountain), overlay.GetComponent<SpriteRenderer>().color);
            Assert.IsNull(bg.Props);
            Assert.AreSame(defaultSprite, go.GetComponent<SpriteRenderer>().sprite);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Background_DestroyingIt_RemovesItsProps()
        {
            var catalog = MakeCatalog();
            TerrainThemeCatalog.Use(catalog);
            GameSession.SortieCastle = MakeCastle("Ye");
            var bg = MakeBackground(MakeSprite(Color.gray), out var go);
            Assert.IsNotNull(GameObject.Find("TerrainProps"));

            Object.Destroy(go);
            yield return null;
            Assert.IsNull(GameObject.Find("TerrainProps"), "배경이 사라지면 소품 루트도 함께 사라진다");
        }

        // ───────────────────────── 실제 에셋 (Step 12-6 셋업 뒤) ─────────────────────────

        [Test]
        public void RealCatalog_HasAllTerrainsWithArt()
        {
            var catalog = Resources.Load<TerrainThemeCatalog>(TerrainThemeCatalog.ResourceName);
            Assert.IsNotNull(catalog, "TerrainThemeCatalog 가 없습니다. 메뉴 Samkuk > Step 12-6 을 먼저 실행하세요.");

            foreach (CastleTerrain terrain in System.Enum.GetValues(typeof(CastleTerrain)))
            {
                var theme = catalog.Get(terrain);
                Assert.IsNotNull(theme, $"{terrain}: 테마가 없습니다");
                Assert.IsNotNull(theme.groundTile, $"{terrain}: 바닥 타일이 없습니다 (tools/terrain_art/generate.ps1 후 Step 12-6)");
                Assert.AreEqual(256f, theme.groundTile.rect.width, $"{terrain}: 바닥 타일은 256x256");
                Assert.AreEqual(64f, theme.groundTile.pixelsPerUnit, $"{terrain}: 바닥 타일 PPU 64 (4유닛)");
                Assert.AreEqual(TextureWrapMode.Repeat, theme.groundTile.texture.wrapMode, $"{terrain}: 바닥 타일은 Repeat");
                Assert.GreaterOrEqual(theme.props.Count, 4, $"{terrain}: 소품이 너무 적다");
                Assert.Greater(theme.propsPerChunk, 0f);
                Assert.IsNotNull(theme.riverWater, $"{terrain}: 강물 그림이 없습니다 (tools/terrain_art/generate.ps1 후 Step 12-6)");
                Assert.IsNotNull(theme.riverBank, $"{terrain}: 강둑 그림이 없습니다");
                Assert.AreEqual(256f, theme.riverWater.rect.width, $"{terrain}: 강 토막은 256x128");
                Assert.Greater(theme.riverWidth, 1f, $"{terrain}: 강 폭");
                Assert.AreEqual(0.5f, theme.riverWater.pivot.y / theme.riverWater.rect.height, 0.01f, $"{terrain}: 강 토막의 피벗은 가운데");
                foreach (var p in theme.props)
                {
                    Assert.IsNotNull(p.sprite, $"{terrain}/{p.name}: 그림이 없다");
                    Assert.Greater(p.weight, 0f);
                    Assert.LessOrEqual(p.scaleMin, p.scaleMax);
                    Assert.Less(p.sprite.pivot.y / p.sprite.rect.height, 0.2f, $"{terrain}/{p.name}: 피벗이 바닥에 닿는 점(아래쪽)이어야 한다");
                }
            }
            Assert.IsNotNull(catalog.pond.sprite, "연못 그림");
            Assert.IsNotNull(catalog.banner.sprite, "깃발 그림");
        }

        [Test]
        public void RealCatalog_EveryCastleGetsAMap()
        {
            var catalog = Resources.Load<TerrainThemeCatalog>(TerrainThemeCatalog.ResourceName);
            var castles = UnityEditor.AssetDatabase.LoadAssetAtPath<CastleCatalog>("Assets/ScriptableObjects/CastleCatalog.asset");
            Assert.IsNotNull(catalog);
            Assert.IsNotNull(castles);

            foreach (var castle in castles.castles)
            {
                var map = TerrainMap.Create(castle, catalog);
                Assert.IsNotNull(map, $"{castle.displayName}: 맵을 만들 수 없다");
                Assert.Greater(Gather(map, 1).Count, 5, $"{castle.displayName}: 소품이 거의 없다");
            }

            // 46개 성의 맵 색조/밀도가 서로 어느 정도 다양해야 "성마다 다른 맵"이다
            var tints = castles.castles.Select(c => TerrainMap.Create(c, catalog).GroundTint).Distinct().Count();
            Assert.Greater(tints, 30, "성마다 바닥 색조가 거의 다 달라야 한다");
        }
    }
}
