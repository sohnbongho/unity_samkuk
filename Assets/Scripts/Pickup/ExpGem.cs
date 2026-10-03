using UnityEngine;

namespace Samkuk.Pickups
{
    /// <summary>경험치 보석 하나의 상태. 이동/획득은 ExpGemManager가 처리한다.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ExpGem : MonoBehaviour
    {
        SpriteRenderer sr;

        public int Value { get; private set; }
        /// <summary>플레이어에게 빨려가는 중인가.</summary>
        public bool Attracted { get; private set; }
        public float Speed { get; internal set; }

        void Awake() => sr = GetComponent<SpriteRenderer>();

        public void Init(Vector2 position, int value)
        {
            Value = value;
            Attracted = false;
            Speed = 0f;
            transform.position = position;
            sr.color = TintFor(value);
        }

        public void Attract(float startSpeed)
        {
            Attracted = true;
            Speed = startSpeed;
        }

        /// <summary>경험치 양에 따라 색을 달리한다 (초록 → 파랑 → 보라).</summary>
        static Color TintFor(int value)
        {
            if (value >= 20) return new Color(0.75f, 0.4f, 1f);
            if (value >= 5) return new Color(0.4f, 0.7f, 1f);
            return Color.white;
        }
    }
}
