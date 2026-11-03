using UnityEngine;
using UnityEngine.Rendering;

namespace OceanGuardians.Player
{
    /// <summary>
    /// Self-building green destination ripple: a LineRenderer ring that expands and fades.
    /// No prefab needed - call ClickMarker.Spawn(). Only one is shown at a time.
    /// </summary>
    public class ClickMarker : MonoBehaviour
    {
        const int Segments = 40;

        [SerializeField] float lifetime = 0.6f;
        [SerializeField] float startRadius = 0.15f;
        [SerializeField] float endRadius = 0.75f;
        [SerializeField] float width = 0.08f;

        static ClickMarker current;
        static Material fallbackMaterial;

        LineRenderer line;
        Color color;
        float age;

        public static ClickMarker Spawn(Vector3 position, Color color, Material material = null)
        {
            if (current != null) Destroy(current.gameObject);

            var go = new GameObject("ClickMarker");
            go.transform.position = position + Vector3.up * 0.03f; // avoid z-fighting with the sand
            current = go.AddComponent<ClickMarker>();
            current.color = color;

            if (material == null)
            {
                if (fallbackMaterial == null) fallbackMaterial = new Material(Shader.Find("Sprites/Default"));
                material = fallbackMaterial;
            }
            current.line.sharedMaterial = material;
            return current;
        }

        void Awake()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.widthMultiplier = width;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = age / lifetime;
            if (k >= 1f) { Destroy(gameObject); return; }

            float eased = 1f - (1f - k) * (1f - k); // ease-out
            float radius = Mathf.Lerp(startRadius, endRadius, eased);
            for (int i = 0; i < Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            var c = color;
            c.a *= 1f - k;
            line.startColor = line.endColor = c;
        }
    }
}
