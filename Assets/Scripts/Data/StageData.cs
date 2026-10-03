using System;
using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.Data
{
    [Serializable]
    public struct WaveEnemy
    {
        public EnemyData data;
        public float weight;
    }

    /// <summary>시간대별 스폰 설정. startTime 이후 다음 웨이브가 시작될 때까지 적용된다.</summary>
    [Serializable]
    public class Wave
    {
        public string name = "웨이브";
        public float startTime;
        [Tooltip("초당 스폰 수")] public float spawnPerSecond = 5f;
        [Tooltip("동시에 존재할 수 있는 최대 적 수")] public int maxAlive = 150;
        public List<WaveEnemy> enemies = new List<WaveEnemy>();
    }

    /// <summary>정해진 시간에 한 번 일어나는 이벤트 (엘리트/보스 출현).</summary>
    [Serializable]
    public class StageEvent
    {
        public float time;
        public EnemyData enemy;
        public int count = 1;
        [Tooltip("true면 보스: 체력바 표시")] public bool isBoss;
        [Tooltip("화면에 띄울 문구")] public string message;
    }

    /// <summary>한 판의 구성: 클리어까지의 시간, 웨이브, 이벤트.</summary>
    [CreateAssetMenu(menuName = "Samkuk/Stage Data", fileName = "Stage_New")]
    public class StageData : ScriptableObject
    {
        public string displayName = "황건적의 난";
        [Tooltip("이 시간(초)을 버티면 클리어")] public float duration = 60f;
        public List<Wave> waves = new List<Wave>();
        public List<StageEvent> events = new List<StageEvent>();
    }
}
