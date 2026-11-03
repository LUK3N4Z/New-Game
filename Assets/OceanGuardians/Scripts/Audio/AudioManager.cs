using UnityEngine;
using UnityEngine.UI;
using OceanGuardians.Core;
using OceanGuardians.Quests;

namespace OceanGuardians.Audio
{
    /// <summary>
    /// Cozy beach audio: ocean/gull ambience + gentle marimba loop + pooled one-shot SFX.
    /// Any clip left empty is synthesized procedurally at startup, so the game is audible
    /// with zero imported .wav files; assign real clips later and they take over.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Optional clip overrides (empty = procedural)")]
        [SerializeField] AudioClip wavesClip;
        [SerializeField] AudioClip musicClip;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] float sfxVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] float ambienceVolume = 0.55f;

        [Tooltip("Shoreline transform; the wave audio is emitted from here (3D). Empty = flat 2D ambience.")]
        [SerializeField] Transform wavesEmitter;

        const int Rate = 44100;
        AudioSource wavesSource, musicSource;
        AudioSource[] pool;
        AudioClip pickupPop, coinJingle, stepSand, stepWater, fanfare, buttonClick, gullCry, birdsong, craftChime, brassFanfare;
        float nextGullTime;

        public float MasterVolume { get => masterVolume; set { masterVolume = Mathf.Clamp01(value); AudioListener.volume = masterVolume; PlayerPrefs.SetFloat("vol_master", masterVolume); PlayerPrefs.Save(); } }
        public float MusicVolume { get => musicVolume; set { musicVolume = Mathf.Clamp01(value); if (musicSource) musicSource.volume = musicVolume; PlayerPrefs.SetFloat("vol_music", musicVolume); PlayerPrefs.Save(); } }
        public float SfxVolume { get => sfxVolume; set { sfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat("vol_sfx", sfxVolume); PlayerPrefs.Save(); } }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; } // persistent across F9 scene reloads
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // Saved settings (Pause menu sliders) win over the Inspector defaults.
            masterVolume = PlayerPrefs.GetFloat("vol_master", masterVolume);
            musicVolume = PlayerPrefs.GetFloat("vol_music", musicVolume);
            sfxVolume = PlayerPrefs.GetFloat("vol_sfx", sfxVolume);
            AudioListener.volume = masterVolume;

            wavesSource = MakeSource("WavesChannel", wavesClip ? wavesClip : MakeWaves(), ambienceVolume);
            if (wavesEmitter) { wavesSource.transform.position = wavesEmitter.position; wavesSource.spatialBlend = 0.7f; wavesSource.maxDistance = 80f; }
            musicSource = MakeSource("MusicChannel", musicClip ? musicClip : MakeMusic(), musicVolume);
            wavesSource.Play();
            musicSource.Play();

            pool = new AudioSource[8];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("SfxPool" + i);
                go.transform.SetParent(transform);
                pool[i] = go.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }

            pickupPop = Chirp("PickupPop", 900f, 1800f, 0.12f, 0.35f);
            coinJingle = Melody("CoinJingle", new[] { 1318f, 1568f, 2093f }, 0.09f, 0.5f, 0.3f);
            fanfare = Melody("RescueFanfare", new[] { 523f, 659f, 784f, 1047f }, 0.16f, 1.0f, 0.32f);
            buttonClick = Chirp("ButtonClick", 700f, 500f, 0.06f, 0.25f);
            gullCry = Chirp("GullCry", 1400f, 900f, 0.35f, 0.12f);
            birdsong = Melody("MorningBirdsong", new[] { 2093f, 2637f, 2349f, 3136f, 2637f, 3520f, 3136f, 2637f }, 0.14f, 1.6f, 0.14f);
            craftChime = Melody("CraftingChime", new[] { 1568f, 1175f, 1568f, 2093f }, 0.08f, 0.7f, 0.28f); // hammer-tap rhythm, assembly sparkle
            brassFanfare = Melody("BrassFanfare", new[] { 392f, 523f, 659f, 784f, 1047f }, 0.15f, 1.4f, 0.34f);
            stepSand = NoiseBurst("StepSand", 0.09f, 0.18f, 0.25f);
            stepWater = NoiseBurst("StepWater", 0.14f, 0.5f, 0.22f);
            nextGullTime = Time.time + Random.Range(6f, 14f);
        }

        AudioSource MakeSource(string name, AudioClip clip, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip; src.loop = true; src.playOnAwake = false; src.volume = volume;
            return src;
        }

        void OnEnable()
        {
            EventManager.LitterDeposited += OnDeposited;
            EventManager.WildlifeRescued += OnRescued;
        }

        void OnDisable()
        {
            EventManager.LitterDeposited -= OnDeposited;
            EventManager.WildlifeRescued -= OnRescued;
        }

        void Start()
        {
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include))
                button.onClick.AddListener(PlayButtonClick);
            if (QuestManager.Instance) QuestManager.Instance.QuestCompleted += _ => PlayRescueFanfare();
        }

        void Update()
        {
            if (Time.time >= nextGullTime) // distant gulls over the surf
            {
                Play(gullCry, Random.Range(0.9f, 1.1f));
                nextGullTime = Time.time + Random.Range(8f, 22f);
            }
        }

        void OnDeposited(int count) { if (count > 0) PlayCoinJingle(); }
        void OnRescued(string _) => PlayRescueFanfare();

        public void PlayPickupPop() => Play(pickupPop, Random.Range(0.95f, 1.08f));
        public void PlayCoinJingle() => Play(coinJingle);
        public void PlayRescueFanfare() => Play(fanfare);
        public void PlayButtonClick() => Play(buttonClick);
        public void PlayFootstep(bool isWater) => Play(isWater ? stepWater : stepSand, Random.Range(0.9f, 1.1f));
        public void PlayMorningBirdsong() => Play(birdsong);
        public void PlayCraftingChime() => Play(craftChime);
        public void PlayBrassFanfare() => Play(brassFanfare);

        void Play(AudioClip clip, float pitch = 1f)
        {
            if (clip == null || pool == null) return;
            foreach (var src in pool)
            {
                if (src.isPlaying) continue;
                src.pitch = pitch;
                src.PlayOneShot(clip, sfxVolume);
                return;
            }
        }

        // ---------- Procedural clips ----------

        static AudioClip Chirp(string name, float from, float to, float seconds, float gain)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                phase += 2f * Mathf.PI * Mathf.Lerp(from, to, k) / Rate;
                data[i] = Mathf.Sin(phase) * (1f - k) * gain;
            }
            return Wrap(name, data);
        }

        /// <summary>Sequenced decaying sine notes (tails wrap, so short melodies ring out fully).</summary>
        static AudioClip Melody(string name, float[] freqs, float stepSeconds, float seconds, float gain)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            for (int note = 0; note < freqs.Length; note++)
            {
                int start = (int)(note * stepSeconds * Rate);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * 6f);
                    if (env < 0.001f) break;
                    int at = (start + i) % n; // wrap: keeps the clip compact without clipping tails
                    data[at] += (Mathf.Sin(2f * Mathf.PI * freqs[note] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freqs[note] * t)) * env * gain;
                }
            }
            return Wrap(name, data);
        }

        /// <param name="brightness">One-pole lowpass coefficient: low = muffled (sand), high = splashy (water).</param>
        static AudioClip NoiseBurst(string name, float seconds, float brightness, float gain)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                low += brightness * (Random.Range(-1f, 1f) - low);
                data[i] = low * (1f - k) * gain;
            }
            return Wrap(name, data);
        }

        /// <summary>8s seamless surf: lowpassed noise swelled by a whole-cycle LFO, ends crossfaded.</summary>
        static AudioClip MakeWaves()
        {
            int n = Rate * 8;
            var data = new float[n];
            float low = 0f;
            for (int pass = 0; pass < 2; pass++) // second pass starts from the filter's settled state: loop-safe
                for (int i = 0; i < n; i++)
                {
                    low += 0.08f * (Random.Range(-1f, 1f) - low);
                    if (pass == 1) data[i] = low;
                }
            for (int i = 0; i < n; i++) // two swells per loop (integer cycles = no click)
                data[i] *= (0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 2f * i / n - Mathf.PI / 2f)) * 1.6f;
            int fade = Rate / 4;
            for (int i = 0; i < fade; i++)
                data[i] = Mathf.Lerp(data[n - fade + i], data[i], i / (float)fade);
            return Wrap("ProceduralWaves", data);
        }

        /// <summary>16s cozy marimba loop: C-Am-F-G plucked melody, tails wrapped so the loop is seamless.</summary>
        static AudioClip MakeMusic()
        {
            // ponytail: fixed 32-note loop, no music theory engine; swap musicClip for a real track later
            float[] melody =
            {
                523f, 659f, 784f, 659f, 440f, 523f, 659f, 523f,
                349f, 440f, 523f, 440f, 392f, 494f, 587f, 494f,
                523f, 659f, 784f, 1047f, 880f, 659f, 784f, 659f,
                698f, 523f, 587f, 440f, 523f, 0f, 392f, 0f,
            };
            return Melody("ProceduralMusic", melody, 0.5f, 16f, 0.16f);
        }

        static AudioClip Wrap(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
