using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.Stages;
using UnityEngine;

namespace Samkuk.Meta
{
    /// <summary>이번 판의 통계: 처치 수, 생존 시간, 도달 레벨.</summary>
    public class RunStats : MonoBehaviour
    {
        [SerializeField] StageController stage;
        [SerializeField] PlayerExperience experience;

        float fallbackSeconds;

        public StageController Stage { get => stage; set => stage = value; }
        public PlayerExperience Experience { get => experience; set => experience = value; }

        public int Kills { get; private set; }
        /// <summary>스테이지가 있으면 그 경과 시간, 없으면 이 컴포넌트가 직접 센 시간.</summary>
        public float Seconds => stage != null ? stage.Elapsed : fallbackSeconds;
        public int Level => experience != null ? experience.Level : 1;

        void OnEnable() => Enemy.Died += OnEnemyDied;
        void OnDisable() => Enemy.Died -= OnEnemyDied;

        void OnEnemyDied(Enemy e) => Kills++;

        void Update()
        {
            if (stage == null) fallbackSeconds += Time.deltaTime;
        }
    }
}
