using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>A simple arrow pointing up, drawn as UI geometry (no sprite needed).</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArrowGraphic : MaskableGraphic
    {
        [Range(0.1f, 1f)] [SerializeField] float headLength = 0.45f;
        [Range(0.05f, 1f)] [SerializeField] float shaftWidth = 0.28f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float w = r.width, h = r.height;
            float headBase = r.yMax - h * headLength;
            float sw = w * shaftWidth * 0.5f;
            Color32 c = color;
            // head
            vh.AddVert(new Vector3(r.center.x, r.yMax), c, Vector4.zero);
            vh.AddVert(new Vector3(r.xMin, headBase), c, Vector4.zero);
            vh.AddVert(new Vector3(r.xMax, headBase), c, Vector4.zero);
            vh.AddTriangle(0, 1, 2);
            // shaft
            vh.AddVert(new Vector3(r.center.x - sw, headBase), c, Vector4.zero);
            vh.AddVert(new Vector3(r.center.x + sw, headBase), c, Vector4.zero);
            vh.AddVert(new Vector3(r.center.x + sw, r.yMin), c, Vector4.zero);
            vh.AddVert(new Vector3(r.center.x - sw, r.yMin), c, Vector4.zero);
            vh.AddTriangle(3, 4, 5);
            vh.AddTriangle(3, 5, 6);
        }
    }
}
