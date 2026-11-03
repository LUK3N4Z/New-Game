using UnityEngine;

namespace OceanGuardians.Environment
{
    /// <summary>
    /// Phase-4 polish layered over ShoreWaves (which owns the surge timing, the wash, and the
    /// sanderlings' flee/forage behaviour): an undulating segmented foam line riding the wave
    /// front, plus a dark "wet sand" band that stays behind after each wave and slowly dries.
    /// Footstep splashes already sync through WaterZone.IsWet (wave wash = DryDistance &lt; 0).
    /// </summary>
    public class ShorelineWave : MonoBehaviour
    {
        [Tooltip("Segmented foam chunks along the shore; each undulates on its own sine phase.")]
        [SerializeField] Transform[] foamSegments = new Transform[0];
        [Tooltip("Wide thin strip darkening freshly-washed sand; fades as it dries.")]
        [SerializeField] Renderer wetSandBand;
        [SerializeField] float undulation = 0.35f;
        [SerializeField] float drySeconds = 9f;

        Material wetMaterial;
        float wetness, peakReach;

        void Awake()
        {
            // Instance the material so runtime fading never edits the shared asset.
            if (wetSandBand) wetMaterial = wetSandBand.material;
        }

        void Update()
        {
            var waves = ShoreWaves.Instance;
            if (waves == null) return;
            var sea = waves.SeaDirection;
            var alongShore = Vector3.Cross(Vector3.up, sea);

            for (int i = 0; i < foamSegments.Length; i++)
            {
                var seg = foamSegments[i];
                if (seg == null) continue;
                float shorePos = Vector3.Dot(seg.position - transform.position, alongShore);
                float wobble = Mathf.Sin(Time.time * 1.7f + shorePos * 0.45f) * undulation;
                seg.position = waves.FrontPosition + alongShore * shorePos - sea * wobble + Vector3.up * 0.04f;
            }

            // Wet band: soaks instantly under a surge, then dries over drySeconds.
            if (waves.IsSurging) { wetness = 1f; peakReach = Mathf.Max(peakReach, waves.FrontOffset); }
            else wetness = Mathf.Max(0f, wetness - Time.deltaTime / drySeconds);
            if (!waves.IsSurging && wetness <= 0f) peakReach = waves.FrontOffset;

            if (wetMaterial && wetSandBand)
            {
                var c = wetMaterial.color;
                c.a = wetness * 0.45f;
                wetMaterial.color = c;
                var band = wetSandBand.transform;
                band.localScale = new Vector3(band.localScale.x, band.localScale.y, Mathf.Max(0.05f, peakReach));
                band.position = transform.position - sea * (peakReach * 0.5f) + Vector3.up * 0.012f;
                wetSandBand.enabled = c.a > 0.01f;
            }
        }
    }
}
