using System;
using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.Enemies
{
    /// <summary>
    /// 모든 활성 적의 추적 이동과 겹침 방지를 한 곳에서 처리한다.
    /// 개별 Update 대신 FixedUpdate 한 번에 처리하고, 해시 그리드로 이웃 탐색을 O(n)에 가깝게 유지한다.
    /// </summary>
    public class EnemyManager : MonoBehaviour
    {
        const int BucketCount = 4096; // 2의 거듭제곱 (비트 마스크용)

        [SerializeField] Transform target;
        [SerializeField, Tooltip("겹친 적을 밀어내는 힘")] float separationStrength = 3f;
        [SerializeField, Tooltip("겹침 방지로 더해지는 최대 속도")] float maxSeparationSpeed = 3f;
        [SerializeField, Tooltip("이 거리보다 멀어진 적은 EnemyTooFar 이벤트로 재배치된다")] float tooFarDistance = 25f;
        [SerializeField, Tooltip("플레이어 반지름: 이 거리까지 접근하면 멈춤")] float playerRadius = 0.4f;
        [SerializeField, Tooltip("해시 그리드 셀 크기. 가장 큰 적의 지름 이상이어야 함")] float cellSize = 1.5f;

        readonly List<Enemy> enemies = new List<Enemy>(512);
        readonly int[] head = new int[BucketCount];
        readonly int[] visited = new int[9];
        int[] next = new int[512];
        Vector2[] positions = new Vector2[512];

        public Transform Target { get => target; set => target = value; }
        public int Count => enemies.Count;
        public IReadOnlyList<Enemy> Active => enemies;

        /// <summary>플레이어에게서 너무 멀어진 적이 있을 때 호출된다 (재배치/제거는 구독자가 결정).</summary>
        public event Action<Enemy> EnemyTooFar;

        void Start()
        {
            if (target == null)
            {
                var pc = FindFirstObjectByType<Samkuk.Player.PlayerController>();
                if (pc != null) target = pc.transform;
            }
        }

        public void Register(Enemy e)
        {
            if (e.ManagerIndex >= 0) return;
            e.ManagerIndex = enemies.Count;
            enemies.Add(e);
        }

        public void Unregister(Enemy e)
        {
            int idx = e.ManagerIndex;
            if (idx < 0) return;
            int last = enemies.Count - 1;
            var moved = enemies[last];
            enemies[idx] = moved;
            moved.ManagerIndex = idx;
            enemies.RemoveAt(last);
            e.ManagerIndex = -1;
        }

        void FixedUpdate()
        {
            int n = enemies.Count;
            if (n == 0 || target == null) return;

            EnsureCapacity(n);
            Array.Fill(head, -1);

            // 1) 위치 캐시 + 해시 그리드 구성
            float invCell = 1f / cellSize;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = enemies[i].Body.position;
                positions[i] = p;
                int b = Bucket(Mathf.FloorToInt(p.x * invCell), Mathf.FloorToInt(p.y * invCell));
                next[i] = head[b];
                head[b] = i;
            }

            Vector2 tp = target.position;
            float tooFarSqr = tooFarDistance * tooFarDistance;

            // 2) 추적 + 겹침 방지
            for (int i = 0; i < n; i++)
            {
                Enemy e = enemies[i];
                Vector2 p = positions[i];
                float r = e.Radius;

                Vector2 toPlayer = tp - p;
                float distSqr = toPlayer.sqrMagnitude;

                if (distSqr > tooFarSqr)
                {
                    EnemyTooFar?.Invoke(e);
                    continue;
                }

                Vector2 desired = Vector2.zero;
                float stop = playerRadius + r;
                if (distSqr > stop * stop)
                    desired = toPlayer / Mathf.Sqrt(distSqr) * e.Data.moveSpeed;

                Vector2 push = ComputePush(i, p, r);
                Vector2 sep = Vector2.ClampMagnitude(push * separationStrength, maxSeparationSpeed);

                Vector2 vel = desired + sep;
                e.Body.linearVelocity = vel;
                if (desired.x > 0.05f) e.SetFacing(false);
                else if (desired.x < -0.05f) e.SetFacing(true);
            }
        }

        Vector2 ComputePush(int self, Vector2 p, float r)
        {
            float invCell = 1f / cellSize;
            int cx = Mathf.FloorToInt(p.x * invCell);
            int cy = Mathf.FloorToInt(p.y * invCell);

            Vector2 push = Vector2.zero;
            int visitedCount = 0;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int b = Bucket(cx + dx, cy + dy);

                    // 해시 충돌로 같은 버킷을 두 번 방문하지 않도록 방지
                    bool seen = false;
                    for (int v = 0; v < visitedCount; v++)
                    {
                        if (visited[v] == b) { seen = true; break; }
                    }
                    if (seen) continue;
                    visited[visitedCount++] = b;

                    for (int j = head[b]; j >= 0; j = next[j])
                    {
                        if (j == self) continue;

                        Vector2 d = p - positions[j];
                        float minDist = r + enemies[j].Radius;
                        float dSqr = d.sqrMagnitude;
                        if (dSqr >= minDist * minDist) continue;

                        if (dSqr < 1e-6f)
                        {
                            // 완전히 같은 위치: 인덱스 기반의 결정적 방향으로 분리
                            float a = (self * 2.399963f) + (j * 0.618f);
                            push += new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        }
                        else
                        {
                            float dist = Mathf.Sqrt(dSqr);
                            push += d / dist * ((minDist - dist) / minDist);
                        }
                    }
                }
            }
            return push;
        }

        static int Bucket(int x, int y)
        {
            unchecked
            {
                return (x * 73856093 ^ y * 19349663) & (BucketCount - 1);
            }
        }

        void EnsureCapacity(int n)
        {
            if (next.Length >= n) return;
            int size = Mathf.NextPowerOfTwo(n);
            Array.Resize(ref next, size);
            Array.Resize(ref positions, size);
        }
    }
}
