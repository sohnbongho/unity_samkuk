using System.Collections.Generic;
using Messages;
using UnityEngine;

namespace Samkuk.Coop
{
    /// <summary>적 하나의 서버 상태 + 보간 위치. DataId 는 EnemySpawn 이벤트가 알려 준다(모르면 빈 문자열).</summary>
    public sealed class EnemyTrack
    {
        public const int FlagStunned = 1, FlagWindup = 2, FlagCharging = 4, FlagShotWarning = 8, FlagMoving = 32;
        public int Id;
        public string DataId = "";
        public int Flags;
        public float LastSeen;
        public readonly MotionTrack Motion = new MotionTrack();
        public bool Stunned => (Flags & FlagStunned) != 0;
        public bool WindingUp => (Flags & FlagWindup) != 0;
        public bool Charging => (Flags & FlagCharging) != 0;
        public bool ShotWarning => (Flags & FlagShotWarning) != 0;
        public bool Moving => (Flags & FlagMoving) != 0;
    }

    public sealed class GemTrack
    {
        public int Id;
        public int Value;
        public float LastSeen;
        public bool Attracted;
        public readonly MotionTrack Motion = new MotionTrack();
    }

    /// <summary>플레이어 한 자리. 서버 PlayerState 를 그대로 담고 위치는 보간한다.</summary>
    public sealed class PlayerTrack
    {
        public const int FlagDead = 1, FlagInvulnerable = 2, FlagMoving = 4, FlagSkillInvulnerable = 8;
        public int Slot;
        public bool Seen;
        public PlayerState State = new PlayerState();
        public readonly MotionTrack Motion = new MotionTrack();
        public bool Dead => (State.Flags & FlagDead) != 0;
        public bool Moving => (State.Flags & FlagMoving) != 0;
    }

    /// <summary>
    /// 협동 전투의 클라이언트 쪽 순수 상태: 스냅샷을 받아 플레이어/적/젬의 보간 트랙과 스테이지 상태를 갱신하고 이벤트를 꺼내 준다.
    /// 분할 스냅샷 규칙(players 가 비면 상태를 덮어쓰지 않고 이벤트만), 관심 영역 밖으로 나가 스냅샷에서 빠진 개체의 숨김(<see cref="Expire"/>)을 담당한다.
    /// 유니티 오브젝트를 모르므로 서버 없이 테스트한다.
    /// </summary>
    public sealed class CoopBattleModel
    {
        /// <summary>스냅샷에서 빠진 적/젬을 숨기기까지의 시간 (coop-design.md 2절).</summary>
        public const float HideAfterSeconds = 0.5f;

        public readonly PlayerTrack[] Players = new PlayerTrack[CoopSettings.MaxMembers];
        public readonly Dictionary<int, EnemyTrack> Enemies = new Dictionary<int, EnemyTrack>();
        public readonly Dictionary<int, GemTrack> Gems = new Dictionary<int, GemTrack>();
        public StageState Stage { get; private set; } = new StageState();
        public int MySlot { get; }
        public float SnapshotInterval { get; }
        public int Tick { get; private set; }
        /// <summary>서버 시뮬 경과(ms). 레벨업 일시정지 중에는 멈춘다. 궤도 각도 등 결정적 연출의 기준.</summary>
        public int ElapsedMs { get; private set; }
        /// <summary>마지막 스냅샷을 받은 시각(Apply 에 넘긴 now).</summary>
        public float LastSnapshotAt { get; private set; } = -1f;
        public bool HasSnapshot => LastSnapshotAt >= 0f;

        public PlayerTrack Me => MySlot >= 0 && MySlot < Players.Length ? Players[MySlot] : null;

        public CoopBattleModel(int mySlot, int snapshotRate)
        {
            MySlot = mySlot;
            SnapshotInterval = 1f / Mathf.Max(1, snapshotRate);
            for (int i = 0; i < Players.Length; i++) Players[i] = new PlayerTrack { Slot = i };
        }

        /// <summary>스냅샷을 반영하고 담긴 이벤트를 events 에 덧붙인다. 상태가 비어 있는 뒤쪽 조각이면 이벤트만.</summary>
        public void Apply(BattleSnapshotNoti noti, float now, List<BattleEvent> events)
        {
            if (noti == null) return;
            Tick = noti.Tick;
            if (noti.Players.Count > 0)
            {
                ElapsedMs = noti.ElapsedMs;
                LastSnapshotAt = now;
                if (noti.Stage != null) Stage = noti.Stage;

                foreach (var ps in noti.Players)
                {
                    if (ps.Slot < 0 || ps.Slot >= Players.Length) continue;
                    var p = Players[ps.Slot];
                    p.State = ps;
                    p.Seen = true;
                    p.Motion.Push(NetUnits.Pos(ps.X, ps.Y), SnapshotInterval);
                }

                var e = noti.Enemies;
                if (e != null)
                {
                    int n = Mathf.Min(e.Ids.Count, Mathf.Min(e.Xs.Count, e.Ys.Count));
                    for (int i = 0; i < n; i++)
                    {
                        var track = GetOrAddEnemy(e.Ids[i]);
                        track.Flags = i < e.Flags.Count ? e.Flags[i] : 0;
                        track.LastSeen = now;
                        track.Motion.Push(NetUnits.Pos(e.Xs[i], e.Ys[i]), SnapshotInterval);
                    }
                }

                var g = noti.Gems;
                if (g != null)
                {
                    int n = Mathf.Min(g.Ids.Count, Mathf.Min(g.Xs.Count, g.Ys.Count));
                    for (int i = 0; i < n; i++)
                    {
                        if (!Gems.TryGetValue(g.Ids[i], out var gem)) continue; // 바닥의 젬은 GemSpawn 이 먼저 온다
                        gem.Attracted = true;
                        gem.LastSeen = now;
                        gem.Motion.Push(NetUnits.Pos(g.Xs[i], g.Ys[i]), SnapshotInterval);
                    }
                }
            }
            if (events != null) foreach (var ev in noti.Events) events.Add(ev);
        }

        public EnemyTrack GetOrAddEnemy(int id)
        {
            if (!Enemies.TryGetValue(id, out var track))
            {
                track = new EnemyTrack { Id = id };
                Enemies.Add(id, track);
            }
            return track;
        }

        /// <summary>EnemySpawn: 종류를 알고 위치에 바로 놓는다.</summary>
        public EnemyTrack SpawnEnemy(int id, string dataId, Vector2 position, float now)
        {
            var track = GetOrAddEnemy(id);
            track.DataId = dataId ?? "";
            track.LastSeen = now;
            track.Motion.Snap(position);
            return track;
        }

        public bool RemoveEnemy(int id) => Enemies.Remove(id);

        public GemTrack SpawnGem(int id, Vector2 position, int value, float now)
        {
            if (!Gems.TryGetValue(id, out var gem))
            {
                gem = new GemTrack { Id = id };
                Gems.Add(id, gem);
            }
            gem.Value = value;
            gem.Attracted = false;
            gem.LastSeen = now;
            gem.Motion.Snap(position);
            return gem;
        }

        public bool RemoveGem(int id) => Gems.Remove(id);

        /// <summary>보간을 dt 만큼 진행한다.</summary>
        public void Advance(float dt)
        {
            foreach (var p in Players) if (p.Seen) p.Motion.Advance(dt);
            foreach (var e in Enemies.Values) e.Motion.Advance(dt);
            foreach (var g in Gems.Values) if (g.Attracted) g.Motion.Advance(dt);
        }

        /// <summary>
        /// 관심 영역 밖으로 나가 스냅샷에서 빠진 적(과 끌려가다 사라진 젬)을 숨긴다: HideAfterSeconds 동안 안 보인 적 id 를 removed 에 모으고 지운다.
        /// 바닥에 놓인(끌려오지 않는) 젬은 스냅샷에 안 담기므로 지우지 않는다.
        /// </summary>
        public void Expire(float now, List<int> removedEnemies, List<int> removedGems)
        {
            removedEnemies?.Clear();
            removedGems?.Clear();
            foreach (var e in Enemies.Values)
                if (now - e.LastSeen > HideAfterSeconds) removedEnemies?.Add(e.Id);
            if (removedEnemies != null) foreach (int id in removedEnemies) Enemies.Remove(id);

            foreach (var g in Gems.Values)
                if (g.Attracted && now - g.LastSeen > HideAfterSeconds) removedGems?.Add(g.Id);
            if (removedGems != null) foreach (int id in removedGems) Gems.Remove(id);
        }

        /// <summary>살아 있는 플레이어 중 position 에서 가장 가까운 쪽 방향 (적이 바라볼 방향). 아무도 없으면 (0,-1).</summary>
        public Vector2 FaceDirectionFrom(Vector2 position)
        {
            float best = float.MaxValue;
            Vector2 dir = Vector2.down;
            foreach (var p in Players)
            {
                if (!p.Seen || p.Dead) continue;
                Vector2 to = p.Motion.Current - position;
                float d = to.sqrMagnitude;
                if (d < best && d > 1e-6f) { best = d; dir = to; }
            }
            return dir;
        }

        /// <summary>서버 시뮬 경과 초: 마지막 스냅샷 값에 그 뒤 흐른 시간을 더한다(일시정지 중에는 더하지 않음).</summary>
        public float ElapsedSeconds(float now)
        {
            float elapsed = ElapsedMs / 1000f;
            if (!HasSnapshot || (Stage != null && Stage.Paused)) return elapsed;
            return elapsed + Mathf.Max(0f, now - LastSnapshotAt);
        }

        /// <summary>궤도 칼날 각도(도): 서버와 같은 식 = 경과초 x 회전 속도 + i x 360 / n (coop-design.md).</summary>
        public static float OrbitAngle(float elapsedSeconds, float rotateSpeed, int index, int count)
        {
            float a = (elapsedSeconds * rotateSpeed) % 360f;
            if (a < 0f) a += 360f;
            return a + 360f * index / Mathf.Max(1, count);
        }
    }
}
