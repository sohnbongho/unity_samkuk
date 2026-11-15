using System;
using System.Collections.Generic;
using Godot;

namespace Samkuk.Game
{
    /// <summary>
    /// 코드로 만드는 모델 (3D 전환의 임시 모델). 도트 그림이 코드 생성 임시 그림인 것과 같은 자리다.
    /// 모델 좌표: 발이 원점, y 위, +z 가 앞(화면 아래쪽을 볼 때의 정면). 사람 모델은 키 1, 소품 모델은 폭 1 x 높이 1 로 만들고 인스턴스 배율로 키운다.
    /// 카툰 3D(G20~): 사람·말은 둥근 모양(회전체 Lathe/Ball/Dome/Pill, 꼭짓점마다 부드러운 법선)으로 빚고 툰 셰이딩·외곽선을 입힌다.
    /// 상자·각기둥·덩어리는 면마다 법선을 따로 둬 각진 면이 그대로 보인다(날붙이, 바위).
    /// 걷기: UV.x = 흔들 방향(±1), UV.y = 흔들 축 높이 (shaders/toon.gdshader 가 읽는다).
    /// 진짜 모델(구매한 번들 등)로 바꿀 때는 <see cref="ModelMirror"/> 의 모델 고르기에서 이 생성기 대신 파일의 메시를 넘기면 된다.
    /// </summary>
    public sealed class LowPolyMesh
    {
        readonly List<Vector3> positions = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Vector2> uv2s = new List<Vector2>();   // x = 1 이면 무기 든 팔 (공격 자세에서 앞으로 든다)
        float arm;
        int faceSeed = 17;

        // 지금 붙이는 부분의 흔들 값
        float swing, pivotY;

        public LowPolyMesh Limb(float swingSign, float pivot) { swing = swingSign; pivotY = pivot; return this; }
        public LowPolyMesh Body() { swing = 0f; pivotY = 0f; arm = 0f; return this; }
        /// <summary>지금부터 붙이는 부분이 무기 든 팔인가 (Limb 뒤에 부른다, Body 가 끈다).</summary>
        public LowPolyMesh WeaponArm() { arm = 1f; return this; }

        public ArrayMesh Build()
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = positions.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV2] = uv2s.ToArray();
            arrays[(int)Mesh.ArrayType.Custom0] = OutlineNormals();
            var mesh = new ArrayMesh();
            var format = (Mesh.ArrayFormat)((long)Mesh.ArrayCustomFormat.RgbFloat << (int)Mesh.ArrayFormat.FormatCustom0Shift);
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, null, null, format);
            return mesh;
        }

        // 외곽선(뒤집은 껍질)이 꼭짓점을 미는 방향: 같은 자리에 있는 꼭짓점들의 법선(서로 다른 것만)을 더한 것.
        // 면마다 법선이 따로라 그대로 밀면 모서리에서 껍질이 갈라진다. 얇은 양면 판(풀잎, 깃발)은 서로 지워져 0 → 선이 없다.
        float[] OutlineNormals()
        {
            var sums = new Dictionary<(int, int, int), List<Vector3>>();
            (int, int, int) Key(Vector3 p) => (Mathf.RoundToInt(p.X * 2000f), Mathf.RoundToInt(p.Y * 2000f), Mathf.RoundToInt(p.Z * 2000f));
            for (int i = 0; i < positions.Count; i++)
            {
                var key = Key(positions[i]);
                if (!sums.TryGetValue(key, out var list)) sums[key] = list = new List<Vector3>(4);
                var n = normals[i];
                bool known = false;
                for (int k = 0; k < list.Count; k++) if (list[k].Dot(n) > 0.999f) { known = true; break; }
                if (!known) list.Add(n);
            }
            var result = new float[positions.Count * 3];
            for (int i = 0; i < positions.Count; i++)
            {
                var sum = Vector3.Zero;
                foreach (var n in sums[Key(positions[i])]) sum += n;
                if (sum.LengthSquared() > 0.01f) sum = sum.Normalized(); else sum = Vector3.Zero;
                result[i * 3] = sum.X; result[i * 3 + 1] = sum.Y; result[i * 3 + 2] = sum.Z;
            }
            return result;
        }

        /// <summary>면마다 밝기를 흔드는 폭. 카툰 화면은 색면이 고르게 칠해져야 해서 0 (각진 3D 때는 0.05).</summary>
        public float faceJitter = 0f;

        // 면마다 밝기를 ±faceJitter 흔든다 (결정적)
        Color Shade(Color c)
        {
            faceSeed = faceSeed * 1103515245 + 12345;
            float k = 1f - faceJitter + ((faceSeed >> 16) & 0xff) / 255f * faceJitter * 2f;
            return new Color(Mathf.Clamp(c.R * k, 0f, 1f), Mathf.Clamp(c.G * k, 0f, 1f), Mathf.Clamp(c.B * k, 0f, 1f), 1f);
        }

        /// <summary>삼각형 하나 (밖에서 볼 때 a → b → c 가 반시계). Godot 은 시계 방향이 앞면이라 뒤집어 넣는다.</summary>
        void Tri(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            var n = (b - a).Cross(c - a);
            if (n.LengthSquared() < 1e-12f) return;
            n = n.Normalized();
            positions.Add(a); positions.Add(c); positions.Add(b);
            for (int i = 0; i < 3; i++) { normals.Add(n); colors.Add(color); uvs.Add(new Vector2(swing, pivotY)); uv2s.Add(new Vector2(arm, 0f)); }
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            var shaded = Shade(color);
            Tri(a, b, c, shaded);
            Tri(a, c, d, shaded);
        }

        /// <summary>상자: 아래 사각형(가운데 x, z / 폭 w, 깊이 d)에서 위 사각형으로. top 배율로 위를 좁히거나 넓힌다.</summary>
        public LowPolyMesh Box(float cx, float y0, float y1, float cz, float w, float d, Color color, float top = 1f, float topShiftZ = 0f)
        {
            float hw = w * 0.5f, hd = d * 0.5f, tw = hw * top, td = hd * top;
            var b0 = new Vector3(cx - hw, y0, cz - hd); var b1 = new Vector3(cx + hw, y0, cz - hd);
            var b2 = new Vector3(cx + hw, y0, cz + hd); var b3 = new Vector3(cx - hw, y0, cz + hd);
            var t0 = new Vector3(cx - tw, y1, cz - td + topShiftZ); var t1 = new Vector3(cx + tw, y1, cz - td + topShiftZ);
            var t2 = new Vector3(cx + tw, y1, cz + td + topShiftZ); var t3 = new Vector3(cx - tw, y1, cz + td + topShiftZ);
            Quad(b3, b2, t2, t3, color);   // 앞 (+z)
            Quad(b1, b0, t0, t1, color);   // 뒤
            Quad(b2, b1, t1, t2, color);   // 오른쪽
            Quad(b0, b3, t3, t0, color);   // 왼쪽
            Quad(t3, t2, t1, t0, color);   // 위
            Quad(b0, b1, b2, b3, color);   // 아래
            return this;
        }

        /// <summary>각기둥·원뿔대: 아래 반지름 r0 에서 위 반지름 r1 로 (r1 = 0 이면 뿔).</summary>
        public LowPolyMesh Prism(float cx, float cz, float y0, float y1, float r0, float r1, int sides, Color color, float turn = 0f)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = turn + Mathf.Tau * i / sides, a1 = turn + Mathf.Tau * (i + 1) / sides;
                var d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)); var d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                var c0 = new Vector3(cx, y0, cz); var c1 = new Vector3(cx, y1, cz);
                var p0 = c0 + d0 * r0; var p1 = c0 + d1 * r0; var q0 = c1 + d0 * r1; var q1 = c1 + d1 * r1;
                var shaded = Shade(color);
                Tri(p0, p1, q1, shaded);
                if (r1 > 0.0001f) { Tri(p0, q1, q0, shaded); Tri(c1, q0, q1, Shade(color)); }
            }
            return this;
        }

        // 정이십면체 (덩어리: 나뭇잎 뭉치, 바위)
        static readonly Vector3[] IcoPoints = BuildIcoPoints();
        static readonly int[] IcoFaces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        static Vector3[] BuildIcoPoints()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var p = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < p.Length; i++) p[i] = p[i].Normalized();
            return p;
        }

        /// <summary>울퉁불퉁한 덩어리: 가운데 c, 반지름 (rx, ry, rz), 꼭짓점을 jitter 만큼 흔든다. 바닥 아래(y &lt; floor)는 눌러 평평하게.</summary>
        public LowPolyMesh Blob(Vector3 c, float rx, float ry, float rz, Color color, int seed, float jitter = 0.18f, float floor = float.NegativeInfinity)
        {
            var rng = new Random(seed);
            var pts = new Vector3[IcoPoints.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                float k = 1f + ((float)rng.NextDouble() * 2f - 1f) * jitter;
                var p = IcoPoints[i] * k;
                pts[i] = new Vector3(c.X + p.X * rx, Mathf.Max(floor, c.Y + p.Y * ry), c.Z + p.Z * rz);
            }
            for (int f = 0; f < IcoFaces.Length; f += 3)
                Tri(pts[IcoFaces[f]], pts[IcoFaces[f + 1]], pts[IcoFaces[f + 2]], Shade(color));
            return this;
        }

        /// <summary>얇은 판 (양면): 풀잎, 깃발.</summary>
        public LowPolyMesh Blade(Vector3 a, Vector3 b, Vector3 tip, Color color)
        {
            var shaded = Shade(color);
            Tri(a, b, tip, shaded);
            Tri(b, a, tip, shaded);
            return this;
        }

        // ───────────────────────── 둥근 모양 (카툰 3D, G21) ─────────────────────────
        // 법선을 꼭짓점마다 부드럽게 줘서 툰 셰이딩의 명암 경계가 곡선으로 떨어진다. 면 수는 낮게 둔다(멀리서 보는 작은 캐릭터).

        // 삼각형 하나, 꼭짓점마다 법선 (밖에서 볼 때 a → b → c 가 반시계)
        void TriS(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Color color)
        {
            if ((b - a).Cross(c - a).LengthSquared() < 1e-12f) return;
            positions.Add(a); positions.Add(c); positions.Add(b);
            normals.Add(na); normals.Add(nc); normals.Add(nb);
            for (int i = 0; i < 3; i++) { colors.Add(color); uvs.Add(new Vector2(swing, pivotY)); uv2s.Add(new Vector2(arm, 0f)); }
        }

        /// <summary>회전체: 옆모습(높이 y 가 커지는 순서의 (y, 반지름) 목록)을 xf 의 y 축 둘레로 돌린다. sx, sz = 가로·앞뒤 배율(타원), 반지름 0 인 끝은 뾰족하게 닫힌다.</summary>
        public LowPolyMesh Lathe(Transform3D xf, (float y, float r)[] prof, float sx, float sz, int sides, Color color)
        {
            int n = prof.Length;
            float s = (sx + sz) * 0.5f;
            var nr = new float[n]; var ny = new float[n];
            for (int i = 0; i < n; i++)
            {
                var prev = prof[Math.Max(i - 1, 0)]; var next = prof[Math.Min(i + 1, n - 1)];
                float dy = next.y - prev.y, dr = (next.r - prev.r) * s;
                float len = Mathf.Sqrt(dy * dy + dr * dr);
                if (len < 1e-6f) { nr[i] = 1f; ny[i] = 0f; } else { nr[i] = dy / len; ny[i] = -dr / len; }
                if (prof[i].r < 1e-5f) { nr[i] = 0f; ny[i] = i == 0 ? -1f : 1f; }
            }
            for (int j = 0; j < sides; j++)
            {
                float a0 = Mathf.Tau * j / sides, a1 = Mathf.Tau * (j + 1) / sides;
                float s0 = Mathf.Sin(a0), c0 = Mathf.Cos(a0), s1 = Mathf.Sin(a1), c1 = Mathf.Cos(a1);
                for (int i = 0; i + 1 < n; i++)
                {
                    Vector3 P(int k, float sn, float cs) => xf * new Vector3(prof[k].r * sx * sn, prof[k].y, prof[k].r * sz * cs);
                    Vector3 N(int k, float sn, float cs) => (xf.Basis * new Vector3(nr[k] * sn * s / sx, ny[k], nr[k] * cs * s / sz)).Normalized();
                    var p00 = P(i, s0, c0); var p01 = P(i, s1, c1); var p11 = P(i + 1, s1, c1); var p10 = P(i + 1, s0, c0);
                    var n00 = N(i, s0, c0); var n01 = N(i, s1, c1); var n11 = N(i + 1, s1, c1); var n10 = N(i + 1, s0, c0);
                    TriS(p00, p01, p11, n00, n01, n11, color);
                    TriS(p00, p11, p10, n00, n11, n10, color);
                }
            }
            return this;
        }

        /// <summary>공·타원체: 가운데 c, 반지름 (rx, ry, rz).</summary>
        public LowPolyMesh Ball(Vector3 c, float rx, float ry, float rz, Color color, int sides = 8, int rings = 5)
        {
            var prof = new (float, float)[rings + 1];
            for (int i = 0; i <= rings; i++) { float t = Mathf.Pi * i / rings; prof[i] = (-Mathf.Cos(t) * ry, i == 0 || i == rings ? 0f : Mathf.Sin(t)); }
            return Lathe(new Transform3D(Basis.Identity, c), prof, rx, rz, sides, color);
        }

        /// <summary>덮개(머리카락, 투구, 지붕): 타원체의 위쪽만 — 꼭대기에서 cover 도(90 = 반구)까지. tilt 만큼 뒤(-z)로 기울인다.</summary>
        public LowPolyMesh Dome(Vector3 c, float rx, float ry, float rz, Color color, float cover = 100f, float tilt = 0f, int sides = 8, int rings = 4)
        {
            var prof = new (float, float)[rings + 1];
            float max = cover * Mathf.Pi / 180f;
            for (int i = 0; i <= rings; i++) { float t = max * (1f - (float)i / rings); prof[i] = (Mathf.Cos(t) * ry, i == rings ? 0f : Mathf.Sin(t)); }
            return Lathe(new Transform3D(new Basis(Vector3.Right, -tilt), c), prof, rx, rz, sides, color);
        }

        /// <summary>알약(끝이 둥근 막대): a 에서 b 로, 반지름 r0 → r1. 팔다리, 자루, 수염, 깃.</summary>
        public LowPolyMesh Pill(Vector3 a, Vector3 b, float r0, float r1, Color color, int sides = 6, float flatten = 1f)
        {
            var d = b - a; float len = d.Length();
            if (len < 1e-5f) return this;
            d /= len;
            var bx = Mathf.Abs(d.Y) > 0.99f ? Vector3.Right : Vector3.Up.Cross(d).Normalized();
            var bz = bx.Cross(d);
            float c0 = Mathf.Min(r0, len * 0.5f), c1 = Mathf.Min(r1, len * 0.5f);
            var prof = new (float, float)[] { (0f, 0f), (c0 * 0.3f, r0 * 0.72f), (c0, r0), (len - c1, r1), (len - c1 * 0.3f, r1 * 0.72f), (len, 0f) };
            return Lathe(new Transform3D(new Basis(bx, d, bz), a), prof, 1f, flatten, sides, color);
        }

        // ───────────────────────── 사람·말 ─────────────────────────

        public struct Palette
        {
            public Color Skin, Hair, Torso, Legs, Accent, Mount;
        }

        public enum Outfit { Plain, Archer, Elite, General }

        /// <summary>장수의 특징 (얼굴·머리 장식). 적 병사는 None: 눈만 있다.</summary>
        public enum Feature { None, LiuBei, GuanYu, ZhangFei, CaoCao, LvBu }

        static Color Darken(Color c, float k) => new Color(c.R * k, c.G * k, c.B * k, 1f);
        static readonly Color Ink = new Color(0.10f, 0.08f, 0.09f);        // 눈, 수염
        static readonly Color Gold = new Color(0.95f, 0.76f, 0.22f);

        /// <summary>서 있는 사람 (키 1, 머리가 키의 40% 인 2.5등신). 다리·팔이 걸을 때 엇갈려 흔들린다.</summary>
        public static ArrayMesh Humanoid(Palette p, Outfit outfit, Feature feature = Feature.None)
        {
            var m = new LowPolyMesh();
            AddPerson(m, p, outfit, feature, 0f, 0f, true);
            return m.Build();
        }

        /// <summary>말 탄 사람 (전체 높이 1.5 쯤, 말 길이 1.2). 말 다리 네 개가 엇갈려 흔들린다.</summary>
        public static ArrayMesh Rider(Palette p, Outfit outfit, Feature feature = Feature.None)
        {
            var m = new LowPolyMesh();
            var horse = p.Mount; var dark = Darken(horse, 0.55f);
            // 말 다리 (앞뒤 엇갈림) + 발굽
            float legTop = 0.44f;
            void Leg(float x, float z, float sign)
            {
                m.Limb(sign, legTop).Pill(new Vector3(x, 0.06f, z), new Vector3(x, legTop + 0.04f, z), 0.05f, 0.065f, horse);
                m.Limb(sign, legTop).Ball(new Vector3(x, 0.045f, z + 0.01f), 0.062f, 0.05f, 0.07f, dark, 6, 3);
            }
            Leg(-0.11f, 0.30f, 1f); Leg(0.11f, 0.30f, -1f); Leg(-0.11f, -0.34f, -1f); Leg(0.11f, -0.34f, 1f);
            // 몸통(통통한 타원체), 목, 머리, 귀, 갈기, 꼬리
            m.Body().Ball(new Vector3(0f, 0.57f, -0.02f), 0.19f, 0.20f, 0.47f, horse, 8, 6);
            m.Pill(new Vector3(0f, 0.62f, 0.32f), new Vector3(0f, 1.00f, 0.50f), 0.13f, 0.10f, horse);
            m.Pill(new Vector3(0f, 1.02f, 0.46f), new Vector3(0f, 0.90f, 0.76f), 0.115f, 0.08f, horse);
            m.Pill(new Vector3(-0.06f, 1.08f, 0.47f), new Vector3(-0.08f, 1.18f, 0.45f), 0.03f, 0.012f, horse, 5);
            m.Pill(new Vector3(0.06f, 1.08f, 0.47f), new Vector3(0.08f, 1.18f, 0.45f), 0.03f, 0.012f, horse, 5);
            m.Pill(new Vector3(0f, 1.10f, 0.42f), new Vector3(0f, 0.70f, 0.22f), 0.05f, 0.04f, dark, 5);         // 갈기
            m.Pill(new Vector3(0f, 0.66f, -0.46f), new Vector3(0f, 0.28f, -0.60f), 0.045f, 0.07f, dark, 5);      // 꼬리
            m.Ball(new Vector3(-0.07f, 0.98f, 0.66f), 0.018f, 0.026f, 0.018f, Ink, 5, 3);                        // 말 눈
            m.Ball(new Vector3(0.07f, 0.98f, 0.66f), 0.018f, 0.026f, 0.018f, Ink, 5, 3);
            m.Ball(new Vector3(0f, 0.75f, -0.02f), 0.205f, 0.06f, 0.24f, p.Accent, 8, 4);                       // 안장
            AddPerson(m, p, outfit, feature, 0.50f, -0.04f, false);
            return m.Build();
        }

        // baseY 위에 사람을 세운다. legs = false 면 다리를 짧게 옆으로(말 탄 자세) 흔들지 않는다
        static void AddPerson(LowPolyMesh m, Palette p, Outfit outfit, Feature feature, float baseY, float cz, bool legs)
        {
            float hip = baseY + 0.24f, shoulder = baseY + 0.56f, headY = baseY + 0.80f, headTop = baseY + 1.0f;
            const float headR = 0.215f, headRy = 0.20f, headRz = 0.20f;
            var legColor = p.Legs; var boots = Darken(p.Legs, 0.55f);
            var sleeve = Darken(p.Torso, 0.88f);
            if (legs)
            {
                void Leg(float x, float sign)
                {
                    m.Limb(sign, hip).Pill(new Vector3(x, baseY + 0.05f, cz), new Vector3(x, hip + 0.04f, cz), 0.07f, 0.075f, legColor);
                    m.Limb(sign, hip).Ball(new Vector3(x, baseY + 0.05f, cz + 0.035f), 0.075f, 0.05f, 0.105f, boots, 6, 3);
                }
                Leg(-0.09f, 1f); Leg(0.09f, -1f);
            }
            else
            {
                m.Body().Pill(new Vector3(-0.20f, hip + 0.04f, cz + 0.02f), new Vector3(-0.225f, baseY - 0.02f, cz + 0.08f), 0.07f, 0.06f, legColor);
                m.Pill(new Vector3(0.20f, hip + 0.04f, cz + 0.02f), new Vector3(0.225f, baseY - 0.02f, cz + 0.08f), 0.07f, 0.06f, legColor);
                m.Ball(new Vector3(-0.225f, baseY - 0.03f, cz + 0.11f), 0.062f, 0.05f, 0.09f, boots, 6, 3);
                m.Ball(new Vector3(0.225f, baseY - 0.03f, cz + 0.11f), 0.062f, 0.05f, 0.09f, boots, 6, 3);
            }
            // 몸통: 아래가 넓은 옷자락(종 모양) + 허리띠
            float t0 = hip - 0.06f;
            var robe = new (float, float)[] { (t0, 0f), (t0 + 0.012f, 0.165f), (t0 + 0.10f, 0.185f), (t0 + 0.22f, 0.165f), (shoulder - 0.03f, 0.14f), (shoulder + 0.03f, 0.085f), (shoulder + 0.05f, 0f) };
            m.Body().Lathe(new Transform3D(Basis.Identity, new Vector3(0f, 0f, cz)), robe, 1f, 0.80f, 8, p.Torso);
            var belt = new (float, float)[] { (hip + 0.085f, 0.150f), (hip + 0.095f, 0.186f), (hip + 0.145f, 0.180f), (hip + 0.155f, 0.150f) };
            m.Lathe(new Transform3D(Basis.Identity, new Vector3(0f, 0f, cz)), belt, 1f, 0.80f, 8, p.Accent);
            // 팔 (다리와 반대로) + 손
            void Arm(float side, bool weapon)
            {
                var top = new Vector3(side * 0.175f, shoulder - 0.01f, cz); var hand = new Vector3(side * 0.235f, hip + 0.085f, cz + 0.01f);
                m.Limb(side < 0f ? -1f : 1f, shoulder - 0.02f); if (weapon) m.WeaponArm();
                m.Pill(top, hand, 0.062f, 0.055f, sleeve);
                m.Ball(hand + new Vector3(0f, -0.02f, 0f), 0.056f, 0.056f, 0.056f, p.Skin, 6, 4);
            }
            Arm(-1f, false); Arm(1f, true);
            // 머리(큰 공) + 머리카락/두건(위와 뒤를 덮는 기울인 덮개)
            var head = new Vector3(0f, headY, cz + 0.01f);
            m.Body().Ball(head, headR, headRy, headRz, p.Skin, 10, 6);
            m.Dome(head + new Vector3(0f, 0.012f, -0.012f), headR * 1.07f, headRy * 1.07f, headRz * 1.08f, p.Hair, 86f, 0.95f, 10, 4);   // 내려다보는 카메라에서 얼굴이 보이게 뒤로 많이 젖힌다
            // 눈: 세로로 긴 검은 타원 + 흰 반짝임 (정면이 어디인지 읽히게, 큰 머리의 인상)
            float eyeY = headY + 0.022f, eyeX = 0.082f;   // 내려다보는 카메라에 맞춰 눈을 조금 위에
            float FaceZ(float x, float y) { float k = 1f - (x * x) / (headR * headR) - ((y - headY) * (y - headY)) / (headRy * headRy); return head.Z + headRz * Mathf.Sqrt(Mathf.Max(0.05f, k)); }
            for (int side = -1; side <= 1; side += 2)
            {
                float z = FaceZ(eyeX, eyeY);
                m.Ball(new Vector3(side * eyeX, eyeY, z - 0.004f), 0.034f, 0.052f, 0.014f, Ink, 6, 4);
                m.Ball(new Vector3(side * eyeX + 0.010f, eyeY + 0.018f, z + 0.007f), 0.010f, 0.012f, 0.006f, new Color(1f, 1f, 1f), 5, 3);
            }
            // 눈썹: 장수만 (thick = 굵기, slant = 안쪽이 내려간 정도 — 성난 눈썹)
            void Brows(float thick, float slant, Color color)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float xi = 0.040f, xo = 0.118f, yi = eyeY + 0.088f - slant, yo = eyeY + 0.090f + slant * 0.6f;
                    m.Pill(new Vector3(side * xi, yi, FaceZ(xi, yi) + 0.004f), new Vector3(side * xo, yo, FaceZ(xo, yo) + 0.004f), thick, thick * 0.7f, color, 5);
                }
            }

            float chinY = headY - headRy * 0.80f, chinZ = FaceZ(0f, headY - headRy * 0.80f);
            switch (feature)
            {
                case Feature.GuanYu:    // 가슴까지 내려오는 긴 수염 + 콧수염, 두건은 그림의 녹색(머리 덮개 색)
                    Brows(0.014f, 0.012f, Ink);
                    m.Pill(new Vector3(0f, chinY + 0.035f, chinZ - 0.01f), new Vector3(0f, shoulder - 0.20f, cz + 0.165f), 0.085f, 0.022f, Ink, 6, 0.55f);
                    m.Pill(new Vector3(-0.012f, headY - 0.085f, FaceZ(0f, headY - 0.085f)), new Vector3(-0.105f, headY - 0.125f, FaceZ(0.105f, headY - 0.125f)), 0.018f, 0.008f, Ink, 5);
                    m.Pill(new Vector3(0.012f, headY - 0.085f, FaceZ(0f, headY - 0.085f)), new Vector3(0.105f, headY - 0.125f, FaceZ(0.105f, headY - 0.125f)), 0.018f, 0.008f, Ink, 5);
                    m.Pill(new Vector3(0f, headTop - 0.01f, cz - 0.03f), new Vector3(0f, headTop + 0.07f, cz - 0.07f), 0.055f, 0.04f, p.Hair, 6);   // 두건 매듭
                    break;
                case Feature.ZhangFei:  // 턱을 두른 뻗친 수염 + 굵고 성난 눈썹
                    Brows(0.024f, 0.030f, Ink);
                    m.Ball(new Vector3(0f, headY - 0.125f, cz + 0.045f), headR * 1.02f, 0.085f, headRz * 0.98f, Ink, 10, 4);
                    for (int i = -2; i <= 2; i++)
                    {
                        float a = i * 0.55f;
                        var from = new Vector3(Mathf.Sin(a) * 0.17f, headY - 0.15f, cz + 0.04f + Mathf.Cos(a) * 0.15f);
                        m.Pill(from, from + new Vector3(Mathf.Sin(a) * 0.075f, -0.075f, Mathf.Cos(a) * 0.075f), 0.04f, 0.006f, Ink, 5);
                    }
                    break;
                case Feature.LiuBei:    // 관(작은 금빛 상투관 + 가로 비녀)
                    Brows(0.012f, 0f, Darken(p.Hair, 0.7f));
                    m.Lathe(new Transform3D(Basis.Identity, new Vector3(0f, 0f, cz - 0.02f)), new (float, float)[] { (headTop - 0.03f, 0.075f), (headTop + 0.07f, 0.066f), (headTop + 0.085f, 0f) }, 1f, 1f, 8, Gold);
                    m.Pill(new Vector3(-0.15f, headTop + 0.025f, cz - 0.02f), new Vector3(0.15f, headTop + 0.025f, cz - 0.02f), 0.014f, 0.014f, Gold, 5);
                    break;
                case Feature.CaoCao:    // 짧은 턱수염·콧수염 + 위가 평평한 검은 관(앞뒤로 긴 판)
                    Brows(0.014f, 0.016f, Ink);
                    m.Pill(new Vector3(0f, chinY + 0.03f, chinZ - 0.015f), new Vector3(0f, chinY - 0.055f, chinZ + 0.01f), 0.05f, 0.012f, Ink, 6, 0.6f);
                    m.Pill(new Vector3(-0.08f, headY - 0.095f, FaceZ(0.08f, headY - 0.095f)), new Vector3(0.08f, headY - 0.095f, FaceZ(0.08f, headY - 0.095f)), 0.014f, 0.014f, Ink, 5);
                    m.Lathe(new Transform3D(Basis.Identity, new Vector3(0f, 0f, cz - 0.02f)), new (float, float)[] { (headTop - 0.04f, 0.095f), (headTop + 0.06f, 0.085f), (headTop + 0.07f, 0f) }, 1f, 1f, 8, new Color(0.16f, 0.14f, 0.2f));
                    m.Box(0f, headTop + 0.07f, headTop + 0.095f, cz - 0.02f, 0.20f, 0.36f, new Color(0.16f, 0.14f, 0.2f));
                    m.Box(0f, headTop + 0.095f, headTop + 0.105f, cz - 0.02f, 0.06f, 0.37f, Gold);
                    break;
                case Feature.LvBu:      // 뒤로 길게 휜 꿩 깃 두 가닥 + 금빛 머리 장식
                    Brows(0.016f, 0.022f, Ink);
                    m.Ball(new Vector3(0f, headTop + 0.01f, cz - 0.01f), 0.06f, 0.05f, 0.06f, Gold, 6, 4);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var a0 = new Vector3(side * 0.04f, headTop + 0.02f, cz - 0.01f); var a1 = new Vector3(side * 0.15f, headTop + 0.30f, cz - 0.10f);
                        var a2 = new Vector3(side * 0.22f, headTop + 0.44f, cz - 0.34f); var a3 = new Vector3(side * 0.25f, headTop + 0.40f, cz - 0.56f);
                        var red = new Color(0.90f, 0.20f, 0.18f);
                        m.Pill(a0, a1, 0.020f, 0.024f, red, 5); m.Pill(a1, a2, 0.024f, 0.022f, red, 5); m.Pill(a2, a3, 0.022f, 0.006f, red, 5);
                    }
                    break;
            }

            switch (outfit)
            {
                case Outfit.Archer:   // 등에 비스듬히 멘 활과 화살통
                    m.Pill(new Vector3(0.14f, hip + 0.02f, cz - 0.17f), new Vector3(-0.10f, shoulder + 0.16f, cz - 0.17f), 0.02f, 0.02f, new Color(0.50f, 0.33f, 0.17f), 5);
                    m.Pill(new Vector3(-0.11f, hip + 0.10f, cz - 0.17f), new Vector3(-0.03f, shoulder + 0.03f, cz - 0.18f), 0.045f, 0.05f, Darken(p.Accent, 0.8f), 6);
                    break;
                case Outfit.Elite:    // 둥근 어깨 갑옷 + 투구 깃
                    m.Ball(new Vector3(-0.20f, shoulder + 0.01f, cz), 0.085f, 0.06f, 0.095f, p.Accent, 6, 4);
                    m.Ball(new Vector3(0.20f, shoulder + 0.01f, cz), 0.085f, 0.06f, 0.095f, p.Accent, 6, 4);
                    if (feature == Feature.None) m.Pill(new Vector3(0f, headTop - 0.01f, cz - 0.02f), new Vector3(0f, headTop + 0.11f, cz - 0.05f), 0.04f, 0.015f, p.Accent, 5);
                    break;
                case Outfit.General:  // 큰 어깨 갑옷 + 망토, 장수 특징이 없으면 긴 투구 깃
                    m.Ball(new Vector3(-0.20f, shoulder + 0.01f, cz), 0.085f, 0.06f, 0.095f, p.Accent, 6, 4);
                    m.Ball(new Vector3(0.20f, shoulder + 0.01f, cz), 0.085f, 0.06f, 0.095f, p.Accent, 6, 4);
                    if (feature == Feature.None) m.Pill(new Vector3(0f, headTop - 0.01f, cz - 0.03f), new Vector3(0f, headTop + 0.20f, cz - 0.12f), 0.045f, 0.012f, p.Accent, 5);
                    m.Ball(new Vector3(0f, hip + 0.13f, cz - 0.155f), 0.195f, 0.24f, 0.045f, Darken(p.Accent, 0.72f), 8, 4);
                    break;
            }
        }

        // ───────────────────────── 소품 (폭 1 x 높이 1) ─────────────────────────

        public enum PropKind { Tree, Pine, Palm, Willow, DeadTree, Bush, Grass, Flowers, Rock, Mound, Stump, Boat, Yurt, Banner }

        /// <summary>소품 모델. main = 주된 색(잎, 바위, 천), second = 둘째 색(줄기, 그늘), seed 로 모양이 조금씩 달라진다.</summary>
        public static ArrayMesh Prop(PropKind kind, Color main, Color second, int seed)
        {
            var m = new LowPolyMesh { faceSeed = seed * 7919 + 3 };
            var trunk = second; var dark = Darken(main, 0.78f); var light = new Color(Mathf.Min(1f, main.R * 1.12f), Mathf.Min(1f, main.G * 1.12f), Mathf.Min(1f, main.B * 1.1f));
            switch (kind)
            {
                case PropKind.Tree:
                    m.Prism(0f, 0f, 0f, 0.46f, 0.085f, 0.06f, 6, trunk);
                    m.Blob(new Vector3(0f, 0.62f, 0f), 0.44f, 0.30f, 0.44f, main, seed);
                    m.Blob(new Vector3(0.14f, 0.80f, 0.06f), 0.28f, 0.20f, 0.28f, light, seed + 1);
                    m.Blob(new Vector3(-0.20f, 0.56f, -0.10f), 0.24f, 0.18f, 0.24f, dark, seed + 2);
                    break;
                case PropKind.Pine:
                    m.Prism(0f, 0f, 0f, 0.28f, 0.07f, 0.055f, 6, trunk);
                    m.Prism(0f, 0f, 0.20f, 0.58f, 0.40f, 0f, 7, dark, 0.2f);
                    m.Prism(0f, 0f, 0.42f, 0.80f, 0.31f, 0f, 7, main, 0.6f);
                    m.Prism(0f, 0f, 0.64f, 1.0f, 0.21f, 0f, 7, light, 1.0f);
                    break;
                case PropKind.Palm:
                    m.Box(0f, 0f, 0.74f, 0f, 0.11f, 0.11f, trunk, 0.7f, 0.10f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Mathf.Tau * i / 6f + 0.3f;
                        var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)); var side = new Vector3(dir.Z, 0f, -dir.X) * 0.10f;
                        var root = new Vector3(0f, 0.76f, 0.10f);
                        m.Blade(root - side, root + side, root + dir * 0.46f + new Vector3(0f, -0.10f, 0f), i % 2 == 0 ? main : dark);
                        m.Blade(root - side * 0.6f + new Vector3(0f, 0.05f, 0f), root + side * 0.6f + new Vector3(0f, 0.05f, 0f), root + dir * 0.34f + new Vector3(0f, 0.16f, 0f), light);
                    }
                    break;
                case PropKind.Willow:
                    m.Prism(0f, 0f, 0f, 0.50f, 0.09f, 0.06f, 6, trunk);
                    m.Blob(new Vector3(0f, 0.70f, 0f), 0.42f, 0.24f, 0.42f, main, seed);
                    for (int i = 0; i < 7; i++)   // 늘어진 가지
                    {
                        float a = Mathf.Tau * i / 7f;
                        m.Box(Mathf.Sin(a) * 0.36f, 0.26f, 0.66f, Mathf.Cos(a) * 0.36f, 0.12f, 0.12f, i % 2 == 0 ? dark : light, 1.5f);
                    }
                    break;
                case PropKind.DeadTree:
                    m.Prism(0f, 0f, 0f, 0.62f, 0.08f, 0.04f, 5, main);
                    m.Box(0.10f, 0.40f, 0.86f, 0f, 0.05f, 0.05f, main, 0.5f, 0.10f);
                    m.Box(-0.12f, 0.34f, 0.74f, 0.02f, 0.05f, 0.05f, dark, 0.5f, -0.12f);
                    m.Box(0.02f, 0.58f, 1.0f, -0.04f, 0.04f, 0.04f, dark, 0.4f, -0.06f);
                    break;
                case PropKind.Bush:
                    m.Blob(new Vector3(0f, 0.34f, 0f), 0.46f, 0.38f, 0.42f, main, seed, 0.2f, 0f);
                    m.Blob(new Vector3(0.20f, 0.52f, 0.08f), 0.26f, 0.26f, 0.26f, light, seed + 1, 0.2f, 0f);
                    m.Blob(new Vector3(-0.24f, 0.30f, -0.06f), 0.24f, 0.26f, 0.24f, dark, seed + 2, 0.2f, 0f);
                    break;
                case PropKind.Grass:
                {
                    var rng = new Random(seed);
                    for (int i = 0; i < 9; i++)
                    {
                        float x = ((float)rng.NextDouble() - 0.5f) * 0.7f, z = ((float)rng.NextDouble() - 0.5f) * 0.5f;
                        float h = 0.55f + (float)rng.NextDouble() * 0.45f, lean = ((float)rng.NextDouble() - 0.5f) * 0.3f;
                        float a = (float)rng.NextDouble() * Mathf.Pi;
                        var side = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.07f;
                        var root = new Vector3(x, 0f, z);
                        m.Blade(root - side, root + side, root + new Vector3(lean, h, lean * 0.5f), i % 3 == 0 ? light : (i % 3 == 1 ? main : dark));
                    }
                    break;
                }
                case PropKind.Flowers:
                {
                    var rng = new Random(seed);
                    for (int i = 0; i < 7; i++)
                    {
                        float x = ((float)rng.NextDouble() - 0.5f) * 0.7f, z = ((float)rng.NextDouble() - 0.5f) * 0.5f, h = 0.45f + (float)rng.NextDouble() * 0.4f;
                        m.Box(x, 0f, h, z, 0.04f, 0.04f, second);
                        m.Prism(x, z, h - 0.02f, h + 0.16f, 0.11f, 0.05f, 5, i % 2 == 0 ? main : light, i);
                    }
                    break;
                }
                case PropKind.Rock:
                    m.Blob(new Vector3(0f, 0.36f, 0f), 0.48f, 0.56f, 0.40f, main, seed, 0.26f, 0f);
                    m.Blob(new Vector3(0.24f, 0.22f, 0.14f), 0.26f, 0.30f, 0.24f, dark, seed + 1, 0.26f, 0f);
                    break;
                case PropKind.Mound:
                    m.Prism(0f, 0f, 0f, 0.62f, 0.50f, 0.30f, 8, main, 0.2f);
                    m.Prism(0f, 0f, 0.62f, 1.0f, 0.30f, 0.12f, 7, light, 0.5f);
                    break;
                case PropKind.Stump:
                    m.Prism(0f, 0f, 0f, 0.80f, 0.44f, 0.36f, 7, main);
                    m.Prism(0f, 0f, 0.80f, 0.86f, 0.33f, 0.30f, 7, light);
                    break;
                case PropKind.Boat:
                    m.Box(0f, 0.10f, 0.52f, 0f, 0.92f, 0.34f, main, 1.08f);
                    m.Box(0f, 0.40f, 0.54f, 0f, 0.80f, 0.24f, dark);
                    m.Box(0.08f, 0.52f, 1.0f, 0f, 0.05f, 0.05f, second);
                    break;
                case PropKind.Yurt:
                    m.Prism(0f, 0f, 0f, 0.56f, 0.50f, 0.48f, 10, main);
                    m.Prism(0f, 0f, 0.56f, 0.96f, 0.52f, 0.08f, 10, light);
                    m.Box(0f, 0f, 0.40f, 0.47f, 0.20f, 0.06f, second);   // 문
                    break;
                case PropKind.Banner:
                    m.Box(0f, 0f, 1.0f, 0f, 0.07f, 0.07f, second);
                    m.Box(0.24f, 0.52f, 0.96f, 0f, 0.42f, 0.03f, main);
                    m.Prism(0f, 0f, 1.0f, 1.08f, 0.08f, 0f, 4, light);
                    break;
            }
            return m.Build();
        }

        // ───────────────────────── 무기·탄환·이펙트 (3D 전환 3단계) ─────────────────────────

        /// <summary>이펙트용 사각형: 면 밝기를 흔들지 않고(색은 인스턴스가 정한다) UV.y 에 옅어지는 정도를 넣는다 (a, b 쪽 fadeA / c, d 쪽 fadeB). 재질이 양면이라 한 면만.</summary>
        void RawQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float fadeA, float fadeB)
        {
            positions.Add(a); positions.Add(b); positions.Add(c); positions.Add(a); positions.Add(c); positions.Add(d);
            var n = (b - a).Cross(c - a); n = n.LengthSquared() > 1e-12f ? n.Normalized() : Vector3.Up;
            float[] fade = { fadeA, fadeA, fadeB, fadeA, fadeB, fadeB };
            for (int i = 0; i < 6; i++) { normals.Add(n); colors.Add(color); uvs.Add(new Vector2(swing, fade[i])); uv2s.Add(Vector2.Zero); }
        }

        public enum WeaponKind { Sword, Spear, Glaive, Halberd }

        /// <summary>손에 든 무기: 손잡이 끝이 원점, +y 로 길이 1. blade = 날 색, shaft = 자루 색.</summary>
        public static ArrayMesh HeldWeapon(WeaponKind kind, Color blade, Color shaft, Color accent)
        {
            var m = new LowPolyMesh();
            switch (kind)
            {
                case WeaponKind.Sword:
                    m.Pill(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.23f, 0f), 0.032f, 0.032f, shaft, 6);
                    m.Box(0f, 0.20f, 0.265f, 0f, 0.30f, 0.085f, accent);
                    m.Box(0f, 0.265f, 0.84f, 0f, 0.15f, 0.045f, blade);
                    m.Box(0f, 0.84f, 1.0f, 0f, 0.15f, 0.045f, blade, 0.05f);
                    break;
                case WeaponKind.Spear:
                    m.Pill(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.78f, 0f), 0.026f, 0.026f, shaft, 6);
                    m.Ball(new Vector3(0f, 0.765f, 0f), 0.07f, 0.035f, 0.07f, accent, 6, 4);
                    m.Box(0f, 0.78f, 0.86f, 0f, 0.07f, 0.045f, blade, 1.9f);
                    m.Box(0f, 0.86f, 1.0f, 0f, 0.133f, 0.045f, blade, 0.05f);
                    break;
                case WeaponKind.Glaive:   // 언월도: 긴 자루 + 한쪽으로 휜 넓은 날
                    m.Pill(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.66f, 0f), 0.028f, 0.028f, shaft, 6);
                    m.Ball(new Vector3(0f, 0.65f, 0f), 0.075f, 0.04f, 0.075f, accent, 6, 4);
                    m.Box(0.05f, 0.67f, 0.90f, 0f, 0.21f, 0.045f, blade, 1.15f);
                    m.Box(0.075f, 0.90f, 1.0f, 0f, 0.24f, 0.045f, blade, 0.1f);
                    break;
                case WeaponKind.Halberd:  // 극: 창날 + 옆의 초승달 날
                    m.Pill(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.80f, 0f), 0.028f, 0.028f, shaft, 6);
                    m.Box(0f, 0.78f, 1.0f, 0f, 0.11f, 0.045f, blade, 0.05f);
                    m.Box(0.13f, 0.64f, 0.87f, 0f, 0.13f, 0.04f, blade, 0.6f);
                    m.Box(0.06f, 0.725f, 0.785f, 0f, 0.17f, 0.05f, accent);
                    m.Box(-0.09f, 0.69f, 0.81f, 0f, 0.09f, 0.04f, blade, 0.4f);
                    break;
            }
            return m.Build();
        }

        // x 방향으로 긴 상자 (x0 → x1), 끝 배율 top
        LowPolyMesh BoxX(float x0, float x1, float h, float d, Color color, float top = 1f)
        {
            float hh = h * 0.5f, hd = d * 0.5f, th = hh * top, td = hd * top;
            var a0 = new Vector3(x0, -hh, -hd); var a1 = new Vector3(x0, hh, -hd); var a2 = new Vector3(x0, hh, hd); var a3 = new Vector3(x0, -hh, hd);
            var b0 = new Vector3(x1, -th, -td); var b1 = new Vector3(x1, th, -td); var b2 = new Vector3(x1, th, td); var b3 = new Vector3(x1, -th, td);
            Quad(a3, b3, b2, a2, color); Quad(b0, a0, a1, b1, color); Quad(a2, b2, b1, a1, color); Quad(a0, b0, b3, a3, color);
            Quad(b3, b0, b1, b2, color); Quad(a0, a3, a2, a1, color);
            return this;
        }

        /// <summary>화살: +x 로 날아간다, 길이 1, 가운데가 원점.</summary>
        public static ArrayMesh Arrow()
        {
            var m = new LowPolyMesh();
            var wood = new Color(0.62f, 0.45f, 0.26f); var steel = new Color(0.86f, 0.88f, 0.92f); var feather = new Color(0.95f, 0.95f, 0.9f);
            m.BoxX(-0.42f, 0.30f, 0.035f, 0.035f, wood);
            m.BoxX(0.28f, 0.5f, 0.11f, 0.05f, steel, 0.05f);
            m.BoxX(-0.5f, -0.30f, 0.16f, 0.02f, feather);
            m.BoxX(-0.5f, -0.30f, 0.02f, 0.16f, feather);
            return m.Build();
        }

        /// <summary>팔면체 (보석, 탄환): 반지름 0.5, 가운데가 원점.</summary>
        public static ArrayMesh Gem(float stretchY = 1.3f)
        {
            var m = new LowPolyMesh();
            var white = new Color(1f, 1f, 1f);
            var top = new Vector3(0f, 0.5f * stretchY, 0f); var bottom = new Vector3(0f, -0.5f * stretchY, 0f);
            for (int i = 0; i < 4; i++)
            {
                float a0 = Mathf.Tau * i / 4f, a1 = Mathf.Tau * (i + 1) / 4f;
                var p0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * 0.5f; var p1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * 0.5f;
                m.Tri(p0, p1, top, m.Shade(white));
                m.Tri(p1, p0, bottom, m.Shade(new Color(0.78f, 0.78f, 0.78f)));
            }
            return m.Build();
        }

        /// <summary>정육면체 (타격 입자): 한 변 1, 가운데가 원점.</summary>
        public static ArrayMesh Cube()
        {
            var m = new LowPolyMesh();
            m.Box(0f, -0.5f, 0.5f, 0f, 1f, 1f, new Color(1f, 1f, 1f));
            return m.Build();
        }

        /// <summary>도는 칼날 (회전 도끼·비도): xz 평면에 누운 날 넷, 지름 1.</summary>
        public static ArrayMesh SpinBlade()
        {
            var m = new LowPolyMesh();
            var steel = new Color(0.88f, 0.9f, 0.95f); var hub = new Color(0.45f, 0.32f, 0.2f);
            m.Prism(0f, 0f, -0.05f, 0.05f, 0.12f, 0.12f, 6, hub);
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.Tau * i / 4f, b = a + 0.9f;
                var p0 = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.1f;
                var p1 = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.5f;
                var p2 = new Vector3(Mathf.Sin(b), 0f, Mathf.Cos(b)) * 0.34f;
                var up = new Vector3(0f, 0.03f, 0f);
                var c = m.Shade(steel);
                m.Tri(p0 + up, p1, p2 + up, c); m.Tri(p1, p0 + up, p2 + up, c);
                m.Tri(p0 - up, p2 - up, p1, c); m.Tri(p2 - up, p0 - up, p1, c);
            }
            return m.Build();
        }

        /// <summary>충격 고리 (범위 공격, 착탄): 지름 1, 낮은 벽이 위로 옅어진다 + 바닥의 얇은 띠.</summary>
        public static ArrayMesh Ring()
        {
            var m = new LowPolyMesh();
            var white = new Color(1f, 1f, 1f, 1f);
            const int sides = 20;
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.Tau * i / sides, a1 = Mathf.Tau * (i + 1) / sides;
                var d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)); var d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                var up = new Vector3(0f, 0.16f, 0f); var lift = new Vector3(0f, 0.02f, 0f);
                m.RawQuad(d0 * 0.5f + lift, d1 * 0.5f + lift, d1 * 0.52f + up, d0 * 0.52f + up, white, 0.15f, 1f);         // 벽
                m.RawQuad(d0 * 0.36f + lift, d1 * 0.36f + lift, d1 * 0.5f + lift, d0 * 0.5f + lift, white, 0.85f, 0.3f);   // 바닥 띠
            }
            return m.Build();
        }

        /// <summary>반원 검기 (베기): +x 로 볼록한 ±75도 호, 반지름 1, 가운데가 두껍고 양 끝이 얇은 세운 날.</summary>
        /// <summary>물결: 물 위에 살짝 솟은 고리 (지름 1). 안팎 가장자리로 갈수록 옅어진다.</summary>
        public static ArrayMesh Ripple()
        {
            var m = new LowPolyMesh();
            var white = new Color(1f, 1f, 1f, 1f);
            const int sides = 16;
            var up = new Vector3(0f, 0.045f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.Tau * i / sides, a1 = Mathf.Tau * (i + 1) / sides;
                var d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)); var d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                m.RawQuad(d0 * 0.36f, d1 * 0.36f, d1 * 0.44f + up, d0 * 0.44f + up, white, 1f, 0.1f);   // 안쪽 비탈
                m.RawQuad(d0 * 0.44f + up, d1 * 0.44f + up, d1 * 0.5f, d0 * 0.5f, white, 0.1f, 1f);     // 바깥 비탈
            }
            return m.Build();
        }

        public static ArrayMesh Crescent()
        {
            var m = new LowPolyMesh();
            var white = new Color(1f, 1f, 1f, 1f);
            const int steps = 10; const float half = 75f * Mathf.Pi / 180f;
            for (int i = 0; i < steps; i++)
            {
                float a0 = -half + 2f * half * i / steps, a1 = -half + 2f * half * (i + 1) / steps;
                float t0 = Mathf.Cos(a0 / half * Mathf.Pi * 0.5f), t1 = Mathf.Cos(a1 / half * Mathf.Pi * 0.5f);
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                float h0 = 0.08f + 0.42f * t0, h1 = 0.08f + 0.42f * t1;
                // 바깥 날(밝다): 세운 면 + 안쪽으로 옅어지는 누운 면
                m.RawQuad(d0 - new Vector3(0f, h0 * 0.5f, 0f), d1 - new Vector3(0f, h1 * 0.5f, 0f), d1 + new Vector3(0f, h1 * 0.5f, 0f), d0 + new Vector3(0f, h0 * 0.5f, 0f), white, 0.25f, 0.25f);
                m.RawQuad(d0, d1, d1 * (1f - 0.3f * t1), d0 * (1f - 0.3f * t0), white, 0f, 1f);
            }
            return m.Build();
        }

        /// <summary>뾰족한 검기 (찌르기): 촉 끝이 +x 의 1, 뒤로 벌어지는 두 날개.</summary>
        public static ArrayMesh Wedge()
        {
            var m = new LowPolyMesh();
            var white = new Color(1f, 1f, 1f, 1f);
            var tip = new Vector3(1f, 0f, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                var wing = new Vector3(-0.1f, 0f, side * 0.62f);
                var inner = new Vector3(0.45f, 0f, 0f);
                m.RawQuad(tip + new Vector3(0f, -0.16f, 0f), wing, wing, tip + new Vector3(0f, 0.16f, 0f), white, 0.1f, 0.9f);   // 세운 면
                m.RawQuad(tip, wing, inner + new Vector3(0f, 0f, side * 0.1f), tip, white, 0f, 1f);                                 // 누운 면
            }
            return m.Build();
        }

        /// <summary>번개 줄기: 바닥이 원점, +y 로 높이 1 의 꺾인 줄기 (십자로 두 장).</summary>
        public static ArrayMesh BoltZig()
        {
            var m = new LowPolyMesh();
            var white = new Color(1f, 1f, 1f, 1f);
            float[] offs = { 0f, 0.09f, -0.07f, 0.10f, -0.06f, 0.05f, 0f };
            for (int i = 0; i + 1 < offs.Length; i++)
            {
                float y0 = (float)i / (offs.Length - 1), y1 = (float)(i + 1) / (offs.Length - 1);
                float w0 = 0.035f + 0.05f * (1f - y0), w1 = 0.035f + 0.05f * (1f - y1);
                m.RawQuad(new Vector3(offs[i] - w0, y0, 0f), new Vector3(offs[i] + w0, y0, 0f), new Vector3(offs[i + 1] + w1, y1, 0f), new Vector3(offs[i + 1] - w1, y1, 0f), white, 0f, 0f);
                m.RawQuad(new Vector3(offs[i], y0, -w0), new Vector3(offs[i], y0, w0), new Vector3(offs[i + 1], y1, w1), new Vector3(offs[i + 1], y1, -w1), white, 0f, 0f);
            }
            return m.Build();
        }

        /// <summary>불길: 지름 1 안에 흩어진 불꽃 뿔들 (끝이 일렁인다) + 바닥의 고리.</summary>
        public static ArrayMesh Flames()
        {
            var m = new LowPolyMesh();
            var rng = new Random(11);
            var core = new Color(1f, 0.95f, 0.75f, 1f); var white = new Color(1f, 1f, 1f, 1f);
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Tau * i / 9f + (float)rng.NextDouble() * 0.5f, r = i == 0 ? 0f : 0.16f + (float)rng.NextDouble() * 0.26f;
                float cx = Mathf.Sin(a) * r, cz = Mathf.Cos(a) * r, h = 0.34f + (float)rng.NextDouble() * 0.3f, w = 0.07f + (float)rng.NextDouble() * 0.05f;
                var top = new Vector3(cx, h, cz);
                for (int k = 0; k < 4; k++)
                {
                    float b0 = Mathf.Tau * k / 4f, b1 = Mathf.Tau * (k + 1) / 4f;
                    var p0 = new Vector3(cx + Mathf.Sin(b0) * w, 0.02f, cz + Mathf.Cos(b0) * w); var p1 = new Vector3(cx + Mathf.Sin(b1) * w, 0.02f, cz + Mathf.Cos(b1) * w);
                    // 끝(위)만 일렁이게 UV.x 를 켠다
                    m.positions.Add(p0); m.positions.Add(p1); m.positions.Add(top);
                    for (int v = 0; v < 3; v++) { m.normals.Add(Vector3.Up); m.colors.Add(v == 2 ? white : core); m.uvs.Add(new Vector2(v == 2 ? 1f : 0f, v == 2 ? 0.75f : 0.05f)); m.uv2s.Add(Vector2.Zero); }   // uv2 가 빠져 불길 메시가 만들어지지 않았다 (G20 에서 고침)
                }
            }
            const int sides = 16;
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.Tau * i / sides, a1 = Mathf.Tau * (i + 1) / sides;
                var d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)); var d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                var lift = new Vector3(0f, 0.02f, 0f);
                m.RawQuad(d0 * 0.2f + lift, d1 * 0.2f + lift, d1 * 0.5f + lift, d0 * 0.5f + lift, white, 0.8f, 0.45f);
            }
            return m.Build();
        }
    }
}
