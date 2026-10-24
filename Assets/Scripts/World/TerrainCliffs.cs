using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Samkuk.World
{
    /// <summary>
    /// 전투 맵의 언덕·절벽 메시(Step 16-2): 카메라 주변 칸마다 <see cref="CliffMeshBuilder"/> 로 만든 메시를 두고 멀어지면 끈다(풀).
    /// 윗면은 바닥 타일 그림, 옆면은 절벽 그림(<see cref="CliffArt"/>, 또는 테마의 그림). 소품 루트(<see cref="TerrainPropSpawner"/> 와 같은 오브젝트)에 붙어 함께 사라진다.
    /// 2.5D 틸트일 때만 보인다 — 2D 렌더러에는 이 셰이더의 패스가 없어 그리지 못한다(벽은 그래도 막는다). 켜고 끌 때 <see cref="TiltView.Changed"/> 로 따라온다.
    /// </summary>
    public class TerrainCliffs : MonoBehaviour
    {
        const float ViewMargin = 4f;

        TerrainMap map;
        Transform follow;
        Camera cam;
        Material topMaterial, wallMaterial;
        readonly Dictionary<Vector2Int, MeshFilter> active = new Dictionary<Vector2Int, MeshFilter>();
        readonly Stack<MeshFilter> pool = new Stack<MeshFilter>();
        readonly List<Vector2Int> removeBuffer = new List<Vector2Int>();
        readonly CliffMeshBuilder.Buffers buffers = new CliffMeshBuilder.Buffers();
        bool visible;

        public TerrainMap Map => map;
        /// <summary>지금 살펴 둔 칸 수 (평지 칸은 메시 없이 자리만 기억한다).</summary>
        public int ActiveChunkCount => active.Count;
        public bool Visible => visible;

        public void Initialize(TerrainMap terrainMap, Transform followTarget, Texture groundTexture)
        {
            map = terrainMap;
            follow = followTarget;
            topMaterial = TiltView.SolidMaterial(groundTexture);
            Texture cliff = terrainMap != null && terrainMap.Theme != null && terrainMap.Theme.cliffTexture != null
                ? terrainMap.Theme.cliffTexture
                : (terrainMap != null ? CliffArt.Texture(terrainMap.Terrain) : null);
            wallMaterial = TiltView.SolidMaterial(cliff);
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
            visible = TiltView.Active && topMaterial != null && wallMaterial != null && map != null;
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

        /// <summary>한 칸의 메시를 다시 만든다 (맵 편집기에서 높이를 고쳤을 때). 화면 주변에 없는 칸이면 아무것도 하지 않는다.</summary>
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
            CliffMeshBuilder.Build(map, key, buffers);
            if (buffers.IsEmpty) { active[key] = null; return; }   // 평지 칸: 메시 없이 자리만 기억해 매 프레임 다시 계산하지 않는다

            var mf = Acquire();
            CliffMeshBuilder.Apply(buffers, mf.sharedMesh);
            mf.name = $"Cliff {key.x},{key.y}";
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
            var go = new GameObject("Cliff");
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = new Mesh { name = "CliffChunk", hideFlags = HideFlags.HideAndDontSave };
            filter.sharedMesh.MarkDynamic();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { topMaterial, wallMaterial };
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
