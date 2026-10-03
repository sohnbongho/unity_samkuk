using UnityEngine;

namespace Samkuk.Core
{
    /// <summary>프로젝트에서 사용하는 레이어/정렬 레이어 이름 상수 (TagManager.asset과 일치해야 함).</summary>
    public static class GameLayers
    {
        public const string Player = "Player";
        public const string Enemy = "Enemy";
        public const string PlayerProjectile = "PlayerProjectile";
        public const string Pickup = "Pickup";

        public static int PlayerMask => LayerMask.GetMask(Player);
        public static int EnemyMask => LayerMask.GetMask(Enemy);
        public static int PickupMask => LayerMask.GetMask(Pickup);

        public static class Sorting
        {
            public const string Background = "Background";
            public const string Pickup = "Pickup";
            public const string Enemy = "Enemy";
            public const string Player = "Player";
            public const string Projectile = "Projectile";
            public const string Effect = "Effect";
        }
    }
}
