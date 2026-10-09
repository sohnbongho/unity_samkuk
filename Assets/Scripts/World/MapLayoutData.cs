using System;
using System.Collections.Generic;

namespace Samkuk.World
{
    /// <summary>맵 위에 놓인 것의 종류. 그리기 순서(바닥 얼룩 &lt; 강둑 &lt; 강물 &lt; 연못 &lt; 소품)와 선택 우선순위를 정한다.</summary>
    public enum MapItemKind { Prop = 0, Patch = 1, Bank = 2, Water = 3, Pond = 4 }

    /// <summary>저장 파일 속 맵 물건 하나 (소품, 연못, 깃발, 바닥 얼룩, 강 토막). 그림은 스프라이트 이름으로 찾는다.</summary>
    [Serializable]
    public class MapItem
    {
        public string sprite;
        public int kind;
        public float x, y;
        public float scale = 1f;
        public float stretchY = 1f;
        public float rotation;
        public bool flipX;
        public bool tinted;
        public float r = 1f, g = 1f, b = 1f, a = 1f;
    }

    /// <summary>12x12 유닛 칸 하나의 직접 편집한 내용. 이 칸은 자동 생성 대신 이 목록을 그대로 쓴다.</summary>
    [Serializable]
    public class MapChunk
    {
        public int cx, cy;
        public List<MapItem> items = new List<MapItem>();
    }

    /// <summary>
    /// 맵 편집기로 고친 맵 한 장 (성 하나, 또는 "FreeBattle" = 성 없이 시작한 판).
    /// 편집한 칸(chunks)만 들어 있고, 나머지 칸은 평소처럼 자동으로 만들어진다. JSON 으로 저장된다(<see cref="MapStore"/>).
    /// </summary>
    [Serializable]
    public class MapLayoutData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string castleId;
        [UnityEngine.Tooltip("바닥 지형을 성의 원래 지형과 다르게 쓸 때의 CastleTerrain 이름. 비어 있으면 성의 지형 그대로")] public string terrain;
        public List<MapChunk> chunks = new List<MapChunk>();

        public MapChunk Find(int cx, int cy)
        {
            foreach (var c in chunks)
                if (c.cx == cx && c.cy == cy) return c;
            return null;
        }

        public int ItemCount
        {
            get
            {
                int n = 0;
                foreach (var c in chunks) n += c.items.Count;
                return n;
            }
        }
    }
}
