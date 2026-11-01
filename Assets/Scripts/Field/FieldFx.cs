using System.Collections.Generic;
using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Feedback;
using UnityEngine;

namespace Samkuk.Field
{
    /// <summary>
    /// 야전 연출(15-4): 모델 이벤트 → 입자·화살·번쩍임·소리. 규칙은 없고 보여 주기만 한다.
    /// 피해(<see cref="FieldBattleModel.Hit"/>)는 매 틱 일어나므로 맞는 부대마다/쏘는 부대마다 간격을 두고 한 번씩만 표현한다.
    /// 입자는 전투의 <see cref="BurstFx"/> 를 그대로 쓰고(코드 스프라이트, 풀), 화살은 여기서 작은 풀로 날린다. 소리는 기존 효과음 재사용.
    /// 장수 개성(15-7): 내 장수 부대의 백병전 타격은 불꽃 대신 장수의 시작 무기 연출(<see cref="FieldWeaponShow"/>)로 보여 준다.
    /// </summary>
    public class FieldFx : MonoBehaviour
    {
        /// <summary>같은 부대가 맞는 불꽃/소리 간격, 같은 궁병이 쏘는 화살 간격 (초).</summary>
        public const float MeleeInterval = 0.2f, ArrowInterval = 0.45f;
        public const float ArrowFlight = 0.3f;
        const int ArrowPoolMax = 32;
        static readonly Color Spark = new Color(1f, 0.95f, 0.7f);
        static readonly Color PlayerColor = new Color(0.45f, 0.6f, 1f);
        static readonly Color EnemyColor = new Color(1f, 0.4f, 0.35f);

        class Arrow
        {
            public SpriteRenderer sr;
            public Vector3 from, to;
            public float t;
        }

        static Sprite arrowSprite;

        FieldBattleModel model;
        BurstFx burst;
        FieldWeaponShow generalShow;
        readonly Dictionary<int, float> lastMelee = new Dictionary<int, float>();
        readonly Dictionary<int, float> lastArrow = new Dictionary<int, float>();
        readonly List<Arrow> arrows = new List<Arrow>(ArrowPoolMax);
        readonly Stack<Arrow> idle = new Stack<Arrow>();
        float clock;
        int created;

        public BurstFx Burst => burst;
        public int ActiveArrows => arrows.Count;
        public int HitCount { get; private set; }
        /// <summary>장수 부대 무기 연출 (장수 무기가 없으면 null).</summary>
        public FieldWeaponShow GeneralShow => generalShow;
        /// <summary>장수 무기 연출이 시작한 타격 수 (테스트용).</summary>
        public int GeneralStrikeCount { get; private set; }

        /// <summary>부대 그림에 번쩍임을 주려고 뷰를 찾는 함수 (컨트롤러가 넣는다).</summary>
        public System.Func<FieldUnit, FieldUnitView> ViewOf { get; set; }

        public void Bind(FieldBattleModel m, WeaponData generalWeapon = null)
        {
            Unbind();
            model = m;
            generalShow?.Destroy();
            generalShow = generalWeapon != null ? new FieldWeaponShow(transform, generalWeapon) : null;
            GeneralStrikeCount = 0;
            if (burst == null)
            {
                var go = new GameObject("Burst");
                go.transform.SetParent(transform, false);
                burst = go.AddComponent<BurstFx>();
            }
            lastMelee.Clear();
            lastArrow.Clear();
            clock = 0f;
            HitCount = 0;
            foreach (var a in arrows) Recycle(a);
            arrows.Clear();
            if (model == null) return;
            model.Hit += OnHit;
            model.UnitRouted += OnRouted;
            model.AutoPaused += OnAutoPaused;
            model.FinishedEvent += OnFinished;
        }

        public void Unbind()
        {
            generalShow?.Hide();
            if (model == null) return;
            model.Hit -= OnHit;
            model.UnitRouted -= OnRouted;
            model.AutoPaused -= OnAutoPaused;
            model.FinishedEvent -= OnFinished;
            model = null;
        }

        void OnDestroy() => Unbind();

        /// <summary>모델이 진행한 만큼 화살을 날린다 (정지 중엔 모델 틱이 없으니 여기도 멈춘다).</summary>
        public void Tick(float dt)
        {
            clock += dt;
            var general = model?.PlayerGeneral;
            if (generalShow != null && general != null)
            {
                if (general.Alive) generalShow.Tick(dt, PositionOf(general));
                else generalShow.Hide();
            }
            for (int i = arrows.Count - 1; i >= 0; i--)
            {
                var a = arrows[i];
                a.t += dt / ArrowFlight;
                if (a.t >= 1f)
                {
                    burst.Burst(a.to, 2, Spark, 2.5f, 0.2f, 0.45f);
                    Recycle(a);
                    arrows.RemoveAt(i);
                    continue;
                }
                // 살짝 포물선
                var p = Vector3.Lerp(a.from, a.to, a.t);
                p.y += Mathf.Sin(a.t * Mathf.PI) * 0.6f;
                a.sr.transform.position = p;
            }
        }

        static Vector3 PositionOf(FieldUnit u)
        {
            IsoGrid.CellToWorld(u.VisualCol, u.VisualRow, out float x, out float y);
            return new Vector3(x, y + 0.45f, 0f);   // 몸통 높이
        }

        void OnHit(FieldUnit attacker, FieldUnit victim, float amount, bool ranged, bool charge)
        {
            HitCount++;
            if (ranged)
            {
                if (lastArrow.TryGetValue(attacker.Id, out float la) && clock - la < ArrowInterval) return;
                lastArrow[attacker.Id] = clock;
                Shoot(PositionOf(attacker), PositionOf(victim));
                AudioManager.Play(SfxId.Arrow, 0.6f);
                return;
            }

            var view = ViewOf?.Invoke(victim);
            if (charge)
            {
                burst.Ring(PositionOf(victim), 10, Color.white, 4f, 0.35f, 0.8f);
                AudioManager.Play(SfxId.Explosion, 0.8f);
                view?.Flash(1f);
                lastMelee[victim.Id] = clock;
                return;
            }
            if (generalShow != null && attacker == model.PlayerGeneral)
            {
                // 장수 부대: 불꽃·타격음 대신 무기를 휘두른다 (동작 중이면 거절 = 간격). 맞는 쪽 번쩍임은 그대로
                if (!generalShow.Strike(PositionOf(attacker), PositionOf(victim))) return;
                GeneralStrikeCount++;
                lastMelee[victim.Id] = clock;
                view?.Flash(0.6f);
                return;
            }
            if (lastMelee.TryGetValue(victim.Id, out float lm) && clock - lm < MeleeInterval) return;
            lastMelee[victim.Id] = clock;
            burst.Burst(PositionOf(victim), 2, Spark, 3.5f, 0.25f, 0.5f);
            AudioManager.Play(SfxId.Hit, 0.7f);
            view?.Flash(0.6f);
        }

        void OnRouted(FieldUnit u)
        {
            var color = u.Side == FieldSide.Player ? PlayerColor : EnemyColor;
            burst.Burst(PositionOf(u), u.IsGeneral ? 36 : 20, color, 5f, 0.7f, 1f);
            AudioManager.Play(SfxId.Kill);
        }

        void OnAutoPaused(FieldUnit u) => AudioManager.Play(SfxId.PlayerHurt);

        void OnFinished(BattleOutcome outcome) => AudioManager.Play(outcome == BattleOutcome.Victory ? SfxId.Victory : SfxId.Defeat);

        // ───────────────────────── 화살 풀 ─────────────────────────

        void Shoot(Vector3 from, Vector3 to)
        {
            Arrow a;
            if (idle.Count > 0) a = idle.Pop();
            else if (created < ArrowPoolMax)
            {
                var go = new GameObject($"Arrow{created++}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ArrowSprite();
                sr.sortingLayerName = GameLayers.Sorting.Effect;
                sr.sortingOrder = 3;
                a = new Arrow { sr = sr };
            }
            else return;

            a.from = from;
            a.to = to;
            a.t = 0f;
            a.sr.transform.position = from;
            var d = to - from;
            a.sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            a.sr.gameObject.SetActive(true);
            arrows.Add(a);
        }

        void Recycle(Arrow a)
        {
            a.sr.gameObject.SetActive(false);
            idle.Push(a);
        }

        /// <summary>12x2 픽셀 화살(나무 대 + 흰 촉), 코드로 만든다.</summary>
        static Sprite ArrowSprite()
        {
            if (arrowSprite != null) return arrowSprite;
            int w = 12, h = 2;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[w * h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    px[y * w + x] = x >= w - 3 ? new Color32(240, 240, 250, 255) : new Color32(150, 100, 50, 255);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            arrowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelArt.PPU, 0, SpriteMeshType.FullRect);
            arrowSprite.hideFlags = HideFlags.HideAndDontSave;
            return arrowSprite;
        }
    }
}
