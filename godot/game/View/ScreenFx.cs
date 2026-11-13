using Godot;
using Samkuk.Compat;
using Samkuk.World;
using Mathf = Samkuk.Compat.Mathf;
using GColor = Godot.Color;

namespace Samkuk.Game
{
    /// <summary>
    /// 전투 화면의 후처리를 비춘다 (G4). 순수 로직의 <see cref="BattlePostFx"/>(블룸·비네트·색 보정·색온도 값)와
    /// <see cref="BattleTiltShift"/>(틸트 시프트 켜짐)를 읽어 Godot 의 환경(글로우, 색 보정)과 전체 화면 셰이더(<c>shaders/screen_fx.gdshader</c>)에 넣는다.
    /// 전투 씬이 아니면(그 컴포넌트가 없으면) 전부 끈다. UI 는 이 층보다 위라 흐려지지 않는다.
    /// </summary>
    public partial class ScreenFx : CanvasLayer
    {
        ColorRect rect;
        ShaderMaterial material;
        BattlePostFx postFx;
        BattleTiltShift tiltShift;
        float nextSearch;

        public override void _Ready()
        {
            Layer = 1;   // 세계 위, UI(10) 아래
            material = new ShaderMaterial { Shader = GD.Load<Godot.Shader>("res://shaders/screen_fx.gdshader") };
            rect = new ColorRect { Name = "ScreenFx", Material = material, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(rect);
        }

        public void Sync(Godot.Environment env, float screenHeight)
        {
            // 전투 카메라의 컴포넌트는 씬마다 하나: 없을 때는 가끔만 찾는다
            if ((postFx == null || tiltShift == null) && Compat.Time.unscaledTime >= nextSearch)
            {
                nextSearch = Compat.Time.unscaledTime + 0.5f;
                postFx = Compat.Object.FindAnyObjectByType<BattlePostFx>();
                tiltShift = Compat.Object.FindAnyObjectByType<BattleTiltShift>();
            }

            bool fx = postFx != null && postFx.IsEnabled && postFx.Profile != null;
            bool tilt = tiltShift != null && tiltShift.IsEnabled;

            float vignette = 0f, smooth = 0.45f, temperature = 0f, tint = 0f;
            env.GlowEnabled = false;
            env.AdjustmentEnabled = false;
            if (fx)
            {
                var profile = postFx.Profile;
                if (profile.TryGet<Bloom>(out var bloom) && bloom.intensity.value > 0f)
                {
                    // 문턱 1: HDR 로 1 을 넘는 빛(횃불, 플레이어 빛이 겹친 곳)만 번진다
                    env.GlowEnabled = true;
                    env.GlowHdrThreshold = bloom.threshold.value;
                    env.GlowIntensity = bloom.intensity.value;
                    env.GlowBloom = 0f;
                    env.GlowStrength = 1f;
                    env.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive;
                }
                if (profile.TryGet<ColorAdjustments>(out var adjust))
                {
                    // 유니티 값은 -100~100 (0 = 그대로), Godot 은 배율(1 = 그대로)
                    env.AdjustmentEnabled = true;
                    env.AdjustmentContrast = 1f + adjust.contrast.value / 100f;
                    env.AdjustmentSaturation = 1f + adjust.saturation.value / 100f;
                    env.AdjustmentBrightness = Mathf.Pow(2f, adjust.postExposure.value);
                }
                if (profile.TryGet<Vignette>(out var v)) { vignette = v.intensity.value; smooth = v.smoothness.value; }
                if (profile.TryGet<WhiteBalance>(out var wb)) { temperature = wb.temperature.value; tint = wb.tint.value; }
            }

            rect.Visible = fx || tilt;
            if (!rect.Visible) return;
            material.SetShaderParameter("tilt_amount", tilt ? TiltShiftPreset.Amount : 0f);
            material.SetShaderParameter("tilt_focus", TiltShiftPreset.Focus);
            material.SetShaderParameter("tilt_band", TiltShiftPreset.Band);
            material.SetShaderParameter("tilt_radius", TiltShiftPreset.RadiusFor(Mathf.RoundToInt(screenHeight)));
            material.SetShaderParameter("vignette_intensity", vignette);
            material.SetShaderParameter("vignette_smoothness", smooth);
            // 색온도(양수 = 따뜻하게): 빨강을 올리고 파랑을 내린다. 기울기(양수 = 자홍): 초록을 내린다. 값이 작아(±12) 어림으로 충분하다
            material.SetShaderParameter("balance", new Godot.Vector3(1f + temperature * 0.002f, 1f - tint * 0.002f, 1f - temperature * 0.002f));
        }
    }
}
