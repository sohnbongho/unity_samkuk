using System;
using System.Collections.Generic;

namespace Samkuk.Compat.InputSystem
{
    // 입력 모형 (G3): 규칙 코드는 유니티 Input System 과 같은 이름으로 "지금 눌렸나"만 묻고,
    // 실제 키·마우스 상태는 화면 층(Godot)이 InputState 로 넣는다. 테스트는 InputState 를 직접 눌러 본다.

    /// <summary>버튼 하나의 상태 (키, 마우스 버튼, 패드 버튼).</summary>
    public sealed class ButtonControl
    {
        public readonly string name;
        internal bool down, pressed, released;
        internal ButtonControl(string name) { this.name = name; }
        public bool isPressed => down;
        public bool wasPressedThisFrame => pressed;
        public bool wasReleasedThisFrame => released;

        internal void Set(bool value)
        {
            if (value == down) return;
            down = value;
            if (value) pressed = true; else released = true;
        }
        internal void EndFrame() { pressed = false; released = false; }
    }

    /// <summary>2D 값 하나 (마우스 위치·이동량·휠, 패드 스틱).</summary>
    public sealed class Vector2Control
    {
        internal Vector2 value;
        public Vector2 ReadValue() => value;
    }

    /// <summary>키보드. 키 이름은 유니티와 같다 (w, upArrow, space, digit1, f5 ...).</summary>
    public sealed class Keyboard
    {
        public static Keyboard current { get; internal set; } = new Keyboard();
        readonly Dictionary<string, ButtonControl> keys = new Dictionary<string, ButtonControl>();
        internal IEnumerable<ButtonControl> All => keys.Values;

        public ButtonControl this[string key]
        {
            get
            {
                if (!keys.TryGetValue(key, out var k)) keys[key] = k = new ButtonControl(key);
                return k;
            }
        }

        public bool anyKeyIsPressed { get { foreach (var k in keys.Values) if (k.down) return true; return false; } }

        public ButtonControl aKey => this["a"]; public ButtonControl bKey => this["b"]; public ButtonControl cKey => this["c"]; public ButtonControl dKey => this["d"];
        public ButtonControl eKey => this["e"]; public ButtonControl fKey => this["f"]; public ButtonControl gKey => this["g"]; public ButtonControl hKey => this["h"];
        public ButtonControl iKey => this["i"]; public ButtonControl jKey => this["j"]; public ButtonControl kKey => this["k"]; public ButtonControl lKey => this["l"];
        public ButtonControl mKey => this["m"]; public ButtonControl nKey => this["n"]; public ButtonControl oKey => this["o"]; public ButtonControl pKey => this["p"];
        public ButtonControl qKey => this["q"]; public ButtonControl rKey => this["r"]; public ButtonControl sKey => this["s"]; public ButtonControl tKey => this["t"];
        public ButtonControl uKey => this["u"]; public ButtonControl vKey => this["v"]; public ButtonControl wKey => this["w"]; public ButtonControl xKey => this["x"];
        public ButtonControl yKey => this["y"]; public ButtonControl zKey => this["z"];
        public ButtonControl digit0Key => this["digit0"]; public ButtonControl digit1Key => this["digit1"]; public ButtonControl digit2Key => this["digit2"];
        public ButtonControl digit3Key => this["digit3"]; public ButtonControl digit4Key => this["digit4"]; public ButtonControl digit5Key => this["digit5"];
        public ButtonControl digit6Key => this["digit6"]; public ButtonControl digit7Key => this["digit7"]; public ButtonControl digit8Key => this["digit8"];
        public ButtonControl digit9Key => this["digit9"];
        public ButtonControl numpad0Key => this["numpad0"]; public ButtonControl numpad1Key => this["numpad1"]; public ButtonControl numpad2Key => this["numpad2"];
        public ButtonControl numpad3Key => this["numpad3"]; public ButtonControl numpad4Key => this["numpad4"]; public ButtonControl numpad5Key => this["numpad5"];
        public ButtonControl numpad6Key => this["numpad6"]; public ButtonControl numpad7Key => this["numpad7"]; public ButtonControl numpad8Key => this["numpad8"];
        public ButtonControl numpad9Key => this["numpad9"]; public ButtonControl numpadEnterKey => this["numpadEnter"];
        public ButtonControl f1Key => this["f1"]; public ButtonControl f2Key => this["f2"]; public ButtonControl f3Key => this["f3"]; public ButtonControl f4Key => this["f4"];
        public ButtonControl f5Key => this["f5"]; public ButtonControl f6Key => this["f6"]; public ButtonControl f7Key => this["f7"]; public ButtonControl f8Key => this["f8"];
        public ButtonControl f9Key => this["f9"]; public ButtonControl f10Key => this["f10"]; public ButtonControl f11Key => this["f11"]; public ButtonControl f12Key => this["f12"];
        public ButtonControl upArrowKey => this["upArrow"]; public ButtonControl downArrowKey => this["downArrow"];
        public ButtonControl leftArrowKey => this["leftArrow"]; public ButtonControl rightArrowKey => this["rightArrow"];
        public ButtonControl spaceKey => this["space"]; public ButtonControl enterKey => this["enter"]; public ButtonControl escapeKey => this["escape"];
        public ButtonControl tabKey => this["tab"]; public ButtonControl backspaceKey => this["backspace"]; public ButtonControl deleteKey => this["delete"];
        public ButtonControl shiftKey => this["shift"]; public ButtonControl ctrlKey => this["ctrl"]; public ButtonControl altKey => this["alt"];
        public ButtonControl leftBracketKey => this["leftBracket"]; public ButtonControl rightBracketKey => this["rightBracket"];
        public ButtonControl minusKey => this["minus"]; public ButtonControl equalsKey => this["equals"];
    }

    public sealed class Mouse
    {
        public static Mouse current { get; internal set; } = new Mouse();
        /// <summary>화면 픽셀 위치 (왼쪽 아래가 0,0 — 유니티와 같다).</summary>
        public readonly Vector2Control position = new Vector2Control();
        public readonly Vector2Control delta = new Vector2Control();
        public readonly Vector2Control scroll = new Vector2Control();
        public readonly ButtonControl leftButton = new ButtonControl("leftButton");
        public readonly ButtonControl rightButton = new ButtonControl("rightButton");
        public readonly ButtonControl middleButton = new ButtonControl("middleButton");
    }

    public sealed class Gamepad
    {
        /// <summary>연결된 패드가 없으면 null.</summary>
        public static Gamepad current { get; internal set; }
        public readonly Vector2Control leftStick = new Vector2Control();
        public readonly ButtonControl buttonSouth = new ButtonControl("buttonSouth");
        public readonly ButtonControl startButton = new ButtonControl("startButton");
        internal ButtonControl Button(string name) => name == "buttonSouth" ? buttonSouth : name == "start" || name == "startButton" ? startButton : null;
    }

    public enum InputActionType { Value, Button, PassThrough }

    /// <summary>입력 동작: 묶어 둔 경로(Keyboard/w, Gamepad/leftStick 등)의 현재 상태를 읽는다.</summary>
    public sealed class InputAction : IDisposable
    {
        const string KeyboardPrefix = "<Keyboard>/", GamepadPrefix = "<Gamepad>/";

        public readonly string name;
        public readonly InputActionType type;
        readonly List<string> bindings = new List<string>();
        readonly List<Composite> composites = new List<Composite>();
        public bool enabled { get; private set; }

        public InputAction(string name = null, InputActionType type = InputActionType.Value) { this.name = name; this.type = type; }

        public sealed class Composite
        {
            internal readonly List<string> up = new List<string>(), down = new List<string>(), left = new List<string>(), right = new List<string>();
            public Composite With(string part, string path)
            {
                switch (part) { case "Up": up.Add(path); break; case "Down": down.Add(path); break; case "Left": left.Add(path); break; case "Right": right.Add(path); break; }
                return this;
            }
        }

        public void AddBinding(string path) => bindings.Add(path);
        public Composite AddCompositeBinding(string kind) { var c = new Composite(); composites.Add(c); return c; }
        public void Enable() => enabled = true;
        public void Disable() => enabled = false;
        public void Dispose() => enabled = false;

        public T ReadValue<T>()
        {
            object v = typeof(T) == typeof(Vector2) ? ReadVector() : typeof(T) == typeof(float) ? (IsPressed() ? 1f : 0f) : (object)default(T);
            return (T)v;
        }

        Vector2 ReadVector()
        {
            if (!enabled) return Vector2.zero;
            Vector2 best = Vector2.zero;
            foreach (var c in composites)
            {
                var v = new Vector2((Any(c.right) ? 1f : 0f) - (Any(c.left) ? 1f : 0f), (Any(c.up) ? 1f : 0f) - (Any(c.down) ? 1f : 0f));
                if (v.sqrMagnitude > 1f) v = v.normalized;   // 유니티 2DVector 의 기본(대각선도 길이 1)
                if (v.sqrMagnitude > best.sqrMagnitude) best = v;
            }
            foreach (string path in bindings)
                if (path == GamepadPrefix + "leftStick" && Gamepad.current != null && Gamepad.current.leftStick.value.sqrMagnitude > best.sqrMagnitude)
                    best = Gamepad.current.leftStick.value;
            return best;
        }

        static bool Any(List<string> paths)
        {
            foreach (string p in paths) { var b = Resolve(p); if (b != null && b.down) return true; }
            return false;
        }

        static ButtonControl Resolve(string path)
        {
            if (path.StartsWith(KeyboardPrefix)) return Keyboard.current?[path.Substring(KeyboardPrefix.Length)];
            if (path.StartsWith(GamepadPrefix)) return Gamepad.current?.Button(path.Substring(GamepadPrefix.Length));
            return null;
        }

        public bool IsPressed()
        {
            if (!enabled) return false;
            foreach (string p in bindings) { var b = Resolve(p); if (b != null && b.down) return true; }
            return false;
        }

        public bool WasPressedThisFrame()
        {
            if (!enabled) return false;
            foreach (string p in bindings) { var b = Resolve(p); if (b != null && b.pressed) return true; }
            return false;
        }
    }

    /// <summary>입력 상태를 넣는 곳 (화면 층, 테스트).</summary>
    public static class InputState
    {
        public static void SetKey(string key, bool down) => Keyboard.current[key].Set(down);
        /// <summary>button: 0 왼쪽, 1 오른쪽, 2 가운데.</summary>
        public static void SetMouseButton(int button, bool down)
            => (button == 0 ? Mouse.current.leftButton : button == 1 ? Mouse.current.rightButton : Mouse.current.middleButton).Set(down);
        public static void SetMouse(Vector2 position, Vector2 delta) { Mouse.current.position.value = position; Mouse.current.delta.value = delta; }
        public static void AddScroll(Vector2 scroll) => Mouse.current.scroll.value += scroll;
        public static void SetGamepad(bool connected, Vector2 leftStick, bool south, bool start)
        {
            if (!connected) { Gamepad.current = null; return; }
            Gamepad.current ??= new Gamepad();
            Gamepad.current.leftStick.value = leftStick;
            Gamepad.current.buttonSouth.Set(south);
            Gamepad.current.startButton.Set(start);
        }

        /// <summary>프레임 끝: "이번 프레임에 눌림/뗌" 표시와 휠·이동량을 지운다. SceneRuntime.Tick 이 부른다.</summary>
        public static void EndFrame()
        {
            foreach (var k in Keyboard.current.All) k.EndFrame();
            var m = Mouse.current;
            m.leftButton.EndFrame(); m.rightButton.EndFrame(); m.middleButton.EndFrame();
            m.scroll.value = Vector2.zero; m.delta.value = Vector2.zero;
            if (Gamepad.current != null) { Gamepad.current.buttonSouth.EndFrame(); Gamepad.current.startButton.EndFrame(); }
        }

        /// <summary>모두 뗀 처음 상태로 (테스트).</summary>
        public static void Reset()
        {
            Keyboard.current = new Keyboard();
            Mouse.current = new Mouse();
            Gamepad.current = null;
        }
    }
}
