using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OceanGuardians.Core;
using OceanGuardians.Environment;

namespace OceanGuardians.UI
{
    /// <summary>
    /// Region gate flow: confirm modal ("Clear the passage...?") -> scenic world-map teaser
    /// with Region 1 stamped MASTERED. Region 2 itself arrives in a later phase.
    /// </summary>
    public class RegionGateUI : MonoBehaviour
    {
        [SerializeField] GameObject confirmPanel;
        [SerializeField] TMP_Text confirmText;
        [SerializeField] Button confirmButton;
        [SerializeField] Button cancelButton;
        [SerializeField] GameObject mapPanel;
        [SerializeField] Button mapCloseButton;

        RegionGate gate;

        void OnEnable() => EventManager.RegionGatePrompt += OnPrompt;
        void OnDisable() => EventManager.RegionGatePrompt -= OnPrompt;

        void Start()
        {
            if (confirmPanel) confirmPanel.SetActive(false);
            if (mapPanel) mapPanel.SetActive(false);
            confirmButton?.onClick.AddListener(OnConfirm);
            cancelButton?.onClick.AddListener(() => { if (confirmPanel) confirmPanel.SetActive(false); });
            mapCloseButton?.onClick.AddListener(() => { if (mapPanel) mapPanel.SetActive(false); });
        }

        void OnPrompt(RegionGate g)
        {
            gate = g;
            if (confirmPanel == null) return;
            confirmPanel.SetActive(g != null);
            if (confirmText) confirmText.text = "Clear the passage and journey to the Rocky Shore?";
        }

        void OnConfirm()
        {
            if (confirmPanel) confirmPanel.SetActive(false);
            gate?.ConfirmJourney();
            if (mapPanel) mapPanel.SetActive(true);
        }
    }
}
