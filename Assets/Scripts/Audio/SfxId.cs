using Samkuk.Data;

namespace Samkuk.Audio
{
    /// <summary>효과음 종류. None은 "소리 없음"이다.</summary>
    public enum SfxId
    {
        None,
        Hit,        // 적 피격
        Kill,       // 적 처치
        PlayerHurt, // 플레이어 피격
        Gem,        // 경험치 보석 획득
        LevelUp,    // 레벨업
        Evolve,     // 무기 진화
        Skill,      // 장수 고유 스킬
        Slash,      // 검 베기
        Thrust,     // 창 찌르기
        Arrow,      // 활/화살비
        Thunder,    // 뇌격
        Explosion,  // 충격파
        Fire,       // 화계
        EnemyShot,  // 적 궁병 발사
        Boss,       // 보스 등장
        Click,      // UI 선택
        Buy,        // 영구 강화 구매
        Victory,    // 스테이지 클리어
        Defeat      // 게임 오버
    }

    public static class SfxMap
    {
        /// <summary>무기 종류별 공격 효과음 (회전 무기는 지속 타격이라 소리를 내지 않는다).</summary>
        public static SfxId ForWeapon(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Arrow: return SfxId.Arrow;
                case WeaponType.Slash: return SfxId.Slash;
                case WeaponType.Thrust: return SfxId.Thrust;
                case WeaponType.FireZone: return SfxId.Fire;
                case WeaponType.Lightning: return SfxId.Thunder;
                case WeaponType.Rain: return SfxId.Arrow;
                case WeaponType.Nova: return SfxId.Explosion;
                default: return SfxId.None; // Orbit
            }
        }
    }
}
