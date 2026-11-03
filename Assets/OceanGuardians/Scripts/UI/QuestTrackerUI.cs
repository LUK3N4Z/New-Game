using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OceanGuardians.Quests;

namespace OceanGuardians.UI
{
    /// <summary>
    /// Collapsible quest card under the minimap. Reactive only: renders QuestManager's current
    /// quest, pulses a gold star and flashes green when a step completes. Tap the header to fold.
    /// </summary>
    public class QuestTrackerUI : MonoBehaviour
    {
        [SerializeField] Image panel;
        [SerializeField] Button headerButton;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text stepsText;
        [SerializeField] Image starIcon;
        [SerializeField] Color flashColor = new(0.45f, 0.95f, 0.5f, 1f);

        Color panelColor;
        bool collapsed;
        QuestManager quests;

        void Start()
        {
            if (panel) panelColor = panel.color;
            if (starIcon) starIcon.gameObject.SetActive(false);
            headerButton?.onClick.AddListener(() =>
            {
                collapsed = !collapsed;
                if (stepsText) stepsText.gameObject.SetActive(!collapsed);
            });
            quests = QuestManager.Instance;
            if (quests)
            {
                quests.QuestChanged += Refresh;
                quests.StepCompleted += OnStepCompleted;
                quests.QuestCompleted += OnStepCompleted;
            }
            Refresh();
        }

        void OnDestroy()
        {
            if (!quests) return;
            quests.QuestChanged -= Refresh;
            quests.StepCompleted -= OnStepCompleted;
            quests.QuestCompleted -= OnStepCompleted;
        }

        void OnStepCompleted(QuestInstance _) { Refresh(); StartCoroutine(Celebrate()); }

        IEnumerator Celebrate()
        {
            if (starIcon)
            {
                starIcon.gameObject.SetActive(true);
                starIcon.color = new Color(1f, 0.82f, 0.2f);
            }
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float k = t / 0.6f;
                if (panel) panel.color = Color.Lerp(flashColor, panelColor, k);
                if (starIcon) starIcon.transform.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.7f);
                yield return null;
            }
            if (panel) panel.color = panelColor;
            if (starIcon) { starIcon.transform.localScale = Vector3.one; starIcon.gameObject.SetActive(false); }
        }

        void Refresh()
        {
            var quest = QuestManager.Instance?.CurrentQuest;
            if (quest == null)
            {
                if (titleText) titleText.text = "All quests done!";
                if (stepsText) stepsText.text = "The beach sparkles. Great work, Guardian!";
                return;
            }
            if (titleText) titleText.text = quest.Data.questTitle;
            if (stepsText == null) return;
            // ponytail: one rich-text block for steps, same as the shop/journal lists; row prefabs when art arrives
            var sb = new StringBuilder();
            foreach (var step in quest.Steps)
            {
                string box = step.isComplete ? "[x]" : "[ ]";
                string color = step.isComplete ? "#2E9E4F" : "#23404A";
                string count = step.targetAmount > 1 ? $" ({step.currentProgress}/{step.targetAmount})" : "";
                sb.AppendLine($"<color={color}>{box} {step.stepDescription}{count}</color>");
            }
            stepsText.text = sb.ToString();
        }
    }
}
