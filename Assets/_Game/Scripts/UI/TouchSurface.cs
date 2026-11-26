using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// An invisible area that catches touches (the drag-to-look half of the screen) without drawing anything.
    /// A transparent Image would still be drawn as a large layer, which costs on low-end phones.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TouchSurface : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
    }
}
