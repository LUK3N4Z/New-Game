using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using OceanGuardians.Audio;
using OceanGuardians.Core;

namespace OceanGuardians.UI
{
    /// <summary>
    /// Cozy pause/settings overlay: Escape or the HUD gear button freezes time
    /// (Time.timeScale = 0) and opens volume sliders (persisted in PlayerPrefs by
    /// AudioManager), a controls refresher, Quick Save, Resume, and Quit.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Button gearButton;
        [SerializeField] Button resumeButton;
        [SerializeField] Button quickSaveButton;
        [SerializeField] Button quitButton;
        [SerializeField] Slider masterSlider;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] TMP_Text masterValue;
        [SerializeField] TMP_Text musicValue;
        [SerializeField] TMP_Text sfxValue;

        public bool IsOpen => panel != null && panel.activeSelf;

        void Start()
        {
            if (panel) panel.SetActive(false);
            gearButton?.onClick.AddListener(() => SetPaused(true));
            resumeButton?.onClick.AddListener(() => SetPaused(false));
            quickSaveButton?.onClick.AddListener(() =>
            {
                SaveManager.Instance?.Save();
                EventManager.RaiseAlert("Game saved!");
                SetPaused(false);
            });
            quitButton?.onClick.AddListener(QuitGame);
            Hook(masterSlider, v => { if (AudioManager.Instance) AudioManager.Instance.MasterVolume = v; Show(masterValue, v); });
            Hook(musicSlider, v => { if (AudioManager.Instance) AudioManager.Instance.MusicVolume = v; Show(musicValue, v); });
            Hook(sfxSlider, v => { if (AudioManager.Instance) AudioManager.Instance.SfxVolume = v; Show(sfxValue, v); });
        }

        static void Show(TMP_Text label, float v) { if (label) label.text = $"{Mathf.RoundToInt(v * 100f)}%"; }

        void Hook(Slider slider, UnityEngine.Events.UnityAction<float> onChange)
        {
            if (slider == null) return;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.onValueChanged.AddListener(onChange);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) SetPaused(!IsOpen);
        }

        public void SetPaused(bool paused)
        {
            if (panel == null) return;
            panel.SetActive(paused);
            Time.timeScale = paused ? 0f : 1f;
            if (!paused) return;
            var audio = AudioManager.Instance;
            if (audio == null) return;
            // Sync sliders without firing their listeners back into PlayerPrefs churn.
            masterSlider?.SetValueWithoutNotify(audio.MasterVolume);
            musicSlider?.SetValueWithoutNotify(audio.MusicVolume);
            sfxSlider?.SetValueWithoutNotify(audio.SfxVolume);
            Show(masterValue, audio.MasterVolume);
            Show(musicValue, audio.MusicVolume);
            Show(sfxValue, audio.SfxVolume);
        }

        void QuitGame()
        {
            Time.timeScale = 1f;
            SaveManager.Instance?.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDestroy()
        {
            if (IsOpen) Time.timeScale = 1f; // never leave the game frozen
        }
    }
}
