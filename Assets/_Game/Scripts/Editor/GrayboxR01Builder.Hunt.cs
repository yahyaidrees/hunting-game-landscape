using System.Linq;
using System.Text;
using Game.Animals;
using Game.Hunt;
using Game.Player;
using Game.Tracking;
using Game.UI;
using Game.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// The first hunt as data (plan section 6, "The hunt chain"): its HuntDefinition, the attack scene at the goat
    /// pen (the Investigate step), the HuntSession that runs the chain, and the briefing and result cards.
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const string HuntsDataDir = Data + "/Hunts";
        const string FirstHuntPath = HuntsDataDir + "/Hunt_R01_01_GoatPen.asset";

        // The goat pen, east of the camp (spawn-relative x right, z forward); its broken side faces the trail start.
        static readonly Vector2 PenCentre = new Vector2(7f, 6f);
        const float PenHalf = 2f;

        // ---------- attack scene (built with the trail, so its signs are on the wolf's trail) ----------

        static void BuildAttackScene(Transform root, Trail trail, Vector3 spawn, Quaternion rot, TrackingAssets a, StringBuilder log)
        {
            Vector3 W(Vector2 rel) => GroundPoint(spawn + rot * new Vector3(rel.x, 0f, rel.y));
            var pen = new GameObject("GoatPen").transform;
            pen.SetParent(root, false);
            pen.SetPositionAndRotation(W(PenCentre), rot);
            farmMats = EnsureFarmMaterials(log);
            BuildGoatPenTimber(pen, farmMats, log);   // natural timber (owner, 2026-11-27: the grey-box boards looked plain)
            float h = PenHalf;

            // the signs: marks on the broken board, then the drag marks out of the gap toward the trail
            Vector3 board = pen.TransformPoint(new Vector3(-h - 0.5f, 0f, -1f));
            AddClue(trail, "Clue_0a_PenMarks", ClueKind.ClawMarks, GroundPoint(board), 0f,
                "Fresh bite and claw marks on the torn board. The tooth holes are about 4 cm apart, too wide for a fox " +
                "or a jackal: a wolf. It forced the boards and took a young goat.", a, 1.4f);

            Vector3 gap = pen.TransformPoint(new Vector3(-h - 0.8f, 0f, -0.8f));
            Vector3 start = trail.points[0];
            Vector3 mid = Vector3.Lerp(gap, start, 0.5f);
            var drag = AddClue(trail, "Clue_0b_DragMarks", ClueKind.DragMarks, GroundPoint(mid), 0f,
                "A wide scuffed line with goat hair in it, leading away from the pen to the south-west. " +
                "The wolf dragged the goat this way: its prints start where the line ends.", a, 1.4f);
            Vector3 dir = start - gap; dir.y = 0f;
            drag.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir : Vector3.forward);
            GroundDecal(drag.transform, "Streak", a.drag, new Vector2(0.9f, dir.magnitude + 1.5f));
            log.AppendLine("attack scene: goat pen with 2 signs");
        }

        // A wide scuff with a few long streaks, dark earth with goat hair tones.
        static byte[] MakeDragMarks()
        {
            const int w = 64, hgt = 256;
            var rng = new System.Random(5);
            var streaks = Enumerable.Range(0, 6).Select(_ => (x: 0.25f + (float)rng.NextDouble() * 0.5f, width: 0.03f + (float)rng.NextDouble() * 0.06f, strength: 0.4f + (float)rng.NextDouble() * 0.5f)).ToArray();
            var px = new Color[w * hgt];
            for (int y = 0; y < hgt; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / hgt;
                float ends = SmoothEdge(0f, 0.12f, v) * SmoothEdge(1f, 0.85f, v);
                float a = 0.25f * (1f - SmoothEdge(0.25f, 0.5f, Mathf.Abs(u - 0.5f)));   // the wide scuff
                foreach (var s in streaks)
                {
                    float wobble = Mathf.Sin(v * 9f + s.x * 20f) * 0.02f;
                    a = Mathf.Max(a, s.strength * (1f - SmoothEdge(0f, s.width, Mathf.Abs(u - s.x - wobble))));
                }
                px[y * w + x] = new Color(0.2f, 0.16f, 0.11f, Mathf.Clamp01(a * ends) * 0.85f);
            }
            return PngRect(px, w, hgt);
        }

        // ---------- hunt session in the scene ----------

        static void BuildHuntSession(Transform root, GameObject player, StringBuilder log)
        {
            EnsureFolder(HuntsDataDir);
            var hunt = EnsureAsset<HuntDefinition>(FirstHuntPath, log);
            var wolf = root.GetComponentsInChildren<AnimalBrain>(true).FirstOrDefault();
            Clue Find(string n) => root.GetComponentsInChildren<Clue>(true).FirstOrDefault(c => c.name == n);
            // the mentor talks the player through the first hunts (tracking stage 1)
            var mentor = EnsureAsset<MentorScript>(HuntsDataDir + "/Mentor_FirstHunts.asset", log);
            if (!hunt.mentor) { hunt.mentor = mentor; EditorUtility.SetDirty(hunt); }

            var go = new GameObject("HuntSession");
            go.transform.SetParent(root, false);
            var session = go.AddComponent<HuntSession>();
            var voice = go.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            if (audioAssets != null && audioAssets.mixer) voice.outputAudioMixerGroup = audioAssets.mixer.FindMatchingGroups("Voice").FirstOrDefault();
            SetRefs(go.AddComponent<MentorDirector>(), ("session", session), ("voice", voice));
            SetRefs(session, ("hunt", hunt), ("target", wolf), ("killSite", Find("Clue_5_KillSite")));
            var so = new SerializedObject(session);
            SetArray(so.FindProperty("attackScene"), new Object[] { Find("Clue_0a_PenMarks"), Find("Clue_0b_DragMarks") });
            SetArray(so.FindProperty("pauseInMenus"), new Object[] { player.GetComponent<FirstPersonController>(), player.GetComponent<WeaponController>() });
            so.ApplyModifiedPropertiesWithoutUndo();

            // "Hunt again" reloads the scene, which needs it in the build list
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) { scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            log.AppendLine($"hunt session: {hunt.huntId} \"{hunt.title}\", target {(wolf ? wolf.name : "MISSING")}");
        }

        static void SetArray(SerializedProperty p, Object[] values)
        {
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ---------- briefing and result cards ----------

        static void AddHuntFlowHud(RectTransform root, Font font, Color parchment, Color forest)
        {
            Color ink = new Color32(0x24, 0x2A, 0x22, 0xFF);
            Color amber = new Color32(0xE9, 0xA2, 0x3B, 0xFF);
            // the mentor's subtitle, under the hunt message and clear of the clue card
            var safe = (RectTransform)root.Find("SafeArea");
            var sub = NewRect("Mentor", safe);
            Anchor(sub, new Vector2(0.5f, 1f), new Vector2(0f, -285f), new Vector2(1180f, 132f));
            var subBg = sub.gameObject.AddComponent<Image>();
            subBg.color = new Color(0.08f, 0.1f, 0.07f, 0.72f); subBg.raycastTarget = false;
            var subGroup = sub.gameObject.AddComponent<CanvasGroup>();
            subGroup.blocksRaycasts = false; subGroup.interactable = false;
            var who = NewText("Speaker", sub, font, "Ranger", 26, amber, TextAnchor.UpperLeft);
            who.fontStyle = FontStyle.Bold;
            Fill(who.rectTransform, new Vector2(24f, 92f), new Vector2(-24f, -8f));
            var said = NewText("Line", sub, font, "", 32, parchment, TextAnchor.UpperLeft);
            said.horizontalOverflow = HorizontalWrapMode.Wrap;
            Fill(said.rectTransform, new Vector2(24f, 8f), new Vector2(-24f, -42f));
            SetRefs(sub.gameObject.AddComponent<MentorView>(), ("group", subGroup), ("speaker", who), ("line", said));

            var others = root.Cast<Transform>().Select(t => t.gameObject).ToArray();   // the hunt HUD, hidden under a card

            var flow = NewRect("HuntFlow", root);
            Stretch(flow);

            RectTransform Card(string name, Vector2 size)
            {
                var screen = NewRect(name, flow);
                Stretch(screen);
                var dim = screen.gameObject.AddComponent<Image>();
                dim.color = new Color(0f, 0f, 0f, 0.75f);   // linear colour: 75% reads as a clear dim; catches touches
                var card = NewRect("Card", screen);
                Anchor(card, new Vector2(0.5f, 0.5f), Vector2.zero, size);
                var bg = card.gameObject.AddComponent<Image>();
                bg.color = new Color(parchment.r, parchment.g, parchment.b, 0.97f);
                var band = NewRect("Band", card);
                band.anchorMin = new Vector2(0f, 1f); band.anchorMax = Vector2.one; band.pivot = new Vector2(0.5f, 1f);
                band.anchoredPosition = Vector2.zero; band.sizeDelta = new Vector2(0f, 14f);
                band.gameObject.AddComponent<Image>().color = forest;
                return card;
            }
            Text Line(string name, RectTransform card, int size, Color color, TextAnchor align, float top, float height, FontStyle style = FontStyle.Normal)
            {
                var t = NewText(name, card, font, "", size, color, align);
                t.fontStyle = style;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                var r = t.rectTransform;
                r.anchorMin = new Vector2(0f, 1f); r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 1f);
                r.anchoredPosition = new Vector2(0f, -top); r.sizeDelta = new Vector2(-80f, height);
                return t;
            }
            Button ButtonAt(string name, RectTransform card, string label, Color color)
            {
                var r = NewRect(name, card);
                Anchor(r, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(380f, 96f));
                var img = r.gameObject.AddComponent<Image>();
                img.color = color;
                var b = r.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                var t = NewText("Label", r, font, label, 40, ink, TextAnchor.MiddleCenter);
                t.fontStyle = FontStyle.Bold;
                Stretch(t.rectTransform);
                return b;
            }

            var brief = Card("Briefing", new Vector2(1180f, 760f));
            var bTitle = Line("Title", brief, 58, ink, TextAnchor.UpperLeft, 50f, 76f, FontStyle.Bold);
            var bBody = Line("Body", brief, 34, ink, TextAnchor.UpperLeft, 150f, 400f);
            var start = ButtonAt("StartButton", brief, "Start hunt", amber);

            var res = Card("Result", new Vector2(1180f, 800f));
            var rTitle = Line("Title", res, 62, ink, TextAnchor.UpperCenter, 46f, 80f, FontStyle.Bold);
            var rStars = Line("Stars", res, 76, amber, TextAnchor.UpperCenter, 130f, 96f);
            var rBody = Line("Body", res, 34, ink, TextAnchor.UpperCenter, 240f, 150f);
            var rStats = Line("Stats", res, 30, new Color(ink.r, ink.g, ink.b, 0.8f), TextAnchor.UpperCenter, 410f, 50f);
            var rCoins = Line("Coins", res, 44, forest, TextAnchor.UpperCenter, 480f, 64f, FontStyle.Bold);
            var again = ButtonAt("AgainButton", res, "Hunt again", amber);

            var view = flow.gameObject.AddComponent<HuntFlowView>();
            SetRefs(view, ("briefing", brief.parent.gameObject), ("briefingTitle", bTitle), ("briefingBody", bBody), ("startButton", start),
                ("result", res.parent.gameObject), ("resultTitle", rTitle), ("resultStars", rStars), ("resultBody", rBody),
                ("resultStats", rStats), ("resultCoins", rCoins), ("againButton", again));
            var so = new SerializedObject(view);
            SetArray(so.FindProperty("hideWithCards"), others.Cast<Object>().ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
