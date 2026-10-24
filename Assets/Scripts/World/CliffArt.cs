using System.Collections.Generic;
using Samkuk.Data;
using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 절벽 옆면 그림(Step 16-2): 에셋 없이 코드로 만드는 임시 도트 그림. 2x2 유닛 = 64x64px (PPU 32), 반복되는 흙 단층 무늬.
    /// 지형마다 색이 다르다(산악은 회색 바위, 황토는 누런 흙, 나머지는 갈색 흙). 진짜 그림은 <c>TerrainTheme.cliffTexture</c> 에 넣으면 그쪽을 쓴다.
    /// </summary>
    public static class CliffArt
    {
        public const int Size = 64;
        static readonly Dictionary<CastleTerrain, Texture2D> cache = new Dictionary<CastleTerrain, Texture2D>();

        public static Texture2D Texture(CastleTerrain terrain)
        {
            if (cache.TryGetValue(terrain, out var tex) && tex != null) return tex;
            tex = Generate(terrain);
            cache[terrain] = tex;
            return tex;
        }

        /// <summary>지형별 기본 흙색.</summary>
        public static Color Base(CastleTerrain terrain)
        {
            switch (terrain)
            {
                case CastleTerrain.Mountain: return new Color(0.42f, 0.4f, 0.4f);
                case CastleTerrain.Loess: return new Color(0.62f, 0.47f, 0.26f);
                case CastleTerrain.Steppe: return new Color(0.5f, 0.36f, 0.22f);
                case CastleTerrain.Jungle: return new Color(0.38f, 0.28f, 0.18f);
                case CastleTerrain.River: return new Color(0.44f, 0.33f, 0.22f);
                default: return new Color(0.47f, 0.33f, 0.2f);
            }
        }

        static Texture2D Generate(CastleTerrain terrain)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "Cliff_" + terrain,
            };
            var px = new Color32[Size * Size];
            var rng = new System.Random((int)terrain * 7919 + 11);
            Color baseColor = Base(terrain);
            // 단층 줄: 몇 줄마다 색이 조금씩 다른 띠, 띠 경계는 어두운 선
            int[] bandStart = { 0, 9, 22, 31, 44, 55 };
            for (int y = 0; y < Size; y++)
            {
                int band = 0;
                for (int k = 0; k < bandStart.Length; k++) if (y >= bandStart[k]) band = k;
                float bandTone = 0.9f + 0.2f * ((band * 37) % 5) / 4f;
                bool edge = y == bandStart[band];
                for (int x = 0; x < Size; x++)
                {
                    float n = (float)rng.NextDouble();
                    float tone = bandTone * (0.9f + n * 0.2f);
                    if (edge) tone *= 0.72f;
                    // 드문드문 박힌 돌 (밝은 점)과 틈 (어두운 점)
                    if (n > 0.965f) tone *= 1.25f;
                    else if (n < 0.03f) tone *= 0.7f;
                    // 윗줄은 풀 그늘이 드리워 어둡게 (윗면과 맞닿는 곳)
                    if (y >= Size - 3) tone *= 0.6f;
                    var c = baseColor * tone;
                    px[y * Size + x] = new Color32(ToByte(c.r), ToByte(c.g), ToByte(c.b), 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }
}
