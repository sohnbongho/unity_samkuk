using System.Collections.Generic;
using Samkuk.Core;
using UnityEngine;

namespace Samkuk.Field
{
    /// <summary>
    /// 부대 시트(<see cref="FieldArtSpec"/>: 48x48 칸 4개 = 앞모습 2프레임 + 뒷모습 2프레임)를 실행 중에 잘라 둔 것.
    /// <c>HeroSpriteSet</c> 처럼 에디터 슬라이스 없이 <see cref="Sprite.Create"/> 로 자르고 같은 텍스처는 한 번만 자른다.
    /// 피벗은 부대가 선 마름모 가운데(아래에서 12픽셀 위, 가로 가운데)라 트랜스폼 위치 = 칸 가운데이고 y 정렬도 그 점으로 한다.
    /// 시트가 없을 때(테스트, 그림 연결 전)는 <see cref="Placeholder"/> 로 색 마름모를 만든다.
    /// </summary>
    public sealed class FieldSpriteSet
    {
        static readonly Dictionary<Texture2D, FieldSpriteSet> Cache = new Dictionary<Texture2D, FieldSpriteSet>();
        static readonly Dictionary<Color, Sprite> Placeholders = new Dictionary<Color, Sprite>();

        readonly Sprite[] sprites = new Sprite[FieldArtSpec.SheetColumns];

        public static FieldSpriteSet Get(Texture2D sheet)
        {
            if (sheet == null) return null;
            if (Cache.TryGetValue(sheet, out var cached) && cached.sprites[0] != null) return cached;
            var set = new FieldSpriteSet();
            set.Slice(sheet);
            Cache[sheet] = set;
            return set;
        }

        void Slice(Texture2D sheet)
        {
            int cw = sheet.width / FieldArtSpec.SheetColumns;
            int ch = sheet.height;
            var pivot = new Vector2(0.5f, ch > 0 ? (float)FieldArtSpec.UnitFootPixels / ch : 0f);
            for (int i = 0; i < FieldArtSpec.SheetColumns; i++)
            {
                var sprite = Sprite.Create(sheet, new Rect(i * cw, 0, cw, ch), pivot, PixelArt.PPU, 0, SpriteMeshType.FullRect);
                sprite.name = $"{sheet.name}_{i}";
                sprites[i] = sprite;
            }
        }

        public Sprite Get(bool back, int frame) => sprites[FieldArtSpec.SheetColumn(back, frame)];

        /// <summary>그림이 없을 때 쓰는 48x48 색 덩어리 (마름모 위에 네모 몸통). 색마다 하나만 만든다.</summary>
        public static Sprite Placeholder(Color color)
        {
            if (Placeholders.TryGetValue(color, out var s) && s != null) return s;
            int n = FieldArtSpec.UnitCell;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            var c = (Color32)color;
            var dark = (Color32)(color * 0.6f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool body = x >= 16 && x < 32 && y >= 12 && y < 40;
                    bool edge = body && (x == 16 || x == 31 || y == 12 || y == 39);
                    px[y * n + x] = body ? (edge ? dark : c) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, (float)FieldArtSpec.UnitFootPixels / n), PixelArt.PPU, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            Placeholders[color] = s;
            return s;
        }

        /// <summary>마름모 테(선택 표시, 목표 표시)나 바닥 타일 대용. 48x24, 테두리만 또는 속까지.</summary>
        public static Sprite Diamond(Color color, bool filled)
        {
            int w = IsoGrid.TileWidthPx, h = IsoGrid.TileHeightPx;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[w * h];
            var c = (Color32)color;
            for (int y = 0; y < h; y++)
            {
                int dy = y < h / 2 ? y : h - 1 - y;
                int half = 2 * (dy + 1);
                int x0 = w / 2 - half, x1 = w / 2 + half;
                for (int x = x0; x < x1; x++)
                {
                    bool edge = x < x0 + 2 || x >= x1 - 2 || dy == 0;
                    if (filled || edge) px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelArt.PPU, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
