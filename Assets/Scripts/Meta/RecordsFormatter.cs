using UnityEngine;

namespace Samkuk.Meta
{
    /// <summary>기록 화면에 보여줄 문구.</summary>
    public static class RecordsFormatter
    {
        public static string Format(SaveData save)
        {
            int runs = save.totalRuns;
            int winRate = runs > 0 ? Mathf.RoundToInt(100f * save.clears / runs) : 0;
            int sec = Mathf.FloorToInt(save.bestSeconds);

            int totalLevels = 0;
            foreach (var u in save.upgrades) totalLevels += u.level;

            return
                $"총 플레이        {runs}회\n" +
                $"클리어              {save.clears}회  ({winRate}%)\n" +
                $"최고 생존 시간   {sec / 60:00}:{sec % 60:00}\n" +
                $"최다 처치          {save.bestKills}\n" +
                $"마지막 장수       {(string.IsNullOrEmpty(save.lastHero) ? "-" : save.lastHero)}\n" +
                $"영구 강화          합계 Lv.{totalLevels}\n" +
                $"보유 골드          {save.gold}";
        }
    }
}
