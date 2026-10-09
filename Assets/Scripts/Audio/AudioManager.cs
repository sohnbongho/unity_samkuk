using System;
using System.Collections.Generic;
using Samkuk.Data;
using Samkuk.Meta;
using UnityEngine;

namespace Samkuk.Audio
{
    /// <summary>효과음 하나를 실제 음원 파일로 교체하기 위한 슬롯.</summary>
    [Serializable]
    public class SfxOverride
    {
        public SfxId id;
        public AudioClip clip;
    }

    /// <summary>
    /// 효과음 재생기. 처음 필요할 때 스스로 만들어지고(씬 이동에도 유지) 효과음은 SfxSynth로 합성한다.
    /// 같은 소리가 한꺼번에 몰리지 않도록 소리별 최소 간격을 두고, 정해진 개수의 AudioSource를 돌려 쓴다.
    /// 볼륨은 저장 데이터(SaveData.sfxVolume)에 보관된다.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const int SourceCount = 8;
        const float DefaultMinInterval = 0.03f;

        /// <summary>효과음 볼륨 단계 (끔 / 작게 / 보통 / 크게).</summary>
        public static readonly float[] VolumeSteps = { 0f, 0.35f, 0.7f, 1f };
        static readonly string[] VolumeNames = { "끔", "작게", "보통", "크게" };

        [SerializeField, Tooltip("합성음 대신 쓸 음원 (비워 두면 합성음)")] List<SfxOverride> overrides = new List<SfxOverride>();

        static AudioManager instance;
        static bool quitting;

        readonly Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
        readonly Dictionary<SfxId, float> lastPlayed = new Dictionary<SfxId, float>();
        readonly Dictionary<SfxId, int> playCounts = new Dictionary<SfxId, int>();
        AudioSource[] sources;
        int nextSource;
        float sfxVolume = 0.7f;

        public float SfxVolume => sfxVolume;

        /// <summary>지금까지 실제로 재생된 횟수 (간격 제한/무음으로 건너뛴 것은 제외).</summary>
        public int GetPlayCount(SfxId id) => playCounts.TryGetValue(id, out int n) ? n : 0;

        /// <summary>인스턴스를 돌려준다. 없으면 만든다 (플레이 중이 아니거나 종료 중이면 null).</summary>
        public static AudioManager Instance
        {
            get
            {
                if (instance != null) return instance;
                if (!Application.isPlaying || quitting) return null;

                var go = new GameObject("AudioManager");
                DontDestroyOnLoad(go);
                return go.AddComponent<AudioManager>(); // Awake에서 instance가 설정된다
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // 도메인 리로드를 끈 에디터에서도 이전 플레이 상태가 남지 않도록
            instance = null;
            quitting = false;
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            Application.quitting += OnQuitting;

            sources = new AudioSource[SourceCount];
            for (int i = 0; i < SourceCount; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f; // 2D
                sources[i] = s;
            }

            sfxVolume = SaveSystem.Current.sfxVolume;
        }

        void OnDestroy()
        {
            Application.quitting -= OnQuitting;
            if (instance == this) instance = null;
            foreach (var c in clips.Values)
                if (c != null) Destroy(c);
        }

        static void OnQuitting() => quitting = true;

        /// <summary>테스트용: 만들어진 인스턴스를 즉시 제거한다.</summary>
        public static void DestroyInstance()
        {
            if (instance == null) return;
            DestroyImmediate(instance.gameObject);
            instance = null;
        }

        // ───────────────────────── 재생 ─────────────────────────

        /// <summary>효과음을 재생한다 (매니저가 없으면 만들고, 플레이 중이 아니면 아무것도 하지 않는다).</summary>
        public static void Play(SfxId id, float volumeScale = 1f) => Instance?.TryPlay(id, volumeScale);

        /// <summary>무기 공격 효과음을 재생한다.</summary>
        public static void PlayWeapon(WeaponType type) => Play(SfxMap.ForWeapon(type));

        /// <summary>재생했으면 true. 음소거 / 소리 없음 / 최소 간격 미만이면 false.</summary>
        public bool TryPlay(SfxId id, float volumeScale = 1f)
        {
            if (id == SfxId.None || sfxVolume <= 0f) return false;

            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out float last) && now - last < MinInterval(id)) return false;

            var clip = GetClip(id);
            if (clip == null) return false;

            lastPlayed[id] = now;
            playCounts[id] = GetPlayCount(id) + 1;

            // 오래된 소리부터 덮어쓰는 라운드 로빈
            var source = sources[nextSource];
            nextSource = (nextSource + 1) % sources.Length;
            source.pitch = 1f + UnityEngine.Random.Range(-PitchVariance(id), PitchVariance(id));
            source.PlayOneShot(clip, Mathf.Clamp01(volumeScale) * sfxVolume);
            return true;
        }

        /// <summary>해당 효과음의 클립: 오버라이드가 있으면 그것, 없으면 합성음(처음 한 번만 만든다).</summary>
        public AudioClip GetClip(SfxId id)
        {
            foreach (var o in overrides)
                if (o != null && o.id == id && o.clip != null) return o.clip;

            if (clips.TryGetValue(id, out var cached) && cached != null) return cached;

            var clip = SfxSynth.CreateClip(id);
            if (clip != null) clips[id] = clip;
            return clip;
        }

        // ───────────────────────── 볼륨 ─────────────────────────

        /// <summary>효과음 볼륨(0~1)을 바꾸고 저장한다.</summary>
        public void SetSfxVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
            var save = SaveSystem.Current;
            save.sfxVolume = sfxVolume;
            SaveSystem.SaveCurrent();
        }

        /// <summary>현재 볼륨에서 가장 가까운 단계의 다음 단계 값 (끝에서 처음으로 돌아간다).</summary>
        public static float NextVolumeStep(float current)
        {
            int nearest = NearestStep(current);
            return VolumeSteps[(nearest + 1) % VolumeSteps.Length];
        }

        /// <summary>볼륨의 표시 이름 (끔 / 작게 / 보통 / 크게).</summary>
        public static string VolumeName(float volume) => VolumeNames[NearestStep(volume)];

        static int NearestStep(float v)
        {
            int best = 0;
            for (int i = 1; i < VolumeSteps.Length; i++)
                if (Mathf.Abs(VolumeSteps[i] - v) < Mathf.Abs(VolumeSteps[best] - v)) best = i;
            return best;
        }

        // ───────────────────────── 소리별 설정 ─────────────────────────

        /// <summary>같은 소리의 최소 재생 간격(초). 수백 마리가 동시에 맞아도 소리가 뭉개지지 않도록 한다.</summary>
        public static float MinInterval(SfxId id)
        {
            switch (id)
            {
                case SfxId.Hit: return 0.05f;
                case SfxId.Kill: return 0.05f;
                case SfxId.Gem: return 0.04f;
                case SfxId.Slash: return 0.08f;
                case SfxId.Arrow: return 0.06f;
                case SfxId.EnemyShot: return 0.08f;
                case SfxId.Thunder: return 0.1f;
                case SfxId.Explosion: return 0.1f;
                case SfxId.Fire: return 0.15f;
                case SfxId.Splash: return 0.25f;   // 아군 셋이 함께 강에 들어가도 한 번처럼 들리게
                default: return DefaultMinInterval;
            }
        }

        /// <summary>자주 반복되는 소리는 음 높이를 조금씩 흔들어 단조롭지 않게 한다.</summary>
        static float PitchVariance(SfxId id)
        {
            switch (id)
            {
                case SfxId.Hit:
                case SfxId.Kill:
                case SfxId.Gem:
                case SfxId.Arrow:
                    return 0.06f;
                default:
                    return 0f;
            }
        }
    }
}
