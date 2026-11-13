using System;
using System.Collections;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;
using NUnit.Framework.Internal.Commands;
using Samkuk.Compat.InputSystem;

[assembly: Samkuk.Compat.FrameBoundary]

// 유니티 테스트 도구의 자리 (Godot 전환 G3). 옮긴 PlayMode 테스트가 그대로 돌도록 같은 이름을 둔다.
// 프레임은 실제 시간이 아니라 장면 모형의 SceneRuntime.Tick 으로 흐르므로 "0.5초 기다리기" 도 곧바로 끝난다.
namespace Samkuk.Compat
{
    /// <summary>한 프레임(또는 조건)을 기다리라는 표시. [UnityTest] 실행기가 해석한다.</summary>
    public abstract class YieldInstruction { }
    public sealed class WaitForSeconds : YieldInstruction { public readonly float seconds; public WaitForSeconds(float seconds) { this.seconds = seconds; } }
    public sealed class WaitForSecondsRealtime : YieldInstruction { public readonly float seconds; public WaitForSecondsRealtime(float seconds) { this.seconds = seconds; } }
    public sealed class WaitForFixedUpdate : YieldInstruction { }
    public sealed class WaitForEndOfFrame : YieldInstruction { }
    public sealed class WaitUntil : YieldInstruction { public readonly Func<bool> predicate; public WaitUntil(Func<bool> predicate) { this.predicate = predicate; } }
    public sealed class WaitWhile : YieldInstruction { public readonly Func<bool> predicate; public WaitWhile(Func<bool> predicate) { this.predicate = predicate; } }

    public enum LogType { Error, Assert, Warning, Log, Exception }

    /// <summary>
    /// 테스트 사이의 경계: 유니티에서는 테스트가 끝나면 프레임이 넘어가며 Destroy 로 예약한 오브젝트가 사라졌다.
    /// 여기서는 테스트마다 끝난 뒤(TearDown 다음) 그 처리를 직접 한다. 시작 전에는 유니티 에디터에서 돌던 조건(작업 폴더 = 프로젝트 루트, 에디터)을 맞춘다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class FrameBoundaryAttribute : Attribute, ITestAction
    {
        static bool prepared;
        public ActionTargets Targets => ActionTargets.Test;

        public void BeforeTest(ITest test)
        {
            if (prepared) return;
            prepared = true;
            Application.isEditor = true;
            // 유니티 파일 경로(Assets/...)를 확인하는 테스트가 있다: 저장소 루트에서 돌던 것처럼
            string repo = System.IO.Path.GetDirectoryName(Samkuk.Tests.AssetDatabase.GodotRoot);
            if (System.IO.Directory.Exists(System.IO.Path.Combine(repo, "Assets"))) Environment.CurrentDirectory = repo;
        }

        public void AfterTest(ITest test) => SceneRuntime.EndFrame();
    }

    /// <summary>유니티에서는 예상한 오류 로그를 알리는 데 썼다. 여기서는 로그가 테스트를 실패시키지 않으므로 아무 일도 하지 않는다.</summary>
    public static class LogAssert
    {
        public static bool ignoreFailingMessages;
        public static void Expect(LogType type, string message) { }
        public static void Expect(LogType type, System.Text.RegularExpressions.Regex message) { }
        public static void NoUnexpectedReceived() { }
    }

    /// <summary>
    /// 프레임을 넘겨 가며 도는 테스트. 메서드는 IEnumerator 를 돌려주고 yield 마다 장면 모형이 한 프레임(1/60초) 진행한다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class UnityTestAttribute : NUnitAttribute, ISimpleTestBuilder, IImplyFixture, IWrapTestMethod
    {
        public const float FrameTime = 1f / 60f;
        const int MaxFrames = 200000;   // 조건이 끝내 참이 되지 않는 테스트가 영원히 돌지 않게

        readonly NUnitTestCaseBuilder builder = new NUnitTestCaseBuilder();

        public TestMethod BuildFrom(IMethodInfo method, Test suite)
        {
            // NUnit 은 값을 돌려주는 테스트에 "기대 결과" 가 없으면 못 돌린다고 본다. 결과는 아래 Wrap 에서 직접 처리한다
            var parms = new TestCaseParameters(new object[0]) { ExpectedResult = new object() };
            return builder.BuildTestMethod(method, suite, parms);
        }

        public TestCommand Wrap(TestCommand command) => new EnumeratorCommand(command);

        sealed class EnumeratorCommand : DelegatingTestCommand
        {
            public EnumeratorCommand(TestCommand inner) : base(inner) { }

            public override TestResult Execute(TestExecutionContext context)
            {
                object returned = context.CurrentTest.Method.Invoke(context.TestObject, null);
                if (returned is IEnumerator routine)
                {
                    int frames = 0;
                    Run(routine, ref frames);
                }
                context.CurrentResult.SetResult(ResultState.Success);
                return context.CurrentResult;
            }
        }

        /// <summary>코루틴을 끝까지 돌린다 (다른 테스트 도우미에서도 쓴다).</summary>
        public static void Run(IEnumerator routine, ref int frames)
        {
            while (routine.MoveNext())
            {
                object y = routine.Current;
                switch (y)
                {
                    case IEnumerator nested:
                        Run(nested, ref frames);
                        break;
                    case WaitForSeconds w:
                    {
                        float until = Time.time + w.seconds;
                        do Step(ref frames); while (Time.time < until - 1e-5f && Time.timeScale > 0f);
                        break;
                    }
                    case WaitForSecondsRealtime w:
                    {
                        float until = Time.unscaledTime + w.seconds;
                        do Step(ref frames); while (Time.unscaledTime < until - 1e-5f);
                        break;
                    }
                    case WaitForFixedUpdate _:
                        // 다음 물리 걸음까지: 물리 간격만큼 한 프레임을 보낸다 (시간이 멈춰 있으면 걸음도 없다)
                        SceneRuntime.Tick(Time.fixedDeltaTime);
                        Count(ref frames);
                        break;
                    case WaitUntil w:
                        do Step(ref frames); while (!w.predicate());
                        break;
                    case WaitWhile w:
                        do Step(ref frames); while (w.predicate());
                        break;
                    default:
                        Step(ref frames);   // null, WaitForEndOfFrame, 그 밖의 것: 한 프레임
                        break;
                }
            }
        }

        static void Step(ref int frames)
        {
            SceneRuntime.Tick(FrameTime);
            Count(ref frames);
        }

        static void Count(ref int frames)
        {
            if (++frames > MaxFrames) throw new InvalidOperationException($"테스트가 {MaxFrames} 프레임을 넘겼습니다 (끝나지 않는 기다림).");
        }
    }

    /// <summary>프레임을 넘기는 준비/정리 메서드 표시 (유니티의 [UnitySetUp]/[UnityTearDown]). 아직 쓰는 테스트가 없어 표시만 둔다.</summary>
    [AttributeUsage(AttributeTargets.Method)] public sealed class UnitySetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class UnityTearDownAttribute : Attribute { }
}

namespace Samkuk.Compat.InputSystem
{
    /// <summary>유니티 InputSystem 의 정적 창구 자리 (테스트가 장치를 "추가" 할 때 쓴다).</summary>
    public static class InputSystem
    {
        public static T AddDevice<T>() where T : class
        {
            if (typeof(T) == typeof(Keyboard)) return Keyboard.current as T;
            if (typeof(T) == typeof(Mouse)) return Mouse.current as T;
            if (typeof(T) == typeof(Gamepad)) { InputState.SetGamepad(true, Vector2.zero, false, false); return Gamepad.current as T; }
            return null;
        }
        public static void Update() { }
    }

    /// <summary>입력을 흉내 내는 테스트의 바탕: 시작할 때 입력을 비우고, Press/Release 로 키를 누르고 뗀다.</summary>
    public class InputTestFixture
    {
        [NUnit.Framework.SetUp] public virtual void Setup() => InputState.Reset();
        [NUnit.Framework.TearDown] public virtual void TearDown() => InputState.Reset();

        public void Press(ButtonControl control) => control.Set(true);
        public void Release(ButtonControl control) => control.Set(false);
        public void PressAndRelease(ButtonControl control) { control.Set(true); control.Set(false); }
        public void Set(Vector2Control control, Vector2 value) => control.value = value;
    }
}
