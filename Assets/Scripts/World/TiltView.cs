using System;
using System.Collections.Generic;
using Samkuk.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Samkuk.World
{
    /// <summary>
    /// 2.5D 틸트(Step 16-1)의 실행 중 상태 한 곳. 설정(<see cref="Hd2dSettings.Tilt"/>)이 켜져 있고 전투 카메라(<see cref="BattleTiltCamera"/>)가
    /// 3D 렌더러·셰이더와 함께 살아 있을 때만 <see cref="Active"/> 다 — 타이틀/내정/맵 편집기/테스트에는 틸트 카메라가 없으므로 자연히 꺼진다.
    /// 켜고 끌 때(<see cref="Refresh"/>) 셰이더 전역값을 맞추고 <see cref="Changed"/> 로 알린다: 카메라, 조명, 배경, 서 있는 그림이 따라온다.
    /// 2D 코드는 좌표를 바꾸지 않는다. 높이가 필요한 곳(손 높이, 체력 막대, 횃불)만 <see cref="Lift"/> 로 카메라 위쪽을 따라 올린다.
    /// </summary>
    public static class TiltView
    {
        public const string RendererName = "Renderer3D";
        public const string ShaderName = "Samkuk/PixelSprite";

        static readonly int StandUpId = Shader.PropertyToID("_SamkukStandUp");
        static readonly int StandTowardId = Shader.PropertyToID("_SamkukStandToward");
        static readonly int StandId = Shader.PropertyToID("_Stand");
        static readonly int FootLiftId = Shader.PropertyToID("_FootLift");
        static readonly int TowardId = Shader.PropertyToID("_Toward");
        static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        static readonly int StretchScaleId = Shader.PropertyToID("_StretchScale");
        static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        static bool ready;
        static Shader shader;
        static bool shaderSearched;
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        /// <summary>켜지거나 꺼졌다 (또는 다시 맞춰야 한다). 구독자는 자기 상태를 <see cref="Active"/> 로 다시 맞춘다.</summary>
        public static event Action Changed;

        /// <summary>전투 카메라가 3D 렌더러와 함께 준비됐는가.</summary>
        public static bool Ready => ready;

        /// <summary>지금 틸트로 보고 있는가.</summary>
        public static bool Active => ready && Hd2dSettings.Tilt;

        /// <summary>전투 카메라가 준비/해제를 알린다. 바뀌면 <see cref="Refresh"/>.</summary>
        public static void SetReady(bool value)
        {
            if (ready == value) return;
            ready = value;
            Refresh();
        }

        /// <summary>설정이 바뀌었을 때(디버그 키 F10) 전역값을 맞추고 모두에게 알린다.</summary>
        public static void Refresh()
        {
            bool on = Active;
            // 세우는 방향 x 세로 배율(1/cos 기울기): 셰이더는 그림의 y 에 이 벡터를 곱하므로 길이가 곧 배율이다 (직교라 화면 어디서나 같다)
            Shader.SetGlobalVector(StandUpId, on ? (Vector4)(TiltPreset.StandUp * TiltPreset.StandScale(TiltPreset.Pitch)) : new Vector4(0f, 1f, 0f, 0f));
            Shader.SetGlobalVector(StandTowardId, on ? (Vector4)(-TiltPreset.Forward(TiltPreset.Pitch)) : Vector4.zero);
            Changed?.Invoke();
        }

        // ───────────────────────── 기하 ─────────────────────────

        /// <summary>카메라가 보는 방향 (틸트와 무관한 상수).</summary>
        public static Vector3 Forward => TiltPreset.Forward(TiltPreset.Pitch);
        /// <summary>서 있는 그림의 위쪽(수직, -z). 꺼져 있으면 2D 의 위(+y).</summary>
        public static Vector3 Up => Active ? TiltPreset.StandUp : Vector3.up;
        /// <summary>카메라의 위쪽 (화면 흔들림용). 꺼져 있으면 +y.</summary>
        public static Vector3 CameraUp => Active ? TiltPreset.Up(TiltPreset.Pitch) : Vector3.up;
        public static Vector3 Right => Vector3.right;
        /// <summary>서 있는 그림의 회전. 꺼져 있으면 회전 없음. 트랜스폼을 직접 돌리는 것(데미지 숫자)만 쓴다 — 스프라이트는 셰이더가 세운다.</summary>
        public static Quaternion StandingRotation => Active ? TiltPreset.StandRotation : Quaternion.identity;

        /// <summary>전투 카메라의 트랜스폼 (<see cref="BattleTiltCamera"/> 가 켜질 때 넣는다).</summary>
        public static Transform CameraTransform { get; set; }

        /// <summary>서 있는 그림의 세로 배율 (틸트면 1/cos 기울기, 아니면 1). 직교 카메라라 화면 어디서나 같다.</summary>
        public static float StandScale => Active ? TiltPreset.StandScale(TiltPreset.Pitch) : 1f;

        /// <summary>어떤 자리의 세로 배율. 직교 카메라에서는 자리와 무관하게 <see cref="StandScale"/> (호출처가 자리를 넘기는 형태를 유지).</summary>
        public static float StandScaleAt(Vector2 position) => StandScale;

        /// <summary>발에서 화면 기준 <paramref name="height"/> 만큼 "위". 틸트면 수직으로 세로 배율을 곱해(그림과 같은 비율), 아니면 +y.</summary>
        public static Vector3 Lift(float height) => Up * (height * StandScale);

        /// <summary>그 자리의 서 있는 그림과 같은 배율로 <paramref name="height"/> 만큼 "위" (무기 손, 체력 막대, 횃불, 데미지 숫자).</summary>
        public static Vector3 Lift(float height, Vector2 at) => Up * (height * StandScaleAt(at));

        /// <summary>바닥 그림의 z: 틸트면 <see cref="TiltPreset.GroundZ"/>, 아니면 예전 값 그대로.</summary>
        public static float GroundZ(float legacyZ) => Active ? TiltPreset.GroundZ : legacyZ;

        /// <summary>카메라 트랜스폼이 바라보는 땅 위의 점 (z = 0). 틸트가 꺼져 있으면 카메라 위치 그대로(z 만 0).</summary>
        public static Vector3 LookPoint(Transform camera)
        {
            Vector3 p = Active ? camera.position + Forward * TiltPreset.Distance : camera.position;
            return new Vector3(p.x, p.y, 0f);
        }

        /// <summary>바라보는 점 기준으로 화면에 보이는 땅의 범위.</summary>
        public static Rect GroundFootprint(float aspect) =>
            TiltPreset.GroundFootprint(TiltPreset.Pitch, TiltPreset.OrthoSize, aspect);

        /// <summary>바라보는 점에서 화면 밖까지의 거리 (적 스폰 링).</summary>
        public static float SpawnRadius(float aspect) => TiltPreset.SpawnRadius(GroundFootprint(aspect));

        // ───────────────────────── 렌더러 / 재질 ─────────────────────────

        /// <summary>URP 에셋의 렌더러 목록에서 3D 렌더러(<see cref="RendererName"/>) 자리. 없으면 -1 (셋업 Step 16-1 이 만든다).</summary>
        public static int FindRendererIndex()
        {
            var asset = UniversalRenderPipeline.asset;
            if (asset == null) return -1;
            var list = asset.rendererDataList;
            for (int i = 0; i < list.Length; i++)
                if (list[i] != null && list[i].name == RendererName) return i;
            return -1;
        }

        /// <summary>틸트 셰이더. 컴파일에 실패했거나 없으면 null — 그러면 틸트를 켜지 않는다.</summary>
        public static Shader TiltShader
        {
            get
            {
                if (!shaderSearched)
                {
                    shaderSearched = true;
                    shader = Shader.Find(ShaderName);
                }
                return shader;
            }
        }

        public static bool ShaderAvailable => TiltShader != null;

        /// <summary>바닥 그림 재질: 눕힌 채 알파 블렌드, 그림자를 받는다. 셰이더가 없으면 null.</summary>
        public static Material GroundMaterial => Get(false, 0f, 0f, 0f, 1f);

        /// <summary>
        /// 서 있는 그림 재질. <paramref name="footLift"/> 는 피벗에서 그림 아래 끝까지(유닛, 배율 1), <paramref name="toward"/> 는 카메라 쪽으로 미는 거리.
        /// <paramref name="translucent"/> 면 알파 블렌드(깊이 안 씀, 그림자 안 드리움). 같은 값은 같은 재질을 돌려준다.
        /// </summary>
        public static Material StandingMaterial(float footLift, float toward = 0f, bool translucent = false, float stretchScale = 1f) =>
            Get(!translucent, 1f, footLift, toward, stretchScale);

        static Material Get(bool cutout, float stand, float footLift, float toward, float stretchScale)
        {
            var s = TiltShader;
            if (s == null) return null;
            string key = $"{(cutout ? "c" : "t")}|{stand}|{footLift:0.####}|{toward:0.####}|{stretchScale:0.####}";
            if (materials.TryGetValue(key, out var m) && m != null) return m;

            m = new Material(s) { name = "PixelSprite " + key, hideFlags = HideFlags.HideAndDontSave };
            m.SetFloat(StandId, stand);
            m.SetFloat(FootLiftId, footLift);
            m.SetFloat(TowardId, toward);
            m.SetFloat(StretchScaleId, stretchScale);
            if (cutout)
            {
                m.SetFloat(SrcBlendId, (float)BlendMode.One);
                m.SetFloat(DstBlendId, (float)BlendMode.Zero);
                m.SetFloat(ZWriteId, 1f);
                m.SetFloat(CutoffId, 0.5f);
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                m.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
                m.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat(ZWriteId, 0f);
                m.SetFloat(CutoffId, 0.01f);
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);   // 반투명한 것은 그림자를 드리우지 않는다
            }
            materials[key] = m;
            return m;
        }

        static readonly Dictionary<Texture, Material> solidMaterials = new Dictionary<Texture, Material>();

        /// <summary>
        /// 메시(절벽 윗면·옆면, Step 16-2)용 불투명 재질: 같은 셰이더를 눕힌 채(_Stand 0) 컷아웃·깊이 쓰기로, 그림자를 드리우고 받는다.
        /// 텍스처마다 하나씩 돌려 쓴다. 셰이더나 텍스처가 없으면 null.
        /// </summary>
        public static Material SolidMaterial(Texture texture)
        {
            if (texture == null) return null;
            if (solidMaterials.TryGetValue(texture, out var m) && m != null) return m;
            var s = TiltShader;
            if (s == null) return null;
            m = new Material(s) { name = "PixelSprite solid " + texture.name, hideFlags = HideFlags.HideAndDontSave, mainTexture = texture };
            m.SetFloat(StandId, 0f);
            m.SetFloat(FootLiftId, 0f);
            m.SetFloat(TowardId, 0f);
            m.SetFloat(SrcBlendId, (float)BlendMode.One);
            m.SetFloat(DstBlendId, (float)BlendMode.Zero);
            m.SetFloat(ZWriteId, 1f);
            m.SetFloat(CutoffId, 0.5f);
            m.renderQueue = (int)RenderQueue.Geometry;
            solidMaterials[texture] = m;
            return m;
        }

        /// <summary>
        /// 바닥 그림 렌더러를 틸트 상태에 맞춘다: 켜져 있으면 바닥 재질 + 그림자 받기, 아니면 <paramref name="fallback"/>.
        /// 돌려주는 값: 지금 넣은 재질 (소품 스포너가 같은 재질을 빌려 쓴다).
        /// </summary>
        public static Material ConfigureGround(SpriteRenderer sr, Material fallback)
        {
            if (sr == null) return fallback;
            var ground = Active ? GroundMaterial : null;
            var mat = ground != null ? ground : fallback;
            if (mat != null && sr.sharedMaterial != mat) sr.sharedMaterial = mat;   // 둘 다 없으면(테스트) 기본 스프라이트 재질 그대로
            sr.receiveShadows = ground != null;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            return mat;
        }

        /// <summary>테스트용: 준비 상태를 내리고 전역값을 되돌린다.</summary>
        public static void ResetForTests()
        {
            ready = false;
            Refresh();
        }
    }
}
