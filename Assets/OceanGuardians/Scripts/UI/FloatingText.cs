using TMPro;
using UnityEngine;

namespace OceanGuardians.UI
{
    /// <summary>
    /// "+1 Plastic Bottle" popup that rises and fades above a world position.
    /// Self-building: call FloatingText.Spawn() with a layer on a Screen Space - Overlay canvas.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        [SerializeField] float lifetime = 1.1f;
        [SerializeField] float risePixels = 90f;

        TMP_Text label;
        Color color;
        Vector3 startScreenPos;
        float age;

        public static FloatingText Spawn(RectTransform layer, Camera cam, Vector3 worldPos, string message, Color color, float fontSize = 34f)
        {
            if (layer == null || cam == null) return null;

            var go = new GameObject("FloatingText", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            rt.sizeDelta = new Vector2(500f, 60f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = message;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.color = color;

            var ft = go.AddComponent<FloatingText>();
            ft.label = tmp;
            ft.color = color;
            ft.startScreenPos = cam.WorldToScreenPoint(worldPos + Vector3.up * 1.2f);
            rt.position = ft.startScreenPos; // overlay canvas: position is in screen pixels
            return ft;
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = age / lifetime;
            if (k >= 1f) { Destroy(gameObject); return; }

            float eased = 1f - (1f - k) * (1f - k);
            transform.position = startScreenPos + Vector3.up * (risePixels * eased);
            var c = color;
            c.a *= 1f - k * k; // hold, then fade
            label.color = c;
        }
    }
}
