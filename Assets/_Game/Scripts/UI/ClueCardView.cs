using Game.Tracking;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The card that opens when a clue or a print is inspected: what it is, how fresh it is, and what it teaches.
    /// Closes with its button or when the hunter walks away.
    /// </summary>
    public sealed class ClueCardView : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Text title;
        [SerializeField] Text freshness;
        [SerializeField] Text body;
        [SerializeField] Button closeButton;
        [Tooltip("Closes by itself when the hunter is this far beyond the inspect range.")]
        [SerializeField] float closeWhenBeyondRange = 3f;

        ClueInteractor interactor;
        bool showing;
        Vector3 shownAt;

        void Awake()
        {
            closeButton.onClick.AddListener(Hide);
            panel.SetActive(false);
        }

        void Update()
        {
            if (!interactor)
            {
                interactor = FindFirstObjectByType<ClueInteractor>();
                if (interactor) { interactor.Inspected += Show; interactor.PrintInspected += ShowPrint; }
            }
            var sys = TrackingSystem.Active;
            if (showing && sys && interactor &&
                Vector3.Distance(shownAt, interactor.transform.position) > sys.Settings.inspectRange + closeWhenBeyondRange)
                Hide();
        }

        void OnDestroy()
        {
            if (interactor) { interactor.Inspected -= Show; interactor.PrintInspected -= ShowPrint; }
        }

        void Show(Clue clue)
        {
            var sys = TrackingSystem.Active;
            if (!sys) return;
            sys.Describe(clue, out string t, out string f, out string b);
            Open(t, f, b, clue.Position);
        }

        void ShowPrint(Footprint print)
        {
            var sys = TrackingSystem.Active;
            if (!sys) return;
            sys.DescribePrint(print, out string t, out string f, out string b);
            Open(t, f, b, print.position);
        }

        void Open(string t, string f, string b, Vector3 at)
        {
            title.text = t;
            freshness.text = f;
            body.text = b;
            showing = true;
            shownAt = at;
            panel.SetActive(true);
        }

        void Hide()
        {
            showing = false;
            panel.SetActive(false);
        }
    }
}
