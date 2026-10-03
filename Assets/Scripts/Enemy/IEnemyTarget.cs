using UnityEngine;

namespace Samkuk.Enemies
{
    /// <summary>
    /// 플레이어 말고 적이 노릴 수 있는 대상(아군). 적은 플레이어와 살아 있는 아군 중 가장 가까운 쪽을 쫓고,
    /// 닿거나 투사체에 맞으면 <see cref="TryContactDamage"/> 로 피해를 준다.
    /// </summary>
    public interface IEnemyTarget
    {
        Vector2 Position { get; }
        /// <summary>충돌 반지름 (적이 이 거리까지 접근하면 멈추고 접촉 피해를 준다).</summary>
        float Radius { get; }
        /// <summary>지금 노릴 수 있는가 (쓰러져 있으면 false).</summary>
        bool IsTargetable { get; }
        /// <summary>접촉/투사체 피해. 무적 시간 중이면 무시하고 false.</summary>
        bool TryContactDamage(float amount);
    }
}
