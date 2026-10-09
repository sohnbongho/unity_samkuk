using Samkuk.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 14-2: 전투 씬 Main Camera 에 <see cref="BattlePostFx"/> 를 붙이고 카메라의 포스트 프로세싱을 켠다.
    /// Volume 과 프로필은 실행 중에 코드로 만들므로 에셋은 없다. 멱등: 이미 붙어 있으면 그대로 둔다.
    /// </summary>
    public static class Step14PostFxSetup
    {
        const string ScenePath = "Assets/Scenes/BattleScene.unity";

        [MenuItem("Samkuk/Step 14-2 - Post FX (후처리)")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Samkuk] Step 14-2: Main Camera 를 찾지 못했습니다.");
                return;
            }

            bool added = false;
            if (cam.GetComponent<BattlePostFx>() == null)
            {
                cam.gameObject.AddComponent<BattlePostFx>();
                added = true;
            }
            // 씬에 저장해 두어 실행 전 씬 뷰에서도 후처리가 켜진 상태로 보이게 한다 (실행 중에는 BattlePostFx 가 설정에 따라 다시 정한다)
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(cam);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Samkuk] Step 14-2 완료: 후처리 {(added ? "컴포넌트 추가" : "이미 있음")} + 카메라 포스트 프로세싱 켬");
        }
    }
}
