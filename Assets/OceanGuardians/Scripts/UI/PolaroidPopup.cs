using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OceanGuardians.Core;
using OceanGuardians.Progression;

namespace OceanGuardians.UI
{
    /// <summary>
    /// Corner "snapshot" card shown when a new Ocean Journal entry unlocks.
    /// Slides in, shows the kid-friendly fact, slides out. Multiple unlocks queue up.
    /// Put this on an always-active object; the card itself is hidden with its CanvasGroup.
    /// </summary>
    public class PolaroidPopup : MonoBehaviour
    {
        [SerializeField] RectTransform card;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image photo;
        [Tooltip("Shown in the photo frame until real photos are assigned on the JournalEntry.")]
        [SerializeField] Color placeholderPhotoColor = new(0.45f, 0.8f, 0.85f);
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text categoryText;
        [SerializeField] TMP_Text factText;

        [SerializeField] float showSeconds = 5f;
        [SerializeField] float slideSeconds = 0.35f;
        [Tooltip("Offset from the shown position where the card slides in from.")]
        [SerializeField] Vector2 hiddenOffset = new(450f, 0f);

        readonly Queue<JournalEntry> queue = new();
        Vector2 shownPos;
        bool running;

        void Awake()
        {
            shownPos = card.anchoredPosition;
            group.alpha = 0f;
            group.blocksRaycasts = false; // never steal taps from the world
        }

        void OnEnable() => EventManager.JournalUnlocked += Enqueue;
        void OnDisable() => EventManager.JournalUnlocked -= Enqueue;

        void Enqueue(JournalEntry entry)
        {
            queue.Enqueue(entry);
            if (!running) StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            running = true;
            while (queue.Count > 0)
            {
                var e = queue.Dequeue();
                titleText.text = $"New Journal Entry #{e.number}\n<size=120%><b>{e.title}</b></size>";
                categoryText.text = e.CategoryLabel;
                factText.text = e.fact;
                photo.sprite = e.photo;
                photo.color = e.photo ? Color.white : placeholderPhotoColor;

                yield return Slide(0f, 1f);
                yield return new WaitForSecondsRealtime(showSeconds);
                yield return Slide(1f, 0f);
            }
            running = false;
        }

        IEnumerator Slide(float from, float to)
        {
            for (float t = 0f; t < slideSeconds; t += Time.unscaledDeltaTime)
            {
                Apply(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / slideSeconds)));
                yield return null;
            }
            Apply(to);
        }

        void Apply(float shown)
        {
            group.alpha = shown;
            card.anchoredPosition = shownPos + hiddenOffset * (1f - shown);
        }
    }
}
