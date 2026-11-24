using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Tracking
{
    /// <summary>
    /// Runs tracking in a hunt scene: builds every trail's footprints at start, keeps the list of clues, makes nearby
    /// clues glow (the stage 1 assist), runs Hunter Sense and counts what the hunter has found.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class TrackingSystem : MonoBehaviour
    {
        [SerializeField] TrackingSettings settings;
        [SerializeField] ClueCatalog catalog;
        [Tooltip("The hunter. Found automatically when empty.")]
        [SerializeField] Transform player;

        readonly List<Trail> trails = new List<Trail>();
        readonly List<Clue> clues = new List<Clue>();
        FootprintPool pool;
        float senseReadyAt;

        public static TrackingSystem Active { get; private set; }
        public TrackingSettings Settings => settings;
        public ClueCatalog Catalog => catalog;
        public Transform Player => player;
        public IReadOnlyList<Trail> Trails => trails;
        public IReadOnlyList<Clue> Clues => clues;
        public FootprintPool Pool => pool;
        public int CluesFound { get; private set; }
        public float SenseCooldownRemaining => Mathf.Max(0f, senseReadyAt - Time.time);
        public float SenseCooldownFraction => settings.senseCooldownSeconds > 0f ? SenseCooldownRemaining / settings.senseCooldownSeconds : 0f;

        public event Action<Clue> ClueDiscovered;
        /// <summary>Raised when Hunter Sense is used: the clue it found, or null if none was in range.</summary>
        public event Action<Clue> SenseUsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = null;

        void OnEnable() => Active = this;

        void OnDisable()
        {
            if (Active == this) Active = null;
        }

        void Start()
        {
            if (!player)
            {
                var controller = FindFirstObjectByType<Game.Player.FirstPersonController>();
                if (controller) player = controller.transform;
            }
            pool = new GameObject("FootprintPool").AddComponent<FootprintPool>();
            pool.transform.SetParent(transform, false);
            pool.Init(settings.footprintPoolSize, settings.footprintDrawDistance);

            trails.Clear();
            trails.AddRange(FindObjectsByType<Trail>(FindObjectsSortMode.None));
            clues.Clear();
            foreach (var t in trails)
            {
                t.Build(pool, settings);
                clues.AddRange(t.Clues);
            }
        }

        void Update()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            Vector3 p = player ? player.position : transform.position;
            float glowR2 = settings.glowRadius * settings.glowRadius;
            foreach (var c in clues)
            {
                float g = 0f;
                if (!c.Discovered)
                {
                    if (c.IsHighlighted) g = 0.65f + 0.35f * pulse;
                    else if (settings.glowNearbyClues && (c.Position - p).sqrMagnitude < glowR2) g = 0.25f + 0.15f * pulse;
                }
                c.SetGlow(g);
            }
        }

        public void Discover(Clue clue)
        {
            if (!clue || clue.Discovered) return;
            clue.MarkDiscovered();
            CluesFound++;
            ClueDiscovered?.Invoke(clue);
        }

        /// <summary>Highlights the nearest clue not yet found. Returns false while cooling down.</summary>
        public bool ActivateSense()
        {
            if (SenseCooldownRemaining > 0f) return false;
            Vector3 p = player ? player.position : transform.position;
            Clue best = null;
            float bestD = settings.senseRadius * settings.senseRadius;
            foreach (var c in clues)
            {
                if (c.Discovered) continue;
                float d = (c.Position - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            if (best) best.Highlight(settings.senseDurationSeconds);
            senseReadyAt = Time.time + settings.senseCooldownSeconds;
            SenseUsed?.Invoke(best);
            return true;
        }

        /// <summary>Clue card text: title ("Wolf footprint"), freshness line and the explanation.</summary>
        public void Describe(Clue clue, out string title, out string freshness, out string body)
        {
            var entry = catalog ? catalog.Get(clue.kind) : null;
            var species = clue.Trail ? clue.Trail.species : null;
            string kindName = entry != null ? entry.displayName : clue.kind.ToString();
            title = species ? $"{species.displayName} {kindName.ToLowerInvariant()}" : kindName;
            freshness = FreshnessUtil.Label(FreshnessUtil.Of(clue.AgeHours, settings));
            if (!string.IsNullOrEmpty(clue.hintOverride)) body = clue.hintOverride;
            else if (clue.kind == ClueKind.Footprint && species && !string.IsNullOrEmpty(species.printHint)) body = species.printHint;
            else body = entry != null ? entry.description : "";
            string reading = Reading(clue);
            if (reading.Length > 0) body = reading + "\n" + body;
        }

        /// <summary>
        /// Which way the animal was going at this sign, and for prints at what pace (plan stage 1: read direction).
        /// Empty for signs that do not travel: where it stopped to feed, or the marks it left at the attack.
        /// </summary>
        public string Reading(Clue clue)
        {
            if (!clue.Trail || clue.kind == ClueKind.KillSite || clue.kind == ClueKind.ClawMarks) return "";
            clue.Trail.Sample(clue.PathDistance, out _, out Vector3 dir);
            string heading = $"Heading {Compass(dir)}";
            if (clue.kind != ClueKind.Footprint || !clue.Trail.species) return heading + ".";
            int stepCm = Mathf.RoundToInt(clue.Trail.species.walkStep * 100f);
            return $"{heading} at a walk: steps about {stepCm} cm apart. A running animal leaves them two or three times farther apart.";
        }

        static readonly string[] CompassNames = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

        /// <summary>The compass word for a horizontal direction (north is the map's up, world +Z).</summary>
        public static string Compass(Vector3 direction)
        {
            float deg = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            int sector = Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / 45f) % 8;
            return CompassNames[sector];
        }
    }
}
