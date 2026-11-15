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
    /// 동작(G16, 판정과 무관한 연출): 공격 자세 = 손에 든 무기가 보이는 동안 그쪽으로 돌아 몸을 숙이고 무기 든 팔을 든다(적은 주인공·아군에 닿아 있는 동안 팔을 내려친다),
    /// 쓰러짐 = 쓰러진 주인공·아군은 뒤로 눕고, 죽은 적은 그 자리에 잠깐 남아 넘어지며 사라진다(규칙은 적을 바로 풀로 돌려보낸다).
    /// </summary>
    public sealed class ModelMirror
    {
        const int Stride = 20;               // 변환 12 + 색 4 + 사용자 값 4
        /// <summary>사람 모델의 키: 그림 속 몸 높이(40px/32)에 곱한다. 내려다보는 각도 때문에 납작해 보이는 만큼 조금 키운다.</summary>
        public const float CharacterScale = 1.3f;
        public const float PropHeightScale = 1.0f;
        /// <summary>야전의 부대 모델 배율: 한 칸(변 약 1.06유닛)에 말 탄 부대가 들어가게 줄인다.</summary>
        public const float IsoUnitScale = 0.62f;
        const float TurnSpeed = 14f;         // 몸 돌리기 (라디안/초)
        const float VelocitySmoothing = 0.1f; // 움직이는 방향을 읽는 속도를 고르게 하는 시간 (초)
        const float StepPerUnit = 5.2f;      // 걸은 거리 1유닛당 다리 흔들기 위상
        const float SwingAngle = 0.62f;      // 다리 흔들 각 (라디안)

        sealed class Batch
        {
            public MultiMesh mesh;
            public MultiMeshInstance3D node;
            public MultiMeshInstance3D outline;   // 외곽선: 같은 MultiMesh 를 뒤집은 껍질 재질로 한 번 더 (없으면 선 없는 종류)
            public float[] buffer = new float[0];
            public int count, capacity;
            public bool character, hero;
            public float baseHeight = 1f;   // 인스턴스 배율 1 일 때의 모델 높이
        }

        sealed class State
        {
            public float yaw, phase, amplitude;
            public float vx, vy;   // 고르게 한 속도 (유닛/초)
            public CVec3 lastPosition;
            public bool started;
            public int seen;
            // 동작: 공격 자세(0~1), 쓰러짐(0~1). 누가 쓰러졌는지는 규칙 컴포넌트에서 읽는다
            public float attack, fall;
            public bool looked;
            public float isoYaw; public bool isoSet;   // 야전: 멈춰 있을 때 바라보는 쪽
            public bool fieldLooked;
            public Samkuk.Field.FieldUnitView unitView;
            public Samkuk.Player.PlayerHealth health;
            public Samkuk.Allies.AllyController ally;
            // 마지막으로 그린 모습 (죽은 적이 넘어지는 연출의 시작)
            public Batch batch;
            public CVec3 position;
            public float sx, sy, sz;
            public Compat.Color color;
        }

        sealed class Ghost { public Batch batch; public CVec3 position; public float yaw, sx, sy, sz, time; public Compat.Color color; public float slow = 1f; }   // slow: 넘어지고 사라지는 시간 배율 (야전의 부대는 천천히)

        const float AttackLean = 0.16f;      // 공격 자세에서 몸을 앞으로 숙이는 각 (라디안)
        const float FallAngle = 1.5f;        // 쓰러졌을 때 뒤로 누운 각
        const float ContactRange = 0.95f;    // 적이 주인공·아군에 이만큼 가까우면 내려친다
        const float GhostFall = 0.28f, GhostFade = 0.3f;   // 죽은 적: 넘어지는 시간, 그 뒤 사라지는 시간
        const int MaxGhosts = 160;

        // 손에 든 무기가 보이는 주인(주인공·아군의 트랜스폼) → (본 프레임, 휘두르는 쪽)
        readonly Dictionary<Transform, (int frame, float yaw)> swings = new Dictionary<Transform, (int, float)>();
        List<CVec3> heroes = new List<CVec3>(), heroesNow = new List<CVec3>();   // 적의 내려치기 판단용 (지난 프레임의 주인공·아군 자리)
        readonly List<Ghost> ghosts = new List<Ghost>();
        bool coop;   // 협동 전투 중인가 (가끔 확인한다)

        readonly Node3D parent;
        readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
        readonly Dictionary<string, Batch> byTexture = new Dictionary<string, Batch>();   // 그림 경로 → 묶음 (없으면 null 을 넣어 다시 찾지 않는다)
        readonly Dictionary<SpriteRenderer, State> states = new Dictionary<SpriteRenderer, State>();
        readonly List<SpriteRenderer> dead = new List<SpriteRenderer>();
        // 카툰 재질 (G20): 캐릭터·무기는 시간대 색조를 절반만 따르고(저녁에도 장수 색이 구분되게) 소품은 그대로 따른다
        ShaderMaterial material, propMaterial, lineMaterial, propLineMaterial;
        /// <summary>외곽선 굵기 (1080p 기준 픽셀): 캐릭터·말·무기 / 서 있는 소품. 풀·꽃은 선이 없다.</summary>
        public const float LineWidth = 2.5f, PropLineWidth = 1.75f;
        /// <summary>그림에서 뽑은 색을 모델에 넣을 때의 보정 (채도, 밝기 배율). 살색은 덜 올린다(주황빛 방지). 도트 화면은 그림 그대로다.</summary>
        public const float VividSaturation = 1.25f, VividValue = 1.1f, SkinSaturation = 1.08f, SkinValue = 1.05f;
        int frame;

        void EnsureMaterials()
        {
            if (material != null) return;
            var toon = GD.Load<Godot.Shader>("res://shaders/toon.gdshader");
            var line = GD.Load<Godot.Shader>("res://shaders/toon_outline.gdshader");
            material = new ShaderMaterial { Shader = toon };
            material.SetShaderParameter("tint_keep", 0.5f);
            propMaterial = new ShaderMaterial { Shader = toon };
            propMaterial.SetShaderParameter("tint_keep", 1f);
            lineMaterial = new ShaderMaterial { Shader = line };
            lineMaterial.SetShaderParameter("width_px", LineWidth);
            propLineMaterial = new ShaderMaterial { Shader = line };
            propLineMaterial.SetShaderParameter("width_px", PropLineWidth);
        }

        /// <summary>환경광 색 (WorldMirror 가 프레임마다 넣는다): 카툰 재질은 엔진의 환경광을 끄고 이 값을 직접 더한다.</summary>
        public void SetAmbient(GColor color)
        {
            EnsureMaterials();
            var v = new GVec3(color.R, color.G, color.B);
            if (v == ambient) return;
            ambient = v;
            material.SetShaderParameter("ambient_color", v);
            propMaterial.SetShaderParameter("ambient_color", v);
        }
        GVec3 ambient = new GVec3(-1f, -1f, -1f);

        static GColor Vivid(GColor c, float saturation = VividSaturation, float value = VividValue)
        {
            c.ToHsv(out float h, out float s, out float v);
            return GColor.FromHsv(h, Mathf.Min(1f, s * saturation), Mathf.Min(1f, v * value));
        }

        // 묶음 하나의 노드: 본 모습 + (line 이 있으면) 외곽선. 외곽선은 그림자를 드리우지 않는다
        Batch AddBatch(string key, ArrayMesh mesh, Godot.Material main, ShaderMaterial line, bool castShadow)
        {
            var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = mesh };
            var node = new MultiMeshInstance3D
            {
                Name = "Model_" + key,
                Multimesh = multi,
                MaterialOverride = main,
                CastShadow = castShadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                ExtraCullMargin = 4096f,   // 인스턴스가 세계 곳곳에 흩어진다: 묶음 전체를 늘 그린다 (인스턴스는 보이는 것만 넣는다)
            };
            parent.AddChild(node);
            var batch = new Batch { mesh = multi, node = node };
            if (line != null)
            {
                batch.outline = new MultiMeshInstance3D
                {
                    Name = "Line_" + key, Multimesh = multi, MaterialOverride = line,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 4096f,
                };
                parent.AddChild(batch.outline);
            }
            batches[key] = batch;
            return batch;
        }

        public int InstanceCount { get; private set; }
        /// <summary>점검용 (--shot-poses): 넘어지는 중인 죽은 적이 있는가, 쓰러져 누운 주인공·아군이 있는가, 공격 자세를 잡은 주인공·아군이 있는가.</summary>
        public bool AnyGhostFalling { get; private set; }
        public bool AnyDown { get; private set; }
        public bool AnyAttacking { get; private set; }
        public int BatchCount => batches.Count;

        public ModelMirror(Node3D parent)
        {
            this.parent = parent;
            Samkuk.Enemies.Enemy.Died += OnEnemyDied;
        }

        // ───────────────────────── 야전의 동작 (G19) ─────────────────────────
        // 규칙(FieldBattleModel)의 이벤트를 듣기만 한다: 때린 부대는 잠깐 상대 쪽으로 돌아 팔을 내려치고(공격 자세), 무너진 부대는 그 자리에서 넘어지며 사라진다.
        /// <summary>야전의 이펙트(무기·검기·화살·입자)가 나오는 높이: 발 + 0.55 (FieldFx.PositionOf 의 손 높이, 2D 유닛).</summary>
        public const float FieldHandLift = 0.55f;
        /// <summary>야전에서 장수가 든 무기 모델의 길이 배율 (부대 모델을 줄인 만큼).</summary>
        public const float FieldWeaponScale = 0.7f;
        const float FieldAttackHold = 0.3f;   // 마지막으로 때린 뒤 공격 자세를 유지하는 시간 (피해는 매 틱 일어난다)
        Samkuk.Field.FieldBattleController fieldController;
        Samkuk.Field.FieldBattleModel fieldModel;
        readonly Dictionary<int, (float time, float yaw)> fieldAttacks = new Dictionary<int, (float, float)>();

        void SyncField()
        {
            Samkuk.Field.FieldBattleModel model = null;
            if (WorldMirror.IsoField)
            {
                if (fieldController == null || fieldController.destroyed || frame % 30 == 1) fieldController = Compat.Object.FindAnyObjectByType<Samkuk.Field.FieldBattleController>();
                if (fieldController != null) model = fieldController.Model;
            }
            if (model == fieldModel) return;
            if (fieldModel != null) { fieldModel.Hit -= OnFieldHit; fieldModel.UnitRouted -= OnFieldRouted; }
            fieldModel = model;
            fieldAttacks.Clear();
            if (model != null) { model.Hit += OnFieldHit; model.UnitRouted += OnFieldRouted; }
        }

        void OnFieldHit(Samkuk.Field.FieldUnit attacker, Samkuk.Field.FieldUnit victim, float amount, bool ranged, bool charge)
        {
            Samkuk.Field.IsoGrid.CellToWorld(attacker.VisualCol, attacker.VisualRow, out float ax, out float ay);
            Samkuk.Field.IsoGrid.CellToWorld(victim.VisualCol, victim.VisualRow, out float vx, out float vy);
            fieldAttacks[attacker.Id] = (Compat.Time.time, Mathf.Atan2(vx - ax, -(vy - ay) * WorldMirror.IsoDepth));
        }

        void OnFieldRouted(Samkuk.Field.FieldUnit unit)
        {
            if (fieldController == null || ghosts.Count >= MaxGhosts) return;
            var view = fieldController.ViewOf(unit);
            var sr = view != null ? view.Renderer : null;
            if (sr == null || !states.TryGetValue(sr, out var s) || s.batch == null || s.seen < frame - 1) return;
            ghosts.Add(new Ghost { batch = s.batch, position = s.position, yaw = s.yaw, sx = s.sx, sy = s.sy, sz = s.sz, color = s.color, slow = 2.2f });
        }

        public void Begin()
        {
            frame++;
            SyncField();
            foreach (var b in batches.Values) b.count = 0;
            AnyGhostFalling = AnyDown = AnyAttacking = false;
            if (frame % 30 == 1) coop = Compat.Object.FindAnyObjectByType<Samkuk.Coop.CoopBattleDriver>() != null;
            var swap = heroes; heroes = heroesNow; heroesNow = swap;
            heroesNow.Clear();
        }

        // 죽은 적: 규칙은 바로 풀로 돌려보낸다. 마지막 모습을 받아 그 자리에서 넘어지는 모델을 잠깐 남긴다
        void OnEnemyDied(Samkuk.Enemies.Enemy enemy)
        {
            if (enemy == null || ghosts.Count >= MaxGhosts) return;
            var sr = enemy.GetComponent<SpriteRenderer>();
            if (sr == null || !states.TryGetValue(sr, out var s) || s.batch == null || s.seen < frame - 1) return;
            ghosts.Add(new Ghost { batch = s.batch, position = s.position, yaw = s.yaw, sx = s.sx, sy = s.sy, sz = s.sz, color = s.color });
        }

        // 사람 모델 하나: yaw 로 돌리고 pitch 만큼 앞(+)·뒤(-)로 기울인다
        void PushCharacter(Batch batch, CVec3 pos, float yaw, float pitch, float sx, float sy, float sz, Compat.Color color, float swing, float bob, float alpha, float attack)
        {
            float c = Mathf.Cos(yaw), sn = Mathf.Sin(yaw), cp = Mathf.Cos(pitch), sp = Mathf.Sin(pitch);
            Push(batch, new GVec3(c, 0f, -sn) * sx, new GVec3(sn * sp, cp, c * sp) * sy, new GVec3(sn * cp, -sp, c * cp) * sz,
                new GVec3(pos.x, -pos.z, -pos.y), color.r, color.g, color.b, 1f, swing, bob, alpha, -attack);
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
            bool iso = WorldMirror.IsoField;
            if (iso) pos = new CVec3(pos.x, pos.y * WorldMirror.IsoDepth, 0f);   // 야전: 2D 아이소메트릭 좌표를 땅 위의 점으로
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
                if (iso)
                {
                    // 야전은 시트의 왼쪽·오른쪽 줄만 쓴다. 땅 위에서는 대각선이다: 오른쪽 줄 = 적진 쪽(열 +), 왼쪽 줄 = 내 진영 쪽(열 -).
                    // 멈춰 있는 동안은 마지막으로 걷던 쪽을 그대로 본다(줄이 바뀌었을 때만 그 줄의 기본 방향으로)
                    bool left = row == 2;
                    if (!state.isoSet || (Mathf.Sin(state.isoYaw) < 0f) != left) { state.isoYaw = left ? -Mathf.PI * 0.25f : Mathf.PI * 0.75f; state.isoSet = true; }
                    target = state.isoYaw;
                }
                if (!state.looked)
                {
                    state.looked = true;
                    state.health = sr.GetComponentInParent<Samkuk.Player.PlayerHealth>();
                    state.ally = sr.GetComponentInParent<Samkuk.Allies.AllyController>();
                }
                // 쓰러짐: 주인공 사망, 아군 쓰러짐. 협동 전투(규칙 컴포넌트가 꺼져 있다)는 꼭두각시의 쓰러진 표시(불투명도 0.35, PuppetWalker.SetLook)로 안다
                bool down = batch.hero && (coop ? color.a > 0.3f && color.a < 0.37f
                    : (state.health != null && state.health.IsDead) || (state.ally != null && state.ally.IsDowned));
                // 공격 자세: 손에 든 무기가 보이는 동안 그쪽으로. 적은 주인공·아군에 닿아 있는 동안 팔을 내려친다
                bool swinging = false; float want = 0f;
                var owner = iso ? sr.transform : sr.transform.parent;   // 야전: 장수의 무기는 부대 밑에 있지 않아 부대 자신으로 찾는다
                if (!down && batch.hero && owner != null && swings.TryGetValue(owner, out var sw) && sw.frame >= frame - 1) { swinging = true; want = 1f; target = sw.yaw; }
                else if (!batch.hero)
                {
                    float reach = ContactRange * Mathf.Max(1f, scale.y);
                    for (int i = 0; i < heroes.Count; i++)
                    {
                        float hx = heroes[i].x - pos.x, hy = heroes[i].y - pos.y;
                        if (hx * hx + hy * hy > reach * reach) continue;
                        want = 0.5f + 0.5f * Mathf.Sin(Compat.Time.time * 9f + (sr.GetHashCode() & 255) * 0.1f);
                        break;
                    }
                }
                // 야전: 방금 때린 부대는 상대 쪽으로 돌아 팔을 내려친다 (장수는 무기를 휘두르는 쪽이 먼저)
                bool fieldAttack = false; float fieldYaw = 0f;
                if (iso)
                {
                    if (!swinging) want = 0f;
                    if (!state.fieldLooked) { state.fieldLooked = true; state.unitView = sr.GetComponent<Samkuk.Field.FieldUnitView>(); }
                    var unit = state.unitView != null ? state.unitView.Unit : null;
                    if (unit != null && fieldAttacks.TryGetValue(unit.Id, out var fa) && Compat.Time.time - fa.time < FieldAttackHold)
                    {
                        fieldAttack = true; fieldYaw = fa.yaw;
                        if (!swinging) want = 0.5f + 0.5f * Mathf.Sin(Compat.Time.time * 9f + (sr.GetHashCode() & 255) * 0.1f);
                    }
                }
                float dt = Mathf.Max(0.0001f, Compat.Time.deltaTime);
                float moved = 0f;
                if (state.started)
                {
                    float dx = pos.x - state.lastPosition.x, dy = pos.y - state.lastPosition.y;
                    moved = Mathf.Sqrt(dx * dx + dy * dy);
                    if (moved > 3f) { moved = 0f; state.vx = state.vy = 0f; }   // 순간 이동(풀에서 다시 나옴, 부활)
                    else
                    {
                        // 움직이는 방향은 고르게 한 속도에서 읽는다. 적은 고정 틱(50Hz)으로 움직여 화면 프레임(60Hz) 가운데 여섯에 하나는 제자리인데,
                        // 프레임마다의 이동만 보면 그 프레임에 목표가 걷기 시트의 4방향으로 튀어 몸(얼굴)이 좌우로 떨렸다
                        float k = 1f - Mathf.Exp(-dt / VelocitySmoothing);
                        state.vx += (dx / dt - state.vx) * k; state.vy += (dy / dt - state.vy) * k;
                        if (state.vx * state.vx + state.vy * state.vy > 0.3f * 0.3f)
                        {
                            float moveYaw = Mathf.Atan2(state.vx, -state.vy);   // Godot 의 +z 는 로직의 남쪽
                            if (iso) { state.isoYaw = moveYaw; if (!swinging) target = moveYaw; }
                            else if (!swinging && Mathf.Abs(Mathf.DeltaAngle(moveYaw * Mathf.Rad2Deg, rowYaw * Mathf.Rad2Deg)) < 75f) target = moveYaw;
                        }
                    }
                }
                else { state.yaw = target; state.started = true; }
                state.lastPosition = pos;
                if (fieldAttack && !swinging && state.vx * state.vx + state.vy * state.vy <= 0.3f * 0.3f) target = state.isoYaw = fieldYaw;
                float diff = Mathf.DeltaAngle(state.yaw * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                state.yaw += Mathf.Clamp(diff, -TurnSpeed * dt, TurnSpeed * dt);
                // 걷기: 움직인 거리만큼 위상이 가고, 멈추면 흔들림이 잦아든다
                float size = Mathf.Max(0.3f, scale.y);
                state.phase += moved * StepPerUnit / size;
                float walking = !down && state.vx * state.vx + state.vy * state.vy > 0.3f * 0.3f ? 1f : 0f;
                state.amplitude = Mathf.MoveTowards(state.amplitude, walking, dt * 8f);
                float s = Mathf.Sin(state.phase);
                swing = s * SwingAngle * state.amplitude;
                bob = Mathf.Abs(Mathf.Cos(state.phase)) * 0.035f * state.amplitude;
                yaw = state.yaw;
                // 그림 한 칸(48px)의 몸 높이 40px 를 모델 키로
                float body = sprite.rect.height / sprite.pixelsPerUnit * (40f / 48f) * CharacterScale / batch.baseHeight;
                if (iso) body *= IsoUnitScale;
                sx = Mathf.Abs(scale.x) * body; sy = scale.y * body; sz = sx;

                state.attack = Mathf.MoveTowards(state.attack, want, dt * (want > state.attack ? 16f : 7f));
                state.fall = Mathf.MoveTowards(state.fall, down ? 1f : 0f, Compat.Time.unscaledDeltaTime * (down ? 3.2f : 5f));   // 게임 오버로 시간이 멈춰도 끝까지 쓰러진다
                float pitch = AttackLean * state.attack - FallAngle * state.fall * state.fall;   // 쓰러질 때는 점점 빨리
                state.batch = batch; state.position = pos; state.sx = sx; state.sy = sy; state.sz = sz; state.color = color;
                if (batch.hero && !down) heroesNow.Add(pos);
                if (batch.hero && state.fall > 0.95f) AnyDown = true;
                if (batch.hero && state.attack > 0.95f) AnyAttacking = true;
                PushCharacter(batch, pos, yaw, pitch, sx, sy, sz, color, swing, bob, color.a, state.attack);
                return true;
            }
            else
            {
                // 소품: 그림 폭·높이에 맞춘다. 자리마다 다른 방향(결정적)으로 돌려 같은 모델이 줄지어 보이지 않게 한다
                float w = sprite.rect.width / sprite.pixelsPerUnit * Mathf.Abs(scale.x);
                float h = sprite.rect.height / sprite.pixelsPerUnit * scale.y * PropHeightScale;
                int hash = Mathf.RoundToInt(pos.x * 13.7f) * 73856093 ^ Mathf.RoundToInt(pos.y * 11.3f) * 19349663;
                yaw = (hash & 1023) / 1023f * (Mathf.PI * 2f);
                sx = w; sy = h; sz = w;   // 좌우 반전은 하지 않는다: 자리마다 돌려 놓는 데다, 뒤집으면 면의 앞뒤가 바뀌어 외곽선 껍질이 몸을 덮는다
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
            // 죽은 적: 뒤로 넘어진 뒤 점묘로 사라진다
            float gdt = Compat.Time.deltaTime;
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var g = ghosts[i];
                g.time += gdt / g.slow;
                if (g.time >= GhostFall + GhostFade) { ghosts.RemoveAt(i); continue; }
                float f = Mathf.Clamp01(g.time / GhostFall);
                if (f > 0.5f && f < 1f) AnyGhostFalling = true;
                float alpha = 1f - Mathf.Clamp01((g.time - GhostFall) / GhostFade);
                PushCharacter(g.batch, g.position, g.yaw, -FallAngle * f * f, g.sx, g.sy, g.sz, g.color, 0f, 0f, alpha * g.color.a, 0f);
            }

            int total = 0;
            foreach (var b in batches.Values)
            {
                total += b.count;
                b.node.Visible = b.count > 0;
                if (b.outline != null) b.outline.Visible = b.count > 0;
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
            foreach (var b in batches.Values) { b.node.Visible = false; if (b.outline != null) b.outline.Visible = false; }
            InstanceCount = 0;
            ghosts.Clear();
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
            if (path.Contains("/Field/") && name.StartsWith("Unit_")) enemy = true;   // 야전의 부대 그림 (걷기 시트 규격): 병과·진영마다 모델 하나
            if (!hero && !enemy && !(path.Contains("/Terrain/") && name.StartsWith("Prop_"))) return null;
            if (batches.TryGetValue(name, out var existing)) return existing;

            var texture = Textures.Of(sprite);
            var image = texture != null ? texture.GetImage() : null;
            if (image == null) return null;
            if (image.IsCompressed()) image.Decompress();

            ArrayMesh mesh;
            bool character = hero || enemy;
            bool lined = true;
            if (character)
            {
                bool rider = name.Contains("Cavalry") || name.Contains("Boss_") || name.Contains("General");
                var outfit = name.Contains("Archer") || name.Contains("Crossbow") ? LowPolyMesh.Outfit.Archer
                    : hero || name.Contains("Boss_") || name.Contains("General") ? LowPolyMesh.Outfit.General
                    : name.Contains("Elite") || name.Contains("Infantry") || name.Contains("Cavalry") ? LowPolyMesh.Outfit.Elite
                    : LowPolyMesh.Outfit.Plain;
                var palette = SamplePalette(image, rider);
                // 장수의 특징(수염·관·깃)은 그림 파일 이름으로 가린다. 적 병사는 눈만
                var feature = name.Contains("GuanYu") ? LowPolyMesh.Feature.GuanYu
                    : name.Contains("ZhangFei") ? LowPolyMesh.Feature.ZhangFei
                    : name.Contains("LiuBei") ? LowPolyMesh.Feature.LiuBei
                    : name.Contains("CaoCao") ? LowPolyMesh.Feature.CaoCao
                    : name.Contains("LvBu") || name.Contains("Lvbu") && name.Contains("Boss_") ? LowPolyMesh.Feature.LvBu
                    : LowPolyMesh.Feature.None;
                mesh = rider ? LowPolyMesh.Rider(palette, outfit, feature) : LowPolyMesh.Humanoid(palette, outfit, feature);
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
                mesh = LowPolyMesh.Prop(kind, Vivid(main), Vivid(second), name.GetHashCode() & 0xffff);
                lined = kind != LowPolyMesh.PropKind.Grass && kind != LowPolyMesh.PropKind.Flowers;   // 작고 많은 것에 선을 넣으면 화면이 까맣게 된다
            }

            EnsureMaterials();
            var batch = AddBatch(name, mesh, character ? material : propMaterial, !lined ? null : character ? lineMaterial : propLineMaterial, true);
            batch.character = character; batch.hero = hero;
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
            // 얼굴이 수염·투구에 가려 살색을 못 찾으면(어둡거나, 흰 눈자위·회색 투구처럼 붉은 기가 없으면) 기본 살색
            if (p.Skin.R < 0.5f || p.Skin.R - p.Skin.B < 0.3f * p.Skin.R || p.Skin.G < p.Skin.B) p.Skin = new GColor(0.95f, 0.78f, 0.62f);
            // 카툰 색 보정: 도트 그림의 차분한 색을 선명하게
            p.Hair = Vivid(p.Hair); p.Torso = Vivid(p.Torso); p.Legs = Vivid(p.Legs);
            p.Skin = Vivid(p.Skin, SkinSaturation, SkinValue);
            p.Mount = p.Legs;
            if (rider)
            {
                Bounds(image, 0, cell * 3, cell, cell, out int hx, out int hy, out int hw, out int hh);
                p.Mount = Vivid(Dominant(image, hx + hw / 5, hy + hh * 6 / 10, hw * 3 / 5, Mathf.Max(1, hh * 2 / 10), new GColor(0.45f, 0.3f, 0.2f)));
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
        internal static GColor Dominant(Image image, int x0, int y0, int w, int h, GColor fallback, bool skin = false)
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

        enum Role { None, Weapon, Arrow, Orb, Gem, Cube, SpinBlade, Ring, Bolt, Crescent, Wedge, Flames, Ripple }

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
            // 야전: 이펙트는 모두 손 높이(발 + 0.55)에서 나온다. 그만큼 내린 점을 땅으로 옮기고 다시 그 높이로 올리면 화면에서 같은 자리다.
            // 2D 평면의 방향은 땅 위의 방향으로 (세로 1 = 깊이 2). 역할마다 더하던 높이는 뺀다
            bool iso = WorldMirror.IsoField;
            float lift = 1f;
            if (iso)
            {
                origin = WorldMirror.IsoGround(new CVec3(p.x, p.y - FieldHandLift, 0f)) + GVec3.Up * (FieldHandLift / WorldMirror.IsoCos);
                var r2 = q * CVec3.right; var u2 = q * CVec3.up;
                right = new GVec3(r2.x, 0f, -r2.y * WorldMirror.IsoDepth);
                up = new GVec3(u2.x, 0f, -u2.y * WorldMirror.IsoDepth);
                lift = 0f;
            }
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
                    float len = h * WeaponScale * (iso ? FieldWeaponScale : 1f);
                    var side = Y.Cross(axis).Normalized() * (sr.flipX ? -1f : 1f);
                    // 무기의 주인(무기 오브젝트의 부모)이 이쪽으로 공격 자세를 잡는다
                    var holder = t.parent != null ? t.parent.parent : null;
                    if (iso)
                    {
                        // 야전: 무기는 연출 오브젝트 밑에 있다. 주인은 내 장수 부대
                        var general = fieldController != null && fieldModel != null ? fieldController.ViewOf(fieldModel.PlayerGeneral) : null;
                        holder = general != null ? general.transform : null;
                    }
                    if (holder != null) swings[holder] = (frame, Mathf.Atan2(flat.X, flat.Z));
                    Push(batch, side * len, axis * len, side.Cross(axis).Normalized() * len, origin, color.r, color.g, color.b, 1f, 0f, 0f, color.a, 0f);
                    break;
                }
                case Role.Arrow:
                {
                    var dir = new GVec3(right.X, 0f, right.Z);
                    if (dir.LengthSquared() < 1e-6f) dir = GVec3.Right;
                    dir = dir.Normalized();
                    float len = Mathf.Max(w, h) * 1.6f;
                    Push(batch, dir * len, Y * len, dir.Cross(Y) * len, origin + Y * (ProjectileHeight * lift), color.r, color.g, color.b, 1f, 0f, 0f, color.a, 0f);
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
                    Push(batch, bx * s, by * s, bz * s, origin + Y * ((0.4f + h01 * 0.6f) * lift), color.r, color.g, color.b, 1f, 0f, 0f, color.a, 0.3f);
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
                    Push(batch, r * radius, Y * radius, r.Cross(Y) * radius, origin + Y * (WaveHeight * lift), color.r, color.g, color.b, color.a, 0f, 0f, 0f, 0f);
                    break;
                }
                case Role.Ripple:
                {
                    // 규칙은 납작한 타원(가짜 원근)으로 그린다: 물 위에서는 폭을 지름으로 한 둥근 고리
                    Push(batch, GVec3.Right * w, Y, GVec3.Back * w, origin + Y * 0.02f, color.r, color.g, color.b, color.a, 0f, 0f, 0f, 0f);
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
            if (sprite.name == "WaterRing" || tex == "WaterRing") return Role.Ripple;
            if (sprite.name == "SlashArc") return Role.Crescent;
            if (sprite.name == "ThrustPoint") return Role.Wedge;
            if (name.StartsWith("Blade") || path.EndsWith("/Blade.png")) return Role.SpinBlade;
            if (WorldMirror.IsoField && name.StartsWith("Arrow")) return Role.Arrow;   // 야전 궁병의 화살 (코드로 만든 그림, Effect 레이어)
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
                    mesh = LowPolyMesh.HeldWeapon(kind, Vivid(blade, 1f, VividValue), Vivid(shaft), accent);
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
                case Role.Ripple: mesh = LowPolyMesh.Ripple(); break;
                default: return null;
            }

            EnsureMaterials();
            if (fxMaterial == null) fxMaterial = new ShaderMaterial { Shader = GD.Load<Godot.Shader>("res://shaders/lowpoly_fx.gdshader"), RenderPriority = 20 };
            // 외곽선은 손에 든 무기에만: 화살·보석·입자·이펙트는 선 없이 깨끗하게
            return AddBatch(key, mesh, glow ? fxMaterial : material, role == Role.Weapon ? lineMaterial : null, !glow);
        }
    }
}
