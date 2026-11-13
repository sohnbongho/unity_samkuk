using System;

namespace Samkuk.Compat
{
    // 그림의 "데이터" 쪽 (Godot 전환, ADR 0004). 순수 로직은 그림을 그리지 않는다: 파일 그림은 경로만, 코드로 만든 그림은 픽셀 배열만 들고
    // 화면 층이 그것을 Godot 텍스처로 바꾼다. 이름과 쓰임새는 유니티 때와 같게 두어 로직·테스트를 그대로 옮긴다.

    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Point, Bilinear }
    public enum TextureWrapMode { Repeat, Clamp }
    public enum FullScreenMode { ExclusiveFullScreen, FullScreenWindow, MaximizedWindow, Windowed }
    public enum SpriteMeshType { FullRect, Tight }
    [Flags] public enum HideFlags { None = 0, DontSave = 52, HideAndDontSave = 61 }

    /// <summary>
    /// 텍스처: 파일 그림이면 <see cref="path"/>(res://...)와 크기만, 코드로 만든 그림이면 <see cref="pixels"/>(왼쪽 아래부터 가로줄 순서, 유니티와 같음)를 든다.
    /// </summary>
    /// <summary>텍스처의 바탕 (이름만).</summary>
    public class Texture : Object
    {
        public string name;
    }

    public sealed class Texture2D : Texture
    {
        public string path;
        public int width, height;
        public FilterMode filterMode = FilterMode.Bilinear;
        public TextureWrapMode wrapMode = TextureWrapMode.Clamp;
        /// <summary>코드로 만든 그림의 픽셀. 파일 그림은 null.</summary>
        public Color32[] pixels;
        /// <summary>픽셀이 바뀔 때마다 오른다 (화면 층이 텍스처를 다시 만들지 판단).</summary>
        public int version;

        public Texture2D() { }
        public Texture2D(string path, int width = 0, int height = 0)
        {
            this.path = path; this.width = width; this.height = height;
            name = System.IO.Path.GetFileNameWithoutExtension(path);
        }
        public Texture2D(int width, int height) : this(width, height, TextureFormat.RGBA32, false) { }
        public Texture2D(int width, int height, TextureFormat format, bool mipChain)
        {
            this.width = width; this.height = height;
            pixels = new Color32[width * height];
        }

        static Texture2D white;
        /// <summary>흰색 4x4 그림 (유니티 Texture2D.whiteTexture 자리).</summary>
        public static Texture2D whiteTexture
        {
            get
            {
                if (white != null) return white;
                white = new Texture2D(4, 4) { name = "white" };
                for (int i = 0; i < white.pixels.Length; i++) white.pixels[i] = new Color32(255, 255, 255, 255);
                return white;
            }
        }

        public void SetPixels32(Color32[] colors) { Array.Copy(colors, pixels, Math.Min(colors.Length, pixels.Length)); version++; }
        public void SetPixels(Color[] colors)
        {
            for (int i = 0; i < colors.Length && i < pixels.Length; i++) pixels[i] = colors[i];
            version++;
        }
        public void SetPixel(int x, int y, Color color) { pixels[y * width + x] = color; version++; }
        public Color32[] GetPixels32() => pixels != null ? (Color32[])pixels.Clone() : new Color32[0];
        public Color GetPixel(int x, int y) => pixels[Mathf.Clamp(y, 0, height - 1) * width + Mathf.Clamp(x, 0, width - 1)];
        /// <summary>유니티에서는 GPU 로 올리는 호출. 여기서는 화면 층이 <see cref="version"/> 을 보고 한다.</summary>
        public void Apply(bool updateMipmaps = true, bool makeNoLongerReadable = false) { }
        public override string ToString() => path ?? name ?? $"Texture2D {width}x{height}";
    }

    /// <summary>그림의 세계 크기 (유니티 Bounds 자리, 크기만).</summary>
    public struct SpriteBounds
    {
        public Vector3 size, center;
        public Vector3 extents => size * 0.5f;
        public Vector3 min => center - extents;
        public Vector3 max => center + extents;
    }

    /// <summary>
    /// 스프라이트: 텍스처의 한 조각 + 피벗 + 유닛당 픽셀. <see cref="rect"/> 와 <see cref="pivot"/> 은 픽셀 단위(유니티와 같음, 왼쪽 아래 기준).
    /// </summary>
    public sealed class Sprite : Object
    {
        public Texture2D texture;
        public Rect rect;
        /// <summary>조각 안에서의 피벗(픽셀).</summary>
        public Vector2 pivot;
        public float pixelsPerUnit = 100f;
        public string name;
        /// <summary>9-슬라이스 테두리(픽셀): x 왼쪽, y 아래, z 오른쪽, w 위. 없으면 0 (UI 프레임 그림만 쓴다).</summary>
        public Vector4 border;

        /// <summary>파일 그림이면 그 경로 (코드로 만든 그림은 null).</summary>
        public string path => texture != null ? texture.path : null;
        /// <summary>피벗을 원점으로 한 그림의 범위 (유닛).</summary>
        public SpriteBounds bounds
        {
            get
            {
                float w = rect.width / pixelsPerUnit, h = rect.height / pixelsPerUnit;
                return new SpriteBounds { size = new Vector3(w, h, 0f), center = new Vector3(w * 0.5f - pivot.x / pixelsPerUnit, h * 0.5f - pivot.y / pixelsPerUnit, 0f) };
            }
        }

        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border)
        {
            var s = Create(texture, rect, pivot, pixelsPerUnit, extrude, meshType);
            s.border = border;
            return s;
        }

        /// <summary><paramref name="pivot"/> 은 0~1 비율 (유니티 Sprite.Create 와 같음).</summary>
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit = 100f, uint extrude = 0, SpriteMeshType meshType = SpriteMeshType.Tight)
            => new Sprite
            {
                texture = texture, rect = rect, pixelsPerUnit = pixelsPerUnit,
                pivot = new Vector2(pivot.x * rect.width, pivot.y * rect.height),
                name = texture != null ? texture.name : null,
            };

        /// <summary>파일 그림 전체를 한 스프라이트로 (데이터 변환이 쓴다). 피벗은 0~1 비율.</summary>
        public static Sprite FromFile(string path, int width, int height, float pixelsPerUnit, Vector2 pivot)
        {
            var s = Create(new Texture2D(path, width, height), new Rect(0, 0, width, height), pivot, pixelsPerUnit);
            s.name = System.IO.Path.GetFileNameWithoutExtension(path);
            return s;
        }

        public override string ToString() => path ?? name ?? "Sprite";
    }
}
