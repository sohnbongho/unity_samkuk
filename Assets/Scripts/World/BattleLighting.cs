using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Samkuk.World
{
    /// <summary>
    /// 전투 맵의 조명(Step 14-1): 씬의 전역 Light2D 에 지형·시간대 색조(<see cref="LightingPreset"/>)를 입히고,
    /// 플레이어 주변에 약한 빛을 붙인다. 지형 맵과 함께 만들어지고(<c>InfiniteBackground.BuildMap</c>) 함께 사라지며,
    /// 사라질 때 전역광을 원래 값으로 되돌린다. 소품의 점광원은 <see cref="TerrainPropSpawner"/> 가 소품과 함께 만든다.
    /// <see cref="Hd2dSettings.Lighting"/> 이 꺼져 있으면 아무것도 바꾸지 않는다.
    /// </summary>
    public class BattleLighting : MonoBehaviour
    {
        const string GlowName = "PlayerGlow";

        Light2D global;
        Color originalColor;
        float originalIntensity;
        bool applied;
        Light2D glow;
        Transform player;
        float nextPlayerSearch;

        public CastleTerrain Terrain { get; private set; }
        public TimeOfDay TimeOfDay { get; private set; }
        /// <summary>지금 전역광에 입혀 둔 값 (꺼져 있으면 마지막으로 입혔던 값).</summary>
        public GlobalLightSettings Applied { get; private set; }
        public bool IsApplied => applied;
        public Light2D GlobalLight => global;
        public Light2D PlayerGlow => glow;

        /// <summary>전역광을 정해 적용한다. <paramref name="globalLight"/> 가 null 이면 씬에서 전역 Light2D 를 찾는다.</summary>
        public void Initialize(CastleTerrain terrain, TimeOfDay time, Light2D globalLight = null)
        {
            Terrain = terrain;
            TimeOfDay = time;
            global = globalLight != null ? globalLight : FindGlobalLight();
            if (global != null)
            {
                originalColor = global.color;
                originalIntensity = global.intensity;
            }
            Apply(Hd2dSettings.Lighting);
        }

        /// <summary>켜면 색조와 플레이어 빛을 입히고, 끄면 원래대로 되돌린다 (디버그 키 F5).</summary>
        public void Apply(bool enabled)
        {
            if (!enabled) { Restore(); return; }

            Applied = LightingPreset.Global(Terrain, TimeOfDay);
            if (global != null)
            {
                global.color = Applied.color;
                global.intensity = Applied.intensity;
            }
            EnsureGlow();
            applied = true;
        }

        void Restore()
        {
            if (applied && global != null)
            {
                global.color = originalColor;
                global.intensity = originalIntensity;
            }
            applied = false;
            if (glow != null) { Destroy(glow.gameObject); glow = null; }
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
            if (glow == null) return;
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
            glow.transform.position = new Vector3(p.x, p.y, glow.transform.position.z);
        }

        void OnDestroy() => Restore();

        /// <summary>씬의 전역 Light2D (없으면 null).</summary>
        public static Light2D FindGlobalLight()
        {
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (l.lightType == Light2D.LightType.Global) return l;
            return null;
        }
    }
}
