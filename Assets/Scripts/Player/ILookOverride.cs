using UnityEngine;

namespace Samkuk.Player
{
    /// <summary>
    /// 잠깐 특정 방향을 바라보게 하는 요청(Step 10-9 휘두르기). 무기가 적 쪽으로 휘두를 때 몸도 그쪽을 보게 해
    /// 달아나면서 등 뒤로 베는 어색함을 줄인다. 주인공(<see cref="PlayerAnimator"/>)과 아군이 구현한다.
    /// </summary>
    public interface ILookOverride
    {
        /// <summary>seconds 동안 direction 쪽(4방향 중 가까운 쪽)을 바라본다. 이동 입력보다 우선한다.</summary>
        void Look(Vector2 direction, float seconds);
    }
}
