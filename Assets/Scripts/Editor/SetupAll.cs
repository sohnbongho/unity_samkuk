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
            Step7Setup.Run();
            Step8EnemiesSetup.Run();
            Step8Setup.Run();
            Step8EvolutionSetup.Run(); // 장수 시작 무기도 진화하므로 Step 8(장수) 이후
            Step10ThemeSetup.Run();
            Step9Setup.Run();
            Step10Setup.Run();
            Step9TitleSetup.Run();
            Step10BalanceSetup.Run();
            Step10PortraitSetup.Run(); // 장수 에셋이 만들어진 뒤
            Step10WalkSetup.Run();     // Player 프리팹이 만들어진 뒤
            Step10EnemyWalkSetup.Run(); // 적 에셋이 만들어진 뒤
            Step10AllySetup.Run();      // 장수 선택 컨트롤러(Step 8)가 만들어진 뒤
            Step12CastleSetup.Run();    // 내정 성 46곳 (다른 Step 과 독립)
            Step12TerrainSetup.Run();   // 전투 맵 지형 테마 (그림은 tools/terrain_art/generate.ps1)
            Step12StrategySetup.Run();  // 내정 화면 씬 (성 에셋과 Step 9-2 의 빌드 설정 뒤)
            Step13MapEditorSetup.Run(); // 맵 편집기 씬 (성 에셋과 지형 테마 뒤)
            Step14PostFxSetup.Run();    // 전투 카메라 후처리 (HD-2D)
            Step14PixelSetup.Run();     // 도트 규격: Pixel Perfect Camera + 걷기 시트 PPU 이전 (시트 연결 10-6/10-7 뒤)
            Debug.Log("[Samkuk] 전체 셋업(Step 2-9 + 타이틀) 완료");
        }
    }
}
