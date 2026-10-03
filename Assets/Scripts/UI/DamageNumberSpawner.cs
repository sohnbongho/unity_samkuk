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

            Vector2 pos = enemy.Position + new Vector2(Random.Range(-0.2f, 0.2f), enemy.Radius + 0.1f);
            t.Show(pos, Format(amount), color);
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
