using Samkuk.Allies;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Heroes;
using Samkuk.Player;
using Samkuk.UI;
using Samkuk.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 10-8: 아군(동행 장수). 게임 씬에 아군 선택 화면(HUD)과 AllyManager(GameSystems)를 만든다.
    /// 장수 선택(Step 8) 뒤에 이어서 나오며, 고른 장수를 뺀 나머지 중 최대 2명을 고른다.
    /// 여러 번 실행해도 같은 결과가 되도록 기존 패널/컴포넌트를 지우고 다시 만든다.
    /// </summary>
    public static class Step10AllySetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const int CardCount = 4; // 고른 장수를 뺀 나머지 (장수 5명 기준)

        [MenuItem("Samkuk/Step 10-8 - Setup Allies (Mercenary Heroes)")]
        public static void Run()
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

            var playerController = player.GetComponent<PlayerController>();
            var weapons = player.GetComponent<WeaponController>();
            var experience = player.GetComponent<PlayerExperience>();
            var enemyManager = systems.GetComponent<EnemyManager>();
            if (playerController == null || weapons == null || enemyManager == null)
            {
                Debug.LogError($"[Samkuk] 참조 로드 실패 player={playerController} weapons={weapons} enemies={enemyManager}");
                return;
            }

            // 다시 만들 수 있도록 지운다
            Step8Setup.DestroyChild(hud.transform, "AllySelectPanel");
            var oldUi = hud.GetComponent<AllySelectUI>();
            if (oldUi != null) Object.DestroyImmediate(oldUi);
            var oldManager = systems.GetComponent<AllyManager>();
            if (oldManager != null) Object.DestroyImmediate(oldManager);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var playerSprite = Step8Setup.LoadSprite("Player");
            BuildPanel(hud.transform, font, playerSprite, out var panel, out var cards, out var countLabel, out var confirm);

            var ui = hud.AddComponent<AllySelectUI>();
            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("panel").objectReferenceValue = panel;
            uiSo.FindProperty("countLabel").objectReferenceValue = countLabel;
            uiSo.FindProperty("confirmButton").objectReferenceValue = confirm;
            var cardsProp = uiSo.FindProperty("cards");
            cardsProp.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                var el = cardsProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("button").objectReferenceValue = cards[i].button;
                el.FindPropertyRelative("hotkey").objectReferenceValue = cards[i].hotkey;
                el.FindPropertyRelative("heroName").objectReferenceValue = cards[i].heroName;
                el.FindPropertyRelative("title").objectReferenceValue = cards[i].title;
                el.FindPropertyRelative("description").objectReferenceValue = cards[i].description;
                el.FindPropertyRelative("portrait").objectReferenceValue = cards[i].portrait;
                el.FindPropertyRelative("selectedMark").objectReferenceValue = cards[i].selectedMark;
            }
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            var manager = systems.AddComponent<AllyManager>();
            var mSo = new SerializedObject(manager);
            mSo.FindProperty("player").objectReferenceValue = playerController;
            mSo.FindProperty("playerWeapons").objectReferenceValue = weapons;
            mSo.FindProperty("enemies").objectReferenceValue = enemyManager;
            mSo.FindProperty("experience").objectReferenceValue = experience;
            mSo.ApplyModifiedPropertiesWithoutUndo();

            // 장수 선택 컨트롤러에 연결 (비어 있어도 실행 중에 찾지만 명시해 둔다)
            var ctrl = systems.GetComponent<HeroSelectController>();
            if (ctrl != null)
            {
                var cSo = new SerializedObject(ctrl);
                cSo.FindProperty("allyUi").objectReferenceValue = ui;
                cSo.FindProperty("allyManager").objectReferenceValue = manager;
                cSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[Samkuk] HeroSelectController 가 없습니다. Step 8 을 먼저 실행하세요.");
            }

            panel.transform.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Samkuk] Step 10-8 setup 완료 (아군 선택 화면 + AllyManager). Play 후 장수를 고르면 아군 선택 화면이 이어집니다.");
        }

        static void BuildPanel(Transform hud, Font font, Sprite portraitSprite, out GameObject panelGo,
            out AllyCardView[] cards, out Text countLabel, out Button confirm)
        {
            var panel = Step8Setup.NewRect("AllySelectPanel", hud);
            Step8Setup.Stretch(panel);
            panel.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.08f, 0.95f);

            // 이름이 Title 인 글자는 금색으로 스킨된다
            var title = Step8Setup.NewText("Title", panel, font, 54, TextAnchor.MiddleCenter, "함께 싸울 장수를 고르세요 (0/2)");
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.93f);
            title.rectTransform.sizeDelta = new Vector2(1400f, 100f);
            countLabel = title;

            var hint = Step8Setup.NewText("Hint", panel, font, 24, TextAnchor.MiddleCenter,
                "카드를 클릭하거나 1 ~ 4 키로 고르고 다시 누르면 해제 · 정원이 차면 먼저 고른 장수가 빠집니다 · 0명도 괜찮습니다");
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0.865f);
            hint.rectTransform.sizeDelta = new Vector2(1700f, 40f);

            // 장수 선택 카드와 같은 모양/이름(Hero1..4)이라 같은 스킨이 입혀진다
            var row = Step8Setup.CreateCardRow(panel, "Allies", 0.46f, 700f, 14f);
            cards = new AllyCardView[CardCount];
            for (int i = 0; i < CardCount; i++)
            {
                var heroCard = Step8Setup.BuildHeroCard(row, font, portraitSprite, i, 320f, 190f);
                var card = new AllyCardView
                {
                    button = heroCard.button, hotkey = heroCard.hotkey, heroName = heroCard.heroName,
                    title = heroCard.title, description = heroCard.description, portrait = heroCard.portrait
                };
                card.selectedMark = BuildMark(heroCard.button.transform, font);
                cards[i] = card;
            }

            confirm = BuildConfirmButton(panel, font);
            panel.gameObject.SetActive(false);
            panelGo = panel.gameObject;
        }

        /// <summary>카드 위쪽에 얹는 "동행" 표시 (골랐을 때만 켜진다).</summary>
        static GameObject BuildMark(Transform card, Font font)
        {
            var rt = Step8Setup.NewRect("Mark", card);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(110f, 44f);
            rt.anchoredPosition = new Vector2(-10f, -10f);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(0.92f, 0.72f, 0.2f, 1f);
            bg.raycastTarget = false;

            var label = Step8Setup.NewText("Label", rt, font, 28, TextAnchor.MiddleCenter, "동행");
            label.color = new Color(0.15f, 0.08f, 0.04f);
            Step8Setup.Stretch(label.rectTransform);

            rt.gameObject.SetActive(false);
            return rt.gameObject;
        }

        static Button BuildConfirmButton(RectTransform panel, Font font)
        {
            // 이름이 StartButton 이면 진홍색 주 버튼으로 스킨된다
            var rt = Step8Setup.NewRect("StartButton", panel);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.07f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(520f, 88f);
            rt.anchoredPosition = Vector2.zero;

            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.55f, 0.2f, 0.18f, 1f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            var text = Step8Setup.NewText("Label", rt, font, 38, TextAnchor.MiddleCenter, "출발 (Enter)");
            Step8Setup.Stretch(text.rectTransform);
            return btn;
        }
    }
}
