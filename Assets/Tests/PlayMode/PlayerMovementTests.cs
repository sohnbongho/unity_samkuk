using System.Collections;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    public class PlayerMovementTests : InputTestFixture
    {
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

        Keyboard keyboard;
        GameObject player;
        GameObject camObj;
        GameObject bgObj;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            if (player != null) Object.Destroy(player);
            if (camObj != null) Object.Destroy(camObj);
            if (bgObj != null) Object.Destroy(bgObj);
            base.TearDown();
        }

        PlayerController SpawnPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, "Player 프리팹이 없습니다. Samkuk > Step 2 를 먼저 실행하세요.");
            player = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            return player.GetComponent<PlayerController>();
        }

        [UnityTest]
        public IEnumerator Player_MovesRight_WhenDPressed()
        {
            var pc = SpawnPlayer();
            yield return null;

            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.5f);

            Assert.Greater(player.transform.position.x, 1f, "D 키로 오른쪽 이동해야 함");
            Assert.AreEqual(0f, player.transform.position.y, 0.01f);
            Assert.AreEqual(Vector2.right, pc.FacingDirection);
        }

        [UnityTest]
        public IEnumerator Player_DiagonalSpeed_IsNotFaster()
        {
            var pc = SpawnPlayer();
            yield return null;

            Press(keyboard.dKey);
            Press(keyboard.wKey);
            yield return new WaitForSeconds(0.5f);

            Assert.LessOrEqual(pc.MoveInput.magnitude, 1.001f, "대각선 입력도 길이 1 이하");
            var rb = player.GetComponent<Rigidbody2D>();
            Assert.AreEqual(pc.MoveSpeed, rb.linearVelocity.magnitude, 0.05f);
        }

        [UnityTest]
        public IEnumerator Player_Stops_WhenKeyReleased()
        {
            SpawnPlayer();
            yield return null;

            Press(keyboard.aKey);
            yield return new WaitForSeconds(0.3f);
            Release(keyboard.aKey);
            yield return new WaitForSeconds(0.2f);

            float x = player.transform.position.x;
            yield return new WaitForSeconds(0.3f);

            Assert.Less(x, -0.5f, "A 키로 왼쪽 이동해야 함");
            Assert.AreEqual(x, player.transform.position.x, 0.01f, "키를 떼면 멈춰야 함");
        }

        [UnityTest]
        public IEnumerator Camera_FollowsPlayer()
        {
            var pc = SpawnPlayer();
            camObj = new GameObject("TestCam", typeof(Camera), typeof(CameraFollow));
            var follow = camObj.GetComponent<CameraFollow>();
            follow.Target = player.transform;
            follow.SnapToTarget();
            yield return null;

            Press(keyboard.dKey);
            yield return new WaitForSeconds(1f);

            Vector3 cp = camObj.transform.position;
            Assert.Greater(cp.x, 1.5f, "카메라가 플레이어를 따라가야 함");
            Assert.AreEqual(-10f, cp.z, 0.001f);
            Assert.AreEqual(player.transform.position.x, cp.x, 1.0f, "카메라 지연이 너무 크지 않아야 함");
        }

        [UnityTest]
        public IEnumerator Background_SnapsToTileGrid()
        {
            camObj = new GameObject("TestCam", typeof(Camera));
            camObj.transform.position = new Vector3(5.3f, -9.9f, -10f);

            bgObj = new GameObject("TestBg", typeof(SpriteRenderer), typeof(InfiniteBackground));
            var sr = bgObj.GetComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Background.png");
            bgObj.GetComponent<InfiniteBackground>().FollowTarget = camObj.transform;
            yield return null;
            yield return null;

            float tile = sr.sprite.rect.width / sr.sprite.pixelsPerUnit; // 4
            Vector3 p = bgObj.transform.position;
            Assert.AreEqual(0f, Mathf.Repeat(p.x, tile), 0.001f);
            Assert.AreEqual(0f, Mathf.Repeat(p.y, tile), 0.001f);
            Assert.AreEqual(4f, p.x, 0.001f);   // round(5.3/4)*4
            Assert.AreEqual(-8f, p.y, 0.001f);  // round(-9.9/4)*4
        }
    }
}
