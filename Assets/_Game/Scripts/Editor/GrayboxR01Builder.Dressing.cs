using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core.Cover;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Meadow look, step C: plants, rocks and trees (style sheet: SourceAssets/Generated/Images/StyleSheet).
    /// Adds to a copy of the environment's scatter data (the original stays untouched): deodar-like conifers and
    /// oaks in groups, shrubs and tall grass hugging the rocks, and flower drifts in the grass, all on the
    /// environment's own baked-light shader. Trees from the owner's "Easy Mobile trees pack" (photo cards, very
    /// cheap); flowers are generated. New trees get soft ground shadows stamped into a copy of the baked shadow
    /// map, and the cover rules know them (trees block sight, shrubs hide). Rocks get lichen and moss (SV_Baked
    /// _ROCKDETAIL with a CC0 mossy-rock photo).
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const string DressingDir = "Assets/_Game/Art/Environment/Dressing";
        const string R01ScatterPath = DressingDir + "/R01_ScatterData.asset";
        const string AlpineScatterPath = "Assets/_Game/Art/Environment/AlpineEnv/Generated/AL_ScatterData.asset";
        const string AlpineShadowPath = "Assets/_Game/Art/Environment/AlpineEnv/Generated/AL_GroundShadow.png";
        const string R01ShadowPath = GroundArtDir + "/Generated/T_R01_GroundShadow.png";
        const string EasyTreesDir = "Assets/_Incoming/OldGame/Environment/Easy Mobile trees pack";

        sealed class DressKind
        {
            public string name, source;   // source: Easy Mobile tree prefab, or "flower:<cell>"
            public float height;          // metres at instance scale 1
            public float crown;           // crown radius as a share of the mesh's width (shadow stamping)
            public bool conifer, shrub;
            // texture grade: a common brightness (sRGB luma), green hue in degrees and saturation, so one plant's
            // photo matches the next; the final match to the environment is the tint below
            public float luma, hue, sat;
            public bool trimBase;         // the photo has a strip of grass under the trunk: cut it
            // instance tint, measured on screen (lit, graded, post-processed) so the plant's average colour matches
            // the environment's own: trees against FirB/FirC (sRGB 0.21, 0.29, 0.12), shrubs against BushA/B
            public Color tint = Color.white;
            public DressKind(string name, string source, float height, float crown = 0.4f, bool conifer = false, bool shrub = false,
                             float luma = 0.18f, float hue = 68f, float sat = 0.36f, bool trimBase = false)
            {
                this.name = name; this.source = source; this.height = height; this.crown = crown; this.conifer = conifer; this.shrub = shrub;
                this.luma = luma; this.hue = hue; this.sat = sat; this.trimBase = trimBase;
            }
        }

        // Only the pack's trees whose shapes sit with the painted firs: the round "lollipop" broadleaf and ball
        // bushes (Tree_05, 15, 21) looked like toys next to them and were dropped; the environment's own firs and
        // bushes fill those roles instead.
        static readonly DressKind[] DressTrees =
        {
            new DressKind("MD_TreeDeodar", "Tree_03", 17f, 0.42f, conifer: true, luma: 0.26f, hue: 72f, sat: 0.36f)
                { tint = new Color(1.216f, 1.467f, 1.116f) },
            new DressKind("MD_TreeOak", "Tree_11", 13f, 0.45f, luma: 0.26f, hue: 74f, sat: 0.38f, trimBase: true)
                { tint = new Color(1.282f, 1.529f, 1.085f) },   // a touch lighter than the conifers
        };
        static readonly DressKind[] DressShrubs =
        {
            new DressKind("MD_ShrubLow", "Tree_18", 1.5f, shrub: true, luma: 0.26f, hue: 74f, sat: 0.45f)
                { tint = new Color(1.045f, 1.142f, 0.867f) },
            new DressKind("MD_ShrubRed", "Tree_17", 2.4f, shrub: true, luma: 0.26f, hue: 74f, sat: 0.42f)
                { tint = new Color(1.054f, 1.054f, 1.054f) },   // the red-flowering shrubs of the style sheet (grey tint: keeps the reds)
        };
        const string TreeAtlasPath = DressingDir + "/Textures/T_TreeAtlas.png";
        static readonly string[] FlowerColours = { "Yellow", "White", "Pink", "Purple" };

        static void BuildDressing(Vector3 spawn, Quaternion rot, StringBuilder log)
        {
            var scatter = Object.FindFirstObjectByType<FarmScatterRenderer>();
            var alpine = AssetDatabase.LoadAssetAtPath<FarmScatterData>(AlpineScatterPath);
            if (!scatter || !alpine) { log.AppendLine("dressing: no scatter renderer or Alpine scatter data"); return; }
            if (!AssetDatabase.IsValidFolder(EasyTreesDir)) { log.AppendLine("dressing: Easy Mobile trees pack not in staging; skipped"); return; }
            foreach (var f in new[] { DressingDir + "/Meshes", DressingDir + "/Materials", DressingDir + "/Textures" }) EnsureFolder(f);

            // ---- materials ----
            var uvMap = BuildTreeAtlas(DressTrees.Concat(DressShrubs).ToArray(), log);
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(TreeAtlasPath);
            string flowerAtlasPath = DressingDir + "/Textures/T_FlowerAtlas.png";
            if (WriteIfChanged(flowerAtlasPath, MakeFlowerAtlas(512))) ImportDressAtlas(flowerAtlasPath, 512);
            var treeMat = BakedMaterial("MD_TreeCards", atlas, wind: 0.05f, fade: Vector2.zero, crownLight: true);
            var shrubMat = BakedMaterial("MD_ShrubCards", atlas, wind: 0.1f, fade: Vector2.zero, crownLight: true);
            var flowerMat = BakedMaterial("MD_FlowerCards", AssetDatabase.LoadAssetAtPath<Texture2D>(flowerAtlasPath), wind: 0.25f, fade: new Vector2(38f, 52f));

            // ---- the scatter data copy: Alpine's own content, then ours ----
            var data = Object.Instantiate(alpine);
            data.name = "R01_ScatterData";
            var kinds = data.kinds.ToList();
            var cells = data.cells.ToList();
            int Kind(FarmScatterData.Kind k) { kinds.Add(k); return kinds.Count - 1; }
            DressRocks(kinds, log);   // on copies of the rock materials: the Alpine scene keeps its own

            var treeKinds = DressTrees.Select(d => (d, Kind(TreeKind(d, treeMat, uvMap[d.source])))).ToArray();
            var shrubKinds = DressShrubs.Select(d => (d, Kind(TreeKind(d, shrubMat, uvMap[d.source])))).ToArray();
            var flowerKinds = FlowerColours.Select((c, i) => Kind(FlowerKind(c, i, flowerMat))).ToArray();
            int[] AlpineKinds(params string[] prefixes) => Enumerable.Range(0, alpine.kinds.Length).Where(i => prefixes.Any(p => alpine.kinds[i].name.StartsWith(p))).ToArray();
            int[] tuftKinds = AlpineKinds("TuftWheat", "TuftGreen");
            int[] bushKinds = AlpineKinds("BushA", "BushB", "BushC");     // the environment's own bushes
            int[] firKinds = AlpineKinds("FirB", "FirC");                 // and firs, mixed into the new groups
            var shadowOf = new Dictionary<int, (float crown, bool conifer)>();
            foreach (var (dk, ki) in treeKinds) shadowOf[ki] = (dk.crown, dk.conifer);
            foreach (var ki in firKinds) shadowOf[ki] = (0.4f, true);

            // ---- placement ----
            var rng = new System.Random(2026);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector3 centre = spawn + rot * new Vector3(LayerAreaCentre.x, 0f, LayerAreaCentre.y);
            var placed = new List<(int kind, Vector3 pos, float scale, Quaternion rot)>();
            var bigThings = new List<(Vector2 pos, float radius)>();   // existing trees and rocks, then our trees
            foreach (var c in alpine.cells)
            {
                var k = alpine.kinds[c.kind];
                if (k.detail || !k.mesh) continue;
                foreach (var p in c.positionScale)
                    bigThings.Add((new Vector2(p.x, p.z), Mathf.Max(k.mesh.bounds.extents.x, k.mesh.bounds.extents.z) * p.w * 0.5f));
            }
            Vector2 Rel(Vector3 w) { Vector3 l = Quaternion.Inverse(rot) * (w - spawn); return new Vector2(l.x, l.z); }

            bool Free(Vector3 w, float clearance, bool forTree)
            {
                Vector2 r = Rel(w);
                if ((r - new Vector2(0f, 6.3f)).magnitude < 8f + clearance) return false;              // the camp
                if ((r - PenCentre).magnitude < PenHalf + 3f + clearance) return false;                // the goat pen
                if (r.magnitude < 6f) return false;                                                    // the spawn
                if ((r - WolfPath[WolfPath.Length - 1]).magnitude < 9f + clearance) return false;      // the kill site
                foreach (var p in Patches) if (Mathf.Abs(r.x - p.offset.x) < p.size.x * 0.5f + 1.5f + clearance && Mathf.Abs(r.y - p.offset.y) < p.size.y * 0.5f + 1.5f + clearance) return false;
                if (DistanceToPolyline(r, WolfPath) < (forTree ? 5f : 1.2f) + clearance) return false;   // never on the wolf's trail
                foreach (var path in HumanPaths) if (DistanceToPolyline(r, path) < 1.2f + clearance) return false;
                return true;
            }

            bool Ground(Vector3 at, out Vector3 point, out float slope)
            {
                point = at; slope = 90f;
                var hits = Physics.RaycastAll(at + Vector3.up * 200f, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
                bool any = false; float lowest = float.MaxValue;
                foreach (var h in hits)
                {
                    if (h.collider.name != "Ground" && !h.collider.name.StartsWith("Sector")) continue;
                    if (h.point.y < lowest) { lowest = h.point.y; point = h.point; slope = Vector3.Angle(h.normal, Vector3.up); any = true; }
                }
                return any;
            }

            bool Clear(Vector3 w, float radius)
            {
                var p = new Vector2(w.x, w.z);
                foreach (var b in bigThings) if ((b.pos - p).magnitude < b.radius + radius) return false;
                return true;
            }

            // trees: mixed groups on the hills and the meadow edges, plus a few lone oaks in the open
            int groups = 0, lone = 0, trees = 0;
            for (int attempt = 0; attempt < 400 && groups < 11; attempt++)
            {
                Vector3 c = centre + new Vector3(R(-115f, 115f), 0f, R(-115f, 115f));
                if (!Ground(c, out var gp, out float slope) || slope > 26f || !Free(gp, 4f, true)) continue;
                int members = rng.Next(2, 6);
                int placedHere = 0;
                for (int m = 0; m < members * 3 && placedHere < members; m++)
                {
                    Vector3 t = gp + new Vector3(R(-8f, 8f), 0f, R(-8f, 8f));
                    if (!Ground(t, out var tp, out float s) || s > 30f || !Free(tp, 2f, true) || !Clear(tp, 3.5f)) continue;
                    double pick = rng.NextDouble();
                    bool fir = pick >= 0.75 && firKinds.Length > 0;
                    int ki = fir ? firKinds[rng.Next(firKinds.Length)] : pick < 0.45 ? treeKinds[0].Item2 : treeKinds[1].Item2;
                    float scale = fir ? R(0.7f, 1.05f) : R(0.85f, 1.3f);
                    placed.Add((ki, tp, scale, Quaternion.Euler(0f, R(0f, 360f), 0f)));
                    bigThings.Add((new Vector2(tp.x, tp.z), 2.5f * scale));
                    placedHere++; trees++;
                }
                if (placedHere > 0) groups++;
            }
            for (int attempt = 0; attempt < 200 && lone < 7; attempt++)
            {
                Vector3 c = centre + new Vector3(R(-100f, 100f), 0f, R(-100f, 100f));
                if (!Ground(c, out var gp, out float slope) || slope > 18f || !Free(gp, 4f, true) || !Clear(gp, 6f)) continue;
                float scale = R(0.95f, 1.35f);
                placed.Add((treeKinds[1].Item2, gp, scale, Quaternion.Euler(0f, R(0f, 360f), 0f)));
                bigThings.Add((new Vector2(gp.x, gp.z), 3f * scale));
                lone++; trees++;
            }

            // shrubs and tall grass hug the rocks (and sit at some tree feet)
            int shrubs = 0, tufts = 0;
            foreach (var c in alpine.cells)
            {
                var k = alpine.kinds[c.kind];
                if (!(k.name.StartsWith("Rock") || k.name.StartsWith("Pile")) || !k.mesh) continue;
                foreach (var p in c.positionScale)
                {
                    Vector3 rp = new Vector3(p.x, p.y, p.z);
                    if (new Vector2(rp.x - centre.x, rp.z - centre.z).magnitude > 125f) continue;
                    float rockR = (k.mesh.bounds.extents.x + k.mesh.bounds.extents.z) * 0.5f * p.w;
                    int n = rng.Next(2, 5);
                    for (int i = 0; i < n; i++)
                    {
                        float ang = R(0f, Mathf.PI * 2f), dist = rockR * R(0.75f, 1f) + R(0.2f, 1.2f);
                        Vector3 sp = rp + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                        if (!Ground(sp, out var gp, out float s) || s > 35f || !Free(gp, 0f, false)) continue;
                        double pick = rng.NextDouble();
                        int ki = pick < 0.15 ? shrubKinds[1].Item2 : pick < 0.32 || bushKinds.Length == 0 ? shrubKinds[0].Item2 : bushKinds[rng.Next(bushKinds.Length)];
                        placed.Add((ki, gp, R(0.8f, 1.25f), Quaternion.Euler(0f, R(0f, 360f), 0f)));
                        shrubs++;
                    }
                    if (tuftKinds.Length == 0) continue;
                    int t = rng.Next(4, 8);
                    for (int i = 0; i < t; i++)
                    {
                        float ang = R(0f, Mathf.PI * 2f), dist = rockR * R(0.6f, 0.95f) + R(0f, 1.6f);
                        Vector3 sp = rp + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                        if (!Ground(sp, out var gp, out float s) || s > 35f || !Free(gp, 0f, false)) continue;
                        placed.Add((tuftKinds[rng.Next(tuftKinds.Length)], gp, R(0.9f, 1.4f), Quaternion.Euler(0f, R(0f, 360f), 0f)));
                        tufts++;
                    }
                }
            }
            foreach (var t in placed.Where(p => shadowOf.ContainsKey(p.kind)).ToArray())
            {
                if (rng.NextDouble() > 0.5) continue;
                float ang = R(0f, Mathf.PI * 2f);
                Vector3 sp = t.pos + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * R(1.5f, 3f);
                if (!Ground(sp, out var gp, out float s) || !Free(gp, 0f, false)) continue;
                int ki = bushKinds.Length > 0 && rng.NextDouble() < 0.7 ? bushKinds[rng.Next(bushKinds.Length)] : shrubKinds[0].Item2;
                placed.Add((ki, gp, R(0.8f, 1.2f), Quaternion.Euler(0f, R(0f, 360f), 0f)));
                shrubs++;
            }

            // flower drifts: loose clusters of one or two colours on the gentle grass
            int flowers = 0;
            for (int d = 0; d < 70; d++)
            {
                Vector3 dc = centre + new Vector3(R(-105f, 105f), 0f, R(-105f, 105f));
                if (!Ground(dc, out var dp, out float ds) || ds > 22f || !Free(dp, 1f, false)) continue;
                int colourA = rng.Next(FlowerColours.Length), colourB = rng.Next(FlowerColours.Length);
                int count = rng.Next(25, 60);
                float radius = R(2.5f, 5.5f);
                for (int i = 0; i < count; i++)
                {
                    // gaussian-ish spread: thick in the middle, thinning out
                    float rr = radius * Mathf.Sqrt(-2f * Mathf.Log(Mathf.Max(1e-4f, (float)rng.NextDouble()))) * 0.5f;
                    float ang = R(0f, Mathf.PI * 2f);
                    Vector3 fp = dp + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rr;
                    if (!Ground(fp, out var gp, out float s) || s > 28f || !Free(gp, 0f, false)) continue;
                    int colour = rng.NextDouble() < 0.75 ? colourA : colourB;
                    placed.Add((flowerKinds[colour], gp, R(0.7f, 1.2f), Quaternion.Euler(0f, R(0f, 360f), 0f)));
                    flowers++;
                }
            }

            // ---- cells: grouped by kind and by 40 m tiles (the renderer culls per cell) ----
            foreach (var group in placed.GroupBy(p => (p.kind, Mathf.FloorToInt(p.pos.x / 40f), Mathf.FloorToInt(p.pos.z / 40f))))
            {
                var items = group.ToArray();
                for (int start = 0; start < items.Length; start += 500)
                {
                    var chunk = items.Skip(start).Take(500).ToArray();
                    var mesh = kinds[group.Key.kind].mesh;
                    var b = new Bounds(chunk[0].pos, Vector3.zero);
                    foreach (var it in chunk)
                    {
                        var mb = mesh.bounds;
                        b.Encapsulate(new Bounds(it.pos + mb.center * it.scale, mb.size * it.scale * 1.42f));
                    }
                    cells.Add(new FarmScatterData.Cell
                    {
                        kind = group.Key.kind,
                        bounds = b,
                        positionScale = chunk.Select(it => new Vector4(it.pos.x, it.pos.y, it.pos.z, it.scale)).ToArray(),
                        rotation = chunk.Select(it => it.rot).ToArray(),
                    });
                }
            }
            data.kinds = kinds.ToArray();
            data.cells = cells.ToArray();
            SaveInPlace(data, R01ScatterPath);
            var saved = AssetDatabase.LoadAssetAtPath<FarmScatterData>(R01ScatterPath);
            scatter.data = saved;
            EditorUtility.SetDirty(scatter);
            scatter.Build();

            // ---- ground shadows of the new trees ----
            StampTreeShadows(placed.Where(p => shadowOf.ContainsKey(p.kind)).Select(p =>
            {
                var (crown, conifer) = shadowOf[p.kind];
                var mb = kinds[p.kind].mesh.bounds;
                return (p.pos, mb.size.y * p.scale, Mathf.Max(mb.extents.x, mb.extents.z) * p.scale * crown / 0.5f, conifer);
            }).ToArray(), log);

            EnsureDressingCoverRules(log);
            log.AppendLine($"dressing: {trees} trees ({groups} groups, {lone} lone oaks), {shrubs} shrubs, {tufts} tall grass tufts, {flowers} flowers");
        }

        static float DistanceToPolyline(Vector2 p, Vector2[] line)
        {
            float best = float.MaxValue;
            for (int i = 1; i < line.Length; i++)
            {
                Vector2 a = line[i - 1], ab = line[i] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return best;
        }

        // ---------- kinds and meshes ----------

        static MeshFilter[] TreeLodFilters(GameObject prefab)
        {
            var lodGroup = prefab.GetComponent<LODGroup>();
            var lods = lodGroup ? lodGroup.GetLODs() : null;
            var all = prefab.GetComponentsInChildren<MeshFilter>();
            MeshFilter Filter(int level) => lods != null && lods.Length > level && lods[level].renderers.Length > 0
                ? lods[level].renderers[0].GetComponent<MeshFilter>() : all[Mathf.Min(level, all.Length - 1)];
            return new[] { Filter(0), Filter(1) };
        }

        static FarmScatterData.Kind TreeKind(DressKind d, Material mat, (Rect src, Rect dst) uvMap)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EasyTreesDir}/Trees_Prefabs/{d.source}.prefab");
            var filters = TreeLodFilters(prefab);
            var near = BakeCardMesh(prefab, filters[0], d, uvMap, $"{d.name}_LOD0");
            var far = BakeCardMesh(prefab, filters[1], d, uvMap, $"{d.name}_LOD1");
            bool tree = !d.shrub;
            return new FarmScatterData.Kind
            {
                name = d.name,
                mesh = near,
                materials = new[] { mat },
                // brightness and colour are graded into the atlas; the tints only vary one plant from the next
                tintA = d.tint * 0.93f,
                tintB = d.tint * 1.06f,
                cullDistance = tree ? 0f : 170f,
                detail = d.shrub,
                lodMesh = far != near ? far : null,
                lodMaterials = new[] { mat },
                lodDistance = tree ? 70f : 0f,
            };
        }

        // One prefab mesh in the prefab's own space, scaled to the wanted height, base at y = 0, wind weight in
        // vertex alpha (0 at the ground, 1 at the top), UVs moved into our tree atlas, and the crown-local position
        // in UV1 (centre of the crown = 0, its edge = 1) for SV_Baked's _CROWNLIGHT sun shading. Saved in place so
        // the asset keeps its GUID on rebuilds.
        static Mesh BakeCardMesh(GameObject prefab, MeshFilter mf, DressKind d, (Rect src, Rect dst) uvMap, string name)
        {
            float height = d.height;
            var src = mf.sharedMesh;
            Matrix4x4 m = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var verts = src.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray();
            var normals = src.normals.Length == verts.Length ? src.normals.Select(n => m.MultiplyVector(n).normalized).ToArray() : null;
            float minY = verts.Min(v => v.y), maxY = verts.Max(v => v.y);
            float s = height / Mathf.Max(0.01f, maxY - minY);
            for (int i = 0; i < verts.Length; i++) verts[i] = new Vector3(verts[i].x * s, (verts[i].y - minY) * s, verts[i].z * s);
            var colours = verts.Select(v => new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(v.y / height), 1.5f))).ToArray();
            var uvs = src.uv.Select(u => new Vector2(
                uvMap.dst.xMin + (u.x - uvMap.src.xMin) / uvMap.src.width * uvMap.dst.width,
                uvMap.dst.yMin + (u.y - uvMap.src.yMin) / uvMap.src.height * uvMap.dst.height)).ToArray();
            float halfW = Mathf.Max(0.1f, verts.Max(v => Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.z))) * 0.9f);
            float cy = height * (d.shrub ? 0.5f : 0.6f), halfH = height * (d.shrub ? 0.5f : 0.42f);
            var crown = verts.Select(v => new Vector3(v.x / halfW, (v.y - cy) / halfH, v.z / halfW)).ToList();

            string path = $"{DressingDir}/Meshes/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = !mesh;
            if (created) mesh = new Mesh { name = name };
            mesh.Clear();
            mesh.vertices = verts;
            if (normals != null) mesh.normals = normals;
            mesh.uv = uvs;
            mesh.SetUVs(1, crown);
            mesh.colors = colours;
            mesh.subMeshCount = 1;
            mesh.SetTriangles(src.triangles, 0);
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // Two crossed cards showing one cell of the flower atlas, about 40 cm tall.
        static FarmScatterData.Kind FlowerKind(string colour, int cell, Material mat)
        {
            string name = "MD_Flower" + colour;
            string path = $"{DressingDir}/Meshes/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = !mesh;
            if (created) mesh = new Mesh { name = name };
            mesh.Clear();
            float w = 0.7f, h = 0.45f;
            Vector2 uv0 = new Vector2((cell % 2) * 0.5f, (cell / 2) * 0.5f);
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var normals = new List<Vector3>(); var tris = new List<int>();
            foreach (float yaw in new[] { 0f, 90f })
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                int b = verts.Count;
                verts.Add(q * new Vector3(-w / 2, 0, 0)); verts.Add(q * new Vector3(w / 2, 0, 0)); verts.Add(q * new Vector3(w / 2, h, 0)); verts.Add(q * new Vector3(-w / 2, h, 0));
                uvs.Add(uv0); uvs.Add(uv0 + new Vector2(0.5f, 0)); uvs.Add(uv0 + new Vector2(0.5f, 0.5f)); uvs.Add(uv0 + new Vector2(0, 0.5f));
                for (int i = 0; i < 4; i++) normals.Add(q * Vector3.forward);
                tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetNormals(normals);
            mesh.SetColors(verts.Select(v => new Color(1f, 1f, 1f, Mathf.Clamp01(v.y / h))).ToList());
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            return new FarmScatterData.Kind
            {
                name = name, mesh = mesh, materials = new[] { mat },
                tintA = new Color(0.92f, 0.92f, 0.92f), tintB = new Color(1.05f, 1.05f, 1.05f),
                cullDistance = 60f, detail = true,
            };
        }

        static Material BakedMaterial(string name, Texture2D tex, float wind, Vector2 fade, bool crownLight = false)
        {
            string path = $"{DressingDir}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("SavannaEnv/Baked")) { name = name }; AssetDatabase.CreateAsset(mat, path); }
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_CUTOUT");
            mat.SetFloat("_Cull", 0f);                    // cards are seen from both sides
            mat.SetFloat("_EdgeFadeOn", 1f); mat.EnableKeyword("_EDGEFADE");
            mat.SetFloat("_ObjectShadow", 0.7f);          // darker when it stands in another tree's shadow
            mat.SetFloat("_WindStrength", wind);
            mat.SetFloat("_FadeStart", fade.x); mat.SetFloat("_FadeEnd", fade.y);
            mat.SetFloat("_CrownLightOn", crownLight ? 1f : 0f);
            if (crownLight)
            {
                // photo cards carry no light of their own: shade each crown like a ball lit by the day preset's sun,
                // as the firs' baked textures are (lit side, shaded side, darker inside and underneath)
                var day = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Game/Art/Environment/AlpineEnv/Lighting/AL_Day.asset");
                Vector3 euler = day ? new SerializedObject(day).FindProperty("lightRotation").vector3Value : new Vector3(52f, 238f, 0f);
                Vector3 toSun = -(Quaternion.Euler(euler) * Vector3.forward);
                mat.SetVector("_CrownSunDir", toSun.normalized);
                mat.SetFloat("_CrownDark", 0.68f); mat.SetFloat("_CrownLit", 1.08f);   // a gentle lit and shaded side
                mat.SetFloat("_CrownCore", 0.8f); mat.SetFloat("_CrownBase", 0.65f);
                mat.EnableKeyword("_CROWNLIGHT");
            }
            else mat.DisableKeyword("_CROWNLIGHT");
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Our own tree atlas, cut from the pack's 4096 px "Nature Atlas": only the plants we use, at their full
        // resolution (the whole pack capped to 1024 left them blurred next to the firs' 2048 px textures). Each
        // plant is cleaned (sky seen through the leaves becomes holes; the oak's strip of grass is cut away),
        // then graded toward the environment's painted firs: brightness, contrast, hue and saturation of the
        // greens (reds, like the shrub's flowers, keep their colour). Colour bleeds into the holes so mipmaps
        // draw no pale halo. Returns, per prefab, the UV rect in the pack's atlas and the one in ours.
        static Dictionary<string, (Rect src, Rect dst)> BuildTreeAtlas(DressKind[] plants, StringBuilder log)
        {
            const int Size = 2048, Pad = 4;
            var srcTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            srcTex.LoadImage(File.ReadAllBytes(EasyTreesDir + "/Nature Atlas.png"));
            int sw = srcTex.width, sh = srcTex.height;
            var spx = srcTex.GetPixels();

            // each plant's pixel rect in the pack's atlas (both LOD meshes)
            var rects = new List<(DressKind d, RectInt px)>();
            foreach (var d in plants)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EasyTreesDir}/Trees_Prefabs/{d.source}.prefab");
                var uvs = TreeLodFilters(prefab).SelectMany(f => f.sharedMesh.uv).ToArray();
                int x0 = Mathf.Max(0, Mathf.FloorToInt(uvs.Min(u => u.x) * sw) - Pad), y0 = Mathf.Max(0, Mathf.FloorToInt(uvs.Min(u => u.y) * sh) - Pad);
                int x1 = Mathf.Min(sw, Mathf.CeilToInt(uvs.Max(u => u.x) * sw) + Pad), y1 = Mathf.Min(sh, Mathf.CeilToInt(uvs.Max(u => u.y) * sh) + Pad);
                rects.Add((d, new RectInt(x0, y0, x1 - x0, y1 - y0)));
            }

            // shelf packing, tallest first; shrink all plants together until they fit
            float scale = 1f;
            var placedRects = new Dictionary<string, RectInt>();
            for (int tries = 0; tries < 30; tries++, scale *= 0.92f)
            {
                placedRects.Clear();
                int x = 0, y = 0, shelf = 0; bool fits = true;
                foreach (var (d, px) in rects.OrderByDescending(r => r.px.height))
                {
                    int w = Mathf.CeilToInt(px.width * scale), h = Mathf.CeilToInt(px.height * scale);
                    if (x + w > Size) { x = 0; y += shelf; shelf = 0; }
                    if (y + h > Size || w > Size) { fits = false; break; }
                    placedRects[d.source] = new RectInt(x, y, w, h);
                    x += w; shelf = Mathf.Max(shelf, h);
                }
                if (fits) break;
            }

            var px2 = new Color[Size * Size];
            var map = new Dictionary<string, (Rect src, Rect dst)>();
            foreach (var (d, sr) in rects)
            {
                var dr = placedRects[d.source];
                for (int y = 0; y < dr.height; y++)
                for (int x = 0; x < dr.width; x++)
                {
                    Color c = scale >= 0.999f
                        ? spx[Mathf.Min(sh - 1, sr.y + y) * sw + Mathf.Min(sw - 1, sr.x + x)]
                        : srcTex.GetPixelBilinear((sr.x + (x + 0.5f) / scale) / sw, (sr.y + (y + 0.5f) / scale) / sh);
                    Color.RGBToHSV(c, out float hue, out float sat, out float val);
                    if (c.a > 0.3f && val > 0.72f && sat < 0.22f) c.a = 0f;   // sky holes
                    px2[(dr.y + y) * Size + dr.x + x] = c;
                }
                if (d.trimBase) TrimGrassStrip(px2, Size, dr);
                GradePlant(px2, Size, dr, d);
                // the source rect in pack pixels maps onto the destination rect (shrunk by scale)
                map[d.source] = (new Rect((float)sr.x / sw, (float)sr.y / sh, (float)sr.width / sw, (float)sr.height / sh),
                                 new Rect((float)dr.x / Size, (float)dr.y / Size, sr.width * scale / Size, sr.height * scale / Size));
            }
            Object.DestroyImmediate(srcTex);
            Bleed(px2, Size, Size, 12);
            if (WriteIfChanged(TreeAtlasPath, PngRect(px2, Size, Size)) || AssetImporter.GetAtPath(TreeAtlasPath) == null) ImportDressAtlas(TreeAtlasPath, Size);
            log.AppendLine($"dressing: tree atlas {Size} px, {plants.Length} plants at {scale * 100f:F0}% of the pack's resolution");
            return map;
        }

        // The oak photo stands on a strip of grass: below the trunk, keep only the trunk's own columns.
        static void TrimGrassStrip(Color[] px, int size, RectInt r)
        {
            int probe = r.y + Mathf.RoundToInt(r.height * 0.12f);
            int mid = r.x + r.width / 2, left = -1, right = -1;
            for (int x = r.x; x < r.xMax; x++)
            {
                if (px[probe * size + x].a < 0.5f) continue;
                // the opaque run closest to the middle is the trunk
                int a = x; while (x < r.xMax && px[probe * size + x].a >= 0.5f) x++;
                if (left < 0 || Mathf.Abs((a + x) / 2 - mid) < Mathf.Abs((left + right) / 2 - mid)) { left = a; right = x; }
            }
            if (left < 0 || right - left > r.width / 4) return;   // no clear trunk: leave it
            int top = r.y + Mathf.RoundToInt(r.height * 0.09f);
            for (int y = r.y; y < top; y++)
            for (int x = r.x; x < r.xMax; x++)
                if (x < left - 3 || x > right + 3) px[y * size + x].a = 0f;
        }

        static void GradePlant(Color[] px, int size, RectInt r, DressKind d)
        {
            float GreenWeight(float h) { float deg = h * 360f; return Mathf.Clamp01(1f - Mathf.Max(40f - deg, deg - 160f) / 20f); }
            // 1. hue and saturation of the greens
            double hueSum = 0, satSum = 0, wSum = 0;
            for (int y = r.y; y < r.yMax; y++)
            for (int x = r.x; x < r.xMax; x++)
            {
                var c = px[y * size + x]; if (c.a < 0.5f) continue;
                Color.RGBToHSV(c, out float h, out float s, out float v);
                float w = GreenWeight(h); hueSum += h * 360f * w; satSum += s * w; wSum += w;
            }
            if (wSum < 1) return;
            float hueShift = d.hue - (float)(hueSum / wSum), satRatio = d.sat / Mathf.Max(0.05f, (float)(satSum / wSum));
            for (int y = r.y; y < r.yMax; y++)
            for (int x = r.x; x < r.xMax; x++)
            {
                int i = y * size + x; var c = px[i];
                Color.RGBToHSV(c, out float h, out float s, out float v);
                float w = GreenWeight(h);
                var g = Color.HSVToRGB(Mathf.Repeat(h + hueShift / 360f * w, 1f), Mathf.Clamp01(Mathf.Lerp(s, s * satRatio, w)), v);
                px[i] = new Color(g.r, g.g, g.b, c.a);
            }
            // 2. brightness and contrast: the firs are darker, with deep shade between lit tips
            double sum = 0, sum2 = 0; int n = 0;
            for (int y = r.y; y < r.yMax; y++)
            for (int x = r.x; x < r.xMax; x++)
            {
                var c = px[y * size + x]; if (c.a < 0.5f) continue;
                float l = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b; sum += l; sum2 += l * l; n++;
            }
            if (n == 0) return;
            float mean = (float)(sum / n), sd = Mathf.Sqrt(Mathf.Max(0f, (float)(sum2 / n) - mean * mean));
            float gamma = Mathf.Clamp(0.5f / Mathf.Max(0.05f, sd / mean), 0.7f, 1.25f);   // flatter: the painted firs are smooth on screen
            double powSum = 0;
            for (int y = r.y; y < r.yMax; y++)
            for (int x = r.x; x < r.xMax; x++)
            {
                var c = px[y * size + x]; if (c.a < 0.5f) continue;
                powSum += Mathf.Pow((0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / mean, gamma);
            }
            float k = d.luma / Mathf.Max(1e-4f, (float)(powSum / n));
            for (int y = r.y; y < r.yMax; y++)
            for (int x = r.x; x < r.xMax; x++)
            {
                int i = y * size + x; var c = px[i];
                float l = Mathf.Max(1e-4f, 0.3f * c.r + 0.59f * c.g + 0.11f * c.b);
                float f = k * Mathf.Pow(l / mean, gamma) / l;
                px[i] = new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), c.a);
            }
        }

        // each transparent pixel takes the mean colour of its solid neighbours, ring by ring
        static void Bleed(Color[] px, int w, int h, int passes)
        {
            var solid = px.Select(c => c.a >= 0.5f).ToArray();
            for (int pass = 0; pass < passes; pass++)
            {
                var next = (bool[])solid.Clone();
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (solid[i]) continue;
                    float r = 0, g = 0, b = 0; int n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        int j = yy * w + xx;
                        if (!solid[j]) continue;
                        r += px[j].r; g += px[j].g; b += px[j].b; n++;
                    }
                    if (n == 0) continue;
                    px[i] = new Color(r / n, g / n, b / n, px[i].a);
                    next[i] = true;
                }
                solid = next;
            }
        }

        static void ImportDressAtlas(string path, int maxSize)
        {
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.crunchedCompression = false;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.maxTextureSize = maxSize;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            { name = "Android", overridden = true, maxTextureSize = maxSize, format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50 });
            ti.SaveAndReimport();
        }

        static void SaveInPlace(FarmScatterData data, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<FarmScatterData>(path);
            if (existing) { EditorUtility.CopySerialized(data, existing); existing.name = Path.GetFileNameWithoutExtension(path); EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
        }

        // ---------- rocks: lichen and moss ----------

        static void DressRocks(List<FarmScatterData.Kind> kinds, StringBuilder log)
        {
            var detail = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GroundArtDir}/Textures/PH_mossy_rock_diff_1k.jpg");
            if (!detail) { log.AppendLine("dressing: no mossy rock photo; rocks left as they are"); return; }
            var copies = new Dictionary<Material, Material>();
            Material Dressed(Material src)
            {
                if (!src) return src;
                if (copies.TryGetValue(src, out var done)) return done;
                string path = $"{DressingDir}/Materials/R01_{src.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat)
                {
                    // starting values, only once: tune on the copied materials afterwards
                    mat = new Material(src) { name = "R01_" + src.name };
                    mat.SetFloat("_DetailScale", 0.33f);
                    mat.SetFloat("_DetailStrength", 0.9f);
                    mat.SetFloat("_DetailMean", 0.33f);
                    mat.SetColor("_MossColor", new Color(0.42f, 0.5f, 0.18f));
                    mat.SetFloat("_MossAmount", 0.45f);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.SetTexture("_MainTex", src.GetTexture("_MainTex"));   // follow the original's baked light
                mat.SetTexture("_DetailTex", detail);
                mat.SetFloat("_RockDetailOn", 1f);
                mat.EnableKeyword("_ROCKDETAIL");
                EditorUtility.SetDirty(mat);
                return copies[src] = mat;
            }
            foreach (var k in kinds)
            {
                if (!(k.name.StartsWith("Rock") || k.name.StartsWith("Pile"))) continue;
                k.materials = k.materials.Select(Dressed).ToArray();
                if (k.lodMaterials != null) k.lodMaterials = k.lodMaterials.Select(Dressed).ToArray();
            }
            log.AppendLine($"dressing: lichen and moss on {copies.Count} rock materials (copies)");
        }

        // ---------- flowers ----------

        // Four cells (2 x 2): yellow buttercups, white daisies, pink spikes, purple bells, each a small clump on stems.
        static byte[] MakeFlowerAtlas(int n)
        {
            var px = new Color[n * n];
            var rng = new System.Random(7);
            int half = n / 2;
            Color[] petal = { new Color(0.98f, 0.82f, 0.12f), new Color(0.97f, 0.96f, 0.92f), new Color(0.92f, 0.45f, 0.68f), new Color(0.58f, 0.42f, 0.86f) };
            for (int cell = 0; cell < 4; cell++)
            {
                int ox = (cell % 2) * half, oy = (cell / 2) * half;
                void Disc(float cx, float cy, float r, Color c)
                {
                    for (int y = Mathf.Max(0, (int)(cy - r - 1)); y <= Mathf.Min(half - 1, (int)(cy + r + 1)); y++)
                    for (int x = Mathf.Max(0, (int)(cx - r - 1)); x <= Mathf.Min(half - 1, (int)(cx + r + 1)); x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        float a = Mathf.Clamp01(r - d + 0.5f);
                        if (a <= 0f) continue;
                        int i = (oy + y) * n + ox + x;
                        px[i] = Color.Lerp(px[i], new Color(c.r, c.g, c.b, 1f), a);
                        px[i].a = Mathf.Max(px[i].a, a);
                    }
                }
                int stems = 18;
                for (int s = 0; s < stems; s++)
                {
                    float bx = half * (0.15f + 0.7f * (float)rng.NextDouble());
                    float top = half * (0.45f + 0.45f * (float)rng.NextDouble());
                    float lean = ((float)rng.NextDouble() - 0.5f) * half * 0.15f;
                    // stem
                    for (float t = 0f; t <= 1f; t += 0.01f)
                        Disc(bx + lean * t, t * top, 1.3f, new Color(0.25f, 0.42f, 0.12f));
                    // a leaf or two near the bottom
                    Disc(bx + 4f, top * 0.18f, 3f, new Color(0.3f, 0.5f, 0.15f));
                    float hx = bx + lean, hy = top;
                    Color pc = petal[cell] * (0.88f + 0.12f * (float)rng.NextDouble());
                    switch (cell)
                    {
                        case 0:   // buttercup: five petals around a darker centre
                        case 1:   // daisy: white petals, yellow centre
                            for (int p = 0; p < 6; p++)
                            {
                                float a = p * Mathf.PI * 2f / 6f;
                                Disc(hx + Mathf.Cos(a) * 6f, hy + Mathf.Sin(a) * 6f, 4.2f, pc);
                            }
                            Disc(hx, hy, 3.4f, cell == 1 ? new Color(0.98f, 0.78f, 0.1f) : new Color(0.85f, 0.6f, 0.05f));
                            break;
                        case 2:   // spike: small blossoms stacked up the top of the stem
                            for (int b = 0; b < 7; b++) Disc(hx + (b % 2 == 0 ? -2.5f : 2.5f), hy - b * 5f, 3.6f, pc);
                            break;
                        default:  // bells: a few drooping heads
                            for (int b = 0; b < 4; b++) Disc(hx + (b - 1.5f) * 5f, hy - Mathf.Abs(b - 1.5f) * 4f, 4f, pc);
                            break;
                    }
                }
            }
            return Png(px, n);
        }

        // ---------- ground shadows of the new trees ----------

        static void StampTreeShadows((Vector3 pos, float height, float crown, bool conifer)[] trees, StringBuilder log)
        {
            var env = Object.FindFirstObjectByType<FarmEnvironment>();
            if (!env || !File.Exists(AlpineShadowPath)) return;
            var so = new SerializedObject(env);
            Vector4 area = so.FindProperty("shadowMapArea").vector4Value;
            var map = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            map.LoadImage(File.ReadAllBytes(AlpineShadowPath));
            int w = map.width, h = map.height;
            var px = map.GetPixels();

            // where the sun's shadows fall: the day preset's sun direction, and how far a height throws a shadow
            var day = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Game/Art/Environment/AlpineEnv/Lighting/AL_Day.asset");
            Vector3 euler = day ? new SerializedObject(day).FindProperty("lightRotation").vector3Value : new Vector3(52f, 238f, 0f);
            Vector3 light = Quaternion.Euler(euler) * Vector3.forward;
            Vector2 dir = new Vector2(light.x, light.z).normalized;
            float throwPerMetre = 1f / Mathf.Tan(euler.x * Mathf.Deg2Rad);

            foreach (var t in trees)
            {
                // the crown as a stack of discs from a third of the height to the top, each thrown along the sun
                for (int s = 0; s <= 10; s++)
                {
                    float f = Mathf.Lerp(0.3f, 1f, s / 10f);
                    float radius = t.conifer ? t.crown * Mathf.Lerp(1f, 0.15f, (f - 0.3f) / 0.7f)
                                             : t.crown * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((f - 0.65f) / 0.35f, 2f)));
                    if (radius < 0.3f) continue;
                    Vector2 c = new Vector2(t.pos.x, t.pos.z) + dir * (t.height * f * throwPerMetre);
                    Stamp(px, w, h, area, c, radius, 0.82f, 0);
                }
                Stamp(px, w, h, area, new Vector2(t.pos.x, t.pos.z), Mathf.Max(1.2f, t.crown * 0.5f), 0.45f, 1);   // contact darkening at the trunk
            }
            map.SetPixels(px);
            map.Apply();
            bool changed = WriteIfChanged(R01ShadowPath, map.EncodeToPNG());
            Object.DestroyImmediate(map);
            if (changed)
            {
                AssetDatabase.ImportAsset(R01ShadowPath);
                var src = (TextureImporter)AssetImporter.GetAtPath(AlpineShadowPath);
                var dst = (TextureImporter)AssetImporter.GetAtPath(R01ShadowPath);
                var settings = new TextureImporterSettings();
                src.ReadTextureSettings(settings);
                dst.SetTextureSettings(settings);
                dst.SetPlatformTextureSettings(src.GetPlatformTextureSettings("Android"));
                dst.SaveAndReimport();
            }
            so.FindProperty("groundShadowMap").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(R01ShadowPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            env.Apply();   // push the new map to the shaders now
            log.AppendLine($"dressing: ground shadows stamped for {trees.Length} trees");
        }

        // darken one channel inside a soft disc (R = sun shadow, G = contact darkening)
        static void Stamp(Color[] px, int w, int h, Vector4 area, Vector2 centre, float radius, float strength, int channel)
        {
            float u = (centre.x - area.x) / area.z * w, v = (centre.y - area.y) / area.w * h;
            float rp = radius / area.z * w;
            for (int y = Mathf.Max(0, (int)(v - rp - 2)); y <= Mathf.Min(h - 1, (int)(v + rp + 2)); y++)
            for (int x = Mathf.Max(0, (int)(u - rp - 2)); x <= Mathf.Min(w - 1, (int)(u + rp + 2)); x++)
            {
                float d = Mathf.Sqrt((x - u) * (x - u) + (y - v) * (y - v)) / Mathf.Max(0.5f, rp);
                float cover = strength * (1f - SmoothEdge(0.55f, 1f, d));
                if (cover <= 0f) continue;
                int i = y * w + x;
                var c = px[i];
                c[channel] = Mathf.Min(c[channel], 1f - cover);
                px[i] = c;
            }
        }

        // ---------- cover rules for the new kinds ----------

        static void EnsureDressingCoverRules(StringBuilder log)
        {
            var rules = AssetDatabase.LoadAssetAtPath<ScatterCoverRules>(CoverRulesPath);
            if (!rules) return;
            bool changed = false;
            void Add(string prefix, CoverKind cover, CoverShape shape, float size, float visibility)
            {
                if (rules.rules.Any(r => r.kindPrefix == prefix)) return;
                rules.rules.Insert(0, new ScatterCoverRules.Rule { kindPrefix = prefix, cover = cover, shape = shape, sizeFactor = size, visibility = visibility });
                changed = true;
            }
            Add("MD_Tree", CoverKind.Solid, CoverShape.Trunk, 0.028f, 1f);       // trunks stop bullets and sight, like the firs
            Add("MD_Shrub", CoverKind.Concealing, CoverShape.Sphere, 0.8f, 0.35f); // shrubs hide, like the bushes
            if (changed) { EditorUtility.SetDirty(rules); log.AppendLine("cover rules: new trees and shrubs"); }
        }
    }
}
