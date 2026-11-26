using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Core.Settings
{
    /// <summary>
    /// What each graphics tier changes (plan section 13, "Quality tiers and budgets"). The render pipeline asset
    /// holds render scale and shadows; the rest is applied by QualityTierApplier. Starting values, to confirm on
    /// the three test phones.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Settings/Quality Tier Set", fileName = "QualityTiers")]
    public sealed class QualityTierSet : ScriptableObject
    {
        [Serializable]
        public sealed class Tier
        {
            [Tooltip("Render scale and shadows live here (one copy of the phone pipeline asset per tier).")]
            public UniversalRenderPipelineAsset pipeline;
            [Tooltip("Frame rate cap when the player has not chosen one.")]
            public int frameRate = 30;
            [Tooltip("0 = full size textures, 1 = half size.")]
            [Range(0, 2)] public int textureMipLimit;
            [Tooltip("Fraction of grass, flowers and small bushes drawn.")]
            [Range(0.1f, 1f)] public float detailDensity = 1f;
            [Tooltip("Multiplies level-of-detail and scatter cull distances.")]
            public float lodBias = 1f;
            [Tooltip("Bloom on the colour grade volume (tonemapping, grade and vignette stay on every tier).")]
            public bool bloom = true;
            [Tooltip("FXAA edge smoothing on the game cameras. Off on Low: on a 720p phone it blurs more than it smooths (about 1.5 ms on a Mali-G71).")]
            public bool fxaa = true;
        }

        public Tier low = new Tier { frameRate = 30, textureMipLimit = 1, detailDensity = 0.3f, lodBias = 0.7f, bloom = false, fxaa = false };
        public Tier medium = new Tier { frameRate = 30, detailDensity = 0.6f, lodBias = 0.85f };
        public Tier high = new Tier { frameRate = 60, detailDensity = 1f, lodBias = 1f };

        public Tier Get(QualityTier tier) => tier switch { QualityTier.Low => low, QualityTier.Medium => medium, _ => high };
    }

    /// <summary>The tier in use, for systems that cannot hold a reference to the applier.</summary>
    public static class QualityTiers
    {
        /// <summary>Fraction of detail scatter to draw, or -1 before any tier is applied (renderers keep their own).</summary>
        public static float DetailDensity { get; internal set; } = -1f;
        public static QualityTier Current { get; internal set; } = QualityTier.High;
        public static bool HasApplied { get; internal set; }
        /// <summary>Raised after a tier is applied (also when only the frame rate changed).</summary>
        public static event Action<QualityTier> Applied;

        internal static void RaiseApplied() => Applied?.Invoke(Current);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Applied = null; DetailDensity = -1f; HasApplied = false; Current = QualityTier.High; }
    }
}
