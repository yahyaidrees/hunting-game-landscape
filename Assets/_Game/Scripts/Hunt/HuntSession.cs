using System;
using System.Linq;
using Game.Animals;
using Game.Core.Analytics;
using Game.Tracking;
using Game.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Hunt
{
    /// <summary>The steps of a hunt (plan section 6, "The hunt chain").</summary>
    public enum HuntStage { Briefing, Investigate, Track, Stalk, FollowBlood, Result }

    public enum HuntOutcome { None, CleanKill, Kill, Escaped }

    /// <summary>What happened in one hunt: shown on the result screen and sent to analytics.</summary>
    public sealed class HuntReport
    {
        public HuntOutcome outcome;
        public float seconds;
        public int cluesFound, cluesTotal, shots, hits, senseUses, stars, coins;
        public float shotDistance;
    }

    /// <summary>
    /// Runs one hunt from the briefing to the result: investigate the attack scene, follow the trail, close in,
    /// take the shot (or follow the blood), then the result with stars and coins. Steps move on from what the
    /// player does (clues read, distance to the animal, hits), never from a timer. The hunt's text and rewards
    /// come from its HuntDefinition; this component ties them to the scene's clues and animal.
    /// </summary>
    public sealed class HuntSession : MonoBehaviour
    {
        [SerializeField] HuntDefinition hunt;
        [SerializeField] AnimalBrain target;
        [Tooltip("Signs at the attack scene, read in the Investigate step.")]
        [SerializeField] Clue[] attackScene;
        [Tooltip("Finding it means the animal is near: the Stalk step.")]
        [SerializeField] Clue killSite;
        [Tooltip("Hunter scripts paused while the briefing or the result is on screen.")]
        [SerializeField] Behaviour[] pauseInMenus;

        AnimalHealth health;
        WeaponController weapon;
        TrackingSystem tracking;
        float startTime, endAt = -1f;
        bool finished;

        public static HuntSession Active { get; private set; }
        public HuntDefinition Hunt => hunt;
        public HuntStage Stage { get; private set; }
        public HuntReport Report { get; } = new HuntReport();
        public event Action<HuntStage> StageChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = null;

        void Awake() => Active = this;

        void Start()
        {
            health = target ? target.Health : null;
            weapon = FindFirstObjectByType<WeaponController>();
            tracking = TrackingSystem.Active ? TrackingSystem.Active : FindFirstObjectByType<TrackingSystem>();
            if (tracking) { tracking.ClueDiscovered += OnClue; tracking.SenseUsed += OnSense; }
            if (target) target.StateChanged += OnTargetState;
            if (health) health.Hit += OnTargetHit;
            if (weapon) weapon.Fired += OnFired;
            Enter(HuntStage.Briefing);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            if (tracking) { tracking.ClueDiscovered -= OnClue; tracking.SenseUsed -= OnSense; }
            if (target) target.StateChanged -= OnTargetState;
            if (health) health.Hit -= OnTargetHit;
            if (weapon) weapon.Fired -= OnFired;
            if (Stage != HuntStage.Briefing && Stage != HuntStage.Result)
                AnalyticsEvents.Log("hunt_abandon", ("hunt", hunt.huntId), ("stage", Stage), ("seconds", Mathf.RoundToInt(Elapsed)));
            if (Time.timeScale == 0f) Time.timeScale = 1f;
            Game.Core.Settings.QualityTiers.MeasurementAllowed = false;
        }

        float Elapsed => Time.time - startTime;

        /// <summary>The briefing's start button.</summary>
        public void BeginHunt()
        {
            if (Stage != HuntStage.Briefing) return;
            startTime = Time.time;
            AnalyticsEvents.Log("hunt_start", ("hunt", hunt.huntId));
            Enter(attackScene != null && attackScene.Length > 0 ? HuntStage.Investigate : HuntStage.Track);
        }

        /// <summary>The result screen's "hunt again".</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            AnalyticsEvents.Log("hunt_restart", ("hunt", hunt.huntId), ("outcome", Report.outcome));
            SceneManager.LoadScene(gameObject.scene.buildIndex >= 0 ? gameObject.scene.buildIndex : SceneManager.GetActiveScene().buildIndex);
        }

        void Update()
        {
            if (finished || Stage == HuntStage.Briefing) return;

            // the end waits for the kill camera and a moment to watch the animal fall
            if (endAt >= 0f)
            {
                bool cinematic = weapon && weapon.KillCamera && weapon.KillCamera.IsPlaying;
                if (!cinematic && Time.time >= endAt) Finish();
                return;
            }
            if (!target || !health) return;
            if (health.IsWounded && Stage != HuntStage.FollowBlood) Enter(HuntStage.FollowBlood);
            else if (Stage == HuntStage.Track && Vector3.Distance(target.transform.position, PlayerPosition) < hunt.stalkDistance)
                Enter(HuntStage.Stalk);
        }

        Vector3 PlayerPosition => tracking && tracking.Player ? tracking.Player.position
                                : weapon ? weapon.transform.position : transform.position;

        void Enter(HuntStage next)
        {
            Stage = next;
            bool menu = next == HuntStage.Briefing || next == HuntStage.Result;
            Game.Core.Settings.QualityTiers.MeasurementAllowed = !menu;   // Auto measures the phone during real hunting only
            foreach (var b in pauseInMenus) if (b) b.enabled = !menu;
            Time.timeScale = next == HuntStage.Briefing ? 0f : 1f;   // the world waits while the player reads
            if (!menu) AnalyticsEvents.Log("hunt_stage", ("hunt", hunt.huntId), ("stage", next), ("seconds", Mathf.RoundToInt(Elapsed)));
            StageChanged?.Invoke(next);
        }

        // ---------- what the player does ----------

        void OnClue(Clue clue)
        {
            Report.cluesFound++;
            bool atScene = attackScene != null && attackScene.Contains(clue);
            AnalyticsEvents.Log("clue_found", ("hunt", hunt.huntId), ("kind", clue.kind), ("scene", atScene), ("seconds", Mathf.RoundToInt(Elapsed)));
            if (Stage == HuntStage.Investigate && (!atScene || attackScene.All(c => !c || c.Discovered))) Enter(HuntStage.Track);
            if (clue == killSite && (Stage == HuntStage.Investigate || Stage == HuntStage.Track)) Enter(HuntStage.Stalk);
        }

        void OnSense(Clue nearest)
        {
            Report.senseUses++;
            AnalyticsEvents.Log("sense_used", ("hunt", hunt.huntId), ("stage", Stage));
        }

        void OnFired(ShotResult shot)
        {
            if (Stage == HuntStage.Briefing || Stage == HuntStage.Result) return;
            Report.shots++;
            if (shot.hitAnimal) Report.hits++;
            AnalyticsEvents.Log("shot", ("hunt", hunt.huntId), ("hit", shot.hitAnimal), ("zone", shot.hitAnimal ? shot.zone.ToString() : "none"),
                                ("distance", Mathf.RoundToInt(shot.distance)));
        }

        void OnTargetHit(HitOutcome outcome)
        {
            if (!health.IsDead || endAt >= 0f) return;
            Report.outcome = outcome == HitOutcome.CleanKill ? HuntOutcome.CleanKill : HuntOutcome.Kill;
            Report.shotDistance = health.LastHit.distance;
            endAt = Time.time + hunt.resultDelay;
        }

        void OnTargetState(AnimalState state)
        {
            if (state != AnimalState.Gone || endAt >= 0f || health && health.IsDead) return;
            Report.outcome = HuntOutcome.Escaped;
            endAt = Time.time + hunt.resultDelay * 0.6f;
        }

        void Finish()
        {
            finished = true;
            var r = Report;
            r.seconds = Elapsed;
            r.cluesTotal = tracking ? tracking.Clues.Count : r.cluesFound;
            r.stars = r.outcome switch { HuntOutcome.CleanKill => 3, HuntOutcome.Kill => 2, _ => 0 };
            int baseCoins = r.outcome switch { HuntOutcome.CleanKill => hunt.cleanKillCoins, HuntOutcome.Kill => hunt.killCoins, _ => 0 };
            r.coins = baseCoins + (r.outcome == HuntOutcome.Escaped ? 0 : r.cluesFound * hunt.coinsPerClue);
            AnalyticsEvents.Log("hunt_end", ("hunt", hunt.huntId), ("outcome", r.outcome), ("seconds", Mathf.RoundToInt(r.seconds)),
                                ("clues", r.cluesFound), ("clues_total", r.cluesTotal), ("shots", r.shots), ("hits", r.hits),
                                ("sense", r.senseUses), ("distance", Mathf.RoundToInt(r.shotDistance)), ("coins", r.coins));
            Enter(HuntStage.Result);
        }

        // ---------- what the HUD shows ----------

        public string ObjectiveTitle => Stage switch
        {
            HuntStage.Investigate => hunt.investigateTitle,
            HuntStage.Track => hunt.trackTitle,
            HuntStage.Stalk => hunt.stalkTitle,
            HuntStage.FollowBlood => hunt.followBloodTitle,
            HuntStage.Result => hunt.title,
            _ => hunt.title,
        };

        public string ObjectiveDetail
        {
            get
            {
                switch (Stage)
                {
                    case HuntStage.Investigate:
                        int seen = attackScene.Count(c => c && c.Discovered);
                        return Fill(hunt.investigateDetail, seen, attackScene.Length);
                    case HuntStage.Track:
                        int trailTotal = tracking ? tracking.Clues.Count - attackScene.Length : 0;
                        int trailFound = tracking ? tracking.Clues.Count(c => c.Discovered && !attackScene.Contains(c)) : 0;
                        return Fill(hunt.trackDetail, trailFound, trailTotal);
                    case HuntStage.Stalk: return hunt.stalkDetail;
                    case HuntStage.FollowBlood: return hunt.followBloodDetail;
                    default: return "";
                }
            }
        }

        static string Fill(string s, int found, int total) => s.Replace("{found}", found.ToString()).Replace("{total}", total.ToString());

        public string ResultTitle => Report.outcome switch
        {
            HuntOutcome.CleanKill => hunt.cleanKillTitle,
            HuntOutcome.Kill => hunt.killTitle,
            _ => hunt.escapedTitle,
        };

        public string ResultText => Report.outcome switch
        {
            HuntOutcome.CleanKill => hunt.cleanKillText,
            HuntOutcome.Kill => hunt.killText,
            _ => hunt.escapedText,
        };
    }
}
