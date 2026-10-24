using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.World;
using UnityEngine;

namespace Samkuk.Tests
{
    /// <summary>언덕·절벽(Step 16-2): 높이 데이터, 자동 생성, 경사로, 절벽 메시, 벽 충돌, 편집·저장.</summary>
    public class TerrainHeightTests
    {
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            MapStore.Disabled = true;
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
            Hd2dSettings.TiltOverride = true;
            TiltView.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in toDestroy) if (o != null) Object.DestroyImmediate(o);
            toDestroy.Clear();
            TiltView.ResetForTests();
            Hd2dSettings.ResetOverrides();
            MapStore.Disabled = false;
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        Sprite MakeSprite()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.1f), 8f);
            toDestroy.Add(tex); toDestroy.Add(sprite);
            return sprite;
        }

        TerrainThemeCatalog MakeCatalog(float density = 0f)
        {
            var c = ScriptableObject.CreateInstance<TerrainThemeCatalog>();
            foreach (CastleTerrain t in System.Enum.GetValues(typeof(CastleTerrain)))
            {
                var theme = ScriptableObject.CreateInstance<TerrainTheme>();
                theme.terrain = t;
                theme.propsPerChunk = density;
                theme.groundTile = MakeSprite();
                theme.props.Add(new TerrainProp { name = "Tree", sprite = MakeSprite(), weight = 1f, standing = true, blockRadius = 0.3f });
                toDestroy.Add(theme);
                c.themes.Add(theme);
            }
            c.pond.sprite = MakeSprite();
            c.banner.sprite = MakeSprite();
            toDestroy.Add(c);
            return c;
        }

        CastleData MakeCastle(string id, CastleTerrain terrain, bool water = false)
        {
            var c = ScriptableObject.CreateInstance<CastleData>();
            c.id = id; c.displayName = id; c.terrain = terrain; c.hasWater = water; c.size = CastleSize.Small;
            toDestroy.Add(c);
            return c;
        }

        /// <summary>칸 (0,0) 의 셀 (2,2) 만 1층인 맵 (세계 (4~6, 4~6)).</summary>
        TerrainMap MakeOneHillMap(RampDir ramp = RampDir.None, int level = 1)
        {
            var map = TerrainMap.Create(MakeCastle("Hill", CastleTerrain.Plain), MakeCatalog());
            var h = new HeightChunk();
            h.Set(2, 2, level, ramp);
            map.SetCustomHeights(Vector2Int.zero, h);
            // 이웃 칸의 자동 언덕이 섞이지 않게 둘레도 평지로
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (dx != 0 || dy != 0) map.SetCustomHeights(new Vector2Int(dx, dy), new HeightChunk());
            return map;
        }

        // ───────────────────────── 데이터 ─────────────────────────

        [Test]
        public void HeightChunk_SetClampsLevel_AndRampNeedsALevel()
        {
            var h = new HeightChunk();
            Assert.IsTrue(h.IsFlat);
            h.Set(1, 1, 9, RampDir.South);
            Assert.AreEqual(TerrainHeight.MaxLevel, h.Level(1, 1));
            Assert.AreEqual(RampDir.South, h.Ramp(1, 1));
            h.Set(1, 1, 0, RampDir.South);
            Assert.AreEqual(RampDir.None, h.Ramp(1, 1), "0층에는 경사로가 없다");
            Assert.IsTrue(h.IsFlat);
            h.Set(5, 5, 2, RampDir.None);
            var copy = h.Clone();
            Assert.IsTrue(copy.SameAs(h));
            copy.Set(5, 5, 1, RampDir.None);
            Assert.IsFalse(copy.SameAs(h), "복사본은 따로 논다");
        }

        [Test]
        public void Surface_RampSlopesFromOneLevelBelow_ToItsLevel()
        {
            Assert.AreEqual(2f, TerrainHeight.Surface(2, RampDir.None, 0.3f, 0.7f));
            Assert.AreEqual(1f, TerrainHeight.Surface(2, RampDir.South, 0.5f, 0f), "남쪽 경사로: 남쪽 끝은 한 층 아래");
            Assert.AreEqual(2f, TerrainHeight.Surface(2, RampDir.South, 0.5f, 1f), "북쪽 끝은 자기 층");
            Assert.AreEqual(1.5f, TerrainHeight.Surface(2, RampDir.North, 0.5f, 0.5f));
            Assert.AreEqual(0f, TerrainHeight.Surface(1, RampDir.West, 0f, 0.5f));
            Assert.AreEqual(1f, TerrainHeight.Surface(1, RampDir.East, 0f, 0.5f));
            Assert.AreEqual(0f, TerrainHeight.Surface(0, RampDir.South, 0.5f, 0.5f), "0층은 늘 0");
            Assert.AreEqual(-1.5f, TerrainHeight.ZOf(1.5f), "위는 -z");
        }

        [Test]
        public void Locate_FindsChunkCellAndFraction()
        {
            TerrainHeight.Locate(new Vector2(5f, 4.5f), out var chunk, out int cx, out int cy, out float fx, out float fy);
            Assert.AreEqual(Vector2Int.zero, chunk);
            Assert.AreEqual(2, cx); Assert.AreEqual(2, cy);
            Assert.AreEqual(0.5f, fx, 1e-5f); Assert.AreEqual(0.25f, fy, 1e-5f);
            TerrainHeight.Locate(new Vector2(-0.5f, 12.5f), out chunk, out cx, out cy, out _, out _);
            Assert.AreEqual(new Vector2Int(-1, 1), chunk);
            Assert.AreEqual(5, cx); Assert.AreEqual(0, cy);
        }

        [Test]
        public void Map_HeightAt_UsesCustomHeights_AndRampInterpolates()
        {
            var map = MakeOneHillMap(RampDir.South);
            Assert.AreEqual(0f, map.HeightAt(new Vector2(1f, 1f)));
            Assert.AreEqual(1f, map.HeightAt(new Vector2(5f, 5.99f)), 1e-3f, "경사로 북쪽 끝 = 1층");
            Assert.AreEqual(0.5f, map.HeightAt(new Vector2(5f, 5f)), 1e-5f, "경사로 가운데");
            Assert.AreEqual(0f, map.HeightAt(new Vector2(5f, 4f)), 1e-5f, "경사로 남쪽 끝 = 0층");
            Assert.AreEqual(0f, map.HeightAt(new Vector2(7f, 5f)), "옆 셀은 평지");
        }

        // ───────────────────────── 자동 생성 ─────────────────────────

        [Test]
        public void Auto_IsDeterministic_AndStartAreaStaysFlat()
        {
            var catalog = MakeCatalog();
            var a = TerrainMap.Create(MakeCastle("Luoyang", CastleTerrain.Mountain), catalog);
            var b = TerrainMap.Create(MakeCastle("Luoyang", CastleTerrain.Mountain), catalog);
            for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                    Assert.IsTrue(a.GetHeights(new Vector2Int(x, y)).SameAs(b.GetHeights(new Vector2Int(x, y))), $"칸 {x},{y}");
            Assert.AreEqual(0f, a.HeightAt(Vector2.zero));
            for (int i = 0; i < 16; i++)
            {
                float ang = i / 16f * Mathf.PI * 2f;
                Assert.AreEqual(0f, a.HeightAt(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * TerrainHeight.FlatRadius * 0.9f), "시작 위치 주변은 평지");
            }
        }

        [Test]
        public void Auto_MountainsHaveHills_PlainsHaveFew_AndRiversStayFlat()
        {
            var catalog = MakeCatalog();
            int Count(TerrainMap map)
            {
                int n = 0;
                for (int y = -4; y <= 4; y++)
                    for (int x = -4; x <= 4; x++)
                    {
                        var h = map.GetHeights(new Vector2Int(x, y));
                        for (int i = 0; i < HeightChunk.Count; i++) if (h.levels[i] > 0) n++;
                    }
                return n;
            }
            int mountain = Count(TerrainMap.Create(MakeCastle("Hanzhong", CastleTerrain.Mountain), catalog));
            int plain = Count(TerrainMap.Create(MakeCastle("Hanzhong", CastleTerrain.Plain), catalog));
            Assert.Greater(mountain, plain, "산악이 평야보다 언덕이 많다");
            Assert.Greater(mountain, 81 * 36 / 20, "산악은 셀의 5% 이상이 언덕");
            Assert.Less(plain, 81 * 36 / 5, "평야는 셀의 20% 미만");

            var river = TerrainMap.Create(MakeCastle("Jianye", CastleTerrain.River, water: true), catalog);
            Assert.IsTrue(river.HasRiver);
            for (int y = -4; y <= 4; y++)
                for (int x = -4; x <= 4; x++)
                {
                    var chunk = new Vector2Int(x, y);
                    var h = river.GetHeights(chunk);
                    for (int j = 0; j < HeightChunk.Cells; j++)
                        for (int i = 0; i < HeightChunk.Cells; i++)
                        {
                            if (h.Level(i, j) == 0) continue;
                            var center = TerrainHeight.CellOrigin(chunk, i, j) + Vector2.one * (TerrainHeight.CellSize * 0.5f);
                            Assert.Greater(river.RiverDistance(center), river.RiverWidth * 0.5f, "강 위에 언덕이 없다");
                        }
                }
        }

        [Test]
        public void Auto_Ramps_OnlyOnEdges_DescendingToOneLevelBelow()
        {
            var map = TerrainMap.Create(MakeCastle("Chengdu", CastleTerrain.Mountain), MakeCatalog());
            int ramps = 0;
            for (int y = -5; y <= 5; y++)
                for (int x = -5; x <= 5; x++)
                {
                    var chunk = new Vector2Int(x, y);
                    var h = map.GetHeights(chunk);
                    for (int j = 0; j < HeightChunk.Cells; j++)
                        for (int i = 0; i < HeightChunk.Cells; i++)
                        {
                            var ramp = h.Ramp(i, j);
                            if (ramp == RampDir.None) continue;
                            ramps++;
                            var o = TerrainHeight.CellOrigin(chunk, i, j);
                            var step = ramp == RampDir.South ? Vector2.down : ramp == RampDir.North ? Vector2.up : ramp == RampDir.West ? Vector2.left : Vector2.right;
                            var below = o + Vector2.one * (TerrainHeight.CellSize * 0.5f) + step * TerrainHeight.CellSize;
                            TerrainHeight.Locate(below, out var nc, out int ncx, out int ncy, out _, out _);
                            Assert.AreEqual(h.Level(i, j) - 1, map.GetHeights(nc).Level(ncx, ncy), "경사로가 내려가는 쪽 이웃은 한 층 낮다");
                        }
                }
            Assert.Greater(ramps, 0, "산악 맵에는 경사로가 생긴다");
        }

        [Test]
        public void Auto_StarterHill_IsNearTheStart_WithARamp_AndOffTheRiver()
        {
            var catalog = MakeCatalog();
            foreach (var map in new[]
            {
                TerrainMap.Create(MakeCastle("Wan", CastleTerrain.Plain), catalog),
                TerrainMap.Create(MakeCastle("Jianye", CastleTerrain.River, water: true), catalog),
                TerrainMap.CreateFreeBattle(catalog),
            })
            {
                Assert.AreEqual(8, map.StarterHillCellCount, "6셀 언덕 + 경사로 아래 평지 1셀 (경사로 셀은 겹침)");
                var ramp = map.StarterRampCenter;
                Assert.Less(ramp.magnitude, 16f, "시작 위치에서 화면 안쪽");
                Assert.Greater(ramp.magnitude, TerrainHeight.FlatRadius, "시작 평지 밖");
                map.GetHeightsAt(ramp, out int level, out var dir);
                Assert.AreEqual(1, level);
                Assert.IsTrue(dir == RampDir.South || dir == RampDir.North, "시작 쪽을 향한 경사로");
                var below = ramp + (dir == RampDir.South ? Vector2.down : Vector2.up) * TerrainHeight.CellSize;
                map.GetHeightsAt(below, out level, out _);
                Assert.AreEqual(0, level, "경사로 아래는 평지");
                var far = ramp + (dir == RampDir.South ? Vector2.up : Vector2.down) * TerrainHeight.CellSize;
                map.GetHeightsAt(far, out level, out _);
                Assert.AreEqual(2, level, "뒷줄 가운데는 2층");
                if (map.HasRiver)
                    Assert.Greater(map.RiverDistance(ramp), map.RiverWidth * 0.5f, "강 위가 아니다");
            }
        }

        // ───────────────────────── 메시 ─────────────────────────

        [Test]
        public void Mesh_RaisedCell_HasTop_AndWallsOnlyWhereHigherThanNeighbor()
        {
            var map = MakeOneHillMap();
            var b = new CliffMeshBuilder.Buffers();
            CliffMeshBuilder.Build(map, Vector2Int.zero, b);
            Assert.AreEqual(1, b.TopQuads, "1층 셀 하나 = 윗면 하나");
            Assert.AreEqual(4, b.WallQuads, "사방이 낮으니 벽 넷");
            foreach (var v in b.vertices) Assert.LessOrEqual(v.z, 0f + 1e-5f, "위는 -z");
            Assert.AreEqual(-1f, b.vertices[0].z, 1e-5f, "윗면은 1층 높이");

            CliffMeshBuilder.Build(map, new Vector2Int(1, 0), b);
            Assert.IsTrue(b.IsEmpty, "평지 칸은 메시가 없다");

            // 경사로: 내려가는 쪽에는 벽이 없고 양옆은 비스듬한 벽
            map = MakeOneHillMap(RampDir.South);
            CliffMeshBuilder.Build(map, Vector2Int.zero, b);
            Assert.AreEqual(1, b.TopQuads);
            Assert.AreEqual(3, b.WallQuads, "남쪽 가장자리는 높이가 같아 벽이 없다");
        }

        [Test]
        public void Mesh_TwoAdjacentCells_ShareNoWallBetween()
        {
            var map = TerrainMap.Create(MakeCastle("Pair", CastleTerrain.Plain), MakeCatalog());
            var h = new HeightChunk();
            h.Set(2, 2, 1, RampDir.None);
            h.Set(3, 2, 1, RampDir.None);
            map.SetCustomHeights(Vector2Int.zero, h);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (dx != 0 || dy != 0) map.SetCustomHeights(new Vector2Int(dx, dy), new HeightChunk());
            var b = new CliffMeshBuilder.Buffers();
            CliffMeshBuilder.Build(map, Vector2Int.zero, b);
            Assert.AreEqual(2, b.TopQuads);
            Assert.AreEqual(6, b.WallQuads, "둘 사이 가장자리에는 벽이 없다 (3 + 3)");
        }

        [Test]
        public void Cliffs_MakeMeshesOnlyUnderTilt_AndRebuildAChunk()
        {
            if (!TiltView.ShaderAvailable) Assert.Ignore($"셰이더 {TiltView.ShaderName} 없음");
            var map = MakeOneHillMap();
            var follow = new GameObject("Follow");
            toDestroy.Add(follow);
            var go = new GameObject("Cliffs");
            toDestroy.Add(go);
            var cliffs = go.AddComponent<TerrainCliffs>();
            cliffs.Initialize(map, follow.transform, Texture2D.whiteTexture);
            Assert.IsFalse(cliffs.Visible, "틸트가 아니면 보이지 않는다");
            Assert.AreEqual(0, cliffs.ActiveChunkCount);

            TiltView.SetReady(true);
            Assert.IsTrue(cliffs.Visible);
            Assert.Greater(cliffs.ActiveChunkCount, 0);
            var filters = go.GetComponentsInChildren<MeshFilter>(false);
            Assert.AreEqual(1, filters.Length, "언덕이 있는 칸 하나만 메시가 있다");
            Assert.AreEqual(2, filters[0].sharedMesh.subMeshCount);

            map.EnsureCustomHeights(Vector2Int.zero).Set(2, 2, 0, RampDir.None);
            cliffs.RebuildChunk(Vector2Int.zero);
            Assert.AreEqual(0, go.GetComponentsInChildren<MeshFilter>(false).Length, "평지로 고치면 메시가 사라진다");

            TiltView.SetReady(false);
            Assert.IsFalse(cliffs.Visible);
        }

        [Test]
        public void CliffArt_IsRepeatingPixelTexture_PerTerrain()
        {
            var a = CliffArt.Texture(CastleTerrain.Mountain);
            var b = CliffArt.Texture(CastleTerrain.Loess);
            Assert.AreSame(a, CliffArt.Texture(CastleTerrain.Mountain), "지형마다 하나를 돌려 쓴다");
            Assert.AreNotSame(a, b);
            Assert.AreEqual(TextureWrapMode.Repeat, a.wrapMode);
            Assert.AreEqual(FilterMode.Point, a.filterMode);
            Assert.AreEqual(CliffArt.Size, a.width);
        }

        // ───────────────────────── 벽 충돌 ─────────────────────────

        [Test]
        public void Collision_ExtractsWalls_AroundRaisedCell_ButNotRampBottom()
        {
            var map = MakeOneHillMap();
            var col = new TerrainCollision(map);
            var walls = new List<TerrainCollision.Wall>();
            col.CollectWalls(Vector2Int.zero, walls);
            // 셀 (2,2) = 세계 (4~6, 4~6): 동(6)·북(6) 가장자리는 이 셀이, 서(4)·남(4) 가장자리는 이웃 셀의 동·북 가장자리로 뽑힌다
            Assert.AreEqual(4, walls.Count);

            map = MakeOneHillMap(RampDir.South);
            col = new TerrainCollision(map);
            walls.Clear();
            col.CollectWalls(Vector2Int.zero, walls);
            Assert.AreEqual(3, walls.Count, "경사로의 남쪽 가장자리에는 벽이 없다");
        }

        [Test]
        public void Collision_WallBlocksCrossing_AndSlidesAlong_AndRampLetsThrough()
        {
            var map = MakeOneHillMap();
            var col = new TerrainCollision(map);
            // 서쪽 벽(x = 4) 바로 밖에서 동쪽으로 가면 막혀 멈추고, 비스듬히 가면 벽을 따라 미끄러진다
            var v = col.Resolve(new Vector2(3.7f, 5f), 0.3f, new Vector2(3f, 0f), 0.02f);
            Assert.LessOrEqual(v.x, 1e-3f, "벽을 뚫고 들어가지 않는다");
            v = col.Resolve(new Vector2(3.7f, 5f), 0.3f, new Vector2(3f, 3f), 0.02f);
            Assert.LessOrEqual(v.x, 1e-3f);
            Assert.AreEqual(3f, v.y, 1e-3f, "벽을 따라 미끄러진다");
            Assert.IsTrue(col.Overlaps(new Vector2(4.1f, 5f), 0.3f));
            Assert.IsFalse(col.Overlaps(new Vector2(3f, 5f), 0.3f));
            // 벽 위에 서면 밖으로 밀려난다
            var pushed = col.PushOut(new Vector2(4.05f, 5f), 0.3f);
            Assert.Greater(Mathf.Abs(pushed.x - 4f), 0.3f);

            // 같은 자리가 남쪽 경사로면 남쪽에서 북쪽으로 올라갈 수 있다
            map = MakeOneHillMap(RampDir.South);
            col = new TerrainCollision(map);
            v = col.Resolve(new Vector2(5f, 3.75f), 0.3f, new Vector2(0f, 3f), 0.02f);
            Assert.AreEqual(3f, v.y, 1e-3f, "경사로 아래쪽에는 벽이 없다");
            v = col.Resolve(new Vector2(3.7f, 5f), 0.3f, new Vector2(3f, 0f), 0.02f);
            Assert.LessOrEqual(v.x, 1e-3f, "경사로 옆은 여전히 벽");
        }

        [Test]
        public void ZAt_IsZeroWithoutTilt_AndFollowsTheMapUnderTilt()
        {
            var map = MakeOneHillMap();
            TerrainCollision.Active = new TerrainCollision(map);
            Assert.AreEqual(0f, TerrainHeight.ZAt(new Vector2(5f, 5f)), "틸트가 아니면 높이를 쓰지 않는다");
            TiltView.SetReady(true);
            Assert.AreEqual(-1f, TerrainHeight.ZAt(new Vector2(5f, 5f)), 1e-5f);
            Assert.AreEqual(new Vector3(5f, 5f, -1f), TerrainHeight.Feet(new Vector2(5f, 5f)));
            Assert.AreEqual(0f, TerrainHeight.ZAt(new Vector2(1f, 1f)));
        }

        // ───────────────────────── 편집 / 저장 ─────────────────────────

        [Test]
        public void Model_HeightEdits_AreUndoable_AndMarkNeighborsDirty()
        {
            var map = TerrainMap.Create(MakeCastle("Edit", CastleTerrain.Plain), MakeCatalog());
            var model = new MapEditModel(map);
            var at = new Vector2(1f, 1f);   // 칸 (0,0) 셀 (0,0): 칸 가장자리라 이웃 칸도 다시 만들어야 한다
            model.GetCell(at, out int level, out var ramp);
            Assert.AreEqual(0, level);

            Assert.IsTrue(model.ChangeLevel(at, +1));
            model.GetCell(at, out level, out ramp);
            Assert.AreEqual(1, level);
            Assert.IsTrue(map.IsCustom(Vector2Int.zero));
            var dirty = model.ConsumeDirtyChunks();
            CollectionAssert.Contains(dirty, Vector2Int.zero);
            CollectionAssert.Contains(dirty, new Vector2Int(-1, -1));
            Assert.AreEqual(9, dirty.Count, "둘레 칸까지");

            Assert.IsTrue(model.SetRamp(at, RampDir.West));
            model.GetCell(at, out level, out ramp);
            Assert.AreEqual(RampDir.West, ramp);
            Assert.IsFalse(model.SetRamp(at, RampDir.West), "같은 값이면 바뀌지 않는다");
            Assert.IsTrue(model.Flatten(at));
            model.GetCell(at, out level, out ramp);
            Assert.AreEqual(0, level);
            Assert.AreEqual(RampDir.None, ramp);
            Assert.IsFalse(model.ChangeLevel(at, -1), "0층 아래로는 못 내린다");

            Assert.IsTrue(model.Undo());
            model.GetCell(at, out level, out ramp);
            Assert.AreEqual(RampDir.West, ramp);
            Assert.IsTrue(model.Undo());
            Assert.IsTrue(model.Undo());
            model.GetCell(at, out level, out ramp);
            Assert.AreEqual(0, level);
            Assert.IsTrue(model.Redo());
            model.GetCell(at, out level, out ramp);
            Assert.AreEqual(1, level);
            Assert.IsTrue(model.Unsaved);
        }

        [Test]
        public void Model_SetRampOnFlatCell_RaisesIt_AndResetChunkDropsHeights()
        {
            var map = TerrainMap.Create(MakeCastle("Edit2", CastleTerrain.Plain), MakeCatalog());
            var model = new MapEditModel(map);
            var at = new Vector2(7f, 7f);
            Assert.IsTrue(model.SetRamp(at, RampDir.South));
            model.GetCell(at, out int level, out var ramp);
            Assert.AreEqual(1, level);
            Assert.AreEqual(RampDir.South, ramp);
            Assert.IsTrue(model.ResetChunk(Vector2Int.zero));
            Assert.IsFalse(map.IsCustom(Vector2Int.zero));
            model.GetCell(at, out level, out _);
            Assert.AreEqual(map.GenerateHeights(Vector2Int.zero).Level(3, 3), level, "자동 생성 높이로 돌아간다");
        }

        [Test]
        public void Layout_RoundTripsHeights_AndOldFilesFallBackToAuto()
        {
            var catalog = MakeCatalog();
            var map = TerrainMap.Create(MakeCastle("Save", CastleTerrain.Mountain), catalog);
            var h = map.EnsureCustomHeights(new Vector2Int(1, 2));
            h.Set(4, 1, 2, RampDir.East);
            h.Set(0, 0, 1, RampDir.None);
            var data = map.ExportLayout();
            Assert.AreEqual(MapLayoutData.CurrentVersion, data.version);
            var chunk = data.Find(1, 2);
            Assert.IsNotNull(chunk);
            Assert.AreEqual(HeightChunk.Count, chunk.levels.Length);
            Assert.AreEqual(2, chunk.levels[HeightChunk.Index(4, 1)]);
            Assert.AreEqual((int)RampDir.East, chunk.ramps[HeightChunk.Index(4, 1)]);

            var json = JsonUtility.ToJson(data);
            var back = JsonUtility.FromJson<MapLayoutData>(json);
            var map2 = TerrainMap.Create(MakeCastle("Save", CastleTerrain.Mountain), catalog);
            map2.ImportLayout(back);
            Assert.IsTrue(map2.GetHeights(new Vector2Int(1, 2)).SameAs(h));
            Assert.IsTrue(map2.IsCustom(new Vector2Int(1, 2)));

            // 높이가 없는 예전 파일: 그 칸은 자동 생성 높이
            var old = new MapLayoutData { castleId = "Save", version = 1 };
            old.chunks.Add(new MapChunk { cx = 0, cy = 3 });
            var map3 = TerrainMap.Create(MakeCastle("Save", CastleTerrain.Mountain), catalog);
            map3.ImportLayout(old);
            Assert.IsTrue(map3.GetHeights(new Vector2Int(0, 3)).SameAs(map3.GenerateHeights(new Vector2Int(0, 3))));
        }
    }
}
