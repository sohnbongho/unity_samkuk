using Samkuk.Enemies;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>가장 가까운 적을 향해 투사체를 부채꼴로 발사한다.</summary>
    public class ArrowWeapon : Weapon
    {
        const float SpreadDegrees = 12f;

        readonly Enemy[] nearest = new Enemy[1];
        float timer;

        protected override void OnInitialized() => timer = Cooldown; // 시작 즉시 첫 발사 가능

        void Update()
        {
            if (Enemies == null) return;

            timer += Time.deltaTime;
            if (timer < Cooldown) return;

            Vector2 origin = OwnerPosition;
            if (Enemies.FindNearest(origin, Data.range, nearest) == 0) return; // 대상이 없으면 준비 상태 유지

            timer = 0f;
            Vector2 toTarget = nearest[0].Position - origin;
            float baseAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            int shots = Count;

            for (int i = 0; i < shots; i++)
            {
                float angle = baseAngle + (i - (shots - 1) * 0.5f) * SpreadDegrees;
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

                Projectile p = Controller.GetProjectile();
                p.Launch(Enemies, Controller.ReleaseProjectile, origin, dir,
                    Damage, Data.projectileSpeed, Data.pierce, Data.duration,
                    Data.size, Data.sprite, Data.tint);
            }
            nearest[0] = null;
        }
    }
}
