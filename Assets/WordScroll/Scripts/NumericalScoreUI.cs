using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles numerical score display with clean animations - numbers only
/// </summary>
public class NumericalScoreUI : MonoBehaviour
{
    [Header("Score Display")]
    [SerializeField] private GameObject scorePanel;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private TextMeshProUGUI currentScoreText;
    [SerializeField] private RectTransform scoreTextTransform;
    
    [Header("Final Score Animation")]
    [SerializeField] private RectTransform finalScoreTransform;
    // targetScoreTransform removed - no longer needed for flying score
    
    [Header("Animation Settings")]
    [SerializeField] private float scorePopScale = 1.3f;
    [SerializeField] private float scorePopDuration = 0.4f;
    [SerializeField] private float stepDelay = 0.6f;
    // finalScoreFlyDuration removed - no longer using flying score animation
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem scoreParticles;
    [SerializeField] private Image backgroundFlash;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip numberPopSound;
    // finalScoreSound removed - no longer using flying score animation
    
    [Header("Manager References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private GameManager gameManager;
    
    private Coroutine currentScoreAnimation;
    private bool isAnimating = false;
    
    public bool IsAnimating => isAnimating;
    
    private void Awake()
    {
        if (panelCanvasGroup == null)
            panelCanvasGroup = scorePanel.GetComponent<CanvasGroup>();
            
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
            
        // Start hidden
        SetPanelVisible(false, false);
    }
    
    /// <summary>
    /// Shows the numerical score breakdown for found words
    /// </summary>
    public void ShowNumericalScore(List<FoundWordData> foundWords)
    {
        if (isAnimating)
        {
            Debug.LogWarning("NumericalScoreUI: Already animating score");
            return;
        }
        
        var scoringData = NumericalScoringData.GenerateFromWords(foundWords, gameManager);
        
        if (currentScoreAnimation != null)
            StopCoroutine(currentScoreAnimation);
            
        currentScoreAnimation = StartCoroutine(AnimateScoreSequence(scoringData));
    }
    
    private IEnumerator AnimateScoreSequence(NumericalScoringData scoringData)
    {
        isAnimating = true;
        
        // Show panel
        SetPanelVisible(true, true);
        yield return new WaitForSeconds(0.3f);
        
        // Animate each score step
        foreach (var step in scoringData.steps)
        {
            yield return new WaitForSeconds(step.animationDelay);
            yield return StartCoroutine(AnimateScoreStep(step));
        }
        
        // Skip final score fly animation - using counting animation instead
        // (Final scoring is now handled by GameManager's TransferScoreAnimation)
        
        // Hide panel
        yield return new WaitForSeconds(0.5f);
        SetPanelVisible(false, true);
        yield return new WaitForSeconds(0.3f);
        
        isAnimating = false;
    }
    
    private IEnumerator AnimateScoreStep(ScoreStep step)
    {
        // Highlight grid cells if applicable
        if (step.gridPositions.Count > 0)
        {
            HighlightGridCells(step.gridPositions, step.highlightColor);
        }
        
        // Set score text and color
        currentScoreText.text = step.displayText;
        currentScoreText.color = GetColorForStepType(step.stepType);
        
        // Different animations based on step type
        switch (step.stepType)
        {
            case ScoreStep.StepType.IntersectingLetters:
                yield return StartCoroutine(AnimateIntersectionScore());
                break;
                
            case ScoreStep.StepType.WordBase:
                yield return StartCoroutine(AnimateWordScore());
                break;
                
            case ScoreStep.StepType.Multiplier:
                yield return StartCoroutine(AnimateMultiplier());
                break;
                
            case ScoreStep.StepType.AdditiveBonus:
                yield return StartCoroutine(AnimateBonus());
                break;
                
            case ScoreStep.StepType.Final:
                yield return StartCoroutine(AnimateFinalScore());
                break;
        }
        
        // Play sound
        PlayScoreSound();
        
        // Clear grid highlights after animation
        if (step.gridPositions.Count > 0)
        {
            yield return new WaitForSeconds(0.3f);
            ClearGridHighlights(step.gridPositions);
        }
    }
    
    private IEnumerator AnimateIntersectionScore()
    {
        // Cyan pulsing animation for intersection
        scoreTextTransform.localScale = Vector3.zero;
        currentScoreText.alpha = 0f;
        
        // Fade in and scale up
        currentScoreText.DOFade(1f, 0.2f);
        scoreTextTransform.DOScale(Vector3.one * scorePopScale, 0.3f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => scoreTextTransform.DOScale(Vector3.one, 0.2f));
            
        yield return new WaitForSeconds(0.5f);
    }
    
    private IEnumerator AnimateWordScore()
    {
        // Yellow slide-in animation for word score
        Vector3 startPos = scoreTextTransform.localPosition + Vector3.right * 100f;
        scoreTextTransform.localPosition = startPos;
        currentScoreText.alpha = 0f;
        
        currentScoreText.DOFade(1f, 0.3f);
        scoreTextTransform.DOLocalMoveX(0f, 0.4f).SetEase(Ease.OutQuad);
        
        yield return new WaitForSeconds(0.5f);
    }
    
    private IEnumerator AnimateMultiplier()
    {
        // Magenta spinning scale animation for multipliers
        scoreTextTransform.localScale = Vector3.zero;
        scoreTextTransform.localRotation = Quaternion.Euler(0, 0, 180);
        currentScoreText.alpha = 0f;
        
        currentScoreText.DOFade(1f, 0.2f);
        scoreTextTransform.DOScale(Vector3.one * scorePopScale, 0.4f).SetEase(Ease.OutBack);
        scoreTextTransform.DORotate(Vector3.zero, 0.4f).SetEase(Ease.OutBack)
            .OnComplete(() => {
                scoreTextTransform.DOScale(Vector3.one, 0.2f);
                // Flash background for multiplier
                if (backgroundFlash != null)
                {
                    backgroundFlash.DOFade(0.3f, 0.1f).OnComplete(() => 
                        backgroundFlash.DOFade(0f, 0.3f));
                }
            });
            
        yield return new WaitForSeconds(0.6f);
    }
    
    private IEnumerator AnimateBonus()
    {
        // Green bounce animation for additive bonuses
        scoreTextTransform.localScale = Vector3.zero;
        currentScoreText.alpha = 0f;
        
        currentScoreText.DOFade(1f, 0.2f);
        scoreTextTransform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBounce);
        
        // Particle effect for bonus
        if (scoreParticles != null)
        {
            scoreParticles.Play();
        }
        
        yield return new WaitForSeconds(0.5f);
    }
    
    private IEnumerator AnimateFinalScore()
    {
        // Gold dramatic scale and glow for final score
        scoreTextTransform.localScale = Vector3.zero;
        currentScoreText.alpha = 0f;
        
        // Large dramatic entrance
        currentScoreText.DOFade(1f, 0.3f);
        scoreTextTransform.DOScale(Vector3.one * 1.5f, 0.5f).SetEase(Ease.OutBack)
            .OnComplete(() => {
                // Pulsing effect
                scoreTextTransform.DOPunchScale(Vector3.one * 0.2f, 0.6f, 3, 0.5f);
            });
            
        // Background flash for final score
        if (backgroundFlash != null)
        {
            backgroundFlash.color = Color.gold;
            backgroundFlash.DOFade(0.5f, 0.2f).OnComplete(() => 
                backgroundFlash.DOFade(0f, 0.4f));
        }
        
        yield return new WaitForSeconds(0.8f);
    }
    
    private Color GetColorForStepType(ScoreStep.StepType stepType)
    {
        return stepType switch
        {
            ScoreStep.StepType.IntersectingLetters => Color.cyan,
            ScoreStep.StepType.WordBase => Color.yellow,
            ScoreStep.StepType.Multiplier => Color.magenta,
            ScoreStep.StepType.AdditiveBonus => Color.green,
            ScoreStep.StepType.Final => Color.gold,
            _ => Color.white
        };
    }
    
    private void SetPanelVisible(bool visible, bool animate)
    {
        if (panelCanvasGroup == null) return;
        
        if (animate)
        {
            panelCanvasGroup.DOFade(visible ? 1f : 0f, 0.3f);
        }
        else
        {
            panelCanvasGroup.alpha = visible ? 1f : 0f;
        }
        
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;
    }
    
    private void HighlightGridCells(List<Vector2Int> positions, Color color)
    {
        if (wordGridManager == null) return;
        
        foreach (var pos in positions)
        {
            var cellController = wordGridManager.GetCellController(pos);
            if (cellController != null)
            {
                cellController.SetHighlightState(true, color);
                cellController.transform.DOPunchScale(Vector3.one * 0.15f, 0.4f, 2, 0.5f);
            }
        }
    }
    
    private void ClearGridHighlights(List<Vector2Int> positions)
    {
        if (wordGridManager == null) return;
        
        foreach (var pos in positions)
        {
            var cellController = wordGridManager.GetCellController(pos);
            if (cellController != null)
            {
                cellController.SetHighlightState(false, cellController.GetDefaultColor());
            }
        }
    }
    
    private void PlayScoreSound()
    {
        if (audioSource != null && numberPopSound != null)
        {
            audioSource.PlayOneShot(numberPopSound);
        }
    }
    
    /// <summary>
    /// Force hide the scoring UI immediately
    /// </summary>
    public void HideImmediately()
    {
        if (currentScoreAnimation != null)
        {
            StopCoroutine(currentScoreAnimation);
            currentScoreAnimation = null;
        }
        
        SetPanelVisible(false, false);
        isAnimating = false;
    }
    
    /// <summary>
    /// Shows the numerical score breakdown for found words (parallel version for simultaneous execution)
    /// </summary>
    public IEnumerator ShowNumericalScoreParallel(List<FoundWordData> foundWords)
    {
        if (isAnimating)
        {
            Debug.LogWarning("NumericalScoreUI: Already animating score");
            yield break;
        }
        
        var scoringData = NumericalScoringData.GenerateFromWords(foundWords, gameManager);
        
        if (currentScoreAnimation != null)
            StopCoroutine(currentScoreAnimation);
            
        currentScoreAnimation = StartCoroutine(AnimateScoreSequenceParallel(scoringData));
        yield return currentScoreAnimation;
    }

    private IEnumerator AnimateScoreSequenceParallel(NumericalScoringData scoringData)
    {
        isAnimating = true;
        
        // Show panel immediately
        SetPanelVisible(true, true);
        yield return new WaitForSeconds(0.3f);
        
        // Show scoring steps in rapid succession (parallel with cell animations)
        foreach (var step in scoringData.steps)
        {
            // Don't wait for step completion, just trigger them quickly
            StartCoroutine(AnimateScoreStepParallel(step));
            yield return new WaitForSeconds(0.2f); // Short delay between steps
        }
        
        // Wait a bit for all step animations to complete
        yield return new WaitForSeconds(1.5f);
        
        // Hide panel
        SetPanelVisible(false, true);
        yield return new WaitForSeconds(0.3f);
        
        isAnimating = false;
    }

    private IEnumerator AnimateScoreStepParallel(ScoreStep step)
    {
        // Highlight grid cells if applicable (shorter duration for parallel mode)
        if (step.gridPositions.Count > 0)
        {
            HighlightGridCells(step.gridPositions, step.highlightColor);
        }
        
        // Set score text and color
        currentScoreText.text = step.displayText;
        currentScoreText.color = GetColorForStepType(step.stepType);
        
        // Faster animations for parallel mode
        switch (step.stepType)
        {
            case ScoreStep.StepType.IntersectingLetters:
                yield return StartCoroutine(AnimateIntersectionScoreFast());
                break;
                
            case ScoreStep.StepType.WordBase:
                yield return StartCoroutine(AnimateWordScoreFast());
                break;
                
            case ScoreStep.StepType.Multiplier:
                yield return StartCoroutine(AnimateMultiplierFast());
                break;
                
            case ScoreStep.StepType.AdditiveBonus:
                yield return StartCoroutine(AnimateBonusFast());
                break;
                
            case ScoreStep.StepType.Final:
                yield return StartCoroutine(AnimateFinalScoreFast());
                break;
        }
        
        // Play sound
        PlayScoreSound();
        
        // Clear grid highlights after shorter time
        if (step.gridPositions.Count > 0)
        {
            yield return new WaitForSeconds(0.15f);
            ClearGridHighlights(step.gridPositions);
        }
    }

    // Fast versions of animation methods for parallel execution
    private IEnumerator AnimateIntersectionScoreFast()
    {
        scoreTextTransform.localScale = Vector3.zero;
        currentScoreText.alpha = 0f;
        
        currentScoreText.DOFade(1f, 0.1f);
        scoreTextTransform.DOScale(Vector3.one * scorePopScale, 0.2f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => scoreTextTransform.DOScale(Vector3.one, 0.1f));
            
        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator AnimateWordScoreFast()
    {
        Vector3 startPos = scoreTextTransform.localPosition + Vector3.right * 50f;
        scoreTextTransform.localPosition = startPos;
        currentScoreText.alpha = 0f;
        
        currentScoreText.DOFade(1f, 0.15f);
        scoreTextTransform.DOLocalMoveX(0f, 0.2f).SetEase(Ease.OutQuad);
        
        yield return new WaitForSeconds(0.25f);
    }

    private IEnumerator AnimateMultiplierFast()
    {
        scoreTextTransform.localScale = Vector3.zero;
        scoreTextTransform.localRotation = Quaternion.Euler(0, 0, 90);
        currentScoreText.alpha = 0f;
        
        currentScoreText.DOFade(1f, 0.1f);
        scoreTextTransform.DOScale(Vector3.one * scorePopScale, 0.2f).SetEase(Ease.OutBack);
        scoreTextTransform.DORotate(Vector3.zero, 0.2f).SetEase(Ease.OutBack);
            
        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator AnimateBonusFast()
    {
        scoreTextTransform.localScale = Vector3.zero;
        currentScoreText.alpha = 0f;
        
        currentScoreText.DOFade(1f, 0.1f);
        scoreTextTransform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBounce);
        
        if (scoreParticles != null)
        {
            scoreParticles.Play();
        }
        
        yield return new WaitForSeconds(0.25f);
    }

    private IEnumerator AnimateFinalScoreFast()
    {
        scoreTextTransform.localScale = Vector3.zero;
        currentScoreText.alpha = 0f;
        
        currentScoreText.DOFade(1f, 0.15f);
        scoreTextTransform.DOScale(Vector3.one * 1.2f, 0.25f).SetEase(Ease.OutBack)
            .OnComplete(() => {
                scoreTextTransform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 2, 0.5f);
            });
            
        yield return new WaitForSeconds(0.4f);
    }
}
