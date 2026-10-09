using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 검기 스프라이트를 코드로 만든다(에셋 없음). 오른쪽(+x)을 향한 모양이며 휘두르는/찌르는 방향으로 돌려 쓴다.
    /// 베기는 반원 띠(<see cref="Arc"/>), 찌르기는 뾰족한 화살촉 띠(<see cref="Point"/>). 둘 다 가운데가 두껍고 끝이 얇으며 바깥 가장자리가 밝다.
    /// 무기 이펙트는 "고해상도 효과" 범주라 도트 규격이 아니라 부드러운 알파를 쓴다.
    /// </summary>
    public static class WaveSprites
    {
        const int Size = 64;
        const float PixelsPerUnit = 32f;
        /// <summary>그림 안 모양의 바깥 반지름(픽셀). 배율 1 일 때 유닛 반지름은 <see cref="RadiusUnits"/>.</summary>
        const float OuterRadiusPx = 28f;
        const float ThicknessPx = 7f;
        const float ArcHalfAngleDeg = 75f;
        /// <summary>뾰족한 검기: 촉 끝(+x)에서 뒤쪽 양 날개 끝까지의 각도(반각). 작을수록 더 뾰족하다.</summary>
        const float PointHalfAngleDeg = 32f;

        public static float RadiusUnits => OuterRadiusPx / PixelsPerUnit;

        static Sprite arc, point;

        /// <summary>반원 검기(베기).</summary>
        public static Sprite Arc()
        {
            if (arc == null) arc = Build("SlashArc", ArcAlpha);
            return arc;
        }

        /// <summary>뾰족한 검기(찌르기).</summary>
        public static Sprite Point()
        {
            if (point == null) point = Build("ThrustPoint", PointAlpha);
            return point;
        }

        /// <summary>반원 띠: 각도 ±75도, 양 끝으로 갈수록 얇다.</summary>
        static float ArcAlpha(float dx, float dy)
        {
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            if (ang > ArcHalfAngleDeg) return 0f;

            float taper = Mathf.Cos(ang / ArcHalfAngleDeg * Mathf.PI * 0.5f);
            float thick = ThicknessPx * Mathf.Max(0.15f, taper);
            float inner = OuterRadiusPx - thick;
            if (r > OuterRadiusPx || r < inner) return 0f;

            float t = (r - inner) / thick;
            float edge = Mathf.Min(OuterRadiusPx - r, r - inner);
            return Mathf.Lerp(0.35f, 1f, t) * Mathf.Clamp01(edge + 0.5f) * Mathf.Lerp(0.5f, 1f, taper);
        }

        /// <summary>
        /// 화살촉 띠: 촉 끝 (R, 0) 에서 뒤쪽 두 날개 끝으로 뻗는 두 선분 주위의 띠. 촉 끝이 가장 두껍고 날개 끝으로 갈수록 얇다.
        /// </summary>
        static float PointAlpha(float dx, float dy)
        {
            var tip = new Vector2(OuterRadiusPx, 0f);
            float wingLen = OuterRadiusPx * 1.3f;
            float rad = PointHalfAngleDeg * Mathf.Deg2Rad;
            var wingDir = new Vector2(-Mathf.Cos(rad), Mathf.Sin(rad));           // 촉 끝에서 날개 끝으로 (위쪽)
            var p = new Vector2(dx, Mathf.Abs(dy));                                 // 위아래 대칭

            // 선분 tip → tip + wingDir*wingLen 까지의 거리
            Vector2 rel = p - tip;
            float along = Mathf.Clamp(Vector2.Dot(rel, wingDir), 0f, wingLen);
            Vector2 closest = tip + wingDir * along;
            float dist = (p - closest).magnitude;

            float taper = 1f - along / wingLen;                                     // 촉 끝 1, 날개 끝 0
            float thick = ThicknessPx * 0.5f * Mathf.Max(0.2f, taper);              // 반두께
            if (dist > thick) return 0f;
            // 촉 끝 바깥은 잘라 뾰족하게
            if (dx > OuterRadiusPx + 0.5f) return 0f;

            float edge = thick - dist;
            float toFront = Mathf.Clamp01(1f - dist / thick);                       // 선분에 가까울수록 밝다
            return Mathf.Lerp(0.3f, 1f, toFront) * Mathf.Clamp01(edge + 0.5f) * Mathf.Lerp(0.45f, 1f, taper);
        }

        static Sprite Build(string name, System.Func<float, float, float> alphaAt)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name
            };
            var px = new Color32[Size * Size];
            float c = Size * 0.5f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float a = alphaAt(x + 0.5f - c, y + 0.5f - c);
                if (a <= 0f) continue;
                px[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(a)));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
