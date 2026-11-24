using Game.Core.Audio;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// The hunter's own sounds: footsteps that change with the ground and are as loud as the noise meter says
    /// (dry leaves crunch, mud is soft, crouching is quiet), and a grunt when an animal knocks the hunter down.
    /// </summary>
    public sealed class PlayerAudio : MonoBehaviour
    {
        [SerializeField] FirstPersonController controller;
        [Tooltip("Used when the ground has no footstep sound of its own.")]
        [SerializeField] SoundCue defaultFootsteps;
        [SerializeField] SoundCue knockedDown;
        [SerializeField, Range(0f, 1f)] float crouchedVolume = 0.45f;
        [Tooltip("Footstep volume at a slow creep (0) and at full walking speed (1): x = slowest, y = fullest.")]
        [SerializeField] Vector2 volumeBySpeed = new Vector2(0.35f, 1f);

        void Awake()
        {
            if (!controller) controller = GetComponent<FirstPersonController>();
            controller.Footstep += OnFootstep;
            controller.Attacked += OnAttacked;
        }

        void OnDestroy()
        {
            if (!controller) return;
            controller.Footstep -= OnFootstep;
            controller.Attacked -= OnAttacked;
        }

        void OnFootstep(FootstepInfo step)
        {
            var cue = step.surface && step.surface.footsteps ? step.surface.footsteps : defaultFootsteps;
            float loud = Mathf.Lerp(volumeBySpeed.x, volumeBySpeed.y, controller.SpeedFraction) * (step.crouched ? crouchedVolume : 1f);
            AudioService.Play(cue, step.position, 0f, loud);
        }

        void OnAttacked(Vector3 from) => AudioService.Play(knockedDown, transform.position + Vector3.up * 1.5f);
    }
}
