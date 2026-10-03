using Samkuk.Data;
using Samkuk.Player;
using UnityEngine;

namespace Samkuk.Meta
{
    /// <summary>게임이 시작될 때 저장된 영구 강화를 플레이어 능력치에 적용한다.</summary>
    public class MetaApplier : MonoBehaviour
    {
        [SerializeField] MetaCatalog catalog;
        [SerializeField] PlayerStats stats;

        public MetaCatalog Catalog { get => catalog; set => catalog = value; }
        public PlayerStats Stats { get => stats; set => stats = value; }
        /// <summary>적용된 영구 강화 보너스 (골드 배율 등 결과 계산에도 사용).</summary>
        public MetaBonuses Bonuses { get; private set; }

        void Start() => Apply();

        public void Apply()
        {
            Bonuses = MetaProgression.ComputeBonuses(catalog, SaveSystem.Current);
            if (stats != null) stats.ApplyMeta(Bonuses);
        }
    }
}
