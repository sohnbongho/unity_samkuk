using UnityEngine;

namespace Samkuk.Data
{
    public enum MetaStat
    {
        MaxHp,        // 최대 체력 (+value/레벨, 고정값)
        Damage,       // 공격력 (+value 비율/레벨)
        MoveSpeed,    // 이동 속도 (+value 비율/레벨)
        Regen,        // 초당 체력 재생 (+value/레벨)
        PickupRadius, // 경험치 획득 범위 (+value 비율/레벨)
        ExpGain,      // 경험치 획득량 (+value 비율/레벨)
        GoldGain      // 런 종료 시 획득 골드 (+value 비율/레벨)
    }

    /// <summary>골드로 구매하는 영구 강화. 레벨이 오를수록 비용이 커진다.</summary>
    [CreateAssetMenu(menuName = "Samkuk/Meta Upgrade Data", fileName = "Meta_New")]
    public class MetaUpgradeData : ScriptableObject
    {
        public string displayName = "영구 강화";
        [TextArea] public string description;
        public MetaStat stat;
        [Tooltip("레벨당 효과량")] public float valuePerLevel = 0.05f;
        public int maxLevel = 5;
        [Tooltip("0 → 1 레벨 구매 비용")] public int baseCost = 80;
        [Tooltip("레벨이 오를 때마다 비용이 곱해지는 배율")] public float costGrowth = 1.6f;

        /// <summary>현재 currentLevel일 때 다음 레벨을 사는 데 드는 골드.</summary>
        public int CostForLevel(int currentLevel) =>
            Mathf.Max(1, Mathf.RoundToInt(baseCost * Mathf.Pow(costGrowth, Mathf.Max(0, currentLevel))));
    }
}
