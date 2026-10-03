using System.Collections.Generic;
using Samkuk.Core;
using Samkuk.Enemies;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>플레이어 주위를 도는 칼날. 닿은 적에게 일정 간격으로 피해를 준다.</summary>
    public class OrbitWeapon : Weapon
    {
        readonly List<Transform> blades = new List<Transform>();
        readonly List<Enemy> hits = new List<Enemy>(32);
        float angle;
        float tickTimer;

        protected override void OnInitialized() => RebuildBlades();
        protected override void OnLevelChanged() => RebuildBlades();

        void RebuildBlades()
        {
            int wanted = Count;
            while (blades.Count < wanted)
            {
                var go = new GameObject($"Blade{blades.Count}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Data.sprite;
                sr.color = Data.tint;
                sr.sortingLayerName = GameLayers.Sorting.Projectile;
                blades.Add(go.transform);
            }
            while (blades.Count > wanted)
            {
                Destroy(blades[blades.Count - 1].gameObject);
                blades.RemoveAt(blades.Count - 1);
            }
        }

        void Update()
        {
            if (Owner == null) return;

            angle = (angle + Data.rotateSpeed * Time.deltaTime) % 360f;

            Vector2 center = OwnerPosition;
            int n = blades.Count;
            for (int i = 0; i < n; i++)
            {
                float a = (angle + 360f * i / n) * Mathf.Deg2Rad;
                blades[i].position = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Data.range;
                blades[i].rotation = Quaternion.Euler(0f, 0f, angle * 2f);
            }

            if (Enemies == null) return;

            tickTimer += Time.deltaTime;
            if (tickTimer < Data.tickInterval) return;
            tickTimer = 0f;

            float dmg = Damage;
            for (int i = 0; i < n; i++)
            {
                hits.Clear();
                Enemies.OverlapCircle(blades[i].position, Data.size, hits);
                for (int k = 0; k < hits.Count; k++) hits[k].TakeDamage(dmg);
            }
        }
    }
}
