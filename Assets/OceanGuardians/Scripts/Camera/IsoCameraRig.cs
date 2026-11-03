using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using OceanGuardians.Player;

namespace OceanGuardians.CameraControl
{
    /// <summary>
    /// RTS-style polish for the fixed 55-degree Cinemachine camera:
    ///  - Mouse-wheel zoom, smoothed and clamped, sliding along the 55-degree line so the pitch never changes.
    ///  - Look-ahead: the camera tracks a target that leads Coral in her walking direction (smoothed).
    /// Obstacle/terrain clipping is handled by a CinemachineDeoccluder on the same camera
    /// (it pulls the camera forward along this same line, so pitch stays exact).
    ///
    /// Setup: CinemachineCamera (Follow = this rig's lead target) + CinemachineFollow (World Space binding)
    /// + CinemachineDeoccluder. Put this component on the CinemachineCamera.
    /// </summary>
    [RequireComponent(typeof(CinemachineFollow))]
    public class IsoCameraRig : MonoBehaviour
    {
        [SerializeField] Transform player;
        [Tooltip("Empty transform the CinemachineCamera follows. Created automatically if empty.")]
        [SerializeField] Transform leadTarget;

        [Header("Angle")]
        [SerializeField, Range(20f, 89f)] float pitch = 55f;

        [Header("Zoom")]
        [SerializeField] float minDistance = 12f;
        [SerializeField] float maxDistance = 36f;
        [SerializeField] float startDistance = 24f;
        [Tooltip("Distance change per wheel notch.")]
        [SerializeField] float zoomStep = 3f;
        [Tooltip("Seconds to ease into a new zoom level.")]
        [SerializeField] float zoomSmoothTime = 0.15f;

        [Header("Lead")]
        [Tooltip("How far ahead of Coral (metres, at full speed) the camera aims.")]
        [SerializeField] float leadDistance = 2f;
        [Tooltip("Seconds for the lead to catch up. Higher = lazier, floatier camera.")]
        [SerializeField] float leadSmoothTime = 0.5f;

        CinemachineFollow follow;
        NavMeshAgent agent;
        float targetDistance, distance, zoomVelocity;
        Vector3 lead, leadVelocity;

        void Awake()
        {
            follow = GetComponent<CinemachineFollow>();
            if (leadTarget == null) leadTarget = new GameObject("CameraLeadTarget").transform;
            if (player) agent = player.GetComponent<NavMeshAgent>();
            targetDistance = distance = Mathf.Clamp(startDistance, minDistance, maxDistance);
            if (player) leadTarget.position = player.position;
            ApplyOffset();
        }

        void Update()
        {
            if (player == null) return;
            ReadZoom();
            distance = Mathf.SmoothDamp(distance, targetDistance, ref zoomVelocity, zoomSmoothTime);
            ApplyOffset();

            // Lead in the direction Coral is moving, scaled by how fast she's going.
            Vector3 v = agent ? agent.velocity : Vector3.zero;
            v.y = 0f;
            float speed01 = agent && agent.speed > 0f ? Mathf.Clamp01(v.magnitude / agent.speed) : 0f;
            Vector3 desiredLead = v.sqrMagnitude > 0.001f ? v.normalized * (leadDistance * speed01) : Vector3.zero;
            lead = Vector3.SmoothDamp(lead, desiredLead, ref leadVelocity, leadSmoothTime);
            leadTarget.position = player.position + lead;
        }

        void ReadZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f) || PlayerController.IsOverUI(mouse.position.ReadValue())) return;
            // Notches arrive as +-120 on Windows and +-1 elsewhere; only the sign matters.
            targetDistance = Mathf.Clamp(targetDistance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);
        }

        void ApplyOffset()
        {
            float rad = pitch * Mathf.Deg2Rad;
            follow.FollowOffset = new Vector3(0f, Mathf.Sin(rad), -Mathf.Cos(rad)) * distance;
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f); // fixed: no rotation control on the vcam
        }
    }
}
