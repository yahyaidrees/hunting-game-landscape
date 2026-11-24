using Game.Hunt;
using Game.Tracking;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Top-left objective (plan section 12, Hunt HUD: "clues found 2 of 4"). With a hunt running it shows the
    /// current step of the hunt chain; without one, plain clue progress.
    /// </summary>
    public sealed class ObjectivePanelView : MonoBehaviour
    {
        [SerializeField] Text title;
        [SerializeField] Text progress;
        [SerializeField] string huntTitle = "Track the wolf";
        [SerializeField] string doneText = "The trail ends at a kill site. The wolf will come back to feed.";

        void Update()
        {
            var session = HuntSession.Active;
            if (session)
            {
                Set(session.ObjectiveTitle, session.ObjectiveDetail);
                return;
            }
            var sys = TrackingSystem.Active;
            if (!sys) return;
            int found = sys.CluesFound, total = sys.Clues.Count;
            Set(huntTitle, total > 0 && found >= total ? doneText : $"Clues found {found} of {total}");
        }

        void Set(string t, string p)
        {
            if (title.text != t) title.text = t;
            if (progress.text != p) progress.text = p;
        }
    }
}
