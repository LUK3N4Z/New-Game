using System.Collections;
using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Progression;

namespace OceanGuardians.Player
{
    /// <summary>
    /// Coral's look evolves with her equipment stage:
    ///  Stage 1: brown canvas bag, short tongs, brown boots.
    ///  Stage 2: bigger teal reinforced backpack (bag objects swap; the stage-1 model is the 1.3x scale reference).
    ///  Stage 3: silver telescopic grabber (material swap on the tong; ProgressionManager adds the +40% reach).
    ///  Stage 5: aqua wading boots.
    /// Buying any stage triggers a happy spin of the Visual + a starburst particle pop.
    /// </summary>
    public class PlayerVisualEquipment : MonoBehaviour
    {
        [Header("Bag (Stage 1 brown / Stage 2 teal reinforced)")]
        [SerializeField] GameObject bagStage1;
        [SerializeField] GameObject bagStage2;

        [Header("Boots (Stage 1 brown / Stage 5 aqua waders)")]
        [SerializeField] GameObject bootsStage1;
        [SerializeField] GameObject bootsStage5;

        [Header("Grabber tong materials (Stage 1 / Stage 3 telescopic silver)")]
        [SerializeField] Renderer grabberRenderer;
        [SerializeField] Material tongStage1Material;
        [SerializeField] Material tongStage3Material;

        [Header("Celebration")]
        [SerializeField] Transform visual;
        [SerializeField] ParticleSystem upgradeSparkle;

        Coroutine spin;

        void Start()
        {
            if (visual == null) visual = transform.Find("Visual");
            Apply(ProgressionManager.Instance ? ProgressionManager.Instance.CurrentStage : 1);
        }

        void OnEnable() => EventManager.StagePurchased += OnStagePurchased;
        void OnDisable() => EventManager.StagePurchased -= OnStagePurchased;

        void OnStagePurchased(EquipmentStage stage)
        {
            Apply(stage.stage);
            if (upgradeSparkle) upgradeSparkle.Play();
            GetComponent<ProceduralAnimator>()?.PlayCelebrate();
            if (spin != null) StopCoroutine(spin);
            spin = StartCoroutine(Spin());
        }

        /// <summary>Reflects a stage on the model instantly (also used on save-load via Start).</summary>
        public void Apply(int stage)
        {
            if (bagStage1) bagStage1.SetActive(stage < 2);
            if (bagStage2) bagStage2.SetActive(stage >= 2);
            if (bootsStage1) bootsStage1.SetActive(stage < 5);
            if (bootsStage5) bootsStage5.SetActive(stage >= 5);
            if (grabberRenderer)
            {
                var mat = stage >= 3 ? tongStage3Material : tongStage1Material;
                if (mat) grabberRenderer.sharedMaterial = mat;
            }
        }

        IEnumerator Spin()
        {
            var proc = GetComponent<ProceduralAnimator>();
            if (proc == null) yield break;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                proc.ExtraYaw = 360f * Mathf.SmoothStep(0f, 1f, t / 0.6f);
                yield return null;
            }
            proc.ExtraYaw = 0f;
            spin = null;
        }
    }
}
