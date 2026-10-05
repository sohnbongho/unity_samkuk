using Samkuk.Core;
using Samkuk.Player;
using Samkuk.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.EditorTools
{
    /// <summary>Step 4: 플레이어 체력, 데미지 숫자, HP 바, 게임오버를 구성한다.</summary>
    public static class Step4Setup
    {
        const string ScenePath = "Assets/Scenes/BattleScene.unity";
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

        [MenuItem("Samkuk/Step 4 - Setup Combat Core")]
        public static void Run()
        {
            // 프리팹 먼저 수정한 뒤 씬을 연다 (OpenScene이 로드된 에셋 참조를 무효화하므로)
            AddPlayerHealthToPrefab();
            BuildScene();
            Debug.Log("[Samkuk] Step 4 setup 완료");
        }

        static void AddPlayerHealthToPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (root.GetComponent<PlayerHealth>() == null) root.AddComponent<PlayerHealth>();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void BuildScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            var systems = GameObject.Find("GameSystems");
            if (player == null || systems == null)
            {
                Debug.LogError("[Samkuk] Player 또는 GameSystems가 씬에 없습니다. Step 2, 3 을 먼저 실행하세요.");
                return;
            }
            var health = player.GetComponent<PlayerHealth>();
            if (health == null)
            {
                Debug.LogError("[Samkuk] Player에 PlayerHealth가 없습니다.");
                return;
            }

            // 데미지 숫자
            if (systems.GetComponent<DamageNumberSpawner>() == null)
                systems.AddComponent<DamageNumberSpawner>();

            // HUD
            var oldHud = GameObject.Find("HUD");
            if (oldHud != null) Object.DestroyImmediate(oldHud);
            var hud = BuildHud(out var fill, out var label, out var gameOverPanel);

            var hudCtrl = hud.AddComponent<HudController>();
            var so = new SerializedObject(hudCtrl);
            so.FindProperty("playerHealth").objectReferenceValue = health;
            so.FindProperty("hpFill").objectReferenceValue = fill;
            so.FindProperty("hpLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 게임 매니저
            var gm = systems.GetComponent<GameManager>();
            if (gm == null) gm = systems.AddComponent<GameManager>();
            var gso = new SerializedObject(gm);
            gso.FindProperty("playerHealth").objectReferenceValue = health;
            gso.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
            gso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static GameObject BuildHud(out RectTransform fill, out Text label, out GameObject gameOverPanel)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // HP 바 배경
            var bg = NewRect("HpBar", canvasGo.transform);
            bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 1f);
            bg.pivot = new Vector2(0.5f, 1f);
            bg.sizeDelta = new Vector2(420f, 26f);
            bg.anchoredPosition = new Vector2(0f, -20f);
            bg.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            // 채움 (왼쪽 피벗, localScale.x로 비율 조절)
            fill = NewRect("Fill", bg);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);
            fill.gameObject.AddComponent<Image>().color = new Color(0.8f, 0.15f, 0.15f);

            label = NewText("Label", bg, font, 18, TextAnchor.MiddleCenter, "HP");
            Stretch(label.rectTransform);

            // 게임오버 패널
            var panel = NewRect("GameOverPanel", canvasGo.transform);
            Stretch(panel);
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var title = NewText("Title", panel, font, 80, TextAnchor.MiddleCenter, "GAME OVER");
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.55f);
            title.rectTransform.sizeDelta = new Vector2(900f, 140f);

            var hint = NewText("Hint", panel, font, 30, TextAnchor.MiddleCenter, "Press R to restart");
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0.42f);
            hint.rectTransform.sizeDelta = new Vector2(900f, 60f);

            gameOverPanel = panel.gameObject;
            panel.gameObject.SetActive(false);
            return canvasGo;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Text NewText(string name, Transform parent, Font font, int size, TextAnchor anchor, string value)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = value;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
