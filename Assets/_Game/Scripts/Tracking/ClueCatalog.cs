using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Tracking
{
    /// <summary>The kinds of sign a hunter can read (plan section 6, "Tracking signs").</summary>
    public enum ClueKind
    {
        Footprint,
        Scat,
        FurOrTwig,
        ClawMarks,
        DragMarks,
        KillSite,
        BloodTrail
    }

    /// <summary>
    /// Name and general explanation of each kind of clue, shown on the clue card. Player-facing text lives here
    /// (later in string tables), not in code.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Tracking/Clue Catalog", fileName = "ClueCatalog")]
    public sealed class ClueCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public ClueKind kind;
            public string displayName;
            [TextArea(2, 5)] public string description;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Get(ClueKind kind)
        {
            foreach (var e in entries)
                if (e.kind == kind) return e;
            return null;
        }
    }
}
