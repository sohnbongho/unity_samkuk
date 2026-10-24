using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 칸 하나의 높이(<see cref="HeightChunk"/>)를 메시로 만드는 순수 계산(Step 16-2): 0층보다 높은 셀의 윗면(바닥 타일 그림, 경사로는 기울어진 면)과
    /// 이웃보다 높은 가장자리의 절벽 옆면(절벽 그림). 옆면은 높은 쪽 셀이 그리므로 한 가장자리가 두 번 그려지지 않는다.
    /// 윗면 UV 는 세계 좌표 / 4 (바닥 타일 한 장 = 4유닛, 배경 타일과 맞물린다), 옆면 UV 는 가장자리 길이 / 2, 높이 / 2 (절벽 그림 한 장 = 2x2 유닛).
    /// 윗면과 옆면은 서브메시 둘(재질이 다르다). 세계 z 는 위가 -z 다.
    /// </summary>
    public static class CliffMeshBuilder
    {
        public const float TopTile = 4f;
        public const float WallTile = 2f;

        public sealed class Buffers
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<Color32> colors = new List<Color32>();
            public readonly List<int> topTriangles = new List<int>();
            public readonly List<int> wallTriangles = new List<int>();

            public void Clear()
            {
                vertices.Clear(); uvs.Clear(); colors.Clear(); topTriangles.Clear(); wallTriangles.Clear();
            }

            public int TopQuads => topTriangles.Count / 6;
            public int WallQuads => wallTriangles.Count / 6;
            public bool IsEmpty => vertices.Count == 0;
        }

        /// <summary>칸의 메시를 버퍼에 쓴다 (버퍼는 비우고 시작). 평지 칸이면 비어 있다.</summary>
        public static void Build(TerrainMap map, Vector2Int chunk, Buffers b)
        {
            b.Clear();
            if (map == null) return;
            var h = map.GetHeights(chunk);
            if (h == null) return;

            for (int cy = 0; cy < HeightChunk.Cells; cy++)
                for (int cx = 0; cx < HeightChunk.Cells; cx++)
                {
                    int level = h.Level(cx, cy);
                    if (level <= 0) continue;
                    var o = TerrainHeight.CellOrigin(chunk, cx, cy);
                    float s = TerrainHeight.CellSize;
                    // 네 모서리 높이 (경사로는 기울어진다): 00 왼아래, 10 오른아래, 01 왼위, 11 오른위
                    float h00 = TerrainHeight.Corner(h, cx, cy, 0, 0), h10 = TerrainHeight.Corner(h, cx, cy, 1, 0);
                    float h01 = TerrainHeight.Corner(h, cx, cy, 0, 1), h11 = TerrainHeight.Corner(h, cx, cy, 1, 1);
                    AddTop(b, o, s, h00, h10, h01, h11);

                    // 옆면: 이웃 셀의 같은 가장자리 높이보다 높으면 그 차이만큼 벽
                    // 남쪽 (y = o.y): 이 셀의 00→10, 이웃(남)의 01→11
                    NeighborEdge(map, chunk, cx, cy - 1, out float n0, out float n1, 0, 1, 1, 1);
                    AddWall(b, new Vector2(o.x, o.y), new Vector2(o.x + s, o.y), h00, h10, n0, n1);
                    // 북쪽 (y = o.y + s): 이 셀의 01→11, 이웃(북)의 00→10
                    NeighborEdge(map, chunk, cx, cy + 1, out n0, out n1, 0, 0, 1, 0);
                    AddWall(b, new Vector2(o.x + s, o.y + s), new Vector2(o.x, o.y + s), h11, h01, n1, n0);
                    // 서쪽 (x = o.x): 이 셀의 01→00, 이웃(서)의 11→10
                    NeighborEdge(map, chunk, cx - 1, cy, out n0, out n1, 1, 1, 1, 0);
                    AddWall(b, new Vector2(o.x, o.y + s), new Vector2(o.x, o.y), h01, h00, n0, n1);
                    // 동쪽 (x = o.x + s): 이 셀의 10→11, 이웃(동)의 00→01
                    NeighborEdge(map, chunk, cx + 1, cy, out n0, out n1, 0, 0, 0, 1);
                    AddWall(b, new Vector2(o.x + s, o.y), new Vector2(o.x + s, o.y + s), h10, h11, n0, n1);
                }
        }

        /// <summary>이웃 셀(칸을 넘어가면 그 칸)의 가장자리 두 모서리 높이.</summary>
        static void NeighborEdge(TerrainMap map, Vector2Int chunk, int cx, int cy, out float a, out float b, int ax, int ay, int bx, int by)
        {
            var nc = chunk;
            if (cx < 0) { cx += HeightChunk.Cells; nc.x--; }
            else if (cx >= HeightChunk.Cells) { cx -= HeightChunk.Cells; nc.x++; }
            if (cy < 0) { cy += HeightChunk.Cells; nc.y--; }
            else if (cy >= HeightChunk.Cells) { cy -= HeightChunk.Cells; nc.y++; }
            var h = map.GetHeights(nc);
            a = TerrainHeight.Corner(h, cx, cy, ax, ay);
            b = TerrainHeight.Corner(h, cx, cy, bx, by);
        }

        static void AddTop(Buffers b, Vector2 o, float s, float h00, float h10, float h01, float h11)
        {
            int i = b.vertices.Count;
            b.vertices.Add(new Vector3(o.x, o.y, TerrainHeight.ZOf(h00)));
            b.vertices.Add(new Vector3(o.x + s, o.y, TerrainHeight.ZOf(h10)));
            b.vertices.Add(new Vector3(o.x, o.y + s, TerrainHeight.ZOf(h01)));
            b.vertices.Add(new Vector3(o.x + s, o.y + s, TerrainHeight.ZOf(h11)));
            b.uvs.Add(new Vector2(o.x / TopTile, o.y / TopTile));
            b.uvs.Add(new Vector2((o.x + s) / TopTile, o.y / TopTile));
            b.uvs.Add(new Vector2(o.x / TopTile, (o.y + s) / TopTile));
            b.uvs.Add(new Vector2((o.x + s) / TopTile, (o.y + s) / TopTile));
            for (int k = 0; k < 4; k++) b.colors.Add(new Color32(255, 255, 255, 255));
            // 카메라(-z 쪽)에서 보이게 시계 반대 방향 (유니티 왼손 좌표계에서 앞면 = 시계 방향, -z 에서 +z 를 보므로)
            b.topTriangles.Add(i); b.topTriangles.Add(i + 2); b.topTriangles.Add(i + 1);
            b.topTriangles.Add(i + 1); b.topTriangles.Add(i + 2); b.topTriangles.Add(i + 3);
        }

        /// <summary>가장자리 p→q 를 따라 이 셀 높이(ha, hb)가 이웃 높이(na, nb)보다 높은 만큼의 벽. 벽은 바깥(이웃) 쪽을 향한다.</summary>
        static void AddWall(Buffers b, Vector2 p, Vector2 q, float ha, float hb, float na, float nb)
        {
            float da = ha - na, db = hb - nb;
            if (da <= TerrainHeight.WallEpsilon && db <= TerrainHeight.WallEpsilon) return;
            // 한쪽만 높으면(경사로 옆) 낮은 쪽은 이웃 높이에 맞춘다
            float ba = Mathf.Min(ha, na), bb = Mathf.Min(hb, nb);
            int i = b.vertices.Count;
            float len = Vector2.Distance(p, q);
            b.vertices.Add(new Vector3(p.x, p.y, TerrainHeight.ZOf(ba)));   // 아래 a
            b.vertices.Add(new Vector3(q.x, q.y, TerrainHeight.ZOf(bb)));   // 아래 b
            b.vertices.Add(new Vector3(p.x, p.y, TerrainHeight.ZOf(ha)));   // 위 a
            b.vertices.Add(new Vector3(q.x, q.y, TerrainHeight.ZOf(hb)));   // 위 b
            float u0 = (p.x + p.y) / WallTile, u1 = u0 + len / WallTile;   // 가장자리 방향과 무관하게 이어지는 u
            b.uvs.Add(new Vector2(u0, ba / WallTile));
            b.uvs.Add(new Vector2(u1, bb / WallTile));
            b.uvs.Add(new Vector2(u0, ha / WallTile));
            b.uvs.Add(new Vector2(u1, hb / WallTile));
            for (int k = 0; k < 4; k++) b.colors.Add(new Color32(255, 255, 255, 255));
            // 양면 셰이더(Cull Off)라 감김 방향은 보이기에 상관없지만, 바깥을 향하는 순서로 둔다
            b.wallTriangles.Add(i); b.wallTriangles.Add(i + 2); b.wallTriangles.Add(i + 1);
            b.wallTriangles.Add(i + 1); b.wallTriangles.Add(i + 2); b.wallTriangles.Add(i + 3);
        }

        /// <summary>버퍼를 메시에 넣는다 (서브메시 0 = 윗면, 1 = 옆면).</summary>
        public static void Apply(Buffers b, Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(b.vertices);
            mesh.SetUVs(0, b.uvs);
            mesh.SetColors(b.colors);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(b.topTriangles, 0);
            mesh.SetTriangles(b.wallTriangles, 1);
            mesh.RecalculateBounds();
        }
    }
}
