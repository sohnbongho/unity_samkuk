using System.Collections.Generic;
using Samkuk.Enemies;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 가장 가까운 적 방향으로 긴 창을 내질러 직선상(길이 range, 폭 size)의 모든 적을 꿰뚫는다.
    /// count가 2 이상이면 360도를 균등 분할한 방향으로 동시에 찌른다.
    /// </summary>
    public class ThrustWeapon : Weapon
    {
        readonly Enemy[] nearest = new Enemy[1];
        readonly List<Enemy> hits = new List<Enemy>(64);
        float timer;

        protected override void OnInitialized() => timer = Cooldown;

        void Update()
        {
            if (Enemies == null) return;

            timer += Time.deltaTime;
            if (timer < Cooldown) return;

            Vector2 origin = OwnerPosition;
            // 창이 닿을 만한 거리 안에 적이 있을 때만 찌른다 (헛손질로 쿨다운을 낭비하지 않음)
            if (Enemies.FindNearest(origin, Data.range + 1f, nearest) == 0) return;

            timer = 0f;
            PlayAttackSound();
            Vector2 toTarget = nearest[0].Position - origin;
            float baseAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            nearest[0] = null;

            int n = Mathf.Max(1, Count);
            for (int i = 0; i < n; i++)
            {
                float angle = baseAngle + 360f * i / n;
                Thrust(origin, angle);
            }
        }

        void Thrust(Vector2 origin, float angleDegrees)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float length = Data.range;
            float halfWidth = Data.size;

            hits.Clear();
            Enemies.OverlapCircle(origin + dir * (length * 0.5f), length * 0.5f + halfWidth, hits);

            float dmg = Damage;
            for (int i = 0; i < hits.Count; i++)
            {
                Enemy e = hits[i];
                Vector2 rel = e.Position - origin;
                float along = Vector2.Dot(rel, dir);
                float side = Mathf.Abs(rel.x * dir.y - rel.y * dir.x);

                if (along < -e.Radius || along > length + e.Radius) continue;
                if (side > halfWidth + e.Radius) continue;

                e.TakeDamage(dmg);
                Knock(e, origin);
            }

            PlayEffect(origin, dir, angleDegrees, length, halfWidth);
        }

        void PlayEffect(Vector2 origin, Vector2 dir, float angleDegrees, float length, float halfWidth)
        {
            if (Fx == null || Data.sprite == null) return;

            Vector2 spriteSize = Data.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

            var scale = new Vector2(length / spriteSize.x, Mathf.Clamp(halfWidth * 2f / spriteSize.y, 1f, 4f));
            Fx.Play(Data.sprite, origin + dir * (length * 0.5f), angleDegrees, scale, Data.tint, Mathf.Max(0.05f, Data.duration));
        }
    }
}
