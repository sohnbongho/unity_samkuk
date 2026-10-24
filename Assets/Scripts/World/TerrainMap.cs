using System.Collections.Generic;
using Samkuk.Data;
using UnityEngine;

namespace Samkuk.World
{
    /// <summary>맵 한 칸에 놓을 소품(또는 강 한 토막) 하나의 배치 결과.</summary>
    public struct PropPlacement
    {
        public TerrainProp prop;
        public Vector2 position;   // 월드 좌표 (소품은 피벗 = 바닥에 닿는 점, 강 토막은 가운데)
        public float scale;
        public float stretchY;     // 세로(폭) 배율. 강 토막만 1 이 아니다
        public float rotation;     // 도(degree). 강 토막이 흐르는 방향으로 눕는다
        public bool flipX;
        public int order;          // 배경 레이어 안에서의 그리기 순서 (아래쪽 소품이 위에 그려지게)
        public bool tinted;        // true 면 tint 를 스프라이트에 곱한다 (바닥 얼룩). false 면 원래 색 그대로
        public Color tint;
    }

    /// <summary>
    /// 코드로 만드는 장식용 그림 (에셋 없음): 가장자리가 부드럽게 사라지는 둥근 얼룩.
    /// 바닥 위에 색을 달리 입혀 "풀이 짙은 곳, 마른 흙 길" 같은 변화를 주는 데 쓴다.
    /// </summary>
    public static class TerrainDecals
    {
        public const string SoftBlobName = "SoftBlob";
        static Sprite softBlob;
        static TerrainProp patchProp;

        /// <summary>흰색 둥근 얼룩 (지름 4유닛, 가운데 불투명 -> 가장자리 투명).</summary>
        public static Sprite SoftBlob
        {
            get
            {
                if (softBlob != null) return softBlob;
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                        float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        float a = t * t * (3f - 2f * t);   // smoothstep: 가장자리가 자연스럽게 번진다
                        px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                softBlob = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 4f);
                softBlob.hideFlags = HideFlags.HideAndDontSave;
                softBlob.name = SoftBlobName;
                return softBlob;
            }
        }

        public static TerrainProp PatchProp
        {
            get
            {
                if (patchProp == null || patchProp.sprite == null) patchProp = new TerrainProp { name = "GroundPatch", sprite = SoftBlob };
                return patchProp;
            }
        }
    }

    /// <summary>
    /// 출진한 성의 전투 맵 구성. 같은 성은 언제나 같은 맵이고(성 아이디로 정한 시드), 성마다 다르다:
    /// 소품 밀도, 소품 비율(어떤 나무가 많은지), 바닥 색조가 조금씩 다르고, 강이 있는 성(과 강변 지형)에는
    /// 구불구불 흐르는 강이, 큰 성(대성/도성)에는 깃발이 놓인다. 무한히 이어지는 세계를 12x12 유닛 "칸" 단위로 나눠
    /// 칸마다 같은 배치를 계산한다. 화면 없이 계산하는 순수 로직이라 테스트할 수 있다
    /// (소품을 실제로 만드는 쪽은 <see cref="TerrainPropSpawner"/>).
    /// </summary>
    public sealed class TerrainMap
    {
        public const float ChunkSize = 12f;
        public const float ClearRadius = 2.5f;     // 플레이어 시작 위치 주변은 비운다
        const float PropSpacing = 1.15f;           // 소품끼리 최소 간격
        const float PondClearRadius = 4f;

        // 맵을 덜 휑하게: 흩뿌리는 소품을 늘리고, 한곳에 모인 무리(숲, 풀밭, 바위 더미)와 바닥 얼룩을 더한다
        const float ScatterBoost = 1.4f;           // 테마 밀도(propsPerChunk)에 곱하는 흩뿌림 배율
        const float ClusterPerBaseDensity = 1.1f;  // 무리 수 = 이 값 x (밀도 / 9)
        const float ClusterRadius = 2.3f;          // 무리가 퍼지는 반지름
        const float PatchPerChunk = 3.5f;          // 바닥 얼룩 평균 개수 (성마다 0.7 ~ 1.3배)

        // 그리기 순서: 바닥 얼룩 < 강둑 < 강물 < 연못 < 소품 (모두 배경 바닥보다 위, 적/플레이어보다 아래)
        public const int OrderPatch = 1, OrderBank = 2, OrderWater = 3, OrderPond = 4, OrderPropBase = 5;

        // 강: 세계 전체에 같은 모양의 강이 60유닛 간격으로 평행하게 흐른다 (방향/굽이는 성마다 다름)
        const float RiverSpacing = 60f;
        public const float RiverStep = 1.5f;              // 강 토막 사이 간격 (토막 길이 4유닛보다 짧게 해 겹쳐 이어 붙인다)
        const float WaterVisibleHeight = 1.6f;     // 물 토막 그림의 눈에 보이는 폭(가장자리는 흐려짐) = 스프라이트 높이 2유닛의 80%
        const float BankVisibleHeight = 1.8f;
        const float BankExtra = 1.35f;             // 강둑은 물보다 이만큼 더 넓다

        public CastleData Castle { get; }
        /// <summary>이 맵의 바닥 지형. 보통 성의 지형이고, 맵 편집기에서 다르게 고른 경우만 다르다.</summary>
        public CastleTerrain Terrain { get; }
        public TerrainTheme Theme { get; }
        public TerrainThemeCatalog Catalog { get; }
        public int Seed { get; }

        /// <summary>소품 밀도 배율 (성마다 0.8 ~ 1.3).</summary>
        public float DensityMultiplier { get; }
        /// <summary>바닥 타일에 곱하는 색조 (성마다 조금씩 다름).</summary>
        public Color GroundTint { get; }
        public float PondPerChunk { get; }
        public float BannerPerChunk { get; }

        /// <summary>이 맵에 강이 흐르는가 (강이 있는 성/강변 지형이고 테마에 강 그림이 있을 때).</summary>
        public bool HasRiver { get; }
        public float RiverWidth { get; }
        public float RiverAngleDegrees => riverAngle * Mathf.Rad2Deg;
        public TerrainProp RiverWaterProp { get; }
        public TerrainProp RiverBankProp { get; }

        readonly float[] weights;   // 소품별 (기본 비중 x 성마다 다른 배율)
        float totalWeight;

        // 강 모양 (성 시드에서 정해짐)
        readonly float riverAngle, riverOffset, riverAmp, riverWave, riverPhase;
        readonly Vector2 dirU, dirV;

        // 맵 편집기로 직접 고친 칸: 이 칸은 자동 생성 대신 이 목록을 그대로 쓴다 (나머지 칸은 평소처럼 자동 생성)
        readonly Dictionary<Vector2Int, List<PropPlacement>> custom = new Dictionary<Vector2Int, List<PropPlacement>>();
        // 직접 고친 칸의 높이(Step 16-2). 고친 칸이 아니면 자동 생성 높이(heightCache)
        readonly Dictionary<Vector2Int, HeightChunk> customHeights = new Dictionary<Vector2Int, HeightChunk>();
        readonly Dictionary<Vector2Int, HeightChunk> heightCache = new Dictionary<Vector2Int, HeightChunk>();
        Dictionary<string, TerrainProp> library;

        /// <summary>언덕이 차지하는 비율(0~1, Step 16-2). 지형별 기본값에 성마다 0.6~1.4배. 0 이면 자동 생성 언덕이 없다.</summary>
        public float HillCoverage { get; }

        // 시작 언덕(Step 16-2): 어느 맵이든 시작 위치 가까이에 늘 보이는 작은 언덕 — 가로 3 x 세로 2 셀 1층, 뒷줄 가운데 2층, 시작 쪽 가운데가 경사로.
        // 경사로가 내려서는 셀은 평지로 묶어 둔다. 강에서 떨어진 쪽(북동 → 남동 → 북서 → 남서)에 놓는다. 세계 셀 좌표 → (층, 경사로)
        readonly Dictionary<Vector2Int, (int level, RampDir ramp)> starterHill = new Dictionary<Vector2Int, (int, RampDir)>();

        /// <summary>시작 언덕의 셀 수 (평지로 묶어 둔 경사로 아래 셀 포함).</summary>
        public int StarterHillCellCount => starterHill.Count;
        /// <summary>시작 언덕의 경사로 셀 가운데 (세계 좌표).</summary>
        public Vector2 StarterRampCenter { get; private set; }

        TerrainMap(CastleData castle, TerrainTheme theme, TerrainThemeCatalog catalog, CastleTerrain terrain)
        {
            Castle = castle;
            Terrain = terrain;
            Theme = theme;
            Catalog = catalog;
            Seed = (int)(Fnv(castle.id ?? "") & 0x7fffffff);

            DensityMultiplier = 0.8f + 0.5f * Frac(1) + (castle.size == CastleSize.Capital ? 0.1f : 0f);
            GroundTint = new Color(1f + (Frac(2) - 0.5f) * 0.14f, 1f + (Frac(3) - 0.5f) * 0.14f, 1f + (Frac(4) - 0.5f) * 0.14f, 1f);

            if (castle.hasWater) PondPerChunk = terrain == CastleTerrain.River ? 0.9f : 0.5f;
            else PondPerChunk = terrain == CastleTerrain.River ? 0.35f : 0f;
            BannerPerChunk = castle.size == CastleSize.Capital ? 0.9f : (castle.size == CastleSize.Large ? 0.45f : 0f);
            HillCoverage = BaseHillCoverage(terrain) * (0.6f + 0.8f * Frac(40));

            weights = new float[theme.props.Count];
            for (int i = 0; i < weights.Length; i++)
            {
                var p = theme.props[i];
                weights[i] = p != null && p.sprite != null ? Mathf.Max(0f, p.weight) * (0.5f + Frac(10 + i)) : 0f;
                totalWeight += weights[i];
            }

            HasRiver = (castle.hasWater || terrain == CastleTerrain.River) && theme.riverWater != null && theme.riverBank != null;
            if (HasRiver)
            {
                riverAngle = Frac(20) * Mathf.PI;
                dirU = new Vector2(Mathf.Cos(riverAngle), Mathf.Sin(riverAngle));
                dirV = new Vector2(-dirU.y, dirU.x);
                // 가장 가까운 강이 시작 위치에서 떨어지게 한다: 3만 개 성으로 시뮬레이션해 가장 넓은 강(5유닛)에서도 물가가 시작 위치에서 2.5유닛 이상 떨어지는 가장 가까운 값(10~13)을 골랐다
                riverOffset = (Frac(21) < 0.5f ? -1f : 1f) * (10f + Frac(22) * 3f);
                riverAmp = 2.5f + Frac(23) * 2f;
                riverWave = 26f + Frac(24) * 14f;
                riverPhase = Frac(25) * Mathf.PI * 2f;
                RiverWidth = Mathf.Max(1f, theme.riverWidth) * (0.85f + Frac(26) * 0.3f);
                RiverWaterProp = new TerrainProp { name = "RiverWater", sprite = theme.riverWater };
                RiverBankProp = new TerrainProp { name = "RiverBank", sprite = theme.riverBank };
            }
            BuildStarterHill();   // 강 모양이 정해진 뒤
        }

        /// <summary>시작 언덕 자리를 고른다: 네 후보 가운데 강에 닿지 않는 첫 번째 (모두 닿으면 마지막 후보).</summary>
        void BuildStarterHill()
        {
            // 후보: 블록의 왼쪽 아래 셀, 시작 쪽이 남쪽(블록이 북쪽에 있음)인가
            var candidates = new[] { (new Vector2Int(3, 3), true), (new Vector2Int(3, -5), false), (new Vector2Int(-6, 3), true), (new Vector2Int(-6, -5), false) };
            foreach (var (origin, southFacing) in candidates)
            {
                starterHill.Clear();
                int nearRow = southFacing ? origin.y : origin.y + 1;      // 시작 쪽 줄
                int farRow = southFacing ? origin.y + 1 : origin.y;       // 먼 줄
                for (int dx = 0; dx < 3; dx++)
                {
                    starterHill[new Vector2Int(origin.x + dx, nearRow)] = (1, RampDir.None);
                    starterHill[new Vector2Int(origin.x + dx, farRow)] = (dx == 1 ? 2 : 1, RampDir.None);
                }
                var rampCell = new Vector2Int(origin.x + 1, nearRow);
                starterHill[rampCell] = (1, southFacing ? RampDir.South : RampDir.North);
                starterHill[new Vector2Int(origin.x + 1, nearRow + (southFacing ? -1 : 1))] = (0, RampDir.None);   // 경사로가 내려서는 자리는 평지
                StarterRampCenter = (rampCell + Vector2.one * 0.5f) * TerrainHeight.CellSize;

                bool clear = true;
                if (HasRiver)
                    foreach (var cell in starterHill.Keys)
                    {
                        var center = (cell + Vector2.one * 0.5f) * TerrainHeight.CellSize;
                        if (RiverDistance(center) < RiverWidth * 0.5f * BankExtra + TerrainHeight.CellSize) { clear = false; break; }
                    }
                if (clear) return;
            }
        }

        /// <summary>
        /// 성의 지형에 맞는 맵을 만든다. 성/카탈로그/테마가 없으면 null (기존 색 덮개 방식으로 대신한다).
        /// <paramref name="terrainOverride"/> 를 주면 성의 지형 대신 그 지형의 바닥/소품/강으로 만든다(맵 편집기).
        /// </summary>
        public static TerrainMap Create(CastleData castle, TerrainThemeCatalog catalog, CastleTerrain? terrainOverride = null)
        {
            if (castle == null || catalog == null) return null;
            var terrain = terrainOverride ?? castle.terrain;
            var theme = catalog.Get(terrain);
            return theme != null ? new TerrainMap(castle, theme, catalog, terrain) : null;
        }

        /// <summary>
        /// 전투에서 쓰는 맵: 자동 생성 맵에 맵 편집기로 고쳐 둔 내용(<see cref="MapStore"/>)이 있으면 덮어쓴다.
        /// 고친 칸만 바뀌고 나머지는 평소와 같다.
        /// </summary>
        public static TerrainMap CreateForBattle(CastleData castle, TerrainThemeCatalog catalog)
        {
            if (castle == null || catalog == null) return null;
            var layout = MapStore.Load(castle.id);
            var map = Create(castle, catalog, ParseTerrain(layout != null ? layout.terrain : null));
            if (map != null && layout != null) map.ImportLayout(layout);
            return map;
        }

        public static CastleTerrain? ParseTerrain(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return System.Enum.TryParse<CastleTerrain>(name, out var t) ? t : (CastleTerrain?)null;
        }

        static CastleData freeBattleCastle;

        /// <summary>성 없이 시작한 판을 나타내는 내부용 가짜 성 (평야 + 강). 맵 편집기에서 이 맵도 고칠 수 있다.</summary>
        public static CastleData FreeBattleCastle
        {
            get
            {
                if (freeBattleCastle == null)
                {
                    freeBattleCastle = ScriptableObject.CreateInstance<CastleData>();
                    freeBattleCastle.hideFlags = HideFlags.HideAndDontSave;
                    freeBattleCastle.id = MapStore.FreeBattleId;
                    freeBattleCastle.displayName = "자유 전투";
                    freeBattleCastle.terrain = CastleTerrain.Plain;
                    freeBattleCastle.size = CastleSize.Medium;
                    freeBattleCastle.hasWater = true;
                }
                return freeBattleCastle;
            }
        }

        /// <summary>
        /// 성 없이 시작한 판(타이틀의 [시작])의 맵: 평야에 강이 흐르는 고정된 맵.
        /// 성 데이터가 없으므로 내부용 가짜 성(자유 전투)으로 같은 방식의 맵을 만든다.
        /// </summary>
        public static TerrainMap CreateFreeBattle(TerrainThemeCatalog catalog)
        {
            if (catalog == null) return null;
            return CreateForBattle(FreeBattleCastle, catalog);
        }

        /// <summary>월드 좌표가 속한 칸 번호.</summary>
        public static Vector2Int ChunkOf(Vector2 position) =>
            new Vector2Int(Mathf.FloorToInt(position.x / ChunkSize), Mathf.FloorToInt(position.y / ChunkSize));

        // ───────────────────────── 강 ─────────────────────────

        float CenterV(float u, int k) => riverOffset + k * RiverSpacing + riverAmp * Mathf.Sin(u * (Mathf.PI * 2f / riverWave) + riverPhase + k * 1.7f);

        float SlopeV(float u, int k) => riverAmp * (Mathf.PI * 2f / riverWave) * Mathf.Cos(u * (Mathf.PI * 2f / riverWave) + riverPhase + k * 1.7f);

        /// <summary>가장 가까운 강의 중심선까지의 (대략적인) 거리. 강이 없는 맵이면 무한대.</summary>
        public float RiverDistance(Vector2 p)
        {
            if (!HasRiver) return float.PositiveInfinity;
            float u = Vector2.Dot(p, dirU), v = Vector2.Dot(p, dirV);
            int center = Mathf.RoundToInt((v - riverOffset) / RiverSpacing);
            float best = float.PositiveInfinity;
            for (int k = center - 1; k <= center + 1; k++)
            {
                float slope = SlopeV(u, k);
                best = Mathf.Min(best, Mathf.Abs(v - CenterV(u, k)) / Mathf.Sqrt(1f + slope * slope));   // 비스듬히 흐르는 만큼 수직 거리로 보정
            }
            return best;
        }

        /// <summary>한 칸을 지나는 강을 토막으로 나눠 놓는다 (토막은 자기가 속한 칸에서만 만든다).</summary>
        void AddRiver(List<PropPlacement> result, int chunkX, int chunkY)
        {
            float ox = chunkX * ChunkSize, oy = chunkY * ChunkSize;
            float uMin = float.MaxValue, uMax = float.MinValue, vMin = float.MaxValue, vMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2(ox + (i % 2) * ChunkSize, oy + (i / 2) * ChunkSize);
                float u = Vector2.Dot(corner, dirU), v = Vector2.Dot(corner, dirV);
                uMin = Mathf.Min(uMin, u); uMax = Mathf.Max(uMax, u); vMin = Mathf.Min(vMin, v); vMax = Mathf.Max(vMax, v);
            }

            float reach = riverAmp + RiverWidth + 1f;
            int k0 = Mathf.FloorToInt((vMin - reach - riverOffset) / RiverSpacing);
            int k1 = Mathf.CeilToInt((vMax + reach - riverOffset) / RiverSpacing);
            int firstIndex = Mathf.FloorToInt(uMin / RiverStep);   // 정수 번호로 세어 모든 칸이 똑같은 격자를 쓴다 (이웃 칸과 토막이 겹치거나 빠지지 않는다)

            for (int k = k0; k <= k1; k++)
                for (int i = firstIndex; i * RiverStep <= uMax + RiverStep; i++)
                {
                    float u = i * RiverStep;
                    float v = CenterV(u, k);
                    Vector2 pos = dirU * u + dirV * v;
                    if (pos.x < ox || pos.x >= ox + ChunkSize || pos.y < oy || pos.y >= oy + ChunkSize) continue;

                    float angle = riverAngle * Mathf.Rad2Deg + Mathf.Atan(SlopeV(u, k)) * Mathf.Rad2Deg;
                    result.Add(new PropPlacement
                    {
                        prop = RiverBankProp, position = pos, scale = 1f, rotation = angle, order = OrderBank,
                        stretchY = RiverWidth * BankExtra / BankVisibleHeight,
                    });
                    result.Add(new PropPlacement
                    {
                        prop = RiverWaterProp, position = pos, scale = 1f, rotation = angle, order = OrderWater,
                        stretchY = RiverWidth / WaterVisibleHeight,
                    });
                }
        }

        // ───────────────────────── 직접 고친 칸 (맵 편집기) ─────────────────────────

        /// <summary>소품의 그리기 순서: 아래쪽(y 가 작은) 소품이 위에 그려지게. <paramref name="chunkTop"/> 은 칸의 위쪽 y.</summary>
        public static int PropOrder(float y, float chunkTop) =>
            OrderPropBase + Mathf.Clamp(Mathf.RoundToInt((chunkTop - y) * 8f), 0, 100);

        /// <summary>강 폭(유닛)에 맞는 강 토막(물 또는 강둑)의 세로 배율. 맵 편집기에서 강을 직접 그릴 때 쓴다.</summary>
        public static float RiverStretch(float riverWidth, bool bank) =>
            bank ? riverWidth * BankExtra / BankVisibleHeight : riverWidth / WaterVisibleHeight;

        /// <summary>강물 토막 하나의 실제 폭(유닛). 세로 배율에서 거꾸로 구하므로 맵 편집기로 다르게 그린 강도 맞는다 (<see cref="TerrainCollision"/>).</summary>
        public static float RiverWidthOf(PropPlacement water) => (water.stretchY > 0f ? water.stretchY : 1f) * WaterVisibleHeight;

        /// <summary>강물 위에서의 속도 배율 (테마 값, 비어 있으면 기본 절반). 1 이면 느려지지 않는다.</summary>
        public float RiverSlowFactor => Theme != null && Theme.riverSlowFactor > 0f ? Theme.riverSlowFactor : TerrainPropKinds.WaterSlowFactor;

        /// <summary>그리기 순서로 물건의 종류를 알아낸다 (소품이 아니면 얼룩/강/연못).</summary>
        public static MapItemKind KindOf(PropPlacement p)
        {
            switch (p.order)
            {
                case OrderPatch: return MapItemKind.Patch;
                case OrderBank: return MapItemKind.Bank;
                case OrderWater: return MapItemKind.Water;
                case OrderPond: return MapItemKind.Pond;
                default: return MapItemKind.Prop;
            }
        }

        public static int OrderOf(MapItemKind kind, float y, int chunkY)
        {
            switch (kind)
            {
                case MapItemKind.Patch: return OrderPatch;
                case MapItemKind.Bank: return OrderBank;
                case MapItemKind.Water: return OrderWater;
                case MapItemKind.Pond: return OrderPond;
                default: return PropOrder(y, (chunkY + 1) * ChunkSize);
            }
        }

        /// <summary>직접 고친 칸이 하나라도 있는가.</summary>
        public bool HasCustom => custom.Count > 0 || customHeights.Count > 0;

        public int CustomChunkCount => custom.Count;

        public IEnumerable<Vector2Int> CustomChunks
        {
            get
            {
                foreach (var k in custom.Keys) yield return k;
                foreach (var k in customHeights.Keys) if (!custom.ContainsKey(k)) yield return k;
            }
        }

        public bool IsCustom(Vector2Int chunk) => custom.ContainsKey(chunk) || customHeights.ContainsKey(chunk);

        /// <summary>한 칸에 실제로 보일 모든 것 (직접 고친 칸이면 그 내용, 아니면 자동 생성: 바닥 얼룩 + 소품/강/연못). 읽기 전용으로 쓴다.</summary>
        public List<PropPlacement> GetChunk(int chunkX, int chunkY)
        {
            if (custom.Count > 0 && custom.TryGetValue(new Vector2Int(chunkX, chunkY), out var list)) return list;
            return Generate(chunkX, chunkY);
        }

        /// <summary>직접 고친 내용과 상관없이 자동 생성한 칸의 내용.</summary>
        public List<PropPlacement> Generate(int chunkX, int chunkY)
        {
            var list = LayoutPatches(chunkX, chunkY);
            list.AddRange(Layout(chunkX, chunkY));
            return list;
        }

        /// <summary>칸을 편집할 수 있게 한다: 아직 고친 적 없으면 지금 보이는 자동 생성 내용(물건과 높이)을 그대로 옮겨 와 시작점으로 삼는다.</summary>
        public List<PropPlacement> EnsureCustomChunk(Vector2Int chunk)
        {
            if (!custom.TryGetValue(chunk, out var list))
            {
                list = Generate(chunk.x, chunk.y);
                custom[chunk] = list;
            }
            if (!customHeights.ContainsKey(chunk)) customHeights[chunk] = GetHeights(chunk).Clone();
            return list;
        }

        /// <summary>칸의 높이를 편집할 수 있게 한다 (물건도 함께 직접 고친 칸이 된다).</summary>
        public HeightChunk EnsureCustomHeights(Vector2Int chunk)
        {
            EnsureCustomChunk(chunk);
            return customHeights[chunk];
        }

        public void SetCustomChunk(Vector2Int chunk, List<PropPlacement> items) => custom[chunk] = items ?? new List<PropPlacement>();

        /// <summary>칸의 높이를 직접 정한다 (null 이면 자동 생성으로).</summary>
        public void SetCustomHeights(Vector2Int chunk, HeightChunk heights)
        {
            if (heights == null) customHeights.Remove(chunk);
            else customHeights[chunk] = heights;
        }

        /// <summary>칸을 다시 자동 생성으로 되돌린다 (물건과 높이 모두).</summary>
        public bool RemoveCustomChunk(Vector2Int chunk)
        {
            bool a = custom.Remove(chunk), b = customHeights.Remove(chunk);
            return a || b;
        }

        public void ClearCustom()
        {
            custom.Clear();
            customHeights.Clear();
        }

        /// <summary>직접 고친 칸 전체(물건과 높이)의 복사본 (실행 취소용).</summary>
        public sealed class CustomSnapshot
        {
            public readonly Dictionary<Vector2Int, List<PropPlacement>> items = new Dictionary<Vector2Int, List<PropPlacement>>();
            public readonly Dictionary<Vector2Int, HeightChunk> heights = new Dictionary<Vector2Int, HeightChunk>();

            public IEnumerable<Vector2Int> Chunks
            {
                get
                {
                    foreach (var k in items.Keys) yield return k;
                    foreach (var k in heights.Keys) if (!items.ContainsKey(k)) yield return k;
                }
            }
        }

        public CustomSnapshot SnapshotCustom()
        {
            var copy = new CustomSnapshot();
            foreach (var kv in custom) copy.items[kv.Key] = new List<PropPlacement>(kv.Value);
            foreach (var kv in customHeights) copy.heights[kv.Key] = kv.Value.Clone();
            return copy;
        }

        public void RestoreCustom(CustomSnapshot snapshot)
        {
            custom.Clear();
            customHeights.Clear();
            foreach (var kv in snapshot.items) custom[kv.Key] = new List<PropPlacement>(kv.Value);
            foreach (var kv in snapshot.heights) customHeights[kv.Key] = kv.Value.Clone();
        }

        // ───────────────────────── 높이 (Step 16-2) ─────────────────────────

        /// <summary>지형별 언덕 비율 기본값. 산악·황토는 언덕이 많고 강변·평야는 거의 평지.</summary>
        public static float BaseHillCoverage(CastleTerrain terrain)
        {
            switch (terrain)
            {
                case CastleTerrain.Mountain: return 0.32f;
                case CastleTerrain.Loess: return 0.22f;
                case CastleTerrain.Jungle: return 0.14f;
                case CastleTerrain.Steppe: return 0.1f;
                case CastleTerrain.River: return 0.04f;
                default: return 0.07f;   // Plain
            }
        }

        /// <summary>한 칸의 높이 (직접 고친 칸이면 그것, 아니면 자동 생성. 자동 생성은 캐시).</summary>
        public HeightChunk GetHeights(Vector2Int chunk)
        {
            if (customHeights.TryGetValue(chunk, out var h)) return h;
            if (heightCache.TryGetValue(chunk, out h)) return h;
            if (heightCache.Count > 512) heightCache.Clear();   // 무한 세계를 오래 돌아다녀도 메모리가 늘지 않게
            h = GenerateHeights(chunk);
            heightCache[chunk] = h;
            return h;
        }

        /// <summary>위치가 속한 셀의 층과 경사로.</summary>
        public void GetHeightsAt(Vector2 position, out int level, out RampDir ramp)
        {
            TerrainHeight.Locate(position, out var chunk, out int cx, out int cy, out _, out _);
            var h = GetHeights(chunk);
            level = h.Level(cx, cy);
            ramp = h.Ramp(cx, cy);
        }

        /// <summary>어떤 위치의 바닥 높이(층 단위 실수). 경사로 위에서는 연속적으로 변한다.</summary>
        public float HeightAt(Vector2 position)
        {
            TerrainHeight.Locate(position, out var chunk, out int cx, out int cy, out float fx, out float fy);
            return TerrainHeight.Surface(GetHeights(chunk), cx, cy, fx, fy);
        }

        /// <summary>
        /// 자동 생성 높이 (같은 입력이면 항상 같은 결과, 이웃 칸과 이어진다): 성 시드의 값 노이즈로 언덕을 뽑고, 외톨이 셀은 깎고,
        /// 언덕 가장자리 일부를 경사로로 만든다. 시작 위치 주변과 강가는 평지.
        /// </summary>
        public HeightChunk GenerateHeights(Vector2Int chunk)
        {
            var h = new HeightChunk();
            for (int cy = 0; cy < HeightChunk.Cells; cy++)
                for (int cx = 0; cx < HeightChunk.Cells; cx++)
                {
                    int gx = chunk.x * HeightChunk.Cells + cx, gy = chunk.y * HeightChunk.Cells + cy;
                    if (starterHill.TryGetValue(new Vector2Int(gx, gy), out var s)) { h.Set(cx, cy, s.level, s.ramp); continue; }
                    if (HillCoverage <= 0f) continue;
                    int level = FinalLevel(gx, gy);
                    h.Set(cx, cy, level, level > 0 ? AutoRamp(gx, gy, level) : RampDir.None);
                }
            return h;
        }

        /// <summary>노이즈만으로 정한 층 (세계 셀 좌표). 시작 언덕 셀은 정해진 값.</summary>
        int RawLevel(int gx, int gy)
        {
            if (starterHill.TryGetValue(new Vector2Int(gx, gy), out var s)) return s.level;
            if (HillCoverage <= 0f) return 0;
            var center = new Vector2((gx + 0.5f) * TerrainHeight.CellSize, (gy + 0.5f) * TerrainHeight.CellSize);
            if (center.magnitude < TerrainHeight.FlatRadius + TerrainHeight.CellSize) return 0;
            if (HasRiver && RiverDistance(center) < RiverWidth * 0.5f * BankExtra + TerrainHeight.CellSize) return 0;
            float n = HillNoise(gx, gy);
            if (n > 1f - HillCoverage * 0.3f && Terrain == CastleTerrain.Mountain) return 2;
            return n > 1f - HillCoverage ? 1 : 0;
        }

        /// <summary>외톨이 셀(4방 이웃이 모두 낮은 셀)은 한 층 깎는다 — 혼자 솟은 기둥이 되지 않게.</summary>
        int FinalLevel(int gx, int gy)
        {
            int l = RawLevel(gx, gy);
            if (l == 0) return 0;
            if (RawLevel(gx, gy - 1) >= l || RawLevel(gx, gy + 1) >= l || RawLevel(gx - 1, gy) >= l || RawLevel(gx + 1, gy) >= l) return l;
            return l - 1;
        }

        /// <summary>언덕 가장자리 셀 일부를 경사로로: 내려가는 쪽 이웃이 한 층 낮고 양옆이 같은 층일 때, 셀마다 정해진 값으로 고른다.</summary>
        RampDir AutoRamp(int gx, int gy, int level)
        {
            int s = FinalLevel(gx, gy - 1), n = FinalLevel(gx, gy + 1), w = FinalLevel(gx - 1, gy), e = FinalLevel(gx + 1, gy);
            float r = Hash01(gx, gy, 77);
            if (s == level - 1 && w == level && e == level && n >= level && r < 0.35f) return RampDir.South;
            if (n == level - 1 && w == level && e == level && s >= level && r >= 0.35f && r < 0.5f) return RampDir.North;
            if (w == level - 1 && n == level && s == level && e >= level && r >= 0.5f && r < 0.65f) return RampDir.West;
            if (e == level - 1 && n == level && s == level && w >= level && r >= 0.65f && r < 0.8f) return RampDir.East;
            return RampDir.None;
        }

        /// <summary>언덕 노이즈 (0~1): 값 노이즈 두 옥타브 (큰 덩어리 + 가장자리 들쭉날쭉).</summary>
        float HillNoise(int gx, int gy) => ValueNoise(gx / 5f, gy / 5f, 1) * 0.65f + ValueNoise(gx / 2.5f, gy / 2.5f, 2) * 0.35f;

        float ValueNoise(float x, float y, int salt)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = Smooth(x - x0), ty = Smooth(y - y0);
            float a = Hash01(x0, y0, salt), b = Hash01(x0 + 1, y0, salt), c = Hash01(x0, y0 + 1, salt), d = Hash01(x0 + 1, y0 + 1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        float Hash01(int x, int y, int salt) =>
            (Mix(Mix((uint)Seed ^ (0x7a3d9f1bu + (uint)salt * 0x9e3779b9u), (uint)x), (uint)y) & 0xffff) / 65535f;

        /// <summary>저장 파일 속 그림 이름으로 소품 정보를 찾는다. 모든 지형 테마의 소품, 강 토막, 연못, 깃발, 얼룩을 포함한다.</summary>
        public TerrainProp ResolveProp(string spriteName)
        {
            if (library == null)
            {
                library = new Dictionary<string, TerrainProp>();
                foreach (var t in Catalog.themes)
                {
                    if (t == null) continue;
                    foreach (var p in t.props) AddToLibrary(p);
                    if (t.riverWater != null) AddToLibrary(new TerrainProp { name = "RiverWater", sprite = t.riverWater });
                    if (t.riverBank != null) AddToLibrary(new TerrainProp { name = "RiverBank", sprite = t.riverBank });
                }
                AddToLibrary(Catalog.pond);
                AddToLibrary(Catalog.banner);
                AddToLibrary(TerrainDecals.PatchProp);
            }
            return spriteName != null && library.TryGetValue(spriteName, out var prop) ? prop : null;
        }

        void AddToLibrary(TerrainProp p)
        {
            if (p == null || p.sprite == null) return;
            library[KeyOf(p)] = p;
        }

        /// <summary>저장 파일에서 소품을 가리키는 이름 (그림 파일 이름, 없으면 소품 이름).</summary>
        public static string KeyOf(TerrainProp p) =>
            p.sprite != null && !string.IsNullOrEmpty(p.sprite.name) ? p.sprite.name : p.name;

        /// <summary>직접 고친 칸을 저장 파일 모양으로 옮긴다 (고친 칸이 없고 지형도 그대로면 chunks 가 비어 있다).</summary>
        public MapLayoutData ExportLayout()
        {
            var data = new MapLayoutData { castleId = Castle.id, terrain = Terrain != Castle.terrain ? Terrain.ToString() : "" };
            foreach (var kv in custom)
            {
                var chunk = new MapChunk { cx = kv.Key.x, cy = kv.Key.y };
                foreach (var p in kv.Value)
                {
                    if (p.prop == null || p.prop.sprite == null) continue;
                    chunk.items.Add(new MapItem
                    {
                        sprite = KeyOf(p.prop), kind = (int)KindOf(p), x = p.position.x, y = p.position.y,
                        scale = p.scale, stretchY = p.stretchY <= 0f ? 1f : p.stretchY, rotation = p.rotation, flipX = p.flipX,
                        tinted = p.tinted, r = p.tint.r, g = p.tint.g, b = p.tint.b, a = p.tint.a,
                    });
                }
                if (customHeights.TryGetValue(kv.Key, out var hc)) WriteHeights(chunk, hc);
                data.chunks.Add(chunk);
            }
            // 높이만 고친 칸 (물건 목록 없이 높이만 있는 경우)
            foreach (var kv in customHeights)
            {
                if (custom.ContainsKey(kv.Key)) continue;
                var chunk = new MapChunk { cx = kv.Key.x, cy = kv.Key.y };
                WriteHeights(chunk, kv.Value);
                data.chunks.Add(chunk);
            }
            data.chunks.Sort((a, b) => a.cy != b.cy ? a.cy.CompareTo(b.cy) : a.cx.CompareTo(b.cx));
            return data;
        }

        static void WriteHeights(MapChunk chunk, HeightChunk h)
        {
            chunk.levels = new int[HeightChunk.Count];
            chunk.ramps = new int[HeightChunk.Count];
            for (int i = 0; i < HeightChunk.Count; i++) { chunk.levels[i] = h.levels[i]; chunk.ramps[i] = h.ramps[i]; }
        }

        /// <summary>저장 파일의 칸들을 이 맵의 직접 고친 칸으로 쓴다 (기존 것은 지운다). 그림을 찾지 못한 물건은 건너뛴다.</summary>
        public void ImportLayout(MapLayoutData data)
        {
            custom.Clear();
            customHeights.Clear();
            if (data == null) return;
            foreach (var chunk in data.chunks)
            {
                var key = new Vector2Int(chunk.cx, chunk.cy);
                // 높이 (예전 파일(version 1)에는 없다 → 자동 생성 높이를 쓴다)
                if (chunk.levels != null && chunk.levels.Length == HeightChunk.Count)
                {
                    var h = new HeightChunk();
                    for (int i = 0; i < HeightChunk.Count; i++)
                    {
                        int ramp = chunk.ramps != null && chunk.ramps.Length == HeightChunk.Count ? chunk.ramps[i] : 0;
                        h.Set(i % HeightChunk.Cells, i / HeightChunk.Cells, chunk.levels[i], ramp >= 1 && ramp <= 4 ? (RampDir)ramp : RampDir.None);
                    }
                    customHeights[key] = h;
                }
                if (chunk.items == null) continue;
                var list = new List<PropPlacement>(chunk.items.Count);
                foreach (var item in chunk.items)
                {
                    var prop = ResolveProp(item.sprite);
                    if (prop == null) continue;
                    var kind = (MapItemKind)item.kind;
                    list.Add(new PropPlacement
                    {
                        prop = prop, position = new Vector2(item.x, item.y), scale = item.scale, stretchY = item.stretchY <= 0f ? 1f : item.stretchY,
                        rotation = item.rotation, flipX = item.flipX, order = OrderOf(kind, item.y, chunk.cy),
                        tinted = item.tinted, tint = new Color(item.r, item.g, item.b, item.a),
                    });
                }
                custom[key] = list;
            }
        }

        // ───────────────────────── 배치 ─────────────────────────

        /// <summary>한 칸에 놓을 소품과 강 토막 배치 (같은 입력이면 항상 같은 결과).</summary>
        public List<PropPlacement> Layout(int chunkX, int chunkY)
        {
            var result = new List<PropPlacement>();
            var rng = new System.Random(unchecked((int)Mix(Mix((uint)Seed, (uint)chunkX), (uint)chunkY)));
            float originX = chunkX * ChunkSize, originY = chunkY * ChunkSize, top = originY + ChunkSize;

            if (HasRiver) AddRiver(result, chunkX, chunkY);
            float riverHalf = HasRiver ? RiverWidth * 0.5f : 0f;

            // 연못을 먼저 (소품이 연못 위에 서지 않게 자리를 비워 둔다). 강 위에는 놓지 않는다.
            var blockers = new List<Vector3>();   // x, y, 반지름
            if (Catalog.pond != null && Catalog.pond.sprite != null)
                for (int i = 0, n = Count(rng, PondPerChunk); i < n; i++)
                {
                    float scale = Range(rng, Catalog.pond.scaleMin, Catalog.pond.scaleMax);
                    float radius = 2.2f * scale;
                    if (TryPlace(rng, originX, originY, PondClearRadius + radius, blockers, radius + 0.5f, 5, riverHalf + radius + 0.3f, out var pos))
                    {
                        blockers.Add(new Vector3(pos.x, pos.y, radius));
                        result.Add(new PropPlacement { prop = Catalog.pond, position = pos, scale = scale, stretchY = 1f, flipX = rng.Next(2) == 0, order = OrderPond });
                    }
                }

            // 큰 성의 깃발
            if (Catalog.banner != null && Catalog.banner.sprite != null)
                for (int i = 0, n = Count(rng, BannerPerChunk); i < n; i++)
                    if (TryPlace(rng, originX, originY, ClearRadius, blockers, PropSpacing, 5, riverHalf + 1f, out var pos))
                    {
                        result.Add(Place(Catalog.banner, pos, rng, top));
                        blockers.Add(new Vector3(pos.x, pos.y, 0.1f));
                    }

            // 무리: 같은 소품이 한곳에 모인 숲/풀밭/바위 더미. 흩뿌리기보다 먼저 놓아 자리를 잡는다
            if (totalWeight > 0f)
                for (int i = 0, n = Count(rng, Theme.propsPerChunk * DensityMultiplier * ClusterPerBaseDensity / 9f); i < n; i++)
                {
                    var main = Pick(rng);
                    if (main == null || !TryPlace(rng, originX, originY, ClearRadius, blockers, PropSpacing, 4, riverHalf + 0.9f, out var center)) continue;
                    result.Add(Place(main, center, rng, top));
                    blockers.Add(new Vector3(center.x, center.y, 0.1f));
                    for (int m = 3 + rng.Next(5); m > 0; m--)
                    {
                        var prop = rng.NextDouble() < 0.75 ? main : Pick(rng);
                        var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                        var pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (1.1f + (float)rng.NextDouble() * (ClusterRadius - 1.1f));
                        if (prop == null || !CanPlace(pos, originX, originY, ClearRadius, blockers, PropSpacing, riverHalf + 0.9f)) continue;
                        result.Add(Place(prop, pos, rng, top));
                        blockers.Add(new Vector3(pos.x, pos.y, 0.1f));
                    }
                }

            // 흩뿌린 지형 소품 (강물 위에는 서지 않는다)
            if (totalWeight > 0f)
                for (int i = 0, n = Count(rng, Theme.propsPerChunk * DensityMultiplier * ScatterBoost); i < n; i++)
                {
                    var prop = Pick(rng);
                    if (prop == null || !TryPlace(rng, originX, originY, ClearRadius, blockers, PropSpacing, 6, riverHalf + 0.9f, out var pos)) continue;
                    result.Add(Place(prop, pos, rng, top));
                    blockers.Add(new Vector3(pos.x, pos.y, 0.1f));
                }
            return result;
        }

        /// <summary>
        /// 한 칸의 바닥 얼룩 (그늘진 곳, 볕 드는 곳, 마른 흙). 소품과 달리 서로 겹쳐도 되는 부드러운 장식이라 따로 계산한다.
        /// 강 곁에는 놓지 않는다.
        /// </summary>
        public List<PropPlacement> LayoutPatches(int chunkX, int chunkY)
        {
            var result = new List<PropPlacement>();
            var rng = new System.Random(unchecked((int)Mix(Mix((uint)Seed ^ 0x5bd1e995u, (uint)chunkX), (uint)chunkY)));
            float originX = chunkX * ChunkSize, originY = chunkY * ChunkSize;
            float riverClear = HasRiver ? RiverWidth * 0.5f * BankExtra + 1.8f : 0f;
            float density = PatchPerChunk * (0.7f + 0.6f * Frac(30));

            for (int i = 0, n = Count(rng, density); i < n; i++)
            {
                var p = new Vector2(originX + (float)rng.NextDouble() * ChunkSize, originY + (float)rng.NextDouble() * ChunkSize);
                if (p.magnitude < ClearRadius) continue;
                if (HasRiver && RiverDistance(p) < riverClear) continue;

                float roll = (float)rng.NextDouble();
                Color tint;
                if (roll < 0.4f) tint = new Color(0f, 0f, 0f, 0.13f);                       // 그늘진 곳
                else if (roll < 0.75f) tint = new Color(1f, 1f, 0.75f, 0.09f);              // 볕이 드는 곳
                else if (Terrain == CastleTerrain.Mountain) tint = new Color(0.62f, 0.64f, 0.66f, 0.2f);   // 드러난 돌바닥
                else tint = new Color(0.55f, 0.42f, 0.24f, 0.24f);                          // 마른 흙

                float scale = 1.3f + (float)rng.NextDouble() * 1.4f;
                result.Add(new PropPlacement
                {
                    prop = TerrainDecals.PatchProp, position = p, scale = scale, stretchY = 0.5f + (float)rng.NextDouble() * 0.35f,
                    rotation = (float)rng.NextDouble() * 180f, order = OrderPatch, tinted = true, tint = tint,
                });
            }
            return result;
        }

        // ───────────────────────── 내부 ─────────────────────────

        PropPlacement Place(TerrainProp prop, Vector2 pos, System.Random rng, float chunkTop)
        {
            // 아래쪽(y 가 작은) 소품이 위에 그려지게 순서를 매긴다. 연못보다 항상 위.
            return new PropPlacement
            {
                prop = prop, position = pos, order = PropOrder(pos.y, chunkTop), stretchY = 1f,
                scale = Range(rng, prop.scaleMin, prop.scaleMax), flipX = rng.Next(2) == 0,
            };
        }

        TerrainProp Pick(System.Random rng)
        {
            float roll = (float)rng.NextDouble() * totalWeight;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f) continue;
                roll -= weights[i];
                if (roll <= 0f) return Theme.props[i];
            }
            for (int i = weights.Length - 1; i >= 0; i--)
                if (weights[i] > 0f) return Theme.props[i];
            return null;
        }

        /// <summary>시작 위치/이미 놓인 것/강에서 떨어진 빈 자리를 몇 번 시도해 찾는다.</summary>
        bool TryPlace(System.Random rng, float originX, float originY, float clearRadius, List<Vector3> blockers, float spacing, int attempts, float riverClearance, out Vector2 position)
        {
            for (int a = 0; a < attempts; a++)
            {
                var p = new Vector2(originX + (float)rng.NextDouble() * ChunkSize, originY + (float)rng.NextDouble() * ChunkSize);
                if (!CanPlace(p, originX, originY, clearRadius, blockers, spacing, riverClearance)) continue;
                position = p;
                return true;
            }
            position = default;
            return false;
        }

        /// <summary>이 칸 안이고, 시작 위치/이미 놓인 것/강에서 충분히 떨어져 있는가.</summary>
        bool CanPlace(Vector2 p, float originX, float originY, float clearRadius, List<Vector3> blockers, float spacing, float riverClearance)
        {
            if (p.x < originX || p.x >= originX + ChunkSize || p.y < originY || p.y >= originY + ChunkSize) return false;
            if (p.magnitude < clearRadius) return false;
            if (HasRiver && RiverDistance(p) < riverClearance) return false;
            foreach (var b in blockers)
                if (Vector2.Distance(p, new Vector2(b.x, b.y)) < Mathf.Max(spacing, b.z)) return false;
            return true;
        }

        /// <summary>평균 <paramref name="expected"/> 개: 정수 부분은 확정, 소수 부분은 확률로 하나 더.</summary>
        static int Count(System.Random rng, float expected)
        {
            int n = Mathf.FloorToInt(expected);
            return n + ((float)rng.NextDouble() < expected - n ? 1 : 0);
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        // 성 시드에서 안정적으로 뽑는 0~1 값 (인덱스마다 다른 값)
        float Frac(int index) => (Mix((uint)Seed, (uint)(index * 7919 + 13)) & 0xffff) / 65535f;

        static uint Fnv(string s)
        {
            uint h = 2166136261;
            foreach (char ch in s) { h ^= ch; h *= 16777619; }
            return h;
        }

        static uint Mix(uint a, uint b)
        {
            unchecked
            {
                uint h = a * 374761393u + b * 668265263u + 0x9e3779b9u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }
    }
}
