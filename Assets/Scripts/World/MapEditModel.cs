using System;
using System.Collections.Generic;
using Samkuk.Data;
using UnityEngine;

namespace Samkuk.World
{
    /// <summary>맵 위의 물건 하나를 가리킨다 (어느 칸의 몇 번째).</summary>
    public struct ItemRef : IEquatable<ItemRef>
    {
        public Vector2Int chunk;
        public int index;

        public static ItemRef None => new ItemRef { index = -1 };
        public bool IsValid => index >= 0;

        public ItemRef(Vector2Int chunk, int index)
        {
            this.chunk = chunk;
            this.index = index;
        }

        public bool Equals(ItemRef other) => chunk == other.chunk && index == other.index;
        public override bool Equals(object obj) => obj is ItemRef r && Equals(r);
        public override int GetHashCode() => chunk.GetHashCode() * 397 ^ index;
    }

    /// <summary>
    /// 맵 편집기의 편집 규칙 (화면 없이 계산하는 순수 로직이라 테스트할 수 있다).
    /// 칸을 처음 고칠 때 지금 보이는 자동 생성 내용을 그대로 옮겨 와 시작점으로 삼고(<see cref="TerrainMap.EnsureCustomChunk"/>),
    /// 이후 그 칸은 직접 고친 내용을 쓴다. 한 번의 조작(붓질 한 획 등)은 실행 취소 한 번이다.
    /// 고친 칸은 <see cref="ConsumeDirtyChunks"/> 로 알려 주어 화면의 소품을 다시 만들게 한다.
    /// </summary>
    public sealed class MapEditModel
    {
        const int MaxUndo = 60;

        readonly HashSet<Vector2Int> dirtyChunks = new HashSet<Vector2Int>();
        readonly List<TerrainMap.CustomSnapshot> undo = new List<TerrainMap.CustomSnapshot>();
        readonly List<TerrainMap.CustomSnapshot> redo = new List<TerrainMap.CustomSnapshot>();
        bool inStroke;

        public TerrainMap Map { get; private set; }

        /// <summary>마지막 저장 뒤에 고친 것이 있는가.</summary>
        public bool Unsaved { get; private set; }

        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;

        public MapEditModel(TerrainMap map) => SetMap(map);

        /// <summary>편집할 맵을 바꾼다 (실행 취소 기록은 비운다).</summary>
        public void SetMap(TerrainMap map)
        {
            Map = map;
            undo.Clear();
            redo.Clear();
            dirtyChunks.Clear();
            inStroke = false;
            Unsaved = false;
        }

        public void MarkSaved() => Unsaved = false;

        /// <summary>편집 기록에 남지 않는 변경(바닥 지형 바꾸기)이 있었음을 알린다.</summary>
        public void MarkChanged() => Unsaved = true;

        /// <summary>고쳐서 화면에서 다시 만들어야 하는 칸들 (한 번 알려 주면 비운다).</summary>
        public List<Vector2Int> ConsumeDirtyChunks()
        {
            var list = new List<Vector2Int>(dirtyChunks);
            dirtyChunks.Clear();
            return list;
        }

        // ───────────────────────── 실행 취소 ─────────────────────────

        /// <summary>한 획(드래그 한 번)의 시작: 여기서 한 번만 실행 취소 지점을 만든다. <see cref="EndStroke"/> 로 끝낸다.</summary>
        public void BeginStroke()
        {
            if (inStroke) return;
            PushUndo();
            inStroke = true;
        }

        public void EndStroke() => inStroke = false;

        void Touch()
        {
            if (!inStroke) PushUndo();
            Unsaved = true;
        }

        void PushUndo()
        {
            undo.Add(Map.SnapshotCustom());
            if (undo.Count > MaxUndo) undo.RemoveAt(0);
            redo.Clear();
        }

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            redo.Add(Map.SnapshotCustom());
            Restore(undo[undo.Count - 1]);
            undo.RemoveAt(undo.Count - 1);
            return true;
        }

        public bool Redo()
        {
            if (redo.Count == 0) return false;
            undo.Add(Map.SnapshotCustom());
            Restore(redo[redo.Count - 1]);
            redo.RemoveAt(redo.Count - 1);
            return true;
        }

        void Restore(TerrainMap.CustomSnapshot snapshot)
        {
            foreach (var key in Map.CustomChunks) MarkDirtyWithNeighbors(key);
            foreach (var key in snapshot.Chunks) MarkDirtyWithNeighbors(key);
            Map.RestoreCustom(snapshot);
            Unsaved = true;
        }

        /// <summary>높이가 바뀌면 이웃 칸의 절벽 옆면·벽도 달라지므로 둘레 칸까지 다시 만들게 한다.</summary>
        void MarkDirtyWithNeighbors(Vector2Int chunk)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++) dirtyChunks.Add(new Vector2Int(chunk.x + dx, chunk.y + dy));
        }

        // ───────────────────────── 높이 (Step 16-2) ─────────────────────────

        /// <summary>위치가 속한 셀의 층과 경사로.</summary>
        public void GetCell(Vector2 position, out int level, out RampDir ramp)
        {
            TerrainHeight.Locate(position, out var chunk, out int cx, out int cy, out _, out _);
            var h = Map.GetHeights(chunk);
            level = h.Level(cx, cy);
            ramp = h.Ramp(cx, cy);
        }

        /// <summary>셀의 층을 바꾼다 (<paramref name="delta"/> 만큼, 0~최대층으로 잘림). 바뀌지 않으면 false. 경사로는 층이 0이 되면 사라진다.</summary>
        public bool ChangeLevel(Vector2 position, int delta)
        {
            TerrainHeight.Locate(position, out var chunk, out int cx, out int cy, out _, out _);
            var current = Map.GetHeights(chunk);
            int level = Mathf.Clamp(current.Level(cx, cy) + delta, 0, TerrainHeight.MaxLevel);
            if (level == current.Level(cx, cy)) return false;
            Touch();
            Map.EnsureCustomHeights(chunk).Set(cx, cy, level, current.Ramp(cx, cy));
            MarkDirtyWithNeighbors(chunk);
            return true;
        }

        /// <summary>셀을 경사로로(또는 경사로를 없앤다). 0층 셀은 한 층 올려 경사로로 만든다.</summary>
        public bool SetRamp(Vector2 position, RampDir ramp)
        {
            TerrainHeight.Locate(position, out var chunk, out int cx, out int cy, out _, out _);
            var current = Map.GetHeights(chunk);
            int level = current.Level(cx, cy);
            if (ramp != RampDir.None && level == 0) level = 1;
            if (level == current.Level(cx, cy) && ramp == current.Ramp(cx, cy)) return false;
            Touch();
            Map.EnsureCustomHeights(chunk).Set(cx, cy, level, ramp);
            MarkDirtyWithNeighbors(chunk);
            return true;
        }

        /// <summary>셀을 평지(0층)로.</summary>
        public bool Flatten(Vector2 position)
        {
            TerrainHeight.Locate(position, out var chunk, out int cx, out int cy, out _, out _);
            var current = Map.GetHeights(chunk);
            if (current.Level(cx, cy) == 0 && current.Ramp(cx, cy) == RampDir.None) return false;
            Touch();
            Map.EnsureCustomHeights(chunk).Set(cx, cy, 0, RampDir.None);
            MarkDirtyWithNeighbors(chunk);
            return true;
        }

        // ───────────────────────── 찾기 ─────────────────────────

        /// <summary>돌릴 수 있는 물건인가. 서 있는 소품(나무, 바위 등)은 눕히면 어색하므로 돌리지 않는다 (반전·크기만).</summary>
        public static bool CanRotate(PropPlacement p) => !(p.prop != null && p.prop.standing);

        public bool TryGet(ItemRef r, out PropPlacement placement)
        {
            placement = default;
            if (!r.IsValid) return false;
            var list = Map.GetChunk(r.chunk.x, r.chunk.y);
            if (r.index >= list.Count) return false;
            placement = list[r.index];
            return true;
        }

        /// <summary>눈에 보이는 중심 (소품은 바닥 점 위쪽, 나머지는 위치 그대로).</summary>
        public static Vector2 VisualCenter(PropPlacement p)
        {
            if (TerrainMap.KindOf(p) != MapItemKind.Prop || p.prop == null || p.prop.sprite == null) return p.position;
            return p.position + Vector2.up * (p.prop.sprite.bounds.size.y * 0.5f * p.scale);
        }

        /// <summary>눌러서 잡히는 반지름.</summary>
        public static float PickRadius(PropPlacement p)
        {
            if (p.prop == null || p.prop.sprite == null) return 0.5f;
            var size = p.prop.sprite.bounds.size;
            float extent = Mathf.Max(size.x, size.y * (p.stretchY > 0f ? p.stretchY : 1f)) * p.scale;
            return Mathf.Clamp(extent * 0.35f, 0.4f, 2.2f);
        }

        /// <summary>
        /// 위치에서 가장 가까운 물건을 찾는다. 소품 &gt; 연못/강 &gt; 바닥 얼룩 순으로 먼저 잡힌다
        /// (얼룩은 크고 소품 밑에 깔려 있으므로 소품을 눌렀는데 얼룩이 잡히면 안 된다).
        /// </summary>
        public bool FindNearest(Vector2 position, out ItemRef result)
        {
            result = ItemRef.None;
            float best = float.MaxValue;
            var center = TerrainMap.ChunkOf(position);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var chunk = new Vector2Int(center.x + dx, center.y + dy);
                    var list = Map.GetChunk(chunk.x, chunk.y);
                    for (int i = 0; i < list.Count; i++)
                    {
                        var p = list[i];
                        float radius = PickRadius(p);
                        float dist = Vector2.Distance(position, VisualCenter(p));
                        if (dist > radius) continue;

                        float score = dist / radius;
                        var kind = TerrainMap.KindOf(p);
                        if (kind == MapItemKind.Patch) score += 2f;
                        else if (kind == MapItemKind.Bank) score += 1f;
                        else if (kind == MapItemKind.Water) score += 0.5f;
                        if (score < best)
                        {
                            best = score;
                            result = new ItemRef(chunk, i);
                        }
                    }
                }
            return result.IsValid;
        }

        // ───────────────────────── 고치기 ─────────────────────────

        /// <summary>물건을 놓는다 (소품이면 그리기 순서를 위치에 맞게 다시 정한다). 놓인 물건을 가리키는 참조를 돌려준다.</summary>
        public ItemRef Add(PropPlacement placement)
        {
            Touch();
            var chunk = TerrainMap.ChunkOf(placement.position);
            placement.order = Reorder(placement, chunk);
            var list = Map.EnsureCustomChunk(chunk);
            list.Add(placement);
            dirtyChunks.Add(chunk);
            return new ItemRef(chunk, list.Count - 1);
        }

        /// <summary>물건을 바꾼다 (이동, 크기, 회전 등). 다른 칸으로 옮겨졌으면 칸을 옮기고 새 참조를 돌려준다.</summary>
        public ItemRef Replace(ItemRef r, PropPlacement placement)
        {
            if (!TryGet(r, out _)) return ItemRef.None;
            Touch();
            var newChunk = TerrainMap.ChunkOf(placement.position);
            placement.order = Reorder(placement, newChunk);

            var oldList = Map.EnsureCustomChunk(r.chunk);
            dirtyChunks.Add(r.chunk);
            if (newChunk == r.chunk)
            {
                oldList[r.index] = placement;
                return r;
            }

            oldList.RemoveAt(r.index);
            var newList = Map.EnsureCustomChunk(newChunk);
            newList.Add(placement);
            dirtyChunks.Add(newChunk);
            return new ItemRef(newChunk, newList.Count - 1);
        }

        public bool Remove(ItemRef r)
        {
            if (!TryGet(r, out _)) return false;
            Touch();
            Map.EnsureCustomChunk(r.chunk).RemoveAt(r.index);
            dirtyChunks.Add(r.chunk);
            return true;
        }

        /// <summary>둥근 범위 안의 물건을 모두 지운다 (<paramref name="filter"/> 가 있으면 그것만). 지운 개수를 돌려준다.</summary>
        public int EraseCircle(Vector2 center, float radius, Func<PropPlacement, bool> filter = null)
        {
            var hits = new List<ItemRef>();
            int minX = Mathf.FloorToInt((center.x - radius) / TerrainMap.ChunkSize), maxX = Mathf.FloorToInt((center.x + radius) / TerrainMap.ChunkSize);
            int minY = Mathf.FloorToInt((center.y - radius) / TerrainMap.ChunkSize), maxY = Mathf.FloorToInt((center.y + radius) / TerrainMap.ChunkSize);
            for (int cy = minY; cy <= maxY; cy++)
                for (int cx = minX; cx <= maxX; cx++)
                {
                    var list = Map.GetChunk(cx, cy);
                    for (int i = 0; i < list.Count; i++)
                    {
                        var p = list[i];
                        if (Vector2.Distance(center, p.position) > radius && Vector2.Distance(center, VisualCenter(p)) > radius) continue;
                        if (filter != null && !filter(p)) continue;
                        hits.Add(new ItemRef(new Vector2Int(cx, cy), i));
                    }
                }
            if (hits.Count == 0) return 0;

            Touch();
            // 뒤에서부터 지워야 앞쪽 번호가 어긋나지 않는다
            hits.Sort((a, b) => a.chunk != b.chunk ? (a.chunk.y != b.chunk.y ? a.chunk.y.CompareTo(b.chunk.y) : a.chunk.x.CompareTo(b.chunk.x)) : b.index.CompareTo(a.index));
            foreach (var h in hits)
            {
                Map.EnsureCustomChunk(h.chunk).RemoveAt(h.index);
                dirtyChunks.Add(h.chunk);
            }
            return hits.Count;
        }

        /// <summary>칸을 자동 생성 상태로 되돌린다 (직접 고친 물건과 높이를 버린다).</summary>
        public bool ResetChunk(Vector2Int chunk)
        {
            if (!Map.IsCustom(chunk)) return false;
            Touch();
            Map.RemoveCustomChunk(chunk);
            MarkDirtyWithNeighbors(chunk);
            return true;
        }

        /// <summary>칸을 텅 비운다 (소품, 얼룩, 강 모두, 높이는 평지로).</summary>
        public void ClearChunk(Vector2Int chunk)
        {
            Touch();
            Map.SetCustomChunk(chunk, new List<PropPlacement>());
            Map.SetCustomHeights(chunk, new HeightChunk());
            MarkDirtyWithNeighbors(chunk);
        }

        /// <summary>맵 전체를 자동 생성 상태로 되돌린다.</summary>
        public bool ResetAll()
        {
            if (!Map.HasCustom) return false;
            Touch();
            foreach (var key in Map.CustomChunks) MarkDirtyWithNeighbors(key);
            Map.ClearCustom();
            return true;
        }

        static int Reorder(PropPlacement p, Vector2Int chunk) => TerrainMap.OrderOf(TerrainMap.KindOf(p), p.position.y, chunk.y);
    }

    /// <summary>팔레트(놓을 수 있는 그림 목록)의 한 칸.</summary>
    public sealed class PaletteEntry
    {
        public string label;
        public string group;
        public TerrainProp prop;
        public MapItemKind kind;
        public float scaleMin = 1f, scaleMax = 1f;
        public float stretchMin = 1f, stretchMax = 1f;
        public bool randomRotation;
        public bool tinted;
        public Color tint = Color.white;
        /// <summary>같이 놓는 짝 (강물을 놓으면 강둑도 함께).</summary>
        public PaletteEntry companion;
        /// <summary>이어서 칠할 때 물건 사이 간격.</summary>
        public float spacing = 1.15f;
        /// <summary>끌면서 칠할 때 끌어 가는 방향으로 눕는가 (강).</summary>
        public bool alignToStroke;

        public Sprite Sprite => prop != null ? prop.sprite : null;

        /// <summary>이 항목으로 물건 하나를 만든다. <paramref name="rng"/> 로 크기와 반전을 무작위로 정한다.</summary>
        public PropPlacement Create(Vector2 position, System.Random rng, float rotation = 0f)
        {
            return new PropPlacement
            {
                prop = prop,
                position = position,
                scale = scaleMin + (scaleMax - scaleMin) * (float)rng.NextDouble(),
                stretchY = stretchMin + (stretchMax - stretchMin) * (float)rng.NextDouble(),
                rotation = randomRotation ? (float)rng.NextDouble() * 180f : rotation,
                flipX = kind == MapItemKind.Prop && rng.Next(2) == 0,
                order = TerrainMap.OrderOf(kind, position.y, TerrainMap.ChunkOf(position).y),
                tinted = tinted,
                tint = tint,
            };
        }
    }

    /// <summary>팔레트를 만든다: 지형별 소품(+강), 공용(연못, 깃발, 바닥 얼룩).</summary>
    public static class MapPalette
    {
        public const string SharedGroup = "공용";

        public static string TerrainLabel(CastleTerrain terrain)
        {
            switch (terrain)
            {
                case CastleTerrain.Steppe: return "초원";
                case CastleTerrain.Mountain: return "산악";
                case CastleTerrain.River: return "강변";
                case CastleTerrain.Jungle: return "남방";
                case CastleTerrain.Loess: return "황토";
                default: return "평야";
            }
        }

        /// <summary>그룹 이름 목록(지형 순서대로, 마지막이 공용)과 항목을 만든다.</summary>
        public static List<PaletteEntry> Build(TerrainThemeCatalog catalog)
        {
            var entries = new List<PaletteEntry>();
            if (catalog == null) return entries;

            foreach (var theme in catalog.themes)
            {
                if (theme == null) continue;
                string group = TerrainLabel(theme.terrain);
                foreach (var p in theme.props)
                {
                    if (p == null || p.sprite == null) continue;
                    entries.Add(new PaletteEntry
                    {
                        label = p.name, group = group, prop = p, kind = MapItemKind.Prop,
                        scaleMin = p.scaleMin, scaleMax = Mathf.Max(p.scaleMin, p.scaleMax),
                    });
                }

                if (theme.riverWater != null && theme.riverBank != null)
                {
                    var bank = new PaletteEntry
                    {
                        label = "강둑", group = group, kind = MapItemKind.Bank, spacing = TerrainMap.RiverStep,
                        prop = new TerrainProp { name = "RiverBank", sprite = theme.riverBank },
                        stretchMin = TerrainMap.RiverStretch(theme.riverWidth, true), stretchMax = TerrainMap.RiverStretch(theme.riverWidth, true),
                        alignToStroke = true,
                    };
                    entries.Add(new PaletteEntry
                    {
                        label = "강", group = group, kind = MapItemKind.Water, spacing = TerrainMap.RiverStep,
                        prop = new TerrainProp { name = "RiverWater", sprite = theme.riverWater },
                        stretchMin = TerrainMap.RiverStretch(theme.riverWidth, false), stretchMax = TerrainMap.RiverStretch(theme.riverWidth, false),
                        alignToStroke = true, companion = bank,
                    });
                }
            }

            if (catalog.pond != null && catalog.pond.sprite != null)
                entries.Add(new PaletteEntry { label = "연못", group = SharedGroup, prop = catalog.pond, kind = MapItemKind.Pond, scaleMin = catalog.pond.scaleMin, scaleMax = catalog.pond.scaleMax, spacing = 3.5f });
            if (catalog.banner != null && catalog.banner.sprite != null)
                entries.Add(new PaletteEntry { label = "깃발", group = SharedGroup, prop = catalog.banner, kind = MapItemKind.Prop, scaleMin = catalog.banner.scaleMin, scaleMax = catalog.banner.scaleMax });

            AddPatch(entries, "그늘", new Color(0f, 0f, 0f, 0.13f));
            AddPatch(entries, "볕", new Color(1f, 1f, 0.75f, 0.09f));
            AddPatch(entries, "마른 흙", new Color(0.55f, 0.42f, 0.24f, 0.24f));
            AddPatch(entries, "돌바닥", new Color(0.62f, 0.64f, 0.66f, 0.2f));
            return entries;
        }

        static void AddPatch(List<PaletteEntry> entries, string label, Color tint)
        {
            entries.Add(new PaletteEntry
            {
                label = "얼룩: " + label, group = SharedGroup, kind = MapItemKind.Patch, prop = TerrainDecals.PatchProp,
                scaleMin = 1.3f, scaleMax = 2.7f, stretchMin = 0.5f, stretchMax = 0.85f, randomRotation = true,
                tinted = true, tint = tint, spacing = 2.4f,
            });
        }

        /// <summary>팔레트에 나오는 그룹 이름들 (등장 순서, 중복 없이).</summary>
        public static List<string> Groups(List<PaletteEntry> entries)
        {
            var groups = new List<string>();
            foreach (var e in entries)
                if (!groups.Contains(e.group)) groups.Add(e.group);
            return groups;
        }
    }
}
