using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 카메라 주변의 칸(<see cref="TerrainMap.ChunkSize"/> 단위)에만 지형 소품을 만들고, 멀어지면 풀로 돌려보낸다.
    /// 무한한 세계를 이동해도 화면 주변 100개 안팎의 스프라이트만 유지한다. 소품은 장식이라 충돌이 없고
    /// 배경 정렬 레이어에 그려져 적/플레이어/투사체/보석 뒤에 깔린다.
    /// </summary>
    public class TerrainPropSpawner : MonoBehaviour
    {
        const float ViewMargin = 3.5f;   // 화면 가장자리 밖으로 더 만들어 두는 거리 (큰 소품이 걸쳐 보이지 않게)
        const float PropZ = 0.5f;        // 배경(z=1) 바로 앞

        TerrainMap map;
        Transform follow;
        Camera cam;
        Material material;
        int sortingLayerId;

        readonly Dictionary<Vector2Int, List<SpriteRenderer>> active = new Dictionary<Vector2Int, List<SpriteRenderer>>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        readonly List<Vector2Int> removeBuffer = new List<Vector2Int>();

        public TerrainMap Map => map;
        public int ActiveChunkCount => active.Count;
        public int ActivePropCount { get; private set; }
        public int PooledCount => pool.Count;

        /// <summary>맵과 따라갈 대상(보통 카메라)을 정하고 첫 칸들을 만든다. <paramref name="reference"/> 는 재질과 정렬 레이어를 빌려 올 배경 렌더러.</summary>
        public void Initialize(TerrainMap terrainMap, Transform followTarget, SpriteRenderer reference)
        {
            map = terrainMap;
            follow = followTarget;
            if (reference != null)
            {
                material = reference.sharedMaterial;   // 배경과 같은 재질 = 같은 조명을 받는다
                sortingLayerId = reference.sortingLayerID;
            }
            Clear();
            Refresh();
        }

        /// <summary>따라갈 대상을 바꾼다 (카메라가 늦게 정해질 때).</summary>
        public void SetFollow(Transform followTarget) => follow = followTarget;

        void LateUpdate() => Refresh();

        /// <summary>화면 주변에 필요한 칸을 만들고 멀어진 칸을 치운다.</summary>
        public void Refresh()
        {
            if (map == null) return;

            Vector2 center = follow != null ? (Vector2)follow.position : Vector2.zero;
            float halfW = 11f, halfH = 6.5f;
            if (cam == null && follow != null) cam = follow.GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                halfH = cam.orthographicSize;
                halfW = halfH * cam.aspect;
            }

            float c = TerrainMap.ChunkSize;
            int x0 = Mathf.FloorToInt((center.x - halfW - ViewMargin) / c), x1 = Mathf.FloorToInt((center.x + halfW + ViewMargin) / c);
            int y0 = Mathf.FloorToInt((center.y - halfH - ViewMargin) / c), y1 = Mathf.FloorToInt((center.y + halfH + ViewMargin) / c);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var key = new Vector2Int(x, y);
                    if (!active.ContainsKey(key)) SpawnChunk(key);
                }

            // 필요 범위에서 한 칸 이상 벗어난 칸은 치운다 (경계에서 오락가락해도 깜빡이지 않게 여유를 둔다)
            removeBuffer.Clear();
            foreach (var key in active.Keys)
                if (key.x < x0 - 1 || key.x > x1 + 1 || key.y < y0 - 1 || key.y > y1 + 1) removeBuffer.Add(key);
            foreach (var key in removeBuffer) DespawnChunk(key);
        }

        /// <summary>한 칸의 소품을 다시 만든다 (맵 편집기에서 그 칸을 고쳤을 때). 화면 주변에 없는 칸이면 아무것도 하지 않는다.</summary>
        public void RebuildChunk(Vector2Int key)
        {
            if (map == null || !active.ContainsKey(key)) return;
            DespawnChunk(key);
            SpawnChunk(key);
        }

        /// <summary>모든 소품을 풀로 돌려보낸다.</summary>
        public void Clear()
        {
            removeBuffer.Clear();
            foreach (var key in active.Keys) removeBuffer.Add(key);
            foreach (var key in removeBuffer) DespawnChunk(key);
        }

        void SpawnChunk(Vector2Int key)
        {
            var list = new List<SpriteRenderer>();
            Place(list, map.GetChunk(key.x, key.y));   // 직접 고친 칸이면 그 내용, 아니면 자동 생성
            active[key] = list;
            ActivePropCount += list.Count;
        }

        void Place(List<SpriteRenderer> list, List<PropPlacement> placements)
        {
            foreach (var p in placements)
            {
                var sr = Acquire();
                sr.sprite = p.prop.sprite;
                sr.color = p.tinted ? p.tint : Color.white;   // 풀에서 꺼낸 것은 이전 색이 남아 있으므로 항상 다시 정한다
                sr.flipX = p.flipX;
                sr.sortingOrder = p.order;
                sr.transform.position = new Vector3(p.position.x, p.position.y, PropZ);
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, p.rotation);   // 풀에서 꺼낸 것은 이전 회전이 남아 있으므로 항상 다시 정한다
                sr.transform.localScale = new Vector3(p.scale, p.scale * (p.stretchY > 0f ? p.stretchY : 1f), 1f);
                list.Add(sr);
            }
        }

        void DespawnChunk(Vector2Int key)
        {
            if (!active.TryGetValue(key, out var list)) return;
            foreach (var sr in list)
            {
                sr.gameObject.SetActive(false);
                pool.Push(sr);
            }
            ActivePropCount -= list.Count;
            active.Remove(key);
        }

        SpriteRenderer Acquire()
        {
            SpriteRenderer sr;
            if (pool.Count > 0)
            {
                sr = pool.Pop();
                sr.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("Prop");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                if (material != null) sr.sharedMaterial = material;
                sr.sortingLayerID = sortingLayerId;
            }
            return sr;
        }
    }
}
