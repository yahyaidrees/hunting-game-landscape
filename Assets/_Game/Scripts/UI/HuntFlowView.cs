using Game.Hunt;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The hunt's two full-screen cards: the briefing before it starts (title, story, Start) and the result after
    /// it ends (outcome, stars, the numbers, coins, Hunt again). The hunt HUD underneath is hidden while a card shows.
    /// </summary>
    public sealed class HuntFlowView : MonoBehaviour
    {
        [Header("Briefing")]
        [SerializeField] GameObject briefing;
        [SerializeField] Text briefingTitle;
        [SerializeField] Text briefingBody;
        [SerializeField] Button startButton;

        [Header("Result")]
        [SerializeField] GameObject result;
        [SerializeField] Text resultTitle;
        [SerializeField] Text resultStars;
        [SerializeField] Text resultBody;
        [SerializeField] Text resultStats;
        [SerializeField] Text resultCoins;
        [SerializeField] Button againButton;

        [Tooltip("Hunt HUD parts hidden while a card shows (sticks, buttons, panels).")]
        [SerializeField] GameObject[] hideWithCards;

        HuntSession session;

        void Awake()
        {
            startButton.onClick.AddListener(() => { if (session) session.BeginHunt(); });
            againButton.onClick.AddListener(() => { if (session) session.Restart(); });
            briefing.SetActive(false);
            result.SetActive(false);
        }

        void Update()
        {
            if (session) return;
            session = HuntSession.Active;
            if (!session) return;
            session.StageChanged += Show;
            Show(session.Stage);
        }

        void OnDestroy()
        {
            if (session) session.StageChanged -= Show;
        }

        void Show(HuntStage stage)
        {
            bool brief = stage == HuntStage.Briefing, done = stage == HuntStage.Result;
            briefing.SetActive(brief);
            result.SetActive(done);
            foreach (var g in hideWithCards) if (g) g.SetActive(!brief && !done);
            var hunt = session.Hunt;
            if (brief)
            {
                briefingTitle.text = hunt.title;
                briefingBody.text = hunt.briefing;
            }
            if (done)
            {
                var r = session.Report;
                resultTitle.text = session.ResultTitle;
                resultStars.text = new string('★', r.stars) + new string('☆', 3 - r.stars);
                resultBody.text = session.ResultText;
                int minutes = Mathf.FloorToInt(r.seconds / 60f), secs = Mathf.FloorToInt(r.seconds % 60f);
                string shot = r.shotDistance > 0f ? $"   Shot  {r.shotDistance:0} m" : "";
                resultStats.text = $"Time  {minutes}:{secs:00}   Clues  {r.cluesFound} of {r.cluesTotal}   Shots  {r.shots}{shot}";
                resultCoins.text = r.coins > 0 ? $"+{r.coins} coins" : "No reward";
            }
        }
    }
}
