using System.Collections.Generic;
using UnityEngine;

namespace OceanGuardians.Quests
{
    [CreateAssetMenu(menuName = "Ocean Guardians/Quest", fileName = "Quest_New")]
    public class QuestData : ScriptableObject
    {
        public string questId;
        public string questTitle;
        [TextArea(2, 4)] public string questDescription;
        public Sprite questIcon;
        public List<QuestStepData> steps = new();
        public int rewardCredits;
        public int rewardCollectorCoins;
        public int rewardEcoTokens;
        [Tooltip("Optional Ocean Journal entry unlocked on completion.")]
        public string rewardJournalEntryId;
        [Tooltip("Linear chaining: questId started when this one completes. Empty = end of chain.")]
        public string nextQuestId;
    }
}
