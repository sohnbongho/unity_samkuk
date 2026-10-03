using Samkuk.Heroes;
using Samkuk.UI;
using Samkuk.Upgrades;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Samkuk.Core
{
    /// <summary>
    /// ESC(또는 게임패드 Start)로 일시정지 메뉴를 열고 닫는다.
    /// 장수 선택/레벨업 선택/게임 종료 화면 중에는 일시정지할 수 없다.
    /// </summary>
    public class PauseController : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] HeroSelectController hero;
        [SerializeField] LevelUpController levelUp;
        [SerializeField] PauseUI ui;

        IPauseView view;

        public GameManager Game { get => game; set => game = value; }
        public HeroSelectController Hero { get => hero; set => hero = value; }
        public LevelUpController LevelUp { get => levelUp; set => levelUp = value; }
        public IPauseView View { get => view; set => view = value; }

        public bool IsPaused { get; private set; }

        /// <summary>지금 일시정지를 걸 수 있는 상태인가.</summary>
        public bool CanPause =>
            !IsPaused
            && (game == null || !game.IsFinished)
            && (hero == null || !hero.IsSelecting)
            && (levelUp == null || !levelUp.IsShowing);

        void Awake()
        {
            if (view == null) view = ui;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool pressed = (kb != null && kb.escapeKey.wasPressedThisFrame)
                        || (pad != null && pad.startButton.wasPressedThisFrame);
            if (pressed) Toggle();
        }

        public void Toggle()
        {
            if (IsPaused) Resume();
            else if (CanPause) Pause();
        }

        public void Pause()
        {
            if (!CanPause) return;
            IsPaused = true;
            Time.timeScale = 0f;
            view?.Show(Resume, Restart, Title);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            view?.Hide();
        }

        void Restart()
        {
            IsPaused = false;
            view?.Hide();
            if (game != null) game.Restart();
        }

        void Title()
        {
            IsPaused = false;
            view?.Hide();
            if (game != null) game.GoToTitle();
        }
    }
}
