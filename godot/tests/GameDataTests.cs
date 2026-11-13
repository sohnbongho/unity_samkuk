using System.IO;
using NUnit.Framework;
using Samkuk.Compat;
using Samkuk.Data;
using Samkuk.UI;

namespace Samkuk.Tests
{
    /// <summary>데이터 창고(Godot 전환 G2): godot/data 의 JSON 이 빠짐없이 읽히고 참조·그림이 이어지는지.</summary>
    public class GameDataTests
    {
        [SetUp]
        public void SetUp()
        {
            GameData.Clear();
            AssetDatabase.EnsureLoaded();
        }

        [Test]
        public void Load_ReadsEveryFile_WithoutProblems()
        {
            Assert.IsEmpty(GameData.Problems, "모르는 형식·없는 참조·겹친 이름이 없어야 한다");
            // 데이터 파일만 센다 (씬·프리팹·프로젝트 설정·그림 색인은 $type 이 없다)
            int files = 0;
            foreach (string f in Directory.GetFiles(Path.Combine(AssetDatabase.GodotRoot, "data"), "*.json", SearchOption.AllDirectories))
                if (File.ReadAllText(f).Contains("\"$type\"")) files++;
            Assert.AreEqual(files, GameData.Count, "파일 하나가 데이터 한 건");
            Assert.AreEqual(46, GameData.All<CastleData>().Count);
            Assert.AreEqual(27, GameData.All<WeaponData>().Count);
            Assert.AreEqual(10, GameData.All<EnemyData>().Count);
            Assert.AreEqual(5, GameData.All<HeroData>().Count);
        }

        [Test]
        public void References_PointToTheSameObjects()
        {
            var hero = GameData.Get<HeroData>("Hero_CaoCao");
            Assert.IsNotNull(hero);
            Assert.AreEqual("조조", hero.displayName);
            Assert.AreSame(GameData.Get<WeaponData>("Weapon_YitianSword"), hero.startingWeapon, "참조는 복사본이 아니라 같은 객체");
            Assert.AreEqual(5, hero.weapons.Count);
            Assert.IsTrue(hero.startingWeapon.comboSwings, "유니티 YAML 의 1 은 true");
            Assert.AreEqual(WeaponType.Slash, hero.startingWeapon.type, "열거형은 숫자로 저장돼 있다");

            var stage = GameData.Get<StageData>("Stage_YellowTurban");
            Assert.Greater(stage.waves.Count, 0);
            Assert.IsNotNull(stage.waves[0].enemies[0].data, "구조체 안의 참조도 이어진다");
        }

        [Test]
        public void Sprites_CarrySizePivotAndPath_AndFilesExist()
        {
            var theme = GameData.Get<TerrainTheme>("Theme_Plain");
            var tree = theme.props.Find(p => p.name == "Prop_Plain_Tree");
            Assert.IsNotNull(tree?.sprite);
            Assert.AreEqual("res://sprites/Terrain/Prop_Plain_Tree.png", tree.sprite.path);
            Assert.AreEqual(32f, tree.sprite.pixelsPerUnit);
            Assert.AreEqual(72f / 32f, tree.sprite.bounds.size.x, 1e-4f);
            Assert.Less(tree.sprite.pivot.y, 8f, "피벗은 발 (아래쪽, 픽셀 단위)");
            Assert.AreSame(tree.sprite, GameData.Get<TerrainTheme>("Theme_Plain").props.Find(p => p.name == "Prop_Plain_Tree").sprite);

            // 데이터가 가리키는 그림 파일이 모두 있다
            foreach (var t in GameData.All<TerrainTheme>())
                foreach (var p in t.props)
                    if (p.sprite != null) Assert.IsTrue(File.Exists(ToDisk(p.sprite.path)), p.sprite.path);
            foreach (var h in GameData.All<HeroData>())
            {
                Assert.IsTrue(File.Exists(ToDisk(h.walkSheet.path)), h.name);
                Assert.AreEqual(192, h.walkSheet.width);
            }
            foreach (var c in GameData.All<CastleData>())
                Assert.IsTrue(File.Exists(ToDisk(c.background.path)), c.name);
        }

        [Test]
        public void Resources_Load_FindsCatalogsByName()
        {
            Assert.IsNotNull(CastleCatalog.Load());
            Assert.AreEqual(46, CastleCatalog.Load().castles.Count);
            TerrainThemeCatalog.Use(null);
            Assert.AreEqual(6, TerrainThemeCatalog.Load().themes.Count);
            UiTheme.ResetCache();
            Assert.IsNotNull(UiTheme.Get().frame, "UI 테마의 프레임 그림");
            Assert.IsNotNull(UiTheme.Get().frame.border, "9-슬라이스 테두리");
            UiTheme.ResetCache();
        }

        static string ToDisk(string resPath) => Path.Combine(AssetDatabase.GodotRoot, resPath.Substring("res://".Length));
    }
}
