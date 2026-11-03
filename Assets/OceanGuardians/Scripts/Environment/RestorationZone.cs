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
    /// and shorebirds/crabs spawn. Litter inside the radius is found automatically at Start.
    /// Natural finds (shells, sea glass) don't count - only trash.
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
        [Tooltip("Wildlife prefabs to spawn (e.g. Ghost Crab, Sanderling).")]
        [SerializeField] GameObject[] wildlifePrefabs;
        [SerializeField] int spawnsPerPrefab = 3;
        [Tooltip("Journal ids unlocked on bloom (the wildlife that returns, the habitat).")]
        [SerializeField] string[] journalEntriesOnBloom = { "ghost_crab", "sanderling", "tidepools" };
        [SerializeField] int ecoTokenReward = 15;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        readonly List<LitterItem> litter = new();
        bool restored;

        public float Progress { get; private set; }

        void Start()
        {
            foreach (var item in FindObjectsByType<LitterItem>())
                if (item.IsLitter && FlatDistance(item.transform.position) <= radius)
                    litter.Add(item);
        }

        void OnEnable() => EventManager.ItemCollected += OnItemCollected;
        void OnDisable() => EventManager.ItemCollected -= OnItemCollected;

        void OnItemCollected(ItemData _, Vector3 __)
        {
            if (restored || litter.Count == 0) return;
            int remaining = 0;
            foreach (var item in litter)
                if (item != null && item.gameObject.activeInHierarchy) remaining++;

            Progress = 1f - remaining / (float)litter.Count;
            if (Progress >= clearThreshold) StartCoroutine(Bloom());
        }

        IEnumerator Bloom()
        {
            restored = true;
            EventManager.RaiseAlert($"{zoneName} is clean! Nature is coming back. +{ecoTokenReward} Eco");
            EconomyManager.Instance?.Add(CurrencyType.EcoTokens, ecoTokenReward);

            // renderer.material makes a per-renderer copy so the shared asset isn't permanently changed.
            Material sand = sandRenderer ? sandRenderer.material : null;
            Material water = waterRenderer ? waterRenderer.material : null;
            Color sandFrom = sand ? sand.GetColor(BaseColorId) : default;
            Color waterFrom = water ? water.GetColor(BaseColorId) : default;

            for (float t = 0f; t < bloomDuration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / bloomDuration);
                if (sand) sand.SetColor(BaseColorId, Color.Lerp(sandFrom, restoredSandColor, k));
                if (water) water.SetColor(BaseColorId, Color.Lerp(waterFrom, restoredWaterColor, k));
                yield return null;
            }

            SpawnWildlife();
            foreach (var id in journalEntriesOnBloom) OceanJournal.Instance?.Discover(id);
        }

        void SpawnWildlife()
        {
            if (wildlifePrefabs == null) return;
            foreach (var prefab in wildlifePrefabs)
            {
                if (prefab == null) continue;
                for (int i = 0; i < spawnsPerPrefab; i++)
                {
                    var p = transform.position + Random.insideUnitSphere * (radius * 0.8f);
                    if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas))
                        Instantiate(prefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
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
