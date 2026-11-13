using System.Collections.Generic;
using Godot;

namespace Samkuk.Game
{
    /// <summary>
    /// 그림 손잡이(순수 로직의 <see cref="Compat.Sprite"/>/<see cref="Compat.Texture2D"/>)를 Godot 텍스처로 바꾸는 한 곳 (G2).
    /// 파일 그림은 경로로 읽어 두고(캐시), 코드로 만든 그림은 픽셀 배열로 ImageTexture 를 만든다(픽셀이 바뀌면 다시 만든다).
    /// </summary>
    public static class Textures
    {
        static readonly Dictionary<string, Texture2D> files = new Dictionary<string, Texture2D>();
        static readonly Dictionary<Compat.Texture2D, (ImageTexture texture, int version)> generated = new Dictionary<Compat.Texture2D, (ImageTexture, int)>();

        /// <summary>손잡이의 Godot 텍스처. 없거나 파일을 못 읽으면 null.</summary>
        public static Texture2D Of(Compat.Texture2D handle)
        {
            if (handle == null) return null;
            if (handle.pixels != null) return OfPixels(handle);
            if (string.IsNullOrEmpty(handle.path)) return null;
            if (files.TryGetValue(handle.path, out var cached)) return cached;

            var loaded = ResourceLoader.Exists(handle.path) ? ResourceLoader.Load<Texture2D>(handle.path) : null;
            if (loaded == null) GD.PushWarning($"[Samkuk] 그림을 읽지 못했습니다: {handle.path}");
            files[handle.path] = loaded;
            return loaded;
        }

        /// <summary>스프라이트가 쓰는 텍스처 전체 (조각 범위는 <see cref="Region"/>).</summary>
        public static Texture2D Of(Compat.Sprite sprite) => sprite != null ? Of(sprite.texture) : null;

        /// <summary>
        /// 스프라이트의 조각 범위를 Godot 좌표(왼쪽 위 기준, y 아래)로. 순수 로직의 rect 는 유니티처럼 왼쪽 아래 기준이다.
        /// </summary>
        public static Rect2 Region(Compat.Sprite sprite)
        {
            var r = sprite.rect;
            float texHeight = sprite.texture != null && sprite.texture.height > 0 ? sprite.texture.height : r.y + r.height;
            return new Rect2(r.x, texHeight - r.y - r.height, r.width, r.height);
        }

        static Texture2D OfPixels(Compat.Texture2D handle)
        {
            if (generated.TryGetValue(handle, out var entry) && entry.version == handle.version) return entry.texture;

            int w = handle.width, h = handle.height;
            var bytes = new byte[w * h * 4];
            // 순수 로직의 픽셀은 아랫줄부터, Godot 이미지는 윗줄부터
            for (int y = 0; y < h; y++)
            {
                int src = (h - 1 - y) * w, dst = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    var c = handle.pixels[src + x];
                    bytes[dst++] = c.r; bytes[dst++] = c.g; bytes[dst++] = c.b; bytes[dst++] = c.a;
                }
            }
            var image = Image.CreateFromData(w, h, false, Image.Format.Rgba8, bytes);
            var texture = entry.texture;
            if (texture == null) texture = ImageTexture.CreateFromImage(image);
            else texture.Update(image);
            generated[handle] = (texture, handle.version);
            return texture;
        }

        /// <summary>로직 색을 Godot 색으로.</summary>
        public static Color ToGodot(this Compat.Color c) => new Color(c.r, c.g, c.b, c.a);
    }
}
