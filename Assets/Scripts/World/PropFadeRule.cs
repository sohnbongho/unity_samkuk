using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 겹친 소품 반투명(Step 14-6)의 규칙. 서 있는 소품이 플레이어 앞에 그려져(소품의 발이 플레이어 발보다 아래) 플레이어 몸을 덮으면
    /// 그 소품을 반투명하게 해 내 위치를 놓치지 않게 한다 (뱀서류에서 내 위치를 놓치면 치명적). 순수 계산이라 테스트할 수 있다.
    /// </summary>
    public static class PropFadeRule
    {
        /// <summary>겹쳤을 때의 소품 알파.</summary>
        public const float FadedAlpha = 0.4f;
        /// <summary>알파가 바뀌는 속도 (초당). 갑자기 깜빡이지 않게.</summary>
        public const float FadeSpeed = 6f;
        /// <summary>플레이어 몸 사각형: 발 기준 좌우 반폭과 높이 (걷기 시트 몸 약 1유닛 + 머리 여유).</summary>
        public const float BodyHalfWidth = 0.3f, BodyHeight = 1.1f;

        /// <summary>플레이어 발 위치에서 몸 사각형.</summary>
        public static Rect BodyRect(Vector2 feet) => new Rect(feet.x - BodyHalfWidth, feet.y, BodyHalfWidth * 2f, BodyHeight);

        /// <summary>
        /// 이 소품을 반투명하게 할지. <paramref name="propFootY"/> 가 플레이어 발보다 낮으면(= 앞에 그려지면) 그리고
        /// 소품의 그림 범위가 플레이어 몸과 겹치면 true.
        /// </summary>
        public static bool ShouldFade(float propFootY, Rect propBounds, Vector2 playerFeet)
        {
            if (propFootY >= playerFeet.y) return false;   // 플레이어 뒤에 그려지는 소품은 가리지 않는다
            return propBounds.Overlaps(BodyRect(playerFeet));
        }

        /// <summary>목표 알파로 한 틱 다가간다.</summary>
        public static float Step(float current, bool fade, float dt) =>
            Mathf.MoveTowards(current, fade ? FadedAlpha : 1f, FadeSpeed * dt);
    }
}
