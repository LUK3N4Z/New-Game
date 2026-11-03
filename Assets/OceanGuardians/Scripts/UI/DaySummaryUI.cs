using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using OceanGuardians.Core;
using OceanGuardians.Environment;

namespace OceanGuardians.UI
{
    /// <summary>
    /// The "Day Wrap-Up" modal: fades to camp, tallies today's haul from DayStats,
    /// highlights new journal snapshots, then "Sleep &amp; Greet Tomorrow" starts a
    /// fresh morning (birdsong + auto-save via DayNightCycle).
    /// </summary>
    public class DaySummaryUI : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text headerText;
        [SerializeField] TMP_Text haulText;
        [SerializeField] TMP_Text journalText;
        [SerializeField] Button sleepButton;
        [Tooltip("Full-screen black image used for the fade to camp.")]
        [SerializeField] Image fadeOverlay;
        [SerializeField] float fadeSeconds = 0.6f;

        bool showing;

        void OnEnable() => EventManager.DayEnded += OnDayEnded;
        void OnDisable() => EventManager.DayEnded -= OnDayEnded;

        void Start()
        {
            if (panel) panel.SetActive(false);
            SetFade(0f);
            sleepButton?.onClick.AddListener(OnSleep);
        }

        void OnDayEnded(int dayNumber)
        {
            if (showing || panel == null) return;
            showing = true;
            StartCoroutine(Show(dayNumber));
        }

        IEnumerator Show(int dayNumber)
        {
            yield return Fade(1f);
            WarpToCamp();
            Fill(dayNumber);
            panel.SetActive(true);
            yield return Fade(0f);
        }

        void WarpToCamp()
        {
            var gm = GameManager.Instance;
            var tent = BaseCampTent.Instance;
            if (gm == null || gm.Player == null || tent == null) return;
            var spot = tent.transform.position + tent.transform.forward * 3f;
            var agent = gm.Player.GetComponent<NavMeshAgent>();
            if (agent) agent.Warp(spot); else gm.Player.transform.position = spot;
            gm.Player.FaceTowards(tent.transform.position);
        }

        void Fill(int dayNumber)
        {
            if (headerText) headerText.text = $"Day {dayNumber} Complete! Great work, Guardian!";
            var s = DayStats.Instance;
            if (haulText)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"<b>Litter Collected:</b> {N(s?.LitterCollected)} pieces  (+{N(s?.CreditsEarned)} Recycling Credits)");
                sb.AppendLine($"<b>Beach Finds:</b> {N(s?.BeachFinds)} shells & sea glass  (+{N(s?.CoinsEarned)} Collector Coins)");
                sb.AppendLine($"<b>Wildlife Helped:</b> {N(s?.WildlifeRescued)} rescues  (+{N(s?.EcoEarned)} Eco Tokens)");
                sb.AppendLine($"<b>Upcycled Creations:</b> {N(s?.CraftedToday)} crafts at the workbench");
                haulText.text = sb.ToString();
            }
            if (journalText)
                journalText.text = s != null && s.JournalTitlesToday.Count > 0
                    ? "<b>New Ocean Journal snapshots:</b> " + string.Join(", ", s.JournalTitlesToday)
                    : "<color=#9AA5B1>No new journal snapshots today - tomorrow's beach awaits!</color>";
            static int N(int? v) => v ?? 0;
        }

        void OnSleep()
        {
            if (!showing) return;
            showing = false;
            panel.SetActive(false);
            DayNightCycle.Instance?.StartNewDay();
            EventManager.RaiseAlert($"Day {DayNightCycle.Instance.DayNumber} - a fresh morning on the beach!");
        }

        IEnumerator Fade(float target)
        {
            if (fadeOverlay == null) yield break;
            float from = fadeOverlay.color.a;
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(from, target, t / fadeSeconds));
                yield return null;
            }
            SetFade(target);
        }

        void SetFade(float alpha)
        {
            if (fadeOverlay == null) return;
            var c = fadeOverlay.color;
            c.a = alpha;
            fadeOverlay.color = c;
            fadeOverlay.raycastTarget = alpha > 0.01f; // block taps only while fading
        }
    }
}
