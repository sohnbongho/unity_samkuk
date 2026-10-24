using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 전투 맵의 흙길 메시(Step 16-3): 칸마다 <see cref="PathMeshBuilder"/> 로 길 띠를 만든다. 그림은 <see cref="PathArt"/>(또는 테마의 <c>pathTexture</c>).
    /// 바닥 타일보다 살짝 앞(z)에 깔리는 불투명 띠라 바닥 얼룩·강 토막은 길 아래에 숨고, 길 양옆은 그림의 알파로 들쭉날쭉하다. 풀링·표시 규칙은 <see cref="ChunkMeshes"/>.
    /// </summary>
    public class TerrainPaths : ChunkMeshes
    {
        public void Initialize(TerrainMap terrainMap, Transform followTarget)
        {
            Texture tex = terrainMap != null && terrainMap.Theme != null && terrainMap.Theme.pathTexture != null
                ? terrainMap.Theme.pathTexture
                : (terrainMap != null ? PathArt.Texture(terrainMap.Terrain) : null);
            var m = TiltView.SolidMaterial(tex);
            Setup(terrainMap, followTarget, m, m);   // 서브메시 둘 다 같은 재질 (길은 윗면만 쓴다)
        }

        protected override void Build(Vector2Int chunk, CliffMeshBuilder.Buffers b) => PathMeshBuilder.Build(map, chunk, b);
    }
}
