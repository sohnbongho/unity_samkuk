using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    /// <summary>
    /// 무기 휘두르기(Step 10-9): 호를 그리는 각도/타격 시점의 순수 규칙(SwingMotion)과,
    /// 베기 무기가 가장 가까운 적 쪽으로 휘둘러 호의 중간에 피해를 주는지 확인한다.
    /// </summary>
    public class WeaponSwingTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";

        GameObject root;
        GameObject playerGo;
        GameObject camGo;
        EnemyManager manager;
        EnemySpawner spawner;
        WeaponController wc;
        EnemyData still;
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            Assert.IsNotNull(enemyPrefab, "Enemy 프리팹이 없습니다. Samkuk > Step 3 를 먼저 실행하세요.");

            playerGo = new GameObject("TestPlayer");
            playerGo.AddComponent<PlayerHealth>();

            camGo = new GameObject("TestCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.aspect = 16f / 9f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            root = new GameObject("TestSystems");
            root.SetActive(false);
            manager = root.AddComponent<EnemyManager>();
            spawner = root.AddComponent<EnemySpawner>();
            manager.Target = playerGo.transform;
            spawner.Manager = manager;
            spawner.Cam = cam;
            spawner.EnemyPrefab = enemyPrefab;
            spawner.autoSpawn = false;
            root.SetActive(true);

            wc = playerGo.AddComponent<WeaponController>();
            wc.EnemyManager = manager;

            still = ScriptableObject.CreateInstance<EnemyData>();
            still.maxHp = 10; still.moveSpeed = 0f; still.contactDamage = 0; still.colliderRadius = 0.3f;
            toDestroy.Add(still);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.Destroy(root);
            if (playerGo != null) Object.Destroy(playerGo);
            if (camGo != null) Object.Destroy(camGo);
            foreach (var o in toDestroy) if (o != null) Object.Destroy(o);
        }

        WeaponData MakeSlash(float cooldown, float swingDuration = 0f, int count = 1)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.type = WeaponType.Slash; w.displayName = "검 베기";
            w.damage = 20f; w.cooldown = cooldown; w.range = 2.4f; w.count = count; w.duration = 0.1f;
            w.swingDuration = swingDuration;
            toDestroy.Add(w);
            return w;
        }

        Sprite MakeSprite()
        {
            var tex = new Texture2D(4, 8, TextureFormat.RGBA32, false);
            var s = Sprite.Create(tex, new Rect(0, 0, 4, 8), new Vector2(0.5f, 0f), 32f);
            toDestroy.Add(tex); toDestroy.Add(s);
            return s;
        }

        // ───────────────────────── SwingMotion (순수 규칙) ─────────────────────────

        [Test]
        public void SwingMotion_StartsAtOneEndOfTheArc_AndEndsAtTheOther()
        {
            var m = new SwingMotion();
            m.Start(0f, 120f, 1f);

            Assert.IsTrue(m.Active);
            Assert.IsFalse(m.FacesLeft);
            Assert.AreEqual(60f, m.Angle, 0.01f, "오른쪽을 향하면 위(+60)에서 시작");
            m.Advance(1f);
            Assert.AreEqual(-60f, m.Angle, 0.01f, "아래(-60)에서 끝");
            Assert.IsFalse(m.Active);
        }

        [Test]
        public void SwingMotion_FacingLeft_AlsoSweepsTopToBottom_AndFlips()
        {
            var m = new SwingMotion();
            m.Start(180f, 120f, 1f);

            Assert.IsTrue(m.FacesLeft);
            Assert.AreEqual(120f, m.Angle, 0.01f, "왼쪽을 향하면 위(120)에서 시작");
            m.Advance(1f);
            Assert.AreEqual(240f, m.Angle, 0.01f, "아래(240)에서 끝");
        }

        [Test]
        public void SwingMotion_ReportsTheHitOnce_AtTheHitFraction()
        {
            var m = new SwingMotion();
            m.Start(0f, 120f, 1f);

            Assert.IsFalse(m.Advance(SwingMotion.HitFraction - 0.05f), "타격 시점 전");
            Assert.IsTrue(m.Advance(0.1f), "타격 시점을 지나는 틱에 한 번");
            Assert.IsFalse(m.Advance(0.1f), "다시 알리지 않음");
            Assert.IsTrue(m.Active, "아직 휘두르는 중");
            Assert.IsFalse(m.Advance(1f));
            Assert.IsFalse(m.Active);
        }

        [Test]
        public void SwingMotion_ZeroValues_UseDefaults()
        {
            var m = new SwingMotion();
            m.Start(0f, 0f, 0f);

            Assert.AreEqual(SwingMotion.DefaultArcDegrees * 0.5f, m.Angle, 0.01f);
            m.Advance(SwingMotion.DefaultDuration - 0.01f);
            Assert.IsTrue(m.Active);
            m.Advance(0.02f);
            Assert.IsFalse(m.Active);
        }

        [Test]
        public void SwingMotion_Stop_EndsTheSwing_WithoutAHit()
        {
            var m = new SwingMotion();
            m.Start(0f, 120f, 1f);
            m.Stop();
            Assert.IsFalse(m.Active);
            Assert.IsFalse(m.Advance(1f));
        }

        // ───────────────────────── SlashWeapon ─────────────────────────

        [UnityTest]
        public IEnumerator Slash_SwingsTowardTheNearestEnemy_EvenAbove()
        {
            // 예전 좌/우 고정 베기(원 중심 (±1.2, 0), 반지름 1.44)는 (0, 1.5) 를 못 맞췄다
            yield return null;
            var above = spawner.SpawnAt(still, new Vector2(0f, 1.5f));
            wc.AddWeapon(MakeSlash(cooldown: 0.5f));

            yield return new WaitForSeconds(1f);

            Assert.IsFalse(above.Alive, "가장 가까운 적 쪽으로 휘둘러 위의 적을 맞춘다");
        }

        [UnityTest]
        public IEnumerator Slash_DamageLandsMidSwing_NotAtTheStart()
        {
            yield return null;
            var e = spawner.SpawnAt(still, new Vector2(1.5f, 0f));
            wc.AddWeapon(MakeSlash(cooldown: 5f, swingDuration: 0.6f)); // 타격은 0.24초 지점

            yield return null;
            yield return null;
            Assert.IsTrue(e.Alive, "휘두르기 시작 직후에는 아직 맞지 않음");
            Assert.AreEqual(10f, e.Hp, 0.001f);

            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(e.Alive, "호의 중간에서 피해");
        }

        [UnityTest]
        public IEnumerator Slash_CountTwo_AlsoHitsTheOppositeSide()
        {
            yield return null;
            var right = spawner.SpawnAt(still, new Vector2(1.5f, 0f));
            var left = spawner.SpawnAt(still, new Vector2(-1.5f, 0f));
            wc.AddWeapon(MakeSlash(cooldown: 0.5f, count: 2));

            yield return new WaitForSeconds(1f);

            Assert.IsFalse(right.Alive);
            Assert.IsFalse(left.Alive, "count 2: 반대편도 함께");
        }

        [UnityTest]
        public IEnumerator Slash_HeldSprite_ShowsWhileSwinging_AndHidesAfter()
        {
            yield return null;
            var data = MakeSlash(cooldown: 5f, swingDuration: 0.4f);
            data.heldSprite = MakeSprite();
            var slash = wc.AddWeapon(data) as SlashWeapon;
            Assert.IsNotNull(slash);

            yield return null; // 첫 Update 에서 바로 휘두른다(쿨다운이 차 있음)
            Assert.IsTrue(slash.IsSwinging);
            var held = slash.HeldRenderer(0);
            Assert.IsNotNull(held, "들고 휘두르는 그림 렌더러");
            Assert.IsTrue(held.enabled, "휘두르는 동안 보임");
            Assert.AreEqual(SlashWeapon.HandHeight, held.transform.position.y - playerGo.transform.position.y, 0.001f, "손 높이에서 돈다");

            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(slash.IsSwinging);
            Assert.IsFalse(held.enabled, "끝나면 숨김");
        }

        [UnityTest]
        public IEnumerator Slash_WithoutHeldSprite_StillSwingsAndHits()
        {
            yield return null;
            var e = spawner.SpawnAt(still, new Vector2(1.5f, 0f));
            var slash = wc.AddWeapon(MakeSlash(cooldown: 5f)) as SlashWeapon;

            yield return null;
            Assert.IsTrue(slash.IsSwinging);
            Assert.IsNull(slash.HeldRenderer(0), "그림이 없으면 렌더러도 없음");

            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(e.Alive);
        }

        [UnityTest]
        public IEnumerator Slash_Trail_IsAHalfArc_ThatFliesForward_AndFades()
        {
            yield return null;
            spawner.SpawnAt(still, new Vector2(0f, 1.5f));
            var data = MakeSlash(cooldown: 5f, swingDuration: 0.2f); // 타격 0.08초
            data.duration = 0.5f;                                     // 검기 0.5초
            var slash = wc.AddWeapon(data) as SlashWeapon;

            yield return new WaitForSeconds(0.15f);
            var trail = slash.TrailRenderer(0);
            Assert.IsNotNull(trail);
            Assert.IsTrue(trail.enabled, "타격 뒤 검기가 보임");
            Assert.AreSame(SlashArcSprite.Get(), trail.sprite, "코드로 만든 반원 검기");
            Assert.AreEqual(90f, trail.transform.rotation.eulerAngles.z, 0.5f, "위쪽 적을 향해 돌아감");
            float y0 = trail.transform.position.y;
            float a0 = trail.color.a;

            yield return new WaitForSeconds(0.15f);
            Assert.Greater(trail.transform.position.y, y0, "앞(위)으로 날아감");
            Assert.Less(trail.color.a, a0, "점점 사라짐");

            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(trail.enabled, "끝나면 숨김");
        }

        [UnityTest]
        public IEnumerator Slash_AsksTheOwnerToLookAtTheSwing()
        {
            yield return null;
            var looker = playerGo.AddComponent<FakeLook>();
            spawner.SpawnAt(still, new Vector2(0f, 1.5f));
            wc.AddWeapon(MakeSlash(cooldown: 5f, swingDuration: 0.3f));

            yield return null;
            Assert.AreEqual(1, looker.Calls, "휘두르기 시작에 한 번");
            Assert.Greater(looker.Direction.y, 0.9f, "위의 적 쪽");
            Assert.AreEqual(0.3f + SlashWeapon.LookHoldSeconds, looker.Seconds, 0.001f, "휘두르는 동안 + 잠깐 더");
        }

        [Test]
        public void PlayerAnimator_DirectionOf_PicksTheNearestOfFour()
        {
            Assert.AreEqual(FacingDir.Right, PlayerAnimator.DirectionOf(new Vector2(1f, 0.5f)));
            Assert.AreEqual(FacingDir.Left, PlayerAnimator.DirectionOf(new Vector2(-1f, -0.5f)));
            Assert.AreEqual(FacingDir.Up, PlayerAnimator.DirectionOf(new Vector2(0.2f, 1f)));
            Assert.AreEqual(FacingDir.Down, PlayerAnimator.DirectionOf(new Vector2(0.2f, -1f)));
        }

        class FakeLook : MonoBehaviour, ILookOverride
        {
            public int Calls;
            public Vector2 Direction;
            public float Seconds;
            public void Look(Vector2 direction, float seconds) { Calls++; Direction = direction.normalized; Seconds = seconds; }
        }

        [UnityTest]
        public IEnumerator Slash_Disabled_StopsTheSwing_AndHidesTheSprite()
        {
            yield return null;
            var data = MakeSlash(cooldown: 5f, swingDuration: 1f);
            data.heldSprite = MakeSprite();
            var slash = wc.AddWeapon(data) as SlashWeapon;

            yield return null;
            Assert.IsTrue(slash.IsSwinging);
            slash.gameObject.SetActive(false);

            Assert.IsFalse(slash.IsSwinging, "꺼지면 휘두르기를 끊음(아군이 쓰러질 때)");
            Assert.IsFalse(slash.HeldRenderer(0).enabled);
        }
    }
}
