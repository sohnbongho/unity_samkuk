using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.Stages;
using Samkuk.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Samkuk.Core
{
    /// <summary>개발용 오버레이: FPS, 활성 적 수, F1 적 +100, F2 전체 무기 레벨업, F3 경험치 +10, F4 시간 +10초, F5 HD-2D 조명 켜고 끄기, F6 후처리 켜고 끄기, F7 도트 격자 맞춤 켜고 끄기.</summary>
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField] EnemySpawner spawner;
        [SerializeField] EnemyManager manager;
        [SerializeField] int burstCount = 100;

        float smoothedDelta = 1f / 60f;
        GUIStyle style;
        WeaponController weapons;
        PlayerExperience experience;
        StageController stage;
        InfiniteBackground background;
        Samkuk.World.BattlePostFx postFx;
        Samkuk.World.BattlePixelCamera pixelCam;

        public EnemySpawner Spawner { get => spawner; set => spawner = value; }
        public EnemyManager Manager { get => manager; set => manager = value; }

        void Awake()
        {
            // 개발용: 에디터와 Development Build 에서만 켠다. 친구에게 나눠 주는 릴리스 빌드에서는 FPS 표시와 F1~F4 치트를 끈다.
            if (!Application.isEditor && !Debug.isDebugBuild) enabled = false;
        }

        void Update()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.05f);

            var kb = Keyboard.current;
            if (kb == null) return;

            if (spawner != null && kb.f1Key.wasPressedThisFrame)
                spawner.SpawnBurst(burstCount);

            if (kb.f2Key.wasPressedThisFrame)
            {
                if (weapons == null) weapons = FindAnyObjectByType<WeaponController>();
                if (weapons != null) weapons.LevelUpAll();
            }

            if (kb.f3Key.wasPressedThisFrame)
            {
                if (experience == null) experience = FindAnyObjectByType<PlayerExperience>();
                if (experience != null) experience.AddExp(10);
            }

            if (kb.f4Key.wasPressedThisFrame)
            {
                if (stage == null) stage = FindAnyObjectByType<StageController>();
                if (stage != null) stage.Tick(10f);
            }

            // HD-2D 조명 전후 비교: 저장 설정은 건드리지 않고 이 판에서만 뒤집는다
            if (kb.f5Key.wasPressedThisFrame)
            {
                Hd2dSettings.LightingOverride = !Hd2dSettings.Lighting;
                if (background == null) background = FindAnyObjectByType<InfiniteBackground>();
                if (background != null) background.SetLightingEnabled(Hd2dSettings.Lighting);
            }

            if (kb.f6Key.wasPressedThisFrame)
            {
                Hd2dSettings.PostFxOverride = !Hd2dSettings.PostFx;
                if (postFx == null) postFx = FindAnyObjectByType<Samkuk.World.BattlePostFx>();
                if (postFx != null) postFx.Apply(Hd2dSettings.PostFx);
            }

            if (kb.f7Key.wasPressedThisFrame)
            {
                Hd2dSettings.PixelPerfectOverride = !Hd2dSettings.PixelPerfect;
                if (pixelCam == null) pixelCam = FindAnyObjectByType<Samkuk.World.BattlePixelCamera>();
                if (pixelCam != null) pixelCam.Apply(Hd2dSettings.PixelPerfect);
            }
        }

        void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                style.normal.textColor = Color.white;
            }

            int count = manager != null ? manager.Count : 0;
            float fps = smoothedDelta > 0f ? 1f / smoothedDelta : 0f;
            GUI.Label(new Rect(10, 8, 500, 26), $"FPS {fps:0}   Enemies {count}", style);
            GUI.Label(new Rect(10, 30, 1000, 26), $"F1: +{burstCount} enemies   F2: weapons level up   F3: +10 exp   F4: +10 sec   F5: lighting {(Hd2dSettings.Lighting ? "on" : "off")}   F6: post fx {(Hd2dSettings.PostFx ? "on" : "off")}   F7: pixel {(Hd2dSettings.PixelPerfect ? "on" : "off")}", style);
        }
    }
}
