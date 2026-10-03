using System;
using System.IO;
using UnityEngine;

namespace Samkuk.Meta
{
    /// <summary>
    /// 저장 파일(JSON) 읽기/쓰기. 파일이 없거나 손상되면 새 데이터로 시작한다.
    /// 경로는 테스트에서 PathOverride로 바꿀 수 있다.
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "save.json";

        static SaveData current;

        /// <summary>테스트용: 지정하면 이 경로를 사용한다.</summary>
        public static string PathOverride { get; set; }

        public static string FilePath =>
            !string.IsNullOrEmpty(PathOverride) ? PathOverride : Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>메모리에 올려 둔 현재 저장 데이터 (없으면 파일에서 읽는다).</summary>
        public static SaveData Current => current ??= Load();

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                    if (loaded != null) return Sanitize(loaded);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 저장 파일을 읽지 못해 새로 시작합니다: {e.Message}");
            }
            return new SaveData();
        }

        public static bool Save(SaveData data)
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                // 쓰는 도중 꺼져도 기존 파일이 깨지지 않도록 임시 파일에 쓴 뒤 교체한다.
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);

                current = data;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 저장 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>현재 메모리 데이터를 저장한다.</summary>
        public static bool SaveCurrent() => Save(Current);

        /// <summary>저장 파일과 메모리 캐시를 모두 지운다.</summary>
        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 삭제 실패: {e.Message}");
            }
            current = null;
        }

        /// <summary>메모리 캐시를 버리고 다음 접근 때 파일에서 다시 읽게 한다.</summary>
        public static void ResetCache() => current = null;

        /// <summary>손상/조작된 값(음수 골드 등)을 안전한 범위로 보정한다.</summary>
        static SaveData Sanitize(SaveData d)
        {
            d.gold = Mathf.Max(0, d.gold);
            d.totalRuns = Mathf.Max(0, d.totalRuns);
            d.clears = Mathf.Max(0, d.clears);
            d.bestKills = Mathf.Max(0, d.bestKills);
            d.bestSeconds = Mathf.Max(0f, d.bestSeconds);
            d.upgrades ??= new System.Collections.Generic.List<MetaLevel>();
            d.upgrades.RemoveAll(u => u == null || string.IsNullOrEmpty(u.id));
            foreach (var u in d.upgrades) u.level = Mathf.Max(0, u.level);
            return d;
        }
    }
}
