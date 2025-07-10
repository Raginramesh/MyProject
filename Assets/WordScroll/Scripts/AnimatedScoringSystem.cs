using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using System.Linq;
using WordScroll.Modifiers;

/// <summary>
/// Comprehensive animated scoring system that handles the complete scoring flow:
/// 1. Cell float animation
/// 2. Letter-by-letter score increment
/// 3. Modifier display and animation
/// 4. Score transfer from current to total
/// </summary>
public class AnimatedScoringSystem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI currentScoreText;
    [SerializeField] private TextMeshProUGUI totalScoreText;
    [SerializeField] private RectTransform currentScoreTransform;
    [SerializeField] private RectTransform totalScoreTransform;
    
    [Header("Cell Float Animation")]
    [SerializeField] private float cellFloatHeight = 20f;
    [SerializeField] private float cellFloatDuration = 0.3f;
    [SerializeField] private Ease cellFloatEase = Ease.OutQuad;
    [SerializeField] private Color cellFloatHighlightColor = Color.yellow;
    
    [Header("Letter Score Animation")]
    [SerializeField] private float letterScoreDelay = 0.15f;
    [SerializeField] private float letterIncrementSpeed = 20f; // Points per second
    [SerializeField] private Color letterScoreColor = Color.white;
    [SerializeField] private float letterScorePulseScale = 1.2f;
    [SerializeField] private float letterScorePulseDuration = 0.2f;
    
    [Header("Modifier Display Animation")]
    [SerializeField] private float modifierDisplayDelay = 0.5f;
    [SerializeField] private Color modifierColor = Color.green;
    [SerializeField] private float modifierPulseScale = 1.3f;
    [SerializeField] private float modifierPulseDuration = 0.3f;
    [SerializeField] private float modifierShowDuration = 1.5f;
    
    [Header("Score Transfer Animation")]
    [SerializeField] private float scoreTransferSpeed = 30f; // Points per second
    [SerializeField] private float scoreTransferMinDelay = 0.02f;
    [SerializeField] private Color transferHighlightColor = Color.cyan;
    [SerializeField] private float transferPulseInterval = 0.1f; // Pulse every X seconds during transfer
    
    [Header("Intersection Bonus")]
    [Tooltip("Fallback color for intersection display - will use WordGridManager's intersection color if available")]
    [SerializeField] private Color intersectionColor = Color.magenta;
    [SerializeField] private float intersectionPulseScale = 1.4f;
    [SerializeField] private float intersectionDisplayDuration = 1f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip letterScoreSound;
    [SerializeField] private AudioClip modifierSound;
    [SerializeField] private AudioClip transferSound;
    [SerializeField] private AudioClip intersectionSound;
    
    // Private state
    private int currentDisplayedScore = 0;
    private int totalScore = 0;
    private bool isAnimating = false;
    private Coroutine currentScoringCoroutine;
    
    // References
    private GameManager gameManager;
    private WordGridManager wordGridManager;
    
    public bool IsAnimating => isAnimating;
    
    private void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        wordGridManager = FindFirstObjectByType<WordGridManager>();
        
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }
    
    /// <summary>
    /// Main method to start the complete scoring animation sequence
    /// </summary>
    public void StartScoringAnimation(NumericalScoringData scoringData, List<RectTransform> cellTransforms)
    {
        if (isAnimating)
        {
            Debug.LogWarning("[AnimatedScoringSystem] Animation already in progress. Skipping new animation.");
            return;
        }
        
        if (currentScoringCoroutine != null)
        {
            StopCoroutine(currentScoringCoroutine);
        }
        
        currentScoringCoroutine = StartCoroutine(ScoringAnimationSequence(scoringData, cellTransforms));
    }
    
    /// <summary>
    /// Complete scoring animation sequence
    /// </summary>
    private IEnumerator ScoringAnimationSequence(NumericalScoringData scoringData, List<RectTransform> cellTransforms)
    {
        isAnimating = true;
        
        Debug.Log("🎬 Starting Animated Scoring Sequence (Parallel Mode)");
        
        // Only initialize total score from gameManager if this is the first time (i.e., totalScore is zero)
        if (totalScore == 0 && gameManager != null)
        {
            totalScore = gameManager.GetCurrentScore();
            UpdateTotalScoreUI();
        }
        
        // Reset current score display - this will accumulate as letters disappear
        currentDisplayedScore = 0;
        UpdateCurrentScoreUI();
        
        // Step 1: Cell Float Animation (start immediately)
        StartCoroutine(AnimateCellFloat(cellTransforms));
        
        // Step 2: Intersection Score (if any) - add immediately to current score
        if (scoringData.intersectionScore > 0)
        {
            yield return StartCoroutine(AddToCurrentScoreInstantly(scoringData.intersectionScore, GetIntersectionColor()));
        }
        
        // Step 3: Letter-by-letter scoring with real-time current score updates
        yield return StartCoroutine(AnimateLetterScoringRealTime(scoringData));
        
        // Step 4: Modifier bonuses - add to current score
        yield return StartCoroutine(AddModifierBonusesToCurrentScore(scoringData));
        
        // Step 5: Final Score Transfer Animation (current → total)
        yield return StartCoroutine(AnimateScoreTransfer());
        
        Debug.Log("✅ Animated Scoring Sequence Complete (Parallel Mode)");
        isAnimating = false;
    }
    
    /// <summary>
    /// Show intersection bonus first
    /// </summary>
    private IEnumerator ShowIntersectionBonus(int intersectionScore)
    {
        Debug.Log($"🔗 Showing intersection bonus: {intersectionScore} points");
        
        // Play sound
        if (intersectionSound != null && audioSource != null)
            audioSource.PlayOneShot(intersectionSound);
        
        // Add intersection score to current display
        yield return StartCoroutine(IncrementCurrentScore(intersectionScore, GetIntersectionColor(), intersectionPulseScale));
        
        // Hold for display
        yield return new WaitForSeconds(intersectionDisplayDuration);
    }
    
    /// <summary>
    /// Animate cells floating up
    /// </summary>
    private IEnumerator AnimateCellFloat(List<RectTransform> cellTransforms)
    {
        Debug.Log($"⬆️ Animating cell float for {cellTransforms.Count} cells");
        
        // Float all cells up simultaneously
        foreach (var cellTransform in cellTransforms)
        {
            if (cellTransform != null)
            {
                // Highlight cell
                var cellController = cellTransform.GetComponent<CellController>();
                if (cellController != null)
                {
                    cellController.SetHighlightState(true, cellFloatHighlightColor);
                }
                
                // Float animation
                Vector3 originalPos = cellTransform.anchoredPosition;
                Vector3 floatPos = originalPos + Vector3.up * cellFloatHeight;
                
                cellTransform.DOAnchorPos(floatPos, cellFloatDuration)
                    .SetEase(cellFloatEase)
                    .OnComplete(() => {
                        // Return to original position
                        cellTransform.DOAnchorPos(originalPos, cellFloatDuration * 0.5f);
                    });
            }
        }
        
        yield return new WaitForSeconds(cellFloatDuration);
    }
    
    /// <summary>
    /// Add points to current score instantly (for intersection bonus)
    /// </summary>
    private IEnumerator AddToCurrentScoreInstantly(int points, Color color)
    {
        Debug.Log($"🔗 Adding {points} points to current score instantly");
        
        // Play sound
        if (intersectionSound != null && audioSource != null)
            audioSource.PlayOneShot(intersectionSound);
        
        // Add points immediately with visual feedback
        Color originalColor = currentScoreText.color;
        currentScoreText.color = color;
        
        currentDisplayedScore += points;
        UpdateCurrentScoreUI();
        
        // Pulse animation
        currentScoreTransform.DOPunchScale(Vector3.one * (intersectionPulseScale - 1f), 0.3f, 1, 0f);
        
        yield return new WaitForSeconds(0.3f);
        
        // Restore color
        currentScoreText.color = originalColor;
    }

    /// <summary>
    /// Animate letter-by-letter scoring with real-time current score updates
    /// </summary>
    private IEnumerator AnimateLetterScoringRealTime(NumericalScoringData scoringData)
    {
        Debug.Log("🔤 Starting real-time letter-by-letter scoring");
        
        // For each word, animate its letters with actual letter scores
        foreach (var word in scoringData.words)
        {
            yield return StartCoroutine(AnimateWordLettersRealTime(word, scoringData));
        }
    }

    /// <summary>
    /// Animate individual word letters with immediate score updates using actual letter values
    /// </summary>
    private IEnumerator AnimateWordLettersRealTime(FoundWordData wordData, NumericalScoringData scoringData)
    {
        // Get shared letter positions to avoid double-counting
        var sharedLetterPositions = GetSharedLetterPositions(scoringData);
        
        for (int i = 0; i < wordData.Coordinates.Count; i++)
        {
            var coord = wordData.Coordinates[i];
            char letter = i < wordData.Word.Length ? wordData.Word[i] : '?';
            
            // Get actual letter score from GameManager
            int actualLetterScore = gameManager != null ? gameManager.GetPointsForActualScoring(letter) : 1;
            
            // Skip intersection letters to avoid double-counting
            bool isIntersectionLetter = sharedLetterPositions.Contains(coord);
            if (isIntersectionLetter)
            {
                Debug.Log($"  Letter '{letter}' at {coord}: Skipping (intersection already counted)");
                // Still animate the cell but don't add to score
                AnimateCellDisappear(coord);
                yield return new WaitForSeconds(letterScoreDelay);
                continue;
            }
            
            // Get cell at coordinate and animate it
            AnimateCellDisappear(coord);
            
            // Add actual score to current score IMMEDIATELY (real-time update)
            currentDisplayedScore += actualLetterScore;
            UpdateCurrentScoreUI();
            
            // Visual feedback for score addition
            currentScoreTransform.DOPunchScale(Vector3.one * (letterScorePulseScale - 1f), letterScorePulseDuration, 1, 0f);
            
            Debug.Log($"  Letter '{letter}' at {coord}: +{actualLetterScore} points (Current: {currentDisplayedScore})");
            
            yield return new WaitForSeconds(letterScoreDelay);
        }
    }
    
    /// <summary>
    /// Get shared letter positions from scoring data
    /// </summary>
    private List<Vector2Int> GetSharedLetterPositions(NumericalScoringData scoringData)
    {
        Dictionary<Vector2Int, int> positionCount = new Dictionary<Vector2Int, int>();
        
        foreach (var word in scoringData.words)
        {
            foreach (var coord in word.Coordinates)
            {
                if (positionCount.ContainsKey(coord))
                    positionCount[coord]++;
                else
                    positionCount[coord] = 1;
            }
        }
        
        return positionCount.Where(kvp => kvp.Value > 1).Select(kvp => kvp.Key).ToList();
    }
    
    /// <summary>
    /// Helper method to animate a cell disappearing
    /// </summary>
    private void AnimateCellDisappear(Vector2Int coord)
    {
        var cellController = wordGridManager?.GetCellController(coord);
        
        if (cellController != null)
        {
            // Animate cell scaling down and fading
            var rectTransform = cellController.RectTransform;
            var canvasGroup = cellController.GetComponent<CanvasGroup>();
            
            if (canvasGroup == null)
                canvasGroup = cellController.gameObject.AddComponent<CanvasGroup>();
            
            // Scale down and fade out animation
            rectTransform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack);
            canvasGroup.DOFade(0f, 0.3f);
            
            // Play sound
            if (letterScoreSound != null && audioSource != null)
                audioSource.PlayOneShot(letterScoreSound);
        }
    }

    /// <summary>
    /// Add modifier bonuses to current score using the same logic as NumericalScoringData
    /// </summary>
    private IEnumerator AddModifierBonusesToCurrentScore(NumericalScoringData scoringData)
    {
        var modifierManager = ModifierManager.Instance;
        if (modifierManager == null) yield break;
        
        var activeModifiers = modifierManager.GetAllActiveModifiers();
        
        Debug.Log($"🎛️  Applying {activeModifiers.Count} active modifiers to current score");
        
        // Store the base score before modifiers
        int baseScoreBeforeModifiers = currentDisplayedScore;
        
        foreach (var modifier in activeModifiers)
        {
            // Handle multipliers first (same logic as NumericalScoringData)
            if (modifier.effectType == ModifierEffectType.GeneralScoreBonusAndMoveReduction ||
                modifier.effectType == ModifierEffectType.SpecificWordLengthScoreBonus)
            {
                float multiplier = GetMultiplierForModifier(modifier, scoringData);
                if (multiplier > 1f)
                {
                    int oldScore = currentDisplayedScore;
                    currentDisplayedScore = Mathf.RoundToInt(currentDisplayedScore * multiplier);
                    int multiplierBonus = currentDisplayedScore - oldScore;
                    
                    if (multiplierBonus > 0)
                    {
                        yield return StartCoroutine(ShowModifierEffect(modifier, multiplierBonus, $"×{multiplier}"));
                    }
                }
            }
            
            // Handle additive bonuses (same logic as NumericalScoringData)
            int additiveBonus = GetAdditiveBonusForModifier(modifier, scoringData);
            if (additiveBonus > 0)
            {
                currentDisplayedScore += additiveBonus;
                yield return StartCoroutine(ShowModifierEffect(modifier, additiveBonus, $"+{additiveBonus}"));
            }
        }
    }
    
    /// <summary>
    /// Show individual modifier effect
    /// </summary>
    private IEnumerator ShowModifierEffect(ModifierCardData modifier, int bonus, string displayText)
    {
        // Show modifier text briefly
        Color originalColor = currentScoreText.color;
        var originalText = currentScoreText.text;
        
        currentScoreText.color = modifierColor;
        currentScoreText.text = displayText;
        currentScoreTransform.DOPunchScale(Vector3.one * (modifierPulseScale - 1f), modifierPulseDuration, 1, 0f);
        
        if (modifierSound != null && audioSource != null)
            audioSource.PlayOneShot(modifierSound);
        
        yield return new WaitForSeconds(0.5f);
        
        // Update display with new score
        UpdateCurrentScoreUI();
        currentScoreText.color = originalColor;
        
        Debug.Log($"  Modifier '{modifier.cardName}': {displayText} (Current: {currentDisplayedScore})");
        
        yield return new WaitForSeconds(0.3f);
    }
    
    /// <summary>
    /// Get multiplier for modifier (matches NumericalScoringData logic)
    /// </summary>
    private float GetMultiplierForModifier(ModifierCardData modifier, NumericalScoringData scoringData)
    {
        switch (modifier.effectType)
        {
            case ModifierEffectType.GeneralScoreBonusAndMoveReduction:
                return modifier.generalScoreMultiplier;
                
            case ModifierEffectType.SpecificWordLengthScoreBonus:
                // Check if any word matches the target length
                if (scoringData.words.Any(w => w.Word.Length == modifier.targetWordLength))
                    return modifier.wordLengthScoreMultiplier;
                break;
        }
        return 1f;
    }
    
    /// <summary>
    /// Get additive bonus for modifier (matches NumericalScoringData logic)
    /// </summary>
    private int GetAdditiveBonusForModifier(ModifierCardData modifier, NumericalScoringData scoringData)
    {
        switch (modifier.effectType)
        {
            case ModifierEffectType.VowelCountBonus:
                int totalBonus = 0;
                foreach (var word in scoringData.words)
                {
                    int vowelCount = word.Word.Count(c => "AEIOUaeiou".Contains(c));
                    if (vowelCount >= modifier.minVowelCount)
                    {
                        totalBonus += modifier.vowelBonusPoints;
                    }
                }
                return totalBonus;
        }
        return 0;
    }
    
    /// <summary>
    /// Animate score transfer from current to total
    /// </summary>
    private IEnumerator AnimateScoreTransfer()
    {
        if (currentDisplayedScore <= 0) yield break;
        
        Debug.Log($"🔄 Starting score transfer: {currentDisplayedScore} points");
        
        // Play transfer sound
        if (transferSound != null && audioSource != null)
            audioSource.PlayOneShot(transferSound);
        
        // Calculate transfer timing
        float pointsPerSecond = scoreTransferSpeed;
        float delayBetweenPoints = Mathf.Max(1f / pointsPerSecond, scoreTransferMinDelay);
        
        int pointsToTransfer = currentDisplayedScore;
        float pulseTimer = 0f;
        
        while (currentDisplayedScore > 0)
        {
            // Transfer one point
            currentDisplayedScore--;
            totalScore++;
            
            // Update UI
            UpdateCurrentScoreUI();
            UpdateTotalScoreUI();
            
            // Pulse effect
            pulseTimer += delayBetweenPoints;
            if (pulseTimer >= transferPulseInterval)
            {
                currentScoreTransform.DOPunchScale(Vector3.one * 0.1f, 0.1f, 1, 0f);
                totalScoreTransform.DOPunchScale(Vector3.one * 0.1f, 0.1f, 1, 0f);
                pulseTimer = 0f;
            }
            
            yield return new WaitForSeconds(delayBetweenPoints);
        }
        
        Debug.Log($"✅ Score transfer complete. Total score: {totalScore}");
    }
    
    /// <summary>
    /// Increment current score with animation
    /// </summary>
    private IEnumerator IncrementCurrentScore(int points, Color color, float pulseScale)
    {
        Color originalColor = currentScoreText.color;
        currentScoreText.color = color;
        
        // Add points
        currentDisplayedScore += points;
        UpdateCurrentScoreUI();
        
        // Pulse animation
        currentScoreTransform.DOPunchScale(Vector3.one * (pulseScale - 1f), letterScorePulseDuration, 1, 0f);
        
        yield return new WaitForSeconds(letterScorePulseDuration);
        
        // Restore color
        currentScoreText.color = originalColor;
    }
    
    /// <summary>
    /// Calculate modifier bonus for current score
    /// <summary>
    /// Update current score UI
    /// </summary>
    private void UpdateCurrentScoreUI()
    {
        if (currentScoreText != null)
        {
            currentScoreText.text = currentDisplayedScore.ToString();
        }
    }
    
    /// <summary>
    /// Update total score UI
    /// </summary>
    private void UpdateTotalScoreUI()
    {
        if (totalScoreText != null)
        {
            totalScoreText.text = totalScore.ToString();
        }
    }
    
    /// <summary>
    /// Skip current animation and apply remaining score instantly
    /// </summary>
    public void SkipAnimation()
    {
        if (currentScoringCoroutine != null)
        {
            StopCoroutine(currentScoringCoroutine);
        }
        
        // Apply remaining score instantly
        totalScore += currentDisplayedScore;
        currentDisplayedScore = 0;
        
        UpdateCurrentScoreUI();
        UpdateTotalScoreUI();
        
        isAnimating = false;
        
        Debug.Log("⏭️ Scoring animation skipped");
    }
    
    /// <summary>
    /// Set total score (for game initialization)
    /// </summary>
    public void SetTotalScore(int score)
    {
        totalScore = score;
        UpdateTotalScoreUI();
    }
    
    /// <summary>
    /// Get current total score
    /// </summary>
    public int GetTotalScore()
    {
        return totalScore;
    }
    
    /// <summary>
    /// Called after scoring animation to handle cell cleanup and word validation
    /// </summary>
    public void OnScoringComplete(List<FoundWordData> words)
    {
        if (wordGridManager == null) return;
        
        Debug.Log("🧹 Cleaning up cells after scoring animation");
        
        // Make cells invisible for all processed words
        foreach (var word in words)
        {
            foreach (var coord in word.Coordinates)
            {
                var cellController = wordGridManager.GetCellController(coord);
                if (cellController != null)
                {
                    cellController.SetAlpha(0f);
                }
            }
        }
    }
    
    /// <summary>
    /// Clear any ongoing animations and reset state (replacement for EffectsManager cleanup)
    /// </summary>
    public void ClearAllAnimations()
    {
        if (currentScoringCoroutine != null)
        {
            StopCoroutine(currentScoringCoroutine);
            currentScoringCoroutine = null;
        }
        
        // Reset state
        isAnimating = false;
        
        // Kill any ongoing DOTween animations
        DOTween.KillAll();
        
        Debug.Log("🧹 AnimatedScoringSystem: All animations cleared");
    }
    
    /// <summary>
    /// Gets the intersection color from WordGridManager, with fallback to inspector value
    /// </summary>
    private Color GetIntersectionColor()
    {
        if (wordGridManager != null)
        {
            return wordGridManager.GetIntersectionLetterColor();
        }
        return intersectionColor; // Fallback to inspector value
    }
}
