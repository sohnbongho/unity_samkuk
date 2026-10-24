using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Samkuk.World
{
    /// <summary>
    /// 전투 맵의 조명(Step 14-1): 씬의 전역 Light2D 에 지형·시간대 색조(<see cref="LightingPreset"/>)를 입히고,
    /// 플레이어 주변에 약한 빛을 붙인다. 지형 맵과 함께 만들어지고(<c>InfiniteBackground.BuildMap</c>) 함께 사라지며,
    /// 사라질 때 전역광을 원래 값으로 되돌린다. 소품의 점광원은 <see cref="TerrainPropSpawner"/> 가 소품과 함께 만든다.
    /// <see cref="Hd2dSettings.Lighting"/> 이 꺼져 있으면 아무것도 바꾸지 않는다.
    /// 2.5D 틸트(<see cref="TiltView.Active"/>, Step 16-1)면 Light2D 대신 3D 빛을 쓴다: 같은 색조를 햇빛(방향광, 진짜 그림자)과 환경광으로 나누고
    /// (<see cref="TiltPreset.SunShare"/>/<see cref="TiltPreset.AmbientShare"/>), 해의 방향은 드리운 그림자(<see cref="ShadowPreset"/>)와 같은 자리에서 온다.
    /// </summary>
    public class BattleLighting : MonoBehaviour
    {
        const string GlowName = "PlayerGlow";
        const string SunName = "Sun";

        Light2D global;
        Color originalColor;
        float originalIntensity;
        bool applied, applied2D, appliedTilt;
        Light2D glow;
        Light sun, glow3d;
        bool ambientSaved;
        AmbientMode originalAmbientMode;
        Color originalAmbient;
        bool initialized;
        Transform player;
        float nextPlayerSearch;

        public CastleTerrain Terrain { get; private set; }
        public TimeOfDay TimeOfDay { get; private set; }
        /// <summary>지금 전역광에 입혀 둔 값 (꺼져 있으면 마지막으로 입혔던 값).</summary>
        public GlobalLightSettings Applied { get; private set; }
        public bool IsApplied => applied;
        public Light2D GlobalLight => global;
        public Light2D PlayerGlow => glow;
        /// <summary>2.5D 틸트의 햇빛 (틸트가 아니면 null).</summary>
        public Light Sun => sun;
        /// <summary>2.5D 틸트의 플레이어 빛 (틸트가 아니면 null).</summary>
        public Light PlayerGlow3D => glow3d;
        public bool IsTilt => appliedTilt;

        /// <summary>전역광을 정해 적용한다. <paramref name="globalLight"/> 가 null 이면 씬에서 전역 Light2D 를 찾는다.</summary>
        public void Initialize(CastleTerrain terrain, TimeOfDay time, Light2D globalLight = null)
        {
            Terrain = terrain;
            TimeOfDay = time;
            initialized = true;
            global = globalLight != null ? globalLight : FindGlobalLight();
            if (global != null)
            {
                originalColor = global.color;
                originalIntensity = global.intensity;
            }
            CastShadow.SetSettings(ShadowPreset.For(time));   // 그림자 방향·길이·농도도 같은 시간대를 따른다 (조명 스위치와 무관)
            Apply(Hd2dSettings.Lighting);
        }

        void OnEnable() => TiltView.Changed += OnTiltChanged;
        void OnDisable() => TiltView.Changed -= OnTiltChanged;

        void OnTiltChanged()
        {
            if (initialized) Apply(Hd2dSettings.Lighting);
        }

        /// <summary>색조 없는 중립 빛 (조명을 껐을 때의 틸트: 흰색 햇빛·환경광만, 2D 의 "전역광 그대로"와 같은 밝기).</summary>
        public static readonly GlobalLightSettings Neutral = new GlobalLightSettings { color = Color.white, intensity = 1f };

        /// <summary>
        /// 켜면 색조와 플레이어 빛을 입히고, 끄면 원래대로 되돌린다 (디버그 키 F5).
        /// 2.5D 틸트에서는 꺼도 빛이 없으면 모든 그림이 검게 나오므로 중립 햇빛·환경광(<see cref="Neutral"/>)은 늘 둔다 — 색조와 플레이어 빛만 빠진다.
        /// </summary>
        public void Apply(bool enabled)
        {
            if (!enabled && !TiltView.Active) { Restore(); return; }

            Applied = enabled ? LightingPreset.Global(Terrain, TimeOfDay) : Neutral;
            if (TiltView.Active)
            {
                Restore2D();
                ApplyTilt(enabled);
            }
            else
            {
                RestoreTilt();
                Apply2D();
            }
            applied = enabled;
        }

        void Apply2D()
        {
            if (global != null)
            {
                global.color = Applied.color;
                global.intensity = Applied.intensity;
            }
            EnsureGlow();
            applied2D = true;
        }

        void ApplyTilt(bool withGlow)
        {
            var c = Applied.color;
            float i = Applied.intensity;
            if (!ambientSaved)
            {
                ambientSaved = true;
                originalAmbientMode = RenderSettings.ambientMode;
                originalAmbient = RenderSettings.ambientLight;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = c * (i * TiltPreset.AmbientShare);

            if (sun == null)
            {
                var go = new GameObject(SunName);
                go.transform.SetParent(transform, false);
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadowStrength = 1f;
            }
            sun.color = c;
            sun.intensity = i * TiltPreset.SunShare;
            SyncSunShadows();
            sun.transform.rotation = Quaternion.LookRotation(TiltPreset.SunDirection(ShadowPreset.For(TimeOfDay)));

            if (!withGlow)
            {
                if (glow3d != null) { Destroy(glow3d.gameObject); glow3d = null; }
            }
            else if (glow3d == null)
            {
                var go = new GameObject(GlowName);
                go.transform.SetParent(transform, false);
                glow3d = go.AddComponent<Light>();
                glow3d.type = LightType.Point;
                glow3d.shadows = LightShadows.None;
                glow3d.color = LightingPreset.PlayerGlowColor;
                glow3d.range = LightingPreset.PlayerGlowRadius;
                glow3d.intensity = LightingPreset.PlayerGlowIntensity * TiltPreset.PointLightScale;
            }
            appliedTilt = true;
        }

        /// <summary>햇빛 그림자는 그림자 설정(F8)을 따른다. 도트 그림에는 딱 떨어지는 그림자가 어울려 Hard.</summary>
        void SyncSunShadows()
        {
            if (sun == null) return;
            var want = Hd2dSettings.Shadows ? LightShadows.Hard : LightShadows.None;
            if (sun.shadows != want) sun.shadows = want;
        }

        void Restore()
        {
            Restore2D();
            RestoreTilt();
            applied = false;
        }

        void Restore2D()
        {
            if (applied2D && global != null)
            {
                global.color = originalColor;
                global.intensity = originalIntensity;
            }
            applied2D = false;
            if (glow != null) { Destroy(glow.gameObject); glow = null; }
        }

        void RestoreTilt()
        {
            if (!appliedTilt) return;
            appliedTilt = false;
            if (ambientSaved)
            {
                RenderSettings.ambientMode = originalAmbientMode;
                RenderSettings.ambientLight = originalAmbient;
                ambientSaved = false;
            }
            if (sun != null) { Destroy(sun.gameObject); sun = null; }
            if (glow3d != null) { Destroy(glow3d.gameObject); glow3d = null; }
        }

        void EnsureGlow()
        {
            if (glow != null) return;
            var go = new GameObject(GlowName);
            go.transform.SetParent(transform, false);
            glow = go.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = LightingPreset.PlayerGlowColor;
            glow.intensity = LightingPreset.PlayerGlowIntensity;
            glow.pointLightOuterRadius = LightingPreset.PlayerGlowRadius;
            glow.pointLightInnerRadius = 0f;
            glow.falloffIntensity = 0.7f;
            glow.shadowsEnabled = false;
        }

        void LateUpdate()
        {
            SyncSunShadows();
            if (glow == null && glow3d == null) return;
            if (player == null)
            {
                // 플레이어는 씬에 하나이고 늦게 생길 수 있다. 없을 때 매 프레임 뒤지지 않도록 0.5초마다 찾는다
                if (UnityEngine.Time.unscaledTime < nextPlayerSearch) return;
                nextPlayerSearch = UnityEngine.Time.unscaledTime + 0.5f;
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc == null) return;
                player = pc.transform;
            }
            var p = player.position;
            if (glow != null) glow.transform.position = new Vector3(p.x, p.y, glow.transform.position.z);
            if (glow3d != null) glow3d.transform.position = new Vector3(p.x, p.y, -TiltPreset.GlowHeight);
        }

        void OnDestroy() => Restore();

        /// <summary>씬의 전역 Light2D (없으면 null).</summary>
        public static Light2D FindGlobalLight()
        {
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Exclude))
                if (l.lightType == Light2D.LightType.Global) return l;
            return null;
        }
    }
}
