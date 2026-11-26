using System;
using System.Collections.Generic;
using Game.Tracking;
using UnityEngine;

namespace Game.Hunt
{
    /// <summary>One thing the mentor says: always shown as a subtitle, with an optional voice clip (plan decision 16).</summary>
    [Serializable]
    public sealed class MentorLine
    {
        [TextArea(1, 3)] public string text;
        [Tooltip("Optional voice (one of the plan's ~40 AI-voiced lines). Subtitles show either way.")]
        public AudioClip voice;

        public MentorLine() { }
        public MentorLine(string text) { this.text = text; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(text);
    }

    /// <summary>
    /// What the mentor, an old ranger, says during the first hunts (plan section 6, tracking stage 1 "Follow": clues
    /// glow and the mentor names each sign). A hunt turns him on by pointing at a script; later hunts leave it empty.
    /// Player-facing text moves to string tables with localisation.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Hunt/Mentor Script", fileName = "MentorScript")]
    public sealed class MentorScript : ScriptableObject
    {
        [Serializable]
        public sealed class SignLine
        {
            public ClueKind kind;
            public MentorLine line;
        }

        public string speakerName = "Ranger";

        [Header("When each step starts")]
        public MentorLine investigate = new MentorLine("Easy now. Before we chase anything, read the pen. Walk up and look at the torn boards.");
        public MentorLine track = new MentorLine("Now the trail. The glowing marks are signs. Read each one: the map fills in as you go.");
        public MentorLine stalk = new MentorLine("Slow now. Check the wind before you take another step. Keep it blowing in your face.");
        public MentorLine followBlood = new MentorLine("Now find its blood on the ground and follow it. Stay ready: a hurt wolf can turn on you.");

        [Header("The first time each sign is read")]
        public List<SignLine> firstSign = new List<SignLine>
        {
            new SignLine { kind = ClueKind.ClawMarks, line = new MentorLine("See the tooth marks? Wide apart. Too big for a fox. That's a wolf.") },
            new SignLine { kind = ClueKind.DragMarks, line = new MentorLine("It dragged the goat out this way. Follow the line, and you'll find its prints.") },
            new SignLine { kind = ClueKind.Footprint, line = new MentorLine("Wolf print. Four toes, and the claw tips show. The toes point the way it went.") },
            new SignLine { kind = ClueKind.Scat, line = new MentorLine("Droppings, still dark and soft. It passed here this morning.") },
            new SignLine { kind = ClueKind.FurOrTwig, line = new MentorLine("Grey hair on the grass. It crossed the rocks here, where no print stays.") },
            new SignLine { kind = ClueKind.KillSite, line = new MentorLine("This is where it ate. It'll come back to feed. Find cover downwind, and wait.") },
            new SignLine { kind = ClueKind.BloodTrail, line = new MentorLine("Blood. Fresh drops mean it's close. Watch the bushes ahead.") },
        };

        [Header("Help")]
        [Tooltip("Said once, after the first sign on the trail itself.")]
        public MentorLine mapHint = new MentorLine("Open the map now and then. Every sign you read draws more of the trail.");
        [Tooltip("Said when the hunter has found nothing new for a while.")]
        public MentorLine senseHint = new MentorLine("Lost it? Use your Hunter Sense. It shows you the nearest sign.");
        [Tooltip("Seconds without a new sign before the Hunter Sense hint (and between repeats).")]
        public float senseHintAfterSeconds = 45f;
        [Tooltip("Said the first time the hunter raises the scope while stalking.")]
        public MentorLine firstScope = new MentorLine("Easy. Breathe out, hold it, then squeeze.");

        [Header("Alarm calls (said once each)")]
        [Tooltip("The first time birds burst out because of a predator.")]
        public MentorLine alarmPredator = new MentorLine("Hear that? Birds don't burst out of a bush for nothing. Something is moving over there.");
        [Tooltip("The first time the hunter flushes birds.")]
        public MentorLine alarmHunter = new MentorLine("Careful. When birds go up, every animal nearby knows where you are. Crouch near bushes.");
        [Tooltip("The first time a bird scolds from a tree.")]
        public MentorLine alarmScold = new MentorLine("That bird is scolding something on the ground. Could be our wolf. Note where it is.");

        [Header("After a shot")]
        public MentorLine miss = new MentorLine("Missed. Don't chase it. Breathe, and wait for a clear shot.");
        public MentorLine wounded = new MentorLine("Hit, but not clean. Aim just behind the shoulder next time: heart and lungs.");

        [Header("Pacing")]
        [Tooltip("Shortest time between two lines, in seconds.")]
        public float minGapSeconds = 1f;

        public MentorLine FirstSign(ClueKind kind)
        {
            foreach (var s in firstSign) if (s.kind == kind) return s.line;
            return null;
        }
    }
}
