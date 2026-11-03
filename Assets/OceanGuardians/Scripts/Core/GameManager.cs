using UnityEngine;
using OceanGuardians.Player;

namespace OceanGuardians.Core
{
    /// <summary>
    /// Scene bootstrap. Lives on the "_Game" object next to the other managers
    /// (EconomyManager, BagSystem, ProgressionManager, OceanJournal) and gives
    /// everything one place to find the player.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] PlayerController player;
        [Tooltip("Mobile devices default to 30fps; 60 keeps tap-to-move feeling smooth.")]
        [SerializeField] int targetFrameRate = 60;

        public PlayerController Player => player;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = targetFrameRate;
            if (player == null) player = FindAnyObjectByType<PlayerController>();
        }
    }
}
