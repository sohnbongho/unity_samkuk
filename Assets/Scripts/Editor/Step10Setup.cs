using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Feedback;
using Samkuk.Heroes;
using Samkuk.Player;
using Samkuk.Skills;
using Samkuk.Stages;
using Samkuk.UI;
using Samkuk.Upgrades;
using Samkuk.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 10-1/10-2: 게임 씬에 효과음 연결(SfxHooks)과 타격감 연출(화면 흔들림, 입자, 피격 번쩍임,
    /// FeedbackHooks)을 추가한다. AudioManager는 처음 소리가 날 때 스스로 만들어지므로 씬에 둘 필요가 없다.
    /// </summary>
    public static class Step10Setup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Samkuk/Step 10 - Setup Sound & Feedback")]
        public static void Run()
        {
            // 주의: OpenScene(Single) 이후에 컴포넌트를 찾는다.
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            var systems = GameObject.Find("GameSystems");
            if (player == null || systems == null)
            {
                Debug.LogError("[Samkuk] Player / GameSystems 가 씬에 없습니다. Step 2~9 를 먼저 실행하세요.");
                return;
            }

            var old = systems.GetComponent<SfxHooks>();
            if (old != null) Object.DestroyImmediate(old);

            var hooks = systems.AddComponent<SfxHooks>();
            var so = new SerializedObject(hooks);
            so.FindProperty("playerHealth").objectReferenceValue = player.GetComponent<PlayerHealth>();
            so.FindProperty("experience").objectReferenceValue = player.GetComponent<PlayerExperience>();
            so.FindProperty("weapons").objectReferenceValue = player.GetComponent<WeaponController>();
            so.FindProperty("skills").objectReferenceValue = player.GetComponent<SkillController>();
            so.FindProperty("stage").objectReferenceValue = systems.GetComponent<StageController>();
            so.FindProperty("game").objectReferenceValue = systems.GetComponent<GameManager>();
            so.FindProperty("levelUp").objectReferenceValue = systems.GetComponent<LevelUpController>();
            so.FindProperty("hero").objectReferenceValue = systems.GetComponent<HeroSelectController>();
            so.ApplyModifiedPropertiesWithoutUndo();

            if (hooks.PlayerHealth == null || hooks.Weapons == null || hooks.Game == null)
                Debug.LogError("[Samkuk] SfxHooks 참조 연결 실패");

            BuildFeedback(player, systems);

            // 중국풍 UI 스킨 (HUD 아래의 카드/버튼/패널/바에 테마를 입힌다)
            var hud = GameObject.Find("HUD");
            if (hud != null && hud.GetComponent<UiSkin>() == null) hud.AddComponent<UiSkin>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Samkuk] Step 10 setup 완료 (효과음 + 타격감 연출)");
        }

        // ───────────────────────── 10-1 타격감 ─────────────────────────

        static void BuildFeedback(GameObject player, GameObject systems)
        {
            var hud = GameObject.Find("HUD");
            var follow = Object.FindAnyObjectByType<CameraFollow>();
            if (hud == null || follow == null)
            {
                Debug.LogError("[Samkuk] HUD / CameraFollow 가 씬에 없습니다. Step 2~9 를 먼저 실행하세요.");
                return;
            }

            // 화면 흔들림: 카메라에 붙인다 (CameraFollow 가 Offset 을 적용)
            var shake = follow.GetComponent<ScreenShake>();
            if (shake == null) shake = follow.gameObject.AddComponent<ScreenShake>();

            // 피격 번쩍임: HUD 의 전체 화면 붉은 이미지 (다른 UI 뒤에 그려지도록 맨 앞 형제로)
            var oldFlash = hud.transform.Find("DamageFlash");
            if (oldFlash != null) Object.DestroyImmediate(oldFlash.gameObject);
            var flashGo = new GameObject("DamageFlash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(hud.transform, false);
            var rt = (RectTransform)flashGo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = flashGo.GetComponent<Image>();
            img.color = new Color(0.9f, 0.05f, 0.05f, 0f);
            img.raycastTarget = false;
            flashGo.transform.SetAsFirstSibling();
            var flash = flashGo.AddComponent<DamageFlashView>();
            flash.Image = img;

            // 입자 + 이벤트 연결
            var burst = systems.GetComponent<BurstFx>();
            if (burst == null) burst = systems.AddComponent<BurstFx>();

            var old = systems.GetComponent<FeedbackHooks>();
            if (old != null) Object.DestroyImmediate(old);
            var hooks = systems.AddComponent<FeedbackHooks>();
            var so = new SerializedObject(hooks);
            so.FindProperty("playerHealth").objectReferenceValue = player.GetComponent<PlayerHealth>();
            so.FindProperty("experience").objectReferenceValue = player.GetComponent<PlayerExperience>();
            so.FindProperty("weapons").objectReferenceValue = player.GetComponent<WeaponController>();
            so.FindProperty("skills").objectReferenceValue = player.GetComponent<SkillController>();
            so.FindProperty("stage").objectReferenceValue = systems.GetComponent<StageController>();
            so.FindProperty("shake").objectReferenceValue = shake;
            so.FindProperty("burst").objectReferenceValue = burst;
            so.FindProperty("flash").objectReferenceValue = flash;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (hooks.Burst == null || hooks.Shake == null || hooks.Flash == null || hooks.PlayerHealth == null)
                Debug.LogError("[Samkuk] FeedbackHooks 참조 연결 실패");
        }
    }
}
