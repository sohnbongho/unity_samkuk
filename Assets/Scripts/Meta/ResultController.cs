using System;
using Samkuk.Core;
using Samkuk.Heroes;
using Samkuk.Strategy;
using Samkuk.UI;
using UnityEngine;

namespace Samkuk.Meta
{
    /// <summary>한 판의 결과.</summary>
    public struct RunResult
    {
        public bool cleared;
        public float seconds;
        public int kills;
        public int level;
        public int goldEarned;
        public int totalGold;
        public bool newBestTime;
        public bool newBestKills;
        public string heroName;
        /// <summary>내정에서 공격한 성의 이름 (타이틀의 [시작]으로 시작한 판은 비어 있음).</summary>
        public string castleName;
        /// <summary>이 판의 승리로 그 성을 새로 차지했는가.</summary>
        public bool conquered;
        /// <summary>정산 뒤 내가 차지한 성의 수 (내정에서 출진한 판에서만 의미가 있다).</summary>
        public int ownedCount;
    }

    /// <summary>
    /// 한 판이 끝나면 통계로 골드를 계산해 저장하고(기록 갱신 포함) 결과 화면을 띄운다.
    /// </summary>
    public class ResultController : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] RunStats run;
        [SerializeField] MetaApplier meta;
        [SerializeField] HeroSelectController hero;
        [SerializeField] ResultUI ui;

        IResultView view;

        public GameManager Game { get => game; set => SetGame(value); }
        public RunStats Run { get => run; set => run = value; }
        public MetaApplier Meta { get => meta; set => meta = value; }
        public HeroSelectController Hero { get => hero; set => hero = value; }
        public IResultView View { get => view; set => view = value; }

        /// <summary>가장 최근에 계산된 결과 (아직 없으면 null).</summary>
        public RunResult? LastResult { get; private set; }

        void Awake()
        {
            if (view == null) view = ui;
        }

        void OnEnable()
        {
            if (game != null) game.Finished += OnFinished;
        }

        void OnDisable()
        {
            if (game != null) game.Finished -= OnFinished;
        }

        void SetGame(GameManager value)
        {
            if (game != null) game.Finished -= OnFinished;
            game = value;
            if (game != null && isActiveAndEnabled) game.Finished += OnFinished;
        }

        void OnFinished(bool cleared)
        {
            var result = Settle(cleared);
            LastResult = result;
            // 내정에서 출진한 판이면 결과 화면에 [내정으로] 버튼을 켠다
            if (ui != null) ui.SetStrategyReturn(GameSession.SortieCastle != null ? (Action)OnStrategy : null);
            view?.Show(result, OnRetry, OnTitle);
        }

        /// <summary>보상을 계산해 저장 데이터에 반영하고 결과를 돌려준다.</summary>
        RunResult Settle(bool cleared)
        {
            var save = SaveSystem.Current;

            int kills = run != null ? run.Kills : 0;
            float seconds = run != null ? run.Seconds : 0f;
            int level = run != null ? run.Level : 1;
            float goldMultiplier = 1f + (meta != null ? meta.Bonuses.goldGain : 0f);
            int earned = RunRewards.Calculate(kills, seconds, cleared, goldMultiplier);

            var result = new RunResult
            {
                cleared = cleared,
                seconds = seconds,
                kills = kills,
                level = level,
                goldEarned = earned,
                newBestTime = seconds > save.bestSeconds,
                newBestKills = kills > save.bestKills,
                heroName = hero != null && hero.Current != null ? hero.Current.displayName : "",
                castleName = GameSession.SortieCastle != null ? GameSession.SortieCastle.displayName : ""
            };

            save.gold += earned;
            save.totalRuns++;
            if (cleared) save.clears++;
            if (result.newBestTime) save.bestSeconds = seconds;
            if (result.newBestKills) save.bestKills = kills;
            if (!string.IsNullOrEmpty(result.heroName)) save.lastHero = result.heroName;

            // 내정에서 공격한 성을 이겼으면 영토로 만든다 (이미 내 성이면 아무 일도 없다 = 다시 하기)
            var target = GameSession.SortieCastle;
            if (target != null)
            {
                if (cleared && Territory.Conquer(save, target.id))
                {
                    result.conquered = true;
                    StrategySession.LastCastleId = target.id; // 내정으로 돌아가면 차지한 성이 선택되어 있게
                }
                result.ownedCount = Territory.Count(save);
            }

            SaveSystem.Save(save);
            result.totalGold = save.gold;
            return result;
        }

        void OnRetry()
        {
            if (game != null) game.Restart();
        }

        void OnTitle()
        {
            if (game != null) game.GoToTitle();
        }

        void OnStrategy()
        {
            if (game != null) game.GoToStrategy();
        }
    }
}
