using System;
using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>맵에 흩뿌려지는 소품 하나(나무, 바위 등)의 그림과 나오는 비중.</summary>
    [Serializable]
    public class TerrainProp
    {
        public string name;
        [Tooltip("바닥에 닿는 점이 스프라이트 피벗(아래쪽 가운데)인 그림")] public Sprite sprite;
        [Tooltip("나올 확률 비중 (다른 소품과의 상대값)")] public float weight = 1f;
        [Tooltip("크기 배율 범위")] public float scaleMin = 0.9f;
        public float scaleMax = 1.2f;
    }

    /// <summary>
    /// 한 지형(평야, 초원, 산악, 강변, 남방, 황토)의 전투 맵 구성: 바닥 타일과 흩뿌릴 소품 목록.
    /// 성마다 같은 지형이라도 소품 비율, 밀도, 바닥 색조가 조금씩 달라진다(<c>TerrainMap</c>).
    /// </summary>
    [CreateAssetMenu(menuName = "Samkuk/Terrain Theme", fileName = "Theme_New")]
    public class TerrainTheme : ScriptableObject
    {
        public CastleTerrain terrain;
        [Tooltip("이어 붙여도 이음새가 없는 바닥 타일 (256x256, PPU 64 = 4유닛). 비워 두면 기본 배경 + 색 덮개")] public Sprite groundTile;
        [Tooltip("한 칸(12x12 유닛)당 소품 평균 개수")] public float propsPerChunk = 9f;
        public List<TerrainProp> props = new List<TerrainProp>();

        [Header("강")]
        [Tooltip("강 한 토막(물) 그림: 여러 개를 겹쳐 구불구불 흐르는 강을 만든다. 비워 두면 이 지형에는 강이 없다")] public Sprite riverWater;
        [Tooltip("강둑: 물 밑에 더 넓게 깔리는 흙색 가장자리")] public Sprite riverBank;
        [Tooltip("강 폭 (유닛, 성마다 +-15%)")] public float riverWidth = 3.6f;
    }

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
