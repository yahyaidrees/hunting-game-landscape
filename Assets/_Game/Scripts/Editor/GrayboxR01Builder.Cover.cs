using System.Collections.Generic;
using System.Text;
using Game.Core.Cover;
using Game.Core.Surfaces;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Cover part of the graybox builder: the environment's rocks, trees and bushes are drawn by FarmScatterRenderer
    /// without GameObjects, so nothing collides with them. This generates colliders from the same placement data:
    /// solid cover (rocks, trunks, camp) and concealing triggers (bushes) for the animals' line of sight.
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const string CoverRulesPath = WorldDataDir + "/ScatterCover_Alpine.asset";
        const int ConcealmentLayer = 2;   // "Ignore Raycast": left out of the navigation mesh, still hit by explicit sight rays

        static ScatterCoverRules EnsureCoverRules(Dictionary<SurfaceKind, SurfaceDefinition> surfaces, StringBuilder log)
        {
            EnsureFolder(WorldDataDir);
            var rules = AssetDatabase.LoadAssetAtPath<ScatterCoverRules>(CoverRulesPath);
            if (rules) return rules;
            rules = ScriptableObject.CreateInstance<ScatterCoverRules>();
            var rock = surfaces[SurfaceKind.Rock];
            void Add(string prefix, CoverKind cover, CoverShape shape, float size, float visibility, SurfaceDefinition surface) =>
                rules.rules.Add(new ScatterCoverRules.Rule { kindPrefix = prefix, cover = cover, shape = shape, sizeFactor = size, visibility = visibility, surface = surface });
            // order matters: the first matching prefix wins (FirBaby before Fir)
            Add("FirBaby", CoverKind.Concealing, CoverShape.Sphere, 0.7f, 0.3f, null);
            Add("Fir", CoverKind.Solid, CoverShape.Trunk, 0.028f, 1f, null);
            Add("Rock", CoverKind.Solid, CoverShape.Mesh, 1f, 1f, rock);
            Add("Pile", CoverKind.Solid, CoverShape.Mesh, 1f, 1f, rock);
            Add("Camp", CoverKind.Solid, CoverShape.Mesh, 1f, 1f, null);
            Add("Tent", CoverKind.Solid, CoverShape.Mesh, 1f, 1f, null);
            Add("Bush", CoverKind.Concealing, CoverShape.Sphere, 0.8f, 0.35f, null);
            AssetDatabase.CreateAsset(rules, CoverRulesPath);
            log.AppendLine("created " + CoverRulesPath);
            return rules;
        }

        static void BakeScatterCover(Transform root, Vector3 centre, ScatterCoverRules rules, StringBuilder log)
        {
            var scatter = Object.FindFirstObjectByType<FarmScatterRenderer>();
            if (!scatter || !scatter.data) { log.AppendLine("WARNING: no scatter renderer; no cover generated"); return; }

            var parent = new GameObject("ScatterCover").transform;
            parent.SetParent(root, false);
            var groups = new Dictionary<string, Transform>();
            int solid = 0, soft = 0;
            float r2 = rules.concealingRadius * rules.concealingRadius;

            foreach (var cell in scatter.data.cells)
            {
                var kind = scatter.data.kinds[cell.kind];
                var rule = rules.Find(kind.name);
                if (rule == null || rule.cover == CoverKind.None || !kind.mesh) continue;
                if (!groups.TryGetValue(kind.name, out var group))
                {
                    group = new GameObject(kind.name).transform;
                    group.SetParent(parent, false);
                    groups[kind.name] = group;
                }
                Bounds b = kind.mesh.bounds;
                for (int i = 0; i < cell.positionScale.Length; i++)
                {
                    Vector4 ps = cell.positionScale[i];
                    Vector3 pos = ps;
                    if (rule.cover == CoverKind.Concealing)
                    {
                        Vector2 flat = new Vector2(pos.x - centre.x, pos.z - centre.z);
                        if (flat.sqrMagnitude > r2) continue;
                    }
                    var go = new GameObject($"{kind.name}_{group.childCount}");
                    go.transform.SetParent(group, false);
                    go.transform.SetPositionAndRotation(pos, cell.rotation[i]);
                    go.transform.localScale = Vector3.one * ps.w;
                    go.isStatic = true;

                    Collider col;
                    switch (rule.shape)
                    {
                        case CoverShape.Trunk:
                            var cap = go.AddComponent<CapsuleCollider>();
                            cap.direction = 1;
                            cap.radius = b.size.x * rule.sizeFactor;
                            cap.height = b.size.y;
                            cap.center = new Vector3(0f, b.min.y + b.size.y * 0.5f, 0f);
                            col = cap;
                            break;
                        case CoverShape.Sphere:
                            var sphere = go.AddComponent<SphereCollider>();
                            sphere.radius = b.extents.x * rule.sizeFactor;
                            sphere.center = new Vector3(0f, b.center.y, 0f);
                            col = sphere;
                            break;
                        default:
                            var mc = go.AddComponent<MeshCollider>();
                            mc.sharedMesh = kind.mesh;
                            col = mc;
                            break;
                    }

                    if (rule.cover == CoverKind.Concealing)
                    {
                        col.isTrigger = true;
                        go.layer = ConcealmentLayer;
                        go.AddComponent<Concealment>().visibility = rule.visibility;
                        soft++;
                    }
                    else
                    {
                        if (rule.surface) go.AddComponent<SurfaceTag>().surface = rule.surface;
                        solid++;
                    }
                }
            }
            Physics.SyncTransforms();
            log.AppendLine($"cover: {solid} solid colliders (rocks, trunks, camp), {soft} concealing bushes within {rules.concealingRadius:0} m");
        }
    }
}
