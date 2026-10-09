using Samkuk.Core;
using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 월드 정렬(Step 14-4): 캐릭터(주인공, 아군, 적)와 서 있는 소품을 한 정렬 레이어("World")에 두고 발 위치(y)로 앞뒤를 정한다.
    /// 2D 렌더러의 투명 정렬 축을 (0, 1, 0) 커스텀 축으로 두면(셋업 Step 14-4) 유니티가 스프라이트의 정렬 기준점(피벗 = 발)으로
    /// 알아서 정렬하므로 프레임마다 정렬 값을 쓰는 코드가 없다. 아래에 있을수록 앞에 그려진다.
    /// "World" 레이어가 아직 없으면(셋업 전) Player 레이어를 대신 써서 적어도 사라지지는 않게 한다.
    /// </summary>
    public static class WorldSorting
    {
        public static int LayerId
        {
            get
            {
                int id = SortingLayer.NameToID(GameLayers.Sorting.World);
                if (id != 0 && SortingLayer.IsValid(id)) return id;
                return SortingLayer.NameToID(GameLayers.Sorting.Player);
            }
        }

        /// <summary>셋업이 끝나 "World" 레이어가 있는가.</summary>
        public static bool HasWorldLayer => SortingLayer.NameToID(GameLayers.Sorting.World) != 0;

        /// <summary>렌더러를 월드 정렬에 넣는다: World 레이어, 순서 0, 정렬 기준점 = 피벗(발).</summary>
        public static void Configure(SpriteRenderer sr)
        {
            if (sr == null) return;
            sr.sortingLayerID = LayerId;
            sr.sortingOrder = 0;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        /// <summary>월드 정렬에 들어 있는가 (레이어와 기준점으로 판단).</summary>
        public static bool IsConfigured(SpriteRenderer sr) =>
            sr != null && sr.sortingLayerID == LayerId && sr.spriteSortPoint == SpriteSortPoint.Pivot;
    }
}
