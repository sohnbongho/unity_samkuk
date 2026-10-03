using Samkuk.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Samkuk.Core
{
    /// <summary>개발용 오버레이: FPS, 활성 적 수, F1로 적 100마리 추가 스폰.</summary>
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField] EnemySpawner spawner;
        [SerializeField] EnemyManager manager;
        [SerializeField] int burstCount = 100;

        float smoothedDelta = 1f / 60f;
        GUIStyle style;

        public EnemySpawner Spawner { get => spawner; set => spawner = value; }
        public EnemyManager Manager { get => manager; set => manager = value; }

        void Update()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.05f);

            var kb = Keyboard.current;
            if (kb != null && spawner != null && kb.f1Key.wasPressedThisFrame)
                spawner.SpawnBurst(burstCount);
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
            GUI.Label(new Rect(10, 8, 400, 26), $"FPS {fps:0}   Enemies {count}", style);
            GUI.Label(new Rect(10, 30, 400, 26), $"F1: +{burstCount} enemies", style);
        }
    }
}
