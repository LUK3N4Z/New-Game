using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using OceanGuardians.Core;

namespace OceanGuardians.Player
{
    /// <summary>
    /// Unified click/tap-to-move. One code path for PC and mobile:
    ///  - Left click / tap (or right click, League-style) on ground -> green ripple + pathfind there.
    ///  - Hold the button and drag -> keeps re-pathing under the cursor.
    ///  - Click on litter / kiosk / rescue -> hands the target to PlayerInteraction (smart context click).
    /// Uses Pointer.current from the Input System, which is the mouse on PC and the touchscreen on mobile.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(PlayerInteraction))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [Tooltip("Layers clicks can hit (ground + interactables).")]
        [SerializeField] LayerMask clickMask = ~0;
        [Tooltip("How far from a clicked point we search for walkable NavMesh.")]
        [SerializeField] float navSnapRadius = 2f;
        [Tooltip("While the button is held, re-path this often (seconds).")]
        [SerializeField] float holdRepathInterval = 0.12f;

        [Header("Click Marker")]
        [SerializeField] Color markerColor = new(0.35f, 0.95f, 0.45f, 1f);
        [Tooltip("Unlit/Sprites material for the ripple. Assigning one guarantees it ships in builds.")]
        [SerializeField] Material markerMaterial;

        [Header("Animation (optional)")]
        [Tooltip("Expected params: float Speed, trigger Grab, bool Crouch, trigger Snip, trigger Celebrate. Missing ones are skipped.")]
        [SerializeField] Animator animator;

        static readonly List<RaycastResult> uiHits = new();
        static readonly int SpeedHash = Animator.StringToHash("Speed");

        NavMeshAgent agent;
        PlayerInteraction interaction;
        readonly HashSet<string> animParams = new();
        bool holding;
        float nextRepathTime;
        [Header("Footsteps")]
        [Tooltip("Metres walked between footstep sounds (sand crunch vs tidepool/wash splash).")]
        [SerializeField] float strideLength = 1.6f;
        float strideTravelled;
        Vector3 lastPos;

        /// <summary>True during scripted moments (e.g. the Tender Care rescue) to ignore player input.</summary>
        public bool InputLocked { get; set; }
        public NavMeshAgent Agent => agent;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            interaction = GetComponent<PlayerInteraction>();
            lastPos = transform.position;
            if (cam == null) cam = Camera.main;
            if (animator != null)
                foreach (var p in animator.parameters) animParams.Add(p.name);
        }

        void Update()
        {
            if (animParams.Contains("Speed")) animator.SetFloat(SpeedHash, agent.velocity.magnitude);
            UpdateFootsteps();
            if (!InputLocked) HandlePointer();
        }

        void HandlePointer()
        {
            // Two-finger gesture = camera pinch-zoom (IsoCameraRig), never a move order.
            var touch = Touchscreen.current;
            if (touch != null && (touch.touches[0].isInProgress && touch.touches[1].isInProgress))
            {
                holding = false;
                return;
            }
            var pointer = Pointer.current;
            if (pointer == null || cam == null) return;

            var mouse = Mouse.current;
            bool pressed = pointer.press.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame);
            bool held = pointer.press.isPressed || (mouse != null && mouse.rightButton.isPressed);
            Vector2 screenPos = pointer.position.ReadValue();

            if (pressed)
            {
                holding = !IsOverUI(screenPos);
                if (holding) HandleClick(screenPos, initialPress: true);
            }
            else if (holding && held && Time.time >= nextRepathTime && interaction.Target == null)
            {
                HandleClick(screenPos, initialPress: false);
            }

            if (!held) holding = false;
        }

        void HandleClick(Vector2 screenPos, bool initialPress)
        {
            nextRepathTime = Time.time + holdRepathInterval;
            var ray = cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 500f, clickMask, QueryTriggerInteraction.Collide)) return;

            // Smart context click: only on the initial press so dragging never grabs things by accident.
            if (initialPress)
            {
                var target = hit.collider.GetComponentInParent<IInteractable>();
                if (target != null) { interaction.SetTarget(target); return; }
            }

            if (NavMesh.SamplePosition(hit.point, out var navHit, navSnapRadius, NavMesh.AllAreas))
            {
                interaction.ClearTarget();
                agent.SetDestination(navHit.position);
                if (initialPress) ClickMarker.Spawn(navHit.position, markerColor, markerMaterial);
            }
        }

        void UpdateFootsteps()
        {
            // Distance-based stride: splash inside tidepools/wave wash, soft crunch on sand.
            strideTravelled += Vector3.Distance(transform.position, lastPos);
            lastPos = transform.position;
            if (strideTravelled < strideLength || agent.velocity.sqrMagnitude < 0.04f) return;
            strideTravelled = 0f;
            Audio.AudioManager.Instance?.PlayFootstep(Environment.WaterZone.IsWet(transform.position));
        }

        /// <summary>UI raycast at the exact pointer position (reliable for touch on the press frame).
        /// Equivalent to EventSystem.IsPointerOverGameObject(touchId) but works for mouse, pen and touch alike.</summary>
        public static bool IsOverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            uiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screenPos }, uiHits);
            return uiHits.Count > 0;
        }

        /// <summary>Pathfind to the walkable point nearest to <paramref name="worldPos"/>.</summary>
        public bool MoveTo(Vector3 worldPos)
        {
            if (!NavMesh.SamplePosition(worldPos, out var navHit, 3f, NavMesh.AllAreas)) return false;
            agent.isStopped = false;
            return agent.SetDestination(navHit.position);
        }

        public void StopMoving()
        {
            if (agent.isOnNavMesh) agent.ResetPath();
        }

        public void FaceTowards(Vector3 worldPos)
        {
            var dir = worldPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
        }

        public void SetAnimTrigger(string name)
        {
            if (animParams.Contains(name)) animator.SetTrigger(name);
        }

        public void SetAnimBool(string name, bool value)
        {
            if (animParams.Contains(name)) animator.SetBool(name, value);
        }
    }
}
