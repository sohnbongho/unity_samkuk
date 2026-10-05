using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.Stages;
using Samkuk.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.EditorTools
{
    /// <summary>Step 7: 적 종류 추가, 스테이지(웨이브/엘리트/보스) 데이터와 HUD(타이머, 배너, 보스바, 클리어 화면)를 구성한다.</summary>
    public static class Step7Setup
    {
        const string ScenePath = "Assets/Scenes/BattleScene.unity";
        const string EnemyDir = "Assets/ScriptableObjects/Enemies";
        const string StageDir = "Assets/ScriptableObjects/Stage";
        const string StagePath = StageDir + "/Stage_YellowTurban.asset";

        [MenuItem("Samkuk/Step 7 - Setup Stage (Waves & Boss)")]
        public static void Run()
        {
            System.IO.Directory.CreateDirectory(StageDir);
            AssetDatabase.Refresh();

            CreateEnemies();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            CreateStage();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildScene();
            Debug.Log("[Samkuk] Step 7 setup 완료 (클리어 60초. Play 후 F4: 시간 +10초)");
        }

        // ───────────────────────── 적 데이터 ─────────────────────────

        static void CreateEnemies()
        {
            // 동탁군
            Create("Enemy_DongzhuoInfantry", d =>
            {
                d.displayName = "동탁군 보병";
                d.tint = new Color(0.7f, 0.5f, 0.9f);
                d.scale = 1.1f; d.moveSpeed = 1.8f; d.maxHp = 28; d.contactDamage = 8; d.expReward = 2;
            });
            Create("Enemy_XiliangCavalry", d =>
            {
                d.displayName = "서량 기병";
                d.tint = new Color(0.85f, 0.55f, 0.25f);
                d.scale = 0.95f; d.moveSpeed = 2.4f; d.maxHp = 18; d.contactDamage = 8; d.expReward = 2;
                d.chargeInterval = 3.5f; d.chargeWindup = 0.5f; d.chargeDuration = 0.6f; d.chargeSpeedMultiplier = 3f;
            });
            // 여포군
            Create("Enemy_LvbuElite", d =>
            {
                d.displayName = "여포군 정예";
                d.tint = new Color(1f, 0.8f, 0.3f);
                d.scale = 1.2f; d.moveSpeed = 2.2f; d.maxHp = 60; d.contactDamage = 12; d.expReward = 3;
            });
            // 엘리트
            Create("Enemy_YellowTurbanGeneral", d =>
            {
                d.displayName = "황건 장수";
                d.tint = new Color(1f, 0.9f, 0.2f);
                d.scale = 1.6f; d.moveSpeed = 1.7f; d.maxHp = 120; d.contactDamage = 12; d.expReward = 15;
            });
            // 보스
            Create("Boss_Lvbu", d =>
            {
                d.displayName = "여포";
                d.tint = new Color(1f, 0.35f, 0.2f);
                d.scale = 2.4f; d.moveSpeed = 2.0f; d.maxHp = 500; d.contactDamage = 20; d.expReward = 40;
                d.chargeInterval = 4f; d.chargeWindup = 0.8f; d.chargeDuration = 0.9f; d.chargeSpeedMultiplier = 3.5f;
                d.colliderRadius = 0.4f;
            });
        }

        static void Create(string assetName, System.Action<EnemyData> init)
        {
            string path = $"{EnemyDir}/{assetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<EnemyData>(path) != null) return; // 사용자가 조정한 값 유지

            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.contactDamage = 5; d.expReward = 1; d.colliderRadius = 0.4f;
            init(d);
            AssetDatabase.CreateAsset(d, path);
        }

        static EnemyData Load(string assetName) =>
            AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDir}/{assetName}.asset");

        // ───────────────────────── 스테이지 ─────────────────────────

        static void CreateStage()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(StagePath);
            if (stage == null)
            {
                stage = ScriptableObject.CreateInstance<StageData>();
                AssetDatabase.CreateAsset(stage, StagePath);
            }

            var soldier = Load("Enemy_Soldier");
            var scout = Load("Enemy_Scout");
            var infantry = Load("Enemy_DongzhuoInfantry");
            var cavalry = Load("Enemy_XiliangCavalry");
            var lvbuElite = Load("Enemy_LvbuElite");
            var general = Load("Enemy_YellowTurbanGeneral");
            var boss = Load("Boss_Lvbu");

            stage.displayName = "황건적의 난";
            stage.duration = 60f;

            stage.waves.Clear();
            // 타격감 확인용으로 적 수를 크게 줄임: 스폰 속도와 동시 최대 수를 이전 값의 30%로 (이전 2.1/4.2/56/84/112/154)
            stage.waves.Add(MakeWave("황건적 습격", 0f, 0.63f, 17, (soldier, 1f)));
            stage.waves.Add(MakeWave("황건 척후대", 15f, 1.26f, 25, (soldier, 3f), (scout, 1f)));
            stage.waves.Add(MakeWave("동탁군 출병", 30f, 1.26f, 34, (infantry, 3f), (cavalry, 1f), (soldier, 1f)));
            // 마지막 웨이브: 정예 비중을 낮추고 스폰 속도를 줄여 평균 빌드 기준 압박비가 약 0.7~1.0이 되도록 (BalanceModel)
            stage.waves.Add(MakeWave("여포군 돌격", 45f, 1.26f, 46, (lvbuElite, 2f), (cavalry, 2f), (infantry, 2f)));

            stage.events.Clear();
            stage.events.Add(MakeEvent(20f, general, 1, false, "황건 장수 출현!"));
            stage.events.Add(MakeEvent(38f, general, 2, false, "황건 장수 둘이 나타났다!"));
            stage.events.Add(MakeEvent(45f, boss, 1, true, "여포 출현!"));

            EditorUtility.SetDirty(stage);
        }

        static Wave MakeWave(string name, float start, float rate, int max, params (EnemyData data, float weight)[] enemies)
        {
            var w = new Wave { name = name, startTime = start, spawnPerSecond = rate, maxAlive = max };
            foreach (var (data, weight) in enemies)
                w.enemies.Add(new WaveEnemy { data = data, weight = weight });
            return w;
        }

        static StageEvent MakeEvent(float time, EnemyData enemy, int count, bool isBoss, string message) =>
            new StageEvent { time = time, enemy = enemy, count = count, isBoss = isBoss, message = message };

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
                Debug.LogError("[Samkuk] Player / GameSystems / HUD 가 씬에 없습니다. Step 2~6 을 먼저 실행하세요.");
                return;
            }

            var stageData = AssetDatabase.LoadAssetAtPath<StageData>(StagePath);
            var spawner = systems.GetComponent<EnemySpawner>();
            var health = player.GetComponent<PlayerHealth>();
            var gm = systems.GetComponent<GameManager>();
            if (stageData == null || spawner == null || health == null || gm == null)
            {
                Debug.LogError($"[Samkuk] 참조 로드 실패 stage={stageData} spawner={spawner} health={health} gm={gm}");
                return;
            }

            // StageController
            var old = systems.GetComponent<StageController>();
            if (old != null) Object.DestroyImmediate(old);
            var sc = systems.AddComponent<StageController>();
            var scSo = new SerializedObject(sc);
            scSo.FindProperty("stage").objectReferenceValue = stageData;
            scSo.FindProperty("spawner").objectReferenceValue = spawner;
            scSo.FindProperty("health").objectReferenceValue = health;
            scSo.ApplyModifiedPropertiesWithoutUndo();

            // HUD
            foreach (var n in new[] { "TimerLabel", "WaveLabel", "Banner", "BossBar", "ClearPanel" })
            {
                var child = hud.transform.Find(n);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }
            var oldView = hud.GetComponent<StageHudView>();
            if (oldView != null) Object.DestroyImmediate(oldView);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var timer = AnchoredText("TimerLabel", hud.transform, font, 40, TextAnchor.UpperRight, "01:00",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -14f), new Vector2(300f, 52f));
            var wave = AnchoredText("WaveLabel", hud.transform, font, 24, TextAnchor.UpperRight, "",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -68f), new Vector2(520f, 36f));
            var banner = AnchoredText("Banner", hud.transform, font, 56, TextAnchor.MiddleCenter, "",
                new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 110f));
            banner.color = new Color(1f, 0.85f, 0.3f);
            var shadow = banner.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(3f, -3f);

            BuildBossBar(hud.transform, font, out var bossBar, out var bossFill, out var bossName);
            var clearPanel = BuildClearPanel(hud.transform, font);

            var view = hud.AddComponent<StageHudView>();
            var vSo = new SerializedObject(view);
            vSo.FindProperty("stage").objectReferenceValue = sc;
            vSo.FindProperty("timerLabel").objectReferenceValue = timer;
            vSo.FindProperty("waveLabel").objectReferenceValue = wave;
            vSo.FindProperty("bannerLabel").objectReferenceValue = banner;
            vSo.FindProperty("bossBar").objectReferenceValue = bossBar;
            vSo.FindProperty("bossFill").objectReferenceValue = bossFill;
            vSo.FindProperty("bossName").objectReferenceValue = bossName;
            vSo.ApplyModifiedPropertiesWithoutUndo();

            // GameManager 연결
            var gmSo = new SerializedObject(gm);
            gmSo.FindProperty("stage").objectReferenceValue = sc;
            gmSo.FindProperty("clearPanel").objectReferenceValue = clearPanel;
            gmSo.ApplyModifiedPropertiesWithoutUndo();

            // 레벨업 패널이 클리어/보스 UI보다 위에 그려지도록 마지막으로 이동
            var lv = hud.transform.Find("LevelUpPanel");
            if (lv != null) lv.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void BuildBossBar(Transform hud, Font font, out GameObject bar, out RectTransform fill, out Text nameLabel)
        {
            var rt = NewRect("BossBar", hud);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(640f, 32f);
            rt.anchoredPosition = new Vector2(0f, -60f);
            rt.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.04f, 0.04f, 0.9f);

            fill = NewRect("Fill", rt);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);
            fill.gameObject.AddComponent<Image>().color = new Color(0.85f, 0.15f, 0.1f);

            nameLabel = AnchoredText("Name", rt, font, 20, TextAnchor.MiddleCenter, "보스",
                Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            nameLabel.rectTransform.anchorMin = Vector2.zero;
            nameLabel.rectTransform.anchorMax = Vector2.one;
            nameLabel.rectTransform.offsetMin = Vector2.zero;
            nameLabel.rectTransform.offsetMax = Vector2.zero;

            bar = rt.gameObject;
            bar.SetActive(false);
        }

        static GameObject BuildClearPanel(Transform hud, Font font)
        {
            var panel = NewRect("ClearPanel", hud);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var title = AnchoredText("Title", panel, font, 80, TextAnchor.MiddleCenter, "스테이지 클리어!",
                new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 140f));
            title.color = new Color(1f, 0.85f, 0.3f);
            AnchoredText("Hint", panel, font, 30, TextAnchor.MiddleCenter, "R 키: 다시 시작",
                new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 60f));

            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Text AnchoredText(string name, Transform parent, Font font, int size, TextAnchor align, string value,
            Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 sizeDelta)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = sizeDelta;

            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.text = value;
            t.raycastTarget = false;
            return t;
        }
    }
}
