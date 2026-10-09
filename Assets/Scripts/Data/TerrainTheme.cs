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

        [Header("이동 (지형 효과)")]
        [Tooltip("막는 반지름(유닛, 소품 크기 배율을 곱한다). 바닥에 닿는 점이 중심. 0 이면 그대로 지나간다")] public float blockRadius;
        [Tooltip("느려지는 범위의 반지름(유닛, 배율을 곱한다). 0 이면 없음. 연못처럼 밟으면 느려지는 것에 쓴다")] public float slowRadius;
        [Tooltip("느려지는 범위 안에서의 속도 배율 (0.5 = 절반)")] public float slowFactor = 1f;

        /// <summary>이 소품이 이동을 막는가.</summary>
        public bool Blocks => blockRadius > 0f;
        /// <summary>이 소품 위에서 느려지는가 (배율이 1 미만일 때만).</summary>
        public bool Slows => slowRadius > 0f && slowFactor > 0f && slowFactor < 1f;
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
        [Tooltip("강물 위에서의 속도 배율 (0.5 = 절반). 1 이면 느려지지 않는다")] public float riverSlowFactor = 0.5f;
    }
}
