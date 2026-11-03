using UnityEngine;

namespace OceanGuardians.Core
{
    public enum CurrencyType
    {
        RecyclingCredits, // sorting / depositing trash -> equipment upgrades
        CollectorCoins,   // natural beach finds (sea glass, shells)
        EcoTokens         // rescues, journal entries, zone cleanup milestones
    }

    /// <summary>Tri-currency wallet. The only place balances change.</summary>
    [DefaultExecutionOrder(-100)]
    public class EconomyManager : MonoBehaviour
    {
        public static EconomyManager Instance { get; private set; }

        [Tooltip("Starting balance per currency, in CurrencyType order: Credits, Coins, Eco.")]
        [SerializeField] int[] startingBalances = new int[3];

        readonly int[] balances = new int[3];

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < balances.Length && i < startingBalances.Length; i++)
                balances[i] = startingBalances[i];
        }

        public int Get(CurrencyType type) => balances[(int)type];

        public void Add(CurrencyType type, int amount)
        {
            if (amount <= 0) return;
            balances[(int)type] += amount;
            EventManager.RaiseCurrencyChanged(type, balances[(int)type]);
        }

        /// <summary>Save-load: sets a balance directly.</summary>
        public void SetBalance(CurrencyType type, int amount)
        {
            balances[(int)type] = Mathf.Max(0, amount);
            EventManager.RaiseCurrencyChanged(type, balances[(int)type]);
        }

        /// <summary>Spends only if the full amount is available.</summary>
        public bool TrySpend(CurrencyType type, int amount)
        {
            if (amount < 0 || balances[(int)type] < amount) return false;
            balances[(int)type] -= amount;
            EventManager.RaiseCurrencyChanged(type, balances[(int)type]);
            return true;
        }
    }
}
