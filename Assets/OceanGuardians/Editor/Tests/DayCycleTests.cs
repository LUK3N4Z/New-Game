using NUnit.Framework;
using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Environment;
using OceanGuardians.Items;

namespace OceanGuardians.EditorTools.Tests
{
    /// <summary>Edit Mode tests for the daylight phase math and the daily-haul tally.</summary>
    public class DayCycleTests
    {
        [Test]
        public void PhasesRunMorningToDusk()
        {
            Assert.AreEqual(DayPhase.Morning, DayNightCycle.PhaseAt(0.05f));
            Assert.AreEqual(DayPhase.Midday, DayNightCycle.PhaseAt(0.45f));
            Assert.AreEqual(DayPhase.Sunset, DayNightCycle.PhaseAt(0.72f));
            Assert.AreEqual(DayPhase.Dusk, DayNightCycle.PhaseAt(0.95f));
            Assert.AreEqual(DayPhase.Morning, DayNightCycle.PhaseAt(-1f), "clamped");
            Assert.AreEqual(DayPhase.Dusk, DayNightCycle.PhaseAt(2f), "clamped");
        }

        [Test]
        public void SunColorInterpolatesAcrossStops()
        {
            var c = DayNightCycle.EvaluateStops(new[] { Color.black, Color.white, Color.black, Color.black }, 0.25f);
            Assert.Greater(c.r, 0.4f);
            Assert.Less(c.r, 0.9f);
            Assert.AreEqual(0f, DayNightCycle.EvaluateStops(new[] { Color.black, Color.white, Color.white, Color.white }, 0f).r, 0.001f);
        }

        GameObject go;
        DayStats stats;
        EconomyManager economy;

        static void Send(MonoBehaviour mb, string method) =>
            mb.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(mb, null);

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("DayStatsTest");
            economy = go.AddComponent<EconomyManager>();
            stats = go.AddComponent<DayStats>();
            Send(economy, "Awake");
            Send(stats, "Awake");
            Send(stats, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            Send(stats, "OnDisable");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void StatsTallyPickupsRescuesAndEarnings()
        {
            var bottle = ScriptableObject.CreateInstance<LitterItemData>();
            var shell = ScriptableObject.CreateInstance<CollectibleItemData>();
            EventManager.RaiseItemCollected(bottle, Vector3.zero);
            EventManager.RaiseItemCollected(bottle, Vector3.zero);
            EventManager.RaiseItemCollected(shell, Vector3.zero);
            EventManager.RaiseWildlifeRescued("seal_pup");
            economy.Add(CurrencyType.RecyclingCredits, 30);
            economy.Add(CurrencyType.EcoTokens, 25);
            economy.TrySpend(CurrencyType.RecyclingCredits, 10); // spending must not reduce "earned today"

            Assert.AreEqual(2, stats.LitterCollected);
            Assert.AreEqual(1, stats.BeachFinds);
            Assert.AreEqual(1, stats.WildlifeRescued);
            Assert.AreEqual(30, stats.CreditsEarned);
            Assert.AreEqual(25, stats.EcoEarned);
        }

        [Test]
        public void StatsResetForNewDay()
        {
            EventManager.RaiseItemCollected(ScriptableObject.CreateInstance<LitterItemData>(), Vector3.zero);
            stats.ResetDay();
            Assert.AreEqual(0, stats.LitterCollected);
            Assert.AreEqual(0, stats.JournalTitlesToday.Count);
        }
    }
}
