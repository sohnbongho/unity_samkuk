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
using CQuat = Samkuk.Compat.Quaternion;
using GColor = Godot.Color;
using GVec3 = Godot.Vector3;

namespace Samkuk.Game
{
    /// <summary>
    /// 장면 모형의 "세계"를 Godot 3D 노드로 비춘다 (G3). 규칙 코드가 정한 그림·빛·메시·카메라를 매 프레임 읽어 같은 모양의 노드를 맞출 뿐,
    /// 게임 규칙은 여기 없다.
    ///
    /// 좌표: 로직은 x 오른쪽, y 북쪽, 땅 z = 0, 위 = -z. Godot 은 Y 가 위이므로 (x, y, z) → (x, -z, -y) (북쪽 = -Z, 카메라는 남쪽 +Z 에서 본다).
    /// 그림 한 장 = Sprite3D 하나:
    ///  - 서 있는 그림(재질의 _Stand = 1): 수직으로 세우고 세로를 틸트 배율만큼 늘린다. 컷아웃이면 깊이를 쓰고 햇빛 그림자를 드리운다.
    ///  - 바닥 그림(재질의 _Stand = 0): 땅에 눕히고 그림자를 받는다. 정렬 순서는 렌더 우선순위로.
    ///  - 재질 없는 것(이펙트, 투사체, 보석): 땅에 눕힌 채 빛을 받지 않는다.
    /// </summary>
    public partial class WorldMirror : Node3D
    {
        sealed class SpriteView { public Sprite3D node; public int seen; }
        sealed class LightView { public Light3D node; public int seen; public bool directional; }
        sealed class MeshView { public MeshInstance3D node; public int seen; public int version = -1; public Mesh mesh; }
        sealed class TextView { public Label3D node; public int seen; }
        sealed class TiledView { public MeshInstance3D node; public int seen; public StandardMaterial3D material; }

        static readonly int StandId = Shader.PropertyToID("_Stand");
        static readonly int FootLiftId = Shader.PropertyToID("_FootLift");
        static readonly int TowardId = Shader.PropertyToID("_Toward");
        static readonly int StretchId = Shader.PropertyToID("_StretchScale");

        readonly Dictionary<SpriteRenderer, SpriteView> sprites = new Dictionary<SpriteRenderer, SpriteView>();
        readonly Dictionary<Light, LightView> lights = new Dictionary<Light, LightView>();
        readonly Dictionary<MeshFilter, MeshView> meshes = new Dictionary<MeshFilter, MeshView>();
        readonly Dictionary<TextMesh, TextView> texts = new Dictionary<TextMesh, TextView>();
        readonly Dictionary<SpriteRenderer, TiledView> tiled = new Dictionary<SpriteRenderer, TiledView>();
        readonly Dictionary<Compat.Material, StandardMaterial3D> materials = new Dictionary<Compat.Material, StandardMaterial3D>();
        readonly List<object> dead = new List<object>();
        readonly List<Component> scratch = new List<Component>();

        Camera3D camera;
        WorldEnvironment environment;
        int frame;

        public Camera3D Camera => camera;
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
        }

        /// <summary>로직 좌표를 Godot 좌표로.</summary>
        public static GVec3 Map(CVec3 p) => new GVec3(p.x, -p.z, -p.y);

        /// <summary>장면 모형을 읽어 노드를 맞춘다 (프레임마다).</summary>
        public void Sync()
        {
            frame++;
            int visible = 0;
            bool tilt = TiltView.Active;
            float standScale = tilt ? TiltView.StandScale : 1f;
            Compat.Camera mainCamera = null;

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
                            if (SyncSprite(sr, tilt, standScale)) visible++;
                            break;
                        case Light light:
                            if (light.enabled) SyncLight(light);
                            break;
                        case MeshFilter mf:
                            SyncMesh(mf);
                            break;
                        case TextMesh tm:
                            SyncText(tm, tilt, standScale);
                            break;
                        case Compat.Camera cam:
                            if (cam.enabled && (mainCamera == null || go.tag == "MainCamera")) mainCamera = cam;
                            break;
                    }
                }
            }
            SpriteCount = visible;

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

        bool SyncSprite(SpriteRenderer sr, bool tilt, float standScale)
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

            if (sr.drawMode == SpriteDrawMode.Tiled)
            {
                node.Visible = false;
                SyncTiled(sr, sprite, texture);
                return true;
            }
            if (tiled.TryGetValue(sr, out var oldTiled)) oldTiled.node.Visible = false;

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
            node.TextureFilter = sprite.texture != null && sprite.texture.filterMode == FilterMode.Point
                ? BaseMaterial3D.TextureFilterEnum.Nearest : BaseMaterial3D.TextureFilterEnum.Linear;

            var t = sr.transform;
            var pos = t.position;
            var scale = t.lossyScale;
            var material = sr.sharedMaterial;
            bool pixelShader = material != null && material.shader != null && material.shader.name == TiltView.ShaderName;
            bool standing = pixelShader && material.GetFloat(StandId) > 0.5f;
            int layer = Mathf.Max(0, SortingLayer.Names.IndexOf(sr.sortingLayerName));

            if (standing)
            {
                bool cutout = material.renderQueue < (int)RenderQueue.Transparent;
                float stretch = material.GetFloat(StretchId) > 0f ? material.GetFloat(StretchId) : 1f;
                float vertical = scale.y * standScale * stretch;
                float lift = material.GetFloat(FootLiftId) * vertical;
                float toward = material.GetFloat(TowardId);
                node.Position = new GVec3(pos.x, -pos.z + lift, -pos.y + toward);
                float angle = t.rotation.eulerAngles.z * Mathf.Deg2Rad;
                node.Basis = new Basis(GVec3.Back, angle).Scaled(new GVec3(scale.x, vertical, 1f));
                node.Shaded = true;
                node.AlphaCut = cutout ? SpriteBase3D.AlphaCutMode.Discard : SpriteBase3D.AlphaCutMode.Disabled;
                node.CastShadow = cutout ? GeometryInstance3D.ShadowCastingSetting.DoubleSided : GeometryInstance3D.ShadowCastingSetting.Off;
                node.RenderPriority = cutout ? 0 : 100;
            }
            else
            {
                // 눕힌 그림: 로직의 x-y 평면이 땅. 정렬 순서가 높을수록 살짝 위 + 나중에 그린다
                float order = layer * 0.004f + Mathf.Clamp(sr.sortingOrder, -20, 200) * 0.00004f;
                node.Position = new GVec3(pos.x, -pos.z + order, -pos.y);
                var q = t.rotation;
                var bx = Map(q * CVec3.right) * scale.x;
                var by = Map(q * CVec3.up) * scale.y;
                var bz = Map(q * CVec3.back);
                node.Basis = new Basis(bx, by, bz);
                node.Shaded = pixelShader;   // 바닥 그림은 빛과 그림자를 받고, 이펙트는 받지 않는다
                node.AlphaCut = SpriteBase3D.AlphaCutMode.Disabled;
                node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                node.RenderPriority = Mathf.Clamp(layer * 15 + Mathf.Clamp(sr.sortingOrder, 0, 14) - 60, -128, 127);
            }
            return true;
        }

        // 타일처럼 이어 붙이는 그림(바닥 배경): 큰 사각형 한 장에 그림을 반복해 입힌다
        void SyncTiled(SpriteRenderer sr, Sprite sprite, Godot.Texture2D texture)
        {
            if (!tiled.TryGetValue(sr, out var view))
            {
                var material = new StandardMaterial3D
                {
                    TextureRepeat = true, Roughness = 1f, SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                };
                view = new TiledView { node = new MeshInstance3D { Mesh = new QuadMesh(), MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }, material = material };
                AddChild(view.node);
                tiled[sr] = view;
            }
            view.seen = frame;
            var t = sr.transform;
            var scale = t.lossyScale;
            float w = sr.size.x * scale.x, h = sr.size.y * scale.y;
            float tileW = sprite.rect.width / sprite.pixelsPerUnit, tileH = sprite.rect.height / sprite.pixelsPerUnit;
            view.node.Visible = true;
            var m = view.material;
            if (m.AlbedoTexture != texture) m.AlbedoTexture = texture;
            m.TextureFilter = sprite.texture != null && sprite.texture.filterMode == FilterMode.Point ? BaseMaterial3D.TextureFilterEnum.Nearest : BaseMaterial3D.TextureFilterEnum.Linear;
            m.AlbedoColor = sr.color.ToGodot();
            m.Uv1Scale = new GVec3(w / Mathf.Max(0.01f, tileW), h / Mathf.Max(0.01f, tileH), 1f);
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
                    ? new DirectionalLight3D { DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal, DirectionalShadowMaxDistance = 40f }
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
                var st = new SurfaceTool();
                array.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
                int surface = array.GetSurfaceCount() - 1;
                var mat = s < renderer.sharedMaterials.Length ? renderer.sharedMaterials[s] : null;
                array.SurfaceSetMaterial(surface, MaterialOf(mat));
            }
            // 법선: 면마다 평평하게 (절벽은 각진 면이라 충분하다)
            var tool = new SurfaceTool();
            var smooth = new ArrayMesh();
            for (int s = 0; s < array.GetSurfaceCount(); s++)
            {
                tool.Clear();
                tool.CreateFrom(array, s);
                tool.GenerateNormals();
                tool.Commit(smooth);
                smooth.SurfaceSetMaterial(s, array.SurfaceGetMaterial(s));
            }
            view.node.Mesh = smooth;
        }

        StandardMaterial3D MaterialOf(Compat.Material material)
        {
            if (material == null) return null;
            if (materials.TryGetValue(material, out var cached)) return cached;
            var m = new StandardMaterial3D
            {
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                TextureRepeat = true,
                VertexColorUseAsAlbedo = true,
                Roughness = 1f,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            };
            if (material.mainTexture is Compat.Texture2D tex) m.AlbedoTexture = Textures.Of(tex);
            // 그림의 투명한 부분은 잘라 낸다 (흙길 가장자리). 반투명 재질만 섞는다
            if (material.renderQueue >= (int)RenderQueue.Transparent) m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            else { m.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor; m.AlphaScissorThreshold = 0.5f; }
            materials[material] = m;
            return m;
        }

        void SyncText(TextMesh tm, bool tilt, float standScale)
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
            var q = t.rotation;
            // Godot 카메라는 -Z 를 본다: 로직의 보는 방향(+z)을 뒤집어 넣는다
            var basis = new Basis(Map(q * CVec3.right), Map(q * CVec3.up), -Map(q * CVec3.forward));
            camera.Basis = basis.Orthonormalized();
            camera.Position = Map(t.position);
            camera.Projection = cam.orthographic ? Camera3D.ProjectionType.Orthogonal : Camera3D.ProjectionType.Perspective;
            camera.Size = cam.orthographicSize * 2f;   // Godot 은 전체 높이, 로직은 반높이
            camera.Near = Mathf.Max(0.05f, cam.nearClipPlane);
            camera.Far = Mathf.Max(camera.Near + 1f, cam.farClipPlane);
        }

        void SyncEnvironment(Compat.Camera cam)
        {
            var env = environment.Environment;
            if (cam != null) env.BackgroundColor = cam.backgroundColor.ToGodot();
            // 빛이 없는 장면(타이틀, 내정)은 그림 색 그대로 보이게 환경광을 1 로, 전투는 규칙 코드가 정한 환경광
            bool lit = lights.Count > 0;
            var a = RenderSettings.ambientLight;
            env.AmbientLightColor = lit ? new GColor(a.r, a.g, a.b) : new GColor(1f, 1f, 1f);
            env.AmbientLightEnergy = 1f;
        }
    }
}
