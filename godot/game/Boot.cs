using Godot;
using Samkuk.Data;
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

            // 데이터 창고: res://data 의 JSON 을 전부 읽는다. 문제가 있으면(없는 참조 등) 경고로 남긴다
            GameData.Load(new GodotDataSource());
            foreach (string problem in GameData.Problems) GD.PushWarning("[Samkuk] 데이터: " + problem);
            var tree = GameData.Get<TerrainTheme>("Theme_Plain")?.props.Find(p => p.name == "Prop_Plain_Tree");
            var treeTexture = Textures.Of(tree?.sprite);
            var blob = Textures.Of(Samkuk.World.TerrainDecals.SoftBlob);
            GD.Print($"[Samkuk] 데이터 {GameData.Count}건 (문제 {GameData.Problems.Count}), 나무 그림 {treeTexture?.GetSize()}, 코드 그림 {blob?.GetSize()}");

            foreach (string arg in OS.GetCmdlineUserArgs())
                if (arg == "--smoke") { GetTree().Quit(); return; }
        }
    }
}
