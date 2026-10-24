using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.World;
using UnityEngine;

namespace Samkuk.Tests
{
    /// <summary>흙길(Step 16-3): 성마다 정해진 굽은 길, 시작 가까이 지남, 소품·얼룩·언덕이 길을 비킴, 띠 메시, 임시 그림.</summary>
    public class TerrainPathTests
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

        Sprite MakeSprite()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.1f), 8f);
            toDestroy.Add(tex); toDestroy.Add(sprite);
            return sprite;
        }

        TerrainThemeCatalog MakeCatalog()
        {
            var c = ScriptableObject.CreateInstance<TerrainThemeCatalog>();
            foreach (CastleTerrain t in System.Enum.GetValues(typeof(CastleTerrain)))
            {
                var theme = ScriptableObject.CreateInstance<TerrainTheme>();
                theme.terrain = t;
                theme.propsPerChunk = 9f;
                theme.groundTile = MakeSprite();
                theme.props.Add(new TerrainProp { name = "Tree", sprite = MakeSprite(), weight = 1f, standing = true, blockRadius = 0.3f });
                theme.props.Add(new TerrainProp { name = "Bush", sprite = MakeSprite(), weight = 1f });
                theme.riverWater = MakeSprite();
                theme.riverBank = MakeSprite();
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
            c.id = id; c.displayName = id; c.terrain = terrain; c.hasWater = water; c.size = CastleSize.Medium;
            toDestroy.Add(c);
            return c;
        }

        [Test]
        public void Path_IsDeterministic_PassesNearTheStart_AndDiffersBetweenCastles()
        {
            var catalog = MakeCatalog();
            var a = TerrainMap.Create(MakeCastle("Xuchang", CastleTerrain.Plain), catalog);
            var b = TerrainMap.Create(MakeCastle("Xuchang", CastleTerrain.Plain), catalog);
            var c = TerrainMap.Create(MakeCastle("Chengdu", CastleTerrain.Plain), catalog);
            Assert.IsTrue(a.HasPath);
            Assert.That(a.PathWidth, Is.InRange(1.8f, 2.6f));
            for (int i = 0; i < 20; i++)
            {
                var p = new Vector2(i * 1.7f - 10f, i * 0.9f - 5f);
                Assert.AreEqual(a.PathDistance(p), b.PathDistance(p), 1e-5f, "같은 성은 같은 길");
            }
            Assert.Greater(Mathf.Abs(a.PathDistance(new Vector2(3f, 7f)) - c.PathDistance(new Vector2(3f, 7f))), 1e-3f, "성마다 다른 길");
            float d0 = a.PathDistance(Vector2.zero);
            Assert.That(d0, Is.InRange(2f, 12f), "길이 시작 위치 가까이를 지난다 (시작점 위는 아니고 첫 화면 안)");
            Assert.Greater(d0, a.PathWidth * 0.5f, "시작점이 길 한가운데는 아니다");
        }

        [Test]
        public void Path_PointsLieOnTheCenterLine_AndTangentFollowsIt()
        {
            var map = TerrainMap.Create(MakeCastle("Luoyang", CastleTerrain.Steppe), MakeCatalog());
            for (int i = -5; i <= 5; i++)
            {
                float u = i * 3.3f;
                var p = map.PathPoint(u, 0, out var tangent);
                Assert.AreEqual(0f, map.PathDistance(p), 0.05f, "중심선 위의 점은 거리 0");
                Assert.AreEqual(1f, tangent.magnitude, 1e-4f);
                var ahead = map.PathPoint(u + 0.5f, 0, out _);
                Assert.Greater(Vector2.Dot((ahead - p).normalized, tangent), 0.95f, "진행 방향은 다음 점 쪽");
            }
        }

        [Test]
        public void Path_PropsPatchesAndHills_KeepOffThePath()
        {
            var catalog = MakeCatalog();
            foreach (var map in new[]
            {
                TerrainMap.Create(MakeCastle("Hanzhong", CastleTerrain.Mountain), catalog),
                TerrainMap.Create(MakeCastle("Jianye", CastleTerrain.River, water: true), catalog),
            })
            {
                for (int y = -3; y <= 3; y++)
                    for (int x = -3; x <= 3; x++)
                    {
                        foreach (var p in map.Layout(x, y).Where(p => TerrainMap.KindOf(p) == MapItemKind.Prop || TerrainMap.KindOf(p) == MapItemKind.Pond))
                            Assert.GreaterOrEqual(map.PathDistance(p.position), map.PathClearance - 1e-4f, $"{p.prop.name} 이(가) 길 위에 있다");
                        foreach (var p in map.LayoutPatches(x, y))
                            Assert.GreaterOrEqual(map.PathDistance(p.position), map.PathClearance, "얼룩이 길 위에 있다");
                        var h = map.GetHeights(new Vector2Int(x, y));
                        for (int j = 0; j < HeightChunk.Cells; j++)
                            for (int i = 0; i < HeightChunk.Cells; i++)
                            {
                                if (h.Level(i, j) == 0) continue;
                                var center = TerrainHeight.CellOrigin(new Vector2Int(x, y), i, j) + Vector2.one * (TerrainHeight.CellSize * 0.5f);
                                Assert.GreaterOrEqual(map.PathDistance(center), map.PathClearance, "길 위에 언덕이 있다");
                            }
                    }
            }
        }

        [Test]
        public void Path_StarterHill_IsOffThePath()
        {
            var catalog = MakeCatalog();
            foreach (var id in new[] { "Wan", "Jianye", "Chengdu", "Luoyang", "FreeBattle" })
            {
                var map = TerrainMap.Create(MakeCastle(id, CastleTerrain.Plain, water: id == "Jianye"), catalog);
                Assert.Greater(map.PathDistance(map.StarterRampCenter), map.PathWidth * 0.5f, $"{id}: 시작 언덕이 길 위에 있다");
            }
        }

        [Test]
        public void Mesh_BuildsAStrip_WhereThePathCrosses_AndNothingElsewhere()
        {
            var map = TerrainMap.Create(MakeCastle("Wan", CastleTerrain.Plain), MakeCatalog());
            var b = new CliffMeshBuilder.Buffers();

            // 길 위의 한 점을 품은 칸에는 띠가 있다
            var on = map.PathPoint(0f, 0, out _);
            var chunk = TerrainMap.ChunkOf(on);
            PathMeshBuilder.Build(map, chunk, b);
            Assert.Greater(b.TopQuads, 3, "칸을 지나는 길은 여러 조각");
            Assert.AreEqual(0, b.WallQuads);
            foreach (var v in b.vertices)
            {
                Assert.AreEqual(PathMeshBuilder.Z, v.z, 1e-5f, "바닥 바로 위");
                Assert.LessOrEqual(map.PathDistance(new Vector2(v.x, v.y)), map.PathWidth * 0.5f + 0.3f, "띠는 길 폭 안에");
            }

            // 길에서 멀리 떨어진 칸(길 간격의 절반 거리)에는 아무것도 없다
            var far = on + new Vector2(-Mathf.Sin(0f), 0f);   // 자리만 잡고 아래에서 실제로 먼 칸을 찾는다
            bool found = false;
            for (int dy = -4; dy <= 4 && !found; dy++)
                for (int dx = -4; dx <= 4 && !found; dx++)
                {
                    var c = new Vector2Int(chunk.x + dx, chunk.y + dy);
                    var center = (c + Vector2.one * 0.5f) * TerrainMap.ChunkSize;
                    if (map.PathDistance(center) < TerrainMap.ChunkSize) continue;
                    PathMeshBuilder.Build(map, c, b);
                    Assert.IsTrue(b.IsEmpty, $"길에서 먼 칸 {c} 에 띠가 있다");
                    found = true;
                }
            Assert.IsTrue(found, "길에서 먼 칸을 하나는 찾는다");
        }

        [Test]
        public void Paths_MakeMeshesOnlyUnderTilt()
        {
            if (!TiltView.ShaderAvailable) Assert.Ignore($"셰이더 {TiltView.ShaderName} 없음");
            var map = TerrainMap.Create(MakeCastle("Wan", CastleTerrain.Plain), MakeCatalog());
            var follow = new GameObject("Follow");
            toDestroy.Add(follow);
            var go = new GameObject("Paths");
            toDestroy.Add(go);
            var paths = go.AddComponent<TerrainPaths>();
            paths.Initialize(map, follow.transform);
            Assert.IsFalse(paths.Visible);
            TiltView.SetReady(true);
            Assert.IsTrue(paths.Visible);
            Assert.Greater(go.GetComponentsInChildren<MeshFilter>(false).Length, 0, "시작 가까이 길이 지나므로 띠가 있다");
            TiltView.SetReady(false);
            Assert.AreEqual(0, go.GetComponentsInChildren<MeshFilter>(false).Length);
        }

        [Test]
        public void PathArt_IsRepeatingStrip_WithJaggedTransparentEdges()
        {
            var tex = PathArt.Texture(CastleTerrain.Plain);
            Assert.AreSame(tex, PathArt.Texture(CastleTerrain.Plain));
            Assert.AreEqual(PathArt.Width, tex.width);
            Assert.AreEqual(PathArt.Height, tex.height);
            Assert.AreEqual(TextureWrapMode.Repeat, tex.wrapMode);
            var px = tex.GetPixels32();
            int clearBottom = 0, clearTop = 0, solidMiddle = 0;
            for (int x = 0; x < tex.width; x++)
            {
                if (px[x].a == 0) clearBottom++;
                if (px[(tex.height - 1) * tex.width + x].a == 0) clearTop++;
                if (px[(tex.height / 2) * tex.width + x].a == 255) solidMiddle++;
            }
            Assert.AreEqual(tex.width, clearBottom, "맨 아래 줄은 비어 있다");
            Assert.AreEqual(tex.width, clearTop, "맨 위 줄은 비어 있다");
            Assert.AreEqual(tex.width, solidMiddle, "가운데는 꽉 차 있다");
            int clearRow2 = 0;
            for (int x = 0; x < tex.width; x++) if (px[2 * tex.width + x].a == 0) clearRow2++;
            Assert.That(clearRow2, Is.GreaterThan(0).And.LessThan(tex.width), "셋째 줄은 자리마다 비었다 찼다 — 가장자리가 들쭉날쭉");
        }
    }
}
