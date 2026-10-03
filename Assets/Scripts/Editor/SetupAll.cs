using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>Step 2~7 셋업을 순서대로 모두 실행한다 (씬/프리팹을 처음부터 다시 구성할 때).</summary>
    public static class SetupAll
    {
        [MenuItem("Samkuk/Run All Setup (Step 2-7)")]
        public static void Run()
        {
            Step2Setup.Run();
            Step3Setup.Run();
            Step4Setup.Run();
            Step5Setup.Run();
            Step6Setup.Run();
            Step7Setup.Run();
            Debug.Log("[Samkuk] 전체 셋업(Step 2-7) 완료");
        }
    }
}
