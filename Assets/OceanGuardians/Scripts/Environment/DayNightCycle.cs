using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Items;

namespace OceanGuardians.Environment
{
    public enum DayPhase { Morning, Midday, Sunset, Dusk }

    /// <summary>
    /// Decision 8A daylight cycle: one cozy day of morning -> midday -> sunset -> dusk.
    /// Walking is always free; the clock simply drifts, and heavy hauls (tires, rescues)
    /// nudge it slightly. At dusk's end (or via the camp tent) the Day Wrap-Up opens;
    /// sleeping starts the next morning. Sun color/intensity/rotation, ambient light and
    /// the camera sky color all interpolate across four phase stops (no shadow pops:
    /// the light moves every frame, never in steps).
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        [Tooltip("Real seconds for one full daylight. 540 = 9 minutes.")]
        [SerializeField, Min(30f)] float dayLengthSeconds = 540f;
        [SerializeField] Light sun;
        [Tooltip("Camp lanterns / campfire lights that fade on at dusk.")]
        [SerializeField] Light[] lanterns = new Light[0];
        [Tooltip("Fraction of the day a heavy haul (car tire, ghost-net rescue) uses up.")]
        [SerializeField, Range(0f, 0.1f)] float heavyTaskTime = 0.02f;

        [Header("Phase colors (Morning / Midday / Sunset / Dusk)")]
        [SerializeField] Color[] sunColors = { new(1f, 0.85f, 0.65f), new(1f, 0.98f, 0.92f), new(1f, 0.62f, 0.35f), new(0.55f, 0.55f, 0.85f) };
        [SerializeField] Color[] skyColors = { new(0.98f, 0.82f, 0.66f), new(0.45f, 0.85f, 0.95f), new(0.98f, 0.6f, 0.38f), new(0.32f, 0.34f, 0.62f) };
        [SerializeField] float[] sunIntensity = { 0.9f, 1.25f, 0.85f, 0.3f };
        [SerializeField] float lanternIntensity = 1.6f;

        Camera cam;
        bool duskAnnounced, dayEnded;

        public int DayNumber { get; private set; } = 1;
        /// <summary>0 = morning start, 1 = end of dusk.</summary>
        public float TimeOfDay { get; private set; }
        public DayPhase Phase => PhaseAt(TimeOfDay);
        public bool IsDayEnded => dayEnded;

        public static DayPhase PhaseAt(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.3f ? DayPhase.Morning : t < 0.62f ? DayPhase.Midday : t < 0.85f ? DayPhase.Sunset : DayPhase.Dusk;
        }

        /// <summary>Piecewise lerp across the 4 phase stops (stops sit at t = 0, .45, .75, 1).</summary>
        public static Color EvaluateStops(Color[] stops, float t)
        {
            if (stops == null || stops.Length < 4) return Color.white;
            t = Mathf.Clamp01(t);
            var at = new[] { 0f, 0.45f, 0.75f, 1f };
            for (int i = 0; i < 3; i++)
                if (t <= at[i + 1]) return Color.Lerp(stops[i], stops[i + 1], Mathf.InverseLerp(at[i], at[i + 1], t));
            return stops[3];
        }

        static float EvaluateStops(float[] stops, float t)
        {
            var c = EvaluateStops(new[] { Grey(stops[0]), Grey(stops[1]), Grey(stops[2]), Grey(stops[3]) }, t);
            return c.r;
            static Color Grey(float v) => new(v, v, v);
        }

        void Awake()
        {
            Instance = this;
            cam = Camera.main;
            if (sun == null)
                foreach (var l in FindObjectsByType<Light>())
                    if (l.type == LightType.Directional) { sun = l; break; }
            Apply();
        }

        void OnEnable()
        {
            EventManager.ItemCollected += OnItemCollected;
            EventManager.WildlifeRescued += OnRescued;
        }

        void OnDisable()
        {
            EventManager.ItemCollected -= OnItemCollected;
            EventManager.WildlifeRescued -= OnRescued;
        }

        void OnItemCollected(ItemData item, Vector3 _)
        {
            // Heavy hauling (e.g. the Stage-9 car tire) costs a sliver of daylight; walking never does.
            if (item is LitterItemData litter && litter.requiredStage >= 9) AdvanceTime(heavyTaskTime);
        }

        void OnRescued(string _) => AdvanceTime(heavyTaskTime); // cutting ghost nets is real work

        void Update()
        {
            if (dayEnded) return;
            AdvanceTime(Time.deltaTime / dayLengthSeconds);
        }

        /// <summary>Move the clock forward (also the manual/debug advance). Fires dusk + day-end once each.</summary>
        public void AdvanceTime(float fraction)
        {
            if (dayEnded || fraction <= 0f) return;
            TimeOfDay = Mathf.Clamp01(TimeOfDay + fraction);
            if (!duskAnnounced && Phase == DayPhase.Dusk)
            {
                duskAnnounced = true;
                EventManager.RaiseDuskFalls();
            }
            Apply();
            if (TimeOfDay >= 1f) RequestDayEnd();
        }

        /// <summary>Ends the day now (tent rest or clock expiry) and opens the wrap-up via DayEnded.</summary>
        public void RequestDayEnd()
        {
            if (dayEnded) return;
            dayEnded = true;
            EventManager.RaiseDayEnded(DayNumber);
        }

        /// <summary>"Sleep &amp; Greet Tomorrow": fresh morning, stats cleared, birdsong, auto-save.</summary>
        public void StartNewDay()
        {
            DayNumber++;
            TimeOfDay = 0f;
            duskAnnounced = false;
            dayEnded = false;
            DayStats.Instance?.ResetDay();
            Apply();
            Audio.AudioManager.Instance?.PlayMorningBirdsong();
            EventManager.RaiseDayStarted(DayNumber);
            EventManager.RaiseCheckpoint(); // auto-save the new morning
        }

        /// <summary>Save-load: restore the exact day and sunlight.</summary>
        public void Restore(int dayNumber, float timeOfDay)
        {
            DayNumber = Mathf.Max(1, dayNumber);
            TimeOfDay = Mathf.Clamp01(timeOfDay);
            duskAnnounced = Phase == DayPhase.Dusk;
            dayEnded = false;
            Apply();
        }

        void Apply()
        {
            float t = TimeOfDay;
            if (sun)
            {
                sun.color = EvaluateStops(sunColors, t);
                sun.intensity = EvaluateStops(sunIntensity, t);
                // Sun path: rises low in the east, crests at midday, sinks west into dusk.
                float elevation = Mathf.Lerp(18f, 62f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.08f)));
                float azimuth = Mathf.Lerp(-55f, 55f, t);
                sun.transform.rotation = Quaternion.Euler(elevation, azimuth, 0f);
            }
            Color sky = EvaluateStops(skyColors, t);
            RenderSettings.ambientLight = sky * 0.55f;
            if (cam == null) cam = Camera.main;
            if (cam) cam.backgroundColor = sky;

            float lanternK = Mathf.InverseLerp(0.8f, 0.95f, t);
            foreach (var lantern in lanterns)
                if (lantern) lantern.intensity = lanternIntensity * lanternK;
        }
    }
}
