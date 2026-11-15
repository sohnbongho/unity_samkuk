using System.Collections.Generic;
using Godot;
using Samkuk.Compat;
using Samkuk.World;
using Mesh = Samkuk.Compat.Mesh;
using TextMesh = Samkuk.Compat.TextMesh;
using Mathf = Samkuk.Compat.Mathf;
using Light = Samkuk.Compat.Light;
using Shader = Samkuk.Compat.Shader;
using CColor = Samkuk.Compat.Color;
using CVec3 = Samkuk.Compat.Vector3;
using GColor = Godot.Color;
using GVec3 = Godot.Vector3;

namespace Samkuk.Game
{
    /// <summary>
    /// 장면 모형의 "세계"를 Godot 3D 노드로 비춘다 (G3, G4). 규칙 코드가 정한 그림·빛·메시·카메라를 매 프레임 읽어 같은 모양의 노드를 맞출 뿐,
    /// 게임 규칙은 여기 없다.
    ///
    /// 좌표: 로직은 x 오른쪽, y 북쪽, 땅 z = 0, 위 = -z. Godot 은 Y 가 위이므로 (x, y, z) → (x, -z, -y) (북쪽 = -Z, 카메라는 남쪽 +Z 에서 본다).
    /// 그림 한 장 = Sprite3D 하나. 재질은 유니티 판 PixelSprite 셰이더와 같은 빛 규칙(색 x (환경광 + 햇빛 x 그림자 + 점광원), 면 방향 무시)의
    /// <c>shaders/pixel_sprite_*.gdshader</c> 를 입힌다:
    ///  - 서 있는 그림(재질의 _Stand = 1): 수직으로 세우고 세로를 틸트 배율만큼 늘린다. 컷아웃이면 깊이를 쓰고 햇빛 그림자를 드리운다.
    ///  - 바닥 그림(재질의 _Stand = 0): 땅에 눕히고 그림자를 받는다. 정렬 순서는 렌더 우선순위로.
    ///  - 재질 없는 것(이펙트, 투사체, 보석): 땅에 눕힌 채 빛을 받지 않는다 (Sprite3D 기본 재질).
    /// </summary>
    public partial class WorldMirror : Node3D
    {
        sealed class SpriteView { public Sprite3D node; public int seen; }
        sealed class LightView { public Light3D node; public int seen; public bool directional; }
        sealed class MeshView { public MeshInstance3D node; public int seen; public int version = -1; public Mesh mesh; }
        sealed class TextView { public Label3D node; public int seen; }
        sealed class TiledView { public MeshInstance3D node; public int seen; }

        static readonly int StandId = Shader.PropertyToID("_Stand");
        static readonly int FootLiftId = Shader.PropertyToID("_FootLift");
        static readonly int TowardId = Shader.PropertyToID("_Toward");
        static readonly int StretchId = Shader.PropertyToID("_StretchScale");

        readonly Dictionary<SpriteRenderer, SpriteView> sprites = new Dictionary<SpriteRenderer, SpriteView>();
        readonly Dictionary<Light, LightView> lights = new Dictionary<Light, LightView>();
        readonly Dictionary<MeshFilter, MeshView> meshes = new Dictionary<MeshFilter, MeshView>();
        readonly Dictionary<TextMesh, TextView> texts = new Dictionary<TextMesh, TextView>();
        readonly Dictionary<SpriteRenderer, TiledView> tiled = new Dictionary<SpriteRenderer, TiledView>();
        // 그림 재질: (텍스처, 컷아웃?, 도트 필터?, 렌더 우선순위, UV 배율) 마다 하나
        readonly Dictionary<(Godot.Texture2D, bool, bool, int, Godot.Vector2), ShaderMaterial> spriteMaterials = new Dictionary<(Godot.Texture2D, bool, bool, int, Godot.Vector2), ShaderMaterial>();
        readonly Dictionary<Compat.Material, ShaderMaterial> meshMaterials = new Dictionary<Compat.Material, ShaderMaterial>();
        // 물(연못, 강물) 재질: (텍스처, 렌더 우선순위) 마다 하나. 물이 아닌 그림 경로는 null 을 넣어 다시 따지지 않는다
        readonly Dictionary<(Godot.Texture2D, int), ShaderMaterial> waterMaterials = new Dictionary<(Godot.Texture2D, int), ShaderMaterial>();
        readonly Dictionary<string, bool> waterPaths = new Dictionary<string, bool>();
        Godot.Shader waterShader;
        readonly Godot.Shader[] shaders = new Godot.Shader[4];   // [컷아웃?][도트?]
        readonly List<object> dead = new List<object>();
        readonly List<Component> scratch = new List<Component>();

        Camera3D camera;
        WorldEnvironment environment;
        ModelMirror models;
        int frame;

        /// <summary>서 있는 도트 그림(캐릭터·적·소품)을 로우폴리 3D 모델로 그린다 (3D 전환 1단계). 끄면 예전 도트 그림 (타이틀 [3D 화면], 점검용 --sprites).</summary>
        public static bool Models3D = true;
        /// <summary>점검용 (--dump-sprites): 화면에 나온 그림의 종류를 모은다.</summary>
        public static HashSet<string> DumpKinds;
        public ModelMirror Models => models;

        // ───────────────────────── 야전의 3D 화면 (G18) ─────────────────────────
        // 야전의 규칙·화면 코드는 2D 아이소메트릭 좌표(마름모 48x24)로 부대와 타일을 놓는다. 2:1 마름모는 정사각 칸을 30° 로 내려다본 직교 그림과 같으므로,
        // 2D 의 (x, y) 를 땅 위의 (x, 깊이 y / sin 30° = 2y) 로 옮기고 직교 카메라를 30° 기울이면 **땅 위의 점은 화면에서 예전과 똑같은 자리**에 온다.
        // 그래서 규칙 코드의 마우스 고르기·병사 수 라벨 자리는 그대로 맞고, 화면 층만 타일을 땅에 깔고 부대를 로우폴리 모델로 세운다.
        /// <summary>야전을 3D 로 그린다 (GameRoot 가 프레임마다: 야전 씬 + [3D 화면] 켬).</summary>
        public static bool IsoField;
        /// <summary>내려다보는 각 30°: sin = 0.5 → 2D 의 세로 1 이 땅의 깊이 2.</summary>
        public const float IsoDepth = 2f;
        internal const float IsoSin = 0.5f, IsoCos = 0.8660254f;
        static readonly GVec3 IsoUp = new GVec3(0f, IsoCos, -IsoSin), IsoBack = new GVec3(0f, IsoSin, IsoCos);
        DirectionalLight3D isoSun;

        /// <summary>야전 2D 좌표의 땅 위 점을 Godot 좌표로.</summary>
        public static GVec3 IsoGround(CVec3 p) => new GVec3(p.x, 0f, -p.y * IsoDepth);

        public Camera3D Camera => camera;
        public Godot.Environment Environment => environment.Environment;
        public int SpriteCount { get; private set; }

        public override void _Ready()
        {
            camera = new Camera3D { Name = "Camera", Projection = Camera3D.ProjectionType.Orthogonal, Current = true };
            AddChild(camera);
            environment = new WorldEnvironment { Name = "Environment", Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new GColor(0.07f, 0.08f, 0.1f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new GColor(1f, 1f, 1f),
                AmbientLightEnergy = 1f,
            } };
            AddChild(environment);
            models = new ModelMirror(this);
        }

        /// <summary>로직 좌표를 Godot 좌표로.</summary>
        public static GVec3 Map(CVec3 p) => new GVec3(p.x, -p.z, -p.y);

        // 그림 셰이더: 파일은 도트 필터(filter_nearest) 한 벌이고, 부드러운 그림(얼룩, 강)용으로 필터만 바꾼 변형을 만든다
        Godot.Shader ShaderFor(bool cutout, bool pixel)
        {
            int index = (cutout ? 2 : 0) + (pixel ? 1 : 0);
            if (shaders[index] != null) return shaders[index];
            var source = GD.Load<Godot.Shader>(cutout ? "res://shaders/pixel_sprite_cut.gdshader" : "res://shaders/pixel_sprite_blend.gdshader");
            if (pixel) return shaders[index] = source;
            return shaders[index] = new Godot.Shader { Code = source.Code.Replace("filter_nearest", "filter_linear") };
        }

        ShaderMaterial SpriteMaterial(Godot.Texture2D texture, bool cutout, bool pixel, int priority, Godot.Vector2 uvScale)
        {
            var key = (texture, cutout, pixel, priority, uvScale);
            if (spriteMaterials.TryGetValue(key, out var m)) return m;
            m = new ShaderMaterial { Shader = ShaderFor(cutout, pixel), RenderPriority = priority };
            m.SetShaderParameter("tex", texture);
            m.SetShaderParameter("uv_scale", uvScale);
            spriteMaterials[key] = m;
            return m;
        }

        // 물 그림인가: 연못과 강물 (강둑은 땅의 그림 그대로)
        bool IsWater(Sprite sprite)
        {
            string path = sprite.path;
            if (string.IsNullOrEmpty(path)) return false;
            if (waterPaths.TryGetValue(path, out bool water)) return water;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            water = path.Contains("/Terrain/") && (name.EndsWith("Pond") || (name.StartsWith("River_") && name.EndsWith("_Water")));
            return waterPaths[path] = water;
        }

        // 3D 화면의 물 (G16): 그림은 모양(알파)과 물빛만 주고, 물결·반짝임·물가 거품은 shaders/water.gdshader 가 세계 좌표로 그린다
        ShaderMaterial WaterMaterial(Godot.Texture2D texture, int priority, bool river)
        {
            var key = (texture, priority);
            if (waterMaterials.TryGetValue(key, out var m)) return m;
            if (waterShader == null) waterShader = GD.Load<Godot.Shader>("res://shaders/water.gdshader");
            m = new ShaderMaterial { Shader = waterShader, RenderPriority = priority };
            m.SetShaderParameter("tex", texture);
            var color = new GColor(0.25f, 0.55f, 0.85f);
            var image = texture.GetImage();
            if (image != null)
            {
                if (image.IsCompressed()) image.Decompress();
                color = ModelMirror.Dominant(image, 0, 0, image.GetWidth(), image.GetHeight(), color);
                m.SetShaderParameter("texel", new Godot.Vector2(1f / image.GetWidth(), 1f / image.GetHeight()));
            }
            m.SetShaderParameter("water_color", color);
            m.SetShaderParameter("foam_axes", river ? Godot.Vector2.Zero : Godot.Vector2.One);
            waterMaterials[key] = m;
            return m;
        }

        /// <summary>장면 모형을 읽어 노드를 맞춘다 (프레임마다).</summary>
        public void Sync()
        {
            frame++;
            int visible = 0;
            bool tilt = TiltView.Active;
            float standScale = tilt ? TiltView.StandScale : 1f;
            Compat.Camera mainCamera = null;
            models.Begin();

            var objects = SceneRuntime.Objects;
            for (int i = 0; i < objects.Count; i++)
            {
                var go = objects[i];
                if (!go.activeInHierarchy) continue;
                scratch.Clear();
                scratch.AddRange(go.components);   // 순회 중 컴포넌트가 바뀌어도 안전하게
                foreach (var c in scratch)
                {
                    switch (c)
                    {
                        case SpriteRenderer sr:
                            if (SyncSprite(sr, standScale)) visible++;
                            break;
                        case Light light:
                            if (light.enabled) SyncLight(light);
                            break;
                        case MeshFilter mf:
                            SyncMesh(mf);
                            break;
                        case TextMesh tm:
                            SyncText(tm);
                            break;
                        case Compat.Camera cam:
                            if (cam.enabled && (mainCamera == null || go.tag == "MainCamera")) mainCamera = cam;
                            break;
                    }
                }
            }
            SpriteCount = visible;
            if (Models3D && (tilt || IsoField)) models.End(); else models.Hide();
            SyncIsoSun();

            Sweep(sprites, v => v.seen, v => v.node);
            Sweep(lights, v => v.seen, v => v.node);
            Sweep(meshes, v => v.seen, v => v.node);
            Sweep(texts, v => v.seen, v => v.node);
            Sweep(tiled, v => v.seen, v => v.node);

            SyncCamera(mainCamera);
            SyncEnvironment(mainCamera);
        }

        // 이번 프레임에 안 보인 것: 파괴됐으면 노드를 치우고, 꺼졌으면 숨긴다
        void Sweep<TKey, TView>(Dictionary<TKey, TView> map, System.Func<TView, int> seen, System.Func<TView, Node3D> node) where TKey : Compat.Object
        {
            dead.Clear();
            foreach (var kv in map)
            {
                if (seen(kv.Value) == frame) continue;
                if (kv.Key.destroyed) { node(kv.Value).QueueFree(); dead.Add(kv.Key); }
                else node(kv.Value).Visible = false;
            }
            foreach (var k in dead) map.Remove((TKey)k);
        }

        bool SyncSprite(SpriteRenderer sr, float standScale)
        {
            if (!sprites.TryGetValue(sr, out var view))
            {
                view = new SpriteView { node = new Sprite3D { Centered = true, DoubleSided = true } };
                AddChild(view.node);
                sprites[sr] = view;
            }
            view.seen = frame;
            var node = view.node;
            var sprite = sr.sprite;
            if (!sr.enabled || sprite == null || sr.color.a <= 0.001f) { node.Visible = false; return false; }
            var texture = Textures.Of(sprite);
            if (texture == null) { node.Visible = false; return false; }
            bool pixel = sprite.texture != null && sprite.texture.filterMode == FilterMode.Point;

            if (sr.drawMode == SpriteDrawMode.Tiled)
            {
                node.Visible = false;
                SyncTiled(sr, sprite, texture, pixel);
                return true;
            }
            if (tiled.TryGetValue(sr, out var oldTiled)) oldTiled.node.Visible = false;

            string dumpKind = null;
            if (DumpKinds != null)
            {
                var m0 = sr.sharedMaterial;
                string kind = $"{sr.gameObject.name} | sprite={sprite.name} tex={(sprite.path ?? sprite.texture?.name)} {sprite.rect.width}x{sprite.rect.height} ppu{sprite.pixelsPerUnit} | layer={sr.sortingLayerName} stand={(m0 != null && m0.shader != null && m0.shader.name == TiltView.ShaderName && m0.GetFloat(StandId) > 0.5f)} parent={(sr.transform.parent != null ? sr.transform.parent.name : "-")}";
                dumpKind = kind;
            }
            node.Visible = true;
            if (node.Texture != texture) node.Texture = texture;
            var rect = sprite.rect;
            bool whole = sprite.texture != null && rect.x == 0f && rect.y == 0f && rect.width == sprite.texture.width && rect.height == sprite.texture.height;
            node.RegionEnabled = !whole;
            if (!whole) node.RegionRect = Textures.Region(sprite);
            node.PixelSize = 1f / Mathf.Max(0.01f, sprite.pixelsPerUnit);
            node.FlipH = sr.flipX;
            node.FlipV = sr.flipY;
            // 피벗이 노드 자리에 오게 (뒤집으면 피벗 기준으로 뒤집힌다)
            float ox = rect.width * 0.5f - sprite.pivot.x, oy = rect.height * 0.5f - sprite.pivot.y;
            node.Offset = new Godot.Vector2(sr.flipX ? -ox : ox, sr.flipY ? -oy : oy);
            node.Modulate = sr.color.ToGodot();
            node.TextureFilter = pixel ? BaseMaterial3D.TextureFilterEnum.Nearest : BaseMaterial3D.TextureFilterEnum.Linear;

            var t = sr.transform;
            var pos = t.position;
            var scale = t.lossyScale;
            if (IsoField)
            {
                bool model = SyncIsoSprite(sr, sprite, node, texture, pixel);
                if (dumpKind != null) DumpKinds.Add((model ? "3D " : "2D ") + dumpKind);
                return true;
            }
            node.NoDepthTest = false;
            var material = sr.sharedMaterial;
            bool pixelShader = material != null && material.shader != null && material.shader.name == TiltView.ShaderName;
            bool standing = pixelShader && material.GetFloat(StandId) > 0.5f;
            int layer = Mathf.Max(0, SortingLayer.Names.IndexOf(sr.sortingLayerName));

            if (standing)
            {
                bool cutout = material.renderQueue < (int)RenderQueue.Transparent;
                // 3D 모델로 바꿔 그리는 그림(캐릭터·적·소품): 도트 그림은 숨기고 인스턴스 하나를 더한다
                if (Models3D && (models.TryDraw(sr, sprite) || models.TryDrawEffect(sr, sprite))) { node.Visible = false; if (dumpKind != null) DumpKinds.Add("3D " + dumpKind); return true; }   // 반투명 재질(흐려진 소품, 무적 깜빡임, 쓰러진 아군)도 모델로: 불투명도는 점묘로 그린다
                float stretch = material.GetFloat(StretchId) > 0f ? material.GetFloat(StretchId) : 1f;
                float vertical = scale.y * standScale * stretch;
                float lift = material.GetFloat(FootLiftId) * vertical;
                float toward = material.GetFloat(TowardId);
                node.Position = new GVec3(pos.x, -pos.z + lift, -pos.y + toward);
                float angle = t.rotation.eulerAngles.z * Mathf.Deg2Rad;
                node.Basis = new Basis(GVec3.Back, angle).Scaled(new GVec3(scale.x, vertical, 1f));
                node.MaterialOverride = SpriteMaterial(texture, cutout, pixel, cutout ? 0 : 100, Godot.Vector2.One);
                node.CastShadow = cutout ? GeometryInstance3D.ShadowCastingSetting.DoubleSided : GeometryInstance3D.ShadowCastingSetting.Off;
            }
            else if (Models3D && TiltView.Active && models.TryDrawEffect(sr, sprite))
            {
                // 탄환·보석·이펙트: 3D 메시로 그렸다 (3D 전환 3단계)
                node.Visible = false;
                if (dumpKind != null) DumpKinds.Add("3D " + dumpKind);
                return true;
            }
            else
            {
                // 눕힌 그림: 로직의 x-y 평면이 땅. 정렬 순서가 높을수록 살짝 위 + 나중에 그린다
                float order = layer * 0.004f + Mathf.Clamp(sr.sortingOrder, -20, 200) * 0.00004f;
                node.Position = new GVec3(pos.x, -pos.z + order, -pos.y);
                var q = t.rotation;
                node.Basis = new Basis(Map(q * CVec3.right) * scale.x, Map(q * CVec3.up) * scale.y, Map(q * CVec3.back));
                int priority = Mathf.Clamp(layer * 15 + Mathf.Clamp(sr.sortingOrder, 0, 14) - 60, -128, 127);
                node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                if (Models3D && TiltView.Active && IsWater(sprite)) node.MaterialOverride = WaterMaterial(texture, priority, System.IO.Path.GetFileName(sprite.path).StartsWith("River_"));   // 물: 움직이는 물 표면
                else if (pixelShader) node.MaterialOverride = SpriteMaterial(texture, false, pixel, priority, Godot.Vector2.One);   // 바닥 그림: 빛과 그림자를 받는다
                else
                {
                    // 이펙트·투사체·보석: 빛을 받지 않는 기본 그림
                    node.MaterialOverride = null;
                    node.Shaded = false;
                    node.AlphaCut = SpriteBase3D.AlphaCutMode.Disabled;
                    node.RenderPriority = priority;
                }
            }
            if (dumpKind != null) DumpKinds.Add("2D " + dumpKind);
            return true;
        }

        // 야전의 그림 한 장 (3D 화면). 정렬 레이어가 역할을 알려 준다: World = 부대(모델로), Background = 타일·사거리·선택 테(땅에 깐다),
        // 그 밖(Effect) = 장수의 무기·검기·화살·타격 입자 → 3D 메시로(ModelMirror.TryDrawEffect: 야전의 이펙트는 모두 손 높이에서 나온다).
        // 역할을 모르는 그림만 카메라를 향한 그림으로 같은 화면 자리에 깊이 검사 없이 그린다. 모델·메시로 그렸으면 true.
        bool SyncIsoSprite(SpriteRenderer sr, Sprite sprite, Sprite3D node, Godot.Texture2D texture, bool pixel)
        {
            var t = sr.transform;
            var pos = t.position;
            var scale = t.lossyScale;
            var q = t.rotation;
            string layer = sr.sortingLayerName;
            if (layer == Samkuk.Core.GameLayers.Sorting.World && models.TryDraw(sr, sprite)) { node.Visible = false; return true; }

            var right = q * CVec3.right;
            var up = q * CVec3.up;
            int order = Mathf.Clamp(sr.sortingOrder, 0, 14);
            if (layer == Samkuk.Core.GameLayers.Sorting.Background)
            {
                var ground = IsoGround(pos);
                node.Position = new GVec3(ground.X, 0.002f * (order + 1), ground.Z);
                node.Basis = new Basis(IsoGround(right) * scale.x, IsoGround(up) * scale.y, GVec3.Up);
                node.NoDepthTest = false;
                node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                // 타일(순서 0, 불투명한 마름모)은 컷아웃 재질: 빛과 부대 그림자를 받는다. 사거리·선택 테는 반투명으로 그 위에
                bool tile = sr.sortingOrder == 0 && sr.color.a >= 0.999f;
                node.MaterialOverride = SpriteMaterial(texture, tile, pixel, tile ? 0 : order - 60, Godot.Vector2.One);
                return false;
            }
            if (layer != Samkuk.Core.GameLayers.Sorting.World && models.TryDrawEffect(sr, sprite)) { node.Visible = false; return true; }
            // 카메라를 향한 그림: 2D 평면의 오른쪽·위가 카메라의 오른쪽·위
            node.Position = IsoGround(pos) + IsoBack * 3f;
            node.Basis = new Basis((GVec3.Right * right.x + IsoUp * right.y) * scale.x, (GVec3.Right * up.x + IsoUp * up.y) * scale.y, IsoBack);
            node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            node.MaterialOverride = null;
            node.Shaded = false;
            node.AlphaCut = SpriteBase3D.AlphaCutMode.Disabled;
            node.NoDepthTest = true;
            node.RenderPriority = 100 + order;
            return false;
        }

        // 야전의 햇빛: 씬에는 2D 조명뿐이라 화면 층이 방향광 하나를 둔다 (모델의 음영과 땅에 지는 그림자)
        void SyncIsoSun()
        {
            if (!IsoField) { if (isoSun != null) isoSun.Visible = false; return; }
            if (isoSun == null)
            {
                isoSun = new DirectionalLight3D
                {
                    Name = "IsoSun", DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal, DirectionalShadowMaxDistance = 80f, ShadowBlur = 0f,
                    ShadowEnabled = true, LightEnergy = 0.65f, LightColor = new GColor(1f, 0.97f, 0.9f),
                };
                AddChild(isoSun);
                isoSun.Basis = Basis.LookingAt(new GVec3(0.55f, -1f, -0.3f).Normalized(), GVec3.Up);
            }
            isoSun.Visible = true;
        }

        // 타일처럼 이어 붙이는 그림(바닥 배경): 큰 사각형 한 장에 그림을 반복해 입힌다
        void SyncTiled(SpriteRenderer sr, Sprite sprite, Godot.Texture2D texture, bool pixel)
        {
            if (!tiled.TryGetValue(sr, out var view))
            {
                view = new TiledView { node = new MeshInstance3D { Mesh = new QuadMesh(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off } };
                AddChild(view.node);
                tiled[sr] = view;
            }
            view.seen = frame;
            var t = sr.transform;
            var scale = t.lossyScale;
            float w = sr.size.x * scale.x, h = sr.size.y * scale.y;
            float tileW = sprite.rect.width / sprite.pixelsPerUnit, tileH = sprite.rect.height / sprite.pixelsPerUnit;
            view.node.Visible = true;
            var uvScale = new Godot.Vector2(Mathf.Round(w / Mathf.Max(0.01f, tileW) * 1000f) / 1000f, Mathf.Round(h / Mathf.Max(0.01f, tileH) * 1000f) / 1000f);
            view.node.MaterialOverride = SpriteMaterial(texture, true, pixel, 0, uvScale);
            var pos = t.position;
            view.node.Position = new GVec3(pos.x, -pos.z, -pos.y);
            var q = t.rotation;
            view.node.Basis = new Basis(Map(q * CVec3.right) * w, Map(q * CVec3.up) * h, Map(q * CVec3.back));
        }

        void SyncLight(Light light)
        {
            bool directional = light.type == LightType.Directional;
            if (!lights.TryGetValue(light, out var view) || view.directional != directional)
            {
                view?.node.QueueFree();
                Light3D node = directional
                    ? new DirectionalLight3D { DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal, DirectionalShadowMaxDistance = 50f, ShadowBlur = 0f }
                    : new OmniLight3D();
                view = new LightView { node = node, directional = directional };
                AddChild(node);
                lights[light] = view;
            }
            view.seen = frame;
            var n = view.node;
            n.Visible = true;
            n.LightColor = new GColor(light.color.r, light.color.g, light.color.b);
            n.LightEnergy = light.intensity;
            n.ShadowEnabled = light.shadows != LightShadows.None;
            if (directional)
            {
                var dir = light.direction.sqrMagnitude > 1e-8f ? light.direction.normalized : light.transform.rotation * CVec3.forward;
                var g = Map(dir).Normalized();
                var up = Mathf.Abs(g.Y) > 0.99f ? GVec3.Forward : GVec3.Up;
                n.Basis = Basis.LookingAt(g, up);
            }
            else
            {
                n.Position = Map(light.transform.position);
                ((OmniLight3D)n).OmniRange = Mathf.Max(0.1f, light.range);
            }
        }

        void SyncMesh(MeshFilter mf)
        {
            var mesh = mf.sharedMesh;
            var renderer = mf.GetComponent<MeshRenderer>();
            if (!meshes.TryGetValue(mf, out var view))
            {
                view = new MeshView { node = new MeshInstance3D() };
                AddChild(view.node);
                meshes[mf] = view;
            }
            view.seen = frame;
            if (mesh == null || renderer == null || !renderer.enabled || mesh.vertexCount == 0) { view.node.Visible = false; return; }
            view.node.Visible = true;
            view.node.Position = Map(mf.transform.position);
            view.node.CastShadow = renderer.shadowCastingMode == ShadowCastingMode.Off ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.DoubleSided;
            if (view.mesh == mesh && view.version == mesh.version) return;
            view.mesh = mesh;
            view.version = mesh.version;

            var array = new ArrayMesh();
            var verts = new GVec3[mesh.vertexCount];
            var uvs = new Godot.Vector2[mesh.vertexCount];
            var colors = new GColor[mesh.vertexCount];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = Map(mesh.vertices[i]);
                var uv = i < mesh.uv.Count ? mesh.uv[i] : Compat.Vector2.zero;
                uvs[i] = new Godot.Vector2(uv.x, 1f - uv.y);   // 유니티 UV 는 아래가 0, Godot 은 위가 0
                colors[i] = i < mesh.colors32.Count ? ((CColor)mesh.colors32[i]).ToGodot() : new GColor(1f, 1f, 1f);
            }
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                if (tris.Count == 0) continue;
                var arrays = new Godot.Collections.Array();
                arrays.Resize((int)Godot.Mesh.ArrayType.Max);
                arrays[(int)Godot.Mesh.ArrayType.Vertex] = verts;
                arrays[(int)Godot.Mesh.ArrayType.TexUV] = uvs;
                arrays[(int)Godot.Mesh.ArrayType.Color] = colors;
                arrays[(int)Godot.Mesh.ArrayType.Index] = tris.ToArray();
                array.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
                var mat = s < renderer.sharedMaterials.Length ? renderer.sharedMaterials[s] : null;
                array.SurfaceSetMaterial(array.GetSurfaceCount() - 1, MeshMaterial(mat));
            }
            view.node.Mesh = array;   // 빛은 면 방향을 보지 않으므로 법선이 필요 없다
        }

        ShaderMaterial MeshMaterial(Compat.Material material)
        {
            if (material == null) return null;
            if (meshMaterials.TryGetValue(material, out var cached)) return cached;
            ShaderMaterial m = null;
            if (material.mainTexture is Compat.Texture2D tex)
            {
                var texture = Textures.Of(tex);
                bool cutout = material.renderQueue < (int)RenderQueue.Transparent;
                if (texture != null) m = SpriteMaterial(texture, cutout, tex.filterMode == FilterMode.Point, 0, Godot.Vector2.One);
            }
            meshMaterials[material] = m;
            return m;
        }

        void SyncText(TextMesh tm)
        {
            if (!texts.TryGetValue(tm, out var view))
            {
                view = new TextView { node = new Label3D { DoubleSided = true, Shaded = false, NoDepthTest = true, RenderPriority = 120, OutlineSize = 6, Font = Fonts.Default } };
                AddChild(view.node);
                texts[tm] = view;
            }
            view.seen = frame;
            var node = view.node;
            var renderer = tm.GetComponent<MeshRenderer>();
            if ((renderer != null && !renderer.enabled) || string.IsNullOrEmpty(tm.text) || tm.color.a <= 0.001f) { node.Visible = false; return; }
            node.Visible = true;
            if (node.Text != tm.text) node.Text = tm.text;
            node.Modulate = tm.color.ToGodot();
            node.OutlineModulate = new GColor(0f, 0f, 0f, tm.color.a);
            node.FontSize = Mathf.Max(8, tm.fontSize);
            // 유니티 TextMesh: 글자 높이(유닛) ≈ characterSize x fontSize / 10
            node.PixelSize = tm.characterSize * 0.1f;
            var t = tm.transform;
            var scale = t.lossyScale;
            node.Position = Map(t.position);
            var q = t.rotation;
            node.Basis = new Basis(Map(q * CVec3.right) * scale.x, Map(q * CVec3.up) * scale.y, Map(q * CVec3.back));
        }

        void SyncCamera(Compat.Camera cam)
        {
            if (cam == null) return;
            var t = cam.transform;
            if (IsoField)
            {
                // 야전: 2D 카메라가 보던 곳을 30° 기울인 직교 카메라로 (크기는 같다 → 땅 위의 점이 화면에서 같은 자리)
                camera.Basis = new Basis(GVec3.Right, IsoUp, IsoBack);
                camera.Position = IsoGround(t.position) + IsoBack * 40f;
                camera.Projection = Camera3D.ProjectionType.Orthogonal;
                camera.Size = cam.orthographicSize * 2f;
                camera.KeepAspect = Camera3D.KeepAspectEnum.Height;
                camera.Near = 0.5f;
                camera.Far = 120f;
                return;
            }
            var q = t.rotation;
            // Godot 카메라는 -Z 를 본다: 로직의 보는 방향(+z)을 뒤집어 넣는다
            var basis = new Basis(Map(q * CVec3.right), Map(q * CVec3.up), -Map(q * CVec3.forward));
            camera.Basis = basis.Orthonormalized();
            camera.Position = Map(t.position);
            camera.Projection = cam.orthographic ? Camera3D.ProjectionType.Orthogonal : Camera3D.ProjectionType.Perspective;
            camera.Size = cam.orthographicSize * 2f;   // Godot 은 전체 높이, 로직은 반높이
            camera.KeepAspect = Camera3D.KeepAspectEnum.Height;
            camera.Fov = Mathf.Clamp(cam.fieldOfView, 1f, 179f);   // 세로 화각
            camera.Near = Mathf.Max(0.05f, cam.nearClipPlane);
            camera.Far = Mathf.Max(camera.Near + 1f, cam.farClipPlane);
        }

        void SyncEnvironment(Compat.Camera cam)
        {
            var env = environment.Environment;
            if (cam != null) env.BackgroundColor = cam.backgroundColor.ToGodot();
            // 빛이 없는 장면(타이틀, 내정, 맵 편집기)은 그림 색 그대로 보이게 환경광을 1 로, 전투는 규칙 코드가 정한 환경광
            bool lit = lights.Count > 0;
            var a = RenderSettings.ambientLight;
            if (IsoField) { env.AmbientLightColor = new GColor(0.5f, 0.52f, 0.56f); env.AmbientLightEnergy = 1f; models.SetAmbient(env.AmbientLightColor); return; }   // 야전의 3D 화면: 화면 층의 햇빛 + 이 환경광
            env.AmbientLightColor = lit ? new GColor(a.r, a.g, a.b) : new GColor(1f, 1f, 1f);
            env.AmbientLightEnergy = 1f;
            models.SetAmbient(env.AmbientLightColor);   // 카툰 재질은 엔진의 환경광 대신 이 값을 직접 더한다
        }
    }
}
