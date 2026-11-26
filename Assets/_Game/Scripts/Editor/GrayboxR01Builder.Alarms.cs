using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Animals;
using Game.Core.Audio;
using Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Alarm calls (plan section 6, "Tracking signs"): bird flocks in bushes that burst out when a predator moves
    /// near (or the hunter blunders in), sentinel birds in trees that scold a nearby predator, their placeholder
    /// sounds (CLU-01, CLU-02), the burst of birds, and the compass pulse on the HUD.
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        sealed class AlarmAssets
        {
            public SoundCue flush, scold;
            public ParticleSystem birds;
        }

        /// <summary>Set by Build(), read by the scene builder.</summary>
        static AlarmAssets alarmAssets;

        static AlarmAssets EnsureAlarmAssets(StringBuilder log)
        {
            void PH(string name, int count, System.Func<int, float[]> make)
            {
                for (int i = 1; i <= count; i++) WriteIfChanged($"{PlaceholderDir}/PH_{name}_{i:00}.wav", Synth.Wav(make(i)));
            }
            PH("BirdFlush", 3, Synth.BirdFlush);
            PH("BirdScold", 3, Synth.BirdScold);
            AssetDatabase.Refresh();
            ImportPlaceholders();

            var animals = audioAssets != null && audioAssets.mixer ? audioAssets.mixer.FindMatchingGroups("Animals").FirstOrDefault() : null;
            var a = new AlarmAssets();
            // heard far: an alarm call is a direction the hunter should be able to pick up across the meadow
            a.flush = Cue("Cue_Alarm_BirdFlush", c => { c.clips = PHClips("BirdFlush", 3); c.output = animals; c.minDistance = 6f; c.maxDistance = 140f; c.priority = 40; c.volume = 0.9f; c.placeholderFor = "CLU-01 a flock of small birds bursting out of a bush, wing flaps and alarm chirps"; }, log);
            a.scold = Cue("Cue_Alarm_BirdScold", c => { c.clips = PHClips("BirdScold", 3); c.output = animals; c.minDistance = 6f; c.maxDistance = 140f; c.priority = 50; c.volume = 0.8f; c.placeholderFor = "CLU-02 a single bird giving a sharp repeated alarm call from a tree"; }, log);

            var mat = EnsureVfxMaterial("M_VFX_Bird", "T_VFX_BirdFlap.png", MakeBirdSheet);
            a.birds = BuildParticlePrefab("VFX_BirdFlush", mat, ps =>
            {
                var main = ps.main;
                main.duration = 1f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 5.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3.5f);   // a short jump up, then low and away (the drift)
                main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.65f);   // larger than life, so a flock reads at 50 m
                main.startRotation = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.07f, 0.06f, 0.05f), new Color(0.16f, 0.13f, 0.1f));   // dark silhouettes read on grass and sky
                main.gravityModifier = -0.04f;
                main.maxParticles = 24;
                var e = ps.emission;
                e.SetBursts(new[] { new ParticleSystem.Burst(0f, 7, 12), new ParticleSystem.Burst(0.18f, 2, 4) });
                var shape = ps.shape;
                shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 45f; shape.radius = 0.6f; shape.rotation = new Vector3(-90f, 0f, 0f);   // up out of the bush
                var vel = ps.velocityOverLifetime;
                vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(0.6f); vel.z = new ParticleSystem.MinMaxCurve(0f);
                var noise = ps.noise;
                noise.enabled = true; noise.strength = 1.1f; noise.frequency = 0.5f; noise.scrollSpeed = 0.6f;
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) },
                };
                var sheet = ps.textureSheetAnimation;
                sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid;
                sheet.numTilesX = 2; sheet.numTilesY = 2;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
                sheet.cycleCount = 60;   // about 12 wingbeats a second over a 5 s flight
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.minParticleSize = 0.012f;   // of the screen height: about 13 px at 1080p, so a flock 60 m off still reads
            }, log);
            return a;
        }

        // Four frames (2 x 2) of a small bird seen from below, wings up, level, down, level: white, tinted by the
        // particle colour. 64 px per frame.
        static byte[] MakeBirdSheet()
        {
            const int N = 128, F = 64;
            float[] wingDeg = { 38f, 8f, -26f, 8f };
            var px = new Color[N * N];
            for (int frame = 0; frame < 4; frame++)
            {
                // the grid reads tiles left to right from the top row: frame 0 is the top-left tile
                int ox = (frame % 2) * F, oy = (1 - frame / 2) * F;
                for (int y = 0; y < F; y++)
                for (int x = 0; x < F; x++)
                {
                    float u = x + 0.5f - F * 0.5f, v = y + 0.5f - F * 0.5f;
                    // body and tail
                    float body = Mathf.Sqrt(u * u / 16f + (v - 2f) * (v - 2f) / 64f) - 1f;   // 4 x 8 px ellipse
                    float tail = Mathf.Max(Mathf.Abs(u) - 2.5f + (v + 6f) * 0.25f, Mathf.Abs(v + 9f) - 4f);
                    float d = Mathf.Min(body * 3f, tail);
                    // wings: tapered strokes from the shoulders, slightly swept back
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float a = wingDeg[frame] * Mathf.Deg2Rad;
                        Vector2 s = new Vector2(side * 2f, 4f), dir = new Vector2(side * Mathf.Cos(a), Mathf.Sin(a) - 0.12f).normalized;
                        Vector2 p = new Vector2(u, v) - s;
                        float t = Mathf.Clamp(Vector2.Dot(p, dir), 0f, 26f);
                        float width = Mathf.Lerp(6.5f, 2f, t / 26f);
                        d = Mathf.Min(d, (p - dir * t).magnitude - width);
                    }
                    float alpha = 1f - SmoothEdge(-0.6f, 0.6f, d);
                    px[(oy + y) * N + ox + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            return Png(px, N);
        }

        // ---------- scene ----------

        // Flocks in bushes (and long grass) and sentinels in trees across the hunting area: a few around where the wolf feeds and
        // roams, none right at the camp (the hunter would flush them at the start).
        static void BuildAlarmScene(Transform root, Vector3 spawn, Quaternion rot, AlarmAssets a, StringBuilder log)
        {
            var data = AssetDatabase.LoadAssetAtPath<FarmScatterData>(R01ScatterPath);
            if (data == null || a == null) { log.AppendLine("WARNING: no scatter data; no alarm birds"); return; }
            Vector3 centre = spawn + rot * new Vector3(-12f, 0f, 14f);
            Vector3 kill = GroundPoint(spawn + rot * new Vector3(WolfPath[WolfPath.Length - 1].x, 0f, WolfPath[WolfPath.Length - 1].y));

            var bushes = new List<Vector3>();
            var tufts = new List<Vector3>();   // meadow birds (larks, pipits) also sit in long grass
            var trees = new List<(Vector3 pos, float height)>();
            foreach (var cell in data.cells)
            {
                string kindName = data.kinds[cell.kind].name ?? "";
                bool bush = kindName.Contains("Bush") || kindName.Contains("Shrub");
                bool tree = (kindName.Contains("Tree") || kindName.Contains("Fir")) && !kindName.Contains("Baby");
                bool tuft = kindName.Contains("Tuft");
                if (!bush && !tree && !tuft) continue;
                float h = cell.bounds.size.y;
                // detail scatter (bushes) is thinned on lower tiers by drawing a prefix of each cell: keep to the part
                // the Low tier still draws, so the birds never burst out of a bush that is not there
                int keep = data.kinds[cell.kind].detail ? Mathf.FloorToInt(cell.positionScale.Length * 0.3f) : cell.positionScale.Length;
                for (int i = 0; i < keep; i++)
                {
                    var ps = cell.positionScale[i];
                    var p = new Vector3(ps.x, ps.y, ps.z);
                    if (Flat(p, centre) > 95f || Flat(p, spawn) < 16f) continue;
                    if (bush) bushes.Add(p); else if (tuft) tufts.Add(p); else trees.Add((p, Mathf.Max(4f, h)));
                }
            }

            var rng = new System.Random(4242);
            var flocks = new List<Vector3>();
            // three near the kill site first (the feeding wolf roams about 16 m from it)
            foreach (var p in bushes.Concat(tufts).Where(b => Flat(b, kill) > 6f && Flat(b, kill) < 30f).OrderBy(_ => rng.Next()))
                if (flocks.Count < 3 && flocks.All(f => Flat(f, p) > 12f)) flocks.Add(p);
            foreach (var p in bushes.OrderBy(_ => rng.Next()).Concat(tufts.OrderBy(_ => rng.Next())))
                if (flocks.Count < 16 && flocks.All(f => Flat(f, p) > 20f)) flocks.Add(p);

            var sentinels = new List<Vector3>();
            foreach (var t in trees.Where(t => Flat(t.pos, kill) > 18f && Flat(t.pos, kill) < 36f).OrderBy(_ => rng.Next()).Take(1))
                sentinels.Add(t.pos + Vector3.up * t.height * 0.7f);
            foreach (var t in trees.OrderBy(_ => rng.Next()))
                if (sentinels.Count < 6 && sentinels.All(s => Flat(s, t.pos) > 40f)) sentinels.Add(t.pos + Vector3.up * t.height * 0.7f);

            var group = new GameObject("AlarmBirds").transform;
            group.SetParent(root, false);
            for (int i = 0; i < flocks.Count; i++)
            {
                var go = new GameObject($"Flock_{i:00}");
                go.transform.SetParent(group, false);
                go.transform.position = flocks[i] + Vector3.up * 0.9f;
                var birds = (ParticleSystem)PrefabUtility.InstantiatePrefab(a.birds);
                birds.transform.SetParent(go.transform, false);
                var c = go.AddComponent<AlarmBirds>();
                SetRefs(c, ("call", a.flush), ("birds", birds));
            }
            for (int i = 0; i < sentinels.Count; i++)
            {
                var go = new GameObject($"Sentinel_{i:00}");
                go.transform.SetParent(group, false);
                go.transform.position = sentinels[i];
                var c = go.AddComponent<AlarmBirds>();
                SetRefs(c, ("call", a.scold));
                var so = new SerializedObject(c);
                so.FindProperty("kind").enumValueIndex = (int)AlarmBirds.Kind.Sentinel;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            log.AppendLine($"alarm birds: {flocks.Count} flocks in bushes and long grass, {sentinels.Count} sentinels in trees ({bushes.Count} bushes, {tufts.Count} tufts, {trees.Count} trees in range)");
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        // ---------- HUD ----------

        // Under the wind panel: a marker that slides to the alarm's bearing, points at it, pulses and fades.
        static void AddAlarmHud(RectTransform safe, Font font, Sprite knob, Color parchment, Color forest)
        {
            Color amber = new Color32(0xE9, 0xA2, 0x3B, 0xFF);
            Color ink = new Color32(0x26, 0x1E, 0x17, 0xFF);
            var strip = NewRect("AlarmIndicator", safe);
            Anchor(strip, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(900f, 38f));
            var group = strip.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
            // a faint compass line with a centre tick, so the marker's offset reads as "left of ahead"
            var line = NewRect("Line", strip);
            Anchor(line, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 4f));
            var lineImg = line.gameObject.AddComponent<Image>(); lineImg.color = new Color(forest.r, forest.g, forest.b, 0.6f); lineImg.raycastTarget = false;
            var tick = NewRect("Ahead", strip);
            Anchor(tick, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 22f));
            var tickImg = tick.gameObject.AddComponent<Image>(); tickImg.color = new Color(parchment.r, parchment.g, parchment.b, 0.7f); tickImg.raycastTarget = false;

            var marker = NewRect("Marker", strip);
            Anchor(marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 38f));
            var ring = NewRect("Ring", marker);
            Anchor(ring, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 38f));
            var ringImg = ring.gameObject.AddComponent<Image>(); ringImg.sprite = knob; ringImg.color = amber; ringImg.raycastTarget = false;
            var dot = NewRect("Dot", marker);
            Anchor(dot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 38f));
            var dotImg = dot.gameObject.AddComponent<Image>(); dotImg.sprite = knob; dotImg.color = amber; dotImg.raycastTarget = false;
            var arrowRect = NewRect("Arrow", marker);
            Anchor(arrowRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 26f));
            arrowRect.gameObject.AddComponent<CanvasRenderer>();
            var arrow = arrowRect.gameObject.AddComponent<ArrowGraphic>();
            arrow.color = ink; arrow.raycastTarget = false;
            var label = NewText("Label", marker, font, "Birds flew up", 26, parchment, TextAnchor.MiddleLeft);
            var lr = label.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(1f, 0.5f); lr.pivot = new Vector2(0f, 0.5f);
            lr.anchoredPosition = new Vector2(10f, 0f); lr.sizeDelta = new Vector2(260f, 36f);
            label.gameObject.AddComponent<Shadow>();
            SetRefs(strip.gameObject.AddComponent<AlarmIndicatorView>(),
                ("group", group), ("marker", marker), ("arrow", arrowRect), ("ring", ring), ("label", label));
        }
    }
}
