using System.Collections.Generic;
using Samkuk.Core;
using Samkuk.Enemies;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 가까운 적들이 있는 곳에 불길(장판)을 깐다. 장판은 duration 동안 유지되며
    /// tickInterval마다 반지름 size 안의 적에게 피해를 준다. 한 번에 count개를 서로 다른 적 위치에 설치한다.
    /// </summary>
    public class FireZoneWeapon : Weapon
    {
        const int CandidateCount = 10;

        class Zone
        {
            public Vector2 position;
            public float left;
            public float tick;
            public SpriteRenderer sr;
        }

        readonly Enemy[] candidates = new Enemy[CandidateCount];
        readonly List<Zone> zones = new List<Zone>();
        readonly Stack<SpriteRenderer> idleRenderers = new Stack<SpriteRenderer>();
        readonly List<Enemy> hits = new List<Enemy>(64);
        float timer;

        public int ActiveZones => zones.Count;

        protected override void OnInitialized() => timer = Cooldown;

        void Update()
        {
            if (Enemies == null) return;

            float dt = Time.deltaTime;
            UpdateZones(dt);

            timer += dt;
            if (timer < Cooldown) return;

            int found = Enemies.FindNearest(OwnerPosition, Data.range, candidates);
            if (found == 0) return; // 대상이 없으면 준비 상태 유지

            timer = 0f;
            PlaceZones(found);
        }

        void PlaceZones(int found)
        {
            int toPlace = Mathf.Min(Count, found);

            // 후보 중 무작위로 서로 다른 적 위치를 고른다 (Fisher-Yates 부분 셔플)
            for (int i = 0; i < toPlace; i++)
            {
                int j = Random.Range(i, found);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                AddZone(candidates[i].Position);
            }
            System.Array.Clear(candidates, 0, candidates.Length);
        }

        void AddZone(Vector2 position)
        {
            var zone = new Zone
            {
                position = position,
                left = Data.duration,
                tick = 0f, // 설치 즉시 첫 피해
                sr = AcquireRenderer(position)
            };
            zones.Add(zone);
        }

        void UpdateZones(float dt)
        {
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                Zone z = zones[i];
                z.left -= dt;

                if (z.left <= 0f)
                {
                    ReleaseRenderer(z.sr);
                    zones.RemoveAt(i);
                    continue;
                }

                z.tick -= dt;
                if (z.tick <= 0f)
                {
                    z.tick = Mathf.Max(0.05f, Data.tickInterval);
                    Burn(z.position);
                }

                UpdateVisual(z);
            }
        }

        void Burn(Vector2 position)
        {
            hits.Clear();
            Enemies.OverlapCircle(position, Data.size, hits);
            float dmg = Damage;
            for (int i = 0; i < hits.Count; i++) hits[i].TakeDamage(dmg);
        }

        // ───────────────────────── 연출 ─────────────────────────

        SpriteRenderer AcquireRenderer(Vector2 position)
        {
            if (Data.sprite == null) return null;

            SpriteRenderer sr;
            if (idleRenderers.Count > 0)
            {
                sr = idleRenderers.Pop();
            }
            else
            {
                var go = new GameObject("FireZone");
                // 월드에 고정 (플레이어를 따라 움직이지 않도록 부모를 두지 않는다)
                sr = go.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = GameLayers.Sorting.Pickup;
                sr.sortingOrder = 5;
            }

            sr.sprite = Data.sprite;
            sr.transform.position = position;
            float size = Data.sprite.bounds.size.x;
            sr.transform.localScale = Vector3.one * (size > 0f ? Data.size * 2f / size : 1f);
            sr.gameObject.SetActive(true);
            return sr;
        }

        void ReleaseRenderer(SpriteRenderer sr)
        {
            if (sr == null) return;
            sr.gameObject.SetActive(false);
            idleRenderers.Push(sr);
        }

        void UpdateVisual(Zone z)
        {
            if (z.sr == null) return;

            float fade = Mathf.Clamp01(z.left / 0.5f); // 마지막 0.5초 동안 옅어짐
            float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 14f + z.position.x);
            var c = Data.tint;
            c.a *= 0.65f * fade * flicker;
            z.sr.color = c;
        }

        void OnDestroy()
        {
            foreach (var z in zones) if (z.sr != null) Destroy(z.sr.gameObject);
            foreach (var sr in idleRenderers) if (sr != null) Destroy(sr.gameObject);
        }
    }
}
