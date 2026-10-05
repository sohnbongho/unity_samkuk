using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 11: Windows 빌드. 친구들과 나눠 보기 위한 테스트 빌드를 한 번에 만든다:
    /// 빌드 설정 확인 → 빌드 → 안내문(README.txt) 추가 → 압축(zip). 결과는 프로젝트의 <c>Builds/</c> 폴더(git 제외).
    ///   메뉴 Samkuk > Build > Windows - 친구 공유용  : 릴리스 빌드 (디버그 오버레이와 F1~F4 치트 꺼짐)
    ///   메뉴 Samkuk > Build > Windows - 개발용       : Development Build (디버그 오버레이와 F1~F4 켜짐, 로그 자세히)
    /// 명령줄은 <c>tools/build_windows.ps1</c> (에디터를 닫은 상태에서 한 줄로). 규칙과 문제 해결은 docs/BUILD.md.
    /// </summary>
    public static class BuildTool
    {
        public const string OutputRoot = "Builds";
        const string ExeName = "SamkukSurvivor.exe";

        [MenuItem("Samkuk/Build/Windows - 친구 공유용 (릴리스)")]
        public static void BuildWindowsRelease() => Run(development: false);

        [MenuItem("Samkuk/Build/Windows - 개발용 (디버그 키 포함)")]
        public static void BuildWindowsDevelopment() => Run(development: true);

        [MenuItem("Samkuk/Build/Builds 폴더 열기")]
        public static void OpenBuildsFolder()
        {
            string dir = Path.Combine(ProjectRoot, OutputRoot);
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        /// <summary>명령줄(-executeMethod)용: 릴리스 빌드. 실패하면 종료 코드 1.</summary>
        public static void BuildWindowsReleaseCli() => RunCli(development: false);

        /// <summary>명령줄(-executeMethod)용: 개발용 빌드. 실패하면 종료 코드 1.</summary>
        public static void BuildWindowsDevelopmentCli() => RunCli(development: true);

        static void RunCli(bool development)
        {
            bool ok = Run(development);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        // ───────────────────────── 빌드 ─────────────────────────

        static bool Run(bool development)
        {
            // 1. 빌드 설정: 타이틀 0, 전투 1, 내정 2. 빠진 씬이 있으면 빌드하지 않는다.
            Step12StrategySetup.RegisterBuildScenes();
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            string[] required =
            {
                Step9TitleSetup.TitleScenePath, Step9TitleSetup.BattleScenePath, Step12StrategySetup.ScenePath
            };
            var missing = required.Where(p => !scenes.Contains(p)).ToList();
            if (missing.Count > 0)
            {
                Debug.LogError($"[Samkuk] 빌드 중단: 필요한 씬이 없습니다 → {string.Join(", ", missing)}\n" +
                               "메뉴 Samkuk > Run All Setup 을 먼저 실행하세요.");
                return false;
            }

            // 2. 출력 폴더 (Builds/ 아래에서만 지운다)
            // 폴더 이름에 버전을 넣는다: 압축을 풀면 이 이름의 폴더 하나로 풀린다
            string folder = $"SamkukSurvivor_v{PlayerSettings.bundleVersion}{(development ? "_Dev" : "")}";
            string outDir = Path.Combine(ProjectRoot, OutputRoot, folder);
            if (!Path.GetFullPath(outDir).StartsWith(Path.GetFullPath(Path.Combine(ProjectRoot, OutputRoot)) + Path.DirectorySeparatorChar))
            {
                Debug.LogError($"[Samkuk] 빌드 중단: 출력 경로가 {OutputRoot}/ 밖입니다 ({outDir})");
                return false;
            }
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            // 3. 빌드
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            Debug.Log($"[Samkuk] Windows 빌드 시작 ({(development ? "개발용" : "릴리스")}, v{PlayerSettings.bundleVersion}) → {outDir}");
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Samkuk] 빌드 실패: {summary.result} (오류 {summary.totalErrors}개). 콘솔의 오류를 확인하세요.");
                return false;
            }

            // 4. 안내문 + 압축
            WriteReadme(outDir, development);
            string zip = Zip(outDir, development);

            double mb = summary.totalSize / (1024.0 * 1024.0);
            Debug.Log($"[Samkuk] 빌드 완료: {summary.totalTime:mm\\:ss} 소요, 빌드 {mb:0.0}MB\n폴더: {outDir}\n공유용 압축: {zip}");
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(zip);
            return true;
        }

        static string Zip(string outDir, bool development)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            string name = $"SamkukSurvivor_v{PlayerSettings.bundleVersion}_{stamp}{(development ? "_dev" : "")}.zip";
            string zipPath = Path.Combine(ProjectRoot, OutputRoot, name);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            // 압축을 풀면 폴더 하나로 풀리도록 폴더째 묶는다 (친구가 다른 파일과 섞지 않게)
            ZipFile.CreateFromDirectory(outDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, true);
            return zipPath;
        }

        // ───────────────────────── 안내문 ─────────────────────────

        static void WriteReadme(string outDir, bool development)
        {
            string text =
$@"삼국지 서바이버 - 테스트 빌드 v{PlayerSettings.bundleVersion}{(development ? " (개발용)" : "")}
빌드 시각: {DateTime.Now:yyyy-MM-dd HH:mm}

■ 실행
  SamkukSurvivor.exe 를 실행하세요. (Windows 10/11 64비트, 설치 필요 없음)
  처음 실행할 때 'Windows의 PC 보호' 창이 뜨면 [추가 정보] → [실행] 을 누르세요. (서명하지 않은 개인 빌드라서 나오는 경고입니다)
  압축 파일은 반드시 풀고 나서 실행하세요. 폴더 안의 다른 파일(SamkukSurvivor_Data 등)은 지우지 마세요.

■ 게임 방법
  - 이동: WASD / 방향키 (게임패드 왼쪽 스틱)    - 공격: 자동
  - 장수 고유 스킬: Space
  - 레벨업 카드 / 장수 선택: 마우스 클릭 또는 숫자 키 1~5
  - 일시정지: ESC     - 결과 화면: R 다시 하기, T 타이틀, M 내정으로

■ 모드
  - 시작: 장수를 고르고 1분 동안 생존하는 서바이버 한 판
  - 내정: 삼국지3처럼 지도에서 시작할 성을 고르고, 이웃한 적 성을 골라 출진해 전투에서 이기면 그 성을 차지합니다 (46곳 정복 = 천하 통일)
  - 영구 강화: 한 판에서 번 골드로 능력치를 올립니다

■ 설정 (타이틀 화면 왼쪽 위)
  효과음 볼륨, 화면 흔들림, 해상도, 화면 모드(창 모드 / 전체화면 / 전용 전체화면)

■ 저장 위치
  %USERPROFILE%\AppData\LocalLow\DefaultCompany\Samkuk\save.json
  처음부터 다시 하려면 타이틀의 [저장 초기화]를 두 번 누르거나 이 파일을 지우세요.

■ 알려진 사항
  - 그림과 소리는 임시 소재입니다 (코드로 만든 그림, 합성한 효과음).
  - 밸런스는 조정 중입니다. 지금은 적 수를 일부러 줄여 둔 상태입니다.
  - 성의 규모/지형은 전투 난이도에 아직 영향이 없습니다 (바닥 색만 바뀝니다).
{(development ? "  - 개발용 빌드: F1 적 +100, F2 무기 레벨업, F3 경험치 +10, F4 시간 +10초, 화면 왼쪽 위에 FPS 표시\n" : "")}
■ 문제가 생겼을 때 알려 주세요
  - 어떤 화면/장수/성에서, 무엇을 했을 때, 어떻게 되었는지 (가능하면 스크린샷)
  - 아래 로그 파일도 함께 보내 주시면 원인을 찾기 쉽습니다.
    %USERPROFILE%\AppData\LocalLow\DefaultCompany\Samkuk\Player.log
";
            // 메모장에서 한글이 깨지지 않도록 BOM 이 있는 UTF-8 로 저장한다
            File.WriteAllText(Path.Combine(outDir, "README.txt"), text.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(true));
        }
    }
}
