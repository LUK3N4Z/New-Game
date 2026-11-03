using UnityEngine;

namespace OceanGuardians.Items
{
    /// <summary>
    /// A natural beach find (Sea Glass, Scallop Shell, Conch Shell). Pays Collector Coins, never takes
    /// bag space, and is kept in the separate collection shown in the Ocean Journal.
    /// </summary>
    [CreateAssetMenu(menuName = "Ocean Guardians/Collectible Item", fileName = "Find_")]
    public class CollectibleItemData : ItemData
    {
        public int coinValue = 1;
    }
}
