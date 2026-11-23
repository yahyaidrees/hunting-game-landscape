using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Animals
{
    /// <summary>
    /// Leaves blood drops on the ground while a wounded animal moves (plan section 6: "a poor shot sends the animal
    /// running with a blood trail you must follow"). Kept light per the plan's tone; a settings switch can hide it.
    /// </summary>
    public sealed class BloodTrailEmitter : MonoBehaviour
    {
        [SerializeField] AnimalHealth health;
        [SerializeField] Material dropMaterial;
        [Tooltip("Metres between drops.")]
        [SerializeField] float spacing = 1.4f;
        [SerializeField] Vector2 dropSize = new Vector2(0.28f, 0.28f);
        [SerializeField] int poolSize = 160;

        /// <summary>Global switch for the plan's "blood off" option.</summary>
        public static bool ShowBlood = true;

        DecalProjector[] pool;
        Transform poolRoot;
        int next;
        Vector3 last;
        float travelled;
        readonly RaycastHit[] hits = new RaycastHit[8];

        void Awake()
        {
            if (!health) health = GetComponent<AnimalHealth>();
            health.Hit += OnHit;
            last = transform.position;
        }

        void OnDestroy()
        {
            if (health) health.Hit -= OnHit;
            if (poolRoot) Destroy(poolRoot.gameObject);
        }

        void OnHit(HitOutcome outcome)
        {
            // a bigger mark where it was hit
            Drop(health.LastHit.point, 1.8f);
            last = transform.position;
            travelled = 0f;
        }

        void Update()
        {
            if (!health.IsWounded) return;
            Vector3 p = transform.position;
            travelled += Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(last.x, 0f, last.z));
            last = p;
            if (travelled < spacing) return;
            travelled = 0f;
            Vector2 jitter = Random.insideUnitCircle * 0.25f;
            Drop(p + new Vector3(jitter.x, 0f, jitter.y), Random.Range(0.7f, 1.2f));
        }

        void Drop(Vector3 at, float scale)
        {
            if (!ShowBlood || !dropMaterial) return;
            if (pool == null) CreatePool();
            int n = Physics.RaycastNonAlloc(at + Vector3.up * 3f, Vector3.down, hits, 8f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; RaycastHit ground = default;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].collider is CharacterController || hits[i].collider.transform.IsChildOf(transform)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; ground = hits[i]; }
            }
            if (best == float.MaxValue) return;
            var d = pool[next];
            next = (next + 1) % pool.Length;
            d.transform.SetPositionAndRotation(ground.point, Quaternion.LookRotation(-ground.normal, Random.onUnitSphere));
            d.size = new Vector3(dropSize.x * scale, dropSize.y * scale, 0.25f);
            d.gameObject.SetActive(true);
        }

        void CreatePool()
        {
            pool = new DecalProjector[poolSize];
            poolRoot = new GameObject($"BloodTrail_{name}").transform;   // not a child: drops stay where they fell
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("Drop");
                go.transform.SetParent(poolRoot, false);
                go.SetActive(false);
                var p = go.AddComponent<DecalProjector>();
                p.material = dropMaterial;
                p.pivot = Vector3.zero;
                p.drawDistance = 45f;
                p.startAngleFade = 50f; p.endAngleFade = 75f;   // ground only: not up the animal's legs, rocks or trunks
                pool[i] = p;
            }
        }
    }
}
