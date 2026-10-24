using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Samkuk.World
{
    /// <summary>
    /// 카메라 주변 칸마다 메시 하나를 두고 멀어지면 끄는(풀) 공통 틀(Step 16-2/16-3). 절벽(<see cref="TerrainCliffs"/>)과 흙길(<see cref="TerrainPaths"/>)이 쓴다.
    /// 2.5D 틸트일 때만 보인다 — 2D 렌더러에는 이 셰이더의 패스가 없어 그리지 못한다. 켜고 끌 때 <see cref="TiltView.Changed"/> 로 따라온다.
    /// 자식은 칸의 메시를 버퍼에 쓰는 <see cref="Build"/> 와 재질 둘(서브메시 0·1)을 준다.
    /// </summary>
    public abstract class ChunkMeshes : MonoBehaviour
    {
        const float ViewMargin = 4f;

        protected TerrainMap map;
        Transform follow;
        Camera cam;
        Material[] materials;
        readonly Dictionary<Vector2Int, MeshFilter> active = new Dictionary<Vector2Int, MeshFilter>();
        readonly Stack<MeshFilter> pool = new Stack<MeshFilter>();
        readonly List<Vector2Int> removeBuffer = new List<Vector2Int>();
        readonly CliffMeshBuilder.Buffers buffers = new CliffMeshBuilder.Buffers();
        bool visible;

        public TerrainMap Map => map;
        /// <summary>지금 살펴 둔 칸 수 (메시가 없는 칸은 자리만 기억한다).</summary>
        public int ActiveChunkCount => active.Count;
        public bool Visible => visible;

        /// <summary>칸의 메시를 버퍼에 쓴다 (버퍼는 비어 있다). 아무것도 쓰지 않으면 그 칸은 메시가 없다.</summary>
        protected abstract void Build(Vector2Int chunk, CliffMeshBuilder.Buffers b);

        /// <summary>맵·따라갈 대상·재질(서브메시 0, 1)을 정하고 첫 칸들을 만든다.</summary>
        protected void Setup(TerrainMap terrainMap, Transform followTarget, Material first, Material second)
        {
            map = terrainMap;
            follow = followTarget;
            materials = first != null && second != null ? new[] { first, second } : null;
            Clear();
            Refresh();
        }

        public void SetFollow(Transform followTarget) => follow = followTarget;

        void OnEnable() => TiltView.Changed += OnTiltChanged;
        void OnDisable() => TiltView.Changed -= OnTiltChanged;

        void OnTiltChanged()
        {
            Clear();
            Refresh();
        }

        void LateUpdate() => Refresh();

        /// <summary>화면 주변 칸의 메시를 만들고 멀어진 칸을 치운다. 틸트가 꺼져 있으면(맵 편집기, 2D) 아무것도 보이지 않는다.</summary>
        public void Refresh()
        {
            visible = TiltView.Active && materials != null && map != null;
            if (!visible) { if (active.Count > 0) Clear(); return; }

            if (cam == null && follow != null) cam = follow.GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            var view = TerrainPropSpawner.ViewRect(follow, cam);
            float c = TerrainMap.ChunkSize;
            int x0 = Mathf.FloorToInt((view.xMin - ViewMargin) / c), x1 = Mathf.FloorToInt((view.xMax + ViewMargin) / c);
            int y0 = Mathf.FloorToInt((view.yMin - ViewMargin) / c), y1 = Mathf.FloorToInt((view.yMax + ViewMargin) / c);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var key = new Vector2Int(x, y);
                    if (!active.ContainsKey(key)) Spawn(key);
                }

            removeBuffer.Clear();
            foreach (var key in active.Keys)
                if (key.x < x0 - 1 || key.x > x1 + 1 || key.y < y0 - 1 || key.y > y1 + 1) removeBuffer.Add(key);
            foreach (var key in removeBuffer) Despawn(key);
        }

        /// <summary>한 칸의 메시를 다시 만든다 (맵 편집기에서 그 칸을 고쳤을 때). 화면 주변에 없는 칸이면 아무것도 하지 않는다.</summary>
        public void RebuildChunk(Vector2Int key)
        {
            if (!active.ContainsKey(key)) return;
            Despawn(key);
            Spawn(key);
        }

        public void Clear()
        {
            removeBuffer.Clear();
            foreach (var key in active.Keys) removeBuffer.Add(key);
            foreach (var key in removeBuffer) Despawn(key);
        }

        void Spawn(Vector2Int key)
        {
            buffers.Clear();
            Build(key, buffers);
            if (buffers.IsEmpty) { active[key] = null; return; }   // 빈 칸: 메시 없이 자리만 기억해 매 프레임 다시 계산하지 않는다

            var mf = Acquire();
            CliffMeshBuilder.Apply(buffers, mf.sharedMesh);
            mf.name = $"{GetType().Name} {key.x},{key.y}";
            active[key] = mf;
        }

        void Despawn(Vector2Int key)
        {
            if (!active.TryGetValue(key, out var mf)) return;
            active.Remove(key);
            if (mf == null) return;
            mf.gameObject.SetActive(false);
            pool.Push(mf);
        }

        MeshFilter Acquire()
        {
            if (pool.Count > 0)
            {
                var mf = pool.Pop();
                mf.gameObject.SetActive(true);
                return mf;
            }
            var go = new GameObject(GetType().Name);
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = new Mesh { name = GetType().Name + "Chunk", hideFlags = HideFlags.HideAndDontSave };
            filter.sharedMesh.MarkDynamic();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = materials;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.BlendProbes;
            return filter;
        }

        void OnDestroy()
        {
            foreach (var mf in pool) if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
            foreach (var mf in active.Values) if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
        }
    }
}
