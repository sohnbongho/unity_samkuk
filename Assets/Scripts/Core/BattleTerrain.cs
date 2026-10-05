using Samkuk.Data;
using UnityEngine;

namespace Samkuk.Core
{
    /// <summary>
    /// 내정에서 출진한 성의 지형을 전투 바닥 색으로 옮긴다. 기본 배경(어두운 초록 타일) 위에 반투명 색을 한 겹 덮어
    /// 평야/초원/산악/강변/남방/황토의 분위기를 낸다. 성 없이 시작한 판(타이틀의 [시작])은 기본 배경 그대로.
    /// </summary>
    public static class BattleTerrain
    {
        /// <summary>지형별 덮개 색 (알파가 덮는 세기). 기본 배경 위에 얹으므로 알파가 0이면 그대로다.</summary>
        public static Color Overlay(CastleTerrain terrain)
        {
            switch (terrain)
            {
                case CastleTerrain.Steppe: return new Color(0.80f, 0.70f, 0.40f, 0.55f);   // 마른 풀밭
                case CastleTerrain.Mountain: return new Color(0.50f, 0.52f, 0.56f, 0.50f); // 바위 많은 회색
                case CastleTerrain.River: return new Color(0.25f, 0.55f, 0.60f, 0.45f);    // 물기 있는 청록
                case CastleTerrain.Jungle: return new Color(0.08f, 0.42f, 0.20f, 0.55f);   // 짙은 초록
                case CastleTerrain.Loess: return new Color(0.82f, 0.60f, 0.30f, 0.55f);    // 누런 흙
                default: return new Color(0.35f, 0.58f, 0.25f, 0.35f);                     // 평야: 밝은 초록
            }
        }

        /// <summary>출진한 성이 있으면 그 지형의 덮개 색, 없으면 null.</summary>
        public static Color? ForCurrentSortie()
        {
            var castle = GameSession.SortieCastle;
            return castle != null ? Overlay(castle.terrain) : (Color?)null;
        }
    }
}
