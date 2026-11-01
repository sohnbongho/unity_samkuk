using Samkuk.Data;

namespace Samkuk.Field
{
    /// <summary>
    /// 야전 테스트 모드가 씬을 다시 열어도 기억하는 것: 마지막 준비 화면 값(편성, 배치, 장수 이름, 지형).
    /// 결과창의 [다시]와 [준비 화면]이 같은 조건으로 돌아오게 한다. 저장 데이터(`SaveData`)에는 넣지 않는다 — 테스트 모드라 영구 저장이 필요 없다.
    /// </summary>
    public static class FieldSession
    {
        public static FieldSetup LastSetup { get; set; }
        public static CastleTerrain LastTerrain { get; set; } = CastleTerrain.Plain;
        public static int LastHeroIndex { get; set; }
        /// <summary>마지막으로 고른 장수 (무기 연출·능력치, 결과창 [다시]용).</summary>
        public static HeroData LastHero { get; set; }
        /// <summary>적 배치를 매번 무작위로 고르는가 (준비 화면 옵션).</summary>
        public static bool EnemyFormationRandom { get; set; } = true;
        /// <summary>씬을 새로 열 때 준비 화면을 건너뛰고 마지막 조건으로 바로 시작할지. 지금은 결과창 [다시]가 씬 안에서 다시 시작해 쓰는 곳이 없고,
        /// 뒤에 출진 흐름이 야전 씬을 열 때 쓸 자리다.</summary>
        public static bool StartImmediately { get; set; }
    }
}
