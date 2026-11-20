using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Cover
{
    public enum CoverKind
    {
        None,
        /// <summary>Blocks movement and sight (rocks, tree trunks, camp gear).</summary>
        Solid,
        /// <summary>Walk-through, hides the hunter (bushes, young firs).</summary>
        Concealing
    }

    public enum CoverShape
    {
        /// <summary>The object's own mesh (rocks, piles, camp).</summary>
        Mesh,
        /// <summary>A vertical capsule (tree trunk).</summary>
        Trunk,
        /// <summary>A sphere around the object (bush).</summary>
        Sphere
    }

    /// <summary>
    /// Which scattered nature becomes cover, and how (matched by the scatter kind's name prefix). Used by the editor
    /// to generate colliders for nature that is drawn without GameObjects.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/World/Scatter Cover Rules", fileName = "ScatterCoverRules")]
    public sealed class ScatterCoverRules : ScriptableObject
    {
        [Serializable]
        public sealed class Rule
        {
            [Tooltip("Scatter kinds whose name starts with this (for example Rock, Fir, Bush).")]
            public string kindPrefix;
            public CoverKind cover;
            public CoverShape shape;
            [Tooltip("Trunk: radius as a fraction of the mesh width. Sphere: radius as a fraction of half the mesh width.")]
            public float sizeFactor = 1f;
            [Tooltip("Concealing cover: visibility through it.")]
            [Range(0f, 1f)] public float visibility = 0.35f;
            [Tooltip("Surface for footsteps and prints on top of it (for example Rock). Empty for none.")]
            public Game.Core.Surfaces.SurfaceDefinition surface;
        }

        public List<Rule> rules = new List<Rule>();
        [Tooltip("Concealing cover is only generated within this distance of the centre (bushes number in the thousands).")]
        public float concealingRadius = 150f;

        public Rule Find(string kindName)
        {
            foreach (var r in rules)
                if (!string.IsNullOrEmpty(r.kindPrefix) && kindName.StartsWith(r.kindPrefix, StringComparison.OrdinalIgnoreCase))
                    return r;
            return null;
        }
    }
}
