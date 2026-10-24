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

        public void Show(Vector2 position, string value, Color color, float scale = 1f)
        {
            transform.position = position;
            transform.rotation = Samkuk.World.TiltView.StandingRotation;   // 2.5D 틸트면 카메라를 향해 세운다
            transform.localScale = Vector3.one * scale;
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

            transform.position += Samkuk.World.TiltView.Up * (RiseSpeed * Time.deltaTime);   // 2.5D 틸트면 화면 위쪽으로 떠오른다
            var c = baseColor;
            c.a = 1f - age / Life;
            text.color = c;
        }
    }
}
