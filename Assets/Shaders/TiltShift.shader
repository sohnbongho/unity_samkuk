// 틸트 시프트(HD-2D Step 14-5): 화면 위아래 띠를 흐리게 해 미니어처(디오라마)처럼 보이게 한다.
// URP 의 Full Screen Pass Renderer Feature 가 카메라 색을 _BlitTexture 로 주고 이 셰이더로 다시 그린다 (2D 렌더러, 후처리 전).
// 깊이를 쓰지 않는다(2D 스프라이트는 깊이가 없다): 화면 y 로만 흐림 세기를 정한다. 가운데 띠(_Band)는 또렷하고 가장자리로 갈수록 2차 곡선으로 흐려진다.
// 흐림은 중심 + 두 고리(8 + 4) 13탭 원판 샘플이라 내장 그래픽에서도 가볍다. 값은 TiltShiftPreset(코드)이 머티리얼에 넣는다.
Shader "Samkuk/TiltShift"
{
    Properties
    {
        _TiltShiftAmount ("Amount", Range(0, 1)) = 1
        _TiltShiftFocus ("Focus Y", Range(0, 1)) = 0.5
        _TiltShiftBand ("Sharp Band Half Height", Range(0, 0.5)) = 0.18
        _TiltShiftRadius ("Max Blur Radius (px)", Float) = 6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            Name "TiltShift"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float _TiltShiftAmount;
            float _TiltShiftFocus;
            float _TiltShiftBand;
            float _TiltShiftRadius;

            half4 Tap(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);
            }

            half4 Frag(Varyings input) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord.xy;

                // 가운데 띠 밖으로 나간 거리(0~1)를 2차 곡선으로: 띠 가장자리에서 부드럽게 시작해 화면 끝에서 최대
                float d = abs(uv.y - _TiltShiftFocus) - _TiltShiftBand;
                float t = saturate(d / max(0.001, 0.5 - _TiltShiftBand));
                float radius = t * t * _TiltShiftRadius * _TiltShiftAmount;

                half4 center = Tap(uv);
                if (radius < 0.5) return center;

                float2 px = _BlitTexture_TexelSize.xy * radius;
                half4 sum = center * 2.0;
                // 안쪽 고리 4탭 (반지름 절반)
                sum += Tap(uv + float2( 0.5,  0.0) * px);
                sum += Tap(uv + float2(-0.5,  0.0) * px);
                sum += Tap(uv + float2( 0.0,  0.5) * px);
                sum += Tap(uv + float2( 0.0, -0.5) * px);
                // 바깥 고리 8탭
                sum += Tap(uv + float2( 1.0,  0.0) * px);
                sum += Tap(uv + float2(-1.0,  0.0) * px);
                sum += Tap(uv + float2( 0.0,  1.0) * px);
                sum += Tap(uv + float2( 0.0, -1.0) * px);
                sum += Tap(uv + float2( 0.707,  0.707) * px);
                sum += Tap(uv + float2(-0.707,  0.707) * px);
                sum += Tap(uv + float2( 0.707, -0.707) * px);
                sum += Tap(uv + float2(-0.707, -0.707) * px);
                return sum / 14.0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
