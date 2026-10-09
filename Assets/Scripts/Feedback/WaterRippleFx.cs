using System.Collections.Generic;
using Samkuk.Allies;
using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.World;
using UnityEngine;

namespace Samkuk.Feedback
{
    /// <summary>
    /// 물(강, 연못)에 들어간 것의 발밑에 물결을 그린다. 지형 이동(<see cref="TerrainCollision"/>)으로 느려지는 자리가
    /// 어디인지 눈에 보이게 하는 연출이다. 플레이어·아군은 물에 들어가는 순간 큰 물결 + 물방울 + 소리(첨벙),
    /// 물 안에서 걸으면 발밑에 작은 물결이 이어진다. 적은 소리 없이 물결만, 수가 많아도 초당 개수를 제한한다.
    /// 물결 그림은 코드로 만든 둥근 테(에셋 없음)를 납작하게 눌러 쓰고, 배경 정렬 레이어(강물 위, 캐릭터 아래)에 그린다.
    /// <c>InfiniteBackground</c> 가 지형 맵을 적용할 때 소품 루트에 함께 붙인다.
    /// </summary>
    public class WaterRippleFx : MonoBehaviour
    {
        public const int MaxRipples = 160;
        const int SortingOrder = 150;              // 강물(3)/연못(4)/소품(5~105) 위. 물결은 물 위에만 생기므로 소품을 가리지 않는다
        const float RippleZ = 0.4f;                // 소품(0.5) 바로 앞
        const float WalkInterval = 0.17f;          // 물 안에서 걸을 때 물결 간격
        const float IdleInterval = 0.75f;          // 물 안에 서 있을 때
        const float MovingSpeed = 0.25f;           // 이 속도보다 빠르면 "걷는 중"
        const float EnemyRipplesPerSecond = 2f;    // 물속 적 한 마리가 내는 물결 (평균)
        const int EnemyRipplesPerFrameCap = 6;     // 떼로 강을 건널 때 한 프레임에 만드는 상한
        const float LookupInterval = 0.5f;         // 플레이어/아군/적 관리자가 아직 없으면 다시 찾는 간격

        static readonly Color RippleColor = new Color(0.86f, 0.95f, 1f, 0.6f);
        static readonly Color DropColor = new Color(0.62f, 0.82f, 1f, 0.9f);

        class Ripple
        {
            public SpriteRenderer sr;
            public float age, life, startSize, endSize, alpha;
        }

        /// <summary>물결을 내는 것 하나의 상태 (플레이어, 아군).</summary>
        class Wader
        {
            public Transform transform;
            public AllyController ally;   // 아군이면 쓰러짐 확인용 (매 프레임 GetComponent 하지 않게 한 번만 찾는다)
            public Vector2 lastPos;
            public bool inWater;
            public float timer;
            public bool makesSound;
        }

        static Sprite ringSprite;

        TerrainCollision terrain;
        Material material;
        Transform root;
        readonly List<Ripple> active = new List<Ripple>(64);
        readonly Stack<Ripple> idle = new Stack<Ripple>();
        readonly List<Wader> waders = new List<Wader>(4);
        readonly HashSet<Transform> known = new HashSet<Transform>();
        EnemyManager enemies;
        AllyManager allies;
        BurstFx burst;
        float lookupTimer;
        int created;

        public int ActiveCount => active.Count;
        public int CreatedCount => created;
        /// <summary>지금까지 물에 "들어간" 횟수 (플레이어/아군, 테스트용).</summary>
        public int SplashCount { get; private set; }

        /// <summary>지형 충돌(물 위치)과 그림 재질(배경과 같은 조명을 받게)을 정한다. 플레이어/아군/적은 스스로 찾는다.</summary>
        public void Initialize(TerrainCollision collision, Material lit)
        {
            terrain = collision;
            material = lit;
            if (root == null)
            {
                root = new GameObject("Ripples").transform;
                root.SetParent(transform, false);
            }
        }

        /// <summary>물결을 낼 대상을 직접 등록한다 (테스트용. 보통은 플레이어/아군을 스스로 찾는다).</summary>
        public void Track(Transform t, bool makesSound)
        {
            if (t == null || !known.Add(t)) return;
            waders.Add(new Wader
            {
                transform = t, ally = t.GetComponent<AllyController>(), lastPos = t.position,
                inWater = terrain != null && terrain.SpeedFactor(t.position) < 1f, makesSound = makesSound,
            });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Advance(dt);
            if (terrain == null || dt <= 0f) return;

            Lookup(dt);
            for (int i = waders.Count - 1; i >= 0; i--)
            {
                var w = waders[i];
                if (w.transform == null || !w.transform.gameObject.activeInHierarchy) { known.Remove(w.transform); waders.RemoveAt(i); continue; }
                Tick(w, dt);
            }
            TickEnemies(dt);
        }

        /// <summary>플레이어·아군·적 관리자를 찾아 둔다 (씬 구성 순서에 상관없이 늦게 생겨도 잡는다).</summary>
        void Lookup(float dt)
        {
            lookupTimer -= dt;
            if (lookupTimer > 0f) return;
            lookupTimer = LookupInterval;

            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) Track(pc.transform, true);
            if (allies == null) allies = FindAnyObjectByType<AllyManager>();
            if (allies != null)
                foreach (var a in allies.Allies)
                    if (a != null) Track(a.transform, true);
            if (enemies == null) enemies = FindAnyObjectByType<EnemyManager>();
            if (burst == null) burst = FindAnyObjectByType<BurstFx>();
        }

        void Tick(Wader w, float dt)
        {
            Vector2 pos = w.transform.position;
            bool downed = w.ally != null && w.ally.IsDowned;   // 쓰러진 아군은 물결을 내지 않는다

            bool inWater = !downed && terrain.SpeedFactor(pos) < 1f;
            if (inWater && !w.inWater) Splash(pos, w.makesSound);
            w.inWater = inWater;

            if (inWater)
            {
                bool moving = (pos - w.lastPos).magnitude / dt > MovingSpeed;
                w.timer -= dt;
                if (w.timer <= 0f)
                {
                    w.timer = moving ? WalkInterval : IdleInterval;
                    Spawn(pos + Random.insideUnitCircle * 0.08f, moving ? 0.22f : 0.15f, moving ? 0.95f : 0.7f, moving ? 0.55f : 0.8f, moving ? 1f : 0.6f);
                }
            }
            w.lastPos = pos;
        }

        /// <summary>물속 적의 물결: 마리마다 상태를 두지 않고 확률로 낸다 (수백 마리여도 가볍게).</summary>
        void TickEnemies(float dt)
        {
            if (enemies == null) return;
            var list = enemies.Active;
            float chance = Mathf.Clamp01(EnemyRipplesPerSecond * dt);
            int made = 0;
            for (int i = 0; i < list.Count && made < EnemyRipplesPerFrameCap; i++)
            {
                var e = list[i];
                if (!e.Alive || Random.value >= chance) continue;
                Vector2 pos = e.Position;
                if (terrain.SpeedFactor(pos) >= 1f) continue;
                Spawn(pos, 0.18f, 0.7f * Mathf.Max(0.6f, e.Radius / 0.3f), 0.5f, 0.7f);
                made++;
            }
        }

        /// <summary>물에 들어가는 순간: 큰 물결 두 겹 + 물방울 + 첨벙 소리.</summary>
        void Splash(Vector2 pos, bool sound)
        {
            SplashCount++;
            Spawn(pos, 0.25f, 1.5f, 0.6f, 1f);
            Spawn(pos, 0.15f, 1.0f, 0.45f, 0.9f);
            burst?.Burst(pos + Vector2.up * 0.1f, 7, DropColor, 2.6f, 0.35f, 0.7f);
            if (sound) AudioManager.Play(SfxId.Splash, 0.8f);
        }

        void Spawn(Vector2 pos, float startSize, float endSize, float life, float alpha)
        {
            var r = Acquire();
            if (r == null) return;   // 상한
            r.age = 0f; r.life = life; r.startSize = startSize; r.endSize = endSize; r.alpha = alpha;
            r.sr.transform.position = new Vector3(pos.x, pos.y, RippleZ);
            r.sr.transform.localScale = new Vector3(startSize, startSize * 0.5f, 1f);
            var c = RippleColor; c.a *= alpha;
            r.sr.color = c;
            r.sr.gameObject.SetActive(true);
            active.Add(r);
        }

        void Advance(float dt)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var r = active[i];
                r.age += dt;
                if (r.age >= r.life)
                {
                    r.sr.gameObject.SetActive(false);
                    idle.Push(r);
                    active.RemoveAt(i);
                    continue;
                }
                float t = r.age / r.life;
                float size = Mathf.Lerp(r.startSize, r.endSize, 1f - (1f - t) * (1f - t));   // 처음 빠르게 퍼지다 느려진다
                r.sr.transform.localScale = new Vector3(size, size * 0.5f, 1f);              // 납작한 타원 = 바닥에 깔린 물결
                var c = RippleColor;
                c.a *= r.alpha * (1f - t);
                r.sr.color = c;
            }
        }

        Ripple Acquire()
        {
            if (idle.Count > 0) return idle.Pop();
            if (created >= MaxRipples) return null;
            if (root == null) Initialize(terrain, material);

            var go = new GameObject($"Ripple{created}");
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RingSprite;
            sr.sortingLayerName = GameLayers.Sorting.Background;
            sr.sortingOrder = SortingOrder;
            if (material != null) sr.sharedMaterial = material;
            go.SetActive(false);
            created++;
            return new Ripple { sr = sr };
        }

        /// <summary>흰 둥근 테 (지름 1유닛, 테 두께 약 10%, 바깥/안쪽 가장자리가 부드럽다). 에셋 없이 코드로 만든다.</summary>
        public static Sprite RingSprite
        {
            get
            {
                if (ringSprite != null) return ringSprite;
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "WaterRing" };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.12f);   // 반지름 0.86 둘레의 테
                        a = a * a * (3f - 2f * a);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                ringSprite.name = "WaterRing";
                ringSprite.hideFlags = HideFlags.HideAndDontSave;
                return ringSprite;
            }
        }
    }
}
