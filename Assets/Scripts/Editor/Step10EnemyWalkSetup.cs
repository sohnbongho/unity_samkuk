using System.Collections.Generic;
using System.IO;
using System.Text;
using Samkuk.Data;
using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 10-7: 적 걷기 애니메이션. <c>Assets/Sprites/EnemyWalk/&lt;적 에셋 이름&gt;_Walk.png</c>
    /// (예: Enemy_Soldier_Walk.png)를 <see cref="EnemyData.walkSheet"/> 에 연결한다.
    /// 시트 규격과 재생 방식은 장수와 같다 (docs/HERO_WALK_SHEETS.md, <c>HeroSpriteSet</c>).
    /// 이미 지정된 시트는 덮어쓰지 않고, 크기(walkPixelsPerUnit)는 처음 연결할 때만 아래 표 값으로 정한다.
    /// </summary>
    public static class Step10EnemyWalkSetup
    {
        public const string SheetDir = "Assets/Sprites/EnemyWalk";
        const string EnemyDir = "Assets/ScriptableObjects/Enemies";

        // 처음 연결할 때 정하는 크기. 클수록 작게 보인다 (한 칸 96픽셀 / 값 = 월드 크기, 여기에 적의 scale 이 곱해진다).
        static readonly Dictionary<string, float> PixelsPerUnit = new Dictionary<string, float>
        {
            { "Enemy_Soldier", 112f },
            { "Enemy_Scout", 112f },
            { "Enemy_YellowArcher", 112f },
            { "Enemy_YellowTurbanGeneral", 104f },
            { "Enemy_DongzhuoInfantry", 112f },
            { "Enemy_DongzhuoCrossbow", 112f },
            { "Enemy_LvbuElite", 108f },
            { "Enemy_LvbuArcher", 112f },
            { "Enemy_XiliangCavalry", 100f },
            { "Boss_Lvbu", 100f },
        };

        [MenuItem("Samkuk/Step 10-7 - Link Enemy Walk Sheets")]
        public static void Run()
        {
            Directory.CreateDirectory(SheetDir);
            AssetDatabase.Refresh();

            var ok = new StringBuilder();
            var none = new StringBuilder();
            int linked = 0, missing = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:EnemyData", new[] { EnemyDir }))
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
                if (enemy == null) continue;

                string expected = $"{SheetDir}/{enemy.name}_Walk.png";
                var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(expected);
                if (sheet == null)
                {
                    none.Append($"\n  - {enemy.displayName}: {expected}");
                    missing++;
                    continue;
                }

                // 직접 지정한 시트와 크기는 그대로 둔다
                if (enemy.walkSheet == null)
                {
                    enemy.walkSheet = sheet;
                    if (PixelsPerUnit.TryGetValue(enemy.name, out float ppu)) enemy.walkPixelsPerUnit = ppu;
                    EditorUtility.SetDirty(enemy);
                }
                ok.Append($"\n  - {enemy.displayName}: {expected}");
                linked++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Samkuk] Step 10-7 완료: 적 걷기 시트 연결 {linked}종, 없음 {missing}종" +
                      (linked > 0 ? $"\n연결됨:{ok}" : "") +
                      (missing > 0 ? $"\n시트 없음(기존 단색 스프라이트 사용):{none}" : ""));
        }
    }
}
