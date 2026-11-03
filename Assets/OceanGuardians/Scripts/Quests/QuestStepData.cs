using System;
using UnityEngine;

namespace OceanGuardians.Quests
{
    public enum StepType { CollectLitter, DepositLitter, RescueWildlife, PurchaseUpgrade, ReachArea }

    /// <summary>
    /// One objective inside a quest. The ScriptableObject asset copy is authoring data only;
    /// QuestManager works on Clone()s so play progress never dirties the asset on disk.
    /// </summary>
    [Serializable]
    public class QuestStepData
    {
        public StepType stepType;
        public string stepDescription;
        [Tooltip("Item id (plastic_bottle), rescue saveId (seal_pup), area id (wildlife_cove) or stage_02. Empty = any.")]
        public string targetId;
        [Min(1)] public int targetAmount = 1;
        public int currentProgress;
        public bool isComplete;

        public QuestStepData Clone() => new()
        {
            stepType = stepType, stepDescription = stepDescription, targetId = targetId,
            targetAmount = Mathf.Max(1, targetAmount), currentProgress = 0, isComplete = false,
        };

        /// <returns>True only on the call that completes the step.</returns>
        public bool AddProgress(int amount)
        {
            if (isComplete || amount <= 0) return false;
            currentProgress = Mathf.Min(currentProgress + amount, targetAmount);
            isComplete = currentProgress >= targetAmount;
            return isComplete;
        }
    }
}
