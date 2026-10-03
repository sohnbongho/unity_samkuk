using System.Collections.Generic;
using Samkuk.Core;
using Samkuk.Enemies;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 플레이어 위치에서 충격파가 퍼져 나간다. 반지름이 0에서 range까지 duration 동안 커지며
    /// 닿은 적에게 한 번씩 피해를 주고 밀어낸다.
    /// </summary>
    public class NovaWeapon : Weapon
    {
        const float StartRadius = 0.3f;

        readonly Enemy[] nearest = new Enemy[1];
        readonly List<Enemy> hits = new List<Enemy>(128);
        readonly HashSet<Enemy> alreadyHit = new HashSet<Enemy>();

        SpriteRenderer ring;
        Vector2 center;
        float elapsed;
        float timer;
        bool expanding;

        public bool IsExpanding => expanding;

        protected override void OnInitialized()
        {
            timer = Cooldown;

            var go = new GameObject("NovaRing");
            ring = go.AddComponent<SpriteRenderer>();
            ring.sprite = Data.sprite;
            ring.sortingLayerName = GameLayers.Sorting.Effect;
            ring.enabled = false;
        }

        void Update()
        {
            if (Enemies == null) return;

            float dt = Time.deltaTime;

            if (expanding)
            {
                Expand(dt);
                return;
            }

            timer += dt;
            if (timer < Cooldown) return;

            // 충격파가 닿을 거리 안에 적이 있을 때만 발동
            if (Enemies.FindNearest(OwnerPosition, Data.range, nearest) == 0) return;
            nearest[0] = null;

            timer = 0f;
            PlayAttackSound();
            Begin();
        }

        void Begin()
        {
            expanding = true;
            elapsed = 0f;
            center = OwnerPosition;
            alreadyHit.Clear();
            if (ring != null && ring.sprite != null) ring.enabled = true;
        }

        void Expand(float dt)
        {
            elapsed += dt;
            float duration = Mathf.Max(0.05f, Data.duration);
            float t = Mathf.Clamp01(elapsed / duration);
            float radius = Mathf.Lerp(StartRadius, Data.range, t);

            hits.Clear();
            Enemies.OverlapCircle(center, radius, hits);
            float dmg = Damage;
            for (int i = 0; i < hits.Count; i++)
            {
                Enemy e = hits[i];
                if (!alreadyHit.Add(e)) continue; // 한 번의 충격파에는 한 번만 맞는다
                e.TakeDamage(dmg);
                Knock(e, center);
            }

            UpdateRing(radius, t);

            if (t >= 1f)
            {
                expanding = false;
                if (ring != null) ring.enabled = false;
            }
        }

        void UpdateRing(float radius, float t)
        {
            if (ring == null || ring.sprite == null) return;

            float size = ring.sprite.bounds.size.x;
            ring.transform.position = center;
            ring.transform.localScale = Vector3.one * (size > 0f ? radius * 2f / size : 1f);

            var c = Data.tint;
            c.a *= 1f - t;
            ring.color = c;
        }

        void OnDestroy()
        {
            if (ring != null) Destroy(ring.gameObject);
        }
    }
}
