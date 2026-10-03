using System;
using System.IO;
using Samkuk.UI;
using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 10-3: 중국풍 UI 테마. 금빛 이중 테두리 프레임 등 스프라이트를 코드로 만들어 PNG로 저장하고
    /// (9-슬라이스 Border 지정), Resources/UiTheme.asset 에 연결한다.
    /// 이미 있는 PNG/슬롯은 덮어쓰지 않으므로, 직접 만든 아트로 교체한 뒤 셋업을 다시 돌려도 안전하다.
    /// (생성 모양을 다시 만들고 싶으면 해당 PNG를 지우고 셋업을 실행한다.)
    /// </summary>
    public static class Step10ThemeSetup
    {
        public const string SpriteDir = "Assets/Sprites/UI";
        public const string ThemePath = "Assets/Resources/UiTheme.asset";

        // 색 (스프라이트에 구워 넣음)
        static readonly Color Ink = new Color(0.10f, 0.05f, 0.04f, 1f);
        static readonly Color GoldHighlight = new Color(1f, 0.92f, 0.55f, 1f);
        static readonly Color Gold = new Color(0.86f, 0.66f, 0.25f, 1f);
        static readonly Color GoldDark = new Color(0.55f, 0.38f, 0.12f, 1f);

        [MenuItem("Samkuk/Step 10-3 - Setup UI Theme")]
        public static void Run()
        {
            Directory.CreateDirectory(SpriteDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));

            WriteSprite("UiFill", 32, 32, 10, FillPixel);
            WriteSprite("UiFrame", 64, 64, 24, FramePixel);
            WriteSprite("UiFrameThin", 32, 32, 10, FrameThinPixel);
            WriteSprite("UiDivider", 256, 16, 0, DividerPixel);
            AssetDatabase.Refresh();

            CreateTheme();
            AssetDatabase.SaveAssets();
            Debug.Log("[Samkuk] Step 10-3 setup 완료 (UI 테마: Assets/Resources/UiTheme.asset)");
        }

        // ───────────────────────── 테마 에셋 ─────────────────────────

        static void CreateTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            // 비어 있는 슬롯만 채운다 (직접 지정한 스프라이트는 유지)
            if (theme.fill == null) theme.fill = Load("UiFill");
            if (theme.frame == null) theme.frame = Load("UiFrame");
            if (theme.frameThin == null) theme.frameThin = Load("UiFrameThin");
            if (theme.divider == null) theme.divider = Load("UiDivider");

            EditorUtility.SetDirty(theme);
            UiTheme.ResetCache();
        }

        static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");

        // ───────────────────────── 스프라이트 생성 ─────────────────────────

        static void WriteSprite(string name, int w, int h, int border, Func<int, int, int, int, Color> pixel)
        {
            string path = $"{SpriteDir}/{name}.png";
            if (File.Exists(path)) return; // 사용자가 교체했을 수 있으므로 덮어쓰지 않음

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, pixel(x, y, w, h));
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100f; // 캔버스 기본 단위와 같아 테두리가 픽셀 그대로 표시된다
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.spriteBorder = new Vector4(border, border, border, border);
            imp.SaveAndReimport();
        }

        /// <summary>둥근 사각형 바탕 (흰색, 가장자리 안티앨리어싱). 색은 Image.color 로 입힌다.</summary>
        static Color FillPixel(int x, int y, int w, int h)
        {
            const float r = 6f;
            float px = Mathf.Min(x + 0.5f, w - x - 0.5f);
            float py = Mathf.Min(y + 0.5f, h - y - 0.5f);

            float signedDistance = (px < r && py < r)
                ? r - Mathf.Sqrt((r - px) * (r - px) + (r - py) * (r - py))
                : Mathf.Min(px, py);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(signedDistance + 0.5f));
        }

        /// <summary>
        /// 패널/카드 테두리: 바깥 먹선 → 금빛 3겹(밝음/중간/어두움) → 얇은 먹선 → 간격 → 안쪽 가는 금선,
        /// 네 모서리에는 마름모 장식. 가운데는 투명.
        /// </summary>
        static Color FramePixel(int x, int y, int w, int h)
        {
            int dx = Mathf.Min(x, w - 1 - x);
            int dy = Mathf.Min(y, h - 1 - y);
            int d = Mathf.Min(dx, dy); // 가장 가까운 변까지의 거리

            // 모서리 장식 (두 변과 모두 가까운 곳에만)
            if (dx <= 26 && dy <= 26)
            {
                int md = Mathf.Abs(dx - 13) + Mathf.Abs(dy - 13);
                if (md <= 3) return GoldHighlight;
                if (md <= 5) return Ink;
            }

            if (d <= 1) return Ink;
            if (d == 2) return GoldHighlight;
            if (d == 3) return Gold;
            if (d == 4) return GoldDark;
            if (d == 5) return new Color(Ink.r, Ink.g, Ink.b, 0.9f);
            if (d == 9) return new Color(Gold.r, Gold.g, Gold.b, 0.55f);
            return Color.clear;
        }

        /// <summary>버튼/바 테두리: 먹선 + 금선 두 겹 + 어두운 금선 + 안쪽 가는 먹선.</summary>
        static Color FrameThinPixel(int x, int y, int w, int h)
        {
            int d = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
            if (d == 0) return Ink;
            if (d == 1) return GoldHighlight;
            if (d == 2) return Gold;
            if (d == 3) return GoldDark;
            if (d == 4) return new Color(Ink.r, Ink.g, Ink.b, 0.8f);
            return Color.clear;
        }

        /// <summary>가운데가 마름모이고 양끝으로 갈수록 옅어지는 가로 구분선 + 작은 점 두 개.</summary>
        static Color DividerPixel(int x, int y, int w, int h)
        {
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            float dx = Mathf.Abs(x - cx), dy = Mathf.Abs(y - cy);

            float md = dx + dy;
            if (md <= 5f) return GoldHighlight;
            if (md <= 6.5f) return Ink;

            // 작은 점
            float dotDist = Mathf.Sqrt((dx - 22f) * (dx - 22f) + dy * dy);
            if (dotDist <= 2.2f) return Gold;

            // 가는 선 (양끝으로 갈수록 옅어짐)
            if (dy <= 1f)
            {
                float t = dx / cx;
                float alpha = Mathf.Clamp01(1f - t * t) * (dy <= 0.5f ? 1f : 0.55f);
                return new Color(Gold.r, Gold.g, Gold.b, alpha);
            }
            return Color.clear;
        }
    }
}
