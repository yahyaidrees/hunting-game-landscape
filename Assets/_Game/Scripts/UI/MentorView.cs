using Game.Hunt;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>The mentor's subtitle: his name and the line, fading in and out (plan: always with subtitles).</summary>
    public sealed class MentorView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Text speaker;
        [SerializeField] Text line;
        [SerializeField] float fadeSeconds = 0.3f;

        float hideAt;

        void Awake() => group.alpha = 0f;
        void OnEnable() => MentorDirector.Said += OnSaid;
        void OnDisable() => MentorDirector.Said -= OnSaid;

        void OnSaid(string who, MentorLine said, float seconds)
        {
            speaker.text = who;
            line.text = said.text;
            hideAt = Time.unscaledTime + seconds;
        }

        void Update()
        {
            float target = Time.unscaledTime < hideAt ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
        }
    }
}
