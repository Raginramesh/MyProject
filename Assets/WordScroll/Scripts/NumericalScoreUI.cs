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
    [SerializeField] private RectTransform targetScoreTransform; // Main score counter position
    
    [Header("Animation Settings")]
    [SerializeField] private float scorePopScale = 1.3f;
    [SerializeField] private float scorePopDuration = 0.4f;
    [SerializeField] private float stepDelay = 0.6f;
    [SerializeField] private float finalScoreFlyDuration = 1f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem scoreParticles;
    [SerializeField] private Image backgroundFlash;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip numberPopSound;
    [SerializeField] private AudioClip finalScoreSound;
    
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
        
        // Final score fly animation
        yield return StartCoroutine(AnimateFinalScoreFly(scoringData.finalScore));
        
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
    
    private IEnumerator AnimateFinalScoreFly(int finalScore)
    {
        if (targetScoreTransform == null) yield break;
        
        // Create flying score text
        GameObject flyingScore = new GameObject("FlyingScore");
        flyingScore.transform.SetParent(transform);
        
        TextMeshProUGUI flyingText = flyingScore.AddComponent<TextMeshProUGUI>();
        flyingText.text = finalScore.ToString();
        flyingText.font = currentScoreText.font;
        flyingText.fontSize = currentScoreText.fontSize;
        flyingText.color = Color.gold;
        flyingText.alignment = TextAlignmentOptions.Center;
        
        RectTransform flyingRect = flyingScore.GetComponent<RectTransform>();
        flyingRect.sizeDelta = currentScoreText.rectTransform.sizeDelta;
        flyingRect.position = scoreTextTransform.position;
        
        // Play final score sound
        if (finalScoreSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(finalScoreSound);
        }
        
        // Animate to target position
        flyingRect.DOMove(targetScoreTransform.position, finalScoreFlyDuration)
            .SetEase(Ease.InOutQuad);
        flyingText.DOFade(0f, finalScoreFlyDuration * 0.8f).SetDelay(finalScoreFlyDuration * 0.2f);
        flyingRect.DOScale(Vector3.zero, finalScoreFlyDuration * 0.3f).SetDelay(finalScoreFlyDuration * 0.7f);
        
        yield return new WaitForSeconds(finalScoreFlyDuration);
        
        // Clean up
        if (flyingScore != null)
            Destroy(flyingScore);
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
}
