using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OceanGuardians.Core;
using OceanGuardians.Environment;
using OceanGuardians.Items;
using OceanGuardians.Player;
using OceanGuardians.Progression;

namespace OceanGuardians.UI
{
    /// <summary>
    /// The whole in-game HUD: top-bar currencies, bag gauge, gentle alerts, context prompts,
    /// floating pickup text, radar minimap, and the Recycling Kiosk modal (Quick Deposit + Upgrade Shop).
    /// Purely reactive: listens to EventManager and calls into systems only from button clicks.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Serializable]
        public class MinimapPin
        {
            public Transform target;
            public RectTransform icon;
        }

        [Header("Top Bar")]
        [SerializeField] TMP_Text creditsText;
        [SerializeField] TMP_Text coinsText;
        [SerializeField] TMP_Text ecoText;

        [Header("Bag Gauge")]
        [SerializeField] Image bagFill;
        [SerializeField] TMP_Text bagText;
        [SerializeField] Color bagFillColor = new(0.35f, 0.85f, 0.4f);
        [SerializeField] Color bagFullColor = new(1f, 0.6f, 0.25f);

        [Header("Alerts")]
        [SerializeField] TMP_Text alertText;
        [SerializeField] float alertSeconds = 2.8f;

        [Header("Context Prompts")]
        [SerializeField] GameObject grabberPrompt;
        [SerializeField] Button careButton;

        [Header("Floating Text")]
        [SerializeField] RectTransform floatingTextLayer;
        [SerializeField] Camera worldCamera;

        [Header("Kiosk Modal")]
        [SerializeField] GameObject kioskPanel;
        [SerializeField] Button depositTabButton;
        [SerializeField] Button shopTabButton;
        [SerializeField] Button closeButton;
        [SerializeField] GameObject depositPage;
        [SerializeField] GameObject shopPage;
        [SerializeField] Button quickDepositButton;
        [SerializeField] TMP_Text depositResultText;

        [Header("Upgrade Shop")]
        [SerializeField] TMP_Text nextStageText;
        [SerializeField] Button buyButton;
        [SerializeField] TMP_Text stageListText;

        [Header("Minimap")]
        [SerializeField] RectTransform minimapRoot;
        [Tooltip("World metres from the player to the radar's edge.")]
        [SerializeField] float minimapWorldRadius = 30f;
        [SerializeField] List<MinimapPin> minimapPins = new();

        RecyclingKiosk openKiosk;
        WildlifeRescue nearbyRescue;
        float alertHideTime;

        void OnEnable()
        {
            EventManager.CurrencyChanged += OnCurrencyChanged;
            EventManager.BagChanged += OnBagChanged;
            EventManager.ItemCollected += OnItemCollected;
            EventManager.Alert += ShowAlert;
            EventManager.TargetChanged += OnTargetChanged;
            EventManager.RescueNearby += OnRescueNearby;
            EventManager.KioskToggled += OnKioskToggled;
            EventManager.StagePurchased += OnStagePurchased;
        }

        void OnDisable()
        {
            EventManager.CurrencyChanged -= OnCurrencyChanged;
            EventManager.BagChanged -= OnBagChanged;
            EventManager.ItemCollected -= OnItemCollected;
            EventManager.Alert -= ShowAlert;
            EventManager.TargetChanged -= OnTargetChanged;
            EventManager.RescueNearby -= OnRescueNearby;
            EventManager.KioskToggled -= OnKioskToggled;
            EventManager.StagePurchased -= OnStagePurchased;
        }

        void Start()
        {
            if (worldCamera == null) worldCamera = Camera.main;

            var economy = EconomyManager.Instance;
            if (economy)
                foreach (CurrencyType c in Enum.GetValues(typeof(CurrencyType)))
                    OnCurrencyChanged(c, economy.Get(c));
            if (BagSystem.Instance) OnBagChanged(BagSystem.Instance.Current, BagSystem.Instance.MaxCapacity);

            alertText.text = "";
            grabberPrompt.SetActive(false);
            careButton.gameObject.SetActive(false);
            kioskPanel.SetActive(false);

            careButton.onClick.AddListener(OnCareClicked);
            depositTabButton.onClick.AddListener(() => ShowKioskPage(shop: false));
            shopTabButton.onClick.AddListener(() => ShowKioskPage(shop: true));
            closeButton.onClick.AddListener(() => openKiosk?.Close());
            quickDepositButton.onClick.AddListener(OnQuickDeposit);
            buyButton.onClick.AddListener(OnBuy);
        }

        void Update()
        {
            if (alertText.text.Length > 0 && Time.time > alertHideTime) alertText.text = "";
            UpdateMinimap();
        }

        // ---------- Top bar & bag ----------

        void OnCurrencyChanged(CurrencyType type, int total)
        {
            switch (type)
            {
                case CurrencyType.RecyclingCredits: creditsText.text = $"Credits: {total}"; break;
                case CurrencyType.CollectorCoins: coinsText.text = $"Finds: {total}"; break;
                case CurrencyType.EcoTokens: ecoText.text = $"Eco: {total}"; break;
            }
            if (kioskPanel.activeSelf) RefreshShop();
        }

        void OnBagChanged(int current, int max)
        {
            bagText.text = $"{current} / {max}";
            bagFill.fillAmount = max > 0 ? current / (float)max : 0f;
            bagFill.color = current >= max ? bagFullColor : bagFillColor;
        }

        void OnItemCollected(ItemData item, Vector3 worldPos)
        {
            string label = item is CollectibleData c ? $"+{c.coinValue} {item.displayName}" : $"+1 {item.displayName}";
            FloatingText.Spawn(floatingTextLayer, worldCamera, worldPos, label, item.popupColor);
        }

        void ShowAlert(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            alertText.text = message;
            alertHideTime = Time.time + alertSeconds;
        }

        // ---------- Context prompts ----------

        void OnTargetChanged(IInteractable target)
        {
            grabberPrompt.SetActive(target != null && target.Kind is InteractionKind.Litter or InteractionKind.Collectible);
        }

        void OnRescueNearby(WildlifeRescue rescue)
        {
            nearbyRescue = rescue;
            careButton.gameObject.SetActive(rescue != null);
        }

        void OnCareClicked()
        {
            var player = GameManager.Instance ? GameManager.Instance.Player : null;
            if (player && nearbyRescue) player.GetComponent<PlayerInteraction>().SetTarget(nearbyRescue);
        }

        // ---------- Kiosk modal ----------

        void OnKioskToggled(RecyclingKiosk kiosk)
        {
            openKiosk = kiosk;
            kioskPanel.SetActive(kiosk != null);
            if (kiosk == null) return;
            depositResultText.text = BagSystem.Instance && BagSystem.Instance.Current > 0
                ? $"You have {BagSystem.Instance.Current} items to sort."
                : "Your bag is empty.";
            ShowKioskPage(shop: false);
        }

        void ShowKioskPage(bool shop)
        {
            depositPage.SetActive(!shop);
            shopPage.SetActive(shop);
            if (shop) RefreshShop();
        }

        void OnQuickDeposit()
        {
            if (openKiosk) depositResultText.text = openKiosk.QuickDepositAll();
        }

        void OnBuy()
        {
            var progression = ProgressionManager.Instance;
            if (progression == null) return;
            progression.TryPurchaseNext(out var message);
            ShowAlert(message);
            RefreshShop();
        }

        void OnStagePurchased(EquipmentStage _) => RefreshShop();

        void RefreshShop()
        {
            var progression = ProgressionManager.Instance;
            if (progression == null || !shopPage.activeInHierarchy) return;

            var next = progression.NextStage;
            int credits = EconomyManager.Instance ? EconomyManager.Instance.Get(CurrencyType.RecyclingCredits) : 0;
            if (next == null)
            {
                nextStageText.text = "<b>All 50 stages complete!</b>\nYou are a true Ocean Guardian.";
                buyButton.interactable = false;
            }
            else
            {
                nextStageText.text = $"<b>Next: Stage {next.stage} - {next.name}</b>\n{next.description}\nCost: {next.creditCost} Credits";
                buyButton.interactable = credits >= next.creditCost;
            }

            // ponytail: one rich-text list for 50 rows; swap for pooled row prefabs when stages get icons/art
            var sb = new StringBuilder();
            var db = progression.Database;
            for (int s = 1; s <= db.Count; s++)
            {
                var stage = db.Get(s);
                string color = s <= progression.CurrentStage ? "#5CD65C" : s == progression.CurrentStage + 1 ? "#FFD24A" : "#9AA5B1";
                string status = s <= progression.CurrentStage ? "Owned" : $"{stage.creditCost} cr";
                sb.AppendLine($"<color={color}>{s:00}  {stage.name}  <size=80%>({status})</size></color>");
            }
            stageListText.text = sb.ToString();
        }

        // ---------- Minimap ----------

        void UpdateMinimap()
        {
            var gm = GameManager.Instance;
            if (minimapRoot == null || gm == null || gm.Player == null) return;

            Vector3 center = gm.Player.transform.position;
            float pxRadius = minimapRoot.rect.width * 0.5f;
            float scale = pxRadius / minimapWorldRadius;
            // Rotate so "up" on the radar matches "up" on screen, whatever the camera's yaw.
            float yaw = worldCamera ? worldCamera.transform.eulerAngles.y : 0f;
            var rot = Quaternion.Euler(0f, 0f, yaw);

            foreach (var pin in minimapPins)
            {
                if (pin.icon == null) continue;
                bool alive = pin.target != null && pin.target.gameObject.activeInHierarchy;
                pin.icon.gameObject.SetActive(alive);
                if (!alive) continue;

                var d = pin.target.position - center;
                Vector2 p = rot * new Vector3(d.x, d.z, 0f) * scale;
                pin.icon.anchoredPosition = Vector2.ClampMagnitude(p, pxRadius - 10f);
            }
        }
    }
}
