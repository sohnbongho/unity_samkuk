using System.Collections.Generic;
using Samkuk.Core;
using UnityEngine;

namespace Samkuk.Feedback
{
    /// <summary>
    /// 작은 사각 입자를 흩뿌리는 풀링 이펙트 (타격 불꽃, 처치 파편, 레벨업 링 등).
    /// 입자는 속도가 점점 줄어들며 작아지고 옅어지다가 사라진다. 전체 개수에 상한이 있어 많이 몰려도 안전하다.
    /// </summary>
    public class BurstFx : MonoBehaviour
    {
        public const int MaxParticles = 400;
        const float Drag = 3f;
        const float PixelsPerUnit = 16f; // 4px 정사각 스프라이트 = 0.25 월드 단위 (size 1)

        class Particle
        {
            public SpriteRenderer sr;
            public Vector2 velocity;
            public float life;
            public float age;
            public float size;
            public Color color;
        }

        static Sprite squareSprite;

        readonly List<Particle> active = new List<Particle>(128);
        readonly Stack<Particle> idle = new Stack<Particle>();
        int created;

        public int ActiveCount => active.Count;
        public int CreatedCount => created;

        /// <summary>
        /// position에서 사방으로 count개의 입자를 흩뿌린다.
        /// speed: 초기 속도(무작위로 50~100%), life: 지속 시간(초), size: 입자 크기(1 = 0.25 월드 단위).
        /// </summary>
        public void Burst(Vector2 position, int count, Color color, float speed, float life, float size)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                if (!Spawn(position, angle, speed * Random.Range(0.5f, 1f), life, size, color)) return;
            }
        }

        /// <summary>position에서 count개의 입자가 같은 속도로 고르게 퍼지는 링을 만든다.</summary>
        public void Ring(Vector2 position, int count, Color color, float speed, float life, float size)
        {
            float offset = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                float angle = offset + Mathf.PI * 2f * i / count;
                if (!Spawn(position, angle, speed, life, size, color)) return;
            }
        }

        bool Spawn(Vector2 position, float angle, float speed, float life, float size, Color color)
        {
            Particle p = Acquire();
            if (p == null) return false; // 상한 도달

            p.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            p.life = Mathf.Max(0.05f, life);
            p.age = 0f;
            p.size = size;
            p.color = color;

            p.sr.color = color;
            p.sr.transform.position = position;
            p.sr.transform.localScale = Vector3.one * size;
            p.sr.gameObject.SetActive(true);
            active.Add(p);
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float damp = Mathf.Exp(-Drag * dt);

            for (int i = active.Count - 1; i >= 0; i--)
            {
                Particle p = active[i];
                p.age += dt;
                if (p.age >= p.life)
                {
                    p.sr.gameObject.SetActive(false);
                    idle.Push(p);
                    active.RemoveAt(i);
                    continue;
                }

                p.velocity *= damp;
                var t = p.sr.transform;
                t.position += (Vector3)(p.velocity * dt);

                float progress = p.age / p.life;
                t.localScale = Vector3.one * (p.size * (1f - 0.6f * progress));
                var c = p.color;
                c.a *= 1f - progress;
                p.sr.color = c;
            }
        }

        Particle Acquire()
        {
            if (idle.Count > 0) return idle.Pop();
            if (created >= MaxParticles) return null;

            var go = new GameObject($"P{created}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSquareSprite();
            sr.sortingLayerName = GameLayers.Sorting.Effect;
            sr.sortingOrder = 4;
            go.SetActive(false);
            created++;
            return new Particle { sr = sr };
        }

        /// <summary>모든 입자가 공유하는 4x4 흰색 정사각 스프라이트 (에셋 없이 코드로 만든다).</summary>
        static Sprite GetSquareSprite()
        {
            if (squareSprite != null) return squareSprite;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "BurstSquare" };
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            squareSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            squareSprite.name = "BurstSquare";
            return squareSprite;
        }
    }
}
