using Game.Tracking;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The card that opens when a clue is inspected: what it is, how fresh it is, and what it teaches.
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
        Clue shown;

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
                if (interactor) interactor.Inspected += Show;
            }
            var sys = TrackingSystem.Active;
            if (shown && sys && interactor &&
                Vector3.Distance(shown.Position, interactor.transform.position) > sys.Settings.inspectRange + closeWhenBeyondRange)
                Hide();
        }

        void OnDestroy()
        {
            if (interactor) interactor.Inspected -= Show;
        }

        void Show(Clue clue)
        {
            var sys = TrackingSystem.Active;
            if (!sys) return;
            sys.Describe(clue, out string t, out string f, out string b);
            title.text = t;
            freshness.text = f;
            body.text = b;
            shown = clue;
            panel.SetActive(true);
        }

        void Hide()
        {
            shown = null;
            panel.SetActive(false);
        }
    }
}
