using UnityEngine;
using UnityEngine.Rendering;

// Draws FarmScatterData with GPU instancing. For every camera that renders this object's layer,
// cells outside the view or beyond their cull distance are skipped (the distance scales with the
// quality LOD bias and with scope zoom, like a LOD Group), and the rest are drawn in batches.
[ExecuteAlways]
public class FarmScatterRenderer : MonoBehaviour
{
    public FarmScatterData data;
    [Tooltip("Fraction of detail instances (grass, bushes) drawn at each quality level (index = level).")]
    public float[] detailDensityByQuality = { 0.4f, 0.65f, 1f, 1f, 1f, 1f };
    [Tooltip("Keep the data's LOD and cull distances on every quality level (for compact scenes with few objects).")]
    public bool ignoreQualityLodBias;

    const int BatchSize = 1023;
    static readonly float Tan30 = Mathf.Tan(30f * Mathf.Deg2Rad);
    static readonly int TintAId = Shader.PropertyToID("_TintA");
    static readonly int TintBId = Shader.PropertyToID("_TintB");

    Matrix4x4[][] cellMatrices;
    Matrix4x4[][] buffers;
    int[] counts;
    MaterialPropertyBlock[] blocks;
    readonly Plane[] planes = new Plane[6];

    // URP port (2026-11-20): Camera.onPreCull never fires under a Scriptable Render Pipeline, so the scatter
    // draws from RenderPipelineManager.beginCameraRendering (before that camera culls and renders).
    void OnEnable()
    {
        Build();
        RenderPipelineManager.beginCameraRendering += DrawSrp;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= DrawSrp;
    }

    void DrawSrp(ScriptableRenderContext context, Camera cam)
    {
        Draw(cam);
    }

    void OnValidate()
    {
        if (isActiveAndEnabled) Build();
    }

    public void Build()
    {
        cellMatrices = null;
        if (!data) return;

        cellMatrices = new Matrix4x4[data.cells.Length][];
        for (int c = 0; c < data.cells.Length; c++)
        {
            var cell = data.cells[c];
            var m = new Matrix4x4[cell.positionScale.Length];
            for (int i = 0; i < m.Length; i++)
            {
                var p = cell.positionScale[i];
                m[i] = Matrix4x4.TRS(p, cell.rotation[i], new Vector3(p.w, p.w, p.w));
            }
            cellMatrices[c] = m;
        }

        // Two batch buffers per kind: [k * 2] full mesh, [k * 2 + 1] LOD mesh.
        buffers = new Matrix4x4[data.kinds.Length * 2][];
        counts = new int[data.kinds.Length * 2];
        blocks = new MaterialPropertyBlock[data.kinds.Length];
        for (int k = 0; k < data.kinds.Length; k++)
        {
            buffers[k * 2] = new Matrix4x4[BatchSize];
            buffers[k * 2 + 1] = new Matrix4x4[BatchSize];
            blocks[k] = new MaterialPropertyBlock();
            blocks[k].SetColor(TintAId, data.kinds[k].tintA);
            blocks[k].SetColor(TintBId, data.kinds[k].tintB);
        }
    }

    void Draw(Camera cam)
    {
        if (cellMatrices == null || (cam.cullingMask & (1 << gameObject.layer)) == 0) return;
        if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;

        GeometryUtility.CalculateFrustumPlanes(cam, planes);
        var camPos = cam.transform.position;
        float zoom = cam.orthographic ? 1f : Tan30 / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float distanceScale = (ignoreQualityLodBias ? 1f : QualitySettings.lodBias) * zoom;
        int quality = QualitySettings.GetQualityLevel();
        float detailDensity = quality < detailDensityByQuality.Length ? detailDensityByQuality[quality] : 1f;

        for (int c = 0; c < cellMatrices.Length; c++)
        {
            var cell = data.cells[c];
            var kind = data.kinds[cell.kind];
            float sqrDistance = cell.bounds.SqrDistance(camPos);
            float cull = kind.cullDistance * distanceScale;
            if (cull > 0f && sqrDistance > cull * cull) continue;
            if (!GeometryUtility.TestPlanesAABB(planes, cell.bounds)) continue;

            float lod = kind.lodDistance * distanceScale;
            int b = cell.kind * 2 + (kind.lodMesh && lod > 0f && sqrDistance > lod * lod ? 1 : 0);
            var src = cellMatrices[c];
            // Instances are stored in random order, so a prefix is an even thinning of the cell.
            int total = kind.detail ? Mathf.CeilToInt(src.Length * detailDensity) : src.Length;
            for (int i = 0; i < total;)
            {
                int n = Mathf.Min(total - i, BatchSize - counts[b]);
                System.Array.Copy(src, i, buffers[b], counts[b], n);
                counts[b] += n;
                i += n;
                if (counts[b] == BatchSize) Flush(b, cam);
            }
        }
        for (int b = 0; b < counts.Length; b++)
            if (counts[b] > 0) Flush(b, cam);
    }

    void Flush(int b, Camera cam)
    {
        var kind = data.kinds[b / 2];
        bool useLod = (b & 1) == 1;
        var mesh = useLod ? kind.lodMesh : kind.mesh;
        var materials = useLod ? kind.lodMaterials : kind.materials;
        if (mesh)
            for (int s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
                if (materials[s])
                    Graphics.DrawMeshInstanced(mesh, s, materials[s], buffers[b], counts[b], blocks[b / 2],
                                               ShadowCastingMode.Off, false, gameObject.layer, cam);
        counts[b] = 0;
    }
}
