// 2.5D 틸트(Step 16-1)의 스프라이트 셰이더. 3D 렌더러(Renderer3D)에서 쓰며 2D 렌더러에서는 쓰지 않는다(Universal2D 패스가 없다).
// 한 셰이더로 세 가지를 만든다 (값은 TiltView 가 머티리얼에 넣는다):
//  - 서 있는 그림(캐릭터, 서 있는 소품, 손에 든 무기): _Stand 1. 트랜스폼은 탑다운 2D 그대로 두고 정점 셰이더가 그림을 피벗(발) 기준으로
//    카메라의 위쪽(_SamkukStandUp)으로 세운다 — Rigidbody2D/정렬/반투명 규칙 같은 2D 코드가 그대로 동작한다. 알파 컷아웃 + 깊이 쓰기 + 그림자 드리움.
//  - 반투명 서 있는 그림(겹쳐서 흐려진 소품, 체력 막대): 같은 세움 + 알파 블렌드, 깊이 안 씀, 그림자 안 드리움.
//  - 바닥 그림(배경 타일, 얼룩, 강, 연못, 바닥 장식): _Stand 0. 눕힌 채 알파 블렌드, 햇빛 그림자를 받는다.
// 빛: 환경광(SH) + 주광 × 그림자 + 점광원. 법선·N·L 은 쓰지 않는다(도트 그림은 평평하게, 그늘만 진다).
Shader "Samkuk/PixelSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Stand ("Stand Up (0 = flat, 1 = standing)", Float) = 1
        _FootLift ("Foot Lift (units, pivot to bottom edge)", Float) = 0
        _Toward ("Toward Camera (units)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _StandSeed ("Batching Breaker (per renderer)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Cutoff;
            float _Stand;
            float _FootLift;
            float _Toward;
        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        // 전역값 (TiltView 가 넣는다): 서 있는 그림의 위쪽 방향과 카메라 쪽 방향. 틸트가 꺼져 있으면 (0,1,0) / 0 이라 세워도 그대로다.
        float4 _SamkukStandUp;
        float4 _SamkukStandToward;

        // 렌더러마다 다른 값(StandingSprite 가 속성 블록으로 넣음). 쓰지는 않는다 — 스프라이트 배칭을 끊어 오브젝트 행렬이 그림마다 남게 하는 용도.
        float _StandSeed;

        // 오브젝트 원점(피벗 = 발)을 지나는 평면에 그림을 세운다: 그림의 x 는 세계 x, 그림의 y 는 카메라 위쪽.
        // 전제: 스프라이트가 배칭되지 않아 UNITY_MATRIX_M 이 이 그림의 트랜스폼이다 (배칭되면 단위 행렬 → 세계 원점 기준으로 세워져 틀린다).
        // 발 아래로 그려진 부분(_FootLift, 걷기 시트 8px)만큼 올려 그림의 아래 끝이 땅(원점)에 닿게 한다. 배율은 오브젝트의 y 축 길이로 따른다.
        float3 StandWS(float3 positionOS)
        {
            float3 origin = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
            float3 pw = TransformObjectToWorld(positionOS);
            float3 d = pw - origin;
            float scaleY = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
            float3 stood = origin + float3(d.x, 0.0, 0.0)
                + _SamkukStandUp.xyz * (d.y + _FootLift * scaleY)
                + _SamkukStandToward.xyz * _Toward;
            return lerp(pw, stood, _Stand);
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 pw = StandWS(v.positionOS.xyz);
                o.positionWS = pw;
                o.positionCS = TransformWorldToHClip(pw);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                clip(c.a - _Cutoff);

                // 환경광 + 주광(그림자) + 점광원. 법선 없이 세기만 더한다
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 light = SampleSH(half3(0.0, 0.0, -1.0))
                    + mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);

                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS);
                    light += l.color * (l.distanceAttenuation * l.shadowAttenuation);
                LIGHT_LOOP_END
                #endif

                return half4(c.rgb * light, c.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half alpha : TEXCOORD1;
            };

            Varyings ShadowVert(Attributes v)
            {
                Varyings o;
                float3 pw = StandWS(v.positionOS.xyz);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - pw);
                #else
                float3 lightDir = _LightDirection;
                #endif
                // 법선을 빛 쪽으로 두면 법선 편향은 0 이고 깊이 편향만 남아, 그림이 제 그림자에 얼룩지지 않는다
                pw = ApplyShadowBias(pw, lightDir, lightDir);
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(pw));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.alpha = v.color.a * _Color.a;
                return o;
            }

            half4 ShadowFrag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.alpha;
                clip(a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
