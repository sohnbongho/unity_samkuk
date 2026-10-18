using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>
    /// 선택 가능한 장수 목록. 에셋은 <c>Assets/Resources/HeroCatalog.asset</c> 에 두어 씬 참조 없이도(타이틀의 협동 방 장수 선택)
    /// <see cref="Load"/> 로 읽을 수 있다. 전투 씬의 HeroSelectController 는 여전히 직렬화 참조로 같은 에셋을 가리킨다.
    /// </summary>
    [CreateAssetMenu(menuName = "Samkuk/Hero Catalog", fileName = "HeroCatalog")]
    public class HeroCatalog : ScriptableObject
    {
        public const string ResourceName = "HeroCatalog";
        static HeroCatalog cached;

        public List<HeroData> heroes = new List<HeroData>();

        /// <summary>Resources 의 카탈로그. 없으면 null (셋업 전).</summary>
        public static HeroCatalog Load()
        {
            if (cached == null) cached = Resources.Load<HeroCatalog>(ResourceName);
            return cached;
        }

        /// <summary>서버가 쓰는 장수 id(에셋 파일명)로 찾는다. 없으면 null.</summary>
        public HeroData Find(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return null;
            foreach (var h in heroes)
                if (h != null && h.name == heroId) return h;
            return null;
        }
    }
}
