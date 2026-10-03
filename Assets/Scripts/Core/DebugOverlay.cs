using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.Stages;
using Samkuk.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Samkuk.Core
{
    /// <summary>개발용 오버레이: FPS, 활성 적 수, F1 적 +100, F2 전체 무기 레벨업, F3 경험치 +10, F4 시간 +10초.</summary>
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

        public EnemySpawner Spawner { get => spawner; set => spawner = value; }
        public EnemyManager Manager { get => manager; set => manager = value; }

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
            GUI.Label(new Rect(10, 30, 800, 26), $"F1: +{burstCount} enemies   F2: weapons level up   F3: +10 exp   F4: +10 sec", style);
        }
    }
}
