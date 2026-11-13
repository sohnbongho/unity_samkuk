using System;
using System.Collections.Generic;
using Godot;
using Samkuk.Compat;
using Samkuk.Compat.InputSystem;
using Samkuk.Data;
using Mathf = Samkuk.Compat.Mathf;
using CVec2 = Samkuk.Compat.Vector2;

namespace Samkuk.Game
{
    /// <summary>
    /// 게임의 뿌리 (G3). 순수 로직(Samkuk.Core)에 엔진 쪽 창구(로그, 저장 폴더, 데이터, 소리, 끝내기)를 꽂고,
    /// 프레임마다 입력 → 장면 모형 한 프레임(<see cref="SceneRuntime.Tick"/>) → 화면에 비추기(<see cref="WorldMirror"/>, <see cref="UiMirror"/>) 순서로 돈다.
    /// 게임 규칙은 전부 순수 로직에 있다. 여기와 View/ 는 "보여 주고 입력을 전하는" 일만 한다.
    ///
    /// 명령줄(점검용, `--` 뒤): --scene=BattleScene (시작 씬), --frames=N (N 프레임 뒤 끝냄), --shot=경로.png (끝낼 때 화면 저장),
    /// --smoke (= --frames=120 + 상태 출력), --keys=d,space (그 키를 누른 채로 — 자동 점검에서 움직여 보기),
    /// --press=40:digit1;90:enter (그 프레임에 키를 잠깐 누름 — 장수 고르기 같은 선택을 자동으로),
    /// --castle=Chengdu (그 성으로 출진한 전투 맵), --click=40:640,360 (그 프레임에 화면의 그 점을 마우스로 누른다),
    /// --save=경로 (저장 파일을 그 경로로 — 자동 점검은 이것을 써서 실제 저장을 건드리지 않는다), --maps=폴더 (맵 편집기 저장 폴더),
    /// --hold=s:100-160 (그 프레임 구간 동안 키를 누름), --drag=60:500,300>600,340:30 (마우스를 누른 채 끌기),
    /// --login=아이디:비밀번호 (로그인 창을 채우고 누름), --shot-when=evolve (레벨업에 진화가 나오면 화면 저장).
    /// </summary>
    public partial class GameRoot : Node
    {
        WorldMirror world;
        UiMirror ui;
        ScreenFx screenFx;
        Label notes;
        string[] autoLogin;
        readonly List<(int frame, Godot.Vector2 from, Godot.Vector2 to, int length)> drags = new List<(int, Godot.Vector2, Godot.Vector2, int)>();   // --drag=60:500,300>600,340:30
        bool waitEvolve, frozen;   // --shot-when=evolve: 레벨업 선택지에 진화가 나오면 입력을 멈추고 곧 화면을 저장한다
        AudioOut audio;
        int frames, quitAfter = -1;
        string shotPath;
        string[] heldKeys = new string[0];
        readonly List<(int frame, string key)> presses = new List<(int, string)>();   // --press=40:digit1;90:enter (그 프레임에 두 프레임 동안 누름)
        readonly List<string> down = new List<string>();
        readonly List<(string key, int from, int to)> holds = new List<(string, int, int)>();   // --hold=s:100-160;d:160-180 (그 프레임 구간 동안 누름)
        readonly List<(int frame, Godot.Vector2 at)> clicks = new List<(int, Godot.Vector2)>();   // --click=40:640,360 (그 프레임에 그 화면 점을 누르고 다음 프레임에 뗀다)
        bool failed;

        public override void _Ready()
        {
            Compat.Debug.Sink = (level, text) =>
            {
                if (level == Compat.Debug.Level.Error) GD.PushError(text);
                else if (level == Compat.Debug.Level.Warning) GD.PushWarning(text);
                else GD.Print(text);
            };
            Compat.Debug.isDebugBuild = OS.IsDebugBuild();
            Compat.Application.persistentDataPath = ProjectSettings.GlobalizePath("user://");
            Compat.Application.isEditor = false;
            Compat.Application.QuitAction = () => GetTree().Quit();
            Compat.Font.OsFontNames = () => OS.GetSystemFonts();

            world = new WorldMirror { Name = "World" };
            AddChild(world);
            screenFx = new ScreenFx { Name = "ScreenFx" };
            AddChild(screenFx);
            ui = new UiMirror { Name = "Ui" };
            AddChild(ui);
            audio = new AudioOut { Name = "Audio" };
            AddChild(audio);
            AudioSource.Output = audio.Play;

            GameData.Load(new GodotDataSource());
            foreach (string problem in GameData.Problems) GD.PushWarning("[Samkuk] 데이터: " + problem);

            string scene = "TitleScene";
            foreach (string arg in OS.GetCmdlineUserArgs())
            {
                if (arg.StartsWith("--scene=")) scene = arg.Substring(8);
                else if (arg.StartsWith("--frames=")) quitAfter = int.Parse(arg.Substring(9));
                else if (arg.StartsWith("--shot=")) shotPath = arg.Substring(7);
                else if (arg.StartsWith("--keys=")) heldKeys = arg.Substring(7).Split(',', StringSplitOptions.RemoveEmptyEntries);
                else if (arg.StartsWith("--press="))
                    foreach (string item in arg.Substring(8).Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] pair = item.Split(':');
                        if (pair.Length == 2 && int.TryParse(pair[0], out int at)) presses.Add((at, pair[1]));
                    }
                else if (arg.StartsWith("--castle="))
                {
                    // 그 성으로 출진한 것처럼 (성의 지형·시간대로 전투 맵이 만들어진다)
                    var castle = CastleCatalog.Load()?.Find(arg.Substring(9));
                    if (castle == null) GD.PushWarning("[Samkuk] 그런 성이 없습니다: " + arg.Substring(9));
                    Samkuk.Core.GameSession.SortieCastle = castle;
                }
                else if (arg.StartsWith("--click="))
                    foreach (string item in arg.Substring(8).Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] pair = item.Split(':');
                        string[] xy = pair.Length == 2 ? pair[1].Split(',') : new string[0];
                        if (xy.Length == 2 && int.TryParse(pair[0], out int at) && float.TryParse(xy[0], out float cx) && float.TryParse(xy[1], out float cy)) clicks.Add((at, new Godot.Vector2(cx, cy)));
                    }
                else if (arg.StartsWith("--hold="))
                    foreach (string item in arg.Substring(7).Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] pair = item.Split(':');
                        string[] range = pair.Length == 2 ? pair[1].Split('-') : new string[0];
                        if (range.Length == 2 && int.TryParse(range[0], out int from) && int.TryParse(range[1], out int to)) holds.Add((pair[0], from, to));
                    }
                else if (arg.StartsWith("--maps=")) Samkuk.World.MapStore.PathOverride = arg.Substring(7);   // 맵 편집기 저장 폴더 (점검이 실제 맵 파일을 건드리지 않게)
                else if (arg.StartsWith("--drag="))
                    foreach (string item in arg.Substring(7).Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        // 프레임:x1,y1>x2,y2:걸리는 프레임
                        string[] part = item.Split(':');
                        string[] ends = part.Length == 3 ? part[1].Split('>') : new string[0];
                        if (ends.Length != 2) continue;
                        string[] a = ends[0].Split(','), b = ends[1].Split(',');
                        drags.Add((int.Parse(part[0]), new Godot.Vector2(float.Parse(a[0]), float.Parse(a[1])), new Godot.Vector2(float.Parse(b[0]), float.Parse(b[1])), int.Parse(part[2])));
                    }
                else if (arg == "--shot-when=evolve") waitEvolve = true;
                else if (arg.StartsWith("--login=")) autoLogin = arg.Substring(8).Split(':');   // --login=아이디:비밀번호 (20번째 프레임에 로그인)
                else if (arg.StartsWith("--save=")) Samkuk.Meta.SaveSystem.PathOverride = arg.Substring(7);   // 점검이 실제 저장을 건드리지 않게
                else if (arg == "--smoke") quitAfter = 120;
            }

            // 해상도·창 모드: 타이틀이 저장된 설정을 적용할 때 실제 창을 바꾼다 (자동 점검 중에는 창을 건드리지 않는다)
            if (quitAfter < 0) Samkuk.Core.DisplaySettings.ApplyAction = ApplyDisplay;
            Compat.Screen.currentResolution = new Compat.Screen.Resolution { width = DisplayServer.ScreenGetSize().X, height = DisplayServer.ScreenGetSize().Y };

            UpdateScreen();
            RuntimeInitializers.Run(false);
            // 씬이 읽힐 때마다 "씬 로드 뒤" 훅을 부른다: 타이틀의 [맵 편집기]·[협동] 버튼, 맵 테스트 안내가 씬을 오갈 때마다 다시 붙는다.
            // (유니티는 이 훅을 실행 시작 때 한 번만 불러서, 전투에서 타이틀로 돌아오면 그 버튼들이 없었다. 코드의 의도대로 고친 것이다)
            SceneManager.sceneLoaded += (_, __) => RuntimeInitializers.Run(true);
            SceneManager.LoadScene(scene);
            foreach (string problem in SceneFile.Problems) GD.PushWarning("[Samkuk] 씬: " + problem);
            GD.Print($"[Samkuk] 시작: 씬 {scene}, 데이터 {GameData.Count}건, 오브젝트 {SceneRuntime.Objects.Count}");
        }

        // 개발용 표시(FPS, F 키 안내)와 맵 테스트 안내: 규칙 코드가 낸 글자를 화면 구석에 그린다 (유니티의 OnGUI 자리)
        void SyncNotes()
        {
            if (notes == null)
            {
                var layer = new CanvasLayer { Layer = 20, Name = "Notes" };
                AddChild(layer);
                notes = new Label { Position = new Godot.Vector2(10f, 6f), MouseFilter = Control.MouseFilterEnum.Ignore };
                notes.AddThemeFontOverride("font", Fonts.Default);
                notes.AddThemeFontSizeOverride("font_size", 15);
                notes.AddThemeColorOverride("font_outline_color", new Godot.Color(0f, 0f, 0f));
                notes.AddThemeConstantOverride("outline_size", 4);
                layer.AddChild(notes);
            }
            string text = "";
            var overlay = Compat.Object.FindAnyObjectByType<Samkuk.Core.DebugOverlay>();
            if (overlay != null) text = overlay.Text;
            if (Compat.Object.FindAnyObjectByType<Samkuk.World.MapTestReturn>() != null) text += (text.Length > 0 ? "\n" : "") + Samkuk.World.MapTestReturn.Notice;
            notes.Visible = text.Length > 0;
            if (notes.Text != text) notes.Text = text;
        }

        static void ApplyDisplay(int width, int height, FullScreenMode mode)
        {
            switch (mode)
            {
                case FullScreenMode.Windowed:
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                    DisplayServer.WindowSetSize(new Vector2I(width, height));
                    // 화면 가운데로
                    var screen = DisplayServer.ScreenGetSize();
                    DisplayServer.WindowSetPosition(new Vector2I(Math.Max(0, (screen.X - width) / 2), Math.Max(0, (screen.Y - height) / 2)));
                    break;
                case FullScreenMode.ExclusiveFullScreen:
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                    break;
                default:
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);   // 테두리 없는 전체화면 (바탕화면 해상도)
                    break;
            }
        }

        void UpdateScreen()
        {
            var size = GetViewport().GetVisibleRect().Size;
            Compat.Screen.width = Mathf.Max(1, (int)size.X);
            Compat.Screen.height = Mathf.Max(1, (int)size.Y);
            float aspect = Compat.Screen.width / (float)Compat.Screen.height;
            foreach (var cam in Compat.Object.FindObjectsByType<Compat.Camera>()) cam.aspect = aspect;
        }

        public override void _Process(double delta)
        {
            if (failed) return;
            try
            {
                UpdateScreen();
                if (autoLogin != null && autoLogin.Length == 2 && frames == 20)
                {
                    // 사람이 하듯: 로그인 창의 두 칸을 채우고 [로그인] 을 누른다
                    var panel = Compat.Object.FindAnyObjectByType<Samkuk.UI.LoginPanel>();
                    var fields = panel != null ? panel.GetComponentsInChildren<Compat.UI.InputField>() : null;
                    var buttons = panel != null ? panel.GetComponentsInChildren<Compat.UI.Button>() : null;
                    if (fields != null && fields.Length >= 2 && buttons.Length >= 1)
                    {
                        fields[0].text = autoLogin[0];
                        fields[1].text = autoLogin[1];
                        buttons[0].Press();
                    }
                    else Samkuk.Net.NetClient.GetOrCreate().Login(autoLogin[0], autoLogin[1]);
                    autoLogin = null;
                }
                InputFeed.OverrideMouse = null;
                foreach (var (at, point) in clicks)
                    if (frames >= at - 2 && frames <= at + 1)
                    {
                        // 규칙 코드가 직접 읽는 마우스(맵 편집기)에도 같은 눌림을 넣는다. 누르기 두 프레임 전부터 그 자리에 올려 둔다(UI 위인지 판정이 먼저 되도록)
                        InputFeed.OverrideMouse = point;
                        InputFeed.OverrideLeft = frames == at;
                    }
                foreach (var (at, from, to, length) in drags)
                    if (frames >= at && frames <= at + length + 1)
                    {
                        // 누른 채 끌기: 마지막 프레임에 뗀다
                        float t = Mathf.Clamp01((frames - at) / (float)Math.Max(1, length));
                        InputFeed.OverrideMouse = from + (to - from) * t;
                        InputFeed.OverrideLeft = frames <= at + length;
                    }
                foreach (var (at, point) in clicks)
                    if (frames == at || frames == at + 1)
                        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = frames == at, Position = point, GlobalPosition = point });
                if (waitEvolve && !frozen)
                {
                    var levelUp = Compat.Object.FindAnyObjectByType<Samkuk.Upgrades.LevelUpController>();
                    if (levelUp != null && levelUp.IsShowing)
                        foreach (var option in levelUp.CurrentOptions)
                            if (option.Kind == Samkuk.Upgrades.UpgradeKind.Evolve) { frozen = true; quitAfter = frames + 4; presses.Clear(); break; }
                }
                down.Clear();
                down.AddRange(heldKeys);
                foreach (var (at, key) in presses) if (frames == at || frames == at + 1) down.Add(key);
                foreach (var (key, from, to) in holds) if (frames >= from && frames <= to) down.Add(key);
                InputFeed.Poll(GetViewport(), down);
                SceneRuntime.Tick((float)Math.Min(delta, 0.1));   // 창을 끌거나 멈췄다 돌아와도 한 번에 너무 멀리 가지 않게
                world.Sync();
                screenFx.Sync(world.Environment, GetViewport().GetVisibleRect().Size.Y);
                SyncNotes();
                ui.Sync(GetViewport().GetVisibleRect().Size);
            }
            catch (Exception e)
            {
                // 규칙 코드의 예외: 한 번 알리고 멈춘다 (매 프레임 같은 오류로 로그를 덮지 않게)
                failed = true;
                GD.PushError("[Samkuk] 프레임 처리 중 예외: " + e);
                if (quitAfter >= 0) GetTree().Quit(1);
                return;
            }

            frames++;
            if (quitAfter >= 0 && frames >= quitAfter) Finish();
        }

        void Finish()
        {
            quitAfter = -1;
            var net = Samkuk.Net.NetClient.Instance;
            if (net != null) GD.Print($"[Samkuk] 네트워크: {net.Phase} {net.Message}");
            GD.Print($"[Samkuk] {frames} 프레임: 씬 {SceneManager.GetActiveScene().name}, 오브젝트 {SceneRuntime.Objects.Count}, 보이는 그림 {world.SpriteCount}, 시간 {Compat.Time.time:0.00}초, FPS {Engine.GetFramesPerSecond():0}");
            if (!string.IsNullOrEmpty(shotPath))
            {
                var image = GetViewport().GetTexture().GetImage();
                var error = image.SavePng(shotPath);
                GD.Print($"[Samkuk] 화면 저장 {shotPath}: {error}");
            }
            GetTree().Quit();
        }

        public override void _Input(InputEvent e)
        {
            if (e is InputEventMouseButton wheel && wheel.Pressed)
            {
                if (wheel.ButtonIndex == MouseButton.WheelUp) InputFeed.AddScroll(1f);
                else if (wheel.ButtonIndex == MouseButton.WheelDown) InputFeed.AddScroll(-1f);
            }
        }
    }

    /// <summary>Godot 의 키·마우스·패드 상태를 순수 로직의 입력 모형으로 옮긴다 (프레임마다 훑는다).</summary>
    public static class InputFeed
    {
        static readonly (string name, Key key)[] Keys = BuildKeys();
        static float pendingScroll;
        static CVec2 lastMouse;

        static (string, Key)[] BuildKeys()
        {
            var list = new List<(string, Key)>();
            for (int i = 0; i < 26; i++) list.Add((((char)('a' + i)).ToString(), Key.A + i));
            for (int i = 0; i < 10; i++) { list.Add(("digit" + i, Key.Key0 + i)); list.Add(("numpad" + i, Key.Kp0 + i)); }
            for (int i = 1; i <= 12; i++) list.Add(("f" + i, Key.F1 + (i - 1)));
            list.AddRange(new[]
            {
                ("upArrow", Key.Up), ("downArrow", Key.Down), ("leftArrow", Key.Left), ("rightArrow", Key.Right),
                ("space", Key.Space), ("enter", Key.Enter), ("numpadEnter", Key.KpEnter), ("escape", Key.Escape), ("tab", Key.Tab),
                ("backspace", Key.Backspace), ("delete", Key.Delete), ("shift", Key.Shift), ("ctrl", Key.Ctrl), ("alt", Key.Alt),
                ("leftBracket", Key.Bracketleft), ("rightBracket", Key.Bracketright), ("minus", Key.Minus), ("equals", Key.Equal),
            });
            return list.ToArray();
        }

        public static void AddScroll(float amount) => pendingScroll += amount;
        /// <summary>자동 점검용: 실제 마우스 대신 이 화면 점과 왼쪽 버튼 상태를 쓴다.</summary>
        public static Godot.Vector2? OverrideMouse;
        public static bool OverrideLeft;

        public static void Poll(Viewport viewport, List<string> heldKeys)
        {
            // 글자를 치는 중(입력 칸에 초점)이면 게임 키로 전하지 않는다
            bool typing = viewport.GuiGetFocusOwner() is LineEdit;
            foreach (var (name, key) in Keys)
                InputState.SetKey(name, !typing && (Input.IsKeyPressed(key) || heldKeys.Contains(name)));

            var size = viewport.GetVisibleRect().Size;
            var m = OverrideMouse ?? viewport.GetMousePosition();
            var pos = new CVec2(m.X, size.Y - m.Y);   // 로직은 왼쪽 아래가 (0, 0)
            InputState.SetMouse(pos, pos - lastMouse);
            lastMouse = pos;
            InputState.SetMouseButton(0, OverrideMouse.HasValue ? OverrideLeft : Input.IsMouseButtonPressed(MouseButton.Left));
            InputState.SetMouseButton(1, Input.IsMouseButtonPressed(MouseButton.Right));
            InputState.SetMouseButton(2, Input.IsMouseButtonPressed(MouseButton.Middle));
            if (pendingScroll != 0f) { InputState.AddScroll(new CVec2(0f, pendingScroll * 120f)); pendingScroll = 0f; }

            var pads = Input.GetConnectedJoypads();
            if (pads.Count == 0) InputState.SetGamepad(false, CVec2.zero, false, false);
            else
            {
                int pad = pads[0];
                var stick = new CVec2(Input.GetJoyAxis(pad, JoyAxis.LeftX), -Input.GetJoyAxis(pad, JoyAxis.LeftY));
                if (stick.magnitude < 0.2f) stick = CVec2.zero;
                InputState.SetGamepad(true, stick, Input.IsJoyButtonPressed(pad, JoyButton.A), Input.IsJoyButtonPressed(pad, JoyButton.Start));
            }
        }
    }

    /// <summary>순수 로직이 요청한 소리를 실제로 낸다. 합성된 샘플(-1~1)을 16비트 스트림으로 바꿔 두고 재생기 8개를 돌려 쓴다.</summary>
    public partial class AudioOut : Node
    {
        const int Players = 8;
        readonly Dictionary<AudioClip, AudioStreamWav> streams = new Dictionary<AudioClip, AudioStreamWav>();
        readonly List<AudioStreamPlayer> players = new List<AudioStreamPlayer>();
        int next;

        public override void _Ready()
        {
            for (int i = 0; i < Players; i++)
            {
                var p = new AudioStreamPlayer();
                AddChild(p);
                players.Add(p);
            }
        }

        public void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || clip.data == null || clip.data.Length == 0 || volume <= 0f || players.Count == 0) return;
            if (!streams.TryGetValue(clip, out var stream))
            {
                var bytes = new byte[clip.data.Length * 2];
                for (int i = 0; i < clip.data.Length; i++)
                {
                    short s = (short)Mathf.RoundToInt(Mathf.Clamp(clip.data[i], -1f, 1f) * 32767f);
                    bytes[i * 2] = (byte)(s & 0xff);
                    bytes[i * 2 + 1] = (byte)((s >> 8) & 0xff);
                }
                stream = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = clip.frequency, Stereo = clip.channels > 1, Data = bytes };
                streams[clip] = stream;
            }
            var player = players[next];
            next = (next + 1) % players.Count;
            player.Stream = stream;
            player.VolumeDb = Godot.Mathf.LinearToDb(Mathf.Clamp(volume, 0.0001f, 1f));
            player.PitchScale = Mathf.Max(0.01f, pitch);
            player.Play();
        }
    }
}
