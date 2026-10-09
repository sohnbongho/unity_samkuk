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
            /// <summary>월드 정렬(Step 14-4): 캐릭터와 서 있는 소품이 함께 쓰며 발 위치(y)로 앞뒤가 정해진다. Pickup 과 Enemy 사이. 셋업 Step 14-4 가 만든다.</summary>
            public const string World = "World";
            public const string Enemy = "Enemy";
            public const string Player = "Player";
            public const string Projectile = "Projectile";
            public const string Effect = "Effect";
        }
    }
}
