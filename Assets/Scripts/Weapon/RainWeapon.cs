using System.Collections.Generic;
using Samkuk.Enemies;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 주변 적 위치에 화살비를 쏟는다. 낙하 지점에 예고 표시가 뜨고 duration초 뒤에
    /// 반지름 size 안의 적에게 폭격 피해를 준다. 한 번에 count발.
    /// </summary>
    public class RainWeapon : Weapon
    {
        const int CandidateCount = 12;
        const float Scatter = 0.6f;

        class Impact
        {
            public Vector2 position;
            public float left;
        }

        readonly Enemy[] candidates = new Enemy[CandidateCount];
        readonly List<Impact> pending = new List<Impact>();
        readonly List<Enemy> hits = new List<Enemy>(32);
        float timer;

        public int PendingImpacts => pending.Count;

        protected override void OnInitialized() => timer = Cooldown;

        void Update()
        {
            if (Enemies == null) return;

            float dt = Time.deltaTime;
            UpdateImpacts(dt);

            timer += dt;
            if (timer < Cooldown) return;

            int found = Enemies.FindNearest(OwnerPosition, Data.range, candidates);
            if (found == 0) return; // 대상이 없으면 준비 상태 유지

            timer = 0f;
            int arrows = Mathf.Max(1, Count);
            for (int i = 0; i < arrows; i++)
            {
                // 가까운 적들 중 무작위로 골라 그 근처에 떨어뜨린다 (같은 적에 여러 발도 가능)
                Enemy target = candidates[Random.Range(0, found)];
                Vector2 pos = target.Position + Random.insideUnitCircle * Scatter;
                Schedule(pos);
            }
            System.Array.Clear(candidates, 0, candidates.Length);
        }

        void Schedule(Vector2 position)
        {
            float delay = Mathf.Max(0.05f, Data.duration);
            pending.Add(new Impact { position = position, left = delay });

            // 낙하 예고: 붉은 원이 점점 작아지며 옅어짐
            if (Fx != null && Data.sprite != null)
                Fx.PlayDisc(Data.sprite, position, Data.size * 2f, new Color(1f, 0.3f, 0.2f, 0.7f), delay, 1.2f, 0.7f);
        }

        void UpdateImpacts(float dt)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Impact impact = pending[i];
                impact.left -= dt;
                if (impact.left > 0f) continue;

                pending.RemoveAt(i);
                Explode(impact.position);
            }
        }

        void Explode(Vector2 position)
        {
            hits.Clear();
            Enemies.OverlapCircle(position, Data.size, hits);

            float dmg = Damage;
            for (int i = 0; i < hits.Count; i++)
            {
                hits[i].TakeDamage(dmg);
                Knock(hits[i], position);
            }

            if (Fx != null && Data.sprite != null)
                Fx.PlayDisc(Data.sprite, position, Data.size * 2f, Data.tint, 0.2f, 0.6f, 1.2f);
        }
    }
}
