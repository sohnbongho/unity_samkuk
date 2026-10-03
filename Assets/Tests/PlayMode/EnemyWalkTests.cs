using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    /// <summary>적 걷기 애니메이션: 플레이어를 바라보며 걷고, 멈추면 서 있는 자세, 시트가 없으면 예전 동작.</summary>
    public class EnemyWalkTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        const string EnemyDir = "Assets/ScriptableObjects/Enemies";

        GameObject root;
        GameObject playerGo;
        EnemyManager manager;
        EnemySpawner spawner;
        Texture2D sheet;
        EnemyData walker;   // 걷기 시트가 있는 적
        EnemyData plain;    // 시트가 없는 적
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            Assert.IsNotNull(prefab, "Enemy 프리팹이 없습니다. Samkuk > Step 3 를 먼저 실행하세요.");

            playerGo = new GameObject("TestPlayer");
            var camGo = new GameObject("TestCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.aspect = 16f / 9f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            toDestroy.Add(camGo);

            root = new GameObject("TestSystems");
            root.SetActive(false);
            manager = root.AddComponent<EnemyManager>();
            spawner = root.AddComponent<EnemySpawner>();
            manager.Target = playerGo.transform;
            spawner.Manager = manager;
            spawner.Cam = cam;
            spawner.EnemyPrefab = prefab;
            spawner.autoSpawn = false;
            root.SetActive(true);

            sheet = new Texture2D(8, 8) { name = "EnemyTestSheet" };
            toDestroy.Add(sheet);

            walker = MakeData("걷는 적", sheet, new Color(1f, 0.3f, 0.3f));
            plain = MakeData("단색 적", null, new Color(0.2f, 0.9f, 0.2f));
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.Destroy(root);
            if (playerGo != null) Object.Destroy(playerGo);
            foreach (var o in toDestroy) if (o != null) Object.Destroy(o);
            toDestroy.Clear();
        }

        EnemyData MakeData(string name, Texture2D walkSheet, Color tint)
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.displayName = name;
            d.moveSpeed = 2f;
            d.colliderRadius = 0.3f;
            d.scale = 1f;
            d.tint = tint;
            d.walkSheet = walkSheet;
            toDestroy.Add(d);
            return d;
        }

        [UnityTest]
        public IEnumerator Spawn_WithSheet_ShowsFrontStandingFrame_InArtColor()
        {
            yield return null;
            var e = spawner.SpawnAt(walker, new Vector2(8f, 0f));
            var sr = e.GetComponent<SpriteRenderer>();

            Assert.IsTrue(e.HasWalkSheet);
            Assert.AreSame(HeroSpriteSet.Get(sheet, walker.walkPixelsPerUnit).Get(FacingDir.Down, 0), sr.sprite);
            Assert.AreEqual(Color.white, sr.color, "그림 색 그대로 (tint 를 곱하지 않음)");
            Assert.IsFalse(sr.flipX);
        }

        [UnityTest]
        public IEnumerator Enemy_FacesThePlayer_FromEverySide()
        {
            yield return null;
            var cases = new[]
            {
                (new Vector2(9f, 0f), FacingDir.Left),    // 오른쪽에서 오는 적은 왼쪽(플레이어)을 본다
                (new Vector2(-9f, 0f), FacingDir.Right),
                (new Vector2(0f, 9f), FacingDir.Down),    // 위에서 오는 적은 아래를 본다
                (new Vector2(0f, -9f), FacingDir.Up),
            };

            foreach (var (pos, expected) in cases)
            {
                var e = spawner.SpawnAt(walker, pos);
                yield return new WaitForSeconds(0.2f);
                Assert.AreEqual(expected, e.Facing, $"{pos} 에서 오는 적의 방향");
                Assert.IsFalse(e.GetComponent<SpriteRenderer>().flipX, "걷기 시트를 쓰면 좌우 반전은 하지 않음");
                e.Despawn();
            }
        }

        [UnityTest]
        public IEnumerator Enemy_WalkFramesCycle_WhileChasing()
        {
            yield return null;
            var e = spawner.SpawnAt(walker, new Vector2(9f, 0f));
            var set = HeroSpriteSet.Get(sheet, walker.walkPixelsPerUnit);

            var frames = new HashSet<int>();
            float until = Time.time + 0.9f;
            while (Time.time < until)
            {
                yield return null;
                frames.Add(e.WalkFrame);
                Assert.AreSame(set.Get(e.Facing, e.WalkFrame), e.GetComponent<SpriteRenderer>().sprite);
            }
            Assert.GreaterOrEqual(frames.Count, 3, "쫓아가는 동안 프레임이 돌아야 함: " + string.Join(",", frames));
        }

        [UnityTest]
        public IEnumerator Enemy_StandsStill_WhenItReachesThePlayer()
        {
            yield return null;
            var e = spawner.SpawnAt(walker, new Vector2(2f, 0f));
            yield return new WaitForSeconds(2.5f); // 플레이어 앞에서 멈출 때까지

            Assert.AreEqual(0, e.WalkFrame, "멈춰 있으면 서 있는 자세(0번 프레임)");
            Assert.AreEqual(FacingDir.Left, e.Facing, "멈춰도 플레이어를 바라본다");
        }

        [UnityTest]
        public IEnumerator Enemy_WithoutSheet_KeepsOldTintAndFlip()
        {
            yield return null;
            var e = spawner.SpawnAt(plain, new Vector2(9f, 0f));
            var sr = e.GetComponent<SpriteRenderer>();
            yield return new WaitForSeconds(0.3f);

            Assert.IsFalse(e.HasWalkSheet);
            Assert.AreEqual(plain.tint, sr.color, "시트가 없으면 예전처럼 tint 색");
            Assert.IsTrue(sr.flipX, "오른쪽에서 왼쪽으로 움직이면 예전처럼 반전");
        }

        [UnityTest]
        public IEnumerator Enemy_Stun_RestoresArtColor()
        {
            yield return null;
            var e = spawner.SpawnAt(walker, new Vector2(9f, 0f));
            var sr = e.GetComponent<SpriteRenderer>();

            e.Stun(0.2f);
            Assert.AreNotEqual(Color.white, sr.color, "기절 중에는 푸른 기운");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(Color.white, sr.color, "기절이 끝나면 그림 색으로 복원");
        }

        [Test]
        public void TickAnimation_TurnsToFaceDirection_AndAdvancesFramesOnlyWhenMoving()
        {
            var e = spawner.SpawnAt(walker, new Vector2(30f, 30f));

            e.TickAnimation(Vector2.up, false, 0.1f);
            Assert.AreEqual(FacingDir.Up, e.Facing);
            Assert.AreEqual(0, e.WalkFrame, "움직이지 않으면 0번 프레임");

            var seen = new HashSet<int>();
            for (int i = 0; i < 40; i++)
            {
                e.TickAnimation(Vector2.right, true, 0.05f);
                seen.Add(e.WalkFrame);
            }
            Assert.AreEqual(FacingDir.Right, e.Facing);
            Assert.AreEqual(4, seen.Count, "한 바퀴 돌면 4프레임을 모두 거침");
        }

        [Test]
        public void EnemyAssets_HaveWalkSheets_WithFourByFourCells()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyData", new[] { EnemyDir }))
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(enemy.walkSheet, $"{enemy.displayName}: 걷기 시트가 연결되지 않았습니다. Samkuk > Step 10-7 을 실행하세요.");
                Assert.AreEqual(enemy.walkSheet.width, enemy.walkSheet.height, $"{enemy.displayName}: 정사각형(4x4칸) 시트여야 함");
                Assert.AreEqual(0, enemy.walkSheet.width % HeroSpriteSet.Columns, $"{enemy.displayName}: 칸 크기가 정수가 아님");
                Assert.Greater(enemy.walkPixelsPerUnit, 0f);
                count++;
            }
            Assert.AreEqual(10, count, "적 종류 10종");
        }
    }
}
