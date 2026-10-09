using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 호 잔상(검기) 스프라이트를 코드로 만든다(에셋 없음). 오른쪽(+x)으로 벌어진 반원 띠로,
    /// 가운데가 두껍고 양 끝으로 갈수록 얇아지며 바깥쪽 가장자리가 밝다. 휘두르는 방향으로 돌려 쓴다.
    /// 무기 이펙트는 "고해상도 효과" 범주라 도트 규격이 아니라 부드러운 알파를 쓴다.
    /// </summary>
    public static class SlashArcSprite
    {
        const int Size = 64;
        const float PixelsPerUnit = 32f;
        /// <summary>그림 안 호의 바깥 반지름(픽셀). 배율 1 일 때 유닛 반지름은 <see cref="RadiusUnits"/>.</summary>
        const float OuterRadiusPx = 28f;
        const float ThicknessPx = 7f;
        const float HalfAngleDeg = 75f;

        public static float RadiusUnits => OuterRadiusPx / PixelsPerUnit;

        static Sprite sprite;

        public static Sprite Get()
        {
            if (sprite != null) return sprite;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "SlashArc"
            };
            var px = new Color32[Size * Size];
            float c = Size * 0.5f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                if (ang > HalfAngleDeg) continue;

                // 양 끝으로 갈수록 얇아진다 (가운데 1, 끝 0)
                float taper = Mathf.Cos(ang / HalfAngleDeg * Mathf.PI * 0.5f);
                float thick = ThicknessPx * Mathf.Max(0.15f, taper);
                float inner = OuterRadiusPx - thick;
                if (r > OuterRadiusPx || r < inner) continue;

                // 바깥 가장자리가 밝고 안쪽으로 갈수록 옅어진다, 가장자리 1픽셀은 부드럽게
                float t = (r - inner) / thick;
                float edge = Mathf.Min(OuterRadiusPx - r, r - inner);
                float a = Mathf.Lerp(0.35f, 1f, t) * Mathf.Clamp01(edge + 0.5f) * Mathf.Lerp(0.5f, 1f, taper);
                byte v = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(a));
                px[y * Size + x] = new Color32(255, 255, 255, v);
            }
            tex.SetPixels32(px);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
