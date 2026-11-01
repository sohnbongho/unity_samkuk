using System.Collections.Generic;
using Samkuk.Core;
using Samkuk.Player;
using UnityEngine;

namespace Samkuk.Field
{
    /// <summary>
    /// 야전의 코드 생성 스프라이트 모음. 부대 시트는 15-8 부터 장수 걷기 시트와 같은 규격(4x4, 칸 48, PPU 32, 발 피벗)이라
    /// 자르기는 <see cref="HeroSpriteSet"/> 이 그대로 맡고, 여기엔 시트가 없을 때의 <see cref="Placeholder"/>(색 덩어리 한 명)와
    /// 바닥 마름모(<see cref="Diamond"/>: 선택/목표 테, 사거리 표시, 타일 대용)만 남는다. 만든 것은 색마다 하나만 두고 재사용한다.
    /// </summary>
    public static class FieldSpriteSet
    {
        static readonly Dictionary<Color, Sprite> Placeholders = new Dictionary<Color, Sprite>();

        /// <summary>그림이 없을 때 쓰는 48x48 색 덩어리 (머리 큰 몸 하나, 발 피벗 = 걷기 시트와 같은 자리). 색마다 하나만 만든다.</summary>
        public static Sprite Placeholder(Color color)
        {
            if (Placeholders.TryGetValue(color, out var s) && s != null) return s;
            int n = FieldArtSpec.UnitCell;
            int foot = FieldArtSpec.UnitFootPixels;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            var c = (Color32)color;
            var dark = (Color32)(color * 0.6f);
            var skin = new Color32(233, 196, 158, 255);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int up = y - foot;                                  // 발에서 위로 (텍스처 y 는 아래에서 위)
                    bool body = x >= 16 && x < 32 && up >= 0 && up < 22;
                    bool head = x >= 15 && x < 33 && up >= 22 && up < 40;
                    bool edge = (body && (x == 16 || x == 31 || up == 0)) || (head && (x == 15 || x == 32 || up == 39));
                    px[y * n + x] = edge ? dark : (body ? c : (head ? skin : new Color32(0, 0, 0, 0)));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), HeroSpriteSet.FootPivot(n), PixelArt.PPU, 0, SpriteMeshType.FullRect);
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
