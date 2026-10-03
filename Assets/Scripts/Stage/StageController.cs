using System;
using System.Collections.Generic;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using UnityEngine;

namespace Samkuk.Stages
{
    /// <summary>
    /// 한 판의 진행을 관리한다: 경과 시간에 따라 웨이브(스폰 설정)를 바꾸고,
    /// 정해진 시간에 엘리트/보스를 출현시키며, duration에 도달하면 클리어 처리한다.
    /// 시간은 Time.deltaTime 기준이라 일시정지/레벨업 선택 중에는 흐르지 않는다.
    /// </summary>
    public class StageController : MonoBehaviour
    {
        [SerializeField] StageData stage;
        [SerializeField] EnemySpawner spawner;
        [SerializeField] PlayerHealth health;

        readonly List<Wave> waves = new List<Wave>();
        readonly List<StageEvent> events = new List<StageEvent>();
        int waveIndex = -1;
        int nextEvent;
        bool started;

        public StageData Stage { get => stage; set { stage = value; started = false; } }
        public EnemySpawner Spawner { get => spawner; set => spawner = value; }
        public PlayerHealth Health { get => health; set => health = value; }

        public float Elapsed { get; private set; }
        public float Duration => stage != null ? stage.duration : 0f;
        public float Remaining => Mathf.Max(0f, Duration - Elapsed);
        /// <summary>현재 웨이브 번호 (1부터, 시작 전에는 0).</summary>
        public int WaveNumber => waveIndex + 1;
        public string WaveName => waveIndex >= 0 && waveIndex < waves.Count ? waves[waveIndex].name : "";
        public bool IsCleared { get; private set; }
        /// <summary>가장 최근에 출현한 보스 (없으면 null).</summary>
        public Enemy Boss { get; private set; }

        public event Action<int> WaveChanged;
        public event Action<StageEvent> EventTriggered;
        public event Action<Enemy> BossSpawned;
        public event Action Cleared;

        void Start() => EnsureStarted();

        void Update() => Tick(Time.deltaTime);

        /// <summary>시간을 dt초 진행시킨다 (Update가 호출하며, 테스트/디버그에서 직접 호출 가능).</summary>
        public void Tick(float dt)
        {
            if (stage == null || IsCleared) return;
            if (health != null && health.IsDead) return;

            EnsureStarted();

            Elapsed += dt;
            UpdateWave();
            ProcessEvents();

            if (Elapsed >= stage.duration) Clear();
        }

        void EnsureStarted()
        {
            if (started || stage == null) return;
            started = true;

            Elapsed = 0f;
            waveIndex = -1;
            nextEvent = 0;
            IsCleared = false;
            Boss = null;

            waves.Clear();
            waves.AddRange(stage.waves);
            waves.Sort((a, b) => a.startTime.CompareTo(b.startTime));

            events.Clear();
            events.AddRange(stage.events);
            events.Sort((a, b) => a.time.CompareTo(b.time));

            UpdateWave();
        }

        void UpdateWave()
        {
            int index = -1;
            for (int i = 0; i < waves.Count; i++)
            {
                if (waves[i].startTime <= Elapsed) index = i;
                else break;
            }
            if (index < 0 || index == waveIndex) return;

            waveIndex = index;
            ApplyWave(waves[index]);
            WaveChanged?.Invoke(WaveNumber);
        }

        void ApplyWave(Wave wave)
        {
            if (spawner == null) return;

            var entries = new List<EnemySpawner.Entry>(wave.enemies.Count);
            foreach (var e in wave.enemies)
                entries.Add(new EnemySpawner.Entry { data = e.data, weight = e.weight });

            spawner.ReplaceSpawnTable(entries);
            spawner.SpawnPerSecond = wave.spawnPerSecond;
            spawner.MaxAlive = wave.maxAlive;
        }

        void ProcessEvents()
        {
            while (nextEvent < events.Count && events[nextEvent].time <= Elapsed)
                Fire(events[nextEvent++]);
        }

        void Fire(StageEvent ev)
        {
            if (spawner != null && ev.enemy != null)
            {
                for (int i = 0; i < Mathf.Max(1, ev.count); i++)
                {
                    var e = spawner.SpawnRing(ev.enemy);
                    if (ev.isBoss && e != null)
                    {
                        Boss = e;
                        BossSpawned?.Invoke(e);
                    }
                }
            }
            EventTriggered?.Invoke(ev);
        }

        void Clear()
        {
            IsCleared = true;
            if (spawner != null) spawner.autoSpawn = false;
            Cleared?.Invoke();
        }
    }
}
