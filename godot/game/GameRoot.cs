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
    /// --castle=Chengdu (그 성으로 출진한 전투 맵), --click=40:640,360 (그 프레임에 화면의 그 점을 마우스로 누른다).
    /// </summary>
    public partial class GameRoot : Node
    {
        WorldMirror world;
        UiMirror ui;
        ScreenFx screenFx;
        AudioOut audio;
        int frames, quitAfter = -1;
        string shotPath;
        string[] heldKeys = new string[0];
        readonly List<(int frame, string key)> presses = new List<(int, string)>();   // --press=40:digit1;90:enter (그 프레임에 두 프레임 동안 누름)
        readonly List<string> down = new List<string>();
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
                else if (arg == "--smoke") quitAfter = 120;
            }

            UpdateScreen();
            SceneManager.LoadScene(scene);
            foreach (string problem in SceneFile.Problems) GD.PushWarning("[Samkuk] 씬: " + problem);
            GD.Print($"[Samkuk] 시작: 씬 {scene}, 데이터 {GameData.Count}건, 오브젝트 {SceneRuntime.Objects.Count}");
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
                foreach (var (at, point) in clicks)
                    if (frames == at || frames == at + 1)
                        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = frames == at, Position = point, GlobalPosition = point });
                down.Clear();
                down.AddRange(heldKeys);
                foreach (var (at, key) in presses) if (frames == at || frames == at + 1) down.Add(key);
                InputFeed.Poll(GetViewport(), down);
                SceneRuntime.Tick((float)Math.Min(delta, 0.1));   // 창을 끌거나 멈췄다 돌아와도 한 번에 너무 멀리 가지 않게
                world.Sync();
                screenFx.Sync(world.Environment, GetViewport().GetVisibleRect().Size.Y);
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

        public static void Poll(Viewport viewport, List<string> heldKeys)
        {
            // 글자를 치는 중(입력 칸에 초점)이면 게임 키로 전하지 않는다
            bool typing = viewport.GuiGetFocusOwner() is LineEdit;
            foreach (var (name, key) in Keys)
                InputState.SetKey(name, !typing && (Input.IsKeyPressed(key) || heldKeys.Contains(name)));

            var size = viewport.GetVisibleRect().Size;
            var m = viewport.GetMousePosition();
            var pos = new CVec2(m.X, size.Y - m.Y);   // 로직은 왼쪽 아래가 (0, 0)
            InputState.SetMouse(pos, pos - lastMouse);
            lastMouse = pos;
            InputState.SetMouseButton(0, Input.IsMouseButtonPressed(MouseButton.Left));
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
