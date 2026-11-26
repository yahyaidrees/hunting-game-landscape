using Game.Tracking;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Inspect button, shown only while an unread clue or print is in reach and near the centre of the view.</summary>
    public sealed class InspectButtonView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] GameObject visual;

        ClueInteractor interactor;

        void Awake() => button.onClick.AddListener(() => { if (interactor) interactor.InspectTarget(); });

        void Update()
        {
            if (!interactor) interactor = FindFirstObjectByType<ClueInteractor>();
            bool show = interactor && interactor.HasInspectable;
            if (visual.activeSelf != show) visual.SetActive(show);
        }
    }
}
