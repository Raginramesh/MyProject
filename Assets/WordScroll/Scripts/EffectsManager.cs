using UnityEngine;
using UnityEngine.UI; // For CanvasGroup if used on prefab
using TMPro; // For TextMeshProUGUI
using DG.Tweening; // For DOTween animations
using System.Collections.Generic; // For List
using System; // For Action<int>

public class EffectsManager : MonoBehaviour
{
    [Header("Fly-To-Score Effect Config")]
    [Tooltip("The prefab used for the flying letter animation. Must have RectTransform, TextMeshProUGUI, CanvasGroup.")]
    [SerializeField] private GameObject flyingLetterPrefab;
    [Tooltip("The parent transform (usually the Canvas) for instantiated flying letters.")]
    [SerializeField] private Transform flyingLetterParent;
    [Tooltip("The RectTransform of the score text UI element (the target).")]
    [SerializeField] private RectTransform scoreTextRect;
    [Tooltip("Explicit starting scale for the flying letter prefab.")]
    [SerializeField] private Vector3 initialFlyingLetterScale = Vector3.one;
    [Tooltip("Vertical distance the letter floats up initially.")]
    [SerializeField] private float flyUpDistance = 30f;
    [Tooltip("Duration of the initial float up animation.")]
    [SerializeField] private float flyUpDuration = 0.2f;
    [Tooltip("How long the letter pauses after floating up before flying to score.")]
    [SerializeField] private float floatDuration = 0.3f;
    [Tooltip("Duration of the flight towards the score text.")]
    [SerializeField] private float flyToScoreDuration = 0.5f;
    [Tooltip("Delay between each letter starting its animation sequence.")]
    [SerializeField] private float delayBetweenLetters = 0.08f;
    [Tooltip("Ease type for the fly-to-score movement.")]
    [SerializeField] private Ease flyEase = Ease.InOutQuad;

    // --- Animation State Tracking ---
    /// <summary>
    /// Gets whether the fly-to-score effect is currently playing.
    /// </summary>
    public bool IsAnimating { get; private set; } = false;

    // --- Other Effect Configs (e.g., Particle Prefabs, Sound Clips) ---


    void Awake()
    {
        IsAnimating = false; // Ensure state is false on awake

        // Validate essential references for Fly-To-Score
        if (flyingLetterPrefab == null) Debug.LogError("EffectsManager: Flying Letter Prefab missing!", this);
        if (flyingLetterParent == null) Debug.LogError("EffectsManager: Flying Letter Parent missing!", this);
        if (scoreTextRect == null) Debug.LogError("EffectsManager: Score Text Rect missing!", this);
        // Add validation for other effects as needed
    }

    /// <summary>
    /// Plays the visual effect for letters flying from source cells to the score text.
    /// Triggers a callback for each letter animation completion with its score value.
    /// Sets the IsAnimating flag during the effect.
    /// </summary>
    /// <param name="sourceCellRects">List of RectTransforms of the original cells (for position).</param>
    /// <param name="word">The word being animated.</param>
    /// <param name="letterScores">List of score values for each corresponding letter in the word.</param>
    /// <param name="onLetterScoreCallback">Action invoked when EACH letter animation ends, passing its score value.</param>
    public void PlayFlyToScoreEffect(
        List<RectTransform> sourceCellRects,
        string word,
        List<int> letterScores,
        Action<int> onLetterScoreCallback)
    {
        // --- Pre-flight Checks ---
        if (IsAnimating)
        {
            Debug.LogWarning("EffectsManager: PlayFlyToScoreEffect called while already animating. Ignoring.");
            return; // Don't start a new animation if one is running
        }
        // Validate parameters
        if (flyingLetterPrefab == null || scoreTextRect == null || flyingLetterParent == null ||
            sourceCellRects == null || sourceCellRects.Count == 0 || letterScores == null || string.IsNullOrEmpty(word) ||
            word.Length != sourceCellRects.Count || word.Length != letterScores.Count)
        {
            Debug.LogError("EffectsManager: Cannot PlayFlyToScoreEffect - invalid parameters or mismatched list counts. " +
                           $"Word Length: {word?.Length}, Source Rects: {sourceCellRects?.Count}, Scores: {letterScores?.Count}", this);
            return;
        }

        // --- Start Animation ---
        IsAnimating = true; // Set animation flag
        // Debug.Log($"EffectsManager: Playing Fly-To-Score for word: {word}. IsAnimating = true.");

        Vector3 targetPosition = scoreTextRect.position; // World position of the score text target
        Sequence masterSequence = DOTween.Sequence(); // Manages all letter animations together
        int lettersToAnimate = word.Length; // Use word length, assuming lists match counts

        for (int i = 0; i < lettersToAnimate; i++)
        {
            // Capture loop variables for the closure to ensure correct values are used in callbacks
            int currentIndex = i;
            int scoreForThisLetter = letterScores[currentIndex]; // Get score for this specific letter

            RectTransform sourceRect = sourceCellRects[currentIndex];
            if (sourceRect == null)
            {
                Debug.LogWarning($"EffectsManager: Source RectTransform at index {currentIndex} is null. Skipping letter '{word[currentIndex]}'.");
                continue; // Skip this letter if its source position is missing
            }
            char letterChar = word[currentIndex];

            // --- Instantiate and Setup Clone ---
            GameObject cloneGO = Instantiate(flyingLetterPrefab, flyingLetterParent);
            RectTransform cloneRect = cloneGO.GetComponent<RectTransform>();
            TextMeshProUGUI cloneText = cloneGO.GetComponentInChildren<TextMeshProUGUI>();
            CanvasGroup cloneCanvasGroup = cloneGO.GetComponent<CanvasGroup>(); // Needed for fading

            // Validate essential components on the prefab instance
            if (cloneRect == null || cloneText == null || cloneCanvasGroup == null)
            {
                Debug.LogError($"EffectsManager: FlyingLetter Prefab '{flyingLetterPrefab.name}' instance is missing RectTransform, TextMeshProUGUI, or CanvasGroup!", cloneGO);
                Destroy(cloneGO); // Clean up invalid instance
                continue;
            }

            // --- Initialize Clone State ---
            cloneRect.position = sourceRect.position; // Match world position of the source cell
            cloneRect.localScale = initialFlyingLetterScale; // Use the configured initial scale
            cloneText.text = letterChar.ToString(); // Set the letter
            cloneCanvasGroup.alpha = 1f; // Ensure it's visible
            cloneRect.SetAsLastSibling(); // Render on top of other UI elements in the parent

            // --- Create Animation Sequence for this Clone ---
            Sequence cloneSequence = DOTween.Sequence();
            // 1. Float Up: Move vertically
            cloneSequence.Append(cloneRect.DOMoveY(cloneRect.position.y + flyUpDistance, flyUpDuration).SetEase(Ease.OutQuad));
            // 2. Pause: Wait for floatDuration
            cloneSequence.AppendInterval(floatDuration);
            // 3. Fly To Score: Move towards the target score text position
            cloneSequence.Append(cloneRect.DOMove(targetPosition, flyToScoreDuration).SetEase(flyEase));

            // Insert Scale and Fade animations to happen *during* the Fly To Score part (step 3)
            float flyStartTime = flyUpDuration + floatDuration; // Calculate when the fly-to-score movement begins
            // Scale down from initial scale to zero during the flight
            cloneSequence.Insert(flyStartTime, cloneRect.DOScale(Vector3.zero, flyToScoreDuration).SetEase(flyEase));
            // Fade out during the flight (can adjust duration multiplier for faster/slower fade)
            cloneSequence.Insert(flyStartTime, cloneCanvasGroup.DOFade(0f, flyToScoreDuration * 0.8f).SetEase(Ease.InQuad));

            // --- Callback and Cleanup for Individual Letter ---
            cloneSequence.OnComplete(() => {
                // Invoke the callback passed from GameManager with the score for THIS letter
                onLetterScoreCallback?.Invoke(scoreForThisLetter);

                // Destroy the clone GameObject when its animation finishes
                if (cloneGO != null) Destroy(cloneGO);
            });

            // Add this clone's sequence to the master sequence with a per-letter delay
            masterSequence.Insert(i * delayBetweenLetters, cloneSequence);
        }

        // --- Master Sequence Completion ---
        // Set IsAnimating to false ONLY when the *entire* master sequence finishes
        masterSequence.OnComplete(() => {
            IsAnimating = false;
            // Debug.Log("EffectsManager: Fly-To-Score Master Sequence Complete. IsAnimating = false.");
        });

        masterSequence.Play(); // Start the entire animation process
    }

    // --- Add other PlayEffect methods here as needed ---
    // Example: public void PlayExplosionVFX(Vector3 position) { /* Instantiate particle system */ }
    // Example: public void PlaySoundEffect(AudioClip clip) { /* Play audio */ }

} // End of EffectsManager class