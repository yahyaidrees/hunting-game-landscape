using System;
using UnityEngine;

namespace Game.Core.Noise
{
    public enum NoiseKind
    {
        Gunshot
    }

    /// <summary>
    /// One-off loud noises (a gunshot) that animals react to at once, separate from the hunter's continuous
    /// footstep noise (PlayerNoise).
    /// </summary>
    public static class NoiseEvents
    {
        /// <summary>Position, radius in metres, kind.</summary>
        public static event Action<Vector3, float, NoiseKind> Emitted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Emitted = null;

        public static void Emit(Vector3 position, float radius, NoiseKind kind) => Emitted?.Invoke(position, radius, kind);
    }
}
