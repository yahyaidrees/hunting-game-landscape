using Game.Core.Audio;
using Game.Core.Combat;
using Game.Core.Settings;
using Game.Core.Surfaces;
using Game.Core.Wind;
using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// What the hunter hears and sees around the rifle (plan sections 13 and 14): the shot and its echo, the bolt,
    /// a dry click, the reload, the scope, bullet impacts by surface (heard late when far: sound is slower than the
    /// bullet), the dull thud of a hit on an animal with a light puff (fur instead of blood when blood is off),
    /// muzzle flash and barrel smoke drifting downwind. It also moves the mixer: scope, held breath, kill camera.
    /// </summary>
    public sealed class WeaponFeedback : MonoBehaviour
    {
        const float SpeedOfSound = 343f;

        [SerializeField] WeaponController weapon;
        [SerializeField] Camera viewCamera;

        [Header("Sounds")]
        [SerializeField] SoundCue shot;
        [SerializeField] SoundCue bolt;
        [SerializeField] SoundCue dryFire;
        [SerializeField] SoundCue reload;
        [SerializeField] SoundCue scope;
        [SerializeField] SoundCue hitAnimal;
        [Tooltip("Used where the ground has no impact sound of its own.")]
        [SerializeField] SoundCue defaultImpact;
        [SerializeField] SoundCue killCameraWhoosh;

        [Header("Effects")]
        [Tooltip("First-person muzzle flash (on the rifle model), shown for a moment after each shot.")]
        [SerializeField] GameObject muzzleFlash;
        [SerializeField] ParticleSystem impactDust;
        [SerializeField] ParticleSystem hitPuff;
        [SerializeField] ParticleSystem barrelSmoke;
        [SerializeField] Color bloodColor = new Color(0.32f, 0.05f, 0.04f, 0.75f);
        [SerializeField] Color furColor = new Color(0.55f, 0.52f, 0.48f, 0.7f);
        [SerializeField] float flashSeconds = 0.05f;
        [Tooltip("Barrel smoke start point in front of the eye (right, up, forward), metres.")]
        [SerializeField] Vector3 smokeOffset = new Vector3(0.15f, -0.12f, 1.1f);

        ParticleSystem[] dustPool, puffPool;
        int nextDust, nextPuff;
        float flashUntil;
        bool wasScoped;
        KillCamera killCamera;

        void Awake()
        {
            if (!weapon) weapon = GetComponent<WeaponController>();
            killCamera = weapon.KillCamera;
            weapon.Fired += OnFired;
            weapon.Empty += OnEmpty;
            weapon.ReloadStarted += OnReload;
            weapon.HitLanded += OnHitLanded;
            if (killCamera) KillCamera.PlayingChanged += OnKillCamera;
            dustPool = MakePool(impactDust, 4);
            puffPool = MakePool(hitPuff, 2);
            if (barrelSmoke) barrelSmoke.transform.SetParent(null, true);
            if (muzzleFlash) muzzleFlash.SetActive(false);
        }

        void OnDestroy()
        {
            if (weapon)
            {
                weapon.Fired -= OnFired;
                weapon.Empty -= OnEmpty;
                weapon.ReloadStarted -= OnReload;
                weapon.HitLanded -= OnHitLanded;
            }
            KillCamera.PlayingChanged -= OnKillCamera;
        }

        void Update()
        {
            if (muzzleFlash && muzzleFlash.activeSelf && Time.unscaledTime > flashUntil) muzzleFlash.SetActive(false);

            bool scoped = weapon.IsScoped;
            if (scoped != wasScoped) AudioService.Play(scope, viewCamera.transform.position, 0f, 1f, scoped ? 1f : 0.85f);
            wasScoped = scoped;

            MixState mix = killCamera && killCamera.IsPlaying ? MixState.KillCamera
                         : weapon.HoldingBreath ? MixState.HoldBreath
                         : weapon.ScopeAmount > 0.5f ? MixState.Scope
                         : MixState.Normal;
            AudioService.SetMix(mix, mix == MixState.KillCamera ? 0.08f : -1f);
        }

        void OnFired(ShotResult r)
        {
            Transform eye = viewCamera.transform;
            AudioService.Play(shot, eye.position);
            bool cinematic = killCamera && killCamera.IsPlaying;
            if (!cinematic) AudioService.Play(bolt, eye.position, weapon.Weapon.cycleSeconds * 0.45f);

            if (muzzleFlash)
            {
                muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                muzzleFlash.SetActive(true);
                flashUntil = Time.unscaledTime + flashSeconds;
            }
            if (barrelSmoke)
            {
                barrelSmoke.transform.position = eye.TransformPoint(smokeOffset);
                var wind = WindSystem.Active;
                var vel = barrelSmoke.velocityOverLifetime;
                Vector3 drift = wind ? wind.Direction * (0.2f + wind.Strength * 0.8f) : Vector3.zero;
                vel.x = drift.x; vel.y = 0.15f; vel.z = drift.z;
                barrelSmoke.Play();
            }

            // the ground, a rock or a trunk; animal hits are shown when the hit lands (see OnHitLanded)
            if (r.hitSomething && !r.hitAnimal)
            {
                var surface = SurfaceQuery.Resolve(r.collider, r.point);
                var cue = surface && surface.bulletImpact ? surface.bulletImpact : defaultImpact;
                AudioService.Play(cue, r.point, DelayTo(r.point));
                Burst(dustPool, ref nextDust, r.point, r.normal.sqrMagnitude > 0.01f ? r.normal : Vector3.up,
                      surface ? surface.impactColor : new Color(0.45f, 0.38f, 0.28f, 0.8f));
            }
        }

        void OnHitLanded(HitInfo hit)
        {
            AudioService.Play(hitAnimal, hit.point, DelayTo(hit.point));
            Burst(puffPool, ref nextPuff, hit.point, -hit.direction, GameSettings.Blood ? bloodColor : furColor);
        }

        void OnEmpty() => AudioService.Play(dryFire, viewCamera.transform.position);

        void OnReload() => AudioService.Play(reload, viewCamera.transform.position);

        void OnKillCamera(bool playing)
        {
            if (playing) AudioService.Play(killCameraWhoosh, viewCamera.transform.position);
        }

        // sound travels at 343 m/s: a far impact is heard a moment after it is seen (from wherever the ears are)
        float DelayTo(Vector3 point)
        {
            var ears = FindListener();
            return ears ? Vector3.Distance(ears.position, point) / SpeedOfSound : 0f;
        }

        AudioListener listener;
        Transform FindListener()
        {
            if (!listener || !listener.isActiveAndEnabled)
            {
                listener = null;
                foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if (l.isActiveAndEnabled) { listener = l; break; }
            }
            return listener ? listener.transform : null;
        }

        static ParticleSystem[] MakePool(ParticleSystem prefab, int count)
        {
            if (!prefab) return null;
            var pool = new ParticleSystem[count];
            for (int i = 0; i < count; i++) pool[i] = Instantiate(prefab);
            return pool;
        }

        static void Burst(ParticleSystem[] pool, ref int next, Vector3 at, Vector3 normal, Color color)
        {
            if (pool == null) return;
            var ps = pool[next];
            next = (next + 1) % pool.Length;
            ps.transform.SetPositionAndRotation(at + normal * 0.02f, Quaternion.LookRotation(normal));
            var main = ps.main;
            main.startColor = color;
            ps.Clear();
            ps.Play();
        }
    }
}
