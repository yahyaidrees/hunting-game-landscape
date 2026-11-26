using System;
using UnityEngine;

namespace Game.Core.Alarms
{
    /// <summary>What gave the alarm (plan section 6, "Tracking signs": birds, monkeys and deer call out).</summary>
    public enum AlarmKind
    {
        /// <summary>A flock of small birds bursting out of a bush (sound CLU-01).</summary>
        BirdFlush,
        /// <summary>A single bird in a tree giving a sharp, repeated alarm call (sound CLU-02).</summary>
        BirdScold,
    }

    /// <summary>One alarm call: where it came from, what made it, and who caused it.</summary>
    public readonly struct AlarmCall
    {
        public readonly Vector3 position;
        public readonly AlarmKind kind;
        /// <summary>True when the hunter set it off (too close, too loud); false when a predator did.</summary>
        public readonly bool causedByHunter;

        public AlarmCall(Vector3 position, AlarmKind kind, bool causedByHunter)
        {
            this.position = position;
            this.kind = kind;
            this.causedByHunter = causedByHunter;
        }
    }

    /// <summary>
    /// Alarm calls in the world. The HUD shows each one on the compass (for players with the sound off), the mentor
    /// explains the first ones, and the hunt logs them.
    /// </summary>
    public static class AlarmEvents
    {
        public static event Action<AlarmCall> Raised;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Raised = null;

        public static void Raise(AlarmCall call) => Raised?.Invoke(call);
    }
}
