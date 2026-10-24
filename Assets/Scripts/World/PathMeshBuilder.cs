using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 칸 하나를 지나는 흙길(<see cref="TerrainMap.PathDistance"/> 의 곡선)을 띠 메시로 만드는 순수 계산(Step 16-3).
    /// 길 중심선을 <see cref="Step"/> 간격으로 따라가며 양옆으로 폭의 절반씩 벌린 사각형을 잇는다. 칸 밖 한 점씩 더 넣어 이웃 칸의 띠와 겹쳐 이어진다.
    /// 강을 건너는 자리는 끊는다(여울처럼). UV 는 길이 / 2(그림 한 장 = 2유닛), 폭 방향 0~1. 윗면 서브메시(0)만 쓴다.
    /// </summary>
    public static class PathMeshBuilder
    {
        public const float Step = 1f;
        public const float TexLength = 2f;
        /// <summary>바닥 타일(z 0.01)보다 살짝 앞, 서 있는 그림의 발(0)보다 뒤.</summary>
        public const float Z = 0.006f;

        public static void Build(TerrainMap map, Vector2Int chunk, CliffMeshBuilder.Buffers b)
        {
            b.Clear();
            if (map == null || !map.HasPath) return;

            float ox = chunk.x * TerrainMap.ChunkSize, oy = chunk.y * TerrainMap.ChunkSize;
            float margin = map.PathWidth;   // 띠 폭만큼 칸 밖까지 보고 이어 붙인다
            float minX = ox - margin, maxX = ox + TerrainMap.ChunkSize + margin, minY = oy - margin, maxY = oy + TerrainMap.ChunkSize + margin;

            map.PathRange(new Rect(ox, oy, TerrainMap.ChunkSize, TerrainMap.ChunkSize), out int k0, out int k1, out float uMin, out float uMax);
            int first = Mathf.FloorToInt(uMin / Step) - 1, last = Mathf.CeilToInt(uMax / Step) + 1;
            float half = map.PathWidth * 0.5f;

            for (int k = k0; k <= k1; k++)
            {
                bool havePrev = false;
                Vector2 prev = default, prevN = default;
                float prevU = 0f;
                for (int i = first; i <= last; i++)
                {
                    float u = i * Step;
                    Vector2 p = map.PathPoint(u, k, out Vector2 tangent);
                    bool inside = p.x >= minX && p.x < maxX && p.y >= minY && p.y < maxY;
                    bool dry = !map.HasRiver || map.RiverDistance(p) >= map.RiverWidth * 0.5f * 1.35f + 0.3f;
                    if (!inside || !dry) { havePrev = false; continue; }

                    Vector2 n = new Vector2(-tangent.y, tangent.x);
                    if (havePrev) AddQuad(b, prev, prevN, p, n, prevU, u, half);
                    prev = p; prevN = n; prevU = u; havePrev = true;
                }
            }
        }

        static void AddQuad(CliffMeshBuilder.Buffers b, Vector2 a, Vector2 an, Vector2 c, Vector2 cn, float ua, float uc, float half)
        {
            int i = b.vertices.Count;
            b.vertices.Add(new Vector3(a.x - an.x * half, a.y - an.y * half, Z));
            b.vertices.Add(new Vector3(a.x + an.x * half, a.y + an.y * half, Z));
            b.vertices.Add(new Vector3(c.x - cn.x * half, c.y - cn.y * half, Z));
            b.vertices.Add(new Vector3(c.x + cn.x * half, c.y + cn.y * half, Z));
            b.uvs.Add(new Vector2(ua / TexLength, 0f));
            b.uvs.Add(new Vector2(ua / TexLength, 1f));
            b.uvs.Add(new Vector2(uc / TexLength, 0f));
            b.uvs.Add(new Vector2(uc / TexLength, 1f));
            for (int k = 0; k < 4; k++) b.colors.Add(new Color32(255, 255, 255, 255));
            b.topTriangles.Add(i); b.topTriangles.Add(i + 2); b.topTriangles.Add(i + 1);
            b.topTriangles.Add(i + 1); b.topTriangles.Add(i + 2); b.topTriangles.Add(i + 3);
        }
    }
}
