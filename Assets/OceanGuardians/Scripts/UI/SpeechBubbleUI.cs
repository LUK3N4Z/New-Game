using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OceanGuardians.UI
{
    /// <summary>
    /// A cozy speech bubble anchored to a world transform. Screen-space (like FloatingText),
    /// so it can never clip through bluffs or hide behind terrain at the 55-degree camera.
    /// Bounces in, follows its anchor, auto-dismisses, and a tap on it skips it early.
    /// Self-building: call SpeechBubbleUI.Show() with a layer on the overlay canvas.
    /// </summary>
    public class SpeechBubbleUI : MonoBehaviour
    {
        Transform anchor;
        Camera cam;
        RectTransform rt;
        float age, lifetime;
        float heightAbove = 2.1f;

        public static SpeechBubbleUI Show(RectTransform layer, Camera cam, Transform anchor, string text, float seconds = 3.5f, float heightAbove = 2.1f)
        {
            if (layer == null || cam == null || anchor == null) return null;

            var go = new GameObject("SpeechBubble", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            rt.sizeDelta = new Vector2(340f, 110f);
            rt.pivot = new Vector2(0.5f, 0f); // bubble grows upward from the head

            var bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.94f);
            var button = go.AddComponent<Button>(); // tap to skip

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(14f, 8f); labelRt.offsetMax = new Vector2(-14f, -8f);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24f;
            tmp.color = new Color(0.13f, 0.24f, 0.29f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            var bubble = go.AddComponent<SpeechBubbleUI>();
            bubble.anchor = anchor; bubble.cam = cam; bubble.rt = rt;
            bubble.lifetime = seconds; bubble.heightAbove = heightAbove;
            rt.localScale = Vector3.zero;
            button.onClick.AddListener(() => bubble.age = bubble.lifetime);
            return bubble;
        }

        void Update()
        {
            if (anchor == null) { Destroy(gameObject); return; }
            age += Time.deltaTime;
            if (age >= lifetime) { Destroy(gameObject); return; }

            // Playful overshoot pop-in (0 -> 1.15 -> 1), gentle shrink right before leaving.
            float scale;
            const float popSeconds = 0.28f;
            if (age < popSeconds)
            {
                float k = age / popSeconds;
                scale = Mathf.Sin(k * Mathf.PI * 0.5f) * (1f + 0.15f * Mathf.Sin(k * Mathf.PI));
            }
            else scale = age > lifetime - 0.2f ? Mathf.Lerp(1f, 0.6f, (age - (lifetime - 0.2f)) / 0.2f) : 1f;
            rt.localScale = Vector3.one * scale;

            var screen = cam.WorldToScreenPoint(anchor.position + Vector3.up * heightAbove);
            rt.position = screen; // overlay canvas: position is in screen pixels
            gameObject.SetActive(screen.z > 0f);
        }
    }
}
