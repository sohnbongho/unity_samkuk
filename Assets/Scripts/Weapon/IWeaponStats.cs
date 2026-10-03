namespace Samkuk.Weapons
{
    /// <summary>무기가 공격력/쿨다운 계산에 쓰는 능력치. 플레이어(PlayerStats)와 아군이 같은 무기를 쓸 수 있게 한다.</summary>
    public interface IWeaponStats
    {
        float DamageMultiplier { get; }
        float CooldownMultiplier { get; }
    }
}
