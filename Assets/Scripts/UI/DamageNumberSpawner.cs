using Samkuk.Core;
using Samkuk.Enemies;
using UnityEngine;

namespace Samkuk.UI
{
    /// <summary>Enemy.Damaged 이벤트를 받아 월드 공간에 떠오르는 데미지 숫자를 표시한다 (풀링).</summary>
    public class DamageNumberSpawner : MonoBehaviour
    {
        [SerializeField] int poolSize = 80;
        [SerializeField] Color color = new Color(1f, 0.92f, 0.3f);

        DamageText[] pool;
        int cursor;
        Font font;
        static string[] cache;

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 32);

            pool = new DamageText[poolSize];
            for (int i = 0; i < poolSize; i++) pool[i] = CreateText(i);
        }

        void OnEnable() => Enemy.Damaged += OnEnemyDamaged;
        void OnDisable() => Enemy.Damaged -= OnEnemyDamaged;

        void OnEnemyDamaged(Enemy enemy, float amount)
        {
            // 가장 오래된 것부터 재사용 (링 버퍼)
            var t = pool[cursor];
            cursor = (cursor + 1) % pool.Length;

            Vector2 pos = enemy.Position + new Vector2(Random.Range(-0.2f, 0.2f), enemy.Radius + 0.8f);   // 기준점이 발이라(Step 14-4) 몸 위에 뜨도록
            GetStyle(amount, color, out Color tierColor, out float tierScale);
            t.Show(pos, Format(amount), tierColor, tierScale);
        }

        /// <summary>
        /// 피해량에 따른 숫자 모양: 12 미만은 기본 색, 30 미만은 주황(1.3배), 그 이상은 붉은 주황(1.6배).
        /// 큰 한 방이 한눈에 보이도록 한다.
        /// </summary>
        public static void GetStyle(float amount, Color baseColor, out Color color, out float scale)
        {
            if (amount >= 30f) { color = new Color(1f, 0.35f, 0.25f); scale = 1.6f; }
            else if (amount >= 12f) { color = new Color(1f, 0.6f, 0.2f); scale = 1.3f; }
            else { color = baseColor; scale = 1f; }
        }

        DamageText CreateText(int index)
        {
            var go = new GameObject($"DamageText{index}");
            go.transform.SetParent(transform, false);

            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = 0.07f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingLayerName = GameLayers.Sorting.Effect;
            mr.sortingOrder = 10;

            var dt = go.AddComponent<DamageText>();
            dt.Setup(tm);
            go.SetActive(false);
            return dt;
        }

        static string Format(float amount)
        {
            int v = Mathf.Max(1, Mathf.RoundToInt(amount));
            if (v >= 1000) return v.ToString();
            cache ??= new string[1000];
            return cache[v] ??= v.ToString();
        }
    }
}
