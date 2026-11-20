using System;
using Game.Core.Combat;
using Game.Core.Noise;
using Game.Player;
using UnityEngine;

namespace Game.Weapons
{
    /// <summary>What a shot did: what it hit, where and how far.</summary>
    public readonly struct ShotResult
    {
        public readonly bool hitSomething;
        public readonly bool hitAnimal;
        public readonly HitZone zone;
        public readonly Vector3 point;
        public readonly float distance;
        public readonly float drop;

        public ShotResult(bool hitSomething, bool hitAnimal, HitZone zone, Vector3 point, float distance, float drop)
        {
            this.hitSomething = hitSomething;
            this.hitAnimal = hitAnimal;
            this.zone = zone;
            this.point = point;
            this.distance = distance;
            this.drop = drop;
        }
    }

    /// <summary>
    /// The hunter's rifle (plan section 6, "Shooting"): scope zoom, sway that grows after moving and shrinks when
    /// crouched, hold breath, recoil, bolt cycling, reload and a ballistic shot with bullet drop. The bullet passes
    /// bushes and hits the most vital zone along its path through an animal. Every shot is a loud noise.
    /// </summary>
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] WeaponDefinition weapon;
        [SerializeField] FirstPersonController controller;
        [SerializeField] PlayerInputReader input;
        [SerializeField] Camera viewCamera;
        [Tooltip("The first-person rifle model; hidden while looking through the scope.")]
        [SerializeField] Transform weaponModel;
        [SerializeField] Vector3 hipPosition = new Vector3(0.22f, -0.2f, 0.45f);
        [SerializeField] Vector3 aimPosition = new Vector3(0f, -0.12f, 0.3f);

        readonly RaycastHit[] hits = new RaycastHit[16];
        bool scopeWanted;
        float scopeAmount, nextShotTime, reloadDoneTime = -1f, breathLeft, recoverUntil, recoil, exertion;

        public WeaponDefinition Weapon => weapon;
        public int Ammo { get; private set; }
        public bool IsReloading => reloadDoneTime > Time.time;
        public bool IsCycling => nextShotTime > Time.time;
        /// <summary>0 hip .. 1 fully looking through the scope.</summary>
        public float ScopeAmount => scopeAmount;
        public bool IsScoped => scopeWanted;
        public bool HoldingBreath { get; private set; }
        public float BreathFraction => weapon.holdBreathSeconds > 0f ? breathLeft / weapon.holdBreathSeconds : 0f;
        public bool RecoveringBreath => recoverUntil > Time.time;
        public event Action<ShotResult> Fired;
        public event Action Empty;

        void Awake()
        {
            if (!controller) controller = GetComponent<FirstPersonController>();
            if (!input) input = GetComponent<PlayerInputReader>();
            Ammo = weapon.magazineSize;
            breathLeft = weapon.holdBreathSeconds;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (input.ScopePressed) scopeWanted = !scopeWanted;
            if (input.ReloadPressed) Reload();
            if (input.FirePressed) Fire();
            if (reloadDoneTime > 0f && Time.time >= reloadDoneTime) { Ammo = weapon.magazineSize; reloadDoneTime = -1f; }

            UpdateBreath(dt);
            float target = scopeWanted && !IsReloading ? 1f : 0f;
            scopeAmount = Mathf.MoveTowards(scopeAmount, target, dt / Mathf.Max(0.01f, weapon.scopeInSeconds));
            float s = Mathf.SmoothStep(0f, 1f, scopeAmount);
            viewCamera.fieldOfView = Mathf.Lerp(weapon.hipFieldOfView, weapon.scopeFieldOfView, s);
            controller.LookScale = viewCamera.fieldOfView / weapon.hipFieldOfView;

            // heavy breathing after moving fades over a few seconds
            exertion = Mathf.MoveTowards(exertion, Mathf.Max(exertion, controller.SpeedFraction), dt * 2f);
            exertion = Mathf.MoveTowards(exertion, 0f, dt * 0.35f);

            float amplitude = Mathf.Lerp(weapon.hipSwayDegrees, weapon.scopeSwayDegrees, s)
                            * Mathf.Lerp(1f, weapon.movementSwayMultiplier, exertion)
                            * (controller.IsCrouched ? weapon.crouchSwayMultiplier : 1f)
                            * (HoldingBreath ? weapon.holdBreathSwayMultiplier : RecoveringBreath ? 1.8f : 1f);
            float t = Time.time * weapon.swaySpeed;
            Vector2 sway = new Vector2(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f) * (2f * amplitude);
            recoil = Mathf.MoveTowards(recoil, 0f, dt * Mathf.Max(6f, recoil * 4f));
            controller.AimOffset = sway + new Vector2(0f, recoil);

            if (weaponModel)
            {
                bool showModel = scopeAmount < 0.85f;
                if (weaponModel.gameObject.activeSelf != showModel) weaponModel.gameObject.SetActive(showModel);
                weaponModel.localPosition = Vector3.Lerp(hipPosition, aimPosition, s) + new Vector3(0f, 0f, -recoil * 0.012f);
                weaponModel.localRotation = Quaternion.Euler(-recoil * 2f, 0f, 0f);
            }
        }

        void UpdateBreath(float dt)
        {
            bool wants = input.BreathHeld && scopeAmount > 0.5f && !RecoveringBreath && breathLeft > 0f;
            if (wants)
            {
                breathLeft -= dt;
                if (breathLeft <= 0f) { breathLeft = 0f; recoverUntil = Time.time + weapon.breathRecoverSeconds; wants = false; }
            }
            else if (!RecoveringBreath)
            {
                breathLeft = Mathf.MoveTowards(breathLeft, weapon.holdBreathSeconds, dt * weapon.holdBreathSeconds / Mathf.Max(0.1f, weapon.breathRecoverSeconds));
            }
            HoldingBreath = wants;
        }

        public void Reload()
        {
            if (IsReloading || Ammo >= weapon.magazineSize) return;
            reloadDoneTime = Time.time + weapon.reloadSeconds;
            scopeWanted = false;
        }

        public void Fire()
        {
            if (IsReloading || IsCycling) return;
            if (Ammo <= 0) { Empty?.Invoke(); return; }
            Ammo--;
            nextShotTime = Time.time + weapon.cycleSeconds;
            recoil += weapon.recoilDegrees;

            Transform eye = viewCamera.transform;
            NoiseEvents.Emit(eye.position, weapon.shotHeardRadius, NoiseKind.Gunshot);
            var result = Trace(eye.position, eye.forward);
            Fired?.Invoke(result);
        }

        /// <summary>Flies the bullet in short steps under gravity and returns the first thing it hits.</summary>
        ShotResult Trace(Vector3 origin, Vector3 direction)
        {
            const float step = 0.01f;   // seconds per segment
            Vector3 pos = origin, vel = direction * weapon.muzzleVelocity;
            float travelled = 0f;
            while (travelled < weapon.maxRange)
            {
                Vector3 next = pos + vel * step;
                vel += Physics.gravity * step;
                Vector3 seg = next - pos;
                float len = seg.magnitude;
                int n = Physics.RaycastNonAlloc(pos, seg / len, hits, len, ~0, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, 0, n, HitDistanceComparer.Instance);
                for (int i = 0; i < n; i++)
                {
                    var c = hits[i].collider;
                    if (c is CharacterController) continue;
                    if (c.TryGetComponent(out Hitbox box) && box.Owner != null)
                        return HitAnimal(box, hits[i].point, seg / len, travelled + hits[i].distance, origin, direction);
                    if (c.isTrigger) continue;   // bushes and other soft cover do not stop the bullet
                    return new ShotResult(true, false, HitZone.Body, hits[i].point, travelled + hits[i].distance, DropAt(hits[i].point, origin, direction));
                }
                travelled += len;
                pos = next;
            }
            return new ShotResult(false, false, HitZone.Body, pos, travelled, DropAt(pos, origin, direction));
        }

        // The bullet goes on through the animal: the most vital zone along the next 1.5 m counts.
        ShotResult HitAnimal(Hitbox first, Vector3 point, Vector3 dir, float distance, Vector3 origin, Vector3 aim)
        {
            var owner = first.Owner;
            HitZone best = first.zone;
            int n = Physics.RaycastNonAlloc(point - dir * 0.05f, dir, hits, 1.5f, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
                if (hits[i].collider.TryGetComponent(out Hitbox b) && b.Owner == owner && b.zone > best) best = b.zone;
            owner.ReceiveHit(new HitInfo(best, point, dir, distance, weapon.damageMultiplier));
            return new ShotResult(true, true, best, point, distance, DropAt(point, origin, aim));
        }

        static float DropAt(Vector3 point, Vector3 origin, Vector3 aim)
        {
            Vector3 onLine = origin + aim * Vector3.Dot(point - origin, aim);
            return onLine.y - point.y;
        }

        sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
