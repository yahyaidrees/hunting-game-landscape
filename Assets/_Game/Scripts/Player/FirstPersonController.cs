using System;
using Game.Core.Settings;
using Game.Core.Surfaces;
using UnityEngine;

namespace Game.Player
{
    /// <summary>A footstep: where, on what, and whether the hunter was crouched. Footprints and footstep audio use it.</summary>
    public readonly struct FootstepInfo
    {
        public readonly Vector3 position;
        public readonly Vector3 forward;
        public readonly SurfaceDefinition surface;
        public readonly bool crouched;
        public readonly bool leftFoot;

        public FootstepInfo(Vector3 position, Vector3 forward, SurfaceDefinition surface, bool crouched, bool leftFoot)
        {
            this.position = position;
            this.forward = forward;
            this.surface = surface;
            this.crouched = crouched;
            this.leftFoot = leftFoot;
        }
    }

    /// <summary>
    /// First-person hunter on a CharacterController: stick-proportional walking (a small push sneaks slowly),
    /// drag-to-look, crouch toggle with a headroom check, gravity, the ground surface underfoot, footstep events
    /// and a subtle head bob. All numbers come from PlayerMotorSettings.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] PlayerMotorSettings settings;
        [SerializeField] PlayerInputReader input;
        [Tooltip("Child at eye height that holds the camera; it pitches, the body yaws.")]
        [SerializeField] Transform cameraPivot;

        CharacterController body;
        float yaw, pitch, verticalVelocity, height, stepDistance, bobPhase;
        Vector3 planarVelocity;
        bool crouched, leftFoot;

        public PlayerMotorSettings Settings => settings;
        public bool IsCrouched => crouched;
        public bool IsGrounded { get; private set; }
        /// <summary>Current speed as a fraction of standing walk speed (0..1).</summary>
        public float SpeedFraction { get; private set; }
        public SurfaceDefinition CurrentSurface { get; private set; }
        public event Action<FootstepInfo> Footstep;

        /// <summary>Extra view rotation in degrees (x yaw, y pitch up) on top of where the hunter looks: weapon sway and recoil.</summary>
        public Vector2 AimOffset { get; set; }
        /// <summary>Look speed multiplier from the weapon (lower while zoomed in), on top of the player's sensitivity setting.</summary>
        public float LookScale { get; set; } = 1f;
        /// <summary>Raised when an animal knocks the hunter down (direction it came from).</summary>
        public event Action<Vector3> Attacked;

        /// <summary>Pushes the hunter (an animal's charge) and raises Attacked.</summary>
        public void TakeHit(Vector3 from, float force)
        {
            Vector3 push = transform.position - from;
            push.y = 0f;
            planarVelocity += push.normalized * force;
            Attacked?.Invoke(from);
        }

        void Awake()
        {
            body = GetComponent<CharacterController>();
            if (!input) input = GetComponent<PlayerInputReader>();
            yaw = transform.eulerAngles.y;
            pitch = 0f;
            height = settings.standHeight;
            body.radius = settings.radius;
            ApplyHeight(0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            UpdateLook(dt);
            UpdateCrouch(dt);
            UpdateMove(dt);
        }

        void UpdateLook(float dt)
        {
            float perPixel = settings.dragDegreesPerScreenHeight / Mathf.Max(1, Screen.height);
            Vector2 delta = (input.LookDrag * perPixel + input.LookStick * (settings.stickDegreesPerSecond * dt)) * (LookScale * GameSettings.LookSensitivity);
            if (GameSettings.InvertLook) delta.y = -delta.y;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, settings.minPitch, settings.maxPitch);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cameraPivot.localRotation = Quaternion.Euler(pitch - AimOffset.y, AimOffset.x, 0f);
        }

        void UpdateCrouch(float dt)
        {
            if (input.CrouchPressed)
            {
                if (!crouched) crouched = true;
                else if (HasHeadroom()) crouched = false;
            }
            float target = crouched ? settings.crouchHeight : settings.standHeight;
            height = Mathf.MoveTowards(height, target, settings.crouchTransitionSpeed * dt);
            ApplyHeight(dt);
        }

        bool HasHeadroom()
        {
            float r = settings.radius * 0.95f;
            Vector3 start = transform.position + Vector3.up * (height - r);
            float distance = settings.standHeight - height + 0.05f;
            return !Physics.SphereCast(start, r, Vector3.up, out _, distance, ~0, QueryTriggerInteraction.Ignore);
        }

        void ApplyHeight(float dt)
        {
            body.height = height;
            body.center = new Vector3(0f, height * 0.5f, 0f);
            float t = Mathf.InverseLerp(settings.standHeight, settings.crouchHeight, height);
            float eye = Mathf.Lerp(settings.standEyeHeight, settings.crouchEyeHeight, t);
            float bob = Mathf.Sin(bobPhase * 2f) * settings.headBobAmplitude * SpeedFraction;
            cameraPivot.localPosition = new Vector3(0f, eye + bob, 0f);
        }

        void UpdateMove(float dt)
        {
            float speed = crouched ? settings.crouchSpeed : settings.walkSpeed;
            if (CurrentSurface) speed *= CurrentSurface.speedMultiplier;
            Vector2 stick = input.Move;
            Vector3 wish = (transform.forward * stick.y + transform.right * stick.x) * speed;
            planarVelocity = Vector3.MoveTowards(planarVelocity, wish, settings.acceleration * dt);

            if (IsGrounded && verticalVelocity < 0f) verticalVelocity = -2f;   // keeps the controller pressed to slopes
            verticalVelocity += settings.gravity * dt;

            Vector3 before = transform.position;
            body.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            IsGrounded = body.isGrounded;

            Vector3 moved = transform.position - before;
            moved.y = 0f;
            float distance = moved.magnitude;
            SpeedFraction = Mathf.Clamp01(distance / dt / settings.walkSpeed);

            SampleSurface();
            AdvanceSteps(distance);
        }

        void SampleSurface()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                CurrentSurface = SurfaceQuery.Resolve(hit.collider, hit.point);
            else
                CurrentSurface = SurfaceQuery.Resolve(null, transform.position);
        }

        void AdvanceSteps(float distance)
        {
            if (!IsGrounded || distance <= 0f) return;
            float stepLength = crouched ? settings.stepLengthCrouched : settings.stepLengthStanding;
            bobPhase += distance / stepLength * Mathf.PI;
            stepDistance += distance;
            if (stepDistance < stepLength) return;
            stepDistance -= stepLength;
            leftFoot = !leftFoot;
            Vector3 side = transform.right * (leftFoot ? -0.12f : 0.12f);
            Footstep?.Invoke(new FootstepInfo(transform.position + side, transform.forward, CurrentSurface, crouched, leftFoot));
        }
    }
}
