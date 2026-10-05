using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Data;
using UnityEditor;
using UnityEngine;

namespace Samkuk.Tests
{
    /// <summary>내정용 성 46곳(Step 12-1) 데이터 검사: 빠진 항목, 비대칭 연결, 끊긴 지도를 잡아낸다.</summary>
    public class CastleTests
    {
        const string CatalogPath = "Assets/ScriptableObjects/CastleCatalog.asset";
        const int ExpectedCount = 46;

        CastleCatalog Load()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CastleCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "CastleCatalog 가 없습니다. 메뉴 Samkuk > Step 12-1 을 먼저 실행하세요.");
            return catalog;
        }

        [Test]
        public void Catalog_HasAllCastles_WithUniqueIdsAndNames()
        {
            var catalog = Load();
            Assert.AreEqual(ExpectedCount, catalog.castles.Count);

            var ids = new HashSet<string>();
            var names = new HashSet<string>();
            foreach (var c in catalog.castles)
            {
                Assert.IsNotNull(c, "카탈로그에 빈 칸이 있습니다");
                Assert.IsFalse(string.IsNullOrEmpty(c.id), "아이디가 비었습니다");
                Assert.IsFalse(string.IsNullOrEmpty(c.displayName), $"{c.id}: 이름이 비었습니다");
                Assert.IsFalse(string.IsNullOrEmpty(c.hanja), $"{c.id}: 한자 이름이 비었습니다");
                Assert.IsTrue(ids.Add(c.id), $"아이디 중복: {c.id}");
                Assert.IsTrue(names.Add(c.displayName), $"이름 중복: {c.displayName}");
                Assert.AreSame(c, catalog.Find(c.id));
            }
            Assert.IsNull(catalog.Find("NoSuchCastle"));
        }

        [Test]
        public void Castles_HaveBackground_Of16By9()
        {
            foreach (var c in Load().castles)
            {
                Assert.IsNotNull(c.background, $"{c.displayName}: 배경이 없습니다 (tools/castle_art/generate.ps1 후 Step 12-1)");
                var r = c.background.rect;
                Assert.AreEqual(16f / 9f, r.width / r.height, 0.01f, $"{c.displayName}: 배경 비율이 16:9 가 아닙니다 ({r.width}x{r.height})");
            }
        }

        [Test]
        public void Castles_HaveMapPosition_InsideMap_AndDistinct()
        {
            var seen = new List<Vector2>();
            foreach (var c in Load().castles)
            {
                Assert.That(c.mapPosition.x, Is.InRange(0f, 1f), $"{c.displayName}: x 가 지도 밖입니다");
                Assert.That(c.mapPosition.y, Is.InRange(0f, 1f), $"{c.displayName}: y 가 지도 밖입니다");
                foreach (var p in seen)
                    Assert.Greater(Vector2.Distance(p, c.mapPosition), 0.02f, $"{c.displayName}: 다른 성과 지도에서 겹칩니다");
                seen.Add(c.mapPosition);
            }
        }

        [Test]
        public void Neighbors_AreSymmetric_NotSelf_AndNotEmpty()
        {
            foreach (var c in Load().castles)
            {
                Assert.Greater(c.neighbors.Count, 0, $"{c.displayName}: 인접한 성이 없습니다");
                var distinct = new HashSet<CastleData>(c.neighbors);
                Assert.AreEqual(c.neighbors.Count, distinct.Count, $"{c.displayName}: 인접 성이 중복되었습니다");
                foreach (var n in c.neighbors)
                {
                    Assert.IsNotNull(n, $"{c.displayName}: 인접 성에 빈 칸이 있습니다");
                    Assert.AreNotSame(c, n, $"{c.displayName}: 자기 자신과 연결되어 있습니다");
                    Assert.IsTrue(n.IsAdjacent(c), $"{c.displayName} -> {n.displayName} 연결이 한쪽 방향뿐입니다");
                }
            }
        }

        [Test]
        public void Map_IsFullyConnected()
        {
            var catalog = Load();
            var seen = new HashSet<CastleData> { catalog.castles[0] };
            var queue = new Queue<CastleData>();
            queue.Enqueue(catalog.castles[0]);
            while (queue.Count > 0)
                foreach (var n in queue.Dequeue().neighbors)
                    if (seen.Add(n)) queue.Enqueue(n);

            Assert.AreEqual(catalog.castles.Count, seen.Count, "어느 성에서도 닿을 수 없는 성이 있습니다 (연결이 끊긴 지도)");
        }

        [Test]
        public void Capitals_AreLuoyangAndChangan()
        {
            var capitals = new List<string>();
            foreach (var c in Load().castles)
                if (c.size == CastleSize.Capital) capitals.Add(c.id);
            CollectionAssert.AreEquivalent(new[] { "Luoyang", "Changan" }, capitals);
        }
    }
}
