using Samkuk.Meta;
using UnityEngine;

namespace Samkuk.Feedback
{
    /// <summary>
    /// 화면 흔들림. "충격량(trauma)"이 쌓였다가 서서히 줄어들고, 흔들림 폭은 충격량의 제곱에 비례한다
    /// (약한 충격은 거의 안 느껴지고 강한 충격은 확 흔들린다). 카메라 위치는 직접 건드리지 않고
    /// Offset 만 계산하며, CameraFollow 가 추적 위치에 더해서 적용한다.
    /// 설정(SaveData.screenShake)으로 끌 수 있다.
    /// </summary>
    public class ScreenShake : MonoBehaviour
    {
        [SerializeField, Tooltip("충격량 1일 때 최대 흔들림 거리 (월드 단위)")] float maxOffset = 0.5f;
        [SerializeField, Tooltip("초당 줄어드는 충격량")] float decayPerSecond = 1.6f;
        [SerializeField, Tooltip("흔들리는 빠르기")] float frequency = 25f;

        float trauma;
        float clock;
        float seed;

        public Vector2 Offset { get; private set; }
        public float Trauma => trauma;
        public bool Enabled { get; private set; } = true;
        public float MaxOffset => maxOffset;

        void Awake()
        {
            Enabled = SaveSystem.Current.screenShake;
            seed = Random.value * 100f;
        }

        /// <summary>흔들림을 켜거나 끄고 저장한다. 끄면 즉시 멈춘다.</summary>
        public void SetEnabled(bool on)
        {
            Enabled = on;
            if (!on)
            {
                trauma = 0f;
                Offset = Vector2.zero;
            }

            var save = SaveSystem.Current;
            save.screenShake = on;
            SaveSystem.SaveCurrent();
        }

        /// <summary>충격량을 더한다 (0~1로 제한). 꺼져 있으면 무시.</summary>
        public void AddTrauma(float amount)
        {
            if (!Enabled || amount <= 0f) return;
            trauma = Mathf.Clamp01(trauma + amount);
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>시간을 dt만큼 진행시키고 Offset을 갱신한다.</summary>
        public void Tick(float dt)
        {
            clock += dt;
            trauma = Mathf.Max(0f, trauma - decayPerSecond * dt);

            if (trauma <= 0f)
            {
                Offset = Vector2.zero;
                return;
            }

            float magnitude = trauma * trauma * maxOffset;
            float t = clock * frequency;
            Offset = new Vector2(
                (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f * magnitude,
                (Mathf.PerlinNoise(t, seed + 31.7f) - 0.5f) * 2f * magnitude);
        }
    }
}
