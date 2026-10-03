using System;
using UnityEngine;

namespace Samkuk.Player
{
    /// <summary>경험치(군공)와 레벨. 여러 레벨이 한 번에 오를 수 있다.</summary>
    public class PlayerExperience : MonoBehaviour
    {
        /// <summary>1레벨에서 2레벨로 가는 데 필요한 경험치.</summary>
        public const int BaseRequired = 7;
        /// <summary>레벨이 하나 오를 때마다 늘어나는 필요 경험치. 1분 스테이지에서 약 12레벨이 되도록 맞춘 값(BalanceModel 참고).</summary>
        public const int RequiredStep = 6;

        PlayerStats stats;

        public int Level { get; private set; } = 1;
        public int Current { get; private set; }
        public int ToNext => RequiredFor(Level);

        /// <summary>(레벨, 현재 경험치, 다음 레벨까지 필요한 경험치)</summary>
        public event Action<int, int, int> Changed;
        /// <summary>레벨이 오를 때마다(새 레벨) 한 번씩 호출된다.</summary>
        public event Action<int> LevelUp;

        void Awake() => stats = GetComponent<PlayerStats>();
        void Start() => Changed?.Invoke(Level, Current, ToNext);

        /// <summary>해당 레벨에서 다음 레벨로 가기 위해 필요한 경험치.</summary>
        public static int RequiredFor(int level) => BaseRequired + (level - 1) * RequiredStep;

        /// <summary>경험치를 추가한다 (획득량 배율 적용). 실제로 더해진 양을 반환.</summary>
        public int AddExp(float amount)
        {
            if (amount <= 0f) return 0;

            float mult = stats != null ? stats.ExpMultiplier : 1f;
            int gained = Mathf.Max(1, Mathf.RoundToInt(amount * mult));
            Current += gained;

            while (Current >= ToNext)
            {
                Current -= ToNext;
                Level++;
                LevelUp?.Invoke(Level);
            }
            Changed?.Invoke(Level, Current, ToNext);
            return gained;
        }
    }
}
