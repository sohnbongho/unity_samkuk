using System;
using UnityEngine;

namespace Samkuk.World
{
    /// <summary>경사로가 내려가는 쪽. 경사로 셀은 자기 층의 반대쪽 가장자리에서 이쪽 가장자리로 한 층 내려간다.</summary>
    public enum RampDir : byte { None = 0, South = 1, North = 2, West = 3, East = 4 }

    /// <summary>
    /// 칸(12x12 유닛) 하나의 높이(Step 16-2): 2x2 유닛 셀 6x6 의 층(0~<see cref="TerrainHeight.MaxLevel"/>)과 경사로 방향.
    /// 층 사이는 절벽(벽)이고 경사로 셀만 한 층을 잇는다. 값 타입이 아니라 복사는 <see cref="Clone"/> 으로.
    /// </summary>
    [Serializable]
    public sealed class HeightChunk
    {
        public const int Cells = 6;   // = TerrainMap.ChunkSize / TerrainHeight.CellSize
        public const int Count = Cells * Cells;

        public byte[] levels = new byte[Count];
        public byte[] ramps = new byte[Count];

        public static int Index(int cx, int cy) => cy * Cells + cx;

        public int Level(int cx, int cy) => levels[Index(cx, cy)];
        public RampDir Ramp(int cx, int cy) => (RampDir)ramps[Index(cx, cy)];

        public void Set(int cx, int cy, int level, RampDir ramp)
        {
            int i = Index(cx, cy);
            levels[i] = (byte)Mathf.Clamp(level, 0, TerrainHeight.MaxLevel);
            ramps[i] = (byte)(levels[i] > 0 ? ramp : RampDir.None);   // 0층에는 경사로가 있을 수 없다
        }

        /// <summary>모두 0층이고 경사로도 없는가.</summary>
        public bool IsFlat
        {
            get
            {
                for (int i = 0; i < Count; i++) if (levels[i] != 0 || ramps[i] != 0) return false;
                return true;
            }
        }

        public HeightChunk Clone()
        {
            var c = new HeightChunk();
            Array.Copy(levels, c.levels, Count);
            Array.Copy(ramps, c.ramps, Count);
            return c;
        }

        public bool SameAs(HeightChunk other)
        {
            if (other == null) return false;
            for (int i = 0; i < Count; i++) if (levels[i] != other.levels[i] || ramps[i] != other.ramps[i]) return false;
            return true;
        }
    }

    /// <summary>
    /// 높이의 수치와 순수 계산(Step 16-2). 셀은 2x2 유닛(세계 격자에 맞춤), 한 층은 1유닛(캐릭터 키 정도).
    /// 세계 z 는 땅이 0, 위가 -z 이므로 높이 h 인 곳의 z 는 -h (<see cref="ZOf"/>).
    /// </summary>
    public static class TerrainHeight
    {
        public const float CellSize = 2f;
        public const int MaxLevel = 3;
        public const float LevelHeight = 1f;
        /// <summary>시작 위치 주변은 평지로 둔다 (벽이 시작점을 둘러싸지 않게, 소품 비우는 반지름보다 넓게).</summary>
        public const float FlatRadius = 4.5f;
        /// <summary>벽으로 치는 높이 차 (이보다 작으면 같은 높이로 본다).</summary>
        public const float WallEpsilon = 0.05f;

        /// <summary>높이(층 단위 실수)를 세계 z 로.</summary>
        public static float ZOf(float height) => -height * LevelHeight;

        /// <summary>세계 위치가 속한 칸과 칸 안 셀 번호, 셀 안 상대 좌표(0~1).</summary>
        public static void Locate(Vector2 pos, out Vector2Int chunk, out int cx, out int cy, out float fx, out float fy)
        {
            chunk = TerrainMap.ChunkOf(pos);
            float lx = pos.x - chunk.x * TerrainMap.ChunkSize, ly = pos.y - chunk.y * TerrainMap.ChunkSize;
            cx = Mathf.Clamp(Mathf.FloorToInt(lx / CellSize), 0, HeightChunk.Cells - 1);
            cy = Mathf.Clamp(Mathf.FloorToInt(ly / CellSize), 0, HeightChunk.Cells - 1);
            fx = Mathf.Clamp01(lx / CellSize - cx);
            fy = Mathf.Clamp01(ly / CellSize - cy);
        }

        /// <summary>셀의 세계 좌표 왼쪽 아래 모서리.</summary>
        public static Vector2 CellOrigin(Vector2Int chunk, int cx, int cy) =>
            new Vector2(chunk.x * TerrainMap.ChunkSize + cx * CellSize, chunk.y * TerrainMap.ChunkSize + cy * CellSize);

        /// <summary>
        /// 셀 안 상대 좌표(0~1)에서의 바닥 높이(층 단위). 경사로면 내려가는 쪽 가장자리에서 한 층 아래, 반대쪽에서 자기 층.
        /// </summary>
        public static float Surface(int level, RampDir ramp, float fx, float fy)
        {
            if (level <= 0) return 0f;
            switch (ramp)
            {
                case RampDir.South: return level - 1 + fy;
                case RampDir.North: return level - 1 + (1f - fy);
                case RampDir.West: return level - 1 + fx;
                case RampDir.East: return level - 1 + (1f - fx);
                default: return level;
            }
        }

        public static float Surface(HeightChunk h, int cx, int cy, float fx, float fy) =>
            h == null ? 0f : Surface(h.Level(cx, cy), h.Ramp(cx, cy), fx, fy);

        /// <summary>셀 모서리(cornerX, cornerY 는 0 또는 1)의 높이.</summary>
        public static float Corner(HeightChunk h, int cx, int cy, int cornerX, int cornerY) =>
            Surface(h, cx, cy, cornerX, cornerY);

        /// <summary>높이 차가 벽인가.</summary>
        public static bool IsWall(float a, float b) => Mathf.Abs(a - b) > WallEpsilon;

        /// <summary>
        /// 전투에서 어떤 2D 위치의 세계 z (틸트일 때만 높이를 쓴다. 지형 맵이 없거나 틸트가 꺼져 있으면 0).
        /// 캐릭터·소품·이펙트가 언덕 위에 올라서게 하는 한 곳.
        /// </summary>
        public static float ZAt(Vector2 pos)
        {
            var c = TerrainCollision.Active;
            if (c == null || c.Map == null || !TiltView.Active) return 0f;
            return ZOf(c.Map.HeightAt(pos));
        }

        /// <summary>2D 위치를 그 자리의 바닥 높이에 올린 3D 위치.</summary>
        public static Vector3 Feet(Vector2 pos) => new Vector3(pos.x, pos.y, ZAt(pos));

        /// <summary>경사로 방향의 반대(올라가는 쪽).</summary>
        public static RampDir Opposite(RampDir d)
        {
            switch (d)
            {
                case RampDir.South: return RampDir.North;
                case RampDir.North: return RampDir.South;
                case RampDir.West: return RampDir.East;
                case RampDir.East: return RampDir.West;
                default: return RampDir.None;
            }
        }
    }
}
