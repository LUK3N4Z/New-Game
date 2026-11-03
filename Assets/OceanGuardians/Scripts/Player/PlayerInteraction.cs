using System.Collections;
using UnityEngine;
using OceanGuardians.Core;

namespace OceanGuardians.Player
{
    /// <summary>
    /// The "smart" half of a context click: walks Coral to the target, stops at its interaction
    /// range (grabber range for litter), plays the reach, then calls IInteractable.Interact.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerInteraction : MonoBehaviour
    {
        [Tooltip("Time between the grabber reach starting and the item popping into the bag.")]
        [SerializeField] float grabDelay = 0.25f;
        [SerializeField] AudioSource audioSource;
        [Tooltip("Optional. A soft procedural chirp is generated if empty.")]
        [SerializeField] AudioClip defaultPickupSound;

        static AudioClip generatedChirp;

        PlayerController controller;
        IInteractable target;
        bool busy;

        public IInteractable Target => target;
        public PlayerController Controller => controller;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        /// <summary>Start walking to an interactable. Blocked targets show their reason (e.g. bag full) right away.</summary>
        public void SetTarget(IInteractable newTarget)
        {
            if (busy || newTarget == null) return;
            if (!newTarget.CanInteract(out var reason))
            {
                EventManager.RaiseAlert(reason);
                return;
            }
            target = newTarget;
            controller.MoveTo(newTarget.transform.position);
            EventManager.RaiseTargetChanged(target);
        }

        public void ClearTarget()
        {
            if (target == null) return;
            target = null;
            EventManager.RaiseTargetChanged(null);
        }

        void Update()
        {
            if (target == null || busy) return;
            if ((Object)target == null) { ClearTarget(); return; } // destroyed while walking

            var delta = target.transform.position - transform.position;
            delta.y = 0f;
            if (delta.magnitude <= target.InteractionRange) StartCoroutine(Use(target));
        }

        IEnumerator Use(IInteractable t)
        {
            busy = true;
            controller.StopMoving();
            controller.FaceTowards(t.transform.position);

            if (t.Kind is InteractionKind.Litter or InteractionKind.Collectible)
            {
                controller.SetAnimTrigger("Grab");
                yield return new WaitForSeconds(grabDelay);
            }

            ClearTarget();
            if ((Object)t != null) t.Interact(this);
            busy = false;
        }

        public void PlayPickupSound(AudioClip clip)
        {
            if (clip == null && Audio.AudioManager.Instance) { Audio.AudioManager.Instance.PlayPickupPop(); return; }
            if (audioSource == null) return;
            if (clip == null) clip = defaultPickupSound;
            if (clip == null) clip = GetChirp();
            audioSource.PlayOneShot(clip);
        }

        /// <summary>Placeholder "pop": a 0.12s rising sine chirp, so pickups have audio before real SFX exist.</summary>
        static AudioClip GetChirp()
        {
            if (generatedChirp != null) return generatedChirp;
            const int rate = 44100;
            const float length = 0.12f;
            int samples = (int)(rate * length);
            var data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float k = i / (float)samples;
                phase += 2f * Mathf.PI * Mathf.Lerp(900f, 1800f, k) / rate;
                data[i] = Mathf.Sin(phase) * (1f - k) * 0.35f;
            }
            generatedChirp = AudioClip.Create("PickupChirp", samples, 1, rate, false);
            generatedChirp.SetData(data, 0);
            return generatedChirp;
        }
    }
}
