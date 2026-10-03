using System.Collections.Generic;
using Samkuk.Core;
using Samkuk.Enemies;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 주기적으로 좌우를 번갈아 근접 범위(원)를 베어 적에게 피해를 준다.
    /// count가 2 이상이면 반대편도 동시에 벤다.
    /// </summary>
    public class SlashWeapon : Weapon
    {
        readonly List<Enemy> hits = new List<Enemy>(64);
        SpriteRenderer fx;
        float timer;
        float fxTimeLeft;
        int side = 1;

        protected override void OnInitialized()
        {
            timer = Cooldown;
            var go = new GameObject("SlashFx");
            go.transform.SetParent(transform, false);
            fx = go.AddComponent<SpriteRenderer>();
            fx.sprite = Data.sprite;
            fx.sortingLayerName = GameLayers.Sorting.Effect;
            fx.enabled = false;
        }

        void Update()
        {
            if (fx != null && fx.enabled)
            {
                fxTimeLeft -= Time.deltaTime;
                var c = Data.tint;
                c.a *= Mathf.Clamp01(fxTimeLeft / Mathf.Max(0.01f, Data.duration));
                fx.color = c;
                if (fxTimeLeft <= 0f) fx.enabled = false;
            }

            if (Enemies == null) return;

            timer += Time.deltaTime;
            if (timer < Cooldown) return;
            timer = 0f;

            Slash(side);
            if (Count >= 2) Slash(-side);
            side = -side;
        }

        void Slash(int dirSign)
        {
            float radius = Data.range * 0.6f;
            Vector2 center = OwnerPosition + new Vector2(dirSign * Data.range * 0.5f, 0f);

            hits.Clear();
            Enemies.OverlapCircle(center, radius, hits);
            float dmg = Damage;
            for (int i = 0; i < hits.Count; i++)
            {
                hits[i].TakeDamage(dmg);
                Knock(hits[i], OwnerPosition);
            }

            ShowEffect(center, radius);
        }

        void ShowEffect(Vector2 center, float radius)
        {
            if (fx == null || fx.sprite == null) return;

            // 두 번째(반대편) 베기는 같은 이펙트를 덮어쓰므로 마지막 위치만 보인다. 단순한 플레이스홀더 연출.
            float spriteSize = fx.sprite.bounds.size.x;
            float scale = spriteSize > 0f ? radius * 2f / spriteSize : 1f;
            fx.transform.position = center;
            fx.transform.localScale = Vector3.one * scale;
            fx.color = Data.tint;
            fx.enabled = true;
            fxTimeLeft = Data.duration;
        }
    }
}
