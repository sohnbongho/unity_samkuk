using System;
using System.IO;
using Samkuk.Data;

namespace Samkuk.Exporter
{
    /// <summary>
    /// 서버 데이터 내보내기 (유니티 메뉴 Samkuk > Export Server Data 의 자리, Step 15-2).
    /// godot/data 의 게임 데이터를 읽어 csharpServer 가 쓰는 JSON 을 만든다.
    ///
    ///   dotnet run --project godot/exporter                 → ServerDataExporter.DefaultOutputDir (csharpServer/GameServer/Data)
    ///   dotnet run --project godot/exporter -- 출력폴더      → 그 폴더에 (비교·점검용)
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            // godot/ 폴더: 실행 파일에서 위로 올라가며 project.godot 을 찾는다
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot"))) dir = dir.Parent;
            if (dir == null) { Console.Error.WriteLine("godot/project.godot 을 찾지 못했습니다."); return 1; }

            GameData.Load(new DirectoryDataSource(Path.Combine(dir.FullName, "data")));
            foreach (string problem in GameData.Problems) Console.Error.WriteLine("데이터 문제: " + problem);
            if (GameData.Problems.Count > 0) return 1;

            string output = args.Length > 0 ? args[0] : ServerDataExporter.DefaultOutputDir;
            var files = ServerDataExporter.Export(output);
            Console.WriteLine($"[ServerData] {files.Count}개 파일을 내보냈다 → {output}");
            foreach (string f in files) Console.WriteLine("  " + Path.GetFileName(f));
            return 0;
        }
    }
}
