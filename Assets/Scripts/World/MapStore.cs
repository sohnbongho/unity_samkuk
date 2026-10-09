using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 맵 편집기로 고친 맵(<see cref="MapLayoutData"/>)의 저장/불러오기. 성 아이디마다 JSON 파일 하나.
    /// 불러오는 순서: 저장 폴더(<c>persistentDataPath/maps</c>, 이 컴퓨터에서 고친 것) → 프로젝트의 <c>Resources/Maps</c>(게임에 포함되는 것).
    /// 에디터에서 저장하면 두 곳에 모두 쓴다(Resources 쪽은 git 으로 공유/빌드에 포함).
    /// </summary>
    public static class MapStore
    {
        public const string ResourcesFolder = "Maps";
        public const string FreeBattleId = "FreeBattle";

        /// <summary>테스트용: 지정하면 이 폴더만 쓴다 (실제 저장 폴더와 Resources 를 건드리지 않는다).</summary>
        public static string PathOverride { get; set; }

        /// <summary>테스트용: true 면 저장된 맵이 없는 것처럼 동작한다.</summary>
        public static bool Disabled { get; set; }

        public static string Directory => PathOverride ?? Path.Combine(Application.persistentDataPath, "maps");

        public static string FileFor(string castleId) => Path.Combine(Directory, FileName(castleId) + ".json");

        /// <summary>파일 이름에 쓸 수 없는 글자를 바꾼다.</summary>
        public static string FileName(string castleId)
        {
            if (string.IsNullOrEmpty(castleId)) return "_";
            var sb = new StringBuilder(castleId.Length);
            foreach (char ch in castleId)
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), ch) >= 0 ? '_' : ch);
            return sb.ToString();
        }

        /// <summary>고쳐 둔 맵이 있으면 돌려주고, 없거나 읽을 수 없으면 null (자동 생성 맵을 쓴다).</summary>
        public static MapLayoutData Load(string castleId)
        {
            if (Disabled || string.IsNullOrEmpty(castleId)) return null;
            try
            {
                string file = FileFor(castleId);
                if (File.Exists(file)) return Parse(File.ReadAllText(file, Encoding.UTF8));
                if (PathOverride == null)
                {
                    var asset = Resources.Load<TextAsset>(ResourcesFolder + "/" + FileName(castleId));
                    if (asset != null) return Parse(asset.text);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Samkuk] 맵 파일을 읽지 못했습니다 ({castleId}): {e.Message}");
            }
            return null;
        }

        static MapLayoutData Parse(string json)
        {
            var data = JsonUtility.FromJson<MapLayoutData>(json);
            return data != null && data.chunks != null ? data : null;
        }

        public static bool Exists(string castleId) => !Disabled && Load(castleId) != null;

        public static bool Save(MapLayoutData data, out string path)
        {
            path = null;
            if (data == null || string.IsNullOrEmpty(data.castleId)) return false;
            try
            {
                string json = JsonUtility.ToJson(data, true);
                System.IO.Directory.CreateDirectory(Directory);
                path = FileFor(data.castleId);
                File.WriteAllText(path, json, new UTF8Encoding(false));
#if UNITY_EDITOR
                if (PathOverride == null) WriteProjectCopy(data.castleId, json);
#endif
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Samkuk] 맵 저장 실패 ({data.castleId}): {e.Message}");
                return false;
            }
        }

        /// <summary>저장한 맵을 지워 자동 생성 맵으로 되돌린다.</summary>
        public static bool Delete(string castleId)
        {
            if (string.IsNullOrEmpty(castleId)) return false;
            bool deleted = false;
            try
            {
                string file = FileFor(castleId);
                if (File.Exists(file)) { File.Delete(file); deleted = true; }
#if UNITY_EDITOR
                if (PathOverride == null) deleted |= DeleteProjectCopy(castleId);
#endif
            }
            catch (Exception e)
            {
                Debug.LogError($"[Samkuk] 맵 삭제 실패 ({castleId}): {e.Message}");
            }
            return deleted;
        }

#if UNITY_EDITOR
        const string ProjectFolder = "Assets/Resources/Maps";

        static string ProjectFile(string castleId) => ProjectFolder + "/" + FileName(castleId) + ".json";

        /// <summary>에디터에서만: 게임에 포함되도록 Assets/Resources/Maps 에도 같은 내용을 쓴다.</summary>
        static void WriteProjectCopy(string castleId, string json)
        {
            System.IO.Directory.CreateDirectory(ProjectFolder);
            File.WriteAllText(ProjectFile(castleId), json, new UTF8Encoding(false));
            UnityEditor.AssetDatabase.ImportAsset(ProjectFile(castleId));
        }

        static bool DeleteProjectCopy(string castleId)
        {
            string file = ProjectFile(castleId);
            if (!File.Exists(file)) return false;
            return UnityEditor.AssetDatabase.DeleteAsset(file);
        }
#endif
    }
}
