using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.World;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Samkuk.Tests
{
    /// <summary>맵 편집기(맵툴): 편집 규칙(MapEditModel), 저장 파일(MapStore), 전투가 고친 맵을 쓰는지, 편집기 화면의 기본 동작.</summary>
    public class MapEditorTests
    {
        readonly List<Object> toDestroy = new List<Object>();
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "samkuk_maptests_" + System.Guid.NewGuid().ToString("N"));
            MapStore.PathOverride = tempDir;
            MapStore.Disabled = false;
            TerrainThemeCatalog.Disabled = false;
            TerrainThemeCatalog.Use(null);
            GameSession.SortieCastle = null;
            GameSession.MapTest = false;
            DestroyLeftoverProps();
        }

        [TearDown]
        public void TearDown()
        {
            MapStore.PathOverride = null;
            MapStore.Disabled = false;
            TerrainThemeCatalog.Disabled = false;
            TerrainThemeCatalog.Use(null);
            GameSession.SortieCastle = null;
            GameSession.MapTest = false;
            MapEditorSession.LastCastleId = null;
            Audio.AudioManager.DestroyInstance();
            foreach (var o in toDestroy) if (o != null) Object.DestroyImmediate(o);
            toDestroy.Clear();
            DestroyLeftoverProps();
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        static void DestroyLeftoverProps()
        {
            foreach (var s in Object.FindObjectsByType<TerrainPropSpawner>())
                Object.DestroyImmediate(s.gameObject);
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        Sprite MakeSprite(string name, Color color)
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[64];
            for (int i = 0; i < px.Length; i++) px[i] = color;
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.1f), 8f);
            sprite.name = name;
            toDestroy.Add(tex);
            toDestroy.Add(sprite);
            return sprite;
        }

        TerrainTheme MakeTheme(CastleTerrain terrain)
        {
            var t = ScriptableObject.CreateInstance<TerrainTheme>();
            string n = terrain.ToString();
            t.terrain = terrain;
            t.propsPerChunk = 8f;
            t.groundTile = MakeSprite("Ground_" + n, Color.green);
            t.props.Add(new TerrainProp { name = "A", sprite = MakeSprite("Prop_" + n + "_A", Color.red), weight = 3f, scaleMin = 0.9f, scaleMax = 1.2f });
            t.props.Add(new TerrainProp { name = "B", sprite = MakeSprite("Prop_" + n + "_B", Color.blue), weight = 2f, scaleMin = 0.9f, scaleMax = 1.2f });
            t.riverWater = MakeSprite("River_" + n + "_Water", Color.cyan);
            t.riverBank = MakeSprite("River_" + n + "_Bank", Color.gray);
            t.riverWidth = 3.5f;
            toDestroy.Add(t);
            return t;
        }

        TerrainThemeCatalog MakeCatalog()
        {
            var c = ScriptableObject.CreateInstance<TerrainThemeCatalog>();
            foreach (CastleTerrain t in System.Enum.GetValues(typeof(CastleTerrain))) c.themes.Add(MakeTheme(t));
            c.pond.sprite = MakeSprite("Prop_Pond", Color.cyan);
            c.banner.sprite = MakeSprite("Prop_Banner", Color.magenta);
            toDestroy.Add(c);
            return c;
        }

        CastleData MakeCastle(string id, CastleTerrain terrain = CastleTerrain.Plain, bool water = false)
        {
            var c = ScriptableObject.CreateInstance<CastleData>();
            c.id = id; c.displayName = id; c.terrain = terrain; c.size = CastleSize.Medium; c.hasWater = water;
            toDestroy.Add(c);
            return c;
        }

        TerrainMap MakeMap(string id = "Wan", CastleTerrain terrain = CastleTerrain.Plain, bool water = false)
        {
            var catalog = MakeCatalog();
            TerrainThemeCatalog.Use(catalog);
            return TerrainMap.Create(MakeCastle(id, terrain, water), catalog);
        }

        static PropPlacement Prop(TerrainMap map, string name, Vector2 pos, float scale = 1f)
        {
            var prop = map.Theme.props.First(p => p.name == name);
            return new PropPlacement { prop = prop, position = pos, scale = scale, stretchY = 1f, order = TerrainMap.OrderPropBase };
        }

        // ───────────────────────── 저장 파일 ─────────────────────────

        [Test]
        public void Export_Import_RoundTripsAllItems()
        {
            var map = MakeMap(water: true);
            var model = new MapEditModel(map);
            var a = Prop(map, "A", new Vector2(15f, 20f), 1.3f); a.rotation = 20f; a.flipX = true;
            model.Add(a);
            model.Add(Prop(map, "B", new Vector2(-5f, -7f)));

            var layout = map.ExportLayout();
            Assert.AreEqual("Wan", layout.castleId);
            Assert.AreEqual(2, layout.chunks.Count, "고친 칸 두 곳만 저장된다");
            Assert.IsTrue(string.IsNullOrEmpty(layout.terrain), "지형을 바꾸지 않았으면 기록하지 않는다");

            var other = TerrainMap.Create(map.Castle, map.Catalog);
            other.ImportLayout(layout);
            var chunk = TerrainMap.ChunkOf(new Vector2(15f, 20f));
            Assert.IsTrue(other.IsCustom(chunk));
            var items = other.GetChunk(chunk.x, chunk.y);
            var back = items.First(p => Mathf.Approximately(p.position.x, 15f) && Mathf.Approximately(p.position.y, 20f));
            Assert.AreEqual("A", back.prop.name);
            Assert.AreEqual(1.3f, back.scale, 1e-4f);
            Assert.AreEqual(20f, back.rotation, 1e-4f);
            Assert.IsTrue(back.flipX);
            Assert.AreEqual(map.Generate(chunk.x, chunk.y).Count + 1, items.Count, "처음 고칠 때 자동 생성 내용을 옮겨 오고 하나를 더한 것");
        }

        [Test]
        public void Layout_ItemKinds_KeepTheirDrawOrder()
        {
            var map = MakeMap(water: true);
            var entries = MapPalette.Build(map.Catalog);
            var model = new MapEditModel(map);
            var rng = new System.Random(1);
            foreach (var e in entries)
            {
                model.Add(e.Create(new Vector2(30f, 30f), rng));
                if (e.companion != null) model.Add(e.companion.Create(new Vector2(30f, 30f), rng));
            }
            var other = TerrainMap.Create(map.Castle, map.Catalog);
            other.ImportLayout(map.ExportLayout());
            var chunk = TerrainMap.ChunkOf(new Vector2(30f, 30f));
            var original = map.GetChunk(chunk.x, chunk.y);
            var imported = other.GetChunk(chunk.x, chunk.y);
            Assert.AreEqual(original.Count, imported.Count);
            for (int i = 0; i < original.Count; i++)
            {
                Assert.AreEqual(TerrainMap.KindOf(original[i]), TerrainMap.KindOf(imported[i]), "종류(그리기 순서)가 저장 뒤에도 같다");
                Assert.AreEqual(original[i].order, imported[i].order);
                Assert.AreEqual(original[i].tinted, imported[i].tinted);
            }
        }

        [Test]
        public void Store_SavesAndLoadsJson_AndDeletes()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            model.Add(Prop(map, "A", new Vector2(3f, 3f)));
            var layout = map.ExportLayout();

            Assert.IsNull(MapStore.Load("Wan"));
            Assert.IsTrue(MapStore.Save(layout, out string path));
            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual(Path.Combine(tempDir, "Wan.json"), path);

            var loaded = MapStore.Load("Wan");
            Assert.IsNotNull(loaded);
            Assert.AreEqual(layout.ItemCount, loaded.ItemCount);
            Assert.AreEqual(layout.chunks.Count, loaded.chunks.Count);

            Assert.IsTrue(MapStore.Delete("Wan"));
            Assert.IsNull(MapStore.Load("Wan"));
            Assert.IsFalse(MapStore.Delete("Wan"));
        }

        [Test]
        public void Store_BrokenFile_IsIgnored_AndInvalidNamesAreSafe()
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(MapStore.FileFor("Bad"), "{ 이건 JSON 이 아니다");
            Assert.IsNull(MapStore.Load("Bad"));
            Assert.IsNull(MapStore.Load(null));
            Assert.IsNull(MapStore.Load(""));
            StringAssert.DoesNotContain("/", MapStore.FileName("a/b:c"));
            MapStore.Disabled = true;
            File.WriteAllText(MapStore.FileFor("Ok"), JsonUtility.ToJson(new MapLayoutData { castleId = "Ok" }));
            Assert.IsNull(MapStore.Load("Ok"), "Disabled 이면 저장된 맵이 없는 것처럼");
        }

        [Test]
        public void Import_SkipsItemsWithUnknownSprites()
        {
            var map = MakeMap();
            var data = new MapLayoutData { castleId = "Wan" };
            var chunk = new MapChunk { cx = 0, cy = 0 };
            chunk.items.Add(new MapItem { sprite = "Prop_Plain_A", x = 5f, y = 5f, scale = 1f });
            chunk.items.Add(new MapItem { sprite = "삭제된_그림", x = 6f, y = 6f, scale = 1f });
            data.chunks.Add(chunk);
            map.ImportLayout(data);
            Assert.AreEqual(1, map.GetChunk(0, 0).Count, "그림을 찾지 못한 물건만 건너뛴다");
        }

        // ───────────────────────── 전투가 고친 맵을 쓴다 ─────────────────────────

        [Test]
        public void CreateForBattle_UsesStoredLayout_AndTerrainOverride()
        {
            var catalog = MakeCatalog();
            var castle = MakeCastle("Wan", CastleTerrain.Plain);
            var plain = TerrainMap.Create(castle, catalog);
            var model = new MapEditModel(plain);
            model.Add(Prop(plain, "A", new Vector2(40f, 40f)));
            var layout = plain.ExportLayout();
            layout.terrain = CastleTerrain.Mountain.ToString();
            Assert.IsTrue(MapStore.Save(layout, out _));

            var battle = TerrainMap.CreateForBattle(castle, catalog);
            Assert.AreEqual(CastleTerrain.Mountain, battle.Terrain, "고른 바닥 지형이 전투에도 쓰인다");
            Assert.AreSame(catalog.Get(CastleTerrain.Mountain), battle.Theme);
            Assert.IsTrue(battle.IsCustom(TerrainMap.ChunkOf(new Vector2(40f, 40f))));

            // 고치지 않은 칸은 평소와 같다 (자동 생성)
            var fresh = TerrainMap.Create(castle, catalog, CastleTerrain.Mountain);
            CollectionAssert.AreEqual(fresh.Layout(-3, -3).Select(p => p.position), battle.Layout(-3, -3).Select(p => p.position));
            CollectionAssert.AreEqual(fresh.Generate(-3, -3).Select(p => p.position), battle.GetChunk(-3, -3).Select(p => p.position));
        }

        [Test]
        public void CreateForBattle_WithoutStoredLayout_IsTheNormalMap()
        {
            var catalog = MakeCatalog();
            var castle = MakeCastle("Wan");
            var battle = TerrainMap.CreateForBattle(castle, catalog);
            Assert.IsFalse(battle.HasCustom);
            CollectionAssert.AreEqual(TerrainMap.Create(castle, catalog).Layout(2, 2).Select(p => p.position), battle.Layout(2, 2).Select(p => p.position));
            Assert.IsNull(TerrainMap.CreateForBattle(null, catalog));
        }

        [Test]
        public void FreeBattleMap_CanAlsoBeEdited()
        {
            var catalog = MakeCatalog();
            var map = TerrainMap.CreateFreeBattle(catalog);
            Assert.AreEqual(MapStore.FreeBattleId, map.Castle.id);
            var model = new MapEditModel(map);
            model.Add(Prop(map, "A", new Vector2(22f, 22f)));
            MapStore.Save(map.ExportLayout(), out _);
            Assert.IsTrue(TerrainMap.CreateFreeBattle(catalog).HasCustom, "성 없이 시작한 판도 고친 맵을 쓴다");
        }

        [Test]
        public void Background_ApplyMap_ShowsTheGivenMap()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            model.Add(Prop(map, "A", new Vector2(20f, 20f)));

            var go = new GameObject("TestBg", typeof(SpriteRenderer));
            toDestroy.Add(go);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = MakeSprite("Default", Color.gray);
            sr.sortingLayerName = GameLayers.Sorting.Background;
            var bg = go.AddComponent<InfiniteBackground>();

            bg.ApplyMap(map);
            Assert.AreSame(map, bg.Props.Map, "편집 중인 맵 그대로 보여 준다");
            Assert.AreSame(map.Theme.groundTile, sr.sprite);
            Assert.IsTrue(bg.Props.Map.IsCustom(TerrainMap.ChunkOf(new Vector2(20f, 20f))));
        }

        [Test]
        public void Spawner_ShowsCustomChunks_AndRebuildsAChangedChunk()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            var follow = new GameObject("Follow");
            toDestroy.Add(follow);
            var refGo = new GameObject("Ref", typeof(SpriteRenderer));
            toDestroy.Add(refGo);
            refGo.GetComponent<SpriteRenderer>().sortingLayerName = GameLayers.Sorting.Background;
            var go = new GameObject("Spawner");
            toDestroy.Add(go);
            var spawner = go.AddComponent<TerrainPropSpawner>();
            spawner.Initialize(map, follow.transform, refGo.GetComponent<SpriteRenderer>());

            model.ClearChunk(new Vector2Int(0, 0));
            foreach (var c in model.ConsumeDirtyChunks()) spawner.RebuildChunk(c);
            int withEmpty = spawner.ActivePropCount;

            model.Add(Prop(map, "A", new Vector2(6f, 6f)));
            foreach (var c in model.ConsumeDirtyChunks()) spawner.RebuildChunk(c);
            Assert.AreEqual(withEmpty + 1, spawner.ActivePropCount, "고친 칸만 다시 만들어져 물건이 하나 늘었다");
            Assert.AreEqual(spawner.ActivePropCount, spawner.GetComponentsInChildren<SpriteRenderer>(false).Length);

            spawner.RebuildChunk(new Vector2Int(500, 500));   // 활성이 아닌 칸은 무시
            Assert.AreEqual(spawner.ActivePropCount, spawner.GetComponentsInChildren<SpriteRenderer>(false).Length);
        }

        // ───────────────────────── 편집 규칙 ─────────────────────────

        [Test]
        public void Add_BakesTheChunkOnce_AndOnlyThatChunk()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            var generated = map.Generate(1, 1);
            Assert.IsFalse(map.IsCustom(new Vector2Int(1, 1)));

            var r = model.Add(Prop(map, "A", new Vector2(14f, 14f)));
            Assert.AreEqual(new Vector2Int(1, 1), r.chunk);
            Assert.IsTrue(map.IsCustom(new Vector2Int(1, 1)));
            Assert.IsFalse(map.IsCustom(new Vector2Int(1, 0)), "이웃 칸은 그대로 자동 생성");
            Assert.AreEqual(generated.Count + 1, map.GetChunk(1, 1).Count);
            CollectionAssert.AreEqual(generated.Select(p => p.position), map.GetChunk(1, 1).Take(generated.Count).Select(p => p.position), "자동 생성 내용을 그대로 옮겨 온다");
            Assert.AreEqual(1, map.CustomChunkCount);
            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 1) }, model.ConsumeDirtyChunks());
            Assert.IsEmpty(model.ConsumeDirtyChunks(), "한 번 알려 주면 비운다");
        }

        [Test]
        public void Add_PropOrder_FollowsItsHeight()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            var low = model.Add(Prop(map, "A", new Vector2(14f, 12.5f)));
            var high = model.Add(Prop(map, "A", new Vector2(14f, 23f)));
            model.TryGet(low, out var lowP);
            model.TryGet(high, out var highP);
            Assert.Greater(lowP.order, highP.order, "아래쪽 소품이 위에 그려진다");
            Assert.GreaterOrEqual(lowP.order, TerrainMap.OrderPropBase);
        }

        [Test]
        public void Replace_MovesItemAcrossChunks_AndRecomputesChunk()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            var r = model.Add(Prop(map, "A", new Vector2(5f, 5f)));
            model.TryGet(r, out var p);
            p.position = new Vector2(30f, 5f);
            var moved = model.Replace(r, p);

            Assert.AreEqual(TerrainMap.ChunkOf(new Vector2(30f, 5f)), moved.chunk);
            Assert.IsTrue(model.TryGet(moved, out var back));
            Assert.AreEqual(30f, back.position.x);
            Assert.IsFalse(map.GetChunk(0, 0).Any(q => q.position == new Vector2(5f, 5f)), "옛 칸에서는 사라진다");
            var dirty = model.ConsumeDirtyChunks();
            CollectionAssert.IsSupersetOf(dirty, new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) });
        }

        [Test]
        public void Remove_DeletesTheItem_AndInvalidRefsAreSafe()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            var r = model.Add(Prop(map, "A", new Vector2(5f, 5f)));
            int before = map.GetChunk(0, 0).Count;
            Assert.IsTrue(model.Remove(r));
            Assert.AreEqual(before - 1, map.GetChunk(0, 0).Count);
            Assert.IsFalse(model.Remove(ItemRef.None));
            Assert.IsFalse(model.Remove(new ItemRef(new Vector2Int(0, 0), 9999)));
            Assert.IsFalse(model.TryGet(ItemRef.None, out _));
        }

        [Test]
        public void FindNearest_PrefersPropsOverPatches_AndMissesFarAway()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            var patch = new PropPlacement { prop = TerrainDecals.PatchProp, position = new Vector2(70f, 70f), scale = 2f, stretchY = 0.7f, order = TerrainMap.OrderPatch, tinted = true, tint = new Color(0, 0, 0, 0.1f) };
            model.Add(patch);
            var propRef = model.Add(Prop(map, "A", new Vector2(70f, 70f)));
            var centerOfProp = MapEditModel.VisualCenter(Prop(map, "A", new Vector2(70f, 70f)));

            Assert.IsTrue(model.FindNearest(centerOfProp, out var found));
            Assert.IsTrue(model.TryGet(found, out var p));
            Assert.AreEqual("A", p.prop.name, "소품이 얼룩보다 먼저 잡힌다");
            Assert.AreEqual(propRef, found);
            Assert.IsFalse(model.FindNearest(new Vector2(900f, 900f), out _));
        }

        [Test]
        public void EraseCircle_RemovesOnlyInsideAndRespectsFilter()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            model.ClearChunk(new Vector2Int(5, 5));   // 칸 (5,5) = x,y 60~72. 이웃 칸의 자동 생성 물건이 섞이지 않게 칸 안쪽에서만 지운다
            model.Add(Prop(map, "A", new Vector2(65f, 65f)));
            model.Add(Prop(map, "B", new Vector2(66f, 65f)));
            model.Add(Prop(map, "A", new Vector2(70.5f, 70.5f)));
            model.ConsumeDirtyChunks();

            Assert.AreEqual(0, model.EraseCircle(new Vector2(100f, 100f), 3f), "멀면 아무것도 지우지 않고 칸도 건드리지 않는다");
            Assert.AreEqual(0, map.CustomChunks.Count(c => c != new Vector2Int(5, 5)));

            Assert.AreEqual(1, model.EraseCircle(new Vector2(65f, 65f), 3f, p => p.prop.name == "B"), "필터에 맞는 것만");
            Assert.AreEqual(2, map.GetChunk(5, 5).Count);
            Assert.AreEqual(1, model.EraseCircle(new Vector2(65f, 65f), 3f));
            Assert.AreEqual(1, map.GetChunk(5, 5).Count);
        }

        [Test]
        public void EraseCircle_OverSeveralAutoChunks_BakesThemAndRemovesEverything()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            var center = new Vector2(24f, 24f);   // 네 칸의 모서리
            int expected = 0;
            for (int cy = 1; cy <= 2; cy++)
                for (int cx = 1; cx <= 2; cx++)
                    expected += map.GetChunk(cx, cy).Count(p => Vector2.Distance(center, p.position) <= 5f || Vector2.Distance(center, MapEditModel.VisualCenter(p)) <= 5f);
            Assert.Greater(expected, 0);

            Assert.AreEqual(expected, model.EraseCircle(center, 5f));
            foreach (var p in Enumerable.Range(0, 4).SelectMany(i => map.GetChunk(1 + i % 2, 1 + i / 2)))
                Assert.Greater(Vector2.Distance(center, p.position), 5f - 0.001f);
        }

        [Test]
        public void Undo_Redo_RestoreMapAndMarkChunksDirty()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            Assert.IsFalse(model.CanUndo);
            model.Add(Prop(map, "A", new Vector2(5f, 5f)));
            model.Add(Prop(map, "B", new Vector2(6f, 6f)));
            model.ConsumeDirtyChunks();
            int after2 = map.GetChunk(0, 0).Count;

            Assert.IsTrue(model.Undo());
            Assert.AreEqual(after2 - 1, map.GetChunk(0, 0).Count);
            CollectionAssert.Contains(model.ConsumeDirtyChunks(), new Vector2Int(0, 0));
            Assert.IsTrue(model.Undo());
            Assert.IsFalse(map.HasCustom, "처음 상태(자동 생성)로 돌아온다");
            Assert.IsFalse(model.Undo());

            Assert.IsTrue(model.Redo());
            Assert.IsTrue(model.Redo());
            Assert.AreEqual(after2, map.GetChunk(0, 0).Count);
            Assert.IsFalse(model.Redo());

            model.Undo();
            model.Add(Prop(map, "A", new Vector2(9f, 9f)));
            Assert.IsFalse(model.CanRedo, "새로 고치면 다시 실행 기록은 사라진다");
        }

        [Test]
        public void Stroke_IsOneUndoStep()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            int generated = map.Generate(0, 0).Count;
            model.BeginStroke();
            for (int i = 0; i < 5; i++) model.Add(Prop(map, "A", new Vector2(2f + i, 3f)));
            model.EndStroke();
            Assert.AreEqual(generated + 5, map.GetChunk(0, 0).Count);

            Assert.IsTrue(model.Undo());
            Assert.AreEqual(generated, map.GetChunk(0, 0).Count, "한 획 전체가 한 번에 취소");
            Assert.IsFalse(model.Undo(), "획 안의 조작마다 기록이 쌓이지 않는다");
        }

        [Test]
        public void ResetChunk_ClearChunk_ResetAll()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            var generated = map.Generate(0, 0).Select(p => p.position).ToList();
            Assert.IsFalse(model.ResetChunk(new Vector2Int(0, 0)), "고친 적 없는 칸은 되돌릴 것이 없다");

            model.ClearChunk(new Vector2Int(0, 0));
            Assert.IsEmpty(map.GetChunk(0, 0));
            Assert.IsTrue(model.ResetChunk(new Vector2Int(0, 0)));
            CollectionAssert.AreEqual(generated, map.GetChunk(0, 0).Select(p => p.position));

            model.Add(Prop(map, "A", new Vector2(40f, 40f)));
            model.ClearChunk(new Vector2Int(0, 0));
            Assert.IsTrue(model.ResetAll());
            Assert.IsFalse(map.HasCustom);
            Assert.IsFalse(model.ResetAll());
        }

        [Test]
        public void Unsaved_TracksChanges()
        {
            var map = MakeMap();
            var model = new MapEditModel(map);
            Assert.IsFalse(model.Unsaved);
            model.Add(Prop(map, "A", new Vector2(5f, 5f)));
            Assert.IsTrue(model.Unsaved);
            model.MarkSaved();
            Assert.IsFalse(model.Unsaved);
            model.Undo();
            Assert.IsTrue(model.Unsaved);
            model.MarkSaved();
            model.MarkChanged();
            Assert.IsTrue(model.Unsaved);
        }

        // ───────────────────────── 팔레트 ─────────────────────────

        [Test]
        public void Palette_HasTerrainGroupsRiverAndSharedItems()
        {
            var catalog = MakeCatalog();
            var entries = MapPalette.Build(catalog);
            var groups = MapPalette.Groups(entries);
            Assert.AreEqual(7, groups.Count, "지형 6곳 + 공용");
            Assert.AreEqual(MapPalette.SharedGroup, groups.Last());
            CollectionAssert.Contains(groups, MapPalette.TerrainLabel(CastleTerrain.Plain));

            var river = entries.First(e => e.label == "강" && e.group == MapPalette.TerrainLabel(CastleTerrain.Plain));
            Assert.AreEqual(MapItemKind.Water, river.kind);
            Assert.IsNotNull(river.companion, "강물을 놓으면 강둑도 함께");
            Assert.AreEqual(MapItemKind.Bank, river.companion.kind);
            Assert.IsTrue(river.alignToStroke);
            Assert.AreEqual(TerrainMap.RiverStretch(3.5f, false), river.stretchMin, 1e-4f);

            Assert.IsTrue(entries.Any(e => e.kind == MapItemKind.Pond));
            Assert.AreEqual(4, entries.Count(e => e.kind == MapItemKind.Patch));
            Assert.IsTrue(entries.Where(e => e.kind == MapItemKind.Patch).All(e => e.tinted && e.prop.sprite != null));
            Assert.IsEmpty(MapPalette.Build(null));
        }

        [Test]
        public void PaletteEntry_Create_MakesPlacementWithKindOrder()
        {
            var catalog = MakeCatalog();
            var entries = MapPalette.Build(catalog);
            var rng = new System.Random(3);
            foreach (var e in entries)
            {
                var p = e.Create(new Vector2(5f, 5f), rng, 33f);
                Assert.AreEqual(e.kind, TerrainMap.KindOf(p), $"{e.label}: 종류가 그리기 순서로 구분된다");
                Assert.That(p.scale, Is.InRange(e.scaleMin - 1e-4f, e.scaleMax + 1e-4f));
                if (e.kind == MapItemKind.Water || e.kind == MapItemKind.Bank) Assert.AreEqual(33f, p.rotation, "강은 끌어 가는 방향으로 눕는다");
            }
        }

        // ───────────────────────── 편집기 화면 ─────────────────────────

        MapEditorController MakeController(out List<string> loaded)
        {
            var catalog = MakeCatalog();
            TerrainThemeCatalog.Use(catalog);
            var go = new GameObject("MapEditorTest");
            toDestroy.Add(go);
            var controller = go.AddComponent<MapEditorController>();
            var list = new List<string>();
            controller.SceneLoader = list.Add;
            controller.EnsureBuilt();
            loaded = list;

            // 편집기가 만든 오브젝트(배경, 카메라, 이벤트 시스템)도 정리 대상
            var bg = Object.FindAnyObjectByType<InfiniteBackground>();
            if (bg != null) toDestroy.Add(bg.gameObject);
            if (controller.Cam != null && controller.Cam.gameObject.name == "Main Camera") toDestroy.Add(controller.Cam.gameObject);
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es != null) toDestroy.Add(es.gameObject);
            return controller;
        }

        [Test]
        public void Controller_Opens_FreeBattleMap_WithPaletteAndTools()
        {
            var c = MakeController(out _);
            Assert.IsNotNull(c.Model);
            Assert.AreEqual(MapStore.FreeBattleId, c.CurrentCastle.id, "맨 앞은 자유 전투 맵");
            Assert.AreEqual(MapEditorController.Tool.Place, c.CurrentTool);
            Assert.Greater(c.Palette.Count, 10);
            Assert.IsFalse(c.Model.Unsaved);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(GameSession.MapTest);
        }

        [Test]
        public void Controller_PlaceSelectModifyUndoSave()
        {
            var c = MakeController(out _);
            var entry = c.Palette.First(e => e.kind == MapItemKind.Prop && e.label == "A");
            c.SelectEntry(entry);
            Assert.AreEqual(entry, c.SelectedEntry);
            Assert.AreEqual(MapEditorController.Tool.Place, c.CurrentTool);

            var pos = new Vector2(20f, 20f);
            var placed = c.PlaceStamp(pos);
            Assert.IsTrue(placed.IsValid);
            Assert.IsTrue(c.Model.Unsaved);
            Assert.IsTrue(c.Map.IsCustom(TerrainMap.ChunkOf(pos)));

            c.SetTool(MapEditorController.Tool.Select);
            c.Model.TryGet(placed, out var p);
            Assert.IsTrue(c.SelectAt(MapEditModel.VisualCenter(p)));
            Assert.IsTrue(c.Selected.IsValid);
            float rotation = p.rotation;
            c.RotateSelected(15f);
            c.Model.TryGet(c.Selected, out var rotated);
            Assert.AreEqual(rotation + 15f, rotated.rotation, 1e-3f);
            c.ScaleSelected(2f);
            c.Model.TryGet(c.Selected, out var scaled);
            Assert.AreEqual(rotated.scale * 2f, scaled.scale, 1e-3f);
            c.FlipSelected();
            c.Model.TryGet(c.Selected, out var flipped);
            Assert.AreNotEqual(scaled.flipX, flipped.flipX);

            c.Undo();
            Assert.IsFalse(c.Selected.IsValid, "실행 취소하면 선택이 풀린다");

            Assert.IsTrue(c.Save());
            Assert.IsFalse(c.Model.Unsaved);
            Assert.IsNotNull(MapStore.Load(MapStore.FreeBattleId));
        }

        [Test]
        public void Controller_DeleteAndEraseTargets()
        {
            var c = MakeController(out _);
            c.SelectEntry(c.Palette.First(e => e.kind == MapItemKind.Prop));
            var r1 = c.PlaceStamp(new Vector2(50f, 50f));
            c.SetTool(MapEditorController.Tool.Select);
            c.Model.TryGet(r1, out var p);
            c.SelectAt(MapEditModel.VisualCenter(p));
            int before = c.Map.GetChunk(r1.chunk.x, r1.chunk.y).Count;
            c.DeleteSelected();
            Assert.IsFalse(c.Selected.IsValid);
            Assert.AreEqual(before - 1, c.Map.GetChunk(r1.chunk.x, r1.chunk.y).Count);

            c.SetTool(MapEditorController.Tool.Erase);
            c.CycleEraseTarget();
            Assert.AreEqual(MapEditorController.EraseTarget.Props, c.CurrentEraseTarget);
            c.ChangeBrush(100f);
            Assert.AreEqual(8f, c.BrushRadius);
            c.ChangeBrush(-100f);
            Assert.AreEqual(0.5f, c.BrushRadius);
        }

        [Test]
        public void Controller_Snap_And_Terrain_Cycle()
        {
            var c = MakeController(out _);
            Assert.AreEqual(new Vector2(1.3f, 2.7f), c.Snap(new Vector2(1.3f, 2.7f)), "맞춤이 꺼져 있으면 그대로");
            c.CycleSnap();   // 0.25
            c.CycleSnap();   // 0.5
            Assert.AreEqual(new Vector2(1.5f, 2.5f), c.Snap(new Vector2(1.3f, 2.7f)));

            Assert.IsNull(c.TerrainOverride);
            c.CycleTerrain();
            Assert.AreEqual(CastleTerrain.Plain, c.TerrainOverride);
            c.CycleTerrain();
            Assert.AreEqual(CastleTerrain.Steppe, c.TerrainOverride);
            Assert.AreEqual(CastleTerrain.Steppe, c.Map.Terrain, "바닥 지형이 바뀐다");
            Assert.IsTrue(c.Model.Unsaved);
            for (int i = 0; i < 5; i++) c.CycleTerrain();
            Assert.IsNull(c.TerrainOverride, "한 바퀴 돌면 성의 원래 지형으로");
        }

        [Test]
        public void Controller_ChangeTerrain_KeepsEditedChunks_AndSavesOverride()
        {
            var c = MakeController(out _);
            c.SelectEntry(c.Palette.First(e => e.kind == MapItemKind.Prop));
            c.PlaceStamp(new Vector2(20f, 20f));
            int custom = c.Map.CustomChunkCount;
            c.SetTerrain(CastleTerrain.Jungle);
            Assert.AreEqual(custom, c.Map.CustomChunkCount, "직접 고친 칸은 지형을 바꿔도 그대로");
            Assert.IsTrue(c.Save());
            Assert.AreEqual("Jungle", MapStore.Load(MapStore.FreeBattleId).terrain);
        }

        [Test]
        public void Controller_Save_WithNothingChanged_DeletesTheFile()
        {
            var c = MakeController(out _);
            MapStore.Save(new MapLayoutData { castleId = MapStore.FreeBattleId }, out _);
            Assert.IsTrue(c.Save());
            Assert.IsNull(MapStore.Load(MapStore.FreeBattleId), "고친 내용이 없으면 자동 생성 맵 그대로 둔다");
        }

        [Test]
        public void Controller_TestBattle_SavesAndStartsBattleInMapTestMode()
        {
            var c = MakeController(out var loaded);
            c.SelectEntry(c.Palette.First(e => e.kind == MapItemKind.Prop));
            c.PlaceStamp(new Vector2(20f, 20f));
            c.TestBattle();

            CollectionAssert.AreEqual(new[] { GameManager.BattleSceneName }, loaded);
            Assert.IsTrue(GameSession.MapTest);
            Assert.IsNull(GameSession.SortieCastle, "자유 전투 맵은 성 없이 시작한 판");
            Assert.IsNull(GameSession.SortieOrigin);
            Assert.IsNotNull(MapStore.Load(MapStore.FreeBattleId), "시험 전에 저장된다");

            GameSession.MapTest = true;
            c.ExitToTitle();
            Assert.IsFalse(GameSession.MapTest);
            Assert.AreEqual(GameManager.TitleSceneName, loaded.Last());
        }

        [Test]
        public void Controller_ResetAll_IsUndoable_AndReloadDropsUnsavedChanges()
        {
            var c = MakeController(out _);
            c.SelectEntry(c.Palette.First(e => e.kind == MapItemKind.Prop));
            c.PlaceStamp(new Vector2(2f, 2f));
            Assert.IsTrue(c.Map.HasCustom);
            c.ResetAll();
            Assert.IsFalse(c.Map.HasCustom);
            c.Undo();
            Assert.IsTrue(c.Map.HasCustom, "전부 되돌린 것도 실행 취소로 복구");
            c.ReloadFromDisk();
            Assert.IsFalse(c.Map.HasCustom, "저장본이 없으니 자동 생성");
            Assert.IsFalse(c.Model.Unsaved);
        }
    }
}
