using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>내정 모드에서 쓰는 성 전체 목록 (삼국지3처럼 46곳).</summary>
    [CreateAssetMenu(menuName = "Samkuk/Castle Catalog", fileName = "CastleCatalog")]
    public class CastleCatalog : ScriptableObject
    {
        public List<CastleData> castles = new List<CastleData>();

        /// <summary>아이디(영문)로 성을 찾는다. 없으면 null.</summary>
        public CastleData Find(string id)
        {
            foreach (var c in castles)
                if (c != null && c.id == id) return c;
            return null;
        }
    }
}
