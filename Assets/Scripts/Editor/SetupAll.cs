using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>Step 2~9 셋업을 순서대로 모두 실행한다 (씬/프리팹을 처음부터 다시 구성할 때).</summary>
    public static class SetupAll
    {
        [MenuItem("Samkuk/Run All Setup (Step 2-9 + 타이틀)")]
        public static void Run()
        {
            Step2Setup.Run();
            Step3Setup.Run();
            Step4Setup.Run();
            Step5Setup.Run();
            Step6Setup.Run();
            Step8WeaponsSetup.Run();
            Step8EvolutionSetup.Run();
            Step7Setup.Run();
            Step8EnemiesSetup.Run();
            Step8Setup.Run();
            Step9Setup.Run();
            Step10Setup.Run();
            Step9TitleSetup.Run();
            Debug.Log("[Samkuk] 전체 셋업(Step 2-9 + 타이틀) 완료");
        }
    }
}
