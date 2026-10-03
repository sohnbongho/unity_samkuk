using System.Collections.Generic;
using Samkuk.Core;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 무기가 쓰는 일회성 스프라이트 이펙트(번개, 베기 궤적, 폭격 예고 등)를 풀링해서 재생한다.
    /// 각 이펙트는 지정한 시간 동안 보였다가 옅어지며 사라진다.
    /// </summary>
    public class WeaponFx : MonoBehaviour
    {
        const int MaxEffects = 160;

        class Entry
        {
            public SpriteRenderer sr;
            public float left;
            public float duration;
            public Color color;
            public float startScale;
            public float endScale;
            public Vector3 baseScale;
        }

        readonly List<Entry> active = new List<Entry>();
        readonly Stack<Entry> idle = new Stack<Entry>();
        int created;

        public int ActiveCount => active.Count;
        public int CreatedCount => created;

        /// <summary>
        /// 이펙트를 재생한다. scale은 월드 크기(스프라이트 원본 크기 기준 배율),
        /// startScale→endScale은 시간에 따른 추가 배율 변화(1 = 변화 없음).
        /// </summary>
        public void Play(Sprite sprite, Vector2 position, float rotationDegrees, Vector2 scale, Color color,
            float duration, float startScale = 1f, float endScale = 1f)
        {
            if (sprite == null) return;

            Entry e = Acquire();
            if (e == null) return;

            e.sr.sprite = sprite;
            e.sr.color = color;
            e.color = color;
            e.duration = Mathf.Max(0.02f, duration);
            e.left = e.duration;
            e.startScale = startScale;
            e.endScale = endScale;
            e.baseScale = new Vector3(scale.x, scale.y, 1f);

            var t = e.sr.transform;
            t.position = position;
            t.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            t.localScale = e.baseScale * startScale;
            e.sr.gameObject.SetActive(true);
            active.Add(e);
        }

        /// <summary>
        /// 지름이 diameter(월드 단위)가 되도록 스프라이트 크기를 맞춰 재생하는 편의 메서드 (원형 이펙트용).
        /// </summary>
        public void PlayDisc(Sprite sprite, Vector2 position, float diameter, Color color, float duration,
            float startScale = 1f, float endScale = 1f)
        {
            if (sprite == null) return;
            float size = sprite.bounds.size.x;
            float s = size > 0f ? diameter / size : 1f;
            Play(sprite, position, 0f, new Vector2(s, s), color, duration, startScale, endScale);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Entry e = active[i];
                e.left -= dt;
                if (e.left <= 0f)
                {
                    e.sr.gameObject.SetActive(false);
                    idle.Push(e);
                    active.RemoveAt(i);
                    continue;
                }

                float p = 1f - e.left / e.duration; // 0 → 1
                float k = Mathf.Lerp(e.startScale, e.endScale, p);
                e.sr.transform.localScale = e.baseScale * k;

                var c = e.color;
                c.a *= 1f - p;
                e.sr.color = c;
            }
        }

        Entry Acquire()
        {
            if (idle.Count > 0) return idle.Pop();
            if (created >= MaxEffects) return null;

            var go = new GameObject($"Fx{created}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = GameLayers.Sorting.Effect;
            go.SetActive(false);
            created++;
            return new Entry { sr = sr };
        }
    }
}
