using Game.Player;
using UnityEngine;

namespace Game.Weapons
{
    /// <summary>How a weapon moves in the hands. Per weapon (on its kit): a heavy rifle sways slower than a shotgun.</summary>
    [System.Serializable]
    public sealed class WeaponMotionSettings
    {
        [Header("Breathing (standing still)")]
        [Tooltip("Up and down in metres.")]
        public float breathLift = 0.0025f;
        [Tooltip("Muzzle rise in degrees.")]
        public float breathPitch = 0.35f;
        [Tooltip("Breaths per second.")]
        public float breathRate = 0.22f;
        [Tooltip("Slow wander of the hands, degrees.")]
        public float drift = 0.25f;

        [Header("Walking (follows the steps)")]
        [Tooltip("Side to side in metres, one swing per two steps.")]
        public float bobSide = 0.011f;
        [Tooltip("Dip in metres on every step.")]
        public float bobDip = 0.009f;
        [Tooltip("Roll in degrees with the side swing.")]
        public float bobRoll = 1.4f;
        [Tooltip("Muzzle nod in degrees on every step.")]
        public float bobPitch = 0.7f;
        [Tooltip("The weapon sits lower and closer while walking (metres).")]
        public Vector3 walkOffset = new Vector3(0f, -0.008f, -0.012f);
        [Tooltip("Muzzle tips down while walking (degrees).")]
        public float walkTilt = 2f;
        [Tooltip("Roll in degrees when strafing.")]
        public float strafeRoll = 3f;

        [Header("Running (speeds above walking pace)")]
        [Tooltip("Speed in m/s where the run pose is complete (walking pace is the motor's walk speed).")]
        public float runSpeed = 4.5f;
        public Vector3 runOffset = new Vector3(0.02f, -0.04f, -0.05f);
        [Tooltip("Run pose in degrees (x muzzle down, y turn left, z roll).")]
        public Vector3 runRotation = new Vector3(10f, -24f, 14f);
        [Tooltip("Bob size while running, times the walking bob.")]
        public float runBob = 1.9f;

        [Header("Look lag")]
        [Tooltip("Weapon turn in degrees per degree turned this frame (it trails behind the view).")]
        public float lookSway = 0.9f;
        [Tooltip("Largest look lag in degrees.")]
        public float lookSwayMax = 3.5f;
        [Tooltip("Sideways trail in metres per degree turned.")]
        public float lookShift = 0.0012f;

        [Header("Crouch")]
        [Tooltip("Drop in metres when crouching or standing up.")]
        public float crouchDip = 0.025f;

        [Header("Feel")]
        [Tooltip("Seconds to follow the pose (higher = heavier).")]
        public float smoothing = 0.09f;
        [Tooltip("How much of the motion stays while aiming down the scope.")]
        public float aimedMotion = 0.2f;
    }

    /// <summary>
    /// The weapon in the hands is never still (owner, 2026-11-27: the gun stayed static walking and standing). On a
    /// transform between the holder (aimed and kicked by WeaponController) and the model: it breathes when standing,
    /// swings and dips with every step, trails behind the view when turning, rolls when strafing, drops when
    /// crouching and takes a lowered run pose at running speed. All of it fades while aiming down the scope.
    /// </summary>
    public sealed class WeaponMotion : MonoBehaviour
    {
        [SerializeField] FirstPersonController controller;
        [SerializeField] WeaponController weapon;
        [SerializeField] WeaponMotionSettings settings = new WeaponMotionSettings();

        Vector3 position, positionVelocity, euler, eulerVelocity, look, lookVelocity;
        float speed, run, dip, dipVelocity;
        bool wasCrouched;

        public WeaponMotionSettings Settings { get => settings; set => settings = value ?? new WeaponMotionSettings(); }

        void Start() => wasCrouched = controller && controller.IsCrouched;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !controller || settings == null) return;
            var s = settings;
            float aimed = weapon ? Mathf.SmoothStep(0f, 1f, weapon.ScopeAmount) : 0f;
            float keep = Mathf.Lerp(1f, s.aimedMotion, aimed);

            // speed and run blend follow the body smoothly (no snapping when the stick is released)
            float walkSpeed = controller.Settings ? controller.Settings.walkSpeed : 2.8f;
            speed = Mathf.MoveTowards(speed, controller.IsGrounded ? controller.SpeedFraction : 0f, dt * 4f);
            float runTarget = Mathf.Clamp01((controller.Speed - walkSpeed) / Mathf.Max(0.1f, s.runSpeed - walkSpeed));
            run = Mathf.MoveTowards(run, runTarget, dt * 3f);

            // breathing and drift, mostly when standing still
            float t = Time.time;
            float still = 1f - speed;
            float breath = Mathf.Sin(t * s.breathRate * Mathf.PI * 2f);
            Vector3 pos = new Vector3(0f, breath * s.breathLift * (0.4f + 0.6f * still), 0f);
            Vector3 rot = new Vector3(
                -breath * s.breathPitch + (Mathf.PerlinNoise(t * 0.3f, 1.7f) - 0.5f) * 2f * s.drift,
                (Mathf.PerlinNoise(4.1f, t * 0.27f) - 0.5f) * 2f * s.drift,
                0f);

            // stride: one side swing per two steps, a dip on every step
            float phase = controller.StepPhase;
            float bob = speed * Mathf.Lerp(1f, s.runBob, run);
            pos.x += Mathf.Sin(phase) * s.bobSide * bob;
            pos.y -= Mathf.Abs(Mathf.Sin(phase)) * s.bobDip * bob;
            rot.z += Mathf.Sin(phase) * s.bobRoll * bob;
            rot.x += Mathf.Abs(Mathf.Cos(phase)) * s.bobPitch * bob;
            pos += s.walkOffset * speed;
            rot.x += s.walkTilt * speed;
            rot.z -= controller.MoveInput.x * s.strafeRoll * speed;

            // the run pose
            pos += s.runOffset * run;
            rot += s.runRotation * run;

            // crouching or standing up: a short drop that springs back
            if (controller.IsCrouched != wasCrouched) { wasCrouched = controller.IsCrouched; dip -= s.crouchDip; }
            dip = Mathf.SmoothDamp(dip, 0f, ref dipVelocity, 0.12f);
            pos.y += dip;

            // look lag: the weapon trails the turn and settles back
            Vector2 turn = controller.LookDelta;
            Vector3 lookTarget = new Vector3(
                Mathf.Clamp(turn.y * s.lookSway, -s.lookSwayMax, s.lookSwayMax),
                Mathf.Clamp(-turn.x * s.lookSway, -s.lookSwayMax, s.lookSwayMax),
                Mathf.Clamp(turn.x * s.lookSway * 0.6f, -s.lookSwayMax, s.lookSwayMax));
            look = Vector3.SmoothDamp(look, lookTarget, ref lookVelocity, s.smoothing * 1.4f);
            rot += look;
            pos.x -= Mathf.Clamp(turn.x * s.lookShift, -0.02f, 0.02f) * (1f - aimed);

            position = Vector3.SmoothDamp(position, pos * keep, ref positionVelocity, s.smoothing);
            euler = Vector3.SmoothDamp(euler, rot * keep, ref eulerVelocity, s.smoothing);
            transform.localPosition = position;
            transform.localRotation = Quaternion.Euler(euler);
        }
    }
}
