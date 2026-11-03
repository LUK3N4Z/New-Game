using System.Collections.Generic;
using UnityEngine;

namespace OceanGuardians.Environment
{
    /// <summary>Marks a tidepool's wet footprint so footsteps splash instead of crunch.</summary>
    public class WaterZone : MonoBehaviour
    {
        [SerializeField] float radius = 1.6f;

        static readonly List<WaterZone> All = new();

        public static bool IsWet(Vector3 pos)
        {
            foreach (var zone in All)
            {
                var d = pos - zone.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude <= zone.radius * zone.radius) return true;
            }
            // Inside the moving wave wash counts as paddling too.
            return ShoreWaves.Instance != null && ShoreWaves.Instance.DryDistance(pos) < 0f;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearAll() => All.Clear();
    }
}
