using Game.Tracking;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Top-left objective with clue progress (plan section 12, Hunt HUD: "clues found 2 of 4").</summary>
    public sealed class ObjectivePanelView : MonoBehaviour
    {
        [SerializeField] Text title;
        [SerializeField] Text progress;
        [SerializeField] string huntTitle = "Track the wolf";
        [SerializeField] string doneText = "The trail ends at a kill site. The wolf will come back to feed.";

        int shownFound = -1, shownTotal = -1;

        void Update()
        {
            var sys = TrackingSystem.Active;
            if (!sys) return;
            int found = sys.CluesFound, total = sys.Clues.Count;
            if (found == shownFound && total == shownTotal) return;
            shownFound = found;
            shownTotal = total;
            title.text = huntTitle;
            progress.text = total > 0 && found >= total ? doneText : $"Clues found {found} of {total}";
        }
    }
}
