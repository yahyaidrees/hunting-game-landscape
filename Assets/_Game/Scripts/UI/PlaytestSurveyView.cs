using System.Collections.Generic;
using Game.Core.Analytics;
using Game.Hunt;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Gate B playtest card (plan section 19): a moment after the result card appears, four one-tap questions and an
    /// optional comment, sent as the analytics event "playtest_survey" (or "playtest_survey_skip"). Only in builds
    /// made with the GAME_PLAYTEST define (Game/Build/Google Play Bundle (AAB, playtest)), so store releases never
    /// show it. The card builds itself at start-up: it is a temporary tool, removed after the gate.
    /// </summary>
    public sealed class PlaytestSurveyView : MonoBehaviour
    {
#if GAME_PLAYTEST
        const bool BuiltForPlaytest = true;
#else
        const bool BuiltForPlaytest = false;
#endif
        /// <summary>Shows the card in a build without the define (editor checks).</summary>
        public static bool ForceEnable { get; set; }
        public static bool Enabled => BuiltForPlaytest || ForceEnable;

        [SerializeField] Font font;
        [SerializeField] Color parchment = new Color32(0xEF, 0xE4, 0xCC, 0xFF);
        [SerializeField] Color forest = new Color32(0x2F, 0x4A, 0x36, 0xFF);
        [SerializeField] Color amber = new Color32(0xE9, 0xA2, 0x3B, 0xFF);
        [SerializeField] Color ink = new Color32(0x24, 0x2A, 0x22, 0xFF);
        [Tooltip("Seconds the result card shows on its own before the questions appear.")]
        [SerializeField] float delay = 1.5f;

        sealed class Question
        {
            public string key;
            public string[] labels, values;
            public Image[] buttons;
            public int chosen = -1;
        }

        readonly List<Question> questions = new List<Question>();
        GameObject screen;
        InputField comment;
        Button send;
        Image sendImage;
        HuntSession session;
        float showAt = -1f;
        bool answered;

        void Awake()
        {
            Build();
            screen.SetActive(false);
        }

        void Update()
        {
            if (!session)
            {
                session = HuntSession.Active;
                if (session) session.StageChanged += OnStage;
            }
            if (showAt >= 0f && Time.unscaledTime >= showAt)
            {
                showAt = -1f;
                if (Enabled && !answered && session && session.Stage == HuntStage.Result) Open();
            }
        }

        void OnDestroy()
        {
            if (session) session.StageChanged -= OnStage;
        }

        void OnStage(HuntStage stage)
        {
            if (stage == HuntStage.Result) { answered = false; showAt = Time.unscaledTime + delay; }
            else { showAt = -1f; screen.SetActive(false); }
        }

        void Open()
        {
            foreach (var q in questions) Choose(q, -1);
            comment.text = "";
            screen.SetActive(true);
            screen.transform.SetAsLastSibling();
        }

        void Choose(Question q, int index)
        {
            q.chosen = index;
            for (int i = 0; i < q.buttons.Length; i++) q.buttons[i].color = i == index ? amber : Shade(parchment, 0.86f);
            bool ready = questions.TrueForAll(x => x.chosen >= 0);
            send.interactable = ready;
            sendImage.color = ready ? amber : Shade(parchment, 0.78f);
        }

        void Send()
        {
            var p = new List<(string, object)>();
            AddHunt(p);
            foreach (var q in questions) p.Add((q.key, q.values[q.chosen]));
            string text = comment.text.Trim();
            if (text.Length > 0) p.Add(("comment", text));
            AnalyticsEvents.Log("playtest_survey", p.ToArray());
            Close();
        }

        void Skip()
        {
            var p = new List<(string, object)>();
            AddHunt(p);
            AnalyticsEvents.Log("playtest_survey_skip", p.ToArray());
            Close();
        }

        void AddHunt(List<(string, object)> p)
        {
            if (!session) return;
            p.Add(("hunt", session.Hunt.huntId));
            p.Add(("outcome", session.Report.outcome));
            p.Add(("seconds", Mathf.RoundToInt(session.Report.seconds)));
        }

        void Close()
        {
            answered = true;
            screen.SetActive(false);
        }

        // ---------- the card (1920 x 1080 reference, scaled by height like the rest of the HUD) ----------

        void Build()
        {
            screen = Rect("PlaytestSurvey", transform).gameObject;
            Stretch((RectTransform)screen.transform);
            screen.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);   // catches touches meant for the result card

            var card = Rect("Card", screen.transform);
            Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 920f));
            card.gameObject.AddComponent<Image>().color = new Color(parchment.r, parchment.g, parchment.b, 1f);   // opaque: the result card is right under it
            var band = Rect("Band", card);
            band.anchorMin = new Vector2(0f, 1f); band.anchorMax = Vector2.one; band.pivot = new Vector2(0.5f, 1f);
            band.anchoredPosition = Vector2.zero; band.sizeDelta = new Vector2(0f, 14f);
            band.gameObject.AddComponent<Image>().color = forest;

            Label("Title", card, "Playtest: four quick questions", 50, FontStyle.Bold, new Vector2(60f, -44f), new Vector2(1440f, 64f));
            Label("Note", card, "Tap one answer on each line. Thank you for helping us test.", 30, FontStyle.Normal,
                new Vector2(60f, -112f), new Vector2(1440f, 44f)).color = Shade(ink, 1f, 0.75f);

            AddQuestion(card, 0, "play_again", "Would you play another hunt?", new[] { "Yes", "Maybe", "No" }, new[] { "yes", "maybe", "no" });
            AddQuestion(card, 1, "knew_what_to_do", "Did you know what to do next?", new[] { "Always", "Mostly", "Often lost" }, new[] { "always", "mostly", "lost" });
            AddQuestion(card, 2, "wolf_alive", "Did the wolf feel like a real animal?", new[] { "Yes", "Somewhat", "No" }, new[] { "yes", "somewhat", "no" });
            AddQuestion(card, 3, "helped", "Did someone help you play?", new[] { "No", "A little", "A lot" }, new[] { "no", "little", "lot" });

            // optional comment
            var box = Rect("Comment", card);
            TopLeft(box, new Vector2(60f, -676f), new Vector2(1440f, 92f));
            box.gameObject.AddComponent<Image>().color = Color.white;
            comment = box.gameObject.AddComponent<InputField>();
            var hint = Label("Placeholder", box, "Optional: anything confusing or annoying?", 32, FontStyle.Italic, Vector2.zero, Vector2.zero);
            hint.color = Shade(ink, 1f, 0.45f);
            var typed = Label("Text", box, "", 32, FontStyle.Normal, Vector2.zero, Vector2.zero);
            typed.supportRichText = false;
            foreach (var t in new[] { hint, typed })
            {
                var r = t.rectTransform;
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
                r.offsetMin = new Vector2(24f, 6f); r.offsetMax = new Vector2(-24f, -6f);
                t.alignment = TextAnchor.MiddleLeft;
            }
            comment.textComponent = typed;
            comment.placeholder = hint;
            comment.characterLimit = 100;   // Firebase keeps 100 characters of a parameter
            comment.lineType = InputField.LineType.SingleLine;

            // Skip (quiet) and Send (amber, enabled when every line has an answer)
            var skip = Rect("SkipButton", card);
            Place(skip, new Vector2(0f, 0f), new Vector2(260f, 84f), new Vector2(300f, 90f));
            var skipImage = skip.gameObject.AddComponent<Image>();
            skipImage.color = new Color(0f, 0f, 0f, 0f);
            var skipButton = skip.gameObject.AddComponent<Button>();
            skipButton.targetGraphic = skipImage;
            skipButton.onClick.AddListener(Skip);
            var skipLabel = Label("Label", skip, "Skip", 36, FontStyle.Normal, Vector2.zero, Vector2.zero);
            Fit(skipLabel);
            skipLabel.color = Shade(ink, 1f, 0.6f);

            var sendRect = Rect("SendButton", card);
            Place(sendRect, new Vector2(1f, 0f), new Vector2(-270f, 84f), new Vector2(380f, 96f));
            sendImage = sendRect.gameObject.AddComponent<Image>();
            send = sendRect.gameObject.AddComponent<Button>();
            send.targetGraphic = sendImage;
            send.onClick.AddListener(Send);
            var sendLabel = Label("Label", sendRect, "Send", 40, FontStyle.Bold, Vector2.zero, Vector2.zero);
            Fit(sendLabel);
        }

        void AddQuestion(RectTransform card, int row, string key, string text, string[] labels, string[] values)
        {
            float top = -190f - row * 118f;
            Label("Question" + row, card, text, 36, FontStyle.Normal, new Vector2(60f, top - 18f), new Vector2(720f, 60f)).alignment = TextAnchor.MiddleLeft;
            var q = new Question { key = key, labels = labels, values = values, buttons = new Image[labels.Length] };
            for (int i = 0; i < labels.Length; i++)
            {
                var r = Rect($"Answer{row}_{i}", card);
                TopLeft(r, new Vector2(800f + i * 240f, top), new Vector2(220f, 92f));
                var img = r.gameObject.AddComponent<Image>();
                var b = r.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.transition = Selectable.Transition.None;   // the chosen answer is shown by its colour
                int index = i;
                b.onClick.AddListener(() => Choose(q, index));
                var l = Label("Label", r, labels[i], 34, FontStyle.Bold, Vector2.zero, Vector2.zero);
                Fit(l);
                q.buttons[i] = img;
            }
            questions.Add(q);
        }

        Text Label(string name, Transform parent, string value, int size, FontStyle style, Vector2 topLeft, Vector2 sizeDelta)
        {
            var r = Rect(name, parent);
            TopLeft(r, topLeft, sizeDelta);
            var t = r.gameObject.AddComponent<Text>();
            t.font = font; t.text = value; t.fontSize = size; t.fontStyle = style; t.color = ink;
            t.alignment = TextAnchor.UpperLeft; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static void Fit(Text t)
        {
            Stretch(t.rectTransform);
            t.alignment = TextAnchor.MiddleCenter;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;   // UI
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        // position is the top-left corner, measured from the parent's top-left
        static void TopLeft(RectTransform r, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        static Color Shade(Color c, float k, float alpha = 1f) => new Color(c.r * k, c.g * k, c.b * k, c.a * alpha);
    }
}
