using System;
using System.Collections.Generic;
using Samkuk.Meta;
using UnityEngine;

namespace Samkuk.Core
{
    /// <summary>
    /// 화면 설정(해상도, 창 모드)의 목록/순환/적용. 값은 <see cref="SaveData"/> 에 저장하고 타이틀에서 바꾼다.
    /// 해상도는 16:9 프리셋 중에서 고르며 모니터보다 큰 것은 목록에서 뺀다.
    /// 테두리 없는 전체화면은 바탕화면 해상도를 그대로 쓰므로 해상도 선택이 의미가 없다
    /// (UI는 기준 해상도에 맞춰 자동으로 늘어나므로 어떤 해상도에서도 배치는 같다).
    /// </summary>
    public static class DisplaySettings
    {
        public const int ModeWindowed = 0, ModeBorderless = 1, ModeExclusive = 2;

        static readonly Vector2Int[] Presets =
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
        };

        static readonly string[] ModeNames = { "창 모드", "전체화면", "전용 전체화면" };

        static bool appliedOnce;

        /// <summary>해상도 적용 방법 (테스트에서 대체 가능). 에디터에서는 게임 뷰를 건드리지 않는다.</summary>
        public static Action<int, int, FullScreenMode> ApplyAction { get; set; } = DefaultApply;

        /// <summary>모니터(바탕화면) 크기 이하의 프리셋. 모니터가 아주 작아도 가장 작은 프리셋은 남긴다.</summary>
        public static List<Vector2Int> Available(int maxWidth, int maxHeight)
        {
            var list = new List<Vector2Int>();
            foreach (var p in Presets)
                if (p.x <= maxWidth && p.y <= maxHeight) list.Add(p);
            if (list.Count == 0) list.Add(Presets[0]);
            return list;
        }

        /// <summary>현재 해상도 다음 프리셋 (끝에서 처음으로). 목록에 없는 값이면 가장 가까운 것 다음.</summary>
        public static Vector2Int NextResolution(SaveData save, int maxWidth, int maxHeight)
        {
            var list = Available(maxWidth, maxHeight);
            int current = 0;
            long best = long.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                long d = Math.Abs((long)list[i].x * list[i].y - (long)save.displayWidth * save.displayHeight);
                if (d < best) { best = d; current = i; }
            }
            return list[(current + 1) % list.Count];
        }

        public static int NextMode(int mode) => (Normalize(mode) + 1) % ModeNames.Length;

        public static int Normalize(int mode) => mode >= 0 && mode < ModeNames.Length ? mode : ModeBorderless;

        public static string ModeName(int mode) => ModeNames[Normalize(mode)];

        /// <summary>해상도 버튼 문구. 테두리 없는 전체화면은 바탕화면 해상도를 쓰므로 "자동".</summary>
        public static string ResolutionLabel(SaveData save) =>
            Normalize(save.windowMode) == ModeBorderless ? "해상도: 자동" : $"해상도: {save.displayWidth}x{save.displayHeight}";

        /// <summary>해상도를 고를 수 있는 모드인가 (창 모드, 전용 전체화면).</summary>
        public static bool CanChooseResolution(SaveData save) => Normalize(save.windowMode) != ModeBorderless;

        public static FullScreenMode ToFullScreenMode(int mode)
        {
            switch (Normalize(mode))
            {
                case ModeWindowed: return FullScreenMode.Windowed;
                case ModeExclusive: return FullScreenMode.ExclusiveFullScreen;
                default: return FullScreenMode.FullScreenWindow;
            }
        }

        /// <summary>저장된 설정을 화면에 적용한다.</summary>
        public static void Apply(SaveData save)
        {
            appliedOnce = true;
            ApplyAction?.Invoke(save.displayWidth, save.displayHeight, ToFullScreenMode(save.windowMode));
        }

        /// <summary>실행 후 처음 한 번만 적용한다 (타이틀에 돌아올 때마다 창이 깜빡이지 않게).</summary>
        public static void ApplyOnce(SaveData save)
        {
            if (!appliedOnce) Apply(save);
        }

        /// <summary>테스트용: 다시 처음 상태로.</summary>
        public static void ResetForTests()
        {
            appliedOnce = false;
            ApplyAction = DefaultApply;
        }

        static void DefaultApply(int width, int height, FullScreenMode mode)
        {
            if (Application.isEditor) return;
            Screen.SetResolution(width, height, mode);
        }
    }
}
