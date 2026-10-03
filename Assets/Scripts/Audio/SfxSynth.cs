using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.Audio
{
    /// <summary>
    /// 오디오 에셋 없이 효과음을 코드로 합성한다. 각 효과음은 음 높이가 변하는 파형(톤)과
    /// 필터를 거친 잡음을 겹친 간단한 레시피로 정의되며, 결과는 항상 같다(잡음도 고정 시드).
    /// 실제 음원으로 바꾸려면 AudioManager의 오버라이드 슬롯에 클립을 넣으면 된다.
    /// </summary>
    public static class SfxSynth
    {
        public const int SampleRate = 22050;

        enum Wave { Sine, Square, Triangle, Saw, Noise }

        struct Tone
        {
            public Wave wave;
            public float f0, f1;   // 시작/끝 주파수 (Hz, 잡음에는 무시)
            public float duration;
            public float volume;
            public float start;    // 효과음 안에서의 시작 시각 (초)
            public float lowpass;  // 0이면 필터 없음, 0~1 값이 작을수록 둔탁한 소리
        }

        static Tone T(Wave wave, float f0, float f1, float duration, float volume, float start = 0f, float lowpass = 0f) =>
            new Tone { wave = wave, f0 = f0, f1 = f1, duration = duration, volume = volume, start = start, lowpass = lowpass };

        // 음계 (Hz)
        const float C5 = 523.25f, E5 = 659.25f, G5 = 783.99f, C6 = 1046.5f, E6 = 1318.5f, G6 = 1568f;

        static readonly Dictionary<SfxId, Tone[]> Recipes = new Dictionary<SfxId, Tone[]>
        {
            { SfxId.Hit, new[] { T(Wave.Noise, 0, 0, 0.06f, 0.5f, 0f, 0.5f), T(Wave.Square, 160f, 80f, 0.06f, 0.3f) } },
            { SfxId.Kill, new[] { T(Wave.Noise, 0, 0, 0.14f, 0.5f, 0f, 0.25f), T(Wave.Triangle, 320f, 90f, 0.14f, 0.5f) } },
            { SfxId.PlayerHurt, new[] { T(Wave.Square, 240f, 90f, 0.22f, 0.5f), T(Wave.Noise, 0, 0, 0.12f, 0.3f, 0f, 0.3f) } },
            { SfxId.Gem, new[] { T(Wave.Sine, 900f, 1400f, 0.07f, 0.5f) } },
            { SfxId.LevelUp, Arpeggio(Wave.Triangle, 0.09f, 0.18f, 0.5f, C5, E5, G5, C6) },
            { SfxId.Evolve, Concat(
                new[] { T(Wave.Noise, 0, 0, 0.45f, 0.25f, 0f, 0.15f) },
                Arpeggio(Wave.Triangle, 0.1f, 0.25f, 0.5f, C5, E5, G5, C6, E6),
                new[] { T(Wave.Triangle, C6, C6, 0.5f, 0.3f, 0.5f), T(Wave.Triangle, E6, E6, 0.5f, 0.3f, 0.5f), T(Wave.Triangle, G6, G6, 0.5f, 0.3f, 0.5f) }) },
            { SfxId.Skill, new[] { T(Wave.Saw, 180f, 700f, 0.3f, 0.35f, 0f, 0.5f), T(Wave.Noise, 0, 0, 0.3f, 0.3f, 0f, 0.4f) } },
            { SfxId.Slash, new[] { T(Wave.Noise, 0, 0, 0.12f, 0.55f, 0f, 0.6f) } },
            { SfxId.Thrust, new[] { T(Wave.Noise, 0, 0, 0.08f, 0.4f, 0f, 0.5f), T(Wave.Square, 500f, 900f, 0.08f, 0.25f) } },
            { SfxId.Arrow, new[] { T(Wave.Triangle, 1000f, 450f, 0.09f, 0.45f), T(Wave.Noise, 0, 0, 0.03f, 0.2f, 0f, 0.5f) } },
            { SfxId.Thunder, new[] {
                T(Wave.Noise, 0, 0, 0.4f, 0.8f, 0f, 0.12f), T(Wave.Sine, 70f, 40f, 0.4f, 0.6f), T(Wave.Noise, 0, 0, 0.06f, 0.5f, 0f, 0.7f) } },
            { SfxId.Explosion, new[] { T(Wave.Noise, 0, 0, 0.35f, 0.8f, 0f, 0.1f), T(Wave.Sine, 100f, 40f, 0.35f, 0.6f) } },
            { SfxId.Fire, new[] { T(Wave.Noise, 0, 0, 0.3f, 0.45f, 0f, 0.2f) } },
            { SfxId.EnemyShot, new[] { T(Wave.Square, 520f, 300f, 0.1f, 0.3f) } },
            { SfxId.Boss, new[] { T(Wave.Saw, 110f, 90f, 0.7f, 0.5f, 0f, 0.15f), T(Wave.Square, 55f, 55f, 0.7f, 0.35f) } },
            { SfxId.Click, new[] { T(Wave.Square, 700f, 700f, 0.03f, 0.3f) } },
            { SfxId.Buy, new[] { T(Wave.Sine, 660f, 660f, 0.08f, 0.5f), T(Wave.Sine, 990f, 990f, 0.14f, 0.5f, 0.08f) } },
            { SfxId.Victory, Concat(
                Arpeggio(Wave.Triangle, 0.12f, 0.3f, 0.5f, C5, E5, G5, C6, E6),
                new[] { T(Wave.Triangle, C6, C6, 0.6f, 0.3f, 0.6f), T(Wave.Triangle, E6, E6, 0.6f, 0.3f, 0.6f), T(Wave.Triangle, G6, G6, 0.6f, 0.3f, 0.6f) }) },
            { SfxId.Defeat, new[] { T(Wave.Square, 330f, 110f, 0.6f, 0.4f, 0f, 0.3f), T(Wave.Triangle, 165f, 55f, 0.6f, 0.4f) } },
        };

        /// <summary>같은 파형의 음들을 step 간격으로 이어 붙인 아르페지오.</summary>
        static Tone[] Arpeggio(Wave wave, float step, float noteDuration, float volume, params float[] notes)
        {
            var tones = new Tone[notes.Length];
            for (int i = 0; i < notes.Length; i++)
                tones[i] = T(wave, notes[i], notes[i], noteDuration, volume, i * step);
            return tones;
        }

        static Tone[] Concat(params Tone[][] parts)
        {
            var list = new List<Tone>();
            foreach (var p in parts) list.AddRange(p);
            return list.ToArray();
        }

        /// <summary>레시피가 있는 효과음인가 (None 제외).</summary>
        public static bool HasRecipe(SfxId id) => Recipes.ContainsKey(id);

        /// <summary>효과음 샘플(-1~1)을 합성한다. 레시피가 없으면 빈 배열.</summary>
        public static float[] Generate(SfxId id)
        {
            if (!Recipes.TryGetValue(id, out var tones)) return new float[0];

            float total = 0f;
            foreach (var t in tones) total = Mathf.Max(total, t.start + t.duration);
            var buffer = new float[Mathf.CeilToInt(total * SampleRate) + 1];
            uint seed = 12345u + (uint)id * 7919u;

            foreach (var t in tones)
            {
                int offset = Mathf.RoundToInt(t.start * SampleRate);
                int length = Mathf.Max(1, Mathf.RoundToInt(t.duration * SampleRate));
                int attackSamples = Mathf.Max(1, Mathf.RoundToInt(0.003f * SampleRate)); // 클릭 잡음 방지
                float phase = 0f, filtered = 0f;

                for (int i = 0; i < length && offset + i < buffer.Length; i++)
                {
                    float p = i / (float)length;
                    phase += Mathf.Lerp(t.f0, t.f1, p) / SampleRate;
                    phase -= Mathf.Floor(phase);

                    float v;
                    switch (t.wave)
                    {
                        case Wave.Sine: v = Mathf.Sin(phase * Mathf.PI * 2f); break;
                        case Wave.Square: v = (phase < 0.5f ? 1f : -1f) * 0.6f; break;
                        case Wave.Triangle: v = 4f * Mathf.Abs(phase - 0.5f) - 1f; break;
                        case Wave.Saw: v = (2f * phase - 1f) * 0.7f; break;
                        default: v = NextNoise(ref seed); break;
                    }

                    if (t.lowpass > 0f)
                    {
                        filtered += t.lowpass * (v - filtered);
                        v = filtered;
                    }

                    float attack = Mathf.Min(1f, i / (float)attackSamples);
                    float decay = (1f - p) * (1f - p); // 끝에서 0으로 수렴
                    buffer[offset + i] += v * attack * decay * t.volume;
                }
            }

            for (int i = 0; i < buffer.Length; i++) buffer[i] = Mathf.Clamp(buffer[i], -1f, 1f);
            return buffer;
        }

        /// <summary>합성한 샘플로 AudioClip을 만든다. 레시피가 없으면 null.</summary>
        public static AudioClip CreateClip(SfxId id)
        {
            var samples = Generate(id);
            if (samples.Length == 0) return null;

            var clip = AudioClip.Create($"sfx_{id}", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>고정 시드 잡음(xorshift) -1~1. 합성 결과가 항상 같도록 UnityEngine.Random을 쓰지 않는다.</summary>
        static float NextNoise(ref uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed / (float)uint.MaxValue * 2f - 1f;
        }
    }
}
