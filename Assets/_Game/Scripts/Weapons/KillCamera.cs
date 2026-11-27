using System;
using System.Collections;
using System.Collections.Generic;
using Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Weapons
{
    /// <summary>
    /// The slow-motion kill camera (plan section 14: "a follow camera on the bullet with slowed time, cutting to an
    /// impact angle; a small shake"). Plays on clean heart-and-lung kills. The world almost stops while the bullet
    /// flies the shot's real ballistic path with a camera riding behind it; near the animal the view cuts to a side
    /// angle, the hit lands when the bullet arrives, a short shake, then time eases back while the animal falls.
    /// A tap skips it. Everything here runs on real (unscaled) time.
    /// </summary>
    public sealed class KillCamera : MonoBehaviour
    {
        [SerializeField] KillCameraSettings settings;
        [SerializeField] Camera shotCamera;
        [Tooltip("The hunter's own camera: switched off while the shot camera shows.")]
        [SerializeField] Camera playerCamera;
        [SerializeField] PlayerInputReader input;
        [Tooltip("Hunter scripts paused during the shot (movement, rifle), so nothing moves or fires underneath.")]
        [SerializeField] Behaviour[] pauseWhilePlaying;
        [SerializeField] Transform bullet;
        [SerializeField] LineRenderer trail;
        [Tooltip("The ears move with the shot camera, so the impact is heard close, not from the hunter's spot.")]
        [SerializeField] AudioListener playerListener;
        [SerializeField] AudioListener shotListener;

        readonly List<Vector3> path = new List<Vector3>();
        readonly List<float> along = new List<float>();
        readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        Vector3[] trailPoints = new Vector3[8];
        Action impact;
        float savedTimeScale = 1f;
        bool skipRequested;

        public bool IsPlaying { get; private set; }
        /// <summary>True when the kill camera starts, false when play resumes (HUD, audio snapshot, analytics).</summary>
        public static event Action<bool> PlayingChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => PlayingChanged = null;

        void Awake()
        {
            transform.SetParent(null, true);   // the rig flies freely, not with the hunter
            shotCamera.enabled = false;
            if (shotListener) shotListener.enabled = false;
            bullet.gameObject.SetActive(false);
            trail.enabled = false;
        }

        void OnDestroy() { if (IsPlaying) Restore(); }

        /// <summary>
        /// Starts the kill camera for a shot along <paramref name="shotPath"/> (muzzle to impact) into a hit zone at
        /// <paramref name="localPoint"/> on <paramref name="target"/>. <paramref name="applyHit"/> runs once, when the
        /// bullet arrives. Returns false (and does nothing) when the shot does not qualify; the caller applies the hit.
        /// </summary>
        public bool TryPlay(IReadOnlyList<Vector3> shotPath, Transform target, Vector3 localPoint, float distance, Action applyHit)
        {
            if (IsPlaying || !settings || !settings.playOnCleanKills || distance < settings.minDistance) return false;
            if (shotPath == null || shotPath.Count < 2 || !target) return false;
            path.Clear();
            path.AddRange(shotPath);
            impact = applyHit;
            StartCoroutine(Play(target, localPoint));
            return true;
        }

        IEnumerator Play(Transform target, Vector3 localPoint)
        {
            Begin();
            Measure();
            float total = along[along.Count - 1];
            Vector3 aimedEnd = path[path.Count - 1];
            float flight = Mathf.Clamp(total / Mathf.Max(1f, settings.bulletScreenSpeed), settings.flightSeconds.x, settings.flightSeconds.y);
            float started = Time.unscaledTime;
            bool cut = false;
            Vector3 impactCamPos = Vector3.zero;

            // ---- the bullet flies; the world almost stops ----
            for (float t = 0f; t < flight; t += Time.unscaledDeltaTime)
            {
                if (SkipPressed(started)) break;
                // the animal still drifts a little in slowed time: bend the path onto where the zone is now
                Vector3 drift = target.TransformPoint(localPoint) - aimedEnd;
                float s = total * (t / flight);
                Vector3 pos = PointAt(s) + drift * (s / total);
                Vector3 dir = (PointAt(Mathf.Min(total, s + 0.5f)) + drift * Mathf.Min(1f, (s + 0.5f) / total) - pos);
                if (dir.sqrMagnitude < 1e-6f) dir = path[path.Count - 1] - path[path.Count - 2];
                dir.Normalize();

                bullet.SetPositionAndRotation(pos, Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, t * 900f));
                // riding behind the bullet, a long trail would pass right by the lens: only a short streak until the cut
                float trailLength = cut ? settings.trailLength : Mathf.Min(settings.trailLength, -settings.followOffset.z * 0.6f);
                DrawTrail(s, total, drift, trailLength);

                if (!cut && total - s < settings.cutDistance)
                {
                    cut = true;
                    impactCamPos = ImpactCameraPosition(target.TransformPoint(localPoint), dir, target);
                    shotCamera.fieldOfView = settings.impactFieldOfView;
                }
                if (cut)
                {
                    shotCamera.transform.SetPositionAndRotation(impactCamPos,
                        Quaternion.LookRotation(Vector3.Lerp(pos, target.TransformPoint(localPoint), 0.6f) - impactCamPos));
                }
                else
                {
                    Quaternion r = Quaternion.LookRotation(dir);
                    shotCamera.transform.SetPositionAndRotation(pos + r * settings.followOffset, Quaternion.LookRotation(pos + dir * 4f - (pos + r * settings.followOffset)));
                }
                yield return null;
            }

            // ---- impact ----
            bullet.gameObject.SetActive(false);
            trail.enabled = false;
            Vector3 hitPoint = target.TransformPoint(localPoint);
            if (!cut)
            {
                Vector3 dir = (path[path.Count - 1] - path[path.Count - 2]).normalized;
                impactCamPos = ImpactCameraPosition(hitPoint, dir, target);
                shotCamera.fieldOfView = settings.impactFieldOfView;
            }
            ApplyHit();
            if (skipRequested) { Restore(); yield break; }

            // ---- the animal falls while time eases back ----
            Vector3 lookAt = hitPoint;
            float after = Mathf.Max(0.1f, settings.afterImpactSeconds);
            for (float a = 0f; a < after; a += Time.unscaledDeltaTime)
            {
                if (SkipPressed(started)) break;
                float k = a / after;
                Time.timeScale = Mathf.Lerp(settings.afterImpactTimeScale, 1f, k * k);
                lookAt = Vector3.Lerp(lookAt, target.TransformPoint(localPoint), 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
                Vector3 camPos = Vector3.Lerp(impactCamPos, lookAt, settings.pushIn * Mathf.SmoothStep(0f, 1f, k));
                float shake = a < settings.shakeSeconds ? settings.shakeDegrees * (1f - a / settings.shakeSeconds) : 0f;
                float n = Time.unscaledTime * 22f;
                Quaternion jitter = Quaternion.Euler((Mathf.PerlinNoise(n, 0.1f) - 0.5f) * 2f * shake, (Mathf.PerlinNoise(0.4f, n) - 0.5f) * 2f * shake, 0f);
                shotCamera.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(lookAt - camPos) * jitter);
                yield return null;
            }
            Restore();
        }

        bool SkipPressed(float started)
        {
            if (Time.unscaledTime - started < settings.skipDelay) return false;
            bool pressed = (input && input.FirePressed) || (Pointer.current != null && Pointer.current.press.wasPressedThisFrame);
            if (pressed) skipRequested = true;
            return pressed;
        }

        void Begin()
        {
            IsPlaying = true;
            skipRequested = false;
            savedTimeScale = Time.timeScale;
            Time.timeScale = settings.flightTimeScale;
            foreach (var b in pauseWhilePlaying) if (b) b.enabled = false;
            hiddenCanvases.Clear();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.enabled && c.renderMode != RenderMode.WorldSpace) { c.enabled = false; hiddenCanvases.Add(c); }
            shotCamera.fieldOfView = settings.followFieldOfView;
            shotCamera.enabled = true;
            if (playerCamera) playerCamera.enabled = false;
            if (playerListener && shotListener) { playerListener.enabled = false; shotListener.enabled = true; }
            bullet.gameObject.SetActive(true);
            trail.enabled = true;
            PlayingChanged?.Invoke(true);
            Game.Core.Settings.QualityTiers.SetCinematic(true);   // the Low tier renders the close-up at a lower scale
        }

        void ApplyHit()
        {
            var a = impact;
            impact = null;
            a?.Invoke();
        }

        void Restore()
        {
            ApplyHit();   // a skip or an interruption still lands the shot
            Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
            foreach (var b in pauseWhilePlaying) if (b) b.enabled = true;
            foreach (var c in hiddenCanvases) if (c) c.enabled = true;
            hiddenCanvases.Clear();
            if (playerCamera) playerCamera.enabled = true;
            if (shotCamera) shotCamera.enabled = false;
            if (shotListener) shotListener.enabled = false;
            if (playerListener) playerListener.enabled = true;
            if (bullet) bullet.gameObject.SetActive(false);
            if (trail) trail.enabled = false;
            IsPlaying = false;
            PlayingChanged?.Invoke(false);
            Game.Core.Settings.QualityTiers.SetCinematic(false);
        }

        // Side-on view of the impact, on whichever side has a clear line and stays above the ground.
        Vector3 ImpactCameraPosition(Vector3 hit, Vector3 dir, Transform target)
        {
            Vector3 flat = new Vector3(dir.x, 0f, dir.z).normalized;
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, flat);
            Vector3 best = hit - flat * settings.impactCameraDistance + Vector3.up * 0.5f;
            foreach (float sign in new[] { 1f, -1f })
            {
                Vector3 offset = (-flat * 0.55f + side * sign + Vector3.up * 0.28f).normalized * settings.impactCameraDistance;
                Vector3 p = hit + offset;
                if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var ground, 10f, ~0, QueryTriggerInteraction.Ignore)
                    && !ground.transform.IsChildOf(target.root) && p.y < ground.point.y + 0.35f)
                    p.y = ground.point.y + 0.35f;
                if (!Physics.Linecast(p, hit, out var block, ~0, QueryTriggerInteraction.Ignore) || block.transform.IsChildOf(target.root))
                    return p;
            }
            return best;
        }

        void Measure()
        {
            along.Clear();
            float sum = 0f;
            along.Add(0f);
            for (int i = 1; i < path.Count; i++) { sum += Vector3.Distance(path[i - 1], path[i]); along.Add(sum); }
        }

        Vector3 PointAt(float s)
        {
            if (s <= 0f) return path[0];
            for (int i = 1; i < path.Count; i++)
                if (s <= along[i])
                {
                    float seg = along[i] - along[i - 1];
                    return Vector3.Lerp(path[i - 1], path[i], seg > 1e-5f ? (s - along[i - 1]) / seg : 1f);
                }
            return path[path.Count - 1];
        }

        void DrawTrail(float s, float total, Vector3 drift, float length)
        {
            int n = trailPoints.Length;
            trail.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float d = Mathf.Max(0f, s - length * (1f - i / (float)(n - 1)));
                trailPoints[i] = PointAt(d) + drift * (d / total);
            }
            trail.SetPositions(trailPoints);
        }
    }
}
