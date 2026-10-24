using System.Collections.Generic;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Pickups;
using Samkuk.Weapons;
using UnityEngine;

namespace Samkuk.Coop
{
    /// <summary>협동 전투에서 서버 id(에셋 파일명) → 에셋. 스테이지의 적, 선택지 카탈로그/진화/장수 전용 무기, 장수 카탈로그에서 모은다.</summary>
    public sealed class CoopCatalog
    {
        readonly Dictionary<string, EnemyData> enemies = new Dictionary<string, EnemyData>();
        readonly Dictionary<string, WeaponData> weapons = new Dictionary<string, WeaponData>();
        readonly Dictionary<string, HeroData> heroes = new Dictionary<string, HeroData>();

        /// <summary>EnemySpawn 을 못 받고 스냅샷에 먼저 나타난 적(관심 영역 경계)에 쓰는 임시 종류.</summary>
        public EnemyData FallbackEnemy { get; private set; }

        public CoopCatalog(StageData stage, UpgradeCatalog upgrades, HeroCatalog heroCatalog)
        {
            if (stage != null)
            {
                foreach (var w in stage.waves) foreach (var we in w.enemies) AddEnemy(we.data);
                foreach (var ev in stage.events) AddEnemy(ev.enemy);
            }
            if (upgrades != null)
            {
                foreach (var w in upgrades.weapons) AddWeapon(w);
                foreach (var e in upgrades.evolutions)
                {
                    if (e == null) continue;
                    AddWeapon(e.baseWeapon);
                    AddWeapon(e.evolvedWeapon);
                }
            }
            if (heroCatalog != null)
            {
                foreach (var h in heroCatalog.heroes)
                {
                    if (h == null) continue;
                    heroes[h.name] = h;
                    AddWeapon(h.startingWeapon);
                    foreach (var w in h.weapons) AddWeapon(w);
                }
            }
        }

        void AddEnemy(EnemyData e)
        {
            if (e == null) return;
            enemies[e.name] = e;
            if (FallbackEnemy == null || e.maxHp < FallbackEnemy.maxHp) FallbackEnemy = e; // 가장 약한 적
        }

        void AddWeapon(WeaponData w)
        {
            if (w != null) weapons[w.name] = w;
        }

        public EnemyData Enemy(string id) => id != null && enemies.TryGetValue(id, out var e) ? e : null;
        public WeaponData Weapon(string id) => id != null && weapons.TryGetValue(id, out var w) ? w : null;
        public HeroData Hero(string id) => id != null && heroes.TryGetValue(id, out var h) ? h : null;
    }

    /// <summary>서버 적 id 별 Enemy 프리팹 꼭두각시 풀. 로직 없이 그림(걷기, 깜빡임, 사망 연출)만 쓴다.</summary>
    public sealed class EnemyPuppetPool
    {
        readonly Enemy prefab;
        readonly Transform root;
        readonly Stack<Enemy> free = new Stack<Enemy>();
        readonly Dictionary<int, Enemy> active = new Dictionary<int, Enemy>();

        public int ActiveCount => active.Count;

        public EnemyPuppetPool(Enemy prefab, Transform root)
        {
            this.prefab = prefab;
            this.root = root;
        }

        public bool TryGet(int id, out Enemy e) => active.TryGetValue(id, out e);

        public Enemy Show(int id, EnemyData data, Vector2 position)
        {
            if (active.TryGetValue(id, out var e)) return e;
            if (prefab == null || data == null) return null;
            e = free.Count > 0 ? free.Pop() : Object.Instantiate(prefab, root);
            e.gameObject.SetActive(true);
            e.Init(data, position);
            e.DespawnHandler = null;
            active.Add(id, e);
            return e;
        }

        public void Hide(int id)
        {
            if (!active.TryGetValue(id, out var e)) return;
            active.Remove(id);
            e.gameObject.SetActive(false);
            free.Push(e);
        }

        public void HideAll()
        {
            foreach (var e in active.Values) { e.gameObject.SetActive(false); free.Push(e); }
            active.Clear();
        }
    }

    /// <summary>경험치 보석 꼭두각시 풀.</summary>
    public sealed class GemPuppetPool
    {
        readonly ExpGem prefab;
        readonly Transform root;
        readonly Stack<ExpGem> free = new Stack<ExpGem>();
        readonly Dictionary<int, ExpGem> active = new Dictionary<int, ExpGem>();

        public GemPuppetPool(ExpGem prefab, Transform root)
        {
            this.prefab = prefab;
            this.root = root;
        }

        public ExpGem Show(int id, Vector2 position, int value)
        {
            if (active.TryGetValue(id, out var g)) return g;
            if (prefab == null) return null;
            g = free.Count > 0 ? free.Pop() : Object.Instantiate(prefab, root);
            g.gameObject.SetActive(true);
            g.Init(position, value);
            active.Add(id, g);
            return g;
        }

        public void Move(int id, Vector2 position)
        {
            if (active.TryGetValue(id, out var g)) g.transform.position = position;
        }

        public void Hide(int id)
        {
            if (!active.TryGetValue(id, out var g)) return;
            active.Remove(id);
            g.gameObject.SetActive(false);
            free.Push(g);
        }
    }

    /// <summary>서버 투사체 하나의 그림: 스폰 이벤트의 방향·속도로 외삽해 움직이고 소멸 이벤트나 수명으로 사라진다.</summary>
    public sealed class ProjectilePuppetPool
    {
        sealed class View
        {
            public SpriteRenderer sr;
            public Vector2 dir;
            public float speed, life;
        }

        readonly Transform root;
        readonly Stack<View> free = new Stack<View>();
        readonly Dictionary<int, View> active = new Dictionary<int, View>();
        readonly List<int> expired = new List<int>();

        public int ActiveCount => active.Count;

        public ProjectilePuppetPool(Transform root) => this.root = root;

        public void Spawn(int id, Vector2 position, Vector2 dir, float speed, float lifetime, Sprite sprite, Color tint, float scale)
        {
            if (active.ContainsKey(id)) Despawn(id);
            View v = free.Count > 0 ? free.Pop() : Create();
            v.dir = dir;
            v.speed = speed;
            v.life = lifetime;
            v.sr.sprite = sprite;
            v.sr.color = tint;
            v.sr.gameObject.SetActive(true);
            v.sr.transform.position = position;
            v.sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            v.sr.transform.localScale = Vector3.one * scale;
            active.Add(id, v);
        }

        View Create()
        {
            var go = new GameObject("Projectile");
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = GameLayers.Sorting.Projectile;
            return new View { sr = sr };
        }

        public bool Despawn(int id)
        {
            if (!active.TryGetValue(id, out var v)) return false;
            active.Remove(id);
            v.sr.gameObject.SetActive(false);
            free.Push(v);
            return true;
        }

        public void Tick(float dt)
        {
            expired.Clear();
            foreach (var kv in active)
            {
                var v = kv.Value;
                v.sr.transform.position += (Vector3)(v.dir * (v.speed * dt));
                v.life -= dt;
                if (v.life <= 0f) expired.Add(kv.Key);
            }
            foreach (int id in expired) Despawn(id);
        }
    }

    /// <summary>
    /// 한 플레이어의 무기 휘두르기 그림(들고 있는 무기 + 검기). WeaponFire 이벤트의 motion 에 따라 SlashWeapon 과 같은 동작을 그리되 판정은 없다.
    /// 내려베기/올려베기/휩쓸기는 호를 그리며 돌고 타격 지점에서 반원 검기가, 찌르기는 뾰족한 검기만 앞으로 나간다.
    /// </summary>
    public sealed class SwingPuppet
    {
        public const int MotionNone = 0, MotionSwingDown = 1, MotionSwingUp = 2, MotionThrust = 3, MotionSweep = 4;

        readonly SwingMotion swing = new SwingMotion();
        readonly SpriteRenderer held;
        readonly WeaponTrail arc, point;
        WeaponData data;
        Vector2 dir;
        Color tint = Color.white;

        public bool IsSwinging => swing.Active;

        public SwingPuppet(Transform root, string name)
        {
            held = Make(root, name + "Held", null, 1);
            Samkuk.World.StandingSprite.Attach(held.gameObject, 0f, Samkuk.World.TiltPreset.HeldToward);   // 2.5D 틸트면 몸과 같은 평면에 세운다
            arc = new WeaponTrail(Make(root, name + "Arc", WaveSprites.Arc(), 0));
            point = new WeaponTrail(Make(root, name + "Point", WaveSprites.Point(), 0));
        }

        static SpriteRenderer Make(Transform root, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = GameLayers.Sorting.Effect;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        /// <summary>휘두르기 시작. 휘두르는 시간(초)을 돌려준다(몸이 그쪽을 보는 시간).</summary>
        public float Play(WeaponData weapon, Vector2 direction, int motion, Vector2 ownerPosition)
        {
            data = weapon;
            dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.right;
            tint = weapon != null ? weapon.tint : Color.white;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float range = weapon != null ? weapon.range : 2f;
            float duration = weapon != null && weapon.swingDuration > 0f ? weapon.swingDuration : SwingMotion.DefaultDuration;

            switch (motion)
            {
                case MotionThrust:
                {
                    // 창 끝에서 뾰족한 검기가 직선으로 (ThrustWeapon 과 같은 그림, 동작 없이)
                    float distance = WeaponTrail.DistanceFor(weapon, range * 0.5f);
                    point.Start(ownerPosition + dir * (range * 0.5f) + new Vector2(0f, SlashWeapon.HandHeight), dir, angle,
                        range * 0.35f, WaveSprites.RadiusUnits, distance, WeaponTrail.SecondsFor(weapon, distance), tint);
                    return duration;
                }
                case MotionSwingDown:
                case MotionSwingUp:
                case MotionSweep:
                {
                    float arcDegrees = motion == MotionSweep ? 200f : (weapon != null ? weapon.swingArcDegrees : 0f);
                    swing.Start(angle, arcDegrees, duration, upward: motion == MotionSwingUp);
                    if (held != null) held.sprite = weapon != null ? weapon.heldSprite : null;
                    return duration;
                }
                default:
                    return 0f;
            }
        }

        public void Tick(float dt, Vector2 ownerPosition)
        {
            TickTrail(arc, dt);
            TickTrail(point, dt);
            if (!swing.Active) { if (held != null) held.enabled = false; return; }

            bool hit = swing.Advance(dt);
            if (hit) Strike(ownerPosition);

            if (held == null) return;
            if (!swing.Active || held.sprite == null) { held.enabled = false; return; }
            held.enabled = true;
            held.flipX = swing.FacesLeft;
            held.transform.position = Samkuk.World.TerrainHeight.Feet(ownerPosition) + Samkuk.World.TiltView.Lift(SlashWeapon.HandHeight, ownerPosition);
            held.transform.rotation = Quaternion.Euler(0f, 0f, swing.Angle - 90f);
        }

        void Strike(Vector2 ownerPosition)
        {
            float range = data != null ? data.range : 2f;
            float radius = range * 0.6f;
            Vector2 center = ownerPosition + dir * (range * 0.5f);
            float scale = data != null && data.trailScale > 0f ? data.trailScale : 1f;
            float distance = WeaponTrail.DistanceFor(data, range * SlashWeapon.TrailTravel);
            arc.Start(center + new Vector2(0f, SlashWeapon.HandHeight), dir, swing.CenterAngle, radius * scale, WaveSprites.RadiusUnits,
                distance, WeaponTrail.SecondsFor(data, distance), tint);
        }

        void TickTrail(WeaponTrail trail, float dt)
        {
            if (trail.Renderer != null && trail.Renderer.enabled) trail.Tick(dt, tint);
        }

        public void Stop()
        {
            swing.Stop();
            arc.Stop();
            point.Stop();
            if (held != null) held.enabled = false;
        }
    }

    /// <summary>한 플레이어의 궤도(회전 도끼) 칼날 그림. 각도는 서버와 같은 결정적 식(<see cref="CoopBattleModel.OrbitAngle"/>)이라 필드 없이 맞는다.</summary>
    public sealed class OrbitPuppet
    {
        readonly Transform root;
        readonly List<SpriteRenderer> blades = new List<SpriteRenderer>();
        public WeaponData Data { get; private set; }

        public OrbitPuppet(Transform root, WeaponData data, int count)
        {
            this.root = root;
            Data = data;
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                var go = new GameObject("Blade" + i);
                go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = data.sprite;
                sr.color = data.tint;
                sr.sortingLayerName = GameLayers.Sorting.Projectile;
                blades.Add(sr);
            }
        }

        public void Tick(Vector2 center, float elapsedSeconds)
        {
            int n = blades.Count;
            for (int i = 0; i < n; i++)
            {
                float a = CoopBattleModel.OrbitAngle(elapsedSeconds, Data.rotateSpeed, i, n);
                float rad = a * Mathf.Deg2Rad;
                blades[i].transform.position = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Data.range;
                blades[i].transform.rotation = Quaternion.Euler(0f, 0f, a * 2f);
            }
        }

        public void Destroy()
        {
            foreach (var b in blades) if (b != null) Object.Destroy(b.gameObject);
            blades.Clear();
        }
    }

    /// <summary>코드로 만든 연출 스프라이트 (범위 연출용 원판).</summary>
    public static class CoopSprites
    {
        static Sprite disc;

        /// <summary>가장자리가 부드러운 지름 1유닛(64px, PPU 64) 원판.</summary>
        public static Sprite Disc()
        {
            if (disc != null) return disc;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = d >= 1f ? 0f : Mathf.Clamp01((1f - d) * 3f) * (0.35f + 0.65f * (1f - d));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            disc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return disc;
        }
    }
}
