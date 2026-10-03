using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>
    /// 한글을 표시할 수 있는 OS 폰트(맑은 고딕 등)를 제공한다.
    /// 기본 내장 폰트에는 한글이 없으므로 uGUI Text에 이 폰트를 적용한다. (PC 전용 가정)
    /// </summary>
    public static class UiFont
    {
        static Font font;

        public static Font Get()
        {
            if (font == null)
            {
                // 테마에 폰트가 지정되어 있으면 그것을, 아니면 한글이 되는 OS 폰트를 쓴다
                var themed = UiTheme.Get().font;
                font = themed != null
                    ? themed
                    : Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 16);
            }
            return font;
        }

        /// <summary>root 아래(비활성 포함)의 모든 Text에 한글 폰트를 적용한다.</summary>
        public static void Apply(GameObject root)
        {
            var f = Get();
            foreach (var t in root.GetComponentsInChildren<Text>(true))
                t.font = f;
        }
    }
}
