using System;
using System.Collections.Generic;

namespace Samkuk.Compat
{
    // 그리기에 대한 "표시 데이터" (G3/G4). 규칙 코드가 유니티 때처럼 재질·빛·메시를 정해 두면
    // 화면 층(Godot)이 그 값을 읽어 자기 방식(셰이더, 3D 빛, ArrayMesh)으로 그린다. 여기서는 아무것도 그리지 않는다.

    [Serializable]
    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Vector4 zero => new Vector4(0f, 0f, 0f, 0f);
        public static implicit operator Vector4(Vector3 v) => new Vector4(v.x, v.y, v.z, 0f);
        public static implicit operator Vector3(Vector4 v) => new Vector3(v.x, v.y, v.z);
        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2}, {w:F2})";
    }

    /// <summary>셰이더 이름표. 화면 층이 이름으로 Godot 셰이더를 고른다.</summary>
    public sealed class Shader : Object
    {
        public readonly string name;
        Shader(string name) { this.name = name; }

        static readonly Dictionary<string, Shader> shaders = new Dictionary<string, Shader>();
        static readonly Dictionary<string, int> ids = new Dictionary<string, int>();
        static readonly List<string> names = new List<string>();
        static readonly Dictionary<int, Vector4> globals = new Dictionary<int, Vector4>();
        /// <summary>테스트용: 없는 것으로 칠 셰이더 이름 (셰이더가 없을 때의 대체 동작 확인).</summary>
        public static readonly HashSet<string> Missing = new HashSet<string>();

        public static Shader Find(string name)
        {
            if (string.IsNullOrEmpty(name) || Missing.Contains(name)) return null;
            if (!shaders.TryGetValue(name, out var s)) shaders[name] = s = new Shader(name);
            return s;
        }

        public static int PropertyToID(string property)
        {
            if (!ids.TryGetValue(property, out int id)) { ids[property] = id = names.Count + 1; names.Add(property); }
            return id;
        }
        public static string PropertyName(int id) => id >= 1 && id <= names.Count ? names[id - 1] : null;

        public static void SetGlobalVector(int id, Vector4 value) => globals[id] = value;
        public static Vector4 GetGlobalVector(int id) => globals.TryGetValue(id, out var v) ? v : Vector4.zero;
        public static void SetGlobalFloat(int id, float value) => globals[id] = new Vector4(value, 0f, 0f, 0f);
        public static float GetGlobalFloat(int id) => GetGlobalVector(id).x;
    }

    public enum BlendMode { Zero = 0, One = 1, SrcAlpha = 5, OneMinusSrcAlpha = 10 }
    public enum RenderQueue { Background = 1000, Geometry = 2000, AlphaTest = 2450, Transparent = 3000, Overlay = 4000 }
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum SpriteSortPoint { Center, Pivot }

    /// <summary>재질: 셰이더 이름표 + 값들. 화면 층이 값(세움, 발 높이, 반투명 등)을 보고 Godot 재질을 만든다.</summary>
    public sealed class Material : Object
    {
        public string name;
        public Shader shader;
        public Texture mainTexture;
        public Color color = Color.white;
        public int renderQueue = (int)RenderQueue.Geometry;
        readonly Dictionary<int, float> floats = new Dictionary<int, float>();
        readonly Dictionary<int, Vector4> vectors = new Dictionary<int, Vector4>();
        readonly HashSet<string> disabledPasses = new HashSet<string>();
        /// <summary>값이 바뀔 때마다 오른다.</summary>
        public int version;

        public Material(Shader shader) { this.shader = shader; }
        public Material(Material source)
        {
            shader = source.shader; mainTexture = source.mainTexture; color = source.color; renderQueue = source.renderQueue; name = source.name;
            foreach (var kv in source.floats) floats[kv.Key] = kv.Value;
            foreach (var kv in source.vectors) vectors[kv.Key] = kv.Value;
        }

        public void SetFloat(int id, float value) { floats[id] = value; version++; }
        public void SetFloat(string property, float value) => SetFloat(Shader.PropertyToID(property), value);
        public float GetFloat(int id) => floats.TryGetValue(id, out float v) ? v : 0f;
        public float GetFloat(string property) => GetFloat(Shader.PropertyToID(property));
        public bool HasProperty(int id) => floats.ContainsKey(id) || vectors.ContainsKey(id);
        public bool HasProperty(string property) => HasProperty(Shader.PropertyToID(property));
        public void SetVector(int id, Vector4 value) { vectors[id] = value; version++; }
        public Vector4 GetVector(int id) => vectors.TryGetValue(id, out var v) ? v : Vector4.zero;
        public void SetColor(int id, Color value) => SetVector(id, new Vector4(value.r, value.g, value.b, value.a));
        public void SetColor(string property, Color value) => SetColor(Shader.PropertyToID(property), value);
        public void SetShaderPassEnabled(string pass, bool enabled) { if (enabled) disabledPasses.Remove(pass); else disabledPasses.Add(pass); }
        public bool GetShaderPassEnabled(string pass) => !disabledPasses.Contains(pass);
        public override string ToString() => name ?? shader?.name ?? "Material";
    }

    /// <summary>렌더러 하나에만 얹는 값 (유니티에서는 배칭을 끊는 용도였다. 여기서는 값만 든다).</summary>
    public sealed class MaterialPropertyBlock
    {
        internal readonly Dictionary<int, float> floats = new Dictionary<int, float>();
        public void SetFloat(int id, float value) => floats[id] = value;
        public float GetFloat(int id) => floats.TryGetValue(id, out float v) ? v : 0f;
        public void Clear() => floats.Clear();
        public bool isEmpty => floats.Count == 0;
    }

    /// <summary>정렬 레이어 이름표 (2D 정렬은 쓰지 않지만 이름은 화면 층이 그리는 순서를 정하는 데 쓴다).</summary>
    public static class SortingLayer
    {
        /// <summary>아래에서 위로 그려지는 순서.</summary>
        public static readonly List<string> Names = new List<string> { "Default", "Background", "Pickup", "World", "Enemy", "Player", "Projectile", "Effect" };
        public static int NameToID(string name) { int i = Names.IndexOf(name); return i < 0 ? 0 : i == 0 ? 0 : i + 100; }
        public static bool IsValid(int id) => id == 0 || (id > 100 && id - 100 < Names.Count);
        public static string IDToName(int id) => id > 100 && id - 100 < Names.Count ? Names[id - 100] : "Default";
        public static int GetLayerValueFromID(int id) => id > 100 ? id - 100 : 0;
    }

    public enum LightType { Spot, Directional, Point }
    public enum LightShadows { None, Hard, Soft }
    public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3 }

    /// <summary>빛 (방향광 = 해, 점광원 = 횃불·플레이어 빛). 방향은 트랜스폼의 forward.</summary>
    public class Light : Behaviour
    {
        public LightType type = LightType.Point;
        public Color color = Color.white;
        public float intensity = 1f;
        public float range = 10f;
        public LightShadows shadows = LightShadows.None;
        public float shadowStrength = 1f;
        public float shadowBias = 0.05f, shadowNormalBias = 0.4f;
        /// <summary>방향광이 나아가는 방향 (로직 좌표). 0 이면 트랜스폼의 forward 를 쓴다.</summary>
        public Vector3 direction;
    }

    /// <summary>장면 전체의 환경광.</summary>
    public static class RenderSettings
    {
        public static AmbientMode ambientMode = AmbientMode.Flat;
        public static Color ambientLight = new Color(0.2f, 0.2f, 0.2f, 1f);
        public static float ambientIntensity = 1f;
        public static Light sun;
    }

    /// <summary>메시 데이터 (절벽, 흙길). 화면 층이 version 을 보고 Godot 메시를 다시 만든다.</summary>
    public sealed class Mesh : Object
    {
        public string name;
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<Color32> colors32 = new List<Color32>();
        readonly List<List<int>> submeshes = new List<List<int>>();
        public int version;
        public Bounds bounds;

        public int vertexCount => vertices.Count;
        public int subMeshCount
        {
            get => submeshes.Count;
            set { while (submeshes.Count < value) submeshes.Add(new List<int>()); while (submeshes.Count > value) submeshes.RemoveAt(submeshes.Count - 1); }
        }

        public void Clear() { vertices.Clear(); uv.Clear(); colors32.Clear(); submeshes.Clear(); version++; }
        public void SetVertices(List<Vector3> v) { vertices.Clear(); vertices.AddRange(v); version++; }
        public void SetUVs(int channel, List<Vector2> v) { uv.Clear(); uv.AddRange(v); version++; }
        public void SetColors(List<Color32> c) { colors32.Clear(); colors32.AddRange(c); version++; }
        public void SetTriangles(List<int> triangles, int submesh)
        {
            if (subMeshCount <= submesh) subMeshCount = submesh + 1;
            submeshes[submesh].Clear();
            submeshes[submesh].AddRange(triangles);
            version++;
        }
        public List<int> GetTriangles(int submesh) => submesh < submeshes.Count ? submeshes[submesh] : new List<int>();
        public int[] triangles
        {
            get { var all = new List<int>(); foreach (var s in submeshes) all.AddRange(s); return all.ToArray(); }
        }
        public void MarkDynamic() { }
        public void RecalculateNormals() { }
        public void RecalculateBounds()
        {
            if (vertices.Count == 0) { bounds = default; return; }
            Vector3 min = vertices[0], max = vertices[0];
            foreach (var v in vertices)
            {
                min = new Vector3(Mathf.Min(min.x, v.x), Mathf.Min(min.y, v.y), Mathf.Min(min.z, v.z));
                max = new Vector3(Mathf.Max(max.x, v.x), Mathf.Max(max.y, v.y), Mathf.Max(max.z, v.z));
            }
            bounds = new Bounds((min + max) * 0.5f, max - min);
        }
    }

    public class MeshFilter : Component
    {
        public Mesh sharedMesh;
        public Mesh mesh { get => sharedMesh; set => sharedMesh = value; }
    }

    public enum LightProbeUsage { Off, BlendProbes }

    public class MeshRenderer : Renderer
    {
        public LightProbeUsage lightProbeUsage = LightProbeUsage.Off;
        public Material[] sharedMaterials = new Material[0];
        public MeshRenderer() { shadowCastingMode = ShadowCastingMode.On; receiveShadows = true; }
    }

    /// <summary>세계에 떠 있는 글자 (데미지 숫자).</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class TextMesh : Component
    {
        public string text = "";
        public Font font;
        public int fontSize = 32;
        public float characterSize = 1f;
        public Color color = Color.white;
        public TextAnchor anchor = TextAnchor.MiddleCenter;
        public TextAlignment alignment = TextAlignment.Center;
        public FontStyle fontStyle = FontStyle.Normal;
    }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }
    /// <summary>유니티에서 실행 시작 때 정적 상태를 지우던 표시. 여기서는 프로세스가 새로 뜨므로 아무 일도 하지 않는다.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }

    public enum LoadSceneMode { Single, Additive }

    /// <summary>씬 한 개의 이름표.</summary>
    public struct Scene
    {
        public string name;
        public int buildIndex;
        public bool IsValid() => !string.IsNullOrEmpty(name);
    }

    /// <summary>
    /// 씬 전환 (타이틀 ↔ 전투 ↔ 내정 ↔ 맵 편집기). 순수 로직은 "이 씬으로" 요청만 하고, 장면 모형을 비운 뒤
    /// <see cref="Builders"/> 에 등록된 조립 함수가 그 씬의 오브젝트를 만든다(유니티의 .unity 파일 자리).
    /// </summary>
    public static class SceneManager
    {
        /// <summary>씬 이름 → 그 씬을 조립하는 함수. 빌드 순서 = 등록 순서.</summary>
        public static readonly Dictionary<string, Action> Builders = new Dictionary<string, Action>();
        static readonly List<string> order = new List<string>();
        static Scene active = new Scene { name = "", buildIndex = -1 };

        public static event Action<Scene, LoadSceneMode> sceneLoaded;
        /// <summary>씬이 바뀔 때 화면 층에 알린다 (비추던 노드를 치운다).</summary>
        public static event Action<string> Loading;
        public static int sceneCountInBuildSettings => order.Count;

        public static void Register(string name, Action builder)
        {
            if (!Builders.ContainsKey(name)) order.Add(name);
            Builders[name] = builder;
        }

        public static Scene GetActiveScene() => active;
        public static bool CanLoad(string name) => Builders.ContainsKey(name);

        public static void LoadScene(string name, LoadSceneMode mode = LoadSceneMode.Single)
        {
            Loading?.Invoke(name);
            if (mode == LoadSceneMode.Single) SceneRuntime.Clear();
            active = new Scene { name = name, buildIndex = order.IndexOf(name) };
            if (Builders.TryGetValue(name, out var build)) build?.Invoke();
            sceneLoaded?.Invoke(active, mode);
        }

        public static void LoadScene(int buildIndex) => LoadScene(order[buildIndex]);

        /// <summary>테스트용: 현재 씬 이름만 바꾼다.</summary>
        public static void SetActiveForTests(string name) => active = new Scene { name = name, buildIndex = order.IndexOf(name) };
    }
}
