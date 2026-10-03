using System.Collections.Generic;
using Samkuk.Enemies;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 주변 적 중 무작위로 count마리를 골라 벼락을 떨어뜨린다.
    /// 벼락은 대상 주변 반지름 size 안의 모든 적에게 피해를 준다.
    /// </summary>
    public class LightningWeapon : Weapon
    {
        const int CandidateCount = 12;
        const float BoltHeight = 5f;

        readonly Enemy[] candidates = new Enemy[CandidateCount];
        readonly List<Enemy> hits = new List<Enemy>(32);
        float timer;

        protected override void OnInitialized() => timer = Cooldown;

        void Update()
        {
            if (Enemies == null) return;

            timer += Time.deltaTime;
            if (timer < Cooldown) return;

            int found = Enemies.FindNearest(OwnerPosition, Data.range, candidates);
            if (found == 0) return; // 대상이 없으면 준비 상태 유지

            timer = 0f;
            int strikes = Mathf.Min(Count, found);

            for (int i = 0; i < strikes; i++)
            {
                // 후보 중 서로 다른 적을 무작위로 선택 (부분 셔플)
                int j = Random.Range(i, found);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                Strike(candidates[i].Position);
            }
            System.Array.Clear(candidates, 0, candidates.Length);
        }

        void Strike(Vector2 position)
        {
            hits.Clear();
            Enemies.OverlapCircle(position, Data.size, hits);

            float dmg = Damage;
            for (int i = 0; i < hits.Count; i++)
            {
                hits[i].TakeDamage(dmg);
                Knock(hits[i], position);
            }

            PlayEffect(position);
        }

        void PlayEffect(Vector2 position)
        {
            if (Fx == null || Data.sprite == null) return;

            Vector2 size = Data.sprite.bounds.size;
            if (size.y <= 0f) return;

            // 번개 줄기: 땅(position)에서 위로 BoltHeight만큼
            float scaleY = BoltHeight / size.y;
            Fx.Play(Data.sprite, position + Vector2.up * (BoltHeight * 0.5f), 0f,
                new Vector2(scaleY * 0.6f, scaleY), Data.tint, Mathf.Max(0.05f, Data.duration));

            // 착탄 섬광
            Fx.PlayDisc(Data.sprite, position, Data.size * 2f, Data.tint, Mathf.Max(0.05f, Data.duration), 0.4f, 1.1f);
        }
    }
}
