using Samkuk.Core;
using Samkuk.Data;
using Samkuk.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 14-3: 도트 규격(PPU 32, 640x360). ① 전투 씬 Main Camera 에 Pixel Perfect Camera + <see cref="BattlePixelCamera"/> 를 붙이고
    /// ② 도트 규격(칸 48) 걷기 시트가 연결된 장수/적의 <c>walkPixelsPerUnit</c> 을 32 로 옮긴다(예전 96칸 시트는 그대로).
    /// 그림 자체는 <c>tools/hero_art/generate.ps1</c>, <c>tools/terrain_art/generate.ps1</c> 로 만들고 임포터가 PPU/필터를 맞춘다.
    /// 멱등: 이미 붙어 있거나 값이 같으면 건드리지 않는다. 시트 연결(Step 10-6/10-7) 뒤에 실행한다.
    /// </summary>
    public static class Step14PixelSetup
    {
        const string ScenePath = "Assets/Scenes/BattleScene.unity";
        const string HeroDir = "Assets/ScriptableObjects/Heroes";
        const string EnemyDir = "Assets/ScriptableObjects/Enemies";

        [MenuItem("Samkuk/Step 14-3 - Pixel Art (도트 규격)")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Samkuk] Step 14-3: Main Camera 를 찾지 못했습니다.");
                return;
            }

            bool added = false;
            var ppc = cam.GetComponent<PixelPerfectCamera>();
            if (ppc == null) { ppc = cam.gameObject.AddComponent<PixelPerfectCamera>(); added = true; }
            BattlePixelCamera.Configure(ppc);
            if (cam.GetComponent<BattlePixelCamera>() == null) { cam.gameObject.AddComponent<BattlePixelCamera>(); added = true; }
            EditorUtility.SetDirty(ppc);
            EditorUtility.SetDirty(cam);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 씬 저장 뒤에 에셋을 다룬다 (씬 전환이 로드된 에셋 참조를 무효화할 수 있음)
            int migrated = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:HeroData", new[] { HeroDir }))
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroData>(AssetDatabase.GUIDToAssetPath(guid));
                if (hero != null && hero.walkSheet != null && PixelArt.IsPixelWalkSheet(hero.walkSheet.width) && hero.walkPixelsPerUnit != PixelArt.PPU)
                {
                    hero.walkPixelsPerUnit = PixelArt.PPU;
                    EditorUtility.SetDirty(hero);
                    migrated++;
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyData", new[] { EnemyDir }))
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
                if (enemy != null && enemy.walkSheet != null && PixelArt.IsPixelWalkSheet(enemy.walkSheet.width) && enemy.walkPixelsPerUnit != PixelArt.PPU)
                {
                    enemy.walkPixelsPerUnit = PixelArt.PPU;
                    EditorUtility.SetDirty(enemy);
                    migrated++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Samkuk] Step 14-3 완료: Pixel Perfect Camera {(added ? "추가" : "이미 있음")} (PPU {PixelArt.PPU}, {PixelArt.RefWidth}x{PixelArt.RefHeight}), 걷기 시트 PPU 이전 {migrated}개");
        }
    }
}
