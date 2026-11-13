using System;
using System.Text.Json;

namespace Samkuk.Compat
{
    // 유니티에서 쓰던 이름을 그대로 두고 옮기기 위한 호환 층 (Godot 전환, ADR 0004).
    // 순수 로직(Samkuk.Core)은 엔진을 모른다: 그림은 경로만 든 손잡이이고, 로그·저장 폴더는 게임 쪽이 꽂아 준다.

    /// <summary>데이터 한 건의 바탕 (유니티 ScriptableObject 자리). <see cref="name"/> 은 데이터 파일 이름 = 아이디.</summary>
    public abstract class ScriptableObject : Object
    {
        public string name;

        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
        public override string ToString() => $"{name} ({GetType().Name})";
    }

    /// <summary>로그. 게임이 <see cref="Sink"/> 에 Godot 출력(GD.Print 등)을 꽂는다. 비어 있으면(테스트) 콘솔로.</summary>
    public static class Debug
    {
        public enum Level { Info, Warning, Error }
        public static Action<Level, string> Sink;
        /// <summary>개발 빌드인가 (치트·FPS 표시). 게임이 시작할 때 정한다.</summary>
        public static bool isDebugBuild = true;

        public static void Log(object message) => Write(Level.Info, message);
        public static void LogWarning(object message) => Write(Level.Warning, message);
        public static void LogError(object message) => Write(Level.Error, message);
        public static void Log(object message, Object context) => Write(Level.Info, message);
        public static void LogWarning(object message, Object context) => Write(Level.Warning, message);
        public static void LogError(object message, Object context) => Write(Level.Error, message);

        static void Write(Level level, object message)
        {
            string text = message?.ToString() ?? "null";
            if (Sink != null) Sink(level, text);
            else Console.WriteLine(level == Level.Info ? text : $"[{level}] {text}");
        }
    }

    /// <summary>실행 환경. 게임이 시작할 때 저장 폴더(user://)를 넣는다. 테스트는 임시 폴더.</summary>
    public static class Application
    {
        public static string persistentDataPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "samkuk");
        public static bool isEditor = false;
        public static bool isPlaying = true;
        public static bool runInBackground;
        public static string version = "0.1.0";
        /// <summary>게임이 끝날 때 (화면 층이 알린다).</summary>
        public static event System.Action quitting;
        /// <summary>끝내기 요청. 화면 층이 실제로 창을 닫는다.</summary>
        public static System.Action QuitAction;
        public static void Quit() { quitting?.Invoke(); QuitAction?.Invoke(); }
        public static bool CanStreamedLevelBeLoaded(string sceneName) => SceneManager.CanLoad(sceneName);
        public static string temporaryCachePath => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "samkuk_cache");
    }

    /// <summary>
    /// 유니티 JsonUtility 자리: public 필드를 그대로 JSON 으로 (System.Text.Json, 필드 포함). 저장 파일 형식(save.json, 맵 JSON)이 유니티 때와 같다.
    /// 깨진 JSON 은 예외를 던지고(호출처가 잡는다) 모르는 항목은 무시한다.
    /// </summary>
    public static class JsonUtility
    {
        static readonly JsonSerializerOptions Compact = Make(false);
        static readonly JsonSerializerOptions Pretty = Make(true);

        static JsonSerializerOptions Make(bool indented) => new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = indented,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // 한글을 \uXXXX 로 바꾸지 않는다
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };

        public static string ToJson(object obj, bool prettyPrint = false) => JsonSerializer.Serialize(obj, obj.GetType(), prettyPrint ? Pretty : Compact);
        public static T FromJson<T>(string json) => string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, Compact);
    }

    /// <summary>유니티 Random 자리 (연출·선택지 뽑기용, 결정적 배치는 System.Random 을 직접 쓴다). 테스트는 <see cref="InitState"/> 로 고정.</summary>
    public static class Random
    {
        static System.Random rng = new System.Random();

        public static void InitState(int seed) => rng = new System.Random(seed);
        public static float value => (float)rng.NextDouble();
        public static float Range(float minInclusive, float maxInclusive) => minInclusive + (float)rng.NextDouble() * (maxInclusive - minInclusive);
        public static int Range(int minInclusive, int maxExclusive) => maxExclusive <= minInclusive ? minInclusive : rng.Next(minInclusive, maxExclusive);

        public static Vector2 insideUnitCircle
        {
            get
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rng.NextDouble());
                return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
        }
    }

    // ---- 인스펙터용 속성: 옮긴 데이터 클래스의 설명을 그대로 남기려고 둔 빈 속성 ----

    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeFieldAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string tooltip) { } }
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string header) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class MinAttribute : Attribute { public MinAttribute(float min) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TextAreaAttribute : Attribute { public TextAreaAttribute() { } public TextAreaAttribute(int min, int max) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; public int order; }
}

namespace Samkuk.Compat
{
    /// <summary>글 파일 한 건 (유니티 TextAsset 자리).</summary>
    public sealed class TextAsset
    {
        public string text;
        public TextAsset(string text) { this.text = text; }
    }

    /// <summary>
    /// 이름으로 데이터를 읽는 창구 (유니티 Resources 자리). 순수 로직은 파일을 모르므로 게임이 <see cref="Loader"/> 에
    /// "형식 + 이름 → 객체" 를 꽂는다. 비어 있거나 없는 이름이면 null (호출처는 이미 null 을 "셋업 전" 으로 다룬다).
    /// </summary>
    public static class Resources
    {
        public static System.Func<System.Type, string, object> Loader;
        public static T Load<T>(string name) where T : class => Loader?.Invoke(typeof(T), name) as T;
        /// <summary>엔진 내장 리소스 자리 (기본 글꼴만).</summary>
        public static T GetBuiltinResource<T>(string name) where T : class => typeof(T) == typeof(Font) ? new Font { name = name } as T : null;
    }
}
