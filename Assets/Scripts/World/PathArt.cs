using System.Collections.Generic;
using Samkuk.Data;
using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 흙길 그림(Step 16-3): 에셋 없이 코드로 만드는 임시 도트 그림. 2x1 유닛 띠 한 토막 = 64x32px (PPU 32), 길이 방향으로 반복.
    /// 위아래 가장자리는 2px 계단으로 들쭉날쭉하게 비워(알파 0) 자연스러운 흙길 가장자리가 된다. 지형마다 흙색이 다르다.
    /// 진짜 그림은 <c>TerrainTheme.pathTexture</c> 에 넣으면 그쪽을 쓴다.
    /// </summary>
    public static class PathArt
    {
        public const int Width = 64, Height = 32;
        static readonly Dictionary<CastleTerrain, Texture2D> cache = new Dictionary<CastleTerrain, Texture2D>();

        public static Texture2D Texture(CastleTerrain terrain)
        {
            if (cache.TryGetValue(terrain, out var tex) && tex != null) return tex;
            tex = Generate(terrain);
            cache[terrain] = tex;
            return tex;
        }

        /// <summary>지형별 흙길 색 (참고 그림의 밝은 주황빛 흙).</summary>
        public static Color Base(CastleTerrain terrain)
        {
            switch (terrain)
            {
                case CastleTerrain.Mountain: return new Color(0.66f, 0.58f, 0.5f);
                case CastleTerrain.Loess: return new Color(0.86f, 0.7f, 0.46f);
                case CastleTerrain.Steppe: return new Color(0.8f, 0.64f, 0.42f);
                case CastleTerrain.Jungle: return new Color(0.68f, 0.5f, 0.34f);
                case CastleTerrain.River: return new Color(0.74f, 0.6f, 0.42f);
                default: return new Color(0.82f, 0.62f, 0.4f);
            }
        }

        static Texture2D Generate(CastleTerrain terrain)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "Path_" + terrain,
            };
            var px = new Color32[Width * Height];
            var rng = new System.Random((int)terrain * 4099 + 7);
            Color baseColor = Base(terrain);

            // 가장자리: 2px 묶음마다 비우는 줄 수가 달라 계단처럼 들쭉날쭉 (위아래 따로)
            int groups = Width / 2;
            var bottomEdge = new int[groups];
            var topEdge = new int[groups];
            for (int g = 0; g < groups; g++)
            {
                bottomEdge[g] = 1 + rng.Next(4);   // 1~4 줄 비움
                topEdge[g] = 1 + rng.Next(4);
            }
            // 반복 경계가 어긋나지 않게 첫 묶음과 마지막 묶음을 같게
            bottomEdge[groups - 1] = bottomEdge[0];
            topEdge[groups - 1] = topEdge[0];

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int g = x / 2;
                    bool clear = y < bottomEdge[g] || y >= Height - topEdge[g];
                    if (clear) { px[y * Width + x] = new Color32(0, 0, 0, 0); continue; }
                    float n = (float)rng.NextDouble();
                    float tone = 0.92f + n * 0.16f;
                    // 가장자리 바로 안쪽은 조금 어둡게 (풀 그늘), 가운데는 밟혀서 밝게
                    bool rim = y < bottomEdge[g] + 2 || y >= Height - topEdge[g] - 2;
                    if (rim) tone *= 0.82f;
                    if (n > 0.975f) tone *= 0.72f;   // 자갈
                    var c = baseColor * tone;
                    px[y * Width + x] = new Color32(ToByte(c.r), ToByte(c.g), ToByte(c.b), 255);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }
}
