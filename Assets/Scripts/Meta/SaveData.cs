using System;
using System.Collections.Generic;

namespace Samkuk.Meta
{
    [Serializable]
    public class MetaLevel
    {
        public string id;
        public int level;
    }

    /// <summary>영구 저장되는 진행 정보 (JsonUtility 직렬화를 위해 단순한 필드만 사용).</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int gold;
        public List<MetaLevel> upgrades = new List<MetaLevel>();

        // 기록
        public int totalRuns;
        public int clears;
        public float bestSeconds;
        public int bestKills;
        public string lastHero;

        public int GetUpgradeLevel(string id)
        {
            foreach (var u in upgrades)
                if (u.id == id) return u.level;
            return 0;
        }

        public void SetUpgradeLevel(string id, int level)
        {
            foreach (var u in upgrades)
            {
                if (u.id != id) continue;
                u.level = level;
                return;
            }
            upgrades.Add(new MetaLevel { id = id, level = level });
        }
    }
}
