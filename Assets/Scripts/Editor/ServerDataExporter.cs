using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Pickups;
using Samkuk.Player;
using Samkuk.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 15-2: 서버 데이터 내보내기. 협동 모드는 서버(csharpServer)가 전투를 시뮬레이션하므로
    /// 유니티의 ScriptableObject(적/무기/패시브/진화/스킬/장수/스테이지/선택지 카탈로그)와 씬·프리팹에 직렬화된 밸런스 값을
    /// JSON 으로 적어 서버가 기동 때 읽게 한다. **원본은 유니티 에셋**이고 JSON 은 생성물이다(값을 고칠 땐 에셋을 고치고 다시 내보낸다).
    ///   메뉴 Samkuk > Export Server Data  →  <see cref="DefaultOutputDir"/> (csharpServer/GameServer/Data)
    /// 규칙: id 는 에셋 파일명(예: Enemy_Soldier), 열거형은 이름 문자열, 키는 camelCase, 들여쓰기 2칸, 색/스프라이트 같은 표시용 값은 뺀다.
    /// 스키마는 csharpServer/Docs/coop-design.md 4절과 docs/NETWORK.md "서버 데이터".
    /// </summary>
    public static class ServerDataExporter
    {
        public const string DefaultOutputDir = @"D:\01-AgentLegion\csharpServer\GameServer\Data";
        const string BattleScenePath = Step9TitleSetup.BattleScenePath;

        [MenuItem("Samkuk/Export Server Data")]
        public static void ExportMenu()
        {
            var files = Export(DefaultOutputDir);
            Debug.Log($"[ServerData] {files.Count}개 파일을 내보냈다 → {DefaultOutputDir}\n" + string.Join("\n", files));
            EditorUtility.RevealInFinder(Path.Combine(DefaultOutputDir, "enemies.json"));
        }

        /// <summary>모든 JSON 을 dir 에 쓴다. 쓴 파일 경로 목록을 돌려준다 (명령줄/검증용).</summary>
        public static List<string> Export(string dir)
        {
            Directory.CreateDirectory(dir);
            var written = new List<string>();
            void Save(string name, string json)
            {
                string path = Path.Combine(dir, name);
                File.WriteAllText(path, json, new UTF8Encoding(false));
                written.Add(path);
            }

            Save("enemies.json", Enemies());
            Save("weapons.json", Weapons());
            Save("passives.json", Passives());
            Save("evolutions.json", Evolutions());
            Save("skills.json", Skills());
            Save("heroes.json", Heroes());
            Save("stages.json", Stages());
            Save("upgrade_catalog.json", UpgradeCatalogJson());
            Save("balance.json", Balance());
            Save("export_info.json", Info());
            return written;
        }

        // ── 에셋 ──

        /// <summary>서버가 쓰는 식별자: 에셋 파일명(확장자 없이). 참조가 비어 있으면 null.</summary>
        public static string Id(UnityEngine.Object asset)
        {
            if (asset == null) return null;
            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? asset.name : Path.GetFileNameWithoutExtension(path);
        }

        static List<T> All<T>() where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) list.Add(a);
            }
            list.Sort((a, b) => string.CompareOrdinal(Id(a), Id(b)));
            return list;
        }

        static string Enemies()
        {
            var j = new Json();
            j.BeginArray();
            foreach (var e in All<EnemyData>())
            {
                j.BeginObject();
                j.Set("id", Id(e)).Set("displayName", e.displayName).Set("role", e.role.ToString()).Set("scale", e.scale)
                 .Set("moveSpeed", e.moveSpeed).Set("maxHp", e.maxHp).Set("contactDamage", e.contactDamage).Set("expReward", e.expReward)
                 .Set("chargeInterval", e.chargeInterval).Set("chargeWindup", e.chargeWindup).Set("chargeDuration", e.chargeDuration)
                 .Set("chargeSpeedMultiplier", e.chargeSpeedMultiplier)
                 .Set("attackRange", e.attackRange).Set("fireInterval", e.fireInterval).Set("projectileSpeed", e.projectileSpeed)
                 .Set("projectileDamage", e.projectileDamage).Set("projectileLifetime", e.projectileLifetime).Set("projectileSize", e.projectileSize)
                 .Set("colliderRadius", e.colliderRadius)
                 .Set("radius", e.colliderRadius * e.scale); // Enemy.Radius 와 같은 식: 실제 충돌 반지름(스케일 적용)
                j.EndObject();
            }
            j.EndArray();
            return j.ToString();
        }

        static string Weapons()
        {
            var j = new Json();
            j.BeginArray();
            foreach (var w in All<WeaponData>())
            {
                j.BeginObject();
                j.Set("id", Id(w)).Set("displayName", w.displayName).Set("description", w.description).Set("type", w.type.ToString())
                 .Set("damage", w.damage).Set("cooldown", w.cooldown).Set("range", w.range).Set("count", w.count)
                 .Set("projectileSpeed", w.projectileSpeed).Set("pierce", w.pierce).Set("duration", w.duration).Set("size", w.size)
                 .Set("rotateSpeed", w.rotateSpeed).Set("tickInterval", w.tickInterval).Set("knockback", w.knockback)
                 .Set("swingArcDegrees", w.swingArcDegrees).Set("comboSwings", w.comboSwings).Set("swingDuration", w.swingDuration)
                 .Set("trailTravel", w.trailTravel).Set("trailScale", w.trailScale).Set("trailDamageRatio", w.trailDamageRatio)
                 .Set("maxLevel", w.maxLevel).Set("damagePerLevel", w.damagePerLevel).Set("cooldownReductionPerLevel", w.cooldownReductionPerLevel)
                 .Set("levelsPerExtraCount", w.levelsPerExtraCount);
                j.EndObject();
            }
            j.EndArray();
            return j.ToString();
        }

        static string Passives()
        {
            var j = new Json();
            j.BeginArray();
            foreach (var p in All<PassiveData>())
            {
                j.BeginObject();
                j.Set("id", Id(p)).Set("displayName", p.displayName).Set("description", p.description).Set("type", p.type.ToString())
                 .Set("valuePerLevel", p.valuePerLevel).Set("maxLevel", p.maxLevel);
                j.EndObject();
            }
            j.EndArray();
            return j.ToString();
        }

        static string Evolutions()
        {
            var j = new Json();
            j.BeginArray();
            foreach (var e in All<EvolutionData>())
            {
                if (!e.IsValid) continue; // 참조가 빈 진화는 게임에서도 안 쓰인다
                j.BeginObject();
                j.Set("id", Id(e)).Set("baseWeapon", Id(e.baseWeapon)).Set("requiredPassive", Id(e.requiredPassive))
                 .Set("evolvedWeapon", Id(e.evolvedWeapon)).Set("requiredLevel", e.requiredLevel).Set("requiredPassiveLevel", e.requiredPassiveLevel);
                j.EndObject();
            }
            j.EndArray();
            return j.ToString();
        }

        static string Skills()
        {
            var j = new Json();
            j.BeginArray();
            foreach (var s in All<SkillData>())
            {
                j.BeginObject();
                j.Set("id", Id(s)).Set("displayName", s.displayName).Set("description", s.description).Set("type", s.type.ToString())
                 .Set("cooldown", s.cooldown).Set("radius", s.radius).Set("damage", s.damage).Set("power", s.power).Set("power2", s.power2)
                 .Set("duration", s.duration).Set("tickInterval", s.tickInterval);
                j.EndObject();
            }
            j.EndArray();
            return j.ToString();
        }

        static string Heroes()
        {
            var j = new Json();
            j.BeginArray();
            foreach (var h in All<HeroData>())
            {
                j.BeginObject();
                j.Set("id", Id(h)).Set("displayName", h.displayName).Set("title", h.title)
                 .Set("maxHpBonus", h.maxHpBonus).Set("damageMultiplier", h.damageMultiplier).Set("moveSpeedMultiplier", h.moveSpeedMultiplier)
                 .Set("expMultiplier", h.expMultiplier).Set("pickupRadiusMultiplier", h.pickupRadiusMultiplier)
                 .Set("startingWeapon", Id(h.startingWeapon)).Set("skill", Id(h.skill));
                j.Key("weapons").BeginArray();
                foreach (var w in h.weapons) if (w != null) j.Value(Id(w));
                j.EndArray();
                j.EndObject();
            }
            j.EndArray();
            return j.ToString();
        }

        static string Stages()
        {
            var j = new Json();
            j.BeginArray();
            foreach (var s in All<StageData>())
            {
                j.BeginObject();
                j.Set("id", Id(s)).Set("displayName", s.displayName).Set("duration", s.duration);
                j.Key("waves").BeginArray();
                foreach (var w in s.waves)
                {
                    j.BeginObject();
                    j.Set("name", w.name).Set("startTime", w.startTime).Set("spawnPerSecond", w.spawnPerSecond).Set("maxAlive", w.maxAlive);
                    j.Key("enemies").BeginArray();
                    foreach (var we in w.enemies)
                    {
                        if (we.data == null) continue;
                        j.BeginObject();
                        j.Set("enemy", Id(we.data)).Set("weight", we.weight);
                        j.EndObject();
                    }
                    j.EndArray();
                    j.EndObject();
                }
                j.EndArray();
                j.Key("events").BeginArray();
                foreach (var ev in s.events)
                {
                    j.BeginObject();
                    j.Set("time", ev.time).Set("enemy", Id(ev.enemy)).Set("count", ev.count).Set("isBoss", ev.isBoss).Set("message", ev.message);
                    j.EndObject();
                }
                j.EndArray();
                j.EndObject();
            }
            j.EndArray();
            return j.ToString();
        }

        static string UpgradeCatalogJson()
        {
            var catalogs = All<UpgradeCatalog>();
            var c = catalogs.Count > 0 ? catalogs[0] : null;
            var j = new Json();
            j.BeginObject();
            if (c != null)
            {
                j.Key("weapons").BeginArray();
                foreach (var w in c.weapons) if (w != null) j.Value(Id(w));
                j.EndArray();
                j.Key("passives").BeginArray();
                foreach (var p in c.passives) if (p != null) j.Value(Id(p));
                j.EndArray();
                j.Key("evolutions").BeginArray();
                foreach (var e in c.evolutions) if (e != null && e.IsValid) j.Value(Id(e));
                j.EndArray();
                j.Set("weaponLevelUpWeight", c.weaponLevelUpWeight).Set("passiveWeight", c.passiveWeight).Set("newWeaponWeight", c.newWeaponWeight);
            }
            j.EndObject();
            return j.ToString();
        }

        // ── 씬/프리팹에 직렬화된 밸런스 값 ──

        /// <summary>
        /// 전투 씬의 GameSystems(EnemyManager/EnemySpawner/ExpGemManager/LevelUpController)와 Player 의 직렬화 값을 읽는다.
        /// 씬이 열려 있지 않으면 **추가 모드로 잠깐 열었다가 저장 없이 닫는다**(작업 중인 씬은 건드리지 않음).
        /// </summary>
        static string Balance()
        {
            var scene = SceneManager.GetSceneByPath(BattleScenePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            try
            {
                var roots = scene.GetRootGameObjects();
                var manager = Find<EnemyManager>(roots);
                var spawner = Find<EnemySpawner>(roots);
                var gems = Find<ExpGemManager>(roots);
                var levelUp = Find<LevelUpController>(roots);
                var health = Find<PlayerHealth>(roots);
                var controller = Find<PlayerController>(roots);
                if (health == null || controller == null)
                {
                    // 씬에 플레이어가 없으면 프리팹 값
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Step2Setup.PlayerPrefabPath);
                    if (prefab != null)
                    {
                        if (health == null) health = prefab.GetComponentInChildren<PlayerHealth>(true);
                        if (controller == null) controller = prefab.GetComponentInChildren<PlayerController>(true);
                    }
                }

                // 스폰 링: 오프라인은 실제 창 비율을 쓰지만 서버는 고정값이 필요하다. 기본 설정(도트 화면 켬)의 반높이 5.625 와 16:9 로 계산.
                float halfH = (float)PixelArt.RefHeight / 2f / PixelArt.PPU;
                float aspect = 16f / 9f;
                float margin = Float(spawner, "spawnMargin", 1.5f);
                float halfW = halfH * aspect;
                float ring = Mathf.Sqrt(halfW * halfW + halfH * halfH) + margin;

                var j = new Json();
                j.BeginObject();
                j.Set("playerMaxHp", Float(health, "maxHp", 100f))
                 .Set("playerMoveSpeed", Float(controller, "moveSpeed", 4f))
                 .Set("playerBodyRadius", PlayerController.BodyRadius)
                 .Set("invulnerableTime", Float(health, "invulnerableTime", 0.5f))
                 .Set("regenTickSeconds", PlayerHealth.RegenTickSeconds)
                 .Set("separationStrength", Float(manager, "separationStrength", 3f))
                 .Set("maxSeparationSpeed", Float(manager, "maxSeparationSpeed", 3f))
                 .Set("tooFarDistance", Float(manager, "tooFarDistance", 25f))
                 .Set("playerRadius", Float(manager, "playerRadius", 0.4f))
                 .Set("contactPadding", Float(manager, "contactPadding", 0.15f))
                 .Set("cameraHalfHeight", halfH).Set("cameraAspect", aspect).Set("spawnMargin", margin).Set("spawnRingRadius", ring)
                 .Set("maxGems", Int(gems, "maxGems", 300))
                 .Set("magnetRadius", Float(gems, "magnetRadius", 2.2f))
                 .Set("collectRadius", Float(gems, "collectRadius", 0.35f))
                 .Set("gemStartSpeed", Float(gems, "startSpeed", 4f))
                 .Set("gemAcceleration", Float(gems, "acceleration", 30f))
                 .Set("gemMaxSpeed", Float(gems, "maxSpeed", 20f))
                 .Set("expBase", PlayerExperience.BaseRequired).Set("expStep", PlayerExperience.RequiredStep)
                 .Set("choiceCount", Int(levelUp, "choiceCount", 3)).Set("maxWeapons", Int(levelUp, "maxWeapons", 4))
                 .Set("healRatio", UpgradeGenerator.HealRatio)
                 .Set("knockbackDecay", Enemy.KnockbackDecay).Set("maxKnockbackSpeed", Enemy.MaxKnockbackSpeed)
                 .Set("shotWarningSeconds", Enemy.ShotWarningSeconds);
                j.EndObject();
                return j.ToString();
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static T Find<T>(GameObject[] roots) where T : Component
        {
            foreach (var r in roots)
            {
                var c = r.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        /// <summary>직렬화된(private [SerializeField]) 값을 읽는다. 대상이나 필드가 없으면 fallback (셋업 기본값) 을 쓰고 경고.</summary>
        static float Float(Component c, string field, float fallback)
        {
            var p = Prop(c, field);
            return p != null ? p.floatValue : fallback;
        }

        static int Int(Component c, string field, int fallback)
        {
            var p = Prop(c, field);
            return p != null ? p.intValue : fallback;
        }

        static SerializedProperty Prop(Component c, string field)
        {
            if (c == null) { Debug.LogWarning($"[ServerData] 컴포넌트가 없어 '{field}' 는 기본값으로 내보낸다 (전투 씬 셋업이 안 된 상태?)"); return null; }
            var p = new SerializedObject(c).FindProperty(field);
            if (p == null) Debug.LogWarning($"[ServerData] {c.GetType().Name}.{field} 필드를 찾지 못해 기본값으로 내보낸다");
            return p;
        }

        static string Info()
        {
            var j = new Json();
            j.BeginObject();
            j.Set("exportedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Set("unityVersion", Application.unityVersion)
             .Set("source", "unity_samkuk ScriptableObjects + BattleScene/Player prefab (Samkuk > Export Server Data)");
            j.EndObject();
            return j.ToString();
        }

        // ── 작은 JSON 쓰기 ──

        /// <summary>
        /// 들여쓰기 2칸의 JSON 작성기. JsonUtility 는 최상위 배열/열거형 문자열/들여쓰기 2칸을 못 하므로 직접 쓴다.
        /// 실수는 소수 5자리로 반올림해 float 의 0.30000001 같은 꼬리를 없애고 지수 표기를 쓰지 않는다.
        /// </summary>
        public sealed class Json
        {
            readonly StringBuilder sb = new StringBuilder();
            readonly Stack<bool> first = new Stack<bool>(); // 각 깊이에서 아직 첫 항목인지
            int depth;
            bool pendingKey;

            public Json BeginObject() { Open('{'); return this; }
            public Json EndObject() { Close('}'); return this; }
            public Json BeginArray() { Open('['); return this; }
            public Json EndArray() { Close(']'); return this; }

            public Json Key(string key)
            {
                Separate();
                sb.Append('"').Append(Escape(key)).Append("\": ");
                pendingKey = true;
                return this;
            }

            public Json Set(string key, string v) => Key(key).Value(v);
            public Json Set(string key, float v) => Key(key).Value(v);
            public Json Set(string key, int v) => Key(key).Value(v);
            public Json Set(string key, bool v) => Key(key).Value(v);

            public Json Value(string v)
            {
                Separate();
                if (v == null) sb.Append("null");
                else sb.Append('"').Append(Escape(v)).Append('"');
                return this;
            }

            public Json Value(float v)
            {
                Separate();
                double d = Math.Round((double)v, 5);
                sb.Append(d.ToString("0.#####", CultureInfo.InvariantCulture));
                return this;
            }

            public Json Value(int v) { Separate(); sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
            public Json Value(bool v) { Separate(); sb.Append(v ? "true" : "false"); return this; }

            void Open(char c)
            {
                Separate();
                sb.Append(c);
                first.Push(true);
                depth++;
            }

            void Close(char c)
            {
                bool empty = first.Pop();
                depth--;
                if (!empty) NewLine();
                sb.Append(c);
            }

            /// <summary>값 앞의 쉼표/줄바꿈. 키 바로 뒤의 값은 같은 줄에 붙는다.</summary>
            void Separate()
            {
                if (pendingKey) { pendingKey = false; return; }
                if (first.Count == 0) return;
                if (!first.Peek()) sb.Append(',');
                first.Pop();
                first.Push(false);
                NewLine();
            }

            void NewLine()
            {
                sb.Append('\n');
                for (int i = 0; i < depth; i++) sb.Append("  ");
            }

            static string Escape(string s)
            {
                var b = new StringBuilder(s.Length + 8);
                foreach (char ch in s)
                {
                    switch (ch)
                    {
                        case '"': b.Append("\\\""); break;
                        case '\\': b.Append("\\\\"); break;
                        case '\n': b.Append("\\n"); break;
                        case '\r': b.Append("\\r"); break;
                        case '\t': b.Append("\\t"); break;
                        default:
                            if (ch < 0x20) b.Append("\\u").Append(((int)ch).ToString("x4"));
                            else b.Append(ch);
                            break;
                    }
                }
                return b.ToString();
            }

            public override string ToString() => sb.ToString() + "\n";
        }
    }
}
