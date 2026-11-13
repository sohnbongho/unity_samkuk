using System;
using System.Collections.Generic;

namespace Samkuk.Compat
{
    // 유니티 URP 쪽 이름의 표시 데이터 (G3/G4). 전투 연출 컴포넌트(BattleLighting, BattlePostFx, BattleTiltShift...)가
    // 원본 그대로 값을 채우고, 화면 층이 그 값을 Godot 의 빛·WorldEnvironment·전체 화면 셰이더로 옮긴다.
    // 2D 조명(Light2D)과 픽셀 퍼펙트 카메라는 Godot 판에서 그리지 않는다(틸트 한 가지뿐) — 옮긴 코드와 테스트가 돌도록 값만 둔다.

    /// <summary>2D 빛의 값 (Godot 판에서는 그리지 않는다).</summary>
    public class Light2D : Behaviour
    {
        public enum LightType { Parametric = 0, Freeform = 1, Sprite = 2, Point = 3, Global = 4 }
        public LightType lightType = LightType.Point;
        public Color color = Color.white;
        public float intensity = 1f;
        public float pointLightOuterRadius = 1f, pointLightInnerRadius;
        public float falloffIntensity = 0.5f;
        public bool shadowsEnabled;
        public float shadowIntensity = 0.75f;
    }

    /// <summary>픽셀 퍼펙트 카메라의 값 (Godot 판에서는 쓰지 않는다).</summary>
    public class PixelPerfectCamera : Behaviour
    {
        public enum CropFrame { None, Pillarbox, Letterbox, Windowbox, StretchFill }
        public enum GridSnapping { None, PixelSnapping, UpscaleRenderTexture }
        public int assetsPPU = 100, refResolutionX = 320, refResolutionY = 180;
        public CropFrame cropFrame = CropFrame.None;
        public GridSnapping gridSnapping = GridSnapping.None;
    }

    /// <summary>카메라의 렌더 설정 (후처리 켜짐, 쓰는 렌더러 번호).</summary>
    public class UniversalAdditionalCameraData : Behaviour
    {
        public bool renderPostProcessing;
        public LayerMask volumeLayerMask = 1;
        public int rendererIndex = -1;
        public void SetRenderer(int index) => rendererIndex = index;
    }

    public static class CameraExtensions
    {
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera)
            => camera.GetComponent<UniversalAdditionalCameraData>() ?? camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
    }

    /// <summary>렌더러 기능의 켜짐 표시 (틸트 시프트 전체 화면 패스).</summary>
    public class ScriptableRendererFeature : ScriptableObject
    {
        public bool isActive { get; private set; }
        public void SetActive(bool active) => isActive = active;
    }

    /// <summary>전체 화면 패스 (틸트 시프트): 쓰는 재질만 든다.</summary>
    public sealed class FullScreenPassRendererFeature : ScriptableRendererFeature
    {
        public Material passMaterial;
    }

    public sealed class ScriptableRendererData : ScriptableObject { }

    public sealed class UniversalRenderPipelineAsset
    {
        public ScriptableRendererData[] rendererDataList = { new ScriptableRendererData { name = "Renderer2D" }, new ScriptableRendererData { name = "Renderer3D" } };
    }

    public static class UniversalRenderPipeline
    {
        public static UniversalRenderPipelineAsset asset = new UniversalRenderPipelineAsset();
    }

    public struct ScriptableRenderContext { }

    public static class RenderPipelineManager
    {
#pragma warning disable CS0067   // 유니티에서는 카메라가 그리기 직전에 불렸다. 여기서는 불리지 않는다(컬링은 Godot 이 한다)
        public static event Action<ScriptableRenderContext, Camera> beginCameraRendering;
#pragma warning restore CS0067
    }

    // ---- 후처리 볼륨 ----

    public class VolumeParameter<T>
    {
        public T value;
        public bool overrideState;
    }
    public sealed class FloatParameter : VolumeParameter<float> { }
    public sealed class BoolParameter : VolumeParameter<bool> { }
    public sealed class ColorParameter : VolumeParameter<Color> { }
    public sealed class Vector2Parameter : VolumeParameter<Vector2> { }

    public class VolumeComponent : ScriptableObject
    {
        public bool active = true;
    }

    public sealed class Bloom : VolumeComponent
    {
        public FloatParameter threshold = new FloatParameter { value = 0.9f }, intensity = new FloatParameter(), scatter = new FloatParameter { value = 0.7f };
        public ColorParameter tint = new ColorParameter { value = Color.white };
        public BoolParameter highQualityFiltering = new BoolParameter();
    }
    public sealed class Vignette : VolumeComponent
    {
        public ColorParameter color = new ColorParameter { value = Color.black };
        public Vector2Parameter center = new Vector2Parameter { value = new Vector2(0.5f, 0.5f) };
        public FloatParameter intensity = new FloatParameter(), smoothness = new FloatParameter { value = 0.2f };
        public BoolParameter rounded = new BoolParameter();
    }
    public sealed class ColorAdjustments : VolumeComponent
    {
        public FloatParameter postExposure = new FloatParameter(), contrast = new FloatParameter(), saturation = new FloatParameter(), hueShift = new FloatParameter();
        public ColorParameter colorFilter = new ColorParameter { value = Color.white };
    }
    public sealed class WhiteBalance : VolumeComponent
    {
        public FloatParameter temperature = new FloatParameter(), tint = new FloatParameter();
    }

    /// <summary>후처리 효과 묶음.</summary>
    public sealed class VolumeProfile : ScriptableObject
    {
        public readonly List<VolumeComponent> components = new List<VolumeComponent>();
        /// <summary>값이 바뀌었을 수 있다는 표시 (화면 층이 다시 읽는다).</summary>
        public int version;

        public bool TryGet<T>(out T component) where T : VolumeComponent
        {
            foreach (var c in components) if (c is T t) { component = t; return true; }
            component = null;
            return false;
        }
        public bool Has<T>() where T : VolumeComponent => TryGet<T>(out _);
        public T Add<T>(bool overrides = false) where T : VolumeComponent
        {
            var c = (T)Activator.CreateInstance(typeof(T));
            if (overrides)
                foreach (var f in typeof(T).GetFields())
                    if (f.GetValue(c) is object p && p.GetType().GetField("overrideState") is System.Reflection.FieldInfo o) o.SetValue(p, true);
            components.Add(c);
            version++;
            return c;
        }
    }

    public class Volume : Behaviour
    {
        public bool isGlobal = true;
        public float priority, weight = 1f;
        public VolumeProfile sharedProfile;
        public VolumeProfile profile { get => sharedProfile; set => sharedProfile = value; }
    }

    // ---- 소리 ----

    /// <summary>소리 한 건: 샘플 배열 (코드 합성) 또는 파일 경로.</summary>
    public sealed class AudioClip : Object
    {
        public string name, path;
        /// <summary>소리 샘플 (-1~1). 화면 층이 이것으로 재생용 스트림을 만든다.</summary>
        public float[] data;
        public int frequency = 44100, channels = 1;
        /// <summary>샘플 수 (채널당).</summary>
        public int samples => data != null ? data.Length / Mathf.Max(1, channels) : 0;
        public float length => frequency > 0 ? samples / (float)frequency : 0f;

        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream)
            => new AudioClip { name = name, data = new float[lengthSamples * Mathf.Max(1, channels)], channels = channels, frequency = frequency };
        public bool SetData(float[] source, int offsetSamples)
        {
            Array.Copy(source, 0, data, offsetSamples, Mathf.Min(source.Length, data.Length - offsetSamples));
            return true;
        }
        public bool GetData(float[] target, int offsetSamples)
        {
            Array.Copy(data, offsetSamples, target, 0, Mathf.Min(target.Length, data.Length - offsetSamples));
            return true;
        }
    }

    /// <summary>소리 내는 곳. 실제 재생은 화면 층이 <see cref="Output"/> 에 꽂는다 (없으면 조용히 넘어간다 — 테스트).</summary>
    public class AudioSource : Behaviour
    {
        public bool playOnAwake = true, loop;
        public float spatialBlend, pitch = 1f, volume = 1f;
        public AudioClip clip;
        /// <summary>(소리, 볼륨, 높낮이)</summary>
        public static Action<AudioClip, float, float> Output;

        public void PlayOneShot(AudioClip clip, float volumeScale = 1f)
        {
            if (clip != null) Output?.Invoke(clip, volumeScale * volume, pitch);
        }
        public void Play() => PlayOneShot(clip);
        public void Stop() { }
    }

    /// <summary>레이어 이름 ↔ 번호.</summary>
    public struct LayerMask
    {
        public int value;
        public static implicit operator int(LayerMask mask) => mask.value;
        public static implicit operator LayerMask(int value) => new LayerMask { value = value };

        public static readonly List<string> Names = new List<string> { "Default", "TransparentFX", "Ignore Raycast", "", "Water", "UI", "", "", "Player", "Enemy", "PlayerProjectile", "Pickup" };
        public static int NameToLayer(string name) => Names.IndexOf(name);
        public static string LayerToName(int layer) => layer >= 0 && layer < Names.Count ? Names[layer] : "";
        public static int GetMask(params string[] names)
        {
            int mask = 0;
            foreach (string n in names) { int i = NameToLayer(n); if (i >= 0) mask |= 1 << i; }
            return mask;
        }
    }
}
