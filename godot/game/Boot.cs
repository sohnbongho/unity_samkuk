using Godot;
using Samkuk.Player;
using Samkuk.World;

namespace Samkuk.Game
{
    /// <summary>
    /// 첫 화면(임시, G3 에서 타이틀·전투로 바뀐다). 순수 로직(Samkuk.Core)에 엔진 쪽 창구(로그, 저장 폴더)를 꽂고,
    /// 명령줄에 `-- --smoke` 가 있으면 연결만 확인하고 끝낸다 (헤드리스 점검용).
    /// </summary>
    public partial class Boot : Node
    {
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
            Compat.Application.isEditor = OS.HasFeature("editor");

            // 순수 로직이 실제로 붙었는지: 경험치 곡선과 틸트 기하를 한 번 불러 본다
            var forward = TiltPreset.Forward(TiltPreset.Pitch);
            GD.Print($"[Samkuk] Core 연결 확인: 2레벨 필요 경험치 {ExperienceCurve.RequiredFor(1)}, 카메라 방향 {forward}, 저장 폴더 {Compat.Application.persistentDataPath}");

            foreach (string arg in OS.GetCmdlineUserArgs())
                if (arg == "--smoke") { GetTree().Quit(); return; }
        }
    }
}
