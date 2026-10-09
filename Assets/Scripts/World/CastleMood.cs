using Samkuk.Data;

namespace Samkuk.World
{
    /// <summary>성 그림과 전투 맵이 공유하는 시간대. 값 순서는 성 배경 생성기(<c>tools/castle_art/CastleArt.cs</c>)의 mood(0 낮, 1 해질녘, 2 새벽)와 같다.</summary>
    public enum TimeOfDay { Day = 0, Dusk = 1, Dawn = 2 }

    /// <summary>
    /// 성의 시간대를 정한다. 성 배경 그림은 생성기가 성 아이디의 FNV 해시를 시드로 <c>new Random(seed).Next(3)</c> 를 첫 호출로 뽑아
    /// 낮/해질녘/새벽을 고르므로, 여기서 똑같이 계산하면 전투 맵의 전역광 색조가 내정 성 화면의 하늘과 맞는다.
    /// (.NET 의 시드 있는 Random 은 플랫폼이 달라도 같은 수열이다.) 성 데이터에 값을 저장하지 않는 이유: 그림을 다시 만들어도 어긋나지 않게.
    /// </summary>
    public static class CastleMood
    {
        /// <summary>성이 없으면(성 없이 시작한 판, 자유 전투) 낮.</summary>
        public static TimeOfDay Of(CastleData castle)
        {
            if (castle == null || string.IsNullOrEmpty(castle.id) || castle.id == MapStore.FreeBattleId) return TimeOfDay.Day;
            return Of(castle.id);
        }

        public static TimeOfDay Of(string castleId)
        {
            var rng = new System.Random(Seed(castleId));
            return (TimeOfDay)rng.Next(3);
        }

        /// <summary>생성기와 같은 FNV-1a 해시 (string.GetHashCode 는 실행마다 달라질 수 있다).</summary>
        public static int Seed(string s)
        {
            uint h = 2166136261;
            foreach (char ch in s) { h ^= ch; h *= 16777619; }
            return (int)(h & 0x7fffffff);
        }

        public static string Label(TimeOfDay time)
        {
            switch (time)
            {
                case TimeOfDay.Dusk: return "해질녘";
                case TimeOfDay.Dawn: return "새벽";
                default: return "낮";
            }
        }
    }
}
