using System.Collections.Generic;
using Samkuk.Data;
using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 전투 맵의 지형이 이동에 주는 효과: 나무/바위 같은 소품은 **막고**, 강물/연못은 **느려진다**.
    /// <see cref="TerrainMap"/> 의 칸 배치(자동 생성 + 직접 고친 칸)에서 "막는 원"과 "느려지는 원"을 뽑아
    /// 4x4 유닛 셀 격자에 넣어 두고, 움직이는 것(플레이어, 적, 아군)이 매 틱 자기 셀 하나만 보고 속도를 고친다.
    /// 유니티 물리 콜라이더를 쓰지 않는 이유: 적 수백 마리가 이미 코드로 겹침을 풀고 있어 같은 방식이 가볍고,
    /// 화면 없이 계산하는 순수 로직이라 테스트할 수 있다. 막힌 쪽으로 가는 속도 성분만 지우므로 장애물을 따라 미끄러진다.
    /// 전투에 적용된 것은 <see cref="Active"/> 로 찾는다 (없으면 지형 효과 없음 = 예전처럼 어디든 걷는다).
    /// </summary>
    public sealed class TerrainCollision
    {
        /// <summary>막는 원(slow = 0) 또는 느려지는 원(slow = 속도 배율).</summary>
        public struct Circle
        {
            public Vector2 center;
            public float radius;
            public float slow;          // 0 이면 장애물, 0 보다 크면 그 안에서의 속도 배율
            public Vector2Int chunk;    // 어느 칸의 물건에서 나왔나 (칸을 다시 만들 때 지우기 위해)
            public bool Blocks => slow <= 0f;
        }

        public const float CellSize = 4f;
        /// <summary>질의하는 몸의 최대 반지름(보스 포함) + 여유. 원을 셀에 넣을 때 이만큼 넓게 넣어 질의는 자기 셀 하나만 본다.</summary>
        const float BodyMargin = 1.6f;
        /// <summary>겹쳤을 때 밀어내는 최대 속도 (유닛/초). 너무 크면 튕기고 너무 작으면 한동안 소품 안에 서 있다.</summary>
        const float PushOutSpeed = 6f;
        /// <summary>닿기 직전부터 미끄러지게 하는 여유. 이산 이동으로 조금 파고드는 것을 줄인다.</summary>
        const float Skin = 0.04f;
        /// <summary>정면으로 막혀 거의 멈췄다고 보는 기준 (남은 속도² / 원래 속도²).</summary>
        const float StuckRatioSqr = 0.3f * 0.3f;

        /// <summary>지금 전투에 적용된 지형 충돌. <c>InfiniteBackground</c> 가 맵을 적용할 때 정하고, 없으면 null.</summary>
        public static TerrainCollision Active { get; set; }

        readonly TerrainMap map;
        readonly Dictionary<Vector2Int, List<Circle>> cells = new Dictionary<Vector2Int, List<Circle>>();
        readonly HashSet<Vector2Int> builtChunks = new HashSet<Vector2Int>();
        readonly List<Circle> scratch = new List<Circle>(64);
        Vector2Int lastEnsured = new Vector2Int(int.MinValue, int.MinValue);

        public TerrainMap Map => map;
        public int BuiltChunkCount => builtChunks.Count;

        public TerrainCollision(TerrainMap terrainMap)
        {
            map = terrainMap;
        }

        // ───────────────────────── 질의 ─────────────────────────

        /// <summary>이 지점에서의 속도 배율 (1 = 평소). 느려지는 원이 겹치면 가장 느린 값. 몸의 중심(발)이 물에 있을 때만 느려진다.</summary>
        public float SpeedFactor(Vector2 position)
        {
            var list = CellAt(position);
            if (list == null) return 1f;

            float factor = 1f;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c.Blocks || c.slow >= factor) continue;
                if ((position - c.center).sqrMagnitude < c.radius * c.radius) factor = c.slow;
            }
            return factor;
        }

        /// <summary>반지름 <paramref name="radius"/> 인 몸이 막는 소품에 겹쳐 있는가.</summary>
        public bool Overlaps(Vector2 position, float radius)
        {
            var list = CellAt(position);
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (!c.Blocks) continue;
                float min = c.radius + radius;
                if ((position - c.center).sqrMagnitude < min * min) return true;
            }
            return false;
        }

        /// <summary>
        /// 이동 속도를 지형에 맞게 고친다: 막는 소품 쪽으로 향하는 성분을 지워 **미끄러지게** 하고, 이미 겹쳐 있으면 밖으로 밀어낸다.
        /// <paramref name="deflect"/> 가 true 면(적) 정면으로 막혀 거의 멈출 때 목표에 가까운 쪽 접선으로 돌아가고(<paramref name="side"/> 는
        /// 완전히 정면일 때 고를 방향, +1/-1), false 면(플레이어) 그냥 멈춘다. 속도 배율(<see cref="SpeedFactor"/>)은 여기서 곱하지 않는다.
        /// </summary>
        public Vector2 Resolve(Vector2 position, float radius, Vector2 velocity, float dt, bool deflect = false, int side = 1)
        {
            var list = CellAt(position);
            if (list == null) return velocity;

            Vector2 slide = velocity;
            Vector2 push = Vector2.zero;
            float speedSqr = velocity.sqrMagnitude;
            float invDt = dt > 1e-5f ? 1f / dt : 0f;

            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (!c.Blocks) continue;

                Vector2 d = position - c.center;
                float min = c.radius + radius;
                float reach = min + Skin;
                float dSqr = d.sqrMagnitude;
                if (dSqr >= reach * reach) continue;

                float dist = Mathf.Sqrt(dSqr);
                Vector2 n = dist > 1e-4f ? d / dist : Tangent(Vector2.right, side);   // 정확히 중심에 있으면 아무 쪽으로든 밀어낸다

                if (dist < min)   // 겹침: 바깥으로. 한 틱에 다 빼지 않고 상한을 둬 튕기지 않게 한다
                    push += n * Mathf.Min((min - dist) * invDt, PushOutSpeed);

                float into = Vector2.Dot(slide, n);
                if (into >= 0f) continue;   // 멀어지는 중이면 그대로
                slide -= n * into;          // 안쪽 성분 제거 = 접선 방향으로 미끄러짐

                if (deflect && speedSqr > 1e-6f && slide.sqrMagnitude < speedSqr * StuckRatioSqr)
                {
                    // 정면으로 막혀 멈춤: 원래 가려던 쪽에 가까운 접선으로 돌아간다 (정확히 정면이면 side 로 고른다)
                    Vector2 t = Tangent(n, 1);
                    float along = Vector2.Dot(t, velocity);
                    int pick = along > 1e-3f ? 1 : along < -1e-3f ? -1 : (side >= 0 ? 1 : -1);
                    slide = t * (pick * Mathf.Sqrt(speedSqr) * 0.8f);
                }
            }
            return slide + push;
        }

        /// <summary>막는 소품 안이면 가장 가까운 바깥 자리로 옮긴 위치 (스폰/순간이동용). 아니면 그대로.</summary>
        public Vector2 PushOut(Vector2 position, float radius)
        {
            for (int pass = 0; pass < 8; pass++)   // 빽빽한 숲에서는 한 나무에서 밀려 나온 자리가 다른 나무 안일 수 있어 몇 번 되풀이한다
            {
                var list = CellAt(position);
                if (list == null) return position;

                bool moved = false;
                for (int i = 0; i < list.Count; i++)
                {
                    var c = list[i];
                    if (!c.Blocks) continue;
                    Vector2 d = position - c.center;
                    float min = c.radius + radius;
                    float dSqr = d.sqrMagnitude;
                    if (dSqr >= min * min) continue;

                    float dist = Mathf.Sqrt(dSqr);
                    Vector2 n = dist > 1e-4f ? d / dist : Vector2.up;
                    position = c.center + n * (min + 0.02f);
                    moved = true;
                }
                if (!moved) return position;
            }
            return position;
        }

        /// <summary>이 칸(과 이웃 칸에 번진 것)의 원을 지워 다음 질의 때 다시 만들게 한다 (맵 편집기에서 칸을 고쳤을 때).</summary>
        public void Invalidate(Vector2Int chunk)
        {
            if (!builtChunks.Remove(chunk)) return;

            // 이 칸의 원은 이웃 셀까지 번져 있으니 칸 둘레 한 셀 여유를 두고 지운다
            float c = TerrainMap.ChunkSize;
            int x0 = Mathf.FloorToInt((chunk.x * c - CellSize) / CellSize), x1 = Mathf.FloorToInt(((chunk.x + 1) * c + CellSize) / CellSize);
            int y0 = Mathf.FloorToInt((chunk.y * c - CellSize) / CellSize), y1 = Mathf.FloorToInt(((chunk.y + 1) * c + CellSize) / CellSize);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (cells.TryGetValue(new Vector2Int(x, y), out var list))
                        list.RemoveAll(ci => ci.chunk == chunk);
            lastEnsured = new Vector2Int(int.MinValue, int.MinValue);
        }

        /// <summary>한 칸에서 나온 원들 (테스트/표시용).</summary>
        public void CollectCircles(Vector2Int chunk, List<Circle> result)
        {
            scratch.Clear();
            Extract(chunk, scratch);
            result.AddRange(scratch);
        }

        // ───────────────────────── 내부 ─────────────────────────

        List<Circle> CellAt(Vector2 position)
        {
            EnsureAround(position);
            cells.TryGetValue(CellOf(position), out var list);
            return list;
        }

        static Vector2Int CellOf(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.y / CellSize));

        /// <summary>위치가 속한 칸과 이웃 8칸을 만들어 둔다 (이웃 칸의 소품이 이 칸으로 번질 수 있다). 같은 칸이 이어지면 바로 돌아간다.</summary>
        void EnsureAround(Vector2 position)
        {
            var center = TerrainMap.ChunkOf(position);
            if (center == lastEnsured) return;
            lastEnsured = center;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    Build(new Vector2Int(center.x + dx, center.y + dy));
        }

        void Build(Vector2Int chunk)
        {
            if (!builtChunks.Add(chunk)) return;

            scratch.Clear();
            Extract(chunk, scratch);
            foreach (var ci in scratch)
            {
                float reach = ci.radius + BodyMargin;
                int x0 = Mathf.FloorToInt((ci.center.x - reach) / CellSize), x1 = Mathf.FloorToInt((ci.center.x + reach) / CellSize);
                int y0 = Mathf.FloorToInt((ci.center.y - reach) / CellSize), y1 = Mathf.FloorToInt((ci.center.y + reach) / CellSize);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var key = new Vector2Int(x, y);
                        if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<Circle>(8);
                        list.Add(ci);
                    }
            }
        }

        /// <summary>칸의 배치에서 막는 원과 느려지는 원을 뽑는다. 강물 토막은 폭의 절반을 반지름으로, 소품은 자기 값 x 크기 배율.</summary>
        void Extract(Vector2Int chunk, List<Circle> result)
        {
            if (map == null) return;
            var placements = map.GetChunk(chunk.x, chunk.y);
            float riverSlow = map.RiverSlowFactor;

            foreach (var p in placements)
            {
                if (p.prop == null) continue;
                switch (TerrainMap.KindOf(p))
                {
                    case MapItemKind.Water:
                        if (riverSlow < 1f)
                            result.Add(new Circle { center = p.position, radius = TerrainMap.RiverWidthOf(p) * 0.5f, slow = riverSlow, chunk = chunk });
                        break;
                    case MapItemKind.Bank:
                    case MapItemKind.Patch:
                        break;
                    default:   // 소품, 연못, 깃발
                        float scale = p.scale > 0f ? p.scale : 1f;
                        if (p.prop.Blocks)
                            result.Add(new Circle { center = p.position, radius = p.prop.blockRadius * scale, slow = 0f, chunk = chunk });
                        if (p.prop.Slows)
                            result.Add(new Circle { center = p.position, radius = p.prop.slowRadius * scale, slow = p.prop.slowFactor, chunk = chunk });
                        break;
                }
            }
        }

        static Vector2 Tangent(Vector2 n, int side) => new Vector2(-n.y, n.x) * (side >= 0 ? 1f : -1f);
    }
}
