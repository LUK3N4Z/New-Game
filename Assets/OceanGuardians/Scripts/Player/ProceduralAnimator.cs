using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using OceanGuardians.Core;

namespace OceanGuardians.Player
{
    /// <summary>
    /// Cartoon life for greybox Coral: locomotion bob + tilt, squash on halting, a reach-down
    /// stretch and extended grabber tongs while picking up litter, and a joyful jump-stretch
    /// on quests/rescues. Also the Animator bridge: if a rigged model with an Animator is
    /// dropped in later, Speed / IsInteracting / Celebrate are fed automatically.
    /// Animates the "Visual" child only, so the NavMeshAgent root never leaves the ground.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(PlayerInteraction))]
    public class ProceduralAnimator : MonoBehaviour
    {
        [Tooltip("The model root that bobs. Defaults to a child named Visual, else this transform.")]
        [SerializeField] Transform visual;
        [Tooltip("Grabber tong mesh, stretched from Coral's hand to the litter while grabbing.")]
        [SerializeField] Transform grabberTong;
        [Tooltip("Optional rigged-model Animator (params: float Speed, bool IsInteracting, trigger Celebrate).")]
        [SerializeField] Animator animator;

        [Header("Bob")]
        [SerializeField] float bobHeight = 0.06f;
        [SerializeField] float bobFrequency = 9f;
        [SerializeField] float tiltDegrees = 3f;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int InteractingHash = Animator.StringToHash("IsInteracting");
        static readonly int CelebrateHash = Animator.StringToHash("Celebrate");

        NavMeshAgent agent;
        PlayerInteraction interaction;
        readonly HashSet<string> animParams = new();
        Vector3 visualBase;
        float bobPhase, squash; // squash: -1 (squashed) .. 1 (stretched), springs to 0
        float squashVelocity;
        float celebrateTime = -1f;
        bool wasMoving;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            interaction = GetComponent<PlayerInteraction>();
            if (visual == null)
            {
                var child = transform.Find("Visual");
                visual = child ? child : transform;
            }
            visualBase = visual.localPosition;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
                foreach (var p in animator.parameters) animParams.Add(p.name);
            if (grabberTong) grabberTong.gameObject.SetActive(false);
        }

        void OnEnable() => EventManager.WildlifeRescued += OnCelebrate;
        void OnDisable() => EventManager.WildlifeRescued -= OnCelebrate;
        void OnCelebrate(string _) => PlayCelebrate();

        void Start()
        {
            if (Quests.QuestManager.Instance)
                Quests.QuestManager.Instance.QuestCompleted += _ => PlayCelebrate();
        }

        /// <summary>Called by PlayerController's anim-trigger forwarding ("Grab" reach).</summary>
        public void PlayGrab() => squash = Mathf.Max(squash, 0.55f);

        /// <summary>Joyful jump-stretch (quest complete, rescue freed).</summary>
        public void PlayCelebrate()
        {
            celebrateTime = 0f;
            squash = 1f;
            if (animParams.Contains("Celebrate")) animator.SetTrigger(CelebrateHash);
        }

        void Update()
        {
            float speed = agent.velocity.magnitude;
            float speed01 = agent.speed > 0f ? Mathf.Clamp01(speed / agent.speed) : 0f;
            bool moving = speed > 0.25f;

            if (moving) bobPhase += Time.deltaTime * bobFrequency * (0.5f + speed01);
            if (wasMoving && !moving) squash = Mathf.Min(squash, -0.7f); // halt squash
            wasMoving = moving;

            // Springy squash & stretch toward neutral.
            squashVelocity += (-squash * 90f - squashVelocity * 12f) * Time.deltaTime;
            squash = Mathf.Clamp(squash + squashVelocity * Time.deltaTime, -1f, 1f);

            float jump = 0f;
            if (celebrateTime >= 0f)
            {
                celebrateTime += Time.deltaTime;
                float k = celebrateTime / 0.55f;
                if (k >= 1f) celebrateTime = -1f;
                else jump = Mathf.Sin(k * Mathf.PI) * 0.35f;
            }

            float bob = moving ? Mathf.Abs(Mathf.Sin(bobPhase)) * bobHeight * speed01 : 0f;
            float tilt = moving ? Mathf.Sin(bobPhase) * tiltDegrees * speed01 : 0f;
            visual.localPosition = visualBase + Vector3.up * (bob + jump);
            visual.localRotation = Quaternion.Euler(speed01 * 4f, 0f, tilt);
            visual.localScale = new Vector3(1f - squash * 0.09f, 1f + squash * 0.14f, 1f - squash * 0.09f);

            UpdateTong();
            UpdateAnimatorBridge(speed);
        }

        void UpdateTong()
        {
            if (grabberTong == null) return;
            var target = interaction.Target;
            bool grabbing = target != null && target.Kind is InteractionKind.Litter or InteractionKind.Collectible;
            if (!grabbing)
            {
                grabberTong.gameObject.SetActive(false);
                return;
            }
            var hand = transform.position + Vector3.up * 0.65f + transform.forward * 0.25f;
            var dest = target.transform.position + Vector3.up * 0.15f;
            var mid = (hand + dest) * 0.5f;
            float length = Vector3.Distance(hand, dest);
            grabberTong.gameObject.SetActive(true);
            grabberTong.position = mid;
            grabberTong.LookAt(dest);
            grabberTong.Rotate(90f, 0f, 0f); // cylinder axis is Y; aim it along the reach
            var s = grabberTong.localScale;
            grabberTong.localScale = new Vector3(s.x, length * 0.5f, s.z);
        }

        void UpdateAnimatorBridge(float speed)
        {
            if (animator == null) return;
            if (animParams.Contains("Speed")) animator.SetFloat(SpeedHash, speed);
            if (animParams.Contains("IsInteracting")) animator.SetBool(InteractingHash, interaction.Target != null);
        }
    }
}
