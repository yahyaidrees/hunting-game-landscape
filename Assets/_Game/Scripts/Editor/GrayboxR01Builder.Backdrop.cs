using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Phone cost of the backdrop (measured on the Galaxy A30s: the far range band alone cost about 3.5 ms a frame):
    /// the far range band and the four painted mountain cards were transparent layers blended over the sky, so the
    /// GPU shaded every pixel there two to four times. Each is now split in two (SV_CloudBand alpha split): a solid
    /// copy, drawn before the sky with depth, keeps the pixels at or above the split alpha, so the sky and the
    /// layers behind are never shaded under them; the original keeps only the soft edges, blended as before. The
    /// image is the same. Depth slots keep the order: mountain cards in front, then the far range, then the sky.
    /// The Alpine scene's shared materials are left alone; the Meadow uses R01_ copies.
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const float SplitAlpha = 0.98f;
        const float MountainSlot = 0.000004f, FarRangeSlot = 0.0000035f;
        const int SolidBackdropQueue = 2480;   // after alpha-tested scenery (2450), before the sky dome (2490)

        static void SplitBackdrop(StringBuilder log)
        {
            var backdrop = GameObject.Find("AlpineEnv/Backdrop");
            if (!backdrop) { log.AppendLine("WARNING: AlpineEnv/Backdrop not found; backdrop not split"); return; }
            int split = 0;
            foreach (Transform layer in backdrop.transform)
            {
                bool far = layer.name.StartsWith("FarRange"), mountain = layer.name.StartsWith("Mountains");
                if (!far && !mountain) continue;
                if (!layer.TryGetComponent<MeshRenderer>(out var renderer) || !layer.TryGetComponent<MeshFilter>(out var filter)) continue;

                // the edge material: our own copy (the far range already is one; the mountain cards are copied here)
                var current = renderer.sharedMaterial;
                Material edge;
                if (far) edge = current;
                else
                {
                    string source = current.name.StartsWith("R01_")
                        ? AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Game/Art/Environment/AlpineEnv/Materials/{current.name.Substring(4)}.mat"))
                        : AssetDatabase.GetAssetPath(current);
                    edge = CopyMaterial(AssetDatabase.LoadAssetAtPath<Material>(source), $"{DistanceDir}/R01_{System.IO.Path.GetFileNameWithoutExtension(source)}.mat");
                    renderer.sharedMaterial = edge;
                }
                float slot = far ? FarRangeSlot : MountainSlot;
                SetSplit(edge, -1f, slot, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, false, edge.renderQueue);

                var solid = CopyMaterial(edge, $"{DistanceDir}/{edge.name}_Solid.mat");
                SetSplit(solid, 1f, slot, BlendMode.One, BlendMode.Zero, true, SolidBackdropQueue);

                var child = layer.Find("Solid");
                if (!child)
                {
                    child = new GameObject("Solid").transform;
                    child.SetParent(layer, false);
                }
                child.gameObject.layer = layer.gameObject.layer;
                if (!child.TryGetComponent<MeshFilter>(out var mf)) mf = child.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = filter.sharedMesh;
                if (!child.TryGetComponent<MeshRenderer>(out var mr)) mr = child.gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = solid;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                split++;
            }
            AssetDatabase.SaveAssets();
            log.AppendLine($"backdrop: {split} layers split into a solid part (drawn before the sky) and soft edges");
        }

        // Copies a material's settings into a file of ours (created once, re-synced every build, GUID kept).
        static Material CopyMaterial(Material source, string path)
        {
            var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!copy)
            {
                copy = new Material(source);
                AssetDatabase.CreateAsset(copy, path);
            }
            else if (copy != source)
            {
                // Not CopyPropertiesFromMaterial: after it, the next SaveAssets wrote the copied state and dropped the
                // split values set on top (2026-11-27: the mountain cards' solid copies lost their split this way)
                var temp = new Material(source);
                EditorUtility.CopySerialized(temp, copy);
                Object.DestroyImmediate(temp);
            }
            copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
            return copy;
        }

        static void SetSplit(Material m, float side, float slot, BlendMode src, BlendMode dst, bool zWrite, int queue)
        {
            m.SetFloat("_AlphaSplitOn", 1f);
            m.EnableKeyword("_ALPHASPLIT");
            m.SetFloat("_AlphaSplit", SplitAlpha);
            m.SetFloat("_SplitSide", side);
            m.SetFloat("_DepthK", slot);
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
            m.SetFloat("_ZWrite", zWrite ? 1f : 0f);
            m.renderQueue = queue;
            EditorUtility.SetDirty(m);
        }
    }
}
