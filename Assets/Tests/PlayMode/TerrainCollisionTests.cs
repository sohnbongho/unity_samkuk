using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    /// <summary>지형 이동(Step 12-7): 나무/바위는 막고 미끄러지며, 강물/연못은 느려진다. 플레이어·적이 실제로 그렇게 움직이는지도 본다.</summary>
    public class TerrainCollisionTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        const float Body = 0.3f;

        readonly List<Object> toDestroy = new List<Object>();
        readonly List<GameObject> gos = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            MapStore.Disabled = true;   // 사용자가 맵 편집기로 고쳐 둔 실제 맵이 섞이지 않게
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
        }

        [TearDown]
        public void TearDown()
        {
            MapStore.Disabled = false;
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
            foreach (var g in gos) if (g != null) Object.Destroy(g);
            gos.Clear();
            foreach (var o in toDestroy) if (o != null) Object.Destroy(o);
            toDestroy.Clear();
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        Sprite MakeSprite(string name)
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.1f), 8f);
            sprite.name = name;
            toDestroy.Add(tex);
            toDestroy.Add(sprite);
            return sprite;
        }

        /// <summary>소품 둘(막는 나무, 통과하는 풀)과 강이 있는 평야 테마 하나짜리 카탈로그.</summary>
        TerrainThemeCatalog MakeCatalog(float riverSlow = 0.5f)
        {
            var c = ScriptableObject.CreateInstance<TerrainThemeCatalog>();
            var t = ScriptableObject.CreateInstance<TerrainTheme>();
            t.terrain = CastleTerrain.Plain;
            t.propsPerChunk = 0f;   // 자동 소품은 끄고(0) 직접 놓은 것만 본다
            t.groundTile = MakeSprite("Ground_Plain");
            t.props.Add(new TerrainProp { name = "Tree", sprite = MakeSprite("Prop_Plain_Tree"), weight = 1f, blockRadius = 0.4f });
            t.props.Add(new TerrainProp { name = "Grass", sprite = MakeSprite("Prop_Plain_Grass"), weight = 1f });
            t.riverWater = MakeSprite("River_Plain_Water");
            t.riverBank = MakeSprite("River_Plain_Bank");
            t.riverWidth = 3.5f;
            t.riverSlowFactor = riverSlow;
            c.themes.Add(t);
            c.pond.sprite = MakeSprite("Prop_Pond");
            c.pond.slowRadius = 1.4f;
            c.pond.slowFactor = 0.5f;
            c.banner.sprite = MakeSprite("Prop_Banner");
            toDestroy.Add(t);
            toDestroy.Add(c);
            return c;
        }

        CastleData MakeCastle(string id, bool water)
        {
            var c = ScriptableObject.CreateInstance<CastleData>();
            c.id = id; c.displayName = id; c.terrain = CastleTerrain.Plain; c.size = CastleSize.Small; c.hasWater = water;
            toDestroy.Add(c);
            return c;
        }

        TerrainMap MakeMap(bool water = false)
        {
            var catalog = MakeCatalog();
            TerrainThemeCatalog.Use(catalog);
            return TerrainMap.Create(MakeCastle("Test", water), catalog);
        }

        static TerrainProp Find(TerrainMap map, string name) => map.Theme.props.Find(p => p.name == name);

        /// <summary>칸을 직접 고친 칸으로 만들고(자동 생성 내용은 비움) 소품 하나를 놓는다.</summary>
        static void Put(TerrainMap map, TerrainProp prop, Vector2 pos, float scale = 1f, int order = TerrainMap.OrderPropBase)
        {
            var chunk = TerrainMap.ChunkOf(pos);
            var list = map.IsCustom(chunk) ? map.GetChunk(chunk.x, chunk.y) : new List<PropPlacement>();
            list.Add(new PropPlacement { prop = prop, position = pos, scale = scale, stretchY = 1f, order = order });
            map.SetCustomChunk(chunk, list);
        }

        // ───────────────────────── 순수 로직 ─────────────────────────

        [Test]
        public void BlockingProp_Overlaps_AndPushOutLeavesIt()
        {
            var map = MakeMap();
            Put(map, Find(map, "Tree"), new Vector2(5f, 5f), scale: 1.5f);   // 막는 반지름 0.4 x 1.5 = 0.6
            var col = new TerrainCollision(map);

            Assert.IsTrue(col.Overlaps(new Vector2(5.5f, 5f), Body), "나무 밑동(0.6) + 몸(0.3) 안");
            Assert.IsFalse(col.Overlaps(new Vector2(6.2f, 5f), Body), "밖");
            Assert.IsFalse(col.Overlaps(new Vector2(0f, 0f), Body));

            var moved = col.PushOut(new Vector2(5.2f, 5f), Body);
            Assert.IsFalse(col.Overlaps(moved, Body), "밀어낸 자리는 비어 있다");
            Assert.AreEqual(0.9f, Vector2.Distance(moved, new Vector2(5f, 5f)), 0.05f, "밑동 + 몸 반지름만큼 떨어진 자리");
            Assert.AreEqual(new Vector2(3f, 3f), col.PushOut(new Vector2(3f, 3f), Body), "겹치지 않으면 그대로");
        }

        [Test]
        public void NonBlockingProp_IsPassable()
        {
            var map = MakeMap();
            Put(map, Find(map, "Grass"), new Vector2(5f, 5f), scale: 3f);
            var col = new TerrainCollision(map);

            Assert.IsFalse(col.Overlaps(new Vector2(5f, 5f), Body), "풀은 지나간다");
            Assert.AreEqual(Vector2.left * 4f, col.Resolve(new Vector2(5.3f, 5f), Body, Vector2.left * 4f, 0.02f));
            Assert.AreEqual(1f, col.SpeedFactor(new Vector2(5f, 5f)));
        }

        [Test]
        public void Resolve_RemovesInwardComponent_KeepsTangent()
        {
            var map = MakeMap();
            Put(map, Find(map, "Tree"), new Vector2(5f, 5f));   // 반지름 0.4
            var col = new TerrainCollision(map);

            // 나무 왼쪽에 닿아 있고(거리 0.7 = 0.4 + 0.3) 오른쪽 위로 가려 한다: 오른쪽 성분만 사라진다
            Vector2 pos = new Vector2(5f - 0.7f, 5f);
            Vector2 v = col.Resolve(pos, Body, new Vector2(3f, 2f), 0.02f);
            Assert.AreEqual(0f, v.x, 0.01f, "나무 쪽(오른쪽) 성분은 지워진다");
            Assert.AreEqual(2f, v.y, 0.01f, "접선(위) 성분은 남는다");

            // 멀어지는 방향은 그대로
            Assert.AreEqual(new Vector2(-3f, 2f), col.Resolve(pos, Body, new Vector2(-3f, 2f), 0.02f));
            // 떨어져 있으면 그대로
            Assert.AreEqual(new Vector2(3f, 2f), col.Resolve(new Vector2(3f, 5f), Body, new Vector2(3f, 2f), 0.02f));
        }

        [Test]
        public void Resolve_HeadOn_PlayerStops_EnemyDeflects()
        {
            var map = MakeMap();
            Put(map, Find(map, "Tree"), new Vector2(5f, 5f));
            var col = new TerrainCollision(map);
            Vector2 pos = new Vector2(5f - 0.7f, 5f);

            var player = col.Resolve(pos, Body, Vector2.right * 4f, 0.02f, deflect: false);
            Assert.Less(player.magnitude, 0.01f, "플레이어는 정면으로 막히면 멈춘다");

            var up = col.Resolve(pos, Body, Vector2.right * 4f, 0.02f, deflect: true, side: 1);
            var down = col.Resolve(pos, Body, Vector2.right * 4f, 0.02f, deflect: true, side: -1);
            Assert.Greater(up.y, 1f, "적은 접선으로 돌아간다 (+1 = 왼쪽 접선 = 위)");
            Assert.Less(down.y, -1f, "-1 이면 반대쪽");
            Assert.AreEqual(0f, up.x, 0.01f);

            // 살짝 비스듬하면 가려던 쪽으로 돈다 (side 와 무관)
            var slant = col.Resolve(pos, Body, new Vector2(4f, -0.5f), 0.02f, deflect: true, side: 1);
            Assert.Less(slant.y, -0.4f, "아래로 가려 했으니 아래쪽 접선");
        }

        [Test]
        public void Resolve_PushesOutOfOverlap_Gradually()
        {
            var map = MakeMap();
            Put(map, Find(map, "Tree"), new Vector2(5f, 5f));
            var col = new TerrainCollision(map);

            Vector2 pos = new Vector2(5f - 0.5f, 5f);   // 0.2 만큼 파고듦
            var v = col.Resolve(pos, Body, Vector2.zero, 0.02f);
            Assert.Less(v.x, 0f, "바깥(왼쪽)으로 밀려난다");
            Assert.LessOrEqual(v.magnitude, 6.01f, "밀어내는 속도에는 상한이 있다 (튕기지 않게)");
        }

        [Test]
        public void River_SlowsOnWater_NotOnLand()
        {
            var map = MakeMap(water: true);
            Assert.IsTrue(map.HasRiver);
            var col = new TerrainCollision(map);

            // 강물 토막이 있는 자리 = 강 위
            bool found = FindWater(map, out var water);
            Assert.IsTrue(found, "시작 주변 칸에 강 토막이 있어야 한다");

            Assert.AreEqual(0.5f, col.SpeedFactor(water.position), 0.001f, "강물 위에서는 절반");
            Assert.AreEqual(1f, col.SpeedFactor(Vector2.zero), "시작 위치는 마른 땅");
            Assert.IsFalse(col.Overlaps(water.position, Body), "강은 막지 않는다 (건널 수 있다)");
            Assert.AreEqual(Vector2.right * 4f, col.Resolve(water.position, Body, Vector2.right * 4f, 0.02f), "속도 방향은 그대로 (배율은 호출하는 쪽이 곱한다)");
        }

        [Test]
        public void River_WithFactorOne_DoesNotSlow()
        {
            var catalog = MakeCatalog(riverSlow: 1f);
            TerrainThemeCatalog.Use(catalog);
            var map = TerrainMap.Create(MakeCastle("NoSlow", true), catalog);
            var col = new TerrainCollision(map);

            Assert.IsTrue(FindWater(map, out var water));
            Assert.AreEqual(1f, col.SpeedFactor(water.position), "배율 1 이면 강 위에서도 느려지지 않는다");
        }

        /// <summary>시작 주변 5x5 칸에서 강물 토막 하나를 찾는다 (강은 시작 위치에서 10~17유닛 떨어져 있어 반드시 이 안에 있다).</summary>
        static bool FindWater(TerrainMap map, out PropPlacement water)
        {
            for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                    foreach (var p in map.Layout(x, y))
                        if (TerrainMap.KindOf(p) == MapItemKind.Water) { water = p; return true; }
            water = default;
            return false;
        }

        [Test]
        public void Pond_Slows_InsideOnly()
        {
            var map = MakeMap();
            Put(map, map.Catalog.pond, new Vector2(-8f, 3f), scale: 1f, order: TerrainMap.OrderPond);   // 느려지는 반지름 1.4
            var col = new TerrainCollision(map);

            Assert.AreEqual(0.5f, col.SpeedFactor(new Vector2(-8f, 3f)), 0.001f);
            Assert.AreEqual(0.5f, col.SpeedFactor(new Vector2(-7f, 3f)), 0.001f);
            Assert.AreEqual(1f, col.SpeedFactor(new Vector2(-6f, 3f)), "가장자리 밖");
            Assert.IsFalse(col.Overlaps(new Vector2(-8f, 3f), Body), "연못은 막지 않는다");
        }

        [Test]
        public void Invalidate_RebuildsChangedChunk()
        {
            var map = MakeMap();
            var col = new TerrainCollision(map);
            Assert.IsFalse(col.Overlaps(new Vector2(5f, 5f), Body), "처음엔 비어 있다");

            Put(map, Find(map, "Tree"), new Vector2(5f, 5f));
            Assert.IsFalse(col.Overlaps(new Vector2(5f, 5f), Body), "다시 만들기 전에는 예전 내용");
            col.Invalidate(TerrainMap.ChunkOf(new Vector2(5f, 5f)));
            Assert.IsTrue(col.Overlaps(new Vector2(5f, 5f), Body), "고친 칸을 다시 읽었다");

            map.RemoveCustomChunk(TerrainMap.ChunkOf(new Vector2(5f, 5f)));
            col.Invalidate(TerrainMap.ChunkOf(new Vector2(5f, 5f)));
            Assert.IsFalse(col.Overlaps(new Vector2(5f, 5f), Body), "되돌린 칸은 다시 비어 있다");
        }

        [Test]
        public void Prop_NearChunkEdge_IsSeenFromNeighborChunk()
        {
            var map = MakeMap();
            Put(map, Find(map, "Tree"), new Vector2(11.9f, 6f), scale: 1.5f);   // 칸 (0,0) 오른쪽 경계 바로 안
            var col = new TerrainCollision(map);

            Assert.IsTrue(col.Overlaps(new Vector2(12.3f, 6f), Body), "이웃 칸 (1,0) 에서 질의해도 보인다");
        }

        [Test]
        public void AutoGeneratedChunk_UsesPropBlockRadius()
        {
            var catalog = MakeCatalog();
            catalog.themes[0].propsPerChunk = 12f;
            TerrainThemeCatalog.Use(catalog);
            var map = TerrainMap.Create(MakeCastle("Auto", false), catalog);
            var col = new TerrainCollision(map);

            var circles = new List<TerrainCollision.Circle>();
            col.CollectCircles(new Vector2Int(2, 2), circles);
            int trees = 0;
            foreach (var p in map.GetChunk(2, 2)) if (p.prop != null && p.prop.name == "Tree") trees++;
            Assert.Greater(trees, 0, "자동 생성 칸에 나무가 있어야 한다");
            Assert.AreEqual(trees, circles.Count, "나무 하나 = 막는 원 하나, 풀은 없음");
            foreach (var c in circles) Assert.IsTrue(c.Blocks);
        }

        [Test]
        public void PropKinds_TreesAndRocksBlock_GrassPasses()
        {
            Assert.Greater(TerrainPropKinds.BlockRadius("tree"), 0f);
            Assert.Greater(TerrainPropKinds.BlockRadius("boulder"), TerrainPropKinds.BlockRadius("rock"), "큰 바위가 더 넓게 막는다");
            Assert.AreEqual(0f, TerrainPropKinds.BlockRadius("tuft"));
            Assert.AreEqual(0f, TerrainPropKinds.BlockRadius("bush"));
            Assert.AreEqual(0f, TerrainPropKinds.BlockRadius("reed"));

            var pond = new TerrainProp();
            TerrainPropKinds.ApplyDefaults(pond, "pond");
            Assert.IsTrue(pond.Slows && !pond.Blocks, "연못은 느려지되 막지 않는다");
            var tree = new TerrainProp();
            TerrainPropKinds.ApplyDefaults(tree, "tree");
            Assert.IsTrue(tree.Blocks && !tree.Slows);
        }

        // ───────────────────────── 실제 이동 ─────────────────────────

        [UnityTest]
        public IEnumerator Enemy_GoesAroundTree_InsteadOfStandingInIt()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            Assert.IsNotNull(prefab, "Enemy 프리팹이 없습니다. Samkuk > Step 3 를 먼저 실행하세요.");

            var map = MakeMap();
            Put(map, Find(map, "Tree"), new Vector2(4f, 0f), scale: 1.5f);   // 플레이어(0,0)와 적(8,0) 사이 한가운데, 반지름 0.6
            var col = new TerrainCollision(map);
            TerrainCollision.Active = col;

            var playerGo = new GameObject("TestPlayer");
            gos.Add(playerGo);
            var root = new GameObject("TestSystems");
            gos.Add(root);
            root.SetActive(false);
            var manager = root.AddComponent<EnemyManager>();
            var spawner = root.AddComponent<EnemySpawner>();
            manager.Target = playerGo.transform;
            spawner.Manager = manager;
            spawner.EnemyPrefab = prefab;
            spawner.autoSpawn = false;
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.moveSpeed = 3f; data.colliderRadius = 0.3f; data.scale = 1f;
            toDestroy.Add(data);
            root.SetActive(true);
            yield return null;

            var e = spawner.SpawnAt(data, new Vector2(8f, 0f));
            float minTreeDist = float.MaxValue;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                yield return null;
                minTreeDist = Mathf.Min(minTreeDist, Vector2.Distance(e.Position, new Vector2(4f, 0f)));
            }

            Assert.Greater(minTreeDist, 0.6f + 0.3f - 0.15f, "나무 밑동 안으로 들어가지 않는다 (약간의 파고듦만 허용)");
            Assert.Less(Vector2.Distance(e.Position, Vector2.zero), 1.2f, "나무를 돌아 플레이어 곁까지 왔다");
        }

        [UnityTest]
        public IEnumerator Enemy_SpawnRing_AvoidsTrees()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            Assert.IsNotNull(prefab);

            var catalog = MakeCatalog();
            catalog.themes[0].propsPerChunk = 30f;   // 나무가 많은 맵
            TerrainThemeCatalog.Use(catalog);
            var map = TerrainMap.Create(MakeCastle("Dense", false), catalog);
            TerrainCollision.Active = new TerrainCollision(map);

            var playerGo = new GameObject("TestPlayer");
            gos.Add(playerGo);
            var camGo = new GameObject("TestCam", typeof(Camera));
            gos.Add(camGo);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 6f; cam.aspect = 16f / 9f;
            var root = new GameObject("TestSystems");
            gos.Add(root);
            root.SetActive(false);
            var manager = root.AddComponent<EnemyManager>();
            var spawner = root.AddComponent<EnemySpawner>();
            manager.Target = playerGo.transform;
            spawner.Manager = manager;
            spawner.Cam = cam;
            spawner.EnemyPrefab = prefab;
            spawner.autoSpawn = false;
            root.SetActive(true);
            yield return null;

            for (int i = 0; i < 200; i++)
            {
                var pos = spawner.RandomRingPosition();
                Assert.IsFalse(TerrainCollision.Active.Overlaps(pos, 0.3f), $"나무 속에 스폰 자리를 고르지 않는다: {pos}");
            }
        }

        [UnityTest]
        public IEnumerator Player_IsBlockedByTree_AndSlowedByRiver()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, "Player 프리팹이 없습니다. Samkuk > Step 2 를 먼저 실행하세요.");

            var map = MakeMap();
            Put(map, Find(map, "Tree"), new Vector2(2f, 0f));   // 오른쪽 2유닛 앞 나무 (반지름 0.4)
            TerrainCollision.Active = new TerrainCollision(map);

            var player = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            gos.Add(player);
            var pc = player.GetComponent<PlayerController>();
            pc.enabled = false;   // 키 입력 대신 속도를 직접 준다
            var rb = player.GetComponent<Rigidbody2D>();
            yield return null;

            // 1초 동안 오른쪽으로 밀어도 나무 앞에서 멈춘다 (PlayerController 의 FixedUpdate 대신 같은 규칙을 직접 적용)
            for (float t = 0f; t < 1f; t += Time.fixedDeltaTime)
            {
                rb.linearVelocity = TerrainCollision.Active.Resolve(rb.position, PlayerController.BodyRadius, Vector2.right * 4f, Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }
            Assert.Less(rb.position.x, 2f - 0.4f - PlayerController.BodyRadius + 0.1f, "나무 밑동 앞에서 멈춘다");
            Assert.Greater(rb.position.x, 1f, "나무 앞까지는 간다");
            Assert.AreEqual(1f, pc.TerrainSpeedFactor, "마른 땅");
        }

        [UnityTest]
        public IEnumerator PlayerController_SlowsInWater()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab);

            var map = MakeMap();
            Put(map, map.Catalog.pond, new Vector2(0f, 0f), scale: 2f, order: TerrainMap.OrderPond);   // 시작 위치가 연못 한가운데 (반지름 2.8)
            TerrainCollision.Active = new TerrainCollision(map);

            var player = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            gos.Add(player);
            var pc = player.GetComponent<PlayerController>();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(0.5f, pc.TerrainSpeedFactor, 0.001f, "연못 안에서는 속도 배율 절반");
        }

        [UnityTest]
        public IEnumerator Background_PublishesActiveCollision_AndClearsOnReset()
        {
            var catalog = MakeCatalog();
            TerrainThemeCatalog.Use(catalog);
            GameSession.SortieCastle = MakeCastle("Bg", true);

            var camGo = new GameObject("TestCam", typeof(Camera));
            gos.Add(camGo);
            var bgGo = new GameObject("TestBackground", typeof(SpriteRenderer));
            gos.Add(bgGo);
            bgGo.GetComponent<SpriteRenderer>().sprite = MakeSprite("Default");
            var bg = bgGo.AddComponent<InfiniteBackground>();
            yield return null;

            Assert.IsNotNull(bg.Collision, "맵이 적용되면 지형 충돌도 만든다");
            Assert.AreSame(bg.Collision, TerrainCollision.Active, "이동하는 것들이 찾는 현재 지형 충돌");
            Assert.AreSame(bg.Props.Map, bg.Collision.Map, "보이는 소품과 같은 맵");

            bg.ResetToDefault();
            Assert.IsNull(bg.Collision);
            Assert.IsNull(TerrainCollision.Active, "맵을 내리면 지형 효과도 없다 (예전처럼 어디든 걷는다)");
            GameSession.SortieCastle = null;
        }
    }
}
