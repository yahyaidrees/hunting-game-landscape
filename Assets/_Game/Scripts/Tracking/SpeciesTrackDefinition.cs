using UnityEngine;

namespace Game.Tracking
{
    /// <summary>Skeleton family (plan section 15): prints and animations are shared inside a family.</summary>
    public enum TrackFamily
    {
        DogFamily,
        CatFamily,
        Bear,
        Boar,
        Hoofed
    }

    /// <summary>How one species' trail looks on the ground: print size, step length, print art and field-guide hint.</summary>
    [CreateAssetMenu(menuName = "Game/Tracking/Species Tracks", fileName = "Tracks")]
    public sealed class SpeciesTrackDefinition : ScriptableObject
    {
        public string speciesId = "wolf";
        public string displayName = "Wolf";
        public TrackFamily family = TrackFamily.DogFamily;

        [Tooltip("Drawn print width (x) and length (y) in metres. Can be larger than life so prints read on a phone; " +
                 "the field-guide hint states the real size.")]
        public Vector2 printSize = new Vector2(0.14f, 0.17f);
        [Tooltip("Distance between one print and the next (alternating left and right) when walking, in metres.")]
        public float walkStep = 0.7f;
        [Tooltip("Sideways offset of each print from the line of travel, in metres.")]
        public float trackHalfWidth = 0.06f;

        [Tooltip("Decal material for fresh and medium prints (sharp edges).")]
        public Material freshPrintMaterial;
        [Tooltip("Decal material for old prints (crumbled, softer).")]
        public Material oldPrintMaterial;

        [Tooltip("What a footprint of this species teaches the player (clue card and field guide).")]
        [TextArea(2, 5)] public string printHint;
    }
}
