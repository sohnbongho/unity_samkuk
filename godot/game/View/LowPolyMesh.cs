using System;
using System.Collections.Generic;
using Godot;

namespace Samkuk.Game
{
    /// <summary>
    /// 코드로 만드는 로우폴리 모델 (3D 전환 1단계의 임시 모델). 도트 그림이 코드 생성 임시 그림인 것과 같은 자리다.
    /// 모델 좌표: 발이 원점, y 위, +z 가 앞(화면 아래쪽을 볼 때의 정면). 사람 모델은 키 1, 소품 모델은 폭 1 x 높이 1 로 만들고 인스턴스 배율로 키운다.
    /// 면마다 법선을 따로 둬(꼭짓점 공유 없음) 각진 면이 그대로 보이고, 면마다 밝기를 조금씩 달리해 덩어리가 읽히게 한다.
    /// 걷기: UV.x = 흔들 방향(±1), UV.y = 흔들 축 높이 (shaders/lowpoly.gdshader 가 읽는다).
    /// 진짜 모델(구매한 번들 등)로 바꿀 때는 <see cref="ModelMirror"/> 의 모델 고르기에서 이 생성기 대신 파일의 메시를 넘기면 된다.
    /// </summary>
    public sealed class LowPolyMesh
    {
        readonly List<Vector3> positions = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<Vector2> uvs = new List<Vector2>();
        int faceSeed = 17;

        // 지금 붙이는 부분의 흔들 값
        float swing, pivotY;

        public LowPolyMesh Limb(float swingSign, float pivot) { swing = swingSign; pivotY = pivot; return this; }
        public LowPolyMesh Body() { swing = 0f; pivotY = 0f; return this; }

        public ArrayMesh Build()
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = positions.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }

        // 면마다 밝기를 ±5% 흔든다 (결정적)
        Color Shade(Color c)
        {
            faceSeed = faceSeed * 1103515245 + 12345;
            float k = 0.95f + ((faceSeed >> 16) & 0xff) / 255f * 0.1f;
            return new Color(Mathf.Clamp(c.R * k, 0f, 1f), Mathf.Clamp(c.G * k, 0f, 1f), Mathf.Clamp(c.B * k, 0f, 1f), 1f);
        }

        /// <summary>삼각형 하나 (밖에서 볼 때 a → b → c 가 반시계). Godot 은 시계 방향이 앞면이라 뒤집어 넣는다.</summary>
        void Tri(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            var n = (b - a).Cross(c - a);
            if (n.LengthSquared() < 1e-12f) return;
            n = n.Normalized();
            positions.Add(a); positions.Add(c); positions.Add(b);
            for (int i = 0; i < 3; i++) { normals.Add(n); colors.Add(color); uvs.Add(new Vector2(swing, pivotY)); }
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

        // ───────────────────────── 사람·말 ─────────────────────────

        public struct Palette
        {
            public Color Skin, Hair, Torso, Legs, Accent, Mount;
        }

        public enum Outfit { Plain, Archer, Elite, General }

        static Color Darken(Color c, float k) => new Color(c.R * k, c.G * k, c.B * k, 1f);

        /// <summary>서 있는 사람 (키 1, 머리가 큰 3등신). 다리·팔이 걸을 때 엇갈려 흔들린다.</summary>
        public static ArrayMesh Humanoid(Palette p, Outfit outfit)
        {
            var m = new LowPolyMesh();
            AddPerson(m, p, outfit, 0f, 0f, true);
            return m.Build();
        }

        /// <summary>말 탄 사람 (전체 높이 1.25 쯤, 말 길이 1.1). 말 다리 네 개가 엇갈려 흔들린다.</summary>
        public static ArrayMesh Rider(Palette p, Outfit outfit)
        {
            var m = new LowPolyMesh();
            var horse = p.Mount; var dark = Darken(horse, 0.62f);
            // 말 다리 (앞뒤 엇갈림)
            float legTop = 0.42f;
            m.Limb(1f, legTop).Box(-0.11f, 0f, legTop, 0.30f, 0.09f, 0.10f, dark);
            m.Limb(-1f, legTop).Box(0.11f, 0f, legTop, 0.30f, 0.09f, 0.10f, dark);
            m.Limb(-1f, legTop).Box(-0.11f, 0f, legTop, -0.34f, 0.09f, 0.10f, dark);
            m.Limb(1f, legTop).Box(0.11f, 0f, legTop, -0.34f, 0.09f, 0.10f, dark);
            // 몸통, 목, 머리, 꼬리
            m.Body().Box(0f, 0.38f, 0.72f, -0.02f, 0.34f, 0.86f, horse, 0.9f);
            m.Box(0f, 0.62f, 0.98f, 0.40f, 0.18f, 0.22f, horse, 0.8f, 0.10f);
            m.Box(0f, 0.86f, 1.04f, 0.60f, 0.17f, 0.34f, horse, 0.85f);
            m.Box(0f, 0.98f, 1.10f, 0.50f, 0.20f, 0.06f, dark);               // 귀·갈기
            m.Box(0f, 0.30f, 0.70f, -0.50f, 0.07f, 0.07f, dark, 1.6f, 0.04f);  // 꼬리
            m.Box(0f, 0.70f, 0.75f, -0.02f, 0.38f, 0.40f, p.Accent);          // 안장
            AddPerson(m, p, outfit, 0.44f, -0.04f, false);
            return m.Build();
        }

        // baseY 위에 사람을 세운다. legs = false 면 다리를 짧게 옆으로(말 탄 자세) 흔들지 않는다
        static void AddPerson(LowPolyMesh m, Palette p, Outfit outfit, float baseY, float cz, bool legs)
        {
            float hip = baseY + 0.34f, shoulder = baseY + 0.66f, headTop = baseY + 1.0f;
            var legColor = p.Legs; var boots = Darken(p.Legs, 0.6f);
            if (legs)
            {
                m.Limb(1f, hip).Box(-0.095f, baseY, baseY + 0.10f, cz + 0.02f, 0.14f, 0.20f, boots);
                m.Limb(1f, hip).Box(-0.095f, baseY + 0.10f, hip + 0.02f, cz, 0.13f, 0.15f, legColor);
                m.Limb(-1f, hip).Box(0.095f, baseY, baseY + 0.10f, cz + 0.02f, 0.14f, 0.20f, boots);
                m.Limb(-1f, hip).Box(0.095f, baseY + 0.10f, hip + 0.02f, cz, 0.13f, 0.15f, legColor);
            }
            else
            {
                m.Body().Box(-0.21f, baseY + 0.02f, hip, cz + 0.04f, 0.10f, 0.16f, legColor);
                m.Box(0.21f, baseY + 0.02f, hip, cz + 0.04f, 0.10f, 0.16f, legColor);
            }
            // 몸통 (아래가 살짝 넓은 옷자락) + 허리띠
            m.Body().Box(0f, hip - 0.04f, shoulder, cz, 0.40f, 0.24f, p.Torso, 0.9f);
            m.Box(0f, hip + 0.04f, hip + 0.10f, cz, 0.42f, 0.26f, p.Accent);
            // 팔 (다리와 반대로)
            m.Limb(-1f, shoulder - 0.02f).Box(-0.255f, hip + 0.06f, shoulder, cz, 0.10f, 0.12f, Darken(p.Torso, 0.86f));
            m.Limb(-1f, shoulder - 0.02f).Box(-0.255f, hip + 0.02f, hip + 0.10f, cz, 0.09f, 0.10f, p.Skin);
            m.Limb(1f, shoulder - 0.02f).Box(0.255f, hip + 0.06f, shoulder, cz, 0.10f, 0.12f, Darken(p.Torso, 0.86f));
            m.Limb(1f, shoulder - 0.02f).Box(0.255f, hip + 0.02f, hip + 0.10f, cz, 0.09f, 0.10f, p.Skin);
            // 머리 + 머리카락/두건 (뒤와 위를 덮는다)
            m.Body().Box(0f, shoulder, headTop - 0.06f, cz + 0.01f, 0.32f, 0.28f, p.Skin, 0.94f);
            m.Box(0f, headTop - 0.14f, headTop, cz - 0.01f, 0.35f, 0.31f, p.Hair, 0.86f);
            m.Box(0f, shoulder + 0.02f, headTop - 0.10f, cz - 0.13f, 0.34f, 0.07f, p.Hair);
            // 눈 (정면이 어디인지 읽히게)
            var eye = new Color(0.08f, 0.07f, 0.07f);
            m.Box(-0.075f, shoulder + 0.11f, shoulder + 0.16f, cz + 0.155f, 0.045f, 0.012f, eye);
            m.Box(0.075f, shoulder + 0.11f, shoulder + 0.16f, cz + 0.155f, 0.045f, 0.012f, eye);

            switch (outfit)
            {
                case Outfit.Archer:   // 등에 멘 활과 화살통
                    m.Box(0.06f, hip + 0.02f, shoulder + 0.14f, cz - 0.16f, 0.05f, 0.04f, new Color(0.45f, 0.3f, 0.16f), 1f, -0.03f);
                    m.Box(-0.10f, hip + 0.10f, shoulder + 0.04f, cz - 0.15f, 0.09f, 0.07f, Darken(p.Accent, 0.8f));
                    break;
                case Outfit.Elite:    // 어깨 갑옷 + 투구 깃
                    m.Box(-0.26f, shoulder - 0.06f, shoulder + 0.04f, cz, 0.16f, 0.18f, p.Accent, 0.8f);
                    m.Box(0.26f, shoulder - 0.06f, shoulder + 0.04f, cz, 0.16f, 0.18f, p.Accent, 0.8f);
                    m.Box(0f, headTop, headTop + 0.10f, cz - 0.02f, 0.05f, 0.14f, p.Accent, 0.6f);
                    break;
                case Outfit.General:  // 어깨 갑옷 + 긴 투구 깃 + 망토
                    m.Box(-0.27f, shoulder - 0.07f, shoulder + 0.05f, cz, 0.18f, 0.20f, p.Accent, 0.8f);
                    m.Box(0.27f, shoulder - 0.07f, shoulder + 0.05f, cz, 0.18f, 0.20f, p.Accent, 0.8f);
                    m.Box(0f, headTop, headTop + 0.20f, cz - 0.04f, 0.05f, 0.10f, p.Accent, 0.5f, -0.08f);
                    m.Box(0f, hip - 0.10f, shoulder - 0.02f, cz - 0.15f, 0.40f, 0.04f, Darken(p.Accent, 0.75f), 0.8f, 0.02f);
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
    }
}
