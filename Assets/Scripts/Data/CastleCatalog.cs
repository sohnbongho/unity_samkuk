using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>
    /// 내정 모드에서 쓰는 성 전체 목록 (삼국지3처럼 46곳). 에셋은 <c>Assets/Resources/CastleCatalog.asset</c> 에 두어
    /// 씬 참조 없이도(타이틀의 협동 방 성 고르기, 협동 전투 배경) <see cref="Load"/> 로 읽는다. 내정/맵 편집기 씬의 직렬화 참조는 그대로다.
    /// </summary>
    [CreateAssetMenu(menuName = "Samkuk/Castle Catalog", fileName = "CastleCatalog")]
    public class CastleCatalog : ScriptableObject
    {
        public const string ResourceName = "CastleCatalog";
        static CastleCatalog cached;

        public List<CastleData> castles = new List<CastleData>();

        /// <summary>Resources 의 카탈로그. 없으면 null (셋업 전).</summary>
        public static CastleCatalog Load()
        {
            if (cached == null) cached = Resources.Load<CastleCatalog>(ResourceName);
            return cached;
        }

        /// <summary>서버가 쓰는 성 id(에셋 파일명, 예: Castle_Luoyang)로 찾는다. 없으면 null.</summary>
        public CastleData FindAsset(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            foreach (var c in castles)
                if (c != null && c.name == assetName) return c;
            return null;
        }

        /// <summary>아이디(영문)로 성을 찾는다. 없으면 null.</summary>
        public CastleData Find(string id)
        {
            foreach (var c in castles)
                if (c != null && c.id == id) return c;
            return null;
        }
    }
}
