using System;
using System.Collections.Generic;
using Messages;
using Samkuk.Allies;
using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Feedback;
using Samkuk.Heroes;
using Samkuk.Meta;
using Samkuk.Net;
using Samkuk.Pickups;
using Samkuk.Player;
using Samkuk.Skills;
using Samkuk.Stages;
using Samkuk.UI;
using Samkuk.Upgrades;
using Samkuk.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Samkuk.Coop
{
    /// <summary>
    /// 협동 전투의 "꼭두각시 모드" 드라이버(Step 15-4). 전투 씬이 협동 전투로 열리면(<see cref="CoopSession.InBattle"/>) 씬 로드 직후 스스로 붙어
    /// 오프라인 규칙 시스템(장수 선택, 이동, 무기, 적 이동/스폰, 젬, 레벨업, 스테이지, 일시정지, 아군)을 **끄고**, 서버 스냅샷/이벤트로
    /// 같은 프리팹·HUD·연출(걷기 시트, 무기 휘두르기 그림, 데미지 숫자, 입자, 효과음)을 움직인다. 입력은 20 Hz 로 서버에 보낸다.
    /// 규칙은 전부 서버(csharpServer/Docs/coop-design.md)에 있고 여기에는 그리는 코드만 있다. 순수 상태는 <see cref="CoopBattleModel"/>.
    /// 레벨업 선택은 선택하는 사람만 서버에서 멈추고(무적, 금빛 표시) 세계는 계속 돈다. 쓰러지면 10초 뒤 서버가 가장 가까운 생존 아군 옆에 부활시킨다(PlayerRevived).
    /// </summary>
    public class CoopBattleDriver : MonoBehaviour
    {
        public const string RootName = "CoopBattle";
        /// <summary>결과 화면에서 돌아갈 때 씬을 여는 방법 (테스트가 바꾼다).</summary>
        public static Action<string> SceneLoader = name => SceneManager.LoadScene(name);

        static readonly Color EnemyShotTint = new Color(1f, 0.45f, 0.25f);
        static readonly Color BloodColor = new Color(0.9f, 0.15f, 0.15f);
        static readonly Color GoldColor = new Color(1f, 0.85f, 0.3f);

        BattleStartNoti start;
        NetClient net;
        CoopBattleModel model;
        CoopCatalog catalog;
        readonly List<BattleEvent> events = new List<BattleEvent>(64);
        readonly List<int> expiredEnemies = new List<int>(), expiredGems = new List<int>();

        // 씬의 기존 시스템
        Transform localPlayer;
        Rigidbody2D localBody;
        HudController hud;
        StageHudView stageHud;
        FeedbackHooks feedback;
        WeaponFx fx;
        ILevelUpView levelUpView;
        Transform hudCanvas;

        // 꼭두각시
        readonly Transform[] playerRoots = new Transform[CoopSettings.MaxMembers];
        readonly PuppetWalker[] walkers = new PuppetWalker[CoopSettings.MaxMembers];
        readonly SwingPuppet[] swings = new SwingPuppet[CoopSettings.MaxMembers];
        readonly List<OrbitPuppet>[] orbits = new List<OrbitPuppet>[CoopSettings.MaxMembers];
        readonly string[] names = new string[CoopSettings.MaxMembers];
        EnemyPuppetPool enemies;
        GemPuppetPool gems;
        ProjectilePuppetPool projectiles;
        Sprite playerProjectileSprite, enemyProjectileSprite;
        Transform puppetRoot;

        // 입력
        InputAction moveAction;
        Vector2 localMove;
        bool skillLatched;
        float sendClock, sendInterval = 0.05f;
        int seq;

        // 진행
        float countdownLeft;
        bool countdownDone, ended;
        float choiceDeadline = -1f;
        LevelUpChoiceNoti choice;
        CoopResultPanel result;

        public CoopBattleModel Model => model;
        public bool Ended => ended;
        public int InputsSent => seq;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>전투 씬이 협동 전투로 열렸으면 드라이버를 붙인다 (Awake 뒤, Start 전).</summary>
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameManager.BattleSceneName) return;
            if (!CoopSession.InBattle || CoopSession.BattleStart == null) return;
            if (FindAnyObjectByType<CoopBattleDriver>() != null) return;
            new GameObject(RootName).AddComponent<CoopBattleDriver>();
        }

        void Awake()
        {
            start = CoopSession.BattleStart;
            net = NetClient.Instance;
            if (start == null || net == null) { Destroy(gameObject); return; }
            model = new CoopBattleModel(start.MySlot, start.SnapshotRate);
            sendInterval = 1f / Mathf.Max(1, start.TickRate);
            countdownLeft = NetUnits.Seconds(start.CountdownMs);
            Time.timeScale = 1f;
            // 전투 배경/소품은 오프라인과 같은 성 지형으로 (InfiniteBackground 가 Start 에서 읽는다). 서버 1차는 개활지라 소품은 장식일 뿐이다
            GameSession.SortieCastle = CastleCatalog.Load()?.FindAsset(start.CastleId);
            GameSession.SortieOrigin = null;

            FindSystems();
            DisableOfflineRules();
            BuildPuppets();
            BuildInput();

            net.MessageReceived += OnMessage;
            net.PhaseChanged += OnPhase;
            if (stageHud != null) stageHud.ShowBanner("곧 전투가 시작됩니다", 1f);
        }

        void OnDestroy()
        {
            if (net != null)
            {
                net.MessageReceived -= OnMessage;
                net.PhaseChanged -= OnPhase;
            }
            moveAction?.Dispose();
            Time.timeScale = 1f;
        }

        // ───────────────────────── 준비 ─────────────────────────

        void FindSystems()
        {
            var controller = FindAnyObjectByType<PlayerController>();
            localPlayer = controller != null ? controller.transform : null;
            localBody = localPlayer != null ? localPlayer.GetComponent<Rigidbody2D>() : null;
            hud = FindAnyObjectByType<HudController>();
            stageHud = FindAnyObjectByType<StageHudView>();
            feedback = FindAnyObjectByType<FeedbackHooks>();
            var weapons = localPlayer != null ? localPlayer.GetComponent<WeaponController>() : null;
            fx = weapons != null ? weapons.Fx : null;
            var levelUp = FindAnyObjectByType<LevelUpController>();
            levelUpView = levelUp != null ? levelUp.View : null;
            var hudGo = GameObject.Find("HUD");
            hudCanvas = hudGo != null ? hudGo.transform : (FindAnyObjectByType<Canvas>()?.transform);

            var stage = FindAnyObjectByType<StageController>();
            catalog = new CoopCatalog(stage != null ? stage.Stage : null, levelUp != null ? levelUp.Catalog : null, HeroCatalog.Load());

            var spawner = FindAnyObjectByType<EnemySpawner>();
            var gemManager = FindAnyObjectByType<ExpGemManager>();
            var enemyShots = FindAnyObjectByType<EnemyProjectileSystem>();
            puppetRoot = new GameObject("CoopPuppets").transform;
            enemies = new EnemyPuppetPool(spawner != null ? spawner.EnemyPrefab : null, puppetRoot);
            gems = new GemPuppetPool(gemManager != null ? gemManager.GemPrefab : null, puppetRoot);
            projectiles = new ProjectilePuppetPool(puppetRoot);
            playerProjectileSprite = weapons != null && weapons.ProjectilePrefab != null ? weapons.ProjectilePrefab.GetComponent<SpriteRenderer>()?.sprite : null;
            enemyProjectileSprite = enemyShots != null && enemyShots.Prefab != null ? enemyShots.Prefab.GetComponent<SpriteRenderer>()?.sprite : null;
        }

        /// <summary>오프라인 규칙 컴포넌트를 끈다. 켜 두면 서버와 따로 적을 만들고 움직여 두 세계가 겹친다.</summary>
        void DisableOfflineRules()
        {
            Disable<HeroSelectController>();
            Disable<PlayerController>();
            Disable<PlayerAnimator>();
            Disable<WeaponController>();
            Disable<SkillController>();
            Disable<EnemyManager>();
            Disable<EnemySpawner>();
            Disable<EnemyProjectileSystem>();
            Disable<ExpGemManager>();
            Disable<LevelUpController>();
            Disable<StageController>();
            Disable<GameManager>();
            Disable<PauseController>();
            Disable<AllyManager>();
            var spawner = FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            if (spawner != null) spawner.autoSpawn = false;
        }

        static void Disable<T>() where T : MonoBehaviour
        {
            foreach (var c in FindObjectsByType<T>(FindObjectsInactive.Include)) c.enabled = false;
        }

        void BuildPuppets()
        {
            for (int i = 0; i < orbits.Length; i++) orbits[i] = new List<OrbitPuppet>();
            foreach (var p in start.Players)
            {
                int slot = p.Slot;
                if (slot < 0 || slot >= playerRoots.Length) continue;
                names[slot] = string.IsNullOrEmpty(p.DisplayName) ? p.UserId : p.DisplayName;
                var hero = catalog.Hero(p.HeroId);
                Transform root;
                if (slot == start.MySlot && localPlayer != null) root = localPlayer;
                else root = MakeRemotePlayer(slot);
                playerRoots[slot] = root;
                var body = root != null ? root.GetComponentInChildren<SpriteRenderer>() : null;
                walkers[slot] = new PuppetWalker(body);
                walkers[slot].SetHero(hero);
                swings[slot] = new SwingPuppet(puppetRoot, $"P{slot}");
                if (slot == start.MySlot && hero != null) GameSession.SelectedHero = hero;
            }
        }

        /// <summary>다른 플레이어: 내 플레이어 오브젝트를 복제해 그림/그림자/정렬만 남기고 동작 컴포넌트는 끈다.</summary>
        Transform MakeRemotePlayer(int slot)
        {
            if (localPlayer == null) return null;
            var go = Instantiate(localPlayer.gameObject, localPlayer.position, Quaternion.identity);
            go.name = $"RemotePlayer{slot}";
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(mb is Samkuk.World.CastShadow) && !(mb is Samkuk.World.StandingSprite)) mb.enabled = false;
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
            return go.transform;
        }

        void BuildInput()
        {
            moveAction = new InputAction("CoopMove", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");
            moveAction.Enable();
        }

        // ───────────────────────── 매 프레임 ─────────────────────────

        void Update()
        {
            float dt = Time.deltaTime;
            float now = Time.time;

            if (!countdownDone)
            {
                countdownLeft -= dt;
                if (stageHud != null) stageHud.ShowBanner(countdownLeft > 0f ? Mathf.CeilToInt(countdownLeft).ToString() : "시작!", 0.3f);
                if (countdownLeft <= 0f && model.HasSnapshot) countdownDone = true;
            }

            if (!ended) SendInput(dt);

            model.Advance(dt);
            model.Expire(now, expiredEnemies, expiredGems);
            foreach (int id in expiredEnemies) enemies.Hide(id);
            foreach (int id in expiredGems) gems.Hide(id);

            RenderPlayers(dt, now);
            RenderEnemies(dt);
            foreach (var g in model.Gems.Values) if (g.Attracted) gems.Move(g.Id, g.Motion.Current);
            projectiles.Tick(dt);

            if (choiceDeadline > 0f && now > choiceDeadline) HideChoice(); // 서버가 자동 선택한 뒤 (timeout)
            if (stageHud != null && model.HasSnapshot && model.Stage != null)
            {
                float remaining = NetUnits.Seconds(model.Stage.RemainingMs) - (model.Stage.Paused ? 0f : now - model.LastSnapshotAt);
                stageHud.SetTimerSeconds(Mathf.CeilToInt(Mathf.Max(0f, remaining)));
            }
        }

        void SendInput(float dt)
        {
            localMove = moveAction != null ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) skillLatched = true;

            sendClock += dt;
            if (sendClock < sendInterval) return;
            sendClock = 0f;
            seq++;
            net.Send(new MessageWrapper { BattleInputRequest = new BattleInputRequest { Seq = seq, MoveX = localMove.x, MoveY = localMove.y, Skill = skillLatched } });
            skillLatched = false;
        }

        void RenderPlayers(float dt, float now)
        {
            float elapsed = model.ElapsedSeconds(now);
            for (int slot = 0; slot < playerRoots.Length; slot++)
            {
                var root = playerRoots[slot];
                var track = model.Players[slot];
                if (root == null || !track.Seen) continue;

                Vector2 pos = track.Motion.Current;
                root.position = pos;
                if (slot == start.MySlot && localBody != null) localBody.position = pos;

                bool me = slot == start.MySlot;
                bool localMoving = me && localMove.sqrMagnitude > 1e-4f;
                var facing = localMoving ? PlayerAnimator.PickDirection(localMove, walkers[slot].Direction) : PuppetWalker.FacingOf(track.State.Facing);
                walkers[slot]?.Tick(facing, (track.Moving || localMoving) && !track.Dead && !track.Choosing, dt);
                walkers[slot]?.SetLook(track.Dead, track.Choosing, track.Invulnerable);
                swings[slot]?.Tick(dt, pos);
                foreach (var o in orbits[slot]) o.Tick(pos, elapsed);
            }
        }

        void RenderEnemies(float dt)
        {
            foreach (var track in model.Enemies.Values)
            {
                if (!enemies.TryGet(track.Id, out var e))
                {
                    e = enemies.Show(track.Id, catalog.Enemy(track.DataId) ?? catalog.FallbackEnemy, track.Motion.Current);
                    if (e == null) continue;
                }
                Vector2 pos = track.Motion.Current;
                e.Teleport(pos);
                e.TickAnimation(model.FaceDirectionFrom(pos), track.Moving || track.Charging, dt);
                e.ShowPuppetFlags(track.Stunned, track.WindingUp, track.ShotWarning);
            }
        }

        // ───────────────────────── 서버 메시지 ─────────────────────────

        void OnMessage(MessageWrapper message)
        {
            switch (message.PayloadCase)
            {
                case MessageWrapper.PayloadOneofCase.BattleSnapshotNoti: OnSnapshot(message.BattleSnapshotNoti); break;
                case MessageWrapper.PayloadOneofCase.LevelUpChoiceNoti: ShowChoice(message.LevelUpChoiceNoti); break;
                case MessageWrapper.PayloadOneofCase.BattleEndNoti: OnEnd(message.BattleEndNoti); break;
            }
        }

        void OnPhase(NetPhase phase)
        {
            if (phase == NetPhase.Online || ended) return;
            ended = true;
            HideChoice();
            ShowResult("서버와의 연결이 끊겼습니다", null);
        }

        public void OnSnapshot(BattleSnapshotNoti noti)
        {
            if (ended) return;
            bool hadState = noti.Players.Count > 0;
            events.Clear();
            model.Apply(noti, Time.time, events);
            foreach (var ev in events) HandleEvent(ev);
            if (hadState) RefreshHud();
        }

        void RefreshHud()
        {
            var me = model.Me;
            if (hud != null && me != null && me.Seen)
            {
                hud.SetHealth(me.State.Hp, me.State.MaxHp);
                hud.SetExp(me.State.Level, me.State.Exp, me.State.ExpToNext);
            }
            var stage = model.Stage;
            if (stageHud == null || stage == null) return;
            // 웨이브 줄 끝에 팀 공용 부활 횟수 (서버 상수 3, 부활마다 1 소모; 0 이면 죽는 즉시 관전)
            stageHud.SetWave(stage.WaveNumber, $"{stage.WaveName}     부활 남음 {stage.RevivesLeft}");
            if (stage.BossId != 0 && model.Enemies.TryGetValue(stage.BossId, out var boss))
                stageHud.ShowBoss(catalog.Enemy(boss.DataId)?.displayName ?? "보스", stage.BossHpPct / 100f);
            else stageHud.HideBoss();

            // 쓰러져 있으면 부활 카운트다운 (서버가 10초 뒤 자동 부활). 레벨업 선택 중인 사람은 금빛으로만 표시하고 세계는 멈추지 않는다
            if (me != null && me.Seen && me.Dead)
                stageHud.ShowBanner(me.ReviveSeconds > 0f ? $"쓰러짐 — {Mathf.CeilToInt(me.ReviveSeconds)}초 뒤 부활" : "쓰러짐 — 부활 불가, 관전 중", 0.5f);
        }

        void HandleEvent(BattleEvent ev)
        {
            float now = Time.time;
            switch (ev.BodyCase)
            {
                case BattleEvent.BodyOneofCase.EnemySpawn:
                {
                    var e = ev.EnemySpawn;
                    var pos = NetUnits.Pos(e.X, e.Y);
                    model.SpawnEnemy(e.Id, e.EnemyId, pos, now);
                    enemies.Show(e.Id, catalog.Enemy(e.EnemyId) ?? catalog.FallbackEnemy, pos);
                    break;
                }
                case BattleEvent.BodyOneofCase.EnemyDespawn:
                {
                    var e = ev.EnemyDespawn;
                    if (enemies.TryGet(e.Id, out var puppet))
                    {
                        if (e.Reason == 0) { puppet.Teleport(NetUnits.Pos(e.X, e.Y)); puppet.ShowDeath(); }
                        enemies.Hide(e.Id);
                    }
                    model.RemoveEnemy(e.Id);
                    break;
                }
                case BattleEvent.BodyOneofCase.Damage:
                {
                    var d = ev.Damage;
                    if (d.TargetKind == 0)
                    {
                        if (enemies.TryGet(d.Id, out var puppet)) puppet.ShowHit(d.Amount, d.HpPct / 100f);
                    }
                    else if (d.Id == start.MySlot)
                    {
                        feedback?.Flash?.Flash(Mathf.Clamp01(0.5f + d.Amount / 100f * 3f));
                        feedback?.Shake?.AddTrauma(0.5f);
                        feedback?.Burst?.Burst(NetUnits.Pos(d.X, d.Y), 8, BloodColor, 4f, 0.5f, 0.9f);
                        if (d.HpPct > 0) AudioManager.Play(SfxId.PlayerHurt);
                    }
                    else feedback?.Burst?.Burst(NetUnits.Pos(d.X, d.Y), 4, BloodColor, 3f, 0.4f, 0.7f);
                    break;
                }
                case BattleEvent.BodyOneofCase.WeaponFire:
                {
                    var w = ev.WeaponFire;
                    if (w.Slot < 0 || w.Slot >= swings.Length) break;
                    var data = catalog.Weapon(w.WeaponId);
                    var dir = NetUnits.Dir(w.DirX, w.DirY);
                    var owner = playerRoots[w.Slot] != null ? (Vector2)playerRoots[w.Slot].position : NetUnits.Pos(w.X, w.Y);
                    float seconds = swings[w.Slot]?.Play(data, dir, w.Motion, owner) ?? 0f;
                    if (seconds > 0f) walkers[w.Slot]?.Look(dir, seconds + SlashWeapon.LookHoldSeconds);
                    AudioManager.Play(w.Motion == SwingPuppet.MotionThrust ? SfxId.Thrust : w.Motion == SwingPuppet.MotionNone ? SfxId.Arrow : SfxId.Slash);
                    break;
                }
                case BattleEvent.BodyOneofCase.ProjectileSpawn:
                {
                    var p = ev.ProjectileSpawn;
                    bool enemyShot = p.OwnerSlot < 0;
                    var weapon = enemyShot ? null : catalog.Weapon(p.WeaponId);
                    var enemyData = enemyShot ? catalog.Enemy(p.EnemyId) : null;
                    Sprite sprite = enemyShot ? enemyProjectileSprite : (weapon != null && weapon.sprite != null ? weapon.sprite : playerProjectileSprite);
                    Color tint = enemyShot ? (enemyData != null ? enemyData.projectileTint : EnemyShotTint) : (weapon != null ? weapon.tint : Color.white);
                    projectiles.Spawn(p.Id, NetUnits.Pos(p.X, p.Y), NetUnits.Dir(p.DirX, p.DirY), NetUnits.Speed(p.Speed), NetUnits.Seconds(p.LifetimeMs), sprite, tint, 1f);
                    break;
                }
                case BattleEvent.BodyOneofCase.ProjectileDespawn:
                    projectiles.Despawn(ev.ProjectileDespawn.Id);
                    break;
                case BattleEvent.BodyOneofCase.GemSpawn:
                {
                    var g = ev.GemSpawn;
                    var pos = NetUnits.Pos(g.X, g.Y);
                    model.SpawnGem(g.Id, pos, g.Value, now);
                    gems.Show(g.Id, pos, g.Value);
                    break;
                }
                case BattleEvent.BodyOneofCase.GemCollect:
                    gems.Hide(ev.GemCollect.Id);
                    model.RemoveGem(ev.GemCollect.Id);
                    if (ev.GemCollect.Slot == start.MySlot) AudioManager.Play(SfxId.Gem);
                    break;
                case BattleEvent.BodyOneofCase.PlayerLevelUp:
                {
                    int slot = ev.PlayerLevelUp.Slot;
                    if (slot >= 0 && slot < playerRoots.Length && playerRoots[slot] != null)
                        feedback?.Burst?.Ring(playerRoots[slot].position, 16, GoldColor, 5f, 0.6f, 1f);
                    if (slot == start.MySlot) AudioManager.Play(SfxId.LevelUp);
                    break;
                }
                case BattleEvent.BodyOneofCase.PlayerDied:
                    if (ev.PlayerDied.Slot == start.MySlot) { stageHud?.ShowBanner("쓰러졌습니다 — 잠시 뒤 부활", 1f); AudioManager.Play(SfxId.Defeat); }
                    else if (ev.PlayerDied.Slot >= 0 && ev.PlayerDied.Slot < names.Length) stageHud?.ShowBanner($"{names[ev.PlayerDied.Slot]} 쓰러짐", 2f);
                    break;
                case BattleEvent.BodyOneofCase.PlayerRevived:
                {
                    var r = ev.PlayerRevived;
                    var pos = NetUnits.Pos(r.X, r.Y);
                    model.RevivePlayer(r.Slot, pos);
                    if (r.Slot >= 0 && r.Slot < playerRoots.Length && playerRoots[r.Slot] != null)
                    {
                        playerRoots[r.Slot].position = pos;
                        if (r.Slot == start.MySlot && localBody != null) localBody.position = pos;
                        feedback?.Burst?.Ring(pos, 20, GoldColor, 5f, 0.6f, 1f);
                    }
                    if (r.Slot == start.MySlot) { stageHud?.ShowBanner("부활!  2초 무적", 2f); AudioManager.Play(SfxId.Skill); }
                    else if (r.Slot >= 0 && r.Slot < names.Length) stageHud?.ShowBanner($"{names[r.Slot]} 부활", 1.5f);
                    break;
                }
                case BattleEvent.BodyOneofCase.WaveChanged:
                    stageHud?.SetWave(ev.WaveChanged.Number, ev.WaveChanged.Name);
                    break;
                case BattleEvent.BodyOneofCase.StageEvent:
                    if (!string.IsNullOrEmpty(ev.StageEvent.Message)) stageHud?.ShowBanner(ev.StageEvent.Message);
                    if (ev.StageEvent.IsBoss) { AudioManager.Play(SfxId.Boss); feedback?.Shake?.AddTrauma(0.7f); }
                    break;
                case BattleEvent.BodyOneofCase.LoadoutChanged:
                    RebuildOrbits(ev.LoadoutChanged);
                    break;
                case BattleEvent.BodyOneofCase.AreaFx:
                {
                    var a = ev.AreaFx;
                    if (fx == null) break;
                    float radius = NetUnits.Units(a.Radius);
                    float seconds = Mathf.Max(0.15f, NetUnits.Seconds(a.DurationMs));
                    Color color = AreaColor(a.Kind);
                    fx.PlayDisc(CoopSprites.Disc(), NetUnits.Pos(a.X, a.Y), radius * 2f, color, seconds, a.Kind == 4 ? 0.2f : 1f, a.Kind == 4 ? 1.2f : 1f);
                    AudioManager.Play(a.Kind == 3 ? SfxId.Thunder : a.Kind == 4 ? SfxId.Explosion : a.Kind == 0 ? SfxId.Fire : SfxId.Arrow);
                    break;
                }
            }
        }

        /// <summary>범위 연출 종류별 색: 0 불길 1 화살비 예고 2 화살비 폭격 3 벼락 4 충격파.</summary>
        static Color AreaColor(int kind)
        {
            switch (kind)
            {
                case 0: return new Color(1f, 0.45f, 0.1f, 0.6f);
                case 1: return new Color(1f, 1f, 1f, 0.25f);
                case 2: return new Color(0.9f, 0.8f, 0.4f, 0.7f);
                case 3: return new Color(0.7f, 0.85f, 1f, 0.9f);
                default: return new Color(1f, 0.9f, 0.6f, 0.5f);
            }
        }

        void RebuildOrbits(LoadoutChangedEvent loadout)
        {
            int slot = loadout.Slot;
            if (slot < 0 || slot >= orbits.Length) return;
            foreach (var o in orbits[slot]) o.Destroy();
            orbits[slot].Clear();
            foreach (var entry in loadout.Weapons)
            {
                var data = catalog.Weapon(entry.Id);
                if (data == null || data.type != WeaponType.Orbit) continue;
                int count = data.count + (data.levelsPerExtraCount > 0 ? (Mathf.Max(1, entry.Level) - 1) / data.levelsPerExtraCount : 0);
                orbits[slot].Add(new OrbitPuppet(puppetRoot, data, count));
            }
        }

        // ───────────────────────── 레벨업 선택 ─────────────────────────

        /// <summary>서버 선택지를 기존 레벨업 카드(UpgradeOption)로. 종류 번호는 유니티 UpgradeKind 순서와 같다.</summary>
        public static List<UpgradeOption> BuildOptions(LevelUpChoiceNoti noti)
        {
            var list = new List<UpgradeOption>();
            if (noti == null) return list;
            foreach (var c in noti.Options)
            {
                var kind = c.Kind >= 0 && c.Kind <= (int)UpgradeKind.Evolve ? (UpgradeKind)c.Kind : UpgradeKind.Passive;
                list.Add(new UpgradeOption
                {
                    Kind = kind,
                    Title = string.IsNullOrEmpty(c.Title) ? c.Id : c.Title,
                    Description = c.Description ?? "",
                });
            }
            return list;
        }

        void ShowChoice(LevelUpChoiceNoti noti)
        {
            if (ended || levelUpView == null) return;
            choice = noti;
            choiceDeadline = Time.time + Mathf.Max(1f, NetUnits.Seconds(noti.TimeoutMs));
            var options = BuildOptions(noti);
            levelUpView.Show(options, index =>
            {
                if (choice == null || index < 0 || index >= choice.Options.Count) return;
                net.Send(new MessageWrapper { UpgradeSelectRequest = new UpgradeSelectRequest { ChoiceId = choice.ChoiceId, Index = choice.Options[index].Index } });
                AudioManager.Play(SfxId.Click);
                HideChoice();
            });
        }

        void HideChoice()
        {
            choice = null;
            choiceDeadline = -1f;
            levelUpView?.Hide();
        }

        // ───────────────────────── 끝 ─────────────────────────

        void OnEnd(BattleEndNoti noti)
        {
            if (ended) return;
            ended = true;
            HideChoice();
            AudioManager.Play(noti.Cleared ? SfxId.Victory : SfxId.Defeat);
            foreach (var r in noti.Results)
            {
                if (r.Slot != start.MySlot || r.Gold <= 0) continue;
                SaveSystem.Current.gold += r.Gold;      // 보상은 서버 계산값 그대로 (영구 강화 배율은 서버 저장으로 옮긴 뒤)
                SaveSystem.SaveCurrent();
            }
            ShowResult(noti.Cleared ? "승리!  천하가 그대를 기억하리라" : $"패배  ({noti.SurviveSeconds}초 버팀)", noti);
        }

        void ShowResult(string title, BattleEndNoti noti)
        {
            if (result != null || hudCanvas == null) return;
            var rows = new List<CoopResultPanel.Row>();
            if (noti != null)
                foreach (var r in noti.Results)
                {
                    string name = r.Slot >= 0 && r.Slot < names.Length ? names[r.Slot] : $"{r.Slot + 1}번";
                    if (r.Slot == start.MySlot) name += " (나)";
                    rows.Add(new CoopResultPanel.Row { Name = name, Hero = catalog.Hero(r.HeroId)?.displayName ?? r.HeroId, Kills = r.Kills, Level = r.Level, Gold = r.Gold, Alive = r.Alive });
                }
            result = CoopResultPanel.Create(hudCanvas, title, rows, () => SceneLoader?.Invoke(GameManager.TitleSceneName));
        }
    }
}
