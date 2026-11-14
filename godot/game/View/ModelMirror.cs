using System.Collections.Generic;
using Godot;
using Samkuk.Compat;
using Mathf = Samkuk.Compat.Mathf;
using Image = Godot.Image;
using GColor = Godot.Color;
using GVec3 = Godot.Vector3;
using CVec3 = Samkuk.Compat.Vector3;

namespace Samkuk.Game
{
    /// <summary>
    /// 3D 전환 1단계: 서 있는 도트 그림(장수·아군·적·지형 소품)을 로우폴리 3D 모델로 바꿔 그린다. 규칙 코드는 그대로다 —
    /// 규칙은 여전히 SpriteRenderer 에 걷기 시트의 칸을 넣고, 화면 층이 그 그림의 **파일 이름**으로 모델을 고르고
    /// **칸의 줄**(아래/위/왼쪽/오른쪽)과 움직인 방향으로 몸을 돌리고, 움직인 거리로 다리를 흔든다.
    /// 성능: 모델 종류마다 MultiMesh 하나(인스턴싱). 적 수백 마리도 종류 수만큼의 드로우 콜이고, 걷기는 뼈대 없이 꼭짓점 셰이더가 한다.
    /// 색은 걷기 시트·소품 그림에서 뽑는다(머리/얼굴/몸/다리 띠의 가장 흔한 색) → 그림을 바꾸면 모델 색도 따라온다.
    /// </summary>
    public sealed class ModelMirror
    {
        const int Stride = 20;               // 변환 12 + 색 4 + 사용자 값 4
        /// <summary>사람 모델의 키: 그림 속 몸 높이(40px/32)에 곱한다. 내려다보는 각도 때문에 납작해 보이는 만큼 조금 키운다.</summary>
        public const float CharacterScale = 1.3f;
        public const float PropHeightScale = 1.0f;
        const float TurnSpeed = 14f;         // 몸 돌리기 (라디안/초)
        const float StepPerUnit = 5.2f;      // 걸은 거리 1유닛당 다리 흔들기 위상
        const float SwingAngle = 0.62f;      // 다리 흔들 각 (라디안)

        sealed class Batch
        {
            public MultiMesh mesh;
            public MultiMeshInstance3D node;
            public float[] buffer = new float[0];
            public int count, capacity;
            public bool character;
            public float baseHeight = 1f;   // 인스턴스 배율 1 일 때의 모델 높이
        }

        sealed class State
        {
            public float yaw, phase, amplitude;
            public CVec3 lastPosition;
            public bool started;
            public int seen;
        }

        readonly Node3D parent;
        readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
        readonly Dictionary<string, Batch> byTexture = new Dictionary<string, Batch>();   // 그림 경로 → 묶음 (없으면 null 을 넣어 다시 찾지 않는다)
        readonly Dictionary<SpriteRenderer, State> states = new Dictionary<SpriteRenderer, State>();
        readonly List<SpriteRenderer> dead = new List<SpriteRenderer>();
        ShaderMaterial material;
        int frame;

        public int InstanceCount { get; private set; }
        public int BatchCount => batches.Count;

        public ModelMirror(Node3D parent) { this.parent = parent; }

        public void Begin()
        {
            frame++;
            foreach (var b in batches.Values) b.count = 0;
        }

        /// <summary>이 그림을 모델로 그렸으면 true (그러면 도트 그림은 숨긴다).</summary>
        public bool TryDraw(SpriteRenderer sr, Compat.Sprite sprite)
        {
            string path = sprite.path;
            if (path == null) return false;
            if (!byTexture.TryGetValue(path, out var batch)) byTexture[path] = batch = CreateBatch(path, sprite);
            if (batch == null) return false;

            var t = sr.transform;
            var pos = t.position;
            var scale = t.lossyScale;
            var color = sr.color;
            float yaw, swing = 0f, bob = 0f, sx, sy, sz;

            if (batch.character)
            {
                if (!states.TryGetValue(sr, out var state)) states[sr] = state = new State();
                state.seen = frame;
                // 바라보는 쪽: 걷기 시트의 줄(규칙 코드가 정한 4방향). 움직이는 방향이 그 줄과 맞으면 더 잘게 움직인 방향을 따른다
                float cell = sprite.rect.height;
                int row = sprite.texture != null && cell > 0f ? Mathf.RoundToInt((sprite.texture.height - sprite.rect.y - sprite.rect.height) / cell) : 0;
                float rowYaw = row == 1 ? Mathf.PI : row == 2 ? -Mathf.PI * 0.5f : row == 3 ? Mathf.PI * 0.5f : 0f;
                float target = rowYaw;
                float dt = Mathf.Max(0.0001f, Compat.Time.deltaTime);
                float moved = 0f;
                if (state.started)
                {
                    float dx = pos.x - state.lastPosition.x, dy = pos.y - state.lastPosition.y;
                    moved = Mathf.Sqrt(dx * dx + dy * dy);
                    if (moved > 3f) moved = 0f;   // 순간 이동(풀에서 다시 나옴, 부활)
                    else if (moved > 0.0005f)
                    {
                        float moveYaw = Mathf.Atan2(dx, -dy);   // Godot 의 +z 는 로직의 남쪽
                        if (Mathf.Abs(Mathf.DeltaAngle(moveYaw * Mathf.Rad2Deg, rowYaw * Mathf.Rad2Deg)) < 75f) target = moveYaw;
                    }
                }
                else { state.yaw = target; state.started = true; }
                state.lastPosition = pos;
                float diff = Mathf.DeltaAngle(state.yaw * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                state.yaw += Mathf.Clamp(diff, -TurnSpeed * dt, TurnSpeed * dt);
                // 걷기: 움직인 거리만큼 위상이 가고, 멈추면 흔들림이 잦아든다
                float size = Mathf.Max(0.3f, scale.y);
                state.phase += moved * StepPerUnit / size;
                float walking = moved / dt > 0.3f ? 1f : 0f;
                state.amplitude = Mathf.MoveTowards(state.amplitude, walking, dt * 8f);
                float s = Mathf.Sin(state.phase);
                swing = s * SwingAngle * state.amplitude;
                bob = Mathf.Abs(Mathf.Cos(state.phase)) * 0.035f * state.amplitude;
                yaw = state.yaw;
                // 그림 한 칸(48px)의 몸 높이 40px 를 모델 키로
                float body = sprite.rect.height / sprite.pixelsPerUnit * (40f / 48f) * CharacterScale / batch.baseHeight;
                sx = Mathf.Abs(scale.x) * body; sy = scale.y * body; sz = sx;
            }
            else
            {
                // 소품: 그림 폭·높이에 맞춘다. 자리마다 다른 방향(결정적)으로 돌려 같은 모델이 줄지어 보이지 않게 한다
                float w = sprite.rect.width / sprite.pixelsPerUnit * Mathf.Abs(scale.x);
                float h = sprite.rect.height / sprite.pixelsPerUnit * scale.y * PropHeightScale;
                int hash = Mathf.RoundToInt(pos.x * 13.7f) * 73856093 ^ Mathf.RoundToInt(pos.y * 11.3f) * 19349663;
                yaw = (hash & 1023) / 1023f * (Mathf.PI * 2f);
                sx = w; sy = h; sz = w;
                if (sr.flipX) sx = -sx;
            }

            if (batch.count >= batch.capacity) Grow(batch);
            int o = batch.count++ * Stride;
            var buffer = batch.buffer;
            float c = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
            // 3x4 행렬 (행 우선): 기저 X = (c, 0, -sn) * sx, Y = (0, sy, 0), Z = (sn, 0, c) * sz
            buffer[o] = c * sx; buffer[o + 1] = 0f; buffer[o + 2] = sn * sz; buffer[o + 3] = pos.x;
            buffer[o + 4] = 0f; buffer[o + 5] = sy; buffer[o + 6] = 0f; buffer[o + 7] = -pos.z;
            buffer[o + 8] = -sn * sx; buffer[o + 9] = 0f; buffer[o + 10] = c * sz; buffer[o + 11] = -pos.y;
            buffer[o + 12] = color.r; buffer[o + 13] = color.g; buffer[o + 14] = color.b; buffer[o + 15] = 1f;
            buffer[o + 16] = swing; buffer[o + 17] = bob; buffer[o + 18] = color.a; buffer[o + 19] = 0f;
            return true;
        }

        public void End()
        {
            int total = 0;
            foreach (var b in batches.Values)
            {
                total += b.count;
                b.node.Visible = b.count > 0;
                if (b.count == 0) continue;
                b.mesh.VisibleInstanceCount = b.count;
                b.mesh.Buffer = b.buffer;
            }
            InstanceCount = total;

            // 사라진 캐릭터의 상태를 가끔 치운다
            if (frame % 240 == 0)
            {
                dead.Clear();
                foreach (var kv in states) if (kv.Key.destroyed) dead.Add(kv.Key);
                foreach (var k in dead) states.Remove(k);
                dead.Clear();
                foreach (var kv in roles) if (kv.Key.destroyed) dead.Add(kv.Key);
                foreach (var k in dead) roles.Remove(k);
            }
        }

        public void Hide()
        {
            foreach (var b in batches.Values) b.node.Visible = false;
            InstanceCount = 0;
        }

        void Grow(Batch b)
        {
            int capacity = Mathf.Max(32, b.capacity * 2);
            var grown = new float[capacity * Stride];
            System.Array.Copy(b.buffer, grown, b.count * Stride);
            // 안 쓰는 칸도 유효한 행렬로 채운다
            for (int i = b.count; i < capacity; i++) { int o = i * Stride; grown[o] = 1f; grown[o + 5] = 1f; grown[o + 10] = 1f; grown[o + 18] = 1f; }
            b.buffer = grown;
            b.capacity = capacity;
            b.mesh.InstanceCount = capacity;
        }

        // ───────────────────────── 모델 고르기 ─────────────────────────

        // 그림 경로로 모델을 정한다. 모델로 바꾸지 않는 그림(무기, 이펙트, UI)은 null
        Batch CreateBatch(string path, Compat.Sprite sprite)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            // 걷기 시트가 없는 주인공(장수를 고르기 전의 기본 그림)도 사람 모델로
            bool hero = path.Contains("/HeroWalk/") || path.EndsWith("/sprites/Player.png"), enemy = path.Contains("/EnemyWalk/");
            if (!hero && !enemy && !(path.Contains("/Terrain/") && name.StartsWith("Prop_"))) return null;
            if (batches.TryGetValue(name, out var existing)) return existing;

            var texture = Textures.Of(sprite);
            var image = texture != null ? texture.GetImage() : null;
            if (image == null) return null;
            if (image.IsCompressed()) image.Decompress();

            ArrayMesh mesh;
            bool character = hero || enemy;
            if (character)
            {
                bool rider = name.Contains("Cavalry") || name.Contains("Boss_") || name.Contains("General");
                var outfit = name.Contains("Archer") || name.Contains("Crossbow") ? LowPolyMesh.Outfit.Archer
                    : hero || name.Contains("Boss_") || name.Contains("General") ? LowPolyMesh.Outfit.General
                    : name.Contains("Elite") || name.Contains("Infantry") || name.Contains("Cavalry") ? LowPolyMesh.Outfit.Elite
                    : LowPolyMesh.Outfit.Plain;
                var palette = SamplePalette(image, rider);
                mesh = rider ? LowPolyMesh.Rider(palette, outfit) : LowPolyMesh.Humanoid(palette, outfit);
            }
            else
            {
                if (!PropKindOf(name, out var kind)) return null;
                // 위쪽 절반의 흔한 색 = 잎·몸, 아래 가운데 = 줄기·그늘
                Bounds(image, 0, 0, image.GetWidth(), image.GetHeight(), out int bx, out int by, out int bw, out int bh);
                var main = Dominant(image, bx, by, bw, Mathf.Max(1, bh * 7 / 10), new GColor(0.3f, 0.5f, 0.25f));
                var second = Dominant(image, bx + bw * 3 / 8, by + bh * 75 / 100, Mathf.Max(1, bw / 4), Mathf.Max(1, bh / 4), new GColor(main.R * 0.6f, main.G * 0.55f, main.B * 0.5f));
                if (kind == LowPolyMesh.PropKind.Tree || kind == LowPolyMesh.PropKind.Pine || kind == LowPolyMesh.PropKind.Palm || kind == LowPolyMesh.PropKind.Willow)
                {
                    // 줄기는 잎과 구별되는 갈색 쪽이어야 한다: 아래쪽도 풀색이면 기본 나무색
                    if (second.G > second.R * 1.05f) second = new GColor(0.42f, 0.29f, 0.17f);
                }
                mesh = LowPolyMesh.Prop(kind, main, second, name.GetHashCode() & 0xffff);
            }

            if (material == null) material = new ShaderMaterial { Shader = GD.Load<Godot.Shader>("res://shaders/lowpoly.gdshader") };
            var multi = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                UseCustomData = true,
                Mesh = mesh,
            };
            var node = new MultiMeshInstance3D
            {
                Name = "Model_" + name,
                Multimesh = multi,
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
                ExtraCullMargin = 4096f,   // 인스턴스가 세계 곳곳에 흩어진다: 묶음 전체를 늘 그린다 (인스턴스는 보이는 것만 넣는다)
            };
            parent.AddChild(node);
            var batch = new Batch { mesh = multi, node = node, character = character };
            batches[name] = batch;
            return batch;
        }

        static bool PropKindOf(string name, out LowPolyMesh.PropKind kind)
        {
            kind = LowPolyMesh.PropKind.Rock;
            if (name.EndsWith("Pond")) return false;   // 연못은 바닥 그림
            if (name.Contains("Pine")) kind = LowPolyMesh.PropKind.Pine;
            else if (name.Contains("Palm")) kind = LowPolyMesh.PropKind.Palm;
            else if (name.Contains("Willow")) kind = LowPolyMesh.PropKind.Willow;
            else if (name.Contains("DeadTree")) kind = LowPolyMesh.PropKind.DeadTree;
            else if (name.Contains("Tree")) kind = LowPolyMesh.PropKind.Tree;
            else if (name.Contains("Bush") || name.Contains("Shrub") || name.Contains("Fern")) kind = LowPolyMesh.PropKind.Bush;
            else if (name.Contains("Flowers")) kind = LowPolyMesh.PropKind.Flowers;
            else if (name.Contains("Grass") || name.Contains("Reed")) kind = LowPolyMesh.PropKind.Grass;
            else if (name.Contains("Mound") || name.Contains("Terrace")) kind = LowPolyMesh.PropKind.Mound;
            else if (name.Contains("Stump")) kind = LowPolyMesh.PropKind.Stump;
            else if (name.Contains("Boat")) kind = LowPolyMesh.PropKind.Boat;
            else if (name.Contains("Yurt")) kind = LowPolyMesh.PropKind.Yurt;
            else if (name.Contains("Banner")) kind = LowPolyMesh.PropKind.Banner;
            else if (name.Contains("Rock") || name.Contains("Boulder")) kind = LowPolyMesh.PropKind.Rock;
            else return false;
            return true;
        }

        // 걷기 시트 첫 칸(정면)에서 몸이 그려진 범위를 찾아 그 안의 띠(머리카락/얼굴/몸통/다리)에서 색을 뽑는다.
        // 말 탄 그림은 옆모습(넷째 줄 첫 칸)의 아래쪽이 말 색이고, 사람은 정면 칸의 위쪽 절반이다
        static LowPolyMesh.Palette SamplePalette(Image image, bool rider)
        {
            int cell = image.GetWidth() / 4;
            Bounds(image, 0, 0, cell, cell, out int bx, out int by, out int bw, out int bh);
            if (rider) bh = bh * 55 / 100;   // 위쪽이 사람
            int cx = bx + bw / 4, cw = Mathf.Max(2, bw / 2);
            GColor Band(float from, float to, GColor fallback, bool skin = false)
                => Dominant(image, cx, by + (int)(bh * from), cw, Mathf.Max(1, (int)(bh * (to - from))), fallback, skin);
            var p = new LowPolyMesh.Palette
            {
                Hair = Band(0.02f, 0.2f, new GColor(0.2f, 0.15f, 0.12f)),
                Skin = Band(0.2f, 0.45f, new GColor(0.93f, 0.76f, 0.6f), true),
                Torso = Band(0.5f, 0.74f, new GColor(0.4f, 0.4f, 0.5f)),
                Legs = Band(0.8f, 0.97f, new GColor(0.3f, 0.3f, 0.35f)),
            };
            if (p.Skin.R < 0.5f) p.Skin = new GColor(0.93f, 0.76f, 0.6f);   // 얼굴이 수염·투구에 가려 살색을 못 찾으면 기본 살색
            p.Mount = p.Legs;
            if (rider)
            {
                Bounds(image, 0, cell * 3, cell, cell, out int hx, out int hy, out int hw, out int hh);
                p.Mount = Dominant(image, hx + hw / 5, hy + hh * 6 / 10, hw * 3 / 5, Mathf.Max(1, hh * 2 / 10), new GColor(0.45f, 0.3f, 0.2f));
                p.Legs = new GColor(p.Torso.R * 0.7f, p.Torso.G * 0.7f, p.Torso.B * 0.7f);
            }
            // 강조색: 몸통 색을 밝고 진하게 (허리띠, 어깨 갑옷, 깃)
            p.Accent = new GColor(Mathf.Min(1f, p.Torso.R * 0.55f + 0.42f), Mathf.Min(1f, p.Torso.G * 0.5f + 0.3f), Mathf.Min(1f, p.Torso.B * 0.45f + 0.12f));
            return p;
        }

        // 사각형 안에서 불투명한 점이 있는 범위
        static void Bounds(Image image, int x0, int y0, int w, int h, out int bx, out int by, out int bw, out int bh)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = y0; y < y0 + h && y < image.GetHeight(); y++)
                for (int x = x0; x < x0 + w && x < image.GetWidth(); x++)
                    if (image.GetPixel(x, y).A >= 0.5f)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            if (maxX < 0) { bx = x0; by = y0; bw = w; bh = h; return; }
            bx = minX; by = minY; bw = maxX - minX + 1; bh = maxY - minY + 1;
        }

        // 사각형 안에서 가장 흔한 색 (투명·외곽선의 검정은 뺀다). skin = true 면 살색에 가까운 색을 우선한다
        static GColor Dominant(Image image, int x0, int y0, int w, int h, GColor fallback, bool skin = false)
        {
            var counts = new Dictionary<int, (int n, float r, float g, float b)>();
            int x1 = Mathf.Min(image.GetWidth(), x0 + Mathf.Max(1, w)), y1 = Mathf.Min(image.GetHeight(), y0 + Mathf.Max(1, h));
            for (int y = Mathf.Max(0, y0); y < y1; y++)
                for (int x = Mathf.Max(0, x0); x < x1; x++)
                {
                    var c = image.GetPixel(x, y);
                    if (c.A < 0.5f) continue;
                    float lum = c.R * 0.3f + c.G * 0.59f + c.B * 0.11f;
                    if (lum < 0.13f) continue;
                    int key = ((int)(c.R * 7.99f) << 6) | ((int)(c.G * 7.99f) << 3) | (int)(c.B * 7.99f);
                    counts.TryGetValue(key, out var e);
                    counts[key] = (e.n + 1, e.r + c.R, e.g + c.G, e.b + c.B);
                }
            float best = 0f; GColor result = fallback;
            foreach (var e in counts.Values)
            {
                var c = new GColor(e.r / e.n, e.g / e.n, e.b / e.n);
                float score = e.n * (0.4f + Mathf.Max(c.R, Mathf.Max(c.G, c.B)));   // 그늘 색보다 밝은 본색을 조금 우선
                if (skin)
                {
                    // 살색: 붉은 기가 가장 세고 꽤 밝다
                    bool like = c.R > 0.55f && c.R > c.G && c.G > c.B && c.G > 0.35f;
                    score *= like ? 4f : 1f;
                }
                if (score > best) { best = score; result = c; }
            }
            return result;
        }

        // ───────────────────────── 무기·탄환·이펙트 (3D 전환 3단계) ─────────────────────────
        // 규칙 코드는 여전히 납작한 그림(SpriteRenderer)을 움직인다. 화면 층이 그 그림의 역할(이름·정렬 레이어·그림 파일)을 보고
        // 같은 자리·방향·크기·색의 3D 모델/이펙트 메시로 바꿔 그린다. 체력 막대·데미지 숫자·물결·바닥 얼룩은 그대로 둔다.

        enum Role { None, Weapon, Arrow, Orb, Gem, Cube, SpinBlade, Ring, Bolt, Crescent, Wedge, Flames }

        sealed class RoleEntry { public Compat.Sprite sprite; public Role role; public Batch batch; public Compat.Color tint = Compat.Color.white; }

        readonly Dictionary<string, Compat.Color> textureColors = new Dictionary<string, Compat.Color>();

        // 그림 파일의 가장 흔한 색 (밝게 맞춘다). 코드로 만든 그림(검기, 입자)은 흰색
        Compat.Color TextureColor(Compat.Sprite sprite)
        {
            string path = sprite.path;
            if (string.IsNullOrEmpty(path)) return Compat.Color.white;
            if (textureColors.TryGetValue(path, out var cached)) return cached;
            var result = Compat.Color.white;
            var texture = Textures.Of(sprite);
            var image = texture != null ? texture.GetImage() : null;
            if (image != null)
            {
                if (image.IsCompressed()) image.Decompress();
                var c = Dominant(image, 0, 0, image.GetWidth(), image.GetHeight(), new GColor(1f, 1f, 1f));
                float max = Mathf.Max(0.05f, Mathf.Max(c.R, Mathf.Max(c.G, c.B)));
                result = new Compat.Color(c.R / max, c.G / max, c.B / max, 1f);
            }
            textureColors[path] = result;
            return result;
        }

        /// <summary>땅에서 띄우는 높이(유닛): 탄환·검기는 몸통 높이, 보석은 발치.</summary>
        public const float ProjectileHeight = 0.8f, WaveHeight = 0.75f, GemHeight = 0.32f, BladeHeight = 0.7f;
        /// <summary>손에 든 무기의 길이 배율 (사람 모델을 키운 만큼) 과 들어 올린 각도(수평에서, 라디안 탄젠트).</summary>
        public const float WeaponScale = 1.7f, WeaponRise = 0.35f;

        readonly Dictionary<SpriteRenderer, RoleEntry> roles = new Dictionary<SpriteRenderer, RoleEntry>();
        ShaderMaterial fxMaterial;

        static GVec3 Map(CVec3 v) => new GVec3(v.x, -v.z, -v.y);

        /// <summary>손에 든 무기·탄환·보석·이펙트 그림을 3D 로 그렸으면 true (그러면 도트 그림은 숨긴다).</summary>
        public bool TryDrawEffect(SpriteRenderer sr, Compat.Sprite sprite)
        {
            if (!roles.TryGetValue(sr, out var entry)) roles[sr] = entry = new RoleEntry();
            if (entry.sprite != sprite)
            {
                entry.sprite = sprite;
                entry.role = RoleOf(sr, sprite);
                entry.batch = entry.role == Role.None ? null : EffectBatch(entry.role, sprite);
                // 무기·화살·칼날은 모델이 제 색을 갖는다. 나머지는 그림의 본색을 입힌다
                bool ownColor = entry.role == Role.Weapon || entry.role == Role.Arrow || entry.role == Role.SpinBlade || entry.role == Role.None;
                entry.tint = ownColor ? Compat.Color.white : TextureColor(sprite);
            }
            if (entry.batch == null) return false;

            var role = entry.role;
            var t = sr.transform;
            var p = t.position;
            var q = t.rotation;
            var scale = t.lossyScale;
            float sxAbs = Mathf.Abs(scale.x), syAbs = Mathf.Abs(scale.y);
            float w = sprite.rect.width / sprite.pixelsPerUnit * sxAbs, h = sprite.rect.height / sprite.pixelsPerUnit * syAbs;
            var color = sr.color;
            color = new Compat.Color(color.r * entry.tint.r, color.g * entry.tint.g, color.b * entry.tint.b, color.a);
            var origin = Map(p);
            var right = Map(q * CVec3.right);
            var up = Map(q * CVec3.up);
            var Y = GVec3.Up;
            int hash = (Mathf.RoundToInt(p.x * 31.7f) * 73856093) ^ (Mathf.RoundToInt(p.y * 17.3f) * 19349663) ^ sr.GetHashCode();
            float h01 = (hash & 1023) / 1023f;
            float time = Compat.Time.time;
            var batch = entry.batch;

            // 그 자리에서 번개인지 고리인지 갈리는 이펙트 (같은 그림을 줄기와 착탄 고리에 함께 쓴다)
            if (role == Role.Bolt && Mathf.Abs(sxAbs - syAbs) < 0.02f * Mathf.Max(sxAbs, syAbs)) { role = Role.Ring; batch = EffectBatch(Role.Ring, sprite); }

            switch (role)
            {
                case Role.Weapon:
                {
                    // 그림의 위쪽(날 방향)을 땅 위의 방향으로: 몸 둘레를 수평으로 휘두르고 끝을 조금 든다
                    var flat = new GVec3(up.X, 0f, up.Z);
                    if (flat.LengthSquared() < 1e-6f) flat = GVec3.Back;
                    var axis = (flat.Normalized() + Y * WeaponRise).Normalized();
                    float len = h * WeaponScale;
                    var side = Y.Cross(axis).Normalized() * (sr.flipX ? -1f : 1f);
                    Push(batch, side * len, axis * len, side.Cross(axis).Normalized() * len, origin, color.r, color.g, color.b, 1f, 0f, 0f, color.a, 0f);
                    break;
                }
                case Role.Arrow:
                {
                    var dir = new GVec3(right.X, 0f, right.Z);
                    if (dir.LengthSquared() < 1e-6f) dir = GVec3.Right;
                    dir = dir.Normalized();
                    float len = Mathf.Max(w, h) * 1.6f;
                    Push(batch, dir * len, Y * len, dir.Cross(Y) * len, origin + Y * ProjectileHeight, color.r, color.g, color.b, 1f, 0f, 0f, color.a, 0f);
                    break;
                }
                case Role.Orb:
                {
                    float s = Mathf.Max(w, h) * 1.3f, yaw = time * 5f + h01 * 6.28f, c = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
                    Push(batch, new GVec3(c, 0f, -sn) * s, Y * s, new GVec3(sn, 0f, c) * s, origin + Y * ProjectileHeight, color.r, color.g, color.b, color.a, 0f, 0f, 0f, 0f);
                    break;
                }
                case Role.Gem:
                {
                    float s = Mathf.Max(w, h) * 1.0f, yaw = time * 2.4f + h01 * 6.28f, c = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
                    float bob = Mathf.Sin(time * 3f + h01 * 6.28f) * 0.06f;
                    Push(batch, new GVec3(c, 0f, -sn) * s, Y * s, new GVec3(sn, 0f, c) * s, origin + Y * (GemHeight + bob), color.r, color.g, color.b, 1f, 0f, 0f, color.a, 0.22f);
                    break;
                }
                case Role.Cube:
                {
                    float s = Mathf.Max(w, h) * 0.7f, yaw = time * 7f + h01 * 6.28f, c = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
                    // 기울여 굴린다: 세 면이 다 보여야 덩어리로 읽힌다
                    var bx = new GVec3(c, 0.5f, -sn).Normalized(); var bz = bx.Cross(Y).Normalized(); var by = bz.Cross(bx);
                    Push(batch, bx * s, by * s, bz * s, origin + Y * (0.4f + h01 * 0.6f), color.r, color.g, color.b, 1f, 0f, 0f, color.a, 0.3f);
                    break;
                }
                case Role.SpinBlade:
                {
                    float s = Mathf.Max(w, h) * 1.2f;
                    var r = new GVec3(right.X, 0f, right.Z).Normalized();
                    Push(batch, r * s, Y * s, r.Cross(Y) * s, origin + Y * BladeHeight, color.r, color.g, color.b, 1f, 0f, 0f, color.a, 0f);
                    break;
                }
                case Role.Ring:
                {
                    float d = Mathf.Max(w, h);
                    Push(batch, GVec3.Right * d, Y * Mathf.Clamp(d, 0.8f, 3f), GVec3.Back * d, origin + Y * 0.03f, color.r, color.g, color.b, color.a, 0f, 0f, 0f, 0f);
                    break;
                }
                case Role.Bolt:
                {
                    // 그림은 땅에 누워 있다: 아래 끝(남쪽 끝)이 떨어지는 자리. 그 자리에서 하늘로 세운다
                    var foot = Map(p - q * CVec3.up * (h * 0.5f));
                    Push(batch, GVec3.Right * h * 0.9f, Y * h * 1.5f, GVec3.Back * h * 0.9f, foot, color.r, color.g, color.b, color.a, 0f, 0f, 0f, 0f);
                    break;
                }
                case Role.Crescent:
                case Role.Wedge:
                {
                    float radius = Samkuk.Weapons.WaveSprites.RadiusUnits * sxAbs;
                    var r = new GVec3(right.X, 0f, right.Z).Normalized();
                    Push(batch, r * radius, Y * radius, r.Cross(Y) * radius, origin + Y * WaveHeight, color.r, color.g, color.b, color.a, 0f, 0f, 0f, 0f);
                    break;
                }
                case Role.Flames:
                {
                    float d = Mathf.Max(w, h);
                    Push(batch, GVec3.Right * d, Y * Mathf.Clamp(d * 0.6f, 0.9f, 2f), GVec3.Back * d, origin + Y * 0.02f, color.r, color.g, color.b, color.a, h01, 0f, 0f, 0f);
                    break;
                }
            }
            return true;
        }

        void Push(Batch b, GVec3 bx, GVec3 by, GVec3 bz, GVec3 origin, float r, float g, float bl, float a, float c0, float c1, float c2, float c3)
        {
            if (b.count >= b.capacity) Grow(b);
            int o = b.count++ * Stride;
            var f = b.buffer;
            f[o] = bx.X; f[o + 1] = by.X; f[o + 2] = bz.X; f[o + 3] = origin.X;
            f[o + 4] = bx.Y; f[o + 5] = by.Y; f[o + 6] = bz.Y; f[o + 7] = origin.Y;
            f[o + 8] = bx.Z; f[o + 9] = by.Z; f[o + 10] = bz.Z; f[o + 11] = origin.Z;
            f[o + 12] = r; f[o + 13] = g; f[o + 14] = bl; f[o + 15] = a;
            f[o + 16] = c0; f[o + 17] = c1; f[o + 18] = c2; f[o + 19] = c3;
        }

        static Role RoleOf(SpriteRenderer sr, Compat.Sprite sprite)
        {
            string path = sprite.path ?? "";
            string tex = sprite.texture != null ? sprite.texture.name ?? "" : "";
            string name = sr.gameObject.name ?? "";
            var parent = sr.transform.parent;
            string parentName = parent != null ? parent.gameObject.name ?? "" : "";

            if (path.Contains("/Weapons/") && name.Contains("Held")) return Role.Weapon;   // 오프라인 무기는 Held#, 협동 꼭두각시는 P#Held
            if (path.EndsWith("/ExpGem.png")) return Role.Gem;
            if (tex == "BurstSquare" || sprite.name == "BurstSquare") return Role.Cube;
            if (sprite.name == "SlashArc") return Role.Crescent;
            if (sprite.name == "ThrustPoint") return Role.Wedge;
            if (name.StartsWith("Blade") || path.EndsWith("/Blade.png")) return Role.SpinBlade;
            if (sr.sortingLayerName == "Projectile") return path.EndsWith("/Arrow.png") || path.EndsWith("/Bolt.png") ? Role.Arrow : Role.Orb;
            bool effect = parentName == "WeaponFx" || name == "SkillFx" || name == "NovaRing" || name == "FireZone";
            if (!effect) return Role.None;
            if (path.EndsWith("/Fire.png")) return Role.Flames;
            if (path.EndsWith("/Bolt.png")) return Role.Bolt;   // 줄기인지 고리인지는 그릴 때 배율로 가른다
            return Role.Ring;
        }

        Batch EffectBatch(Role role, Compat.Sprite sprite)
        {
            string key = "Fx_" + role;
            if (role == Role.Weapon) key = System.IO.Path.GetFileNameWithoutExtension(sprite.path);
            if (batches.TryGetValue(key, out var existing)) return existing;

            ArrayMesh mesh; bool glow = true;
            switch (role)
            {
                case Role.Weapon:
                {
                    glow = false;
                    var texture = Textures.Of(sprite);
                    var image = texture != null ? texture.GetImage() : null;
                    var blade = new GColor(0.85f, 0.87f, 0.92f); var shaft = new GColor(0.45f, 0.3f, 0.18f);
                    if (image != null)
                    {
                        if (image.IsCompressed()) image.Decompress();
                        Bounds(image, 0, 0, image.GetWidth(), image.GetHeight(), out int bx, out int by, out int bw, out int bh);
                        blade = Dominant(image, bx, by, bw, Mathf.Max(1, bh * 4 / 10), blade);
                        shaft = Dominant(image, bx, by + bh * 6 / 10, bw, Mathf.Max(1, bh * 4 / 10), shaft);
                    }
                    var accent = new GColor(0.85f, 0.68f, 0.25f);
                    string n = key;
                    var kind = n.Contains("GreenDragon") || n.Contains("MoonDragon") ? LowPolyMesh.WeaponKind.Glaive
                        : n.Contains("SkyPiercer") || n.Contains("Halberd") ? LowPolyMesh.WeaponKind.Halberd
                        : n.Contains("Spear") || n.Contains("Thrust") ? LowPolyMesh.WeaponKind.Spear
                        : LowPolyMesh.WeaponKind.Sword;
                    mesh = LowPolyMesh.HeldWeapon(kind, blade, shaft, accent);
                    break;
                }
                case Role.Arrow: mesh = LowPolyMesh.Arrow(); glow = false; break;
                case Role.Gem: mesh = LowPolyMesh.Gem(); glow = false; break;
                case Role.SpinBlade: mesh = LowPolyMesh.SpinBlade(); glow = false; break;
                case Role.Orb: mesh = LowPolyMesh.Gem(1f); break;
                case Role.Cube: mesh = LowPolyMesh.Cube(); glow = false; break;
                case Role.Ring: mesh = LowPolyMesh.Ring(); break;
                case Role.Bolt: mesh = LowPolyMesh.BoltZig(); break;
                case Role.Crescent: mesh = LowPolyMesh.Crescent(); break;
                case Role.Wedge: mesh = LowPolyMesh.Wedge(); break;
                case Role.Flames: mesh = LowPolyMesh.Flames(); break;
                default: return null;
            }

            if (material == null) material = new ShaderMaterial { Shader = GD.Load<Godot.Shader>("res://shaders/lowpoly.gdshader") };
            if (fxMaterial == null) fxMaterial = new ShaderMaterial { Shader = GD.Load<Godot.Shader>("res://shaders/lowpoly_fx.gdshader"), RenderPriority = 20 };
            var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = mesh };
            var node = new MultiMeshInstance3D
            {
                Name = "Model_" + key,
                Multimesh = multi,
                MaterialOverride = glow ? fxMaterial : material,
                CastShadow = glow ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
                ExtraCullMargin = 4096f,
            };
            parent.AddChild(node);
            var batch = new Batch { mesh = multi, node = node };
            batches[key] = batch;
            return batch;
        }
    }
}
