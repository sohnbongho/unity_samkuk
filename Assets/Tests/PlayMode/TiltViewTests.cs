using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Samkuk.Tests
{
    /// <summary>2.5D 틸트(Step 16-1): 카메라 기하, 서 있는 그림 재질, 틸트일 때 비켜 주는 것들(픽셀 퍼펙트, 가짜 그림자), 3D 조명.</summary>
    public class TiltViewTests
    {
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            MapStore.Disabled = true;
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
            Hd2dSettings.TiltOverride = true;
            TiltView.ResetForTests();   // 준비 안 됨 = 꺼짐. 켜는 테스트는 SetReady(true)
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in toDestroy) if (o != null) Object.DestroyImmediate(o);
            toDestroy.Clear();
            TiltView.ResetForTests();
            Hd2dSettings.ResetOverrides();
            MapStore.Disabled = false;
            TerrainCollision.Active = null;
            TerrainThemeCatalog.Use(null);
        }

        // ───────────────────────── 기하 ─────────────────────────

        [Test]
        public void Preset_CameraPose_LooksAtTargetFromSouthAbove()
        {
            var look = new Vector3(3f, 4f, 0f);
            TiltPreset.CameraPose(look, TiltPreset.Pitch, TiltPreset.Distance, out var pos, out var rot);
            Assert.Less(pos.y, look.y, "남쪽으로 물러난다");
            Assert.Less(pos.z, 0f, "땅 위(카메라 쪽 = -z)");
            Assert.AreEqual(look.x, pos.x, 1e-5f);
            Assert.AreEqual(TiltPreset.Distance, Vector3.Distance(pos, look), 1e-4f);

            var forward = rot * Vector3.forward;
            var toLook = (look - pos).normalized;
            Assert.AreEqual(1f, Vector3.Dot(forward, toLook), 1e-4f, "회전이 바라보는 점을 향한다");
            Assert.AreEqual(1f, Vector3.Dot(rot * Vector3.up, TiltPreset.Up(TiltPreset.Pitch)), 1e-4f, "카메라 위쪽 = 서 있는 그림의 위쪽");
        }

        [Test]
        public void Preset_Up_IsPerpendicularToForward_AndRisesFromGround()
        {
            var f = TiltPreset.Forward(TiltPreset.Pitch);
            var u = TiltPreset.Up(TiltPreset.Pitch);
            Assert.AreEqual(0f, Vector3.Dot(f, u), 1e-5f);
            Assert.Less(u.z, 0f, "위 = -z");
            Assert.Greater(u.y, 0f, "위는 화면에서 북쪽으로도 간다");
            Assert.Greater(f.z, 0f, "카메라는 땅을 향한다");
            Assert.AreEqual(Vector3.forward, TiltPreset.Forward(90f), "90도면 완전 탑다운");
        }

        [Test]
        public void Preset_GroundFootprint_CoversOldOrthographicView_AndReachesFartherNorth()
        {
            var r = TiltPreset.GroundFootprint(TiltPreset.Pitch, TiltPreset.Distance, TiltPreset.FieldOfView, 16f / 9f);
            Assert.That(r.width, Is.GreaterThanOrEqualTo(18f).And.LessThan(40f), "예전 직교 화면 폭(20) 안팎");
            Assert.That(r.height, Is.GreaterThanOrEqualTo(11f).And.LessThan(30f), "예전 직교 화면 높이(11.25) 이상");
            Assert.IsTrue(r.Contains(Vector2.zero), "바라보는 점은 화면 안");
            Assert.Greater(r.yMax, -r.yMin, "북쪽(멀리)이 남쪽보다 넓게 보인다");
            Assert.AreEqual(-r.xMin, r.xMax, 1e-4f, "좌우 대칭");
        }

        [Test]
        public void Preset_SpawnRadius_IsBeyondEveryCorner()
        {
            var r = TiltPreset.GroundFootprint(TiltPreset.Pitch, TiltPreset.Distance, TiltPreset.FieldOfView, 16f / 9f);
            float radius = TiltPreset.SpawnRadius(r);
            foreach (var corner in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) })
                Assert.LessOrEqual(corner.magnitude, radius + 1e-4f);
            Assert.Greater(radius, Mathf.Sqrt(10f * 10f + 5.625f * 5.625f), "예전 직교 화면 대각선보다 멀리");
        }

        [TestCase(TimeOfDay.Day)]
        [TestCase(TimeOfDay.Dusk)]
        [TestCase(TimeOfDay.Dawn)]
        public void Preset_SunDirection_CastsShadowLikeShadowPreset(TimeOfDay time)
        {
            var s = ShadowPreset.For(time);
            var d = TiltPreset.SunDirection(s);
            Assert.Greater(d.z, 0f, "해는 땅을 향해 내리쬔다");
            // 높이 1 인 점이 땅에 닿을 때까지 빛을 따라가면 (shearX, -length) 만큼 밀린다
            float k = 1f / d.z;
            Assert.AreEqual(s.shearX, d.x * k, 1e-4f);
            Assert.AreEqual(-s.length, d.y * k, 1e-4f);
        }

        // ───────────────────────── 상태 ─────────────────────────

        [Test]
        public void View_IsInactive_WithoutBattleCamera_EvenIfSettingIsOn()
        {
            Assert.IsTrue(Hd2dSettings.Tilt);
            Assert.IsFalse(TiltView.Active, "틸트 카메라가 준비를 알리기 전엔 꺼져 있다 (타이틀/내정/맵 편집기/테스트)");
            Assert.AreEqual(Vector3.up, TiltView.Up);
            Assert.AreEqual(new Vector3(0f, 2f, 0f), TiltView.Lift(2f));
            Assert.AreEqual(Quaternion.identity, TiltView.StandingRotation);
            Assert.AreEqual(0.5f, TiltView.GroundZ(0.5f));
        }

        [Test]
        public void View_Active_FollowsSetting_AndRaisesChanged()
        {
            int changed = 0;
            void OnChanged() => changed++;
            TiltView.Changed += OnChanged;
            try
            {
                TiltView.SetReady(true);
                Assert.IsTrue(TiltView.Active);
                Assert.AreEqual(1, changed);
                Assert.AreEqual(TiltPreset.Up(TiltPreset.Pitch), TiltView.Lift(1f));
                Assert.AreEqual(TiltPreset.GroundZ, TiltView.GroundZ(0.5f));
                Assert.AreEqual(TiltPreset.Rotation(TiltPreset.Pitch), TiltView.StandingRotation);

                Hd2dSettings.TiltOverride = false;
                Assert.IsFalse(TiltView.Active, "설정이 꺼지면 카메라가 준비됐어도 꺼진다");
                TiltView.SetReady(true);
                Assert.AreEqual(1, changed, "같은 값이면 알리지 않는다");
            }
            finally { TiltView.Changed -= OnChanged; }
        }

        [Test]
        public void View_LookPoint_IsOnGround_InFrontOfCamera()
        {
            TiltView.SetReady(true);
            var go = new GameObject("Cam");
            toDestroy.Add(go);
            TiltPreset.CameraPose(new Vector3(5f, 7f, 0f), TiltPreset.Pitch, TiltPreset.Distance, out var pos, out var rot);
            go.transform.SetPositionAndRotation(pos, rot);
            var look = TiltView.LookPoint(go.transform);
            Assert.AreEqual(5f, look.x, 1e-4f);
            Assert.AreEqual(7f, look.y, 1e-4f);
            Assert.AreEqual(0f, look.z, 1e-4f);
        }

        // ───────────────────────── 재질 / 서 있는 그림 ─────────────────────────

        [Test]
        public void Materials_CutoutWritesDepthAndCastsShadow_TranslucentDoesNot()
        {
            if (!TiltView.ShaderAvailable) Assert.Ignore($"셰이더 {TiltView.ShaderName} 없음 (컴파일 오류?)");
            var cutout = TiltView.StandingMaterial(0.25f);
            var translucent = TiltView.StandingMaterial(0.25f, 0f, translucent: true);
            var ground = TiltView.GroundMaterial;
            Assert.AreNotSame(cutout, translucent);
            Assert.AreSame(cutout, TiltView.StandingMaterial(0.25f), "같은 값은 같은 재질");
            Assert.AreEqual((int)RenderQueue.AlphaTest, cutout.renderQueue);
            Assert.AreEqual(1f, cutout.GetFloat("_ZWrite"));
            Assert.AreEqual(0.25f, cutout.GetFloat("_FootLift"));
            Assert.AreEqual(1f, cutout.GetFloat("_Stand"));
            Assert.IsTrue(cutout.GetShaderPassEnabled("ShadowCaster"));
            Assert.AreEqual((int)RenderQueue.Transparent, translucent.renderQueue);
            Assert.AreEqual(0f, translucent.GetFloat("_ZWrite"));
            Assert.IsFalse(translucent.GetShaderPassEnabled("ShadowCaster"));
            Assert.AreEqual(0f, ground.GetFloat("_Stand"), "바닥은 눕힌 채");
            Assert.AreEqual((int)RenderQueue.Transparent, ground.renderQueue);
        }

        [Test]
        public void StandingSprite_SwapsMaterial_WhileTiltIsActive_AndRestores()
        {
            if (!TiltView.ShaderAvailable) Assert.Ignore($"셰이더 {TiltView.ShaderName} 없음 (컴파일 오류?)");
            var go = new GameObject("Body", typeof(SpriteRenderer));
            toDestroy.Add(go);
            var sr = go.GetComponent<SpriteRenderer>();
            var original = new Material(Shader.Find("Sprites/Default"));
            toDestroy.Add(original);
            sr.sharedMaterial = original;

            WorldSorting.Configure(sr, PixelArt.WalkFootUnits);
            var stand = go.GetComponent<StandingSprite>();
            Assert.IsNotNull(stand, "월드 정렬에 넣으면 서 있는 그림으로도 등록된다");
            Assert.AreEqual(PixelArt.WalkFootUnits, stand.FootLift);
            Assert.AreSame(original, sr.sharedMaterial, "꺼져 있으면 원래 재질");
            Assert.IsFalse(stand.IsStanding);

            TiltView.SetReady(true);
            Assert.IsTrue(stand.IsStanding);
            Assert.AreSame(TiltView.StandingMaterial(PixelArt.WalkFootUnits), sr.sharedMaterial);
            Assert.AreEqual(ShadowCastingMode.TwoSided, sr.shadowCastingMode, "스프라이트는 기본이 그림자 없음이라 켜 줘야 한다");
            Assert.IsTrue(sr.receiveShadows);
            Assert.AreEqual(Quaternion.identity, go.transform.rotation, "트랜스폼은 돌리지 않는다 (셰이더가 세운다)");

            stand.SetTranslucent(true);
            Assert.AreSame(TiltView.StandingMaterial(PixelArt.WalkFootUnits, 0f, true), sr.sharedMaterial);
            Assert.AreEqual(ShadowCastingMode.Off, sr.shadowCastingMode);

            TiltView.SetReady(false);
            Assert.AreSame(original, sr.sharedMaterial, "끄면 원래 재질로");
            Assert.AreEqual(ShadowCastingMode.Off, sr.shadowCastingMode);
        }

        [Test]
        public void StandingSprite_LiftHeight_MovesAlongBillboardUp()
        {
            var root = new GameObject("Ally");
            toDestroy.Add(root);
            var bar = new GameObject("HpBar");
            bar.transform.SetParent(root.transform, false);
            StandingSprite.Attach(bar, liftHeight: 1.4f);
            Assert.AreEqual(new Vector3(0f, 1.4f, 0f), bar.transform.localPosition);
            TiltView.SetReady(true);
            Assert.AreEqual(TiltPreset.Up(TiltPreset.Pitch) * 1.4f, bar.transform.localPosition);
            TiltView.SetReady(false);
            Assert.AreEqual(new Vector3(0f, 1.4f, 0f), bar.transform.localPosition);
        }

        [Test]
        public void StandingSprite_Release_RestoresAndStaysOff_UntilAttachedAgain()
        {
            if (!TiltView.ShaderAvailable) Assert.Ignore($"셰이더 {TiltView.ShaderName} 없음 (컴파일 오류?)");
            TiltView.SetReady(true);
            var go = new GameObject("Prop", typeof(SpriteRenderer));
            toDestroy.Add(go);
            var sr = go.GetComponent<SpriteRenderer>();
            var original = sr.sharedMaterial;
            WorldSorting.Configure(sr, PixelArt.PropFootUnits);
            Assert.AreNotSame(original, sr.sharedMaterial);

            StandingSprite.Release(sr);   // 풀에서 바닥 장식으로 다시 쓰일 때
            Assert.AreSame(original, sr.sharedMaterial);
            Assert.IsFalse(go.GetComponent<StandingSprite>().enabled);

            WorldSorting.Configure(sr, PixelArt.PropFootUnits);   // 다시 서 있는 소품으로
            Assert.IsTrue(go.GetComponent<StandingSprite>().enabled);
            Assert.AreSame(TiltView.StandingMaterial(PixelArt.PropFootUnits), sr.sharedMaterial);
        }

        // ───────────────────────── 틸트일 때 비켜 주는 것들 ─────────────────────────

        [Test]
        public void CameraFollow_Tilt_PlacesCameraBehindAndAbove_AndRestoresTopDown()
        {
            var target = new GameObject("Target");
            toDestroy.Add(target);
            target.transform.position = new Vector3(3f, 4f, 0f);
            var camGo = new GameObject("Cam", typeof(Camera), typeof(CameraFollow));
            toDestroy.Add(camGo);
            var follow = camGo.GetComponent<CameraFollow>();
            follow.Target = target.transform;

            TiltView.SetReady(true);
            follow.SnapToTarget();
            TiltPreset.CameraPose(new Vector3(3f, 4f, 0f), TiltPreset.Pitch, TiltPreset.Distance, out var pos, out var rot);
            Assert.AreEqual(pos.x, camGo.transform.position.x, 1e-4f);
            Assert.AreEqual(pos.y, camGo.transform.position.y, 1e-4f);
            Assert.AreEqual(pos.z, camGo.transform.position.z, 1e-4f);
            Assert.AreEqual(1f, Quaternion.Dot(rot, camGo.transform.rotation), 1e-4f);

            TiltView.SetReady(false);
            follow.SnapToTarget();
            Assert.AreEqual(new Vector3(3f, 4f, -10f), camGo.transform.position);
            Assert.AreEqual(Quaternion.identity, camGo.transform.rotation);
        }

        [Test]
        public void PixelCamera_IsForcedOff_UnderTilt()
        {
            var go = new GameObject("Cam", typeof(Camera));
            toDestroy.Add(go);
            go.GetComponent<Camera>().orthographic = true;
            Hd2dSettings.PixelPerfectOverride = true;
            var pc = go.AddComponent<BattlePixelCamera>();
            Assert.IsTrue(pc.IsEnabled);

            TiltView.SetReady(true);
            pc.Apply(true);
            Assert.IsFalse(pc.IsEnabled, "원근 카메라에서는 픽셀 퍼펙트를 쓸 수 없다");
            Assert.IsFalse(pc.PixelPerfect.enabled);

            TiltView.SetReady(false);
            pc.Apply(true);
            Assert.IsTrue(pc.IsEnabled);
        }

        [Test]
        public void CastShadow_IsHidden_UnderTilt()
        {
            Hd2dSettings.ShadowsOverride = true;
            var go = new GameObject("Body", typeof(SpriteRenderer));
            toDestroy.Add(go);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 4f);
            toDestroy.Add(sr.sprite);
            var shadow = CastShadow.Attach(sr);
            shadow.Sync();
            Assert.IsTrue(shadow.Renderer.enabled);

            TiltView.SetReady(true);
            shadow.Sync();
            Assert.IsFalse(shadow.Renderer.enabled, "3D 빛이 진짜 그림자를 드리우므로 가짜 그림자는 숨긴다");
        }

        [Test]
        public void EnemySpawnRadius_UnderTilt_IsBeyondTheGroundFootprint()
        {
            TiltView.SetReady(true);
            float r = TiltView.SpawnRadius(16f / 9f);
            var foot = TiltView.GroundFootprint(16f / 9f);
            Assert.GreaterOrEqual(r, new Vector2(foot.xMax, foot.yMax).magnitude - 1e-4f);
        }

        // ───────────────────────── 3D 조명 ─────────────────────────

        [Test]
        public void BattleLighting_Tilt_MakesSunAndAmbient_AndRestoresWhenDestroyed()
        {
            Hd2dSettings.LightingOverride = true;
            TiltView.SetReady(true);
            var originalMode = RenderSettings.ambientMode;
            var originalAmbient = RenderSettings.ambientLight;

            var go = new GameObject("Lighting");
            var lighting = go.AddComponent<BattleLighting>();
            lighting.Initialize(CastleTerrain.Plain, TimeOfDay.Dusk);

            Assert.IsTrue(lighting.IsApplied);
            Assert.IsTrue(lighting.IsTilt);
            Assert.IsNull(lighting.PlayerGlow, "틸트에서는 Light2D 를 만들지 않는다");
            Assert.IsNotNull(lighting.Sun);
            Assert.AreEqual(LightType.Directional, lighting.Sun.type);
            Assert.AreEqual(LightShadows.Hard, lighting.Sun.shadows);
            var expected = LightingPreset.Global(CastleTerrain.Plain, TimeOfDay.Dusk);
            Assert.AreEqual(expected.intensity * TiltPreset.SunShare, lighting.Sun.intensity, 1e-4f);
            Assert.AreEqual(expected.color, lighting.Sun.color);
            Assert.AreEqual(AmbientMode.Flat, RenderSettings.ambientMode);
            Assert.AreEqual(expected.color.r * expected.intensity * TiltPreset.AmbientShare, RenderSettings.ambientLight.r, 1e-4f);
            var sunDir = lighting.Sun.transform.forward;
            var want = TiltPreset.SunDirection(ShadowPreset.For(TimeOfDay.Dusk));
            Assert.AreEqual(1f, Vector3.Dot(sunDir, want), 1e-4f, "해는 드리운 그림자와 같은 방향에서");
            Assert.IsNotNull(lighting.PlayerGlow3D);
            Assert.AreEqual(LightType.Point, lighting.PlayerGlow3D.type);

            Object.DestroyImmediate(go);
            Assert.AreEqual(originalMode, RenderSettings.ambientMode, "사라질 때 환경광을 되돌린다");
            Assert.AreEqual(originalAmbient, RenderSettings.ambientLight);
        }

        [Test]
        public void BattleLighting_Tilt_LightingOff_KeepsNeutralSun_WithoutGlow()
        {
            Hd2dSettings.LightingOverride = false;
            Hd2dSettings.ShadowsOverride = false;
            TiltView.SetReady(true);
            var go = new GameObject("Lighting");
            toDestroy.Add(go);
            var lighting = go.AddComponent<BattleLighting>();
            lighting.Initialize(CastleTerrain.Loess, TimeOfDay.Dusk);

            Assert.IsFalse(lighting.IsApplied, "조명 설정은 꺼짐");
            Assert.IsNotNull(lighting.Sun, "틸트에서는 빛이 없으면 모두 검게 나오므로 중립 햇빛을 둔다");
            Assert.AreEqual(Color.white, lighting.Sun.color);
            Assert.AreEqual(TiltPreset.SunShare, lighting.Sun.intensity, 1e-4f);
            Assert.AreEqual(LightShadows.None, lighting.Sun.shadows, "그림자 설정(F8)을 따른다");
            Assert.AreEqual(TiltPreset.AmbientShare, RenderSettings.ambientLight.r, 1e-4f);
            Assert.IsNull(lighting.PlayerGlow3D, "플레이어 빛은 조명 연출에 속한다");

            Hd2dSettings.ShadowsOverride = true;
            lighting.Apply(false);
            Assert.AreEqual(LightShadows.Hard, lighting.Sun.shadows);
        }

        [Test]
        public void BattleLighting_SwitchesBetween2DAnd3D_WhenTiltChanges()
        {
            Hd2dSettings.LightingOverride = true;
            var go = new GameObject("Lighting");
            toDestroy.Add(go);
            var lighting = go.AddComponent<BattleLighting>();
            lighting.Initialize(CastleTerrain.Plain, TimeOfDay.Day);
            Assert.IsNotNull(lighting.PlayerGlow);
            Assert.IsNull(lighting.Sun);

            TiltView.SetReady(true);   // Changed → 다시 적용
            Assert.IsNotNull(lighting.Sun);
            Assert.IsTrue(lighting.IsTilt);

            TiltView.SetReady(false);
            Assert.IsFalse(lighting.IsTilt);
            Assert.IsNull(lighting.Sun);
            Assert.IsNotNull(lighting.PlayerGlow);
        }

        [Test]
        public void Spawner_UnderTilt_Makes3DPointLight_LiftedAlongBillboardUp()
        {
            Hd2dSettings.LightingOverride = true;
            TiltView.SetReady(true);
            var c = ScriptableObject.CreateInstance<TerrainThemeCatalog>();
            toDestroy.Add(c);
            var t = ScriptableObject.CreateInstance<TerrainTheme>();
            toDestroy.Add(t);
            t.terrain = CastleTerrain.Plain;
            t.propsPerChunk = 0f;
            c.themes = new List<TerrainTheme> { t };
            c.banner = new TerrainProp { name = "banner", standing = true, lightColor = Color.red, lightRadius = 2f, lightIntensity = 0.5f, lightHeight = 1.6f, scaleMin = 1f, scaleMax = 1f };
            var castle = ScriptableObject.CreateInstance<CastleData>();
            toDestroy.Add(castle);
            castle.id = "Test";
            castle.terrain = CastleTerrain.Plain;
            var map = TerrainMap.Create(castle, c);
            map.SetCustomChunk(TerrainMap.ChunkOf(Vector2.zero), new List<PropPlacement>
            {
                new PropPlacement { prop = c.banner, position = new Vector2(3f, 2f), scale = 1f, stretchY = 1f, order = TerrainMap.OrderPropBase },
            });

            var follow = new GameObject("Follow");
            toDestroy.Add(follow);
            var go = new GameObject("TerrainProps");
            toDestroy.Add(go);
            var spawner = go.AddComponent<TerrainPropSpawner>();
            spawner.Initialize(map, follow.transform, null);

            Assert.AreEqual(1, spawner.ActiveLightCount);
            var lights = spawner.GetComponentsInChildren<Light>();
            Assert.AreEqual(1, lights.Length, "틸트에서는 3D 점광원");
            Assert.AreEqual(LightType.Point, lights[0].type);
            Assert.AreEqual(2f, lights[0].range, 1e-4f);
            Assert.AreEqual(0.5f * TiltPreset.PointLightScale, lights[0].intensity, 1e-4f);
            var want = new Vector3(3f, 2f, 0f) + TiltPreset.Up(TiltPreset.Pitch) * 1.6f;
            Assert.AreEqual(want.y, lights[0].transform.position.y, 1e-4f, "횃불은 카메라 위쪽으로 올린 자리");
            Assert.AreEqual(want.z, lights[0].transform.position.z, 1e-4f);
        }
    }
}
