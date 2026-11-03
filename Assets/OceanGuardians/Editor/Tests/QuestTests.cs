using NUnit.Framework;
using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Items;
using OceanGuardians.Quests;

namespace OceanGuardians.EditorTools.Tests
{
    /// <summary>Edit Mode tests for the quest chain: progress, rewards, chaining, save-restore.</summary>
    public class QuestTests
    {
        GameObject go;
        QuestManager quests;
        EconomyManager economy;

        static QuestData Quest(string id, string next, params QuestStepData[] steps)
        {
            var q = ScriptableObject.CreateInstance<QuestData>();
            q.questId = id; q.questTitle = id; q.nextQuestId = next;
            q.rewardCredits = 50; q.rewardEcoTokens = 10;
            q.steps.AddRange(steps);
            return q;
        }

        static QuestStepData Step(StepType type, string target, int amount) =>
            new() { stepType = type, targetId = target, targetAmount = amount, stepDescription = target };

        // EditMode never calls Awake/OnEnable on plain MonoBehaviours; invoke the lifecycle by hand.
        static void Send(MonoBehaviour mb, string method) =>
            mb.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(mb, null);

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("QuestTest");
            economy = go.AddComponent<EconomyManager>();
            quests = go.AddComponent<QuestManager>();
            Send(economy, "Awake");
            Send(quests, "Awake"); // initializes the built-in catalog; replaced below
            Send(quests, "OnEnable");
            quests.Initialize(new[]
            {
                Quest("q1", "q2", Step(StepType.CollectLitter, "plastic_bottle", 2), Step(StepType.DepositLitter, "", 1)),
                Quest("q2", "", Step(StepType.RescueWildlife, "seal_pup", 1)),
            });
        }

        [TearDown]
        public void TearDown()
        {
            Send(quests, "OnDisable");
            Object.DestroyImmediate(go);
        }

        static LitterItemData Bottle()
        {
            var d = ScriptableObject.CreateInstance<LitterItemData>();
            d.SetId("plastic_bottle");
            return d;
        }

        [Test]
        public void CollectProgressIsSequentialAndClamped()
        {
            var bottle = Bottle();
            EventManager.RaiseItemCollected(bottle, Vector3.zero);
            Assert.AreEqual(1, quests.CurrentQuest.CurrentStep.currentProgress);
            // Deposit must NOT count while the collect step is current (steps run in order).
            EventManager.RaiseLitterDeposited(3);
            Assert.AreEqual(0, quests.CurrentQuest.CurrentStepIndex);
            EventManager.RaiseItemCollected(bottle, Vector3.zero);
            EventManager.RaiseItemCollected(bottle, Vector3.zero); // over-collect: clamped, next step untouched by it
            Assert.AreEqual(1, quests.CurrentQuest.CurrentStepIndex);
            Assert.IsTrue(quests.CurrentQuest.Steps[0].isComplete);
            Assert.AreEqual(2, quests.CurrentQuest.Steps[0].currentProgress);
        }

        [Test]
        public void NonMatchingItemDoesNotProgress()
        {
            var can = ScriptableObject.CreateInstance<LitterItemData>();
            can.SetId("soda_can");
            EventManager.RaiseItemCollected(can, Vector3.zero);
            Assert.AreEqual(0, quests.CurrentQuest.CurrentStep.currentProgress);
        }

        [Test]
        public void CompletionPaysRewardsAndChainsToNextQuest()
        {
            var bottle = Bottle();
            EventManager.RaiseItemCollected(bottle, Vector3.zero);
            EventManager.RaiseItemCollected(bottle, Vector3.zero);
            EventManager.RaiseLitterDeposited(2);
            Assert.AreEqual(50, economy.Get(CurrencyType.RecyclingCredits));
            Assert.AreEqual(10, economy.Get(CurrencyType.EcoTokens));
            Assert.IsTrue(System.Linq.Enumerable.Contains(quests.CompletedQuestIds, "q1"));
            Assert.AreEqual("q2", quests.CurrentQuest.Data.questId);

            EventManager.RaiseWildlifeRescued("seal_pup");
            Assert.IsNull(quests.CurrentQuest, "chain finished: no active quest");
            Assert.AreEqual(100, economy.Get(CurrencyType.RecyclingCredits));
        }

        [Test]
        public void RestoreResumesMidQuestAndSkipsCompleted()
        {
            quests.RestoreState("q1", 1, 0, new string[0]);
            Assert.AreEqual(1, quests.CurrentQuest.CurrentStepIndex);
            Assert.IsTrue(quests.CurrentQuest.Steps[0].isComplete);

            quests.RestoreState("", 0, 0, new[] { "q1", "q2" });
            Assert.IsNull(quests.CurrentQuest);
            Assert.AreEqual(2, quests.CompletedQuestIds.Count);
        }

        [Test]
        public void RuntimeProgressNeverMutatesTheAsset()
        {
            EventManager.RaiseItemCollected(Bottle(), Vector3.zero);
            Assert.AreEqual(0, quests.CurrentQuest.Data.steps[0].currentProgress, "ScriptableObject asset must stay pristine");
        }
    }
}
