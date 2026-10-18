using System;
using System.Collections.Generic;
using Samkuk.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>협동 전투 결과 창(코드 생성): 승패 문구, 참가자별 처치/레벨/골드, [방으로 돌아가기]. 서버가 준 값을 그대로 보여 준다.</summary>
    public class CoopResultPanel : MonoBehaviour
    {
        public const string RootName = "CoopResultPanel";

        public struct Row
        {
            public string Name, Hero;
            public int Kills, Level, Gold;
            public bool Alive;
        }

        public static CoopResultPanel Create(Transform parent, string title, IReadOnlyList<Row> rows, Action onBack)
        {
            var go = new GameObject(RootName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var blocker = go.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.65f);

            var panel = go.AddComponent<CoopResultPanel>();
            panel.Build(title, rows, onBack);
            return panel;
        }

        void Build(string title, IReadOnlyList<Row> rows, Action onBack)
        {
            var font = UiFont.Get();
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(transform, false);
            UiBuild.Place((RectTransform)box.transform, 0f, 0f, 900f, 560f);
            var img = box.GetComponent<Image>();
            img.color = new Color(0.12f, 0.09f, 0.07f, 0.96f);
            UiSkin.StylePanel(img);

            var titleText = UiBuild.Text("Title", box.transform, font, 40, TextAnchor.MiddleCenter, title);
            UiBuild.Place(titleText.rectTransform, 0f, 220f, 860f, 60f);
            UiSkin.StyleTitle(titleText);

            var header = UiBuild.Text("Header", box.transform, font, 24, TextAnchor.MiddleLeft, "   이름                      장수            처치      레벨      골드");
            UiBuild.Place(header.rectTransform, 0f, 150f, 820f, 36f);
            header.color = new Color(1f, 0.9f, 0.6f);

            for (int i = 0; i < rows.Count && i < 4; i++)
            {
                var r = rows[i];
                string line = $"{i + 1}. {Pad(r.Name, 22)}{Pad(r.Hero, 14)}{Pad(r.Kills.ToString(), 9)}{Pad("Lv." + r.Level, 9)}{r.Gold}" + (r.Alive ? "" : "   (쓰러짐)");
                var row = UiBuild.Text("Row" + i, box.transform, font, 26, TextAnchor.MiddleLeft, line);
                UiBuild.Place(row.rectTransform, 0f, 100f - i * 48f, 820f, 44f);
            }
            if (rows.Count == 0)
            {
                var none = UiBuild.Text("None", box.transform, font, 26, TextAnchor.MiddleCenter, "결과를 받지 못했습니다");
                UiBuild.Place(none.rectTransform, 0f, 60f, 820f, 44f);
            }

            var back = UiBuild.Button("BackButton", box.transform, font, "방으로 돌아가기", -200f, new Vector2(400f, 72f), new Color(0.55f, 0.2f, 0.18f));
            back.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); onBack?.Invoke(); });
        }

        static string Pad(string s, int width)
        {
            s ??= "";
            // 한글은 두 칸 폭이라 대략 맞춘다 (정확한 표가 필요하면 열을 따로 둔다)
            int w = 0;
            foreach (char c in s) w += c > 0x7f ? 2 : 1;
            return s + new string(' ', Mathf.Max(1, width - w));
        }
    }
}
