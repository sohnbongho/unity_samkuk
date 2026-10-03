using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Heroes;
using Samkuk.Player;
using Samkuk.Skills;
using Samkuk.Stages;
using Samkuk.Upgrades;
using Samkuk.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 10-2: 효과음. 게임 씬에 SfxHooks(이벤트 → 효과음 연결)를 추가한다.
    /// AudioManager는 처음 소리가 날 때 스스로 만들어지므로 씬에 둘 필요가 없다.
    /// </summary>
    public static class Step10Setup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Samkuk/Step 10-2 - Setup Sound Effects")]
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

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Samkuk] Step 10-2 setup 완료 (효과음 연결)");
        }
    }
}
