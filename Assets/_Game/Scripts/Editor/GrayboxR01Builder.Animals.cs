using System.Text;
using Game.Animals;
using Game.Core.Wind;
using Game.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Animals and wind part of the graybox builder: data, placeholder wolf, navigation mesh, wind HUD.</summary>
    public static partial class GrayboxR01Builder
    {
        const string AnimalsDataDir = Data + "/Animals";
        const string WorldDataDir = Data + "/World";
        const string WolfPrefabPath = "Assets/_Game/Prefabs/Animals/Wolf_Placeholder.prefab";
        const string NavMeshPath = "Assets/_Game/Scenes/Graybox/R01_Meadow_Graybox_NavMesh.asset";

        public sealed class AnimalAssets
        {
            public WindSettings wind;
            public AnimalBehaviourSettings wolf;
            public GameObject wolfPrefab;
        }

        static AnimalAssets EnsureAnimalAssets(StringBuilder log)
        {
            foreach (var f in new[] { AnimalsDataDir, WorldDataDir, "Assets/_Game/Prefabs/Animals" }) EnsureFolder(f);
            var a = new AnimalAssets
            {
                wind = EnsureAsset<WindSettings>(WorldDataDir + "/WindSettings_R01.asset", log),
                wolf = EnsureAsset<AnimalBehaviourSettings>(AnimalsDataDir + "/Behaviour_Wolf.asset", log),
            };
            var mat = LitMaterial("GB_WolfPlaceholder", new Color(0.42f, 0.40f, 0.37f), log);
            a.wolfPrefab = BuildWolfPrefab(a.wolf, mat, log);
            return a;
        }

        // Grey stand-in at wolf size (about 0.8 m at the shoulder, 1.4 m long) until the real wolf is through the pipeline.
        static GameObject BuildWolfPrefab(AnimalBehaviourSettings behaviour, Material mat, StringBuilder log)
        {
            var root = new GameObject("Wolf_Placeholder");
            try
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.speed = behaviour.walkSpeed;
                agent.radius = 0.35f;
                agent.height = 0.9f;
                agent.angularSpeed = 240f;
                agent.acceleration = 8f;
                agent.stoppingDistance = 0.2f;
                agent.autoBraking = true;

                var senses = root.AddComponent<AnimalSenses>();
                SetRefs(senses, ("settings", behaviour));
                var brain = root.AddComponent<AnimalBrain>();
                SetRefs(brain, ("settings", behaviour), ("senses", senses));

                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                Primitive(PrimitiveType.Capsule, visual, "Body", new Vector3(0f, 0.58f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.36f, 0.5f, 0.36f), mat);
                Primitive(PrimitiveType.Sphere, visual, "Head", new Vector3(0f, 0.78f, 0.6f), Quaternion.identity, new Vector3(0.26f, 0.26f, 0.3f), mat);
                Primitive(PrimitiveType.Cube, visual, "Snout", new Vector3(0f, 0.74f, 0.8f), Quaternion.identity, new Vector3(0.1f, 0.1f, 0.18f), mat);
                Primitive(PrimitiveType.Cube, visual, "EarL", new Vector3(-0.07f, 0.94f, 0.56f), Quaternion.identity, new Vector3(0.05f, 0.1f, 0.03f), mat);
                Primitive(PrimitiveType.Cube, visual, "EarR", new Vector3(0.07f, 0.94f, 0.56f), Quaternion.identity, new Vector3(0.05f, 0.1f, 0.03f), mat);
                Primitive(PrimitiveType.Cylinder, visual, "Tail", new Vector3(0f, 0.55f, -0.62f), Quaternion.Euler(-50f, 0f, 0f), new Vector3(0.07f, 0.22f, 0.07f), mat);
                foreach (var (x, z) in new[] { (-0.12f, 0.35f), (0.12f, 0.35f), (-0.12f, -0.35f), (0.12f, -0.35f) })
                    Primitive(PrimitiveType.Cylinder, visual, "Leg", new Vector3(x, 0.2f, z), Quaternion.identity, new Vector3(0.07f, 0.2f, 0.07f), mat);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, WolfPrefabPath);
                log.AppendLine("saved " + WolfPrefabPath);
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void AddWindHud(RectTransform safe, Font font, Color parchment, Color forest)
        {
            Color amber = new Color32(0xE9, 0xA2, 0x3B, 0xFF);
            var panel = NewRect("Wind", safe);
            Anchor(panel, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(340f, 84f));
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(forest.r, forest.g, forest.b, 0.75f); bg.raycastTarget = false;
            var arrowRect = NewRect("Arrow", panel);
            Anchor(arrowRect, new Vector2(0f, 0.5f), new Vector2(52f, 0f), new Vector2(44f, 58f));
            arrowRect.gameObject.AddComponent<CanvasRenderer>();
            var arrow = arrowRect.gameObject.AddComponent<ArrowGraphic>();
            arrow.color = amber; arrow.raycastTarget = false;
            var label = NewText("Label", panel, font, "Wind", 28, parchment, TextAnchor.MiddleLeft);
            Fill(label.rectTransform, new Vector2(96f, 0f), new Vector2(-12f, 0f));
            SetRefs(panel.gameObject.AddComponent<WindIndicatorView>(), ("arrow", arrowRect), ("label", label));
        }

        static void BuildAnimalScene(Transform root, Vector3 spawn, Quaternion rot, AnimalAssets a, StringBuilder log)
        {
            Vector3 W(Vector2 rel) => GroundPoint(spawn + rot * new Vector3(rel.x, 0f, rel.y));

            var windGo = new GameObject("WindSystem");
            windGo.transform.SetParent(root, false);
            SetRefs(windGo.AddComponent<WindSystem>(), ("settings", a.wind));

            // Navigation mesh over the hunting area (ground collider only; scattered nature has no colliders yet).
            var navGo = new GameObject("NavMesh");
            navGo.transform.SetParent(root, false);
            navGo.transform.position = spawn + rot * new Vector3(-12f, 0f, 14f);
            var surface = navGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.size = new Vector3(200f, 80f, 200f);
            surface.center = Vector3.zero;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << ConcealmentLayer);   // bushes are walk-through cover, not obstacles
            surface.BuildNavMesh();
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath)) AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            log.AppendLine("baked " + NavMeshPath);

            // The wolf feeds at the end of its trail (the kill site).
            Vector3 kill = W(WolfPath[WolfPath.Length - 1]);
            Vector3 pos = W(WolfPath[WolfPath.Length - 1] + new Vector2(1.8f, 1.2f));
            if (NavMesh.SamplePosition(pos, out var hit, 5f, NavMesh.AllAreas)) pos = hit.position;
            var wolf = (GameObject)PrefabUtility.InstantiatePrefab(a.wolfPrefab, root.gameObject.scene);
            wolf.transform.SetParent(root, true);
            Vector3 face = kill - pos; face.y = 0f;
            wolf.transform.SetPositionAndRotation(pos, face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face) : Quaternion.identity);
            log.AppendLine($"wolf placed at the kill site {pos}");
        }
    }
}
