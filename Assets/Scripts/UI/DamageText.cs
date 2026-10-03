using UnityEngine;

namespace Samkuk.UI
{
    /// <summary>위로 떠오르며 사라지는 데미지 숫자 하나.</summary>
    public class DamageText : MonoBehaviour
    {
        const float Life = 0.6f;
        const float RiseSpeed = 1.2f;

        TextMesh text;
        Color baseColor;
        float age;

        public void Setup(TextMesh tm) => text = tm;

        public void Show(Vector2 position, string value, Color color)
        {
            transform.position = position;
            text.text = value;
            baseColor = color;
            text.color = color;
            age = 0f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= Life)
            {
                gameObject.SetActive(false);
                return;
            }

            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);
            var c = baseColor;
            c.a = 1f - age / Life;
            text.color = c;
        }
    }
}
