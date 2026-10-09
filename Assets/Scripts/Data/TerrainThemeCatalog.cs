using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>
    /// 모든 지형 테마와 공용 소품(연못, 깃발). <c>Resources/TerrainThemeCatalog.asset</c> 한 곳에 두어
    /// 전투 씬이 씬 연결 없이 불러 쓴다(내정에서 출진했을 때만 사용). 없으면 기존 색 덮개 방식으로 대신한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Samkuk/Terrain Theme Catalog", fileName = "TerrainThemeCatalog")]
    public class TerrainThemeCatalog : ScriptableObject
    {
        public const string ResourceName = "TerrainThemeCatalog";

        public List<TerrainTheme> themes = new List<TerrainTheme>();
        [Tooltip("강/바다가 있는 성(hasWater)과 강변 지형에 놓이는 연못")] public TerrainProp pond = new TerrainProp { name = "Pond", scaleMin = 0.8f, scaleMax = 1.5f };
        [Tooltip("큰 성(대성, 도성)의 맵에 꽂히는 깃발")] public TerrainProp banner = new TerrainProp { name = "Banner", scaleMin = 0.95f, scaleMax = 1.1f };

        public TerrainTheme Get(CastleTerrain terrain)
        {
            foreach (var t in themes)
                if (t != null && t.terrain == terrain) return t;
            return null;
        }

        static TerrainThemeCatalog cached;

        /// <summary>테스트용: true 면 테마가 없는 것처럼 동작한다 (기존 색 덮개 방식 확인).</summary>
        public static bool Disabled { get; set; }

        /// <summary>Resources 의 카탈로그. 없으면(또는 Disabled) null.</summary>
        public static TerrainThemeCatalog Load()
        {
            if (Disabled) return null;
            if (cached == null) cached = Resources.Load<TerrainThemeCatalog>(ResourceName);
            return cached;
        }

        /// <summary>지정한 카탈로그를 현재 것으로 쓴다 (테스트용). null 이면 캐시를 비워 Resources 에서 다시 읽는다.</summary>
        public static void Use(TerrainThemeCatalog catalog) => cached = catalog;
    }
}
