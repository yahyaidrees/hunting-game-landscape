using System;
using System.Collections.Generic;
using Game.Animals;
using Game.Core.Alarms;
using Game.Core.Audio;
using Game.Tracking;
using Game.Weapons;
using UnityEngine;

namespace Game.Hunt
{
    /// <summary>
    /// Decides when the mentor speaks in a hunt that has a MentorScript: as each step starts, the first time each
    /// kind of sign is read, a map tip, a Hunter Sense tip when the hunter has found nothing for a while, a breathing
    /// tip at the first scope, what the first alarm calls mean, and a word after a miss or a poor hit. One line at a time, never the same line twice
    /// in a hunt, quiet during the briefing, the kill camera and the result.
    /// </summary>
    public sealed class MentorDirector : MonoBehaviour
    {
        [SerializeField] HuntSession session;
        [Tooltip("2D source for voiced lines, on the Voice mixer group.")]
        [SerializeField] AudioSource voice;

        /// <summary>Speaker name, the line, and how long to show it.</summary>
        public static event Action<string, MentorLine, float> Said;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Said = null;

        // a line waiting to be said: reactions to what the player just did are urgent and may cut in
        // step lines (keep) never go stale: they teach what the new step asks
        struct Pending { public MentorLine line; public float since; public bool urgent, keep; }

        const float CutInAfterSeconds = 2f;   // an urgent line may replace one that has been up this long
        const float StaleAfterSeconds = 10f;  // a line that waited longer than this has missed its moment

        readonly List<Pending> queue = new List<Pending>();
        readonly HashSet<MentorLine> said = new HashSet<MentorLine>();
        MentorScript script;
        TrackingSystem tracking;
        WeaponController weapon;
        float nextLineAt, lineStartedAt = -99f, lastProgressAt, nextSenseHintAt;

        void Start()
        {
            if (!session) session = GetComponent<HuntSession>();
            script = session && session.Hunt ? session.Hunt.mentor : null;
            if (!script) { enabled = false; return; }
            tracking = TrackingSystem.Active ? TrackingSystem.Active : FindFirstObjectByType<TrackingSystem>();
            weapon = FindFirstObjectByType<WeaponController>();
            session.StageChanged += OnStage;
            if (tracking) { tracking.ClueDiscovered += OnClue; tracking.PrintRead += OnPrint; }
            if (weapon) weapon.Fired += OnFired;
            AnimalHealth.AnyHit += OnAnimalHit;
            AlarmEvents.Raised += OnAlarm;
        }

        void OnDestroy()
        {
            if (session) session.StageChanged -= OnStage;
            if (tracking) { tracking.ClueDiscovered -= OnClue; tracking.PrintRead -= OnPrint; }
            if (weapon) weapon.Fired -= OnFired;
            AnimalHealth.AnyHit -= OnAnimalHit;
            AlarmEvents.Raised -= OnAlarm;
        }

        void Update()
        {
            var stage = session.Stage;
            if (stage == HuntStage.Briefing || stage == HuntStage.Result) return;

            // stuck: point at Hunter Sense (again later if still stuck)
            if ((stage == HuntStage.Investigate || stage == HuntStage.Track)
                && Time.time - lastProgressAt > script.senseHintAfterSeconds && Time.time >= nextSenseHintAt)
            {
                Say(script.senseHint, allowRepeat: true);
                nextSenseHintAt = Time.time + script.senseHintAfterSeconds * 2f;
            }
            if (stage == HuntStage.Stalk && weapon && weapon.IsScoped) Say(script.firstScope);

            float now = Time.unscaledTime;
            queue.RemoveAll(p => !p.keep && now - p.since > StaleAfterSeconds);
            bool cinematic = weapon && weapon.KillCamera && weapon.KillCamera.IsPlaying;
            if (queue.Count == 0 || cinematic) return;
            var next = queue[0];
            bool cutIn = next.urgent && now - lineStartedAt >= CutInAfterSeconds;
            if (now >= nextLineAt || cutIn)
            {
                queue.RemoveAt(0);
                Speak(next.line);
            }
        }

        void OnStage(HuntStage stage)
        {
            lastProgressAt = Time.time;
            nextSenseHintAt = 0f;
            switch (stage)
            {
                case HuntStage.Investigate: Say(script.investigate, keep: true); break;
                case HuntStage.Track: Say(script.track, keep: true); break;
                case HuntStage.Stalk: Say(script.stalk, keep: true); break;
                case HuntStage.FollowBlood: Say(script.followBlood, keep: true); break;
                case HuntStage.Result: queue.Clear(); break;
            }
        }

        void OnClue(Clue clue)
        {
            lastProgressAt = Time.time;
            Say(script.FirstSign(clue.kind), urgent: true);
            bool onTrail = clue.kind != ClueKind.ClawMarks && clue.kind != ClueKind.DragMarks;
            if (onTrail) Say(script.mapHint);
        }

        // any print read counts as progress; the first print gets the footprint lesson, whether a clue or not
        void OnPrint(Footprint print)
        {
            lastProgressAt = Time.time;
            Say(script.FirstSign(ClueKind.Footprint), urgent: true);
            if (print.running) Say(script.runningPrint, urgent: true);
            else if (print.IsLive) Say(script.livePrint, urgent: true);
        }

        void OnFired(ShotResult shot)
        {
            if (!shot.hitAnimal) Say(script.miss, urgent: true);
        }

        void OnAlarm(AlarmCall call)
        {
            var stage = session.Stage;
            if (stage == HuntStage.Briefing || stage == HuntStage.Result) return;
            var line = call.causedByHunter ? script.alarmHunter
                     : call.kind == AlarmKind.BirdScold ? script.alarmScold : script.alarmPredator;
            Say(line, urgent: call.causedByHunter);
        }

        void OnAnimalHit(AnimalHealth animal, Game.Core.Combat.HitInfo hit, HitOutcome outcome)
        {
            if (outcome == HitOutcome.Wounded || outcome == HitOutcome.WoundedLeg) Say(script.wounded, urgent: true);
        }

        void Say(MentorLine line, bool allowRepeat = false, bool urgent = false, bool keep = false)
        {
            if (line == null || line.IsEmpty) return;
            if (!allowRepeat && !said.Add(line)) return;
            if (queue.Exists(p => p.line == line)) return;
            var pending = new Pending { line = line, since = Time.unscaledTime, urgent = urgent, keep = keep };
            // order: reactions first, then step lines, then general tips
            int at = urgent ? queue.FindIndex(p => !p.urgent)
                   : keep ? queue.FindIndex(p => !p.urgent && !p.keep)
                   : -1;
            if (at < 0) queue.Add(pending); else queue.Insert(at, pending);
        }

        void Speak(MentorLine line)
        {
            float seconds = line.voice ? line.voice.length + 0.6f : 1.8f + line.text.Length * 0.045f;
            lineStartedAt = Time.unscaledTime;
            if (line.voice && voice)
            {
                voice.volume = AudioService.CategoryVolume(SoundCategory.Voice);
                voice.PlayOneShot(line.voice);
            }
            Said?.Invoke(script.speakerName, line, seconds);
            nextLineAt = Time.unscaledTime + seconds + script.minGapSeconds;
        }
    }
}
