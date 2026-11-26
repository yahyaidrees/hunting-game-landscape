using Game.Core.Settings;
using Game.Hunt;
using Game.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The pause button (top right of the hunt HUD) and the settings panel it opens. The world and its sounds stop
    /// while the panel is open. It also opens from the briefing card, by the phone's Back button (Escape in the
    /// editor), and by itself when the phone sends the game to the background in the middle of a hunt.
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Button pauseButton;
        [Tooltip("Other buttons that open the panel (the briefing card's Settings).")]
        [SerializeField] Button[] extraOpeners;
        [SerializeField] Button doneButton;
        [SerializeField] Button restartButton;
        [SerializeField] Button resetControlsButton;
        [SerializeField] Text title;

        bool open;
        float savedTimeScale = 1f;
        KillCamera killCamera;

        public bool IsOpen => open;

        void Awake()
        {
            pauseButton.onClick.AddListener(Open);
            foreach (var b in extraOpeners) if (b) b.onClick.AddListener(Open);
            doneButton.onClick.AddListener(Close);
            restartButton.onClick.AddListener(RestartHunt);
            if (resetControlsButton) resetControlsButton.onClick.AddListener(GameSettings.ResetControls);
            panel.SetActive(false);
            killCamera = FindFirstObjectByType<KillCamera>();
        }

        void OnDestroy()
        {
            if (open) { AudioListener.pause = false; GameSettings.Flush(); }
        }

        void Update()
        {
            var keyboard = Keyboard.current;   // the Android Back button arrives as Escape
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (open) Close(); else if (InHunt) Open();
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && InHunt) Open();
            if (paused) GameSettings.Flush();
        }

        static bool InHunt
        {
            get
            {
                var s = HuntSession.Active;
                return !s || (s.Stage != HuntStage.Briefing && s.Stage != HuntStage.Result);
            }
        }

        public void Open()
        {
            if (open) return;
            if (killCamera && killCamera.IsPlaying) return;   // the shot's slow motion owns the clock; it lasts seconds
            open = true;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            bool inHunt = InHunt;
            restartButton.gameObject.SetActive(inHunt && HuntSession.Active);
            if (title) title.text = inHunt ? "Paused" : "Settings";
            panel.SetActive(true);
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            panel.SetActive(false);
            Time.timeScale = savedTimeScale;
            AudioListener.pause = false;
            GameSettings.Flush();
        }

        void RestartHunt()
        {
            var session = HuntSession.Active;
            if (!session) return;
            open = false;
            AudioListener.pause = false;
            GameSettings.Flush();
            session.Restart();
        }
    }
}
