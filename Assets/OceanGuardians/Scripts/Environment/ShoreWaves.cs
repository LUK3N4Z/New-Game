using UnityEngine;

namespace OceanGuardians.Environment
{
    /// <summary>
    /// Gentle wave wash: a front that surges up the beach and slides back on a loop.
    /// Place at the low-tide waterline with the blue (forward) axis pointing out to sea.
    /// Moves an optional foam strip and wet-sand "wash" so you can see it; shorebirds read it
    /// to chase the retreating water and scamper away from incoming waves.
    /// </summary>
    public class ShoreWaves : MonoBehaviour
    {
        public static ShoreWaves Instance { get; private set; }

        [Tooltip("How far up the beach (metres) a wave runs.")]
        [SerializeField] float reach = 4f;
        [SerializeField] float period = 7f;
        [Tooltip("Fraction of the period spent surging in (the rest is the slow slide back).")]
        [SerializeField, Range(0.1f, 0.9f)] float surgeFraction = 0.35f;
        [SerializeField] Transform foam;
        [Tooltip("Thin water slab between the waterline and the wave front (scaled on Z).")]
        [SerializeField] Transform wash;

        /// <summary>Metres the current front sits inland of the waterline.</summary>
        public float FrontOffset { get; private set; }
        /// <summary>True while the water is rushing in.</summary>
        public bool IsSurging { get; private set; }
        public Vector3 SeaDirection { get; private set; }
        public Vector3 FrontPosition => transform.position - SeaDirection * FrontOffset;

        void Awake()
        {
            Instance = this;
            var f = transform.forward;
            f.y = 0f;
            SeaDirection = f.normalized;
        }

        void Update()
        {
            float k = Mathf.Repeat(Time.time / period, 1f);
            IsSurging = k < surgeFraction;
            float u = IsSurging
                ? Mathf.SmoothStep(0f, 1f, k / surgeFraction)
                : 1f - Mathf.SmoothStep(0f, 1f, (k - surgeFraction) / (1f - surgeFraction));
            FrontOffset = u * reach;

            if (foam) foam.position = FrontPosition + Vector3.up * 0.03f;
            if (wash)
            {
                var s = wash.localScale;
                wash.localScale = new Vector3(s.x, s.y, Mathf.Max(0.01f, FrontOffset));
                wash.position = transform.position - SeaDirection * (FrontOffset * 0.5f) + Vector3.up * 0.015f;
            }
        }

        /// <summary>Metres from the wave front to p, positive on the dry side.</summary>
        public float DryDistance(Vector3 p) => Vector3.Dot(p - FrontPosition, -SeaDirection);

        /// <summary>The point on the wave front level with p (same spot along the shore).</summary>
        public Vector3 ClosestFrontPoint(Vector3 p) => p + SeaDirection * DryDistance(p);
    }
}
