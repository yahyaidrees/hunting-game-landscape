using Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Small developer readout (frame rate, surface, speed, noise). Hidden outside the editor and dev builds.</summary>
    public sealed class DevOverlay : MonoBehaviour
    {
        [SerializeField] Text text;
        [SerializeField] float refreshSeconds = 0.25f;

        FirstPersonController controller;
        PlayerNoise noise;
        float timer, frames;

        void Awake()
        {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            gameObject.SetActive(false);
#endif
        }

        void Update()
        {
            frames++;
            timer += Time.unscaledDeltaTime;
            if (timer < refreshSeconds) return;
            if (!controller) controller = FindFirstObjectByType<FirstPersonController>();
            if (!noise) noise = FindFirstObjectByType<PlayerNoise>();

            float fps = frames / timer;
            frames = 0;
            timer = 0;
            string surface = controller && controller.CurrentSurface ? controller.CurrentSurface.displayName : "-";
            string speed = controller ? (controller.SpeedFraction * 100f).ToString("0") + "%" : "-";
            string posture = controller && controller.IsCrouched ? "crouched" : "standing";
            string level = noise ? noise.Level.ToString("0.00") + " (" + noise.HearingRadius.ToString("0") + " m)" : "-";
            text.text = $"{fps:0} fps\nground: {surface}\nspeed: {speed} {posture}\nnoise: {level}";
        }
    }
}
