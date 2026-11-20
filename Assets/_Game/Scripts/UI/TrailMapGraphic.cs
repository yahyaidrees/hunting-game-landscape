using Game.Tracking;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Draws the hunter's map: each trail up to the furthest clue found, the clues found, and the hunter as an arrow.
    /// North (+Z) is up. Centred on the tracking system's position, mapHalfSizeMetres to each side.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TrailMapGraphic : MaskableGraphic
    {
        [SerializeField] Color trailColor = new Color32(0xE9, 0xA2, 0x3B, 0xFF);
        [SerializeField] Color clueColor = new Color32(0x4A, 0x37, 0x28, 0xFF);
        [SerializeField] Color hunterColor = new Color32(0xC8, 0x45, 0x2F, 0xFF);
        [SerializeField] Color gridColor = new Color32(0x4A, 0x37, 0x28, 0x30);
        [SerializeField] float trailWidth = 7f;
        [SerializeField] float clueSize = 16f;
        [SerializeField] float hunterSize = 26f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var sys = TrackingSystem.Active;
            Rect r = GetPixelAdjustedRect();

            // faint 10 m grid
            float half = sys ? sys.Settings.mapHalfSizeMetres : 45f;
            for (float m = -half; m <= half; m += 10f)
            {
                float u = r.xMin + (m + half) / (2f * half) * r.width;
                float v = r.yMin + (m + half) / (2f * half) * r.height;
                Line(vh, new Vector2(u, r.yMin), new Vector2(u, r.yMax), 1.5f, gridColor);
                Line(vh, new Vector2(r.xMin, v), new Vector2(r.xMax, v), 1.5f, gridColor);
            }
            if (!sys) return;

            Vector3 centre = sys.transform.position;
            Vector2 ToMap(Vector3 w) => new Vector2(
                r.center.x + (w.x - centre.x) / half * r.width * 0.5f,
                r.center.y + (w.z - centre.z) / half * r.height * 0.5f);

            foreach (var trail in sys.Trails)
            {
                float revealed = trail.RevealedDistance;
                if (revealed <= 0f) continue;
                const float step = 1f;
                trail.Sample(0f, out Vector3 prev, out _);
                for (float d = step; d <= revealed + 0.001f; d += step)
                {
                    trail.Sample(Mathf.Min(d, revealed), out Vector3 p, out _);
                    Line(vh, ToMap(prev), ToMap(p), trailWidth, trailColor);
                    prev = p;
                }
            }
            foreach (var c in sys.Clues)
                if (c.Discovered) Square(vh, ToMap(c.Position), clueSize, clueColor);

            if (sys.Player)
            {
                Vector2 pos = ToMap(sys.Player.position);
                Vector3 f3 = sys.Player.forward;
                Vector2 f = new Vector2(f3.x, f3.z).normalized;
                Vector2 side = new Vector2(-f.y, f.x);
                Vector2 tip = pos + f * hunterSize * 0.6f;
                Vector2 bl = pos - f * hunterSize * 0.4f + side * hunterSize * 0.35f;
                Vector2 br = pos - f * hunterSize * 0.4f - side * hunterSize * 0.35f;
                int i = vh.currentVertCount;
                vh.AddVert(tip, hunterColor, Vector4.zero);
                vh.AddVert(bl, hunterColor, Vector4.zero);
                vh.AddVert(br, hunterColor, Vector4.zero);
                vh.AddTriangle(i, i + 1, i + 2);
            }
        }

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-4f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            int i = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector4.zero);
            vh.AddVert(a + n, c, Vector4.zero);
            vh.AddVert(b + n, c, Vector4.zero);
            vh.AddVert(b - n, c, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        static void Square(VertexHelper vh, Vector2 p, float size, Color c)
        {
            float h = size * 0.5f;
            int i = vh.currentVertCount;
            vh.AddVert(p + new Vector2(-h, -h), c, Vector4.zero);
            vh.AddVert(p + new Vector2(-h, h), c, Vector4.zero);
            vh.AddVert(p + new Vector2(h, h), c, Vector4.zero);
            vh.AddVert(p + new Vector2(h, -h), c, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
