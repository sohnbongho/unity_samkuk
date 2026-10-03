using System;
using System.Collections.Generic;
using Samkuk.Data;

namespace Samkuk.Meta
{
    /// <summary>상점의 한 줄: 영구 강화 하나의 현재 상태.</summary>
    public class ShopEntry
    {
        public MetaUpgradeData Upgrade;
        public int Level;
        public int MaxLevel;
        /// <summary>다음 레벨의 비용 (최대 레벨이면 -1).</summary>
        public int Cost;
        public bool IsMax;
        public bool CanBuy;
        /// <summary>현재 레벨의 효과 (예: "+8%").</summary>
        public string EffectText;
        /// <summary>다음 레벨의 효과 (최대 레벨이면 빈 문자열).</summary>
        public string NextEffectText;
    }

    /// <summary>
    /// 영구 강화 상점의 모델. 현재 저장 데이터(SaveSystem.Current)를 기준으로 상태를 계산하고
    /// 구매하면 즉시 저장한다. UI와 무관하게 테스트할 수 있다.
    /// </summary>
    public class MetaShop
    {
        readonly MetaCatalog catalog;
        readonly List<ShopEntry> entries = new List<ShopEntry>();

        public MetaShop(MetaCatalog catalog)
        {
            this.catalog = catalog;
            Refresh();
        }

        public IReadOnlyList<ShopEntry> Entries => entries;
        public int Gold => SaveSystem.Current.gold;

        /// <summary>상태가 바뀌었을 때 (구매 성공 후).</summary>
        public event Action Changed;

        /// <summary>저장 데이터를 다시 읽어 모든 줄을 갱신한다.</summary>
        public void Refresh()
        {
            entries.Clear();
            if (catalog == null) return;

            var save = SaveSystem.Current;
            foreach (var u in catalog.upgrades)
            {
                if (u == null) continue;

                int level = MetaProgression.GetLevel(save, u);
                bool isMax = level >= u.maxLevel;
                entries.Add(new ShopEntry
                {
                    Upgrade = u,
                    Level = level,
                    MaxLevel = u.maxLevel,
                    Cost = MetaProgression.NextCost(save, u),
                    IsMax = isMax,
                    CanBuy = MetaProgression.CanPurchase(save, u),
                    EffectText = MetaProgression.EffectText(u, level),
                    NextEffectText = isMax ? "" : MetaProgression.EffectText(u, level + 1)
                });
            }
        }

        /// <summary>구매하고 저장한다. 골드가 모자라거나 최대 레벨이면 false.</summary>
        public bool Purchase(MetaUpgradeData upgrade)
        {
            var save = SaveSystem.Current;
            if (!MetaProgression.TryPurchase(save, upgrade)) return false;

            if (!SaveSystem.SaveCurrent())
            {
                // 저장에 실패하면 메모리 상태를 되돌려 실제 파일과 어긋나지 않게 한다
                SaveSystem.ResetCache();
                Refresh();
                return false;
            }

            Refresh();
            Changed?.Invoke();
            return true;
        }
    }
}
