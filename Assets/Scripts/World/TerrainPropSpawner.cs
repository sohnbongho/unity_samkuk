using System.Collections.Generic;
using Samkuk.Core;
using Samkuk.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Samkuk.World
{
    /// <summary>
    /// 카메라 주변의 칸(<see cref="TerrainMap.ChunkSize"/> 단위)에만 지형 소품을 만들고, 멀어지면 풀로 돌려보낸다.
    /// 무한한 세계를 이동해도 화면 주변 100개 안팎의 스프라이트만 유지한다. 소품은 장식이라 충돌이 없고
    /// 배경 정렬 레이어에 그려져 적/플레이어/투사체/보석 뒤에 깔린다.
    /// 빛이 있는 소품(<see cref="Samkuk.Data.TerrainProp.HasLight"/>: 깃발의 횃불, 연못)은 점광원 Light2D 를 함께 만든다
    /// (HD-2D 조명, <see cref="Hd2dSettings.Lighting"/> 이 켜져 있을 때만). 화면 주변 칸에만 소품이 있으니 빛 개수도 자연히 제한된다.
    /// 서 있는 소품(<see cref="Samkuk.Data.TerrainProp.standing"/>)은 배경이 아니라 월드 정렬(<see cref="WorldSorting"/>)에 들어가
    /// 캐릭터와 발 위치로 앞뒤가 정해지고, 회전하지 않으며, 드리운 그림자(<see cref="CastShadow"/>)가 붙는다 (Step 14-4).
    /// 서 있는 소품이 플레이어 앞에서 몸을 덮으면 반투명해진다(<see cref="PropFadeRule"/>, Step 14-6) — 맵 편집기에서는 하지 않는다.
    /// </summary>
    public class TerrainPropSpawner : MonoBehaviour
    {
        const float ViewMargin = 3.5f;   // 화면 가장자리 밖으로 더 만들어 두는 거리 (큰 소품이 걸쳐 보이지 않게)
        const float PropZ = 0.5f;        // 배경(z=1) 바로 앞

        TerrainMap map;
        Transform follow;
        Camera cam;
        Material material;
        int sortingLayerId;

        /// <summary>풀에서 돌려 쓰는 소품 하나: 렌더러 + (서 있는 소품으로 쓰였을 때 붙은) 그림자와 서 있는 그림(2.5D 틸트).</summary>
        class PropView
        {
            public SpriteRenderer sr;
            public CastShadow shadow;
            public StandingSprite stand;
            public bool standing;
        }

        readonly List<PropView> standingViews = new List<PropView>();   // 화면 주변의 서 있는 소품 (겹침 검사 대상)
        Transform fadeTarget;
        float nextFadeTargetSearch;

        readonly Dictionary<Vector2Int, List<PropView>> active = new Dictionary<Vector2Int, List<PropView>>();
        readonly Stack<PropView> pool = new Stack<PropView>();
        readonly List<Vector2Int> removeBuffer = new List<Vector2Int>();
        bool fxAllowed = true;

        /// <summary>소품에 붙은 점광원 하나 (Light2D, 또는 2.5D 틸트면 3D 점광원). 일렁임은 매 프레임 세기만 바꾼다.</summary>
        class PropLight
        {
            public Light2D light;
            public Light light3d;
            public float baseIntensity;
            public float flicker;
            public float phase;

            public GameObject GameObject => light != null ? light.gameObject : light3d.gameObject;
            public void SetIntensity(float value)
            {
                if (light != null) light.intensity = value;
                else light3d.intensity = value;
            }
        }

        readonly Dictionary<Vector2Int, List<PropLight>> activeLights = new Dictionary<Vector2Int, List<PropLight>>();
        readonly Stack<PropLight> lightPool = new Stack<PropLight>();      // Light2D
        readonly Stack<PropLight> lightPool3d = new Stack<PropLight>();    // 3D 점광원 (틸트)
        bool lightsEnabled;

        public TerrainMap Map => map;
        public int ActiveChunkCount => active.Count;
        public int ActivePropCount { get; private set; }
        public int PooledCount => pool.Count;
        /// <summary>지금 켜져 있는 소품 점광원 수.</summary>
        public int ActiveLightCount { get; private set; }
        /// <summary>소품 점광원을 만드는가 (초기화 때 <see cref="Hd2dSettings.Lighting"/> 을 읽는다).</summary>
        public bool LightsEnabled => lightsEnabled;

        /// <summary>겹침 검사 대상(플레이어 발 위치). 비워 두면 씬의 <see cref="PlayerController"/> 를 찾는다. 테스트에서 지정한다.</summary>
        public Transform FadeTarget { get => fadeTarget; set => fadeTarget = value; }
        /// <summary>지금 반투명한 서 있는 소품 수 (테스트/디버그).</summary>
        public int FadedCount { get; private set; }

        /// <summary>
        /// 맵과 따라갈 대상(보통 카메라)을 정하고 첫 칸들을 만든다. <paramref name="reference"/> 는 재질과 정렬 레이어를 빌려 올 배경 렌더러.
        /// <paramref name="allowLights"/> 가 false 면(맵 편집기) 설정과 상관없이 점광원과 그림자를 만들지 않는다.
        /// </summary>
        public void Initialize(TerrainMap terrainMap, Transform followTarget, SpriteRenderer reference, bool allowLights = true)
        {
            fxAllowed = allowLights;
            map = terrainMap;
            follow = followTarget;
            if (reference != null)
            {
                material = reference.sharedMaterial;   // 배경과 같은 재질 = 같은 조명을 받는다
                sortingLayerId = reference.sortingLayerID;
            }
            lightsEnabled = allowLights && Hd2dSettings.Lighting;
            Clear();
            Refresh();
        }

        /// <summary>소품 점광원을 켜거나 끈다 (디버그 키 F5). 화면 주변 칸을 다시 만들어 바로 반영한다.</summary>
        public void SetLightsEnabled(bool enabled)
        {
            if (lightsEnabled == enabled) return;
            lightsEnabled = enabled;
            Clear();
            Refresh();
        }

        /// <summary>따라갈 대상을 바꾼다 (카메라가 늦게 정해질 때).</summary>
        public void SetFollow(Transform followTarget) => follow = followTarget;

        /// <summary>바닥 소품의 재질을 바꾸고 화면 주변 칸을 다시 만든다 (2.5D 틸트를 켜고 끌 때 배경이 부른다).</summary>
        public void SetMaterial(Material groundMaterial)
        {
            material = groundMaterial;
            Clear();
            Refresh();
        }

        void LateUpdate()
        {
            Refresh();
            TickFlicker(Time.time);
            TickFade(Time.deltaTime);
        }

        /// <summary>플레이어 앞에서 몸을 덮는 서 있는 소품을 반투명하게, 아니면 되돌린다. 전투에서만(맵 편집기 제외).</summary>
        public void TickFade(float dt)
        {
            if (!fxAllowed || standingViews.Count == 0) return;
            if (fadeTarget == null)
            {
                // 플레이어는 늦게 생길 수 있다. 없을 때 매 프레임 뒤지지 않도록 0.5초마다 찾는다
                if (Time.unscaledTime < nextFadeTargetSearch) return;
                nextFadeTargetSearch = Time.unscaledTime + 0.5f;
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc == null) return;
                fadeTarget = pc.transform;
            }

            Vector2 feet = fadeTarget.position;
            int faded = 0;
            foreach (var view in standingViews)
            {
                var sr = view.sr;
                var b = sr.bounds;
                bool fade = PropFadeRule.ShouldFade(sr.transform.position.y, new Rect(b.min.x, b.min.y, b.size.x, b.size.y), feet);
                float a = PropFadeRule.Step(sr.color.a, fade, dt);
                if (a != sr.color.a)
                {
                    var c = sr.color;
                    c.a = a;
                    sr.color = c;
                }
                if (a < 1f) faded++;   // 틸트에서는 StandingSprite 가 알파 < 1 인 동안 반투명 재질로 스스로 바꾼다
            }
            FadedCount = faded;
        }

        /// <summary>횃불처럼 일렁이는 빛의 세기를 갱신한다. 펄린 노이즈라 깜빡이지 않고 부드럽게 흔들린다.</summary>
        public void TickFlicker(float time)
        {
            foreach (var list in activeLights.Values)
                foreach (var pl in list)
                {
                    if (pl.flicker <= 0f) continue;
                    float n = Mathf.PerlinNoise(time * 6f + pl.phase, pl.phase);   // 0~1
                    pl.SetIntensity(pl.baseIntensity * (1f - pl.flicker * n));
                }
        }

        /// <summary>화면 주변에 필요한 칸을 만들고 멀어진 칸을 치운다.</summary>
        public void Refresh()
        {
            if (map == null) return;

            if (cam == null && follow != null) cam = follow.GetComponent<Camera>();
            if (cam == null) cam = Camera.main;

            // 화면에 보이는 땅의 범위: 틸트면 바라보는 점 기준 사다리꼴을 감싼 사각형(북쪽이 더 멀다), 아니면 직교 화면 사각형
            Rect view;
            if (TiltView.Active)
            {
                Vector2 look = follow != null ? (Vector2)TiltView.LookPoint(follow) : Vector2.zero;
                var r = TiltView.GroundFootprint(cam != null ? cam.aspect : 16f / 9f);
                view = new Rect(look.x + r.xMin, look.y + r.yMin, r.width, r.height);
            }
            else
            {
                Vector2 center = follow != null ? (Vector2)follow.position : Vector2.zero;
                float halfW = 11f, halfH = 6.5f;
                if (cam != null && cam.orthographic)
                {
                    halfH = cam.orthographicSize;
                    halfW = halfH * cam.aspect;
                }
                view = new Rect(center.x - halfW, center.y - halfH, halfW * 2f, halfH * 2f);
            }

            float c = TerrainMap.ChunkSize;
            int x0 = Mathf.FloorToInt((view.xMin - ViewMargin) / c), x1 = Mathf.FloorToInt((view.xMax + ViewMargin) / c);
            int y0 = Mathf.FloorToInt((view.yMin - ViewMargin) / c), y1 = Mathf.FloorToInt((view.yMax + ViewMargin) / c);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var key = new Vector2Int(x, y);
                    if (!active.ContainsKey(key)) SpawnChunk(key);
                }

            // 필요 범위에서 한 칸 이상 벗어난 칸은 치운다 (경계에서 오락가락해도 깜빡이지 않게 여유를 둔다)
            removeBuffer.Clear();
            foreach (var key in active.Keys)
                if (key.x < x0 - 1 || key.x > x1 + 1 || key.y < y0 - 1 || key.y > y1 + 1) removeBuffer.Add(key);
            foreach (var key in removeBuffer) DespawnChunk(key);
        }

        /// <summary>한 칸의 소품을 다시 만든다 (맵 편집기에서 그 칸을 고쳤을 때). 화면 주변에 없는 칸이면 아무것도 하지 않는다.</summary>
        public void RebuildChunk(Vector2Int key)
        {
            if (map == null || !active.ContainsKey(key)) return;
            DespawnChunk(key);
            SpawnChunk(key);
        }

        /// <summary>모든 소품을 풀로 돌려보낸다.</summary>
        public void Clear()
        {
            removeBuffer.Clear();
            foreach (var key in active.Keys) removeBuffer.Add(key);
            foreach (var key in removeBuffer) DespawnChunk(key);
        }

        void SpawnChunk(Vector2Int key)
        {
            var list = new List<PropView>();
            var lights = lightsEnabled ? new List<PropLight>() : null;
            Place(list, lights, map.GetChunk(key.x, key.y));   // 직접 고친 칸이면 그 내용, 아니면 자동 생성
            active[key] = list;
            ActivePropCount += list.Count;
            if (lights != null && lights.Count > 0)
            {
                activeLights[key] = lights;
                ActiveLightCount += lights.Count;
            }
        }

        void Place(List<PropView> list, List<PropLight> lights, List<PropPlacement> placements)
        {
            foreach (var p in placements)
            {
                var view = Acquire();
                var sr = view.sr;
                bool standing = p.prop.standing;
                sr.sprite = p.prop.sprite;
                sr.color = p.tinted ? p.tint : Color.white;   // 풀에서 꺼낸 것은 이전 색이 남아 있으므로 항상 다시 정한다
                sr.flipX = p.flipX;
                sr.transform.position = new Vector3(p.position.x, p.position.y, TiltView.GroundZ(PropZ));
                sr.transform.localScale = new Vector3(p.scale, p.scale * (p.stretchY > 0f ? p.stretchY : 1f), 1f);
                if (standing)
                {
                    // 서 있는 소품: 캐릭터와 같은 월드 정렬(발 y), 눕히지 않는다 (맵 편집기에서 돌려 둔 값이 있어도 무시).
                    // 2.5D 틸트면 서 있는 그림(StandingSprite)이 되어 셰이더가 세운다
                    if (material != null) sr.sharedMaterial = material;   // 서 있는 그림이 틸트 재질로 바꾸기 전의 원래 재질
                    WorldSorting.Configure(sr, PixelArt.PropFootUnits);
                    sr.transform.localRotation = Quaternion.identity;
                    view.stand = sr.GetComponent<StandingSprite>();
                }
                else
                {
                    StandingSprite.Release(sr);           // 풀에서 꺼낸 것은 서 있는 소품이었을 수 있으므로 항상 다시 정한다
                    view.stand = null;
                    TiltView.ConfigureGround(sr, material);
                    sr.sortingLayerID = sortingLayerId;
                    sr.sortingOrder = p.order;
                    sr.spriteSortPoint = SpriteSortPoint.Center;
                    sr.transform.localRotation = Quaternion.Euler(0f, 0f, p.rotation);
                }
                SetShadow(view, standing && fxAllowed);
                view.standing = standing;
                if (standing) standingViews.Add(view);
                list.Add(view);

                if (lights != null && p.prop.HasLight) lights.Add(PlaceLight(p));
            }
        }

        /// <summary>서 있는 소품에만 드리운 그림자를 붙인다 (한 번 붙인 그림자는 풀과 함께 돌려 쓰고 끄기만 한다).</summary>
        static void SetShadow(PropView view, bool on)
        {
            if (on)
            {
                if (view.shadow == null) view.shadow = CastShadow.Attach(view.sr);
                else view.shadow.gameObject.SetActive(true);
            }
            else if (view.shadow != null) view.shadow.gameObject.SetActive(false);
        }

        PropLight PlaceLight(PropPlacement p)
        {
            var pl = AcquireLight();
            var prop = p.prop;
            pl.flicker = prop.lightFlicker;
            pl.phase = (p.position.x * 12.9898f + p.position.y * 78.233f) % 97f;   // 같은 자리의 빛은 늘 같은 위상(결정적), 이웃끼리는 다르게
            // 빛은 소품 스프라이트의 자식이 아니다: 소품의 회전/반전/세로 배율이 빛에 섞이지 않게 자리만 맞춘다
            if (pl.light != null)
            {
                var l = pl.light;
                l.color = prop.lightColor;
                l.pointLightOuterRadius = prop.lightRadius * p.scale;
                l.pointLightInnerRadius = 0f;
                pl.baseIntensity = prop.lightIntensity;
                l.intensity = pl.baseIntensity;
                l.transform.position = new Vector3(p.position.x, p.position.y + prop.lightHeight * p.scale, PropZ);
            }
            else
            {
                // 2.5D 틸트: 3D 점광원. 횃불 높이는 서 있는 그림을 따라 카메라 위쪽으로 올린다
                var l = pl.light3d;
                l.color = prop.lightColor;
                l.range = prop.lightRadius * p.scale;
                pl.baseIntensity = prop.lightIntensity * TiltPreset.PointLightScale;
                l.intensity = pl.baseIntensity;
                float h = Mathf.Max(prop.lightHeight * p.scale, TiltPreset.GlowHeight);
                l.transform.position = new Vector3(p.position.x, p.position.y, 0f) + TiltView.Lift(h);
            }
            return pl;
        }

        void DespawnChunk(Vector2Int key)
        {
            if (!active.TryGetValue(key, out var list)) return;
            foreach (var view in list)
            {
                view.sr.gameObject.SetActive(false);
                if (view.standing) standingViews.Remove(view);
                pool.Push(view);
            }
            ActivePropCount -= list.Count;
            active.Remove(key);

            if (activeLights.TryGetValue(key, out var lights))
            {
                foreach (var pl in lights)
                {
                    pl.GameObject.SetActive(false);
                    (pl.light != null ? lightPool : lightPool3d).Push(pl);
                }
                ActiveLightCount -= lights.Count;
                activeLights.Remove(key);
            }
        }

        PropLight AcquireLight()
        {
            // 틸트 여부에 맞는 풀에서 꺼낸다 (켜고 끄면 Clear 로 모두 풀에 돌아가 있으므로 다른 종류는 그냥 남겨 둔다)
            bool tilt = TiltView.Active;
            var pool = tilt ? lightPool3d : lightPool;
            if (pool.Count > 0)
            {
                var pl = pool.Pop();
                pl.GameObject.SetActive(true);
                return pl;
            }
            var go = new GameObject("PropLight");
            go.transform.SetParent(transform, false);
            if (tilt)
            {
                var light3d = go.AddComponent<Light>();
                light3d.type = LightType.Point;
                light3d.shadows = LightShadows.None;
                return new PropLight { light3d = light3d };
            }
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.falloffIntensity = 0.6f;
            light.shadowsEnabled = false;
            return new PropLight { light = light };
        }

        PropView Acquire()
        {
            if (pool.Count > 0)
            {
                var view = pool.Pop();
                view.sr.gameObject.SetActive(true);
                return view;
            }
            var go = new GameObject("Prop");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (material != null) sr.sharedMaterial = material;
            sr.sortingLayerID = sortingLayerId;
            return new PropView { sr = sr };
        }
    }
}
