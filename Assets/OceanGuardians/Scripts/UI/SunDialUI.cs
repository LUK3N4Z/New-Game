using TMPro;
using UnityEngine;
using OceanGuardians.Environment;

namespace OceanGuardians.UI
{
    /// <summary>Cozy sun-dial next to the top bar: a sun puck rides a little arc, "Day X" underneath.</summary>
    public class SunDialUI : MonoBehaviour
    {
        [SerializeField] RectTransform sunIcon;
        [SerializeField] TMP_Text dayText;
        [Tooltip("Half-width / height of the sun's arc in pixels.")]
        [SerializeField] Vector2 arc = new(55f, 30f);

        void Update()
        {
            var cycle = DayNightCycle.Instance;
            if (cycle == null || sunIcon == null) return;
            float t = cycle.TimeOfDay; // left (morning) -> crest (midday) -> right (dusk)
            sunIcon.anchoredPosition = new Vector2(Mathf.Lerp(-arc.x, arc.x, t), Mathf.Sin(Mathf.PI * t) * arc.y);
            sunIcon.gameObject.SetActive(!cycle.IsDayEnded);
            if (dayText) dayText.text = $"Day {cycle.DayNumber}";
        }
    }
}
