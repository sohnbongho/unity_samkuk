using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Data;
using Samkuk.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    public class EnemyTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";

        GameObject root;
        GameObject playerGo;
        GameObject camGo;
        EnemyManager manager;
        EnemySpawner spawner;
        Camera cam;
        EnemyData data;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            Assert.IsNotNull(prefab, "Enemy 프리팹이 없습니다. Samkuk > Step 3 를 먼저 실행하세요.");

            playerGo = new GameObject("TestPlayer");
            playerGo.transform.position = Vector3.zero;

            camGo = new GameObject("TestCam", typeof(Camera));
            cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.aspect = 16f / 9f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            root = new GameObject("TestSystems");
            root.SetActive(false); // 필드 설정 전에 Awake/OnEnable이 돌지 않도록
            manager = root.AddComponent<EnemyManager>();
            spawner = root.AddComponent<EnemySpawner>();
            manager.Target = playerGo.transform;
            spawner.Manager = manager;
            spawner.Cam = cam;
            spawner.EnemyPrefab = prefab;
            spawner.autoSpawn = false;

            data = ScriptableObject.CreateInstance<EnemyData>();
            data.moveSpeed = 2f;
            data.colliderRadius = 0.3f;
            data.scale = 1f;
            spawner.SpawnTable.Add(new EnemySpawner.Entry { data = data, weight = 1f });

            root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.Destroy(root);
            if (playerGo != null) Object.Destroy(playerGo);
            if (camGo != null) Object.Destroy(camGo);
            if (data != null) Object.Destroy(data);
        }

        [UnityTest]
        public IEnumerator Spawn_PlacesEnemiesOutsideCameraView()
        {
            yield return null;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            for (int i = 0; i < 100; i++)
            {
                var e = spawner.Spawn();
                Assert.IsNotNull(e);
                Vector2 p = e.Position;
                bool outside = Mathf.Abs(p.x) > halfW || Mathf.Abs(p.y) > halfH;
                Assert.IsTrue(outside, $"화면 안에 스폰됨: {p}");
            }
            Assert.AreEqual(100, manager.Count);
        }

        [UnityTest]
        public IEnumerator Enemy_ChasesPlayer()
        {
            yield return null;
            var e = spawner.SpawnAt(data, new Vector2(8f, 0f));
            float before = Vector2.Distance(e.Position, Vector2.zero);

            yield return new WaitForSeconds(1f);

            float after = Vector2.Distance(e.Position, Vector2.zero);
            Assert.Less(after, before - 1.2f, $"1초 동안 속도(2)만큼 가까워져야 함 (before={before}, after={after})");
        }

        [UnityTest]
        public IEnumerator Enemy_StopsNearPlayer_NotOnTopOfIt()
        {
            yield return null;
            var e = spawner.SpawnAt(data, new Vector2(2f, 0f));

            yield return new WaitForSeconds(2f);

            float dist = Vector2.Distance(e.Position, Vector2.zero);
            Assert.Less(dist, 1.0f, "플레이어 근처까지 접근");
            Assert.Greater(dist, 0.3f, "플레이어 위에 겹치지 않음");
        }

        [UnityTest]
        public IEnumerator Enemies_SeparateWhenStacked()
        {
            yield return null;
            var list = new List<Enemy>();
            for (int i = 0; i < 30; i++)
                list.Add(spawner.SpawnAt(data, new Vector2(6f, 0f))); // 전부 같은 위치

            yield return new WaitForSeconds(1f);

            float minDist = float.MaxValue;
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                    minDist = Mathf.Min(minDist, Vector2.Distance(list[i].Position, list[j].Position));

            Assert.Greater(minDist, 0.15f, $"겹친 적들이 분리되어야 함 (min={minDist})");
        }

        [UnityTest]
        public IEnumerator Pool_ReusesInstances()
        {
            yield return null;
            var spawned = new List<Enemy>();
            for (int i = 0; i < 100; i++) spawned.Add(spawner.Spawn());
            Assert.AreEqual(100, spawner.PooledTotal);

            foreach (var e in spawned) e.Despawn();
            Assert.AreEqual(0, manager.Count, "반환하면 매니저에서 제거");
            Assert.AreEqual(100, spawner.PooledTotal);

            for (int i = 0; i < 100; i++) spawner.Spawn();
            Assert.AreEqual(100, spawner.PooledTotal, "재스폰 시 새 인스턴스를 만들지 않음");
            Assert.AreEqual(100, manager.Count);
        }

        [UnityTest]
        public IEnumerator Despawn_Twice_IsSafe()
        {
            yield return null;
            var e = spawner.Spawn();
            e.Despawn();
            e.Despawn();
            Assert.AreEqual(0, manager.Count);
        }

        [UnityTest]
        public IEnumerator FarEnemy_IsRelocatedToRing()
        {
            yield return null;
            var e = spawner.SpawnAt(data, new Vector2(100f, 0f));

            yield return new WaitForSeconds(0.3f);

            Assert.Less(Vector2.Distance(e.Position, Vector2.zero), 25f, "너무 멀어진 적은 화면 밖 원으로 재배치");
            Assert.AreEqual(1, manager.Count, "제거되지 않고 재배치");
        }

        [UnityTest]
        public IEnumerator AutoSpawn_RespectsMaxAlive()
        {
            spawner.autoSpawn = true;
            spawner.SpawnPerSecond = 200f;
            spawner.MaxAlive = 50;

            yield return new WaitForSeconds(1f);

            Assert.AreEqual(50, manager.Count);
        }

        [UnityTest]
        public IEnumerator Stress_600Enemies_StaysResponsive()
        {
            yield return null;
            for (int i = 0; i < 600; i++) spawner.Spawn();

            yield return new WaitForSeconds(0.5f); // 워밍업
            float elapsed = 0f;
            int frames = 0;
            while (elapsed < 2f)
            {
                elapsed += Time.unscaledDeltaTime;
                frames++;
                yield return null;
            }

            float avgMs = elapsed / frames * 1000f;
            Debug.Log($"[Samkuk] 600 enemies: avg frame {avgMs:0.0} ms ({1000f / avgMs:0} fps, editor)");
            Assert.AreEqual(600, manager.Count);
            Assert.Less(avgMs, 100f, "에디터 환경에서도 극단적으로 느리지 않아야 함");
        }
    }
}
