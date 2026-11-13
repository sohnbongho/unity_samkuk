using Samkuk.Core;
using Samkuk.Compat;
using Samkuk.Compat.InputSystem;

namespace Samkuk.World
{
    /// <summary>
    /// 맵 편집기에서 시작한 시험 전투(<see cref="GameSession.MapTest"/>)에서만 나타나는 작은 안내와 M 키.
    /// 전투 씬에 미리 둘 필요가 없도록 씬이 로드될 때 스스로 만들어진다.
    /// </summary>
    public class MapTestReturn : MonoBehaviour
    {
        /// <summary>화면 아래에 띄우는 안내 (화면 층이 그린다).</summary>
        public const string Notice = "맵 테스트 중 (결과는 저장되지 않음)  M: 맵 편집기로 돌아가기";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnSceneLoaded()
        {
            if (!GameSession.MapTest || SceneManager.GetActiveScene().name != GameManager.BattleSceneName) return;
            new GameObject("MapTestReturn").AddComponent<MapTestReturn>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.mKey.wasPressedThisFrame) return;

            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(GameManager.MapEditorSceneName))
                SceneManager.LoadScene(GameManager.MapEditorSceneName);
        }

    }
}
