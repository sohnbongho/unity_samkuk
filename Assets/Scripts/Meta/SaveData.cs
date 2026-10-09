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

        // 설정
        public float sfxVolume = 0.7f;
        public bool screenShake = true;
        // HD-2D 연출(Step 14): 조명(전역광 색조, 소품 점광원, 플레이어 빛)
        public bool hd2dLighting = true;

        // 내정: 시작할 때 고른 "내 성" (CastleData.id, 비어 있으면 아직 고르지 않음)
        public string homeCastleId;
        // 내정: 지금까지 차지한 성들 (CastleData.id). 시작 성(내 성)도 포함한다. 비어 있으면 아직 시작하지 않음
        public List<string> ownedCastleIds = new List<string>();

        // 화면 (기본값은 프로젝트의 기본 설정과 같다: 1920x1080, 테두리 없는 전체화면)
        public int displayWidth = 1920;
        public int displayHeight = 1080;
        public int windowMode = 1; // 0 창 모드, 1 전체화면(테두리 없음), 2 전용 전체화면

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
