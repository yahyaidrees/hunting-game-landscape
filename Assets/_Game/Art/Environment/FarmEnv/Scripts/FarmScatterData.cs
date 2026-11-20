using System;
using UnityEngine;

// Placement data for the farm's scattered nature, written by Tools > Farm Env and drawn by FarmScatterRenderer.
public class FarmScatterData : ScriptableObject
{
    [Serializable]
    public class Kind
    {
        public string name;
        public Mesh mesh;
        public Material[] materials = new Material[0];   // one per submesh
        public Color tintA = Color.white;
        public Color tintB = Color.white;
        [Tooltip("Cull distance (m) at a 60° FOV and LOD bias 1; 0 = never culled by distance.")]
        public float cullDistance;
        [Tooltip("Small detail (grass, bushes) that is thinned out on low quality levels.")]
        public bool detail;
        [Tooltip("Optional lighter mesh used beyond lodDistance.")]
        public Mesh lodMesh;
        public Material[] lodMaterials = new Material[0];
        [Tooltip("Distance (m, at a 60° FOV and LOD bias 1) where lodMesh takes over; 0 = no LOD.")]
        public float lodDistance;
    }

    [Serializable]
    public class Cell
    {
        public int kind;
        public Bounds bounds;
        public Vector4[] positionScale = new Vector4[0];   // xyz = position, w = uniform scale
        public Quaternion[] rotation = new Quaternion[0];
    }

    public Kind[] kinds = new Kind[0];
    public Cell[] cells = new Cell[0];
}
