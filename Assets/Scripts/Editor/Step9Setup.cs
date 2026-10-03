using System.IO;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Heroes;
using Samkuk.Meta;
using Samkuk.Player;
using Samkuk.Stages;
using Samkuk.UI;
using Samkuk.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 9-1: 영구 강화 데이터, 결과 화면(골드/기록 저장), 일시정지 메뉴를 구성한다.
    /// 기존 게임오버/클리어 패널은 결과 화면으로 대체된다.
    /// </summary>
    public static class Step9Setup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string MetaDir = "Assets/ScriptableObjects/Meta";
        public const string MetaCatalogPath = "Assets/ScriptableObjects/MetaCatalog.asset";

        static readonly string[] MetaNames =
        {
            "Meta_Health", "Meta_Might", "Meta_Agility", "Meta_Rations", "Meta_Virtue", "Meta_Study", "Meta_Fortune"
        };

        [MenuItem("Samkuk/Step 9-1 - Setup Save, Result & Pause")]
        public static void Run()
        {
            Directory.CreateDirectory(MetaDir);
            AssetDatabase.Refresh();

            CreateMetaUpgrades();
            CreateMetaCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildScene();
            Debug.Log("[Samkuk] Step 9-1 setup 완료 (ESC: 일시정지, 종료 시 결과 화면/골드 저장)");
        }

        // ───────────────────────── 영구 강화 ─────────────────────────

        static void CreateMetaUpgrades()
        {
            CreateMeta("Meta_Health", "체력 단련", "최대 체력이 늘어난다.", MetaStat.MaxHp, 10f, 5, 60, 1.5f);
            CreateMeta("Meta_Might", "무예 단련", "모든 공격력이 늘어난다.", MetaStat.Damage, 0.04f, 5, 80, 1.6f);
            CreateMeta("Meta_Agility", "경공", "이동 속도가 빨라진다.", MetaStat.MoveSpeed, 0.03f, 5, 70, 1.5f);
            CreateMeta("Meta_Rations", "군량 비축", "체력이 서서히 회복된다.", MetaStat.Regen, 0.15f, 5, 100, 1.7f);
            CreateMeta("Meta_Virtue", "인덕 쌓기", "경험치를 끌어당기는 범위가 넓어진다.", MetaStat.PickupRadius, 0.10f, 5, 60, 1.5f);
            CreateMeta("Meta_Study", "병법 연구", "경험치 획득량이 늘어난다.", MetaStat.ExpGain, 0.05f, 5, 90, 1.6f);
            CreateMeta("Meta_Fortune", "재물운", "판이 끝날 때 얻는 골드가 늘어난다.", MetaStat.GoldGain, 0.10f, 5, 120, 1.7f);
        }

        static void CreateMeta(string name, string display, string desc, MetaStat stat, float perLevel, int maxLevel,
            int baseCost, float growth)
        {
            string path = $"{MetaDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<MetaUpgradeData>(path) != null) return; // 사용자가 조정한 값 유지

            var d = ScriptableObject.CreateInstance<MetaUpgradeData>();
            d.displayName = display; d.description = desc; d.stat = stat;
            d.valuePerLevel = perLevel; d.maxLevel = maxLevel; d.baseCost = baseCost; d.costGrowth = growth;
            AssetDatabase.CreateAsset(d, path);
        }

        static void CreateMetaCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MetaCatalog>(MetaCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MetaCatalog>();
                AssetDatabase.CreateAsset(catalog, MetaCatalogPath);
            }

            catalog.upgrades.RemoveAll(u => u == null);
            foreach (var n in MetaNames)
            {
                var u = AssetDatabase.LoadAssetAtPath<MetaUpgradeData>($"{MetaDir}/{n}.asset");
                if (u != null && !catalog.upgrades.Contains(u)) catalog.upgrades.Add(u);
            }
            EditorUtility.SetDirty(catalog);
        }

        // ───────────────────────── 씬 ─────────────────────────

        static void BuildScene()
        {
            // 주의: OpenScene(Single) 이후에 에셋을 로드한다.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            var systems = GameObject.Find("GameSystems");
            var hud = GameObject.Find("HUD");
            if (player == null || systems == null || hud == null)
            {
                Debug.LogError("[Samkuk] Player / GameSystems / HUD 가 씬에 없습니다. Step 2~8 을 먼저 실행하세요.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<MetaCatalog>(MetaCatalogPath);
            var stats = player.GetComponent<PlayerStats>();
            var exp = player.GetComponent<PlayerExperience>();
            var game = systems.GetComponent<GameManager>();
            var stage = systems.GetComponent<StageController>();
            var hero = systems.GetComponent<HeroSelectController>();
            var levelUp = systems.GetComponent<LevelUpController>();
            if (catalog == null || stats == null || exp == null || game == null)
            {
                Debug.LogError($"[Samkuk] 참조 로드 실패 catalog={catalog} stats={stats} exp={exp} game={game}");
                return;
            }

            // 기존 게임오버/클리어 패널은 결과 화면이 대체한다
            DestroyChild(hud.transform, "GameOverPanel");
            DestroyChild(hud.transform, "ClearPanel");
            var gmSo = new SerializedObject(game);
            gmSo.FindProperty("gameOverPanel").objectReferenceValue = null;
            gmSo.FindProperty("clearPanel").objectReferenceValue = null;
            gmSo.ApplyModifiedPropertiesWithoutUndo();

            // HUD: 결과/일시정지 화면
            DestroyChild(hud.transform, "ResultPanel");
            DestroyChild(hud.transform, "PausePanel");
            RemoveComponent<ResultUI>(hud);
            RemoveComponent<PauseUI>(hud);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildResultPanel(hud.transform, font, out var resultPanel, out var rTitle, out var rDetails, out var rRetry, out var rTitleBtn);
            BuildPausePanel(hud.transform, font, out var pausePanel, out var pResume, out var pRestart, out var pTitleBtn);

            var resultUi = hud.AddComponent<ResultUI>();
            var ruSo = new SerializedObject(resultUi);
            ruSo.FindProperty("panel").objectReferenceValue = resultPanel;
            ruSo.FindProperty("titleLabel").objectReferenceValue = rTitle;
            ruSo.FindProperty("detailsLabel").objectReferenceValue = rDetails;
            ruSo.FindProperty("retryButton").objectReferenceValue = rRetry;
            ruSo.FindProperty("titleButton").objectReferenceValue = rTitleBtn;
            ruSo.ApplyModifiedPropertiesWithoutUndo();

            var pauseUi = hud.AddComponent<PauseUI>();
            var puSo = new SerializedObject(pauseUi);
            puSo.FindProperty("panel").objectReferenceValue = pausePanel;
            puSo.FindProperty("resumeButton").objectReferenceValue = pResume;
            puSo.FindProperty("restartButton").objectReferenceValue = pRestart;
            puSo.FindProperty("titleButton").objectReferenceValue = pTitleBtn;
            puSo.ApplyModifiedPropertiesWithoutUndo();

            // GameSystems 컴포넌트
            RemoveComponent<MetaApplier>(systems);
            RemoveComponent<RunStats>(systems);
            RemoveComponent<ResultController>(systems);
            RemoveComponent<PauseController>(systems);

            var meta = systems.AddComponent<MetaApplier>();
            var mSo = new SerializedObject(meta);
            mSo.FindProperty("catalog").objectReferenceValue = catalog;
            mSo.FindProperty("stats").objectReferenceValue = stats;
            mSo.ApplyModifiedPropertiesWithoutUndo();

            var run = systems.AddComponent<RunStats>();
            var rsSo = new SerializedObject(run);
            rsSo.FindProperty("stage").objectReferenceValue = stage;
            rsSo.FindProperty("experience").objectReferenceValue = exp;
            rsSo.ApplyModifiedPropertiesWithoutUndo();

            var result = systems.AddComponent<ResultController>();
            var rcSo = new SerializedObject(result);
            rcSo.FindProperty("game").objectReferenceValue = game;
            rcSo.FindProperty("run").objectReferenceValue = run;
            rcSo.FindProperty("meta").objectReferenceValue = meta;
            rcSo.FindProperty("hero").objectReferenceValue = hero;
            rcSo.FindProperty("ui").objectReferenceValue = resultUi;
            rcSo.ApplyModifiedPropertiesWithoutUndo();

            var pause = systems.AddComponent<PauseController>();
            var pcSo = new SerializedObject(pause);
            pcSo.FindProperty("game").objectReferenceValue = game;
            pcSo.FindProperty("hero").objectReferenceValue = hero;
            pcSo.FindProperty("levelUp").objectReferenceValue = levelUp;
            pcSo.FindProperty("ui").objectReferenceValue = pauseUi;
            pcSo.ApplyModifiedPropertiesWithoutUndo();

            if (meta.Catalog == null || result.Game == null)
                Debug.LogError("[Samkuk] 메타/결과 컨트롤러 참조 연결 실패");

            // 장수 선택이 가장 위, 그 아래에 결과/일시정지 화면
            var heroPanel = hud.transform.Find("HeroSelectPanel");
            if (heroPanel != null) heroPanel.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void RemoveComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null) Object.DestroyImmediate(c);
        }

        static void DestroyChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        static void BuildResultPanel(Transform hud, Font font, out GameObject panelGo, out Text title, out Text details,
            out Button retry, out Button toTitle)
        {
            var panel = NewRect("ResultPanel", hud);
            Stretch(panel);
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var box = NewRect("Box", panel);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(760f, 700f);
            box.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.12f, 0.18f, 0.98f);

            title = NewText("Title", box, font, 64, TextAnchor.MiddleCenter, "결과");
            SetTop(title.rectTransform, 40f, 110f);

            details = NewText("Details", box, font, 32, TextAnchor.UpperLeft, "");
            details.rectTransform.anchorMin = new Vector2(0f, 0f);
            details.rectTransform.anchorMax = new Vector2(1f, 1f);
            details.rectTransform.offsetMin = new Vector2(80f, 130f);
            details.rectTransform.offsetMax = new Vector2(-60f, -170f);
            details.lineSpacing = 1.15f;

            retry = NewButton("RetryButton", box, font, "다시 하기  [R]", new Vector2(-190f, 70f), new Vector2(320f, 74f));
            toTitle = NewButton("TitleButton", box, font, "타이틀  [T]", new Vector2(190f, 70f), new Vector2(320f, 74f));

            panel.gameObject.SetActive(false);
            panelGo = panel.gameObject;
        }

        static void BuildPausePanel(Transform hud, Font font, out GameObject panelGo, out Button resume,
            out Button restart, out Button toTitle)
        {
            var panel = NewRect("PausePanel", hud);
            Stretch(panel);
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            var title = NewText("Title", panel, font, 64, TextAnchor.MiddleCenter, "일시정지");
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.72f);
            title.rectTransform.sizeDelta = new Vector2(800f, 100f);

            resume = NewCenterButton(panel, font, "계속하기  [ESC]", 0.57f);
            restart = NewCenterButton(panel, font, "다시 시작", 0.47f);
            toTitle = NewCenterButton(panel, font, "타이틀로", 0.37f);

            panel.gameObject.SetActive(false);
            panelGo = panel.gameObject;
        }

        static Button NewCenterButton(RectTransform parent, Font font, string label, float anchorY)
        {
            var b = NewButton(label, parent, font, label, Vector2.zero, new Vector2(420f, 80f));
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY);
            rt.anchoredPosition = Vector2.zero;
            return b;
        }

        static Button NewButton(string name, RectTransform parent, Font font, string label, Vector2 position, Vector2 size)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;

            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.25f, 0.3f, 0.45f, 1f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            var text = NewText("Label", rt, font, 32, TextAnchor.MiddleCenter, label);
            Stretch(text.rectTransform);
            return btn;
        }

        static void SetTop(RectTransform rt, float topOffset, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, -topOffset);
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
