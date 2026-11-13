using System.Collections.Generic;
using Godot;
using Samkuk.Data;

namespace Samkuk.Game
{
    /// <summary>
    /// 데이터 JSON 을 Godot 리소스 경로(res://data)에서 읽는다 (G2). 내보낸 게임에서는 res:// 가 꾸러미 안이라
    /// System.IO 로는 못 읽으므로 Godot 의 DirAccess/FileAccess 를 쓴다. JSON 이 꾸러미에 들어가도록 내보내기 필터에 *.json 을 넣는다(G11).
    /// </summary>
    public sealed class GodotDataSource : IDataSource
    {
        readonly string root;
        public GodotDataSource(string root = "res://data") { this.root = root.TrimEnd('/'); }

        public IEnumerable<string> List()
        {
            var found = new List<string>();
            Walk("", found);
            return found;
        }

        void Walk(string sub, List<string> found)
        {
            using var dir = DirAccess.Open(sub.Length == 0 ? root : root + "/" + sub);
            if (dir == null) return;
            foreach (string name in dir.GetDirectories()) Walk(sub.Length == 0 ? name : sub + "/" + name, found);
            foreach (string name in dir.GetFiles())
                if (name.EndsWith(".json")) found.Add(sub.Length == 0 ? name : sub + "/" + name);
        }

        public string Read(string relativePath)
        {
            string path = root + "/" + relativePath;
            if (!FileAccess.FileExists(path)) return null;
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            return file?.GetAsText();
        }
    }
}
