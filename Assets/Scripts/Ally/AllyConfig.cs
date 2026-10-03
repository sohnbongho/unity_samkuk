using UnityEngine;

namespace Samkuk.Allies
{
    /// <summary>
    /// 아군(동행 장수) 규칙의 수치 모음. 밸런스를 바꿀 때는 이 파일 한 곳만 고친다.
    /// 아군은 자기 장수의 시작 무기로 알아서 싸우고, 적에게 공격받아 쓰러지면 잠시 뒤 되살아난다.
    /// </summary>
    public static class AllyConfig
    {
        /// <summary>한 판에 데려갈 수 있는 아군 수.</summary>
        public const int MaxAllies = 2;

        /// <summary>아군 기본 체력 (장수의 체력 보정이 더해진다). 플레이어의 기본 체력은 100.</summary>
        public const float BaseHp = 70f;
        /// <summary>아군 무기 공격력 배율 (장수의 공격력 보정에 곱해진다). 주인공이 중심이 되도록 약하게.</summary>
        public const float DamageFactor = 0.6f;
        /// <summary>아군이 받는 피해 배율 (적의 접촉 피해/투사체 피해에 곱해진다).</summary>
        public const float DamageTakenFactor = 0.7f;
        /// <summary>피격 후 무적 시간(초).</summary>
        public const float HitInvulnerableSeconds = 0.5f;

        /// <summary>쓰러진 뒤 되살아나기까지 걸리는 시간(초).</summary>
        public const float ReviveSeconds = 15f;
        /// <summary>되살아날 때 회복하는 체력 비율.</summary>
        public const float ReviveHpRatio = 0.5f;
        /// <summary>되살아난 직후 무적 시간(초).</summary>
        public const float ReviveInvulnerableSeconds = 2f;

        /// <summary>플레이어의 이동 속도에 곱해지는 따라가기 속도 배율 (뒤처지지 않도록 약간 빠르게).</summary>
        public const float FollowSpeedMultiplier = 1.15f;
        /// <summary>플레이어와 이 거리보다 멀어지면 곁으로 순간이동한다.</summary>
        public const float TeleportDistance = 10f;
        /// <summary>아군의 충돌 반지름.</summary>
        public const float BodyRadius = 0.4f;

        /// <summary>플레이어 레벨이 이만큼 오를 때마다 아군 무기도 1레벨 오른다.</summary>
        public const int PlayerLevelsPerWeaponLevel = 3;

        /// <summary>플레이어 곁에서 서 있는 자리 (슬롯 순서). 플레이어 위치 기준 상대 좌표.</summary>
        public static readonly Vector2[] Offsets =
        {
            new Vector2(-1.4f, -0.5f),
            new Vector2(1.4f, -0.5f),
            new Vector2(0f, -1.6f),
            new Vector2(0f, 1.2f),
        };

        public static Vector2 OffsetOf(int slot) => Offsets[Mathf.Abs(slot) % Offsets.Length];

        /// <summary>플레이어 레벨에 맞는 아군 무기 레벨.</summary>
        public static int WeaponLevelFor(int playerLevel) => 1 + Mathf.Max(0, playerLevel - 1) / PlayerLevelsPerWeaponLevel;
    }
}
