using UnityEngine;

namespace Game.Hunt
{
    /// <summary>
    /// One hunt as data (plan section 6, "The hunt chain"): what the player is told, what each step asks of them,
    /// how the result reads and what it pays. A new hunt is a new asset plus a scene that places its trail; the
    /// HuntSession in that scene runs the chain. Player-facing text moves to string tables with localisation.
    /// Detail lines may use {found} and {total} (clues).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Hunt/Hunt Definition", fileName = "Hunt")]
    public sealed class HuntDefinition : ScriptableObject
    {
        [Tooltip("Stable id for saves and analytics, e.g. R01_H01.")]
        public string huntId = "R01_H01";
        public string title = "The goat pen";
        [Tooltip("Who or what attacked, for the result screen (\"the wolf\").")]
        public string targetName = "the wolf";

        [Tooltip("The mentor's lines for this hunt (tracking stage 1, hunts 1 to 5). Empty: no mentor.")]
        public MentorScript mentor;

        [Header("Briefing (1. Alert)")]
        [TextArea(3, 8)]
        public string briefing =
            "Something broke into the goat pen last night and took a young goat. The other goats are too scared to eat.\n\n" +
            "Look around the pen first: the marks will tell you what did it and which way it went. Then follow the trail, " +
            "keep the wind in your face, and end it with one clean shot.";

        [Header("Objectives")]
        public string investigateTitle = "Inspect the broken pen";
        public string investigateDetail = "Signs read {found} of {total}";
        public string trackTitle = "Follow the trail";
        public string trackDetail = "Clues found {found} of {total}";
        public string stalkTitle = "The wolf is close";
        public string stalkDetail = "Approach from downwind. Stay low and quiet.";
        public string followBloodTitle = "Follow the blood trail";
        public string followBloodDetail = "Find it before it recovers.";

        [Header("Result")]
        public string cleanKillTitle = "Clean kill";
        [TextArea(2, 4)] public string cleanKillText = "One shot to the heart and lungs. The goats are safe, and the farm will hear about this.";
        public string killTitle = "The wolf is down";
        [TextArea(2, 4)] public string killText = "The farm is safe. A shot to the heart and lungs would have been quicker and earned more.";
        public string escapedTitle = "It got away";
        [TextArea(2, 4)] public string escapedText = "The wolf escaped. It will be back for the goats. Read the wind, move slowly and wait for a clear shot.";

        [Header("Rewards")]
        public int cleanKillCoins = 300;
        public int killCoins = 180;
        [Tooltip("Extra coins for every clue found (tracking is part of the reward).")]
        public int coinsPerClue = 10;

        [Header("Pacing")]
        [Tooltip("The wolf counts as close (stalk step) within this distance, even before the kill site is found.")]
        public float stalkDistance = 25f;
        [Tooltip("Seconds between the end (kill or escape) and the result screen.")]
        public float resultDelay = 2.5f;
    }
}
