using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Data;
using Samkuk.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    /// <summary>장수 걷기 애니메이션: 시트 자르기, 방향 선택, 프레임 재생.</summary>
    public class PlayerAnimatorTests : InputTestFixture
    {
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        const string HeroDir = "Assets/ScriptableObjects/Heroes";

        Keyboard keyboard;
        GameObject player;
        readonly List<Object> toDestroy = new List<Object>();

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            if (player != null) Object.Destroy(player);
            foreach (var o in toDestroy) if (o != null) Object.Destroy(o);
            toDestroy.Clear();
            base.TearDown();
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        /// <summary>8x8 텍스처 = 4x4칸, 한 칸 2x2 픽셀.</summary>
        Texture2D MakeSheet(string name = "TestSheet")
        {
            var tex = new Texture2D(8, 8) { name = name };
            toDestroy.Add(tex);
            return tex;
        }

        HeroData MakeHero(Texture2D sheet)
        {
            var h = ScriptableObject.CreateInstance<HeroData>();
            h.displayName = "테스트";
            h.tint = Color.red;
            h.walkSheet = sheet;
            toDestroy.Add(h);
            return h;
        }

        // ───────────────────────── 시트 자르기 ─────────────────────────

        [Test]
        public void SpriteSet_SlicesSheetIntoFourByFour_RowsAreDownUpLeftRight()
        {
            var set = HeroSpriteSet.Get(MakeSheet(), 96f);

            // 행 0 = 아래(시트 맨 위), 열 = 프레임. 스프라이트 y 는 아래에서 위로 센다.
            Assert.AreEqual(new Rect(0, 6, 2, 2), set.Get(FacingDir.Down, 0).rect);
            Assert.AreEqual(new Rect(2, 4, 2, 2), set.Get(FacingDir.Up, 1).rect);
            Assert.AreEqual(new Rect(4, 2, 2, 2), set.Get(FacingDir.Left, 2).rect);
            Assert.AreEqual(new Rect(6, 0, 2, 2), set.Get(FacingDir.Right, 3).rect);

            var all = new HashSet<Sprite>();
            foreach (FacingDir d in System.Enum.GetValues(typeof(FacingDir)))
                for (int f = 0; f < 4; f++) all.Add(set.Get(d, f));
            Assert.AreEqual(16, all.Count, "칸마다 다른 스프라이트");
        }

        [Test]
        public void SpriteSet_UsesPixelsPerUnit_AndWrapsFrameNumbers()
        {
            var set = HeroSpriteSet.Get(MakeSheet(), 2f);
            Assert.AreEqual(2f, set.Get(FacingDir.Down, 0).pixelsPerUnit);
            Assert.AreEqual(1f, set.Get(FacingDir.Down, 0).bounds.size.x, 0.001f, "2픽셀 / PPU 2 = 1유닛");
            Assert.AreSame(set.Get(FacingDir.Down, 0), set.Get(FacingDir.Down, 4));
            Assert.AreSame(set.Get(FacingDir.Down, 3), set.Get(FacingDir.Down, -1));
        }

        [Test]
        public void SpriteSet_IsCachedPerTexture_AndNullSheetGivesNull()
        {
            var tex = MakeSheet();
            Assert.AreSame(HeroSpriteSet.Get(tex, 96f), HeroSpriteSet.Get(tex, 96f));
            Assert.IsNull(HeroSpriteSet.Get(null, 96f));
        }

        // ───────────────────────── 방향 선택 ─────────────────────────

        [Test]
        public void PickDirection_FollowsTheDominantAxis()
        {
            Assert.AreEqual(FacingDir.Right, PlayerAnimator.PickDirection(Vector2.right, FacingDir.Down));
            Assert.AreEqual(FacingDir.Left, PlayerAnimator.PickDirection(Vector2.left, FacingDir.Down));
            Assert.AreEqual(FacingDir.Up, PlayerAnimator.PickDirection(Vector2.up, FacingDir.Right));
            Assert.AreEqual(FacingDir.Down, PlayerAnimator.PickDirection(Vector2.down, FacingDir.Right));
        }

        [Test]
        public void PickDirection_KeepsCurrentAxisOnNearDiagonals_AndStopsKeepDirection()
        {
            // 대각선에서 양 축이 비슷하면 지금 방향을 유지 (떨림 방지)
            Assert.AreEqual(FacingDir.Right, PlayerAnimator.PickDirection(new Vector2(1f, 1.1f), FacingDir.Right));
            Assert.AreEqual(FacingDir.Up, PlayerAnimator.PickDirection(new Vector2(1.1f, 1f), FacingDir.Up));
            // 다른 축이 확실히 크면 바꾼다
            Assert.AreEqual(FacingDir.Up, PlayerAnimator.PickDirection(new Vector2(1f, 1.5f), FacingDir.Right));
            Assert.AreEqual(FacingDir.Right, PlayerAnimator.PickDirection(new Vector2(1.5f, 1f), FacingDir.Up));
            // 입력이 없으면 그대로
            Assert.AreEqual(FacingDir.Left, PlayerAnimator.PickDirection(Vector2.zero, FacingDir.Left));
        }

        // ───────────────────────── 장수 적용 ─────────────────────────

        [Test]
        public void SetHero_ShowsStandingFrontFrame_AndRestoresFallbackWhenSheetIsRemoved()
        {
            var go = new GameObject("AnimTest");
            toDestroy.Add(go);
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            var sr = bodyGo.AddComponent<SpriteRenderer>();
            var fallbackTex = MakeSheet("Fallback");
            var fallback = Sprite.Create(fallbackTex, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            toDestroy.Add(fallback);
            sr.sprite = fallback;
            var anim = go.AddComponent<PlayerAnimator>();

            Assert.IsFalse(anim.HasSheet);

            var sheet = MakeSheet("Walk");
            anim.SetHero(MakeHero(sheet));
            Assert.IsTrue(anim.HasSheet);
            Assert.AreEqual(FacingDir.Down, anim.Direction);
            Assert.AreSame(HeroSpriteSet.Get(sheet, 96f).Get(FacingDir.Down, 0), sr.sprite);
            Assert.IsFalse(sr.flipX);

            anim.SetHero(MakeHero(null));
            Assert.IsFalse(anim.HasSheet);
            Assert.AreSame(fallback, sr.sprite, "시트가 없는 장수는 기본 스프라이트로 복원");
        }

        // ───────────────────────── 실제 플레이어 ─────────────────────────

        PlayerAnimator SpawnPlayerWithSheet(out SpriteRenderer sr, out Texture2D sheet)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, "Player 프리팹이 없습니다. Samkuk > Step 2 를 먼저 실행하세요.");
            Assert.IsNotNull(prefab.GetComponent<PlayerAnimator>(), "Player 프리팹에 PlayerAnimator 가 없습니다. Samkuk > Step 10-6 을 실행하세요.");

            player = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            sr = player.GetComponentInChildren<SpriteRenderer>();
            sheet = MakeSheet("PlayerWalk");
            var anim = player.GetComponent<PlayerAnimator>();
            anim.SetHero(MakeHero(sheet));
            return anim;
        }

        [UnityTest]
        public IEnumerator Player_TurnsAndWalks_FollowingMoveInput()
        {
            var anim = SpawnPlayerWithSheet(out var sr, out var sheet);
            var set = HeroSpriteSet.Get(sheet, 96f);
            yield return null;

            // 서 있을 때: 정면, 0번 프레임
            Assert.AreEqual(FacingDir.Down, anim.Direction);
            Assert.AreEqual(0, anim.Frame);

            // 오른쪽으로 걷기: 방향 오른쪽, 프레임이 돌아가고, 좌우 반전은 쓰지 않음
            Press(keyboard.dKey);
            yield return null;
            var frames = new HashSet<int>();
            float until = Time.time + 0.7f;
            while (Time.time < until)
            {
                yield return null;
                frames.Add(anim.Frame);
                Assert.AreEqual(FacingDir.Right, anim.Direction);
                Assert.IsFalse(sr.flipX, "걷기 시트를 쓰면 좌우 반전은 하지 않음");
                Assert.AreSame(set.Get(FacingDir.Right, anim.Frame), sr.sprite);
            }
            Assert.GreaterOrEqual(frames.Count, 3, "걷는 동안 프레임이 돌아가야 함: " + string.Join(",", frames));

            // 멈추면 정지 자세, 보던 방향은 유지
            Release(keyboard.dKey);
            yield return null;
            yield return null;
            Assert.AreEqual(0, anim.Frame);
            Assert.AreEqual(FacingDir.Right, anim.Direction);

            // 위 / 왼쪽 / 아래
            Press(keyboard.wKey);
            yield return null; yield return null;
            Assert.AreEqual(FacingDir.Up, anim.Direction);
            Release(keyboard.wKey);

            Press(keyboard.aKey);
            yield return null; yield return null;
            Assert.AreEqual(FacingDir.Left, anim.Direction);
            Release(keyboard.aKey);

            Press(keyboard.sKey);
            yield return null; yield return null;
            Assert.AreEqual(FacingDir.Down, anim.Direction);
        }

        [UnityTest]
        public IEnumerator Player_WithoutSheet_StillFlipsLikeBefore()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, "Player 프리팹이 없습니다. Samkuk > Step 2 를 먼저 실행하세요.");
            player = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            var sr = player.GetComponentInChildren<SpriteRenderer>();
            yield return null;

            Press(keyboard.aKey);
            yield return null; yield return null;
            Assert.IsTrue(sr.flipX, "시트가 없으면 예전처럼 왼쪽 이동 시 반전");
            Release(keyboard.aKey);
            Press(keyboard.dKey);
            yield return null; yield return null;
            Assert.IsFalse(sr.flipX);
        }

        // ───────────────────────── 실제 에셋 ─────────────────────────

        [Test]
        public void HeroAssets_HaveWalkSheets_WithFourByFourCells()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:HeroData", new[] { HeroDir }))
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroData>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(hero.walkSheet, $"{hero.displayName}: 걷기 시트가 연결되지 않았습니다. Samkuk > Step 10-6 을 실행하세요.");
                Assert.AreEqual(hero.walkSheet.width, hero.walkSheet.height, $"{hero.displayName}: 정사각형(4x4칸) 시트여야 함");
                Assert.AreEqual(0, hero.walkSheet.width % HeroSpriteSet.Columns, $"{hero.displayName}: 칸 크기가 정수가 아님");
                Assert.Greater(hero.walkPixelsPerUnit, 0f);
                count++;
            }
            Assert.AreEqual(5, count, "장수 5명");
        }
    }
}
