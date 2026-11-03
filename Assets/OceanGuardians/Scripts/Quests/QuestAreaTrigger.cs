using UnityEngine;
using OceanGuardians.Core;

namespace OceanGuardians.Quests
{
    /// <summary>Raises EventManager.AreaReached(areaId) once each time Coral walks into the radius.</summary>
    public class QuestAreaTrigger : MonoBehaviour
    {
        [SerializeField] string areaId = "wildlife_cove";
        [SerializeField] float radius = 6f;
        [SerializeField] Transform player;

        bool inside;

        void Start()
        {
            if (player == null && GameManager.Instance && GameManager.Instance.Player)
                player = GameManager.Instance.Player.transform;
        }

        void Update()
        {
            if (player == null) return;
            var d = player.position - transform.position;
            d.y = 0f;
            bool now = d.magnitude <= radius;
            if (now && !inside) EventManager.RaiseAreaReached(areaId);
            inside = now;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.6f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
