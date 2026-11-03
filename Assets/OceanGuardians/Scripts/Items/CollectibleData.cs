using UnityEngine;

namespace OceanGuardians.Items
{
    /// <summary>A natural beach find (Sea Glass, Scallop Shell, Conch Shell). Pays Collector Coins and takes no bag space.</summary>
    [CreateAssetMenu(menuName = "Ocean Guardians/Collectible Data", fileName = "Find_")]
    public class CollectibleData : ItemData
    {
        public int coinValue = 1;
    }
}
