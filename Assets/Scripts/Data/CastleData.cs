using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>성이 속한 주(州). 후한 말 13주 + 사례.</summary>
    public enum CastleRegion
    {
        Youzhou, Jizhou, Bingzhou, Qingzhou, Yanzhou, Yuzhou, Xuzhou,
        Sili, Liangzhou, Yizhou, Jingzhou, Yangzhou, Jiaozhou,
    }

    /// <summary>성 주변 지형. 배경 그림의 분위기와 이후 내정 수치 보정의 기준이 된다.</summary>
    public enum CastleTerrain { Plain, Steppe, Mountain, River, Jungle, Loess }

    /// <summary>성의 규모. 배경 그림의 성벽/누각 크기와 같다.</summary>
    public enum CastleSize { Small, Medium, Large, Capital }

    /// <summary>내정 모드의 성(도시) 한 곳: 이름, 위치, 인접 성, 배경 그림.</summary>
    [CreateAssetMenu(menuName = "Samkuk/Castle Data", fileName = "Castle_New")]
    public class CastleData : ScriptableObject
    {
        [Tooltip("영문 아이디 (배경 파일 이름 Castle_<id>.png 와 같음)")] public string id;
        public string displayName = "성";
        [Tooltip("한자 이름 (문루 현판, 깃발)")] public string hanja;
        public CastleRegion region;
        public CastleTerrain terrain;
        public CastleSize size = CastleSize.Medium;
        [Tooltip("성 앞에 강/바다가 흐르는지 (배경 그림에 반영)")] public bool hasWater;
        [Tooltip("전략 지도 위의 위치 (0~1, 왼쪽 위가 서북)")] public Vector2 mapPosition;
        [Tooltip("바로 이동/공격할 수 있는 인접 성 (양방향)")] public List<CastleData> neighbors = new List<CastleData>();
        [Tooltip("내정 화면 배경 (1280x720). Assets/Sprites/Castles/Castle_<id>.png")] public Sprite background;

        public bool IsAdjacent(CastleData other) => other != null && neighbors.Contains(other);

        public string RegionName => RegionLabel(region);

        public static string RegionLabel(CastleRegion region)
        {
            switch (region)
            {
                case CastleRegion.Youzhou: return "유주";
                case CastleRegion.Jizhou: return "기주";
                case CastleRegion.Bingzhou: return "병주";
                case CastleRegion.Qingzhou: return "청주";
                case CastleRegion.Yanzhou: return "연주";
                case CastleRegion.Yuzhou: return "예주";
                case CastleRegion.Xuzhou: return "서주";
                case CastleRegion.Sili: return "사례";
                case CastleRegion.Liangzhou: return "양주(涼)";
                case CastleRegion.Yizhou: return "익주";
                case CastleRegion.Jingzhou: return "형주";
                case CastleRegion.Yangzhou: return "양주(揚)";
                case CastleRegion.Jiaozhou: return "교주";
                default: return region.ToString();
            }
        }

        public static string SizeLabel(CastleSize size)
        {
            switch (size)
            {
                case CastleSize.Small: return "소성";
                case CastleSize.Medium: return "중성";
                case CastleSize.Large: return "대성";
                case CastleSize.Capital: return "도성";
                default: return size.ToString();
            }
        }

        /// <summary>"사례 · 평야 · 도성" 처럼 한 줄 설명.</summary>
        public string Summary => $"{RegionName} · {TerrainLabel(terrain)} · {SizeLabel(size)}";

        public static string TerrainLabel(CastleTerrain terrain)
        {
            switch (terrain)
            {
                case CastleTerrain.Plain: return "평야";
                case CastleTerrain.Steppe: return "초원";
                case CastleTerrain.Mountain: return "산악";
                case CastleTerrain.River: return "강변";
                case CastleTerrain.Jungle: return "남방";
                case CastleTerrain.Loess: return "황토";
                default: return terrain.ToString();
            }
        }
    }
}
