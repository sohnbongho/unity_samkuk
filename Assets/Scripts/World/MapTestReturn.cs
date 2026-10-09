using Samkuk.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Samkuk.World
{
    /// <summary>
    /// 맵 편집기에서 시작한 시험 전투(<see cref="GameSession.MapTest"/>)에서만 나타나는 작은 안내와 M 키.
    /// 전투 씬에 미리 둘 필요가 없도록 씬이 로드될 때 스스로 만들어진다.
    /// </summary>
    public class MapTestReturn : MonoBehaviour
    {
        GUIStyle style;

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

        void OnGUI()
        {
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.9f, 0.4f) } };
            GUI.Label(new Rect(14f, Screen.height - 36f, 700f, 30f), "맵 테스트 중 (결과는 저장되지 않음)  M: 맵 편집기로 돌아가기", style);
        }
    }
}
