using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 전투 맵의 언덕·절벽 메시(Step 16-2): 칸마다 <see cref="CliffMeshBuilder"/> 로 윗면(바닥 타일 그림)과 옆면(절벽 그림: <see cref="CliffArt"/>, 또는 테마의
    /// <c>cliffTexture</c>)을 만든다. 소품 루트(<see cref="TerrainPropSpawner"/> 와 같은 오브젝트)에 붙어 함께 사라진다. 풀링·표시 규칙은 <see cref="ChunkMeshes"/>.
    /// </summary>
    public class TerrainCliffs : ChunkMeshes
    {
        public void Initialize(TerrainMap terrainMap, Transform followTarget, Texture groundTexture)
        {
            Texture cliff = terrainMap != null && terrainMap.Theme != null && terrainMap.Theme.cliffTexture != null
                ? terrainMap.Theme.cliffTexture
                : (terrainMap != null ? CliffArt.Texture(terrainMap.Terrain) : null);
            Setup(terrainMap, followTarget, TiltView.SolidMaterial(groundTexture), TiltView.SolidMaterial(cliff));
        }

        protected override void Build(Vector2Int chunk, CliffMeshBuilder.Buffers b) => CliffMeshBuilder.Build(map, chunk, b);
    }
}
