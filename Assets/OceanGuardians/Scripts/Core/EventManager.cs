using System;
using UnityEngine;
using OceanGuardians.Items;
using OceanGuardians.Environment;
using OceanGuardians.Progression;

namespace OceanGuardians.Core
{
    /// <summary>What a clickable world object is, so the HUD can show the right context icon.</summary>
    public enum InteractionKind { Litter, Collectible, Kiosk, Rescue, Camp }

    /// <summary>
    /// Anything Coral can walk up to and use with a smart context click.
    /// Implemented by LitterItem, RecyclingKiosk and WildlifeRescue.
    /// </summary>
    public interface IInteractable
    {
        InteractionKind Kind { get; }
        Transform transform { get; }
        /// <summary>How close Coral must get (metres, flat distance) before Interact fires.</summary>
        float InteractionRange { get; }
        /// <summary>Return false with a kid-friendly reason to block the interaction (e.g. bag full).</summary>
        bool CanInteract(out string reason);
        void Interact(Player.PlayerInteraction player);
    }

    /// <summary>
    /// Central, decoupled event hub. Systems raise events here and never reference each other's UI.
    /// Static C# events: no assets to wire, and every handler is cleared on play-mode entry so
    /// "Enter Play Mode without Domain Reload" never leaves stale subscribers behind.
    /// </summary>
    public static class EventManager
    {
        /// <summary>(currency, newTotal)</summary>
        public static event Action<CurrencyType, int> CurrencyChanged;
        /// <summary>(currentSlots, maxSlots)</summary>
        public static event Action<int, int> BagChanged;
        /// <summary>(item, worldPosition) - fired when litter or a natural find is picked up.</summary>
        public static event Action<ItemData, Vector3> ItemCollected;
        /// <summary>Gentle on-screen message, e.g. "Bag is full!".</summary>
        public static event Action<string> Alert;
        public static event Action<JournalEntry> JournalUnlocked;
        public static event Action<EquipmentStage> StagePurchased;
        /// <summary>The object Coral is currently walking toward (null when none).</summary>
        public static event Action<IInteractable> TargetChanged;
        /// <summary>A rescue Coral is standing near (null when she walks away or it is done).</summary>
        public static event Action<WildlifeRescue> RescueNearby;
        /// <summary>Kiosk modal open (kiosk) / closed (null).</summary>
        public static event Action<RecyclingKiosk> KioskToggled;
        /// <summary>The natural-finds collection changed (journal view refreshes).</summary>
        public static event Action CollectionChanged;
        /// <summary>A meaningful moment happened (deposit, rescue) - SaveManager auto-saves.</summary>
        public static event Action Checkpoint;
        /// <summary>(itemCount) - litter sorted into the kiosk bins. Drives quest deposit steps.</summary>
        public static event Action<int> LitterDeposited;
        /// <summary>(rescueSaveId) - a Tender Care rescue finished. Drives quest rescue steps.</summary>
        public static event Action<string> WildlifeRescued;
        /// <summary>(areaId) - Coral entered a named place. Drives quest visit steps.</summary>
        public static event Action<string> AreaReached;
        /// <summary>Dusk phase began (Coral's "head back to camp" moment).</summary>
        public static event Action DuskFalls;
        /// <summary>(dayNumber) - the day ended; the Day Wrap-Up screen opens.</summary>
        public static event Action<int> DayEnded;
        /// <summary>(dayNumber) - a fresh morning began after sleeping at camp.</summary>
        public static event Action<int> DayStarted;

        public static void RaiseCurrencyChanged(CurrencyType type, int total) => CurrencyChanged?.Invoke(type, total);
        public static void RaiseBagChanged(int current, int max) => BagChanged?.Invoke(current, max);
        public static void RaiseItemCollected(ItemData item, Vector3 pos) => ItemCollected?.Invoke(item, pos);
        public static void RaiseAlert(string message) => Alert?.Invoke(message);
        public static void RaiseJournalUnlocked(JournalEntry entry) => JournalUnlocked?.Invoke(entry);
        public static void RaiseStagePurchased(EquipmentStage stage) => StagePurchased?.Invoke(stage);
        public static void RaiseTargetChanged(IInteractable target) => TargetChanged?.Invoke(target);
        public static void RaiseRescueNearby(WildlifeRescue rescue) => RescueNearby?.Invoke(rescue);
        public static void RaiseKioskToggled(RecyclingKiosk kiosk) => KioskToggled?.Invoke(kiosk);
        public static void RaiseCollectionChanged() => CollectionChanged?.Invoke();
        public static void RaiseCheckpoint() => Checkpoint?.Invoke();
        public static void RaiseLitterDeposited(int itemCount) => LitterDeposited?.Invoke(itemCount);
        public static void RaiseWildlifeRescued(string saveId) => WildlifeRescued?.Invoke(saveId);
        public static void RaiseAreaReached(string areaId) => AreaReached?.Invoke(areaId);
        public static void RaiseDuskFalls() => DuskFalls?.Invoke();
        public static void RaiseDayEnded(int dayNumber) => DayEnded?.Invoke(dayNumber);
        public static void RaiseDayStarted(int dayNumber) => DayStarted?.Invoke(dayNumber);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearAll()
        {
            CurrencyChanged = null; BagChanged = null; ItemCollected = null; Alert = null;
            JournalUnlocked = null; StagePurchased = null; TargetChanged = null;
            RescueNearby = null; KioskToggled = null; CollectionChanged = null; Checkpoint = null;
            LitterDeposited = null; WildlifeRescued = null; AreaReached = null;
            DuskFalls = null; DayEnded = null; DayStarted = null;
        }
    }
}
