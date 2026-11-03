using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using OceanGuardians.Core;
using OceanGuardians.Items;
using OceanGuardians.Progression;

namespace OceanGuardians.Environment
{
    /// <summary>
    /// A beach area that "blooms" once its litter is cleared: sand lightens, water clears,
    /// and shorebirds/crabs move in. Also the zone's wildlife spawner: a few creatures from the
    /// start, more after the bloom. Litter inside the radius is found automatically at Start.
    /// Natural finds and heavy litter (needs a later equipment stage) don't count toward the bloom.
    /// </summary>
    public class RestorationZone : MonoBehaviour
    {
        [SerializeField] string zoneName = "Seal Cove";
        [SerializeField] float radius = 15f;
        [Tooltip("Fraction of litter that must be cleared (1 = all of it).")]
        [SerializeField, Range(0.1f, 1f)] float clearThreshold = 1f;

        [Header("Restoration Bloom")]
        [SerializeField] Renderer sandRenderer;
        [SerializeField] Color restoredSandColor = new(0.98f, 0.93f, 0.78f);
        [SerializeField] Renderer waterRenderer;
        [SerializeField] Color restoredWaterColor = new(0.25f, 0.85f, 0.85f);
        [SerializeField] float bloomDuration = 2.5f;
        [Tooltip("Journal ids unlocked on bloom (the wildlife that returns, the habitat).")]
        [SerializeField] string[] journalEntriesOnBloom = { "ghost_crab", "sanderling", "tidepools" };
        [SerializeField] int ecoTokenReward = 15;

        [Header("Wildlife Spawner")]
        [Tooltip("AmbientCreature prefabs (e.g. Ghost Crab, Sanderling).")]
        [SerializeField] GameObject[] wildlifePrefabs;
        [Tooltip("Per prefab, present from the start so the beach isn't empty.")]
        [SerializeField] int spawnsAtStart = 1;
        [Tooltip("Per prefab, added when the zone blooms.")]
        [SerializeField] int spawnsPerPrefab = 3;
        [Tooltip("Rocks and tidepools crabs scuttle between.")]
        [SerializeField] Transform[] habitatPoints;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        readonly List<LitterItem> litter = new();
        bool restored;

        public float Progress { get; private set; }

        void Start()
        {
            // Include inactive: SaveManager deactivates litter that a loaded save says is already cleared.
            foreach (var item in FindObjectsByType<LitterItem>(FindObjectsInactive.Include))
                if (item.Data is LitterItemData { requiredStage: <= 1 } && FlatDistance(item.transform.position) <= radius)
                    litter.Add(item);

            Spawn(spawnsAtStart);
            if (UpdateProgress()) StartCoroutine(Bloom(instant: true)); // already clean in the loaded save
        }

        void OnEnable() => EventManager.ItemCollected += OnItemCollected;
        void OnDisable() => EventManager.ItemCollected -= OnItemCollected;

        void OnItemCollected(ItemData _, Vector3 __)
        {
            if (!restored && UpdateProgress()) StartCoroutine(Bloom(instant: false));
        }

        bool UpdateProgress()
        {
            if (litter.Count == 0) return false;
            int remaining = 0;
            foreach (var item in litter)
                if (item != null && item.gameObject.activeInHierarchy) remaining++;
            Progress = 1f - remaining / (float)litter.Count;
            return Progress >= clearThreshold;
        }

        IEnumerator Bloom(bool instant)
        {
            restored = true;
            if (!instant)
            {
                EventManager.RaiseAlert($"{zoneName} is clean! Nature is coming back. +{ecoTokenReward} Eco");
                EconomyManager.Instance?.Add(CurrencyType.EcoTokens, ecoTokenReward);
            }

            // renderer.material makes a per-renderer copy so the shared asset isn't permanently changed.
            Material sand = sandRenderer ? sandRenderer.material : null;
            Material water = waterRenderer ? waterRenderer.material : null;
            Color sandFrom = sand ? sand.GetColor(BaseColorId) : default;
            Color waterFrom = water ? water.GetColor(BaseColorId) : default;

            for (float t = 0f; !instant && t < bloomDuration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / bloomDuration);
                if (sand) sand.SetColor(BaseColorId, Color.Lerp(sandFrom, restoredSandColor, k));
                if (water) water.SetColor(BaseColorId, Color.Lerp(waterFrom, restoredWaterColor, k));
                yield return null;
            }
            if (sand) sand.SetColor(BaseColorId, restoredSandColor);
            if (water) water.SetColor(BaseColorId, restoredWaterColor);

            Spawn(spawnsPerPrefab);
            if (!instant)
                foreach (var id in journalEntriesOnBloom) OceanJournal.Instance?.Discover(id);
        }

        void Spawn(int perPrefab)
        {
            if (wildlifePrefabs == null) return;
            var habitat = new List<Vector3>();
            if (habitatPoints != null)
                foreach (var h in habitatPoints)
                    if (h) habitat.Add(h.position);

            foreach (var prefab in wildlifePrefabs)
            {
                if (prefab == null) continue;
                for (int i = 0; i < perPrefab; i++)
                {
                    var p = transform.position + Random.insideUnitSphere * (radius * 0.8f);
                    if (!NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) continue;
                    var go = Instantiate(prefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    if (go.TryGetComponent<AmbientCreature>(out var creature)) creature.Init(habitat);
                }
            }
        }

        float FlatDistance(Vector3 p)
        {
            var d = p - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
