using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OceanGuardians.Core;
using OceanGuardians.Crafting;

namespace OceanGuardians.UI
{
    /// <summary>
    /// Upcycling Workshop modal: one row per recipe (built in code when the bench opens),
    /// green/red ingredient counts, and an Upcycle button that lights up when craftable.
    /// Refreshes on bag/collection/coin changes so counts stay live.
    /// </summary>
    public class CraftingUI : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] RectTransform rowsRoot;
        [SerializeField] Button closeButton;
        [SerializeField] TMP_Text resultText;

        CraftingWorkbench bench;
        readonly List<(CraftingRecipeData recipe, TMP_Text label, Button button)> rows = new();

        void OnEnable()
        {
            EventManager.CraftingToggled += OnToggled;
            EventManager.BagChanged += OnInventoryChanged;
            EventManager.CollectionChanged += RefreshRows;
            EventManager.ItemCrafted += OnCrafted;
        }

        void OnDisable()
        {
            EventManager.CraftingToggled -= OnToggled;
            EventManager.BagChanged -= OnInventoryChanged;
            EventManager.CollectionChanged -= RefreshRows;
            EventManager.ItemCrafted -= OnCrafted;
        }

        void Start()
        {
            if (panel) panel.SetActive(false);
            closeButton?.onClick.AddListener(() => bench?.Close());
        }

        void OnInventoryChanged(int _, int __) => RefreshRows();
        void OnCrafted(string _)
        {
            RefreshRows();
            SpeechBubbleManager.Instance?.SayCoral("Ta-da! Trash turned into treasure!");
        }

        void OnToggled(CraftingWorkbench workbench)
        {
            bench = workbench;
            if (panel == null) return;
            panel.SetActive(workbench != null);
            if (workbench == null) return;
            if (resultText) resultText.text = "Turn beach litter into treasures!";
            BuildRows();
        }

        void BuildRows()
        {
            rows.Clear();
            foreach (Transform child in rowsRoot) Destroy(child.gameObject);
            if (bench == null) return;

            foreach (var recipe in bench.Recipes)
            {
                var rowGo = new GameObject("Recipe_" + recipe.recipeId, typeof(RectTransform));
                var rt = (RectTransform)rowGo.transform;
                rt.SetParent(rowsRoot, false);
                rt.sizeDelta = new Vector2(760f, 120f);
                var bg = rowGo.AddComponent<Image>();
                bg.color = new Color(1f, 1f, 1f, 0.6f);

                var labelGo = new GameObject("Label", typeof(RectTransform));
                var labelRt = (RectTransform)labelGo.transform;
                labelRt.SetParent(rt, false);
                labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = new Vector2(16f, 8f); labelRt.offsetMax = new Vector2(-220f, -8f);
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.fontSize = 22f;
                label.color = new Color(0.15f, 0.22f, 0.27f);
                label.raycastTarget = false;

                var btnGo = new GameObject("Upcycle", typeof(RectTransform));
                var btnRt = (RectTransform)btnGo.transform;
                btnRt.SetParent(rt, false);
                btnRt.anchorMin = new Vector2(1f, 0.5f); btnRt.anchorMax = new Vector2(1f, 0.5f);
                btnRt.anchoredPosition = new Vector2(-110f, 0f); btnRt.sizeDelta = new Vector2(190f, 64f);
                var btnImg = btnGo.AddComponent<Image>();
                btnImg.color = new Color(0.25f, 0.75f, 0.35f);
                var button = btnGo.AddComponent<Button>();
                var btnLabelGo = new GameObject("Label", typeof(RectTransform));
                var btnLabelRt = (RectTransform)btnLabelGo.transform;
                btnLabelRt.SetParent(btnRt, false);
                btnLabelRt.anchorMin = Vector2.zero; btnLabelRt.anchorMax = Vector2.one;
                btnLabelRt.offsetMin = Vector2.zero; btnLabelRt.offsetMax = Vector2.zero;
                var btnLabel = btnLabelGo.AddComponent<TextMeshProUGUI>();
                btnLabel.text = "Upcycle";
                btnLabel.fontSize = 26f; btnLabel.fontStyle = FontStyles.Bold;
                btnLabel.alignment = TextAlignmentOptions.Center;
                btnLabel.raycastTarget = false;

                var captured = recipe;
                button.onClick.AddListener(() =>
                {
                    string message = "";
                    if (bench != null) bench.TryCraft(captured, out message);
                    if (resultText && !string.IsNullOrEmpty(message)) resultText.text = message;
                    RefreshRows();
                });
                rows.Add((recipe, label, button));
            }
            RefreshRows();
        }

        void RefreshRows()
        {
            if (bench == null || panel == null || !panel.activeSelf) return;
            foreach (var (recipe, label, button) in rows)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"<b>{recipe.recipeName}</b>  <size=80%>{recipe.recipeDescription}</size>");
                foreach (var cost in recipe.requiredLitter) AppendCost(sb, cost);
                foreach (var cost in recipe.requiredFinds) AppendCost(sb, cost);
                sb.Append($"-> +{recipe.rewardCollectorCoins} Collector Coins, +{recipe.rewardEcoTokens} Eco");
                if (bench.LifetimeCount(recipe.recipeId) > 0) sb.Append($"  (crafted x{bench.LifetimeCount(recipe.recipeId)})");
                label.text = sb.ToString();
                button.interactable = bench.CanCraft(recipe);
            }

            void AppendCost(StringBuilder sb, InventoryCost cost)
            {
                int owned = bench.OwnedCount(cost);
                string color = owned >= cost.quantity ? "#2E9E4F" : "#C0392B";
                sb.Append($"<color={color}>{cost.quantity}x {cost.item.displayName} ({owned}/{cost.quantity})</color>  ");
            }
        }
    }
}
