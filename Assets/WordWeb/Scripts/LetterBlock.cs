using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Individual letter block component within a word tile.
/// Displays a single letter with its score value.
/// </summary>
[RequireComponent(typeof(Image))]
public class LetterBlock : MonoBehaviour
{
    [Header("Visual References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI letterText;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Visual Settings")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private TMP_FontAsset letterFont;
    [SerializeField] private int letterFontSize = 24;
    [SerializeField] private int scoreFontSize = 12;

    [Header("Animation")]
    [SerializeField] private float animationDuration = 0.2f;

    // Letter data
    private char letter;
    private int score;
    private int index;
    private bool isHighlighted = false;

    // Visual state
    private Vector3 originalScale;
    private Color originalColor;
    private RectTransform rectTransform;

    #region Initialization

    void Awake()
    {
        ValidateReferences();
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;
        originalColor = backgroundImage != null ? backgroundImage.color : Color.white;
    }

    private void ValidateReferences()
    {
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (letterText == null)
        {
            letterText = GetComponentInChildren<TextMeshProUGUI>();
            
            // If still null, try to find by name
            if (letterText == null)
            {
                Transform letterTransform = transform.Find("LetterText");
                if (letterTransform != null)
                {
                    letterText = letterTransform.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        if (scoreText == null)
        {
            // Try to find score text by name
            Transform scoreTransform = transform.Find("ScoreText");
            if (scoreTransform != null)
            {
                scoreText = scoreTransform.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    public void Initialize(char letterChar, int letterScore, int letterIndex)
    {
        letter = letterChar;
        score = letterScore;
        index = letterIndex;
        
        UpdateVisuals();
        
        // Set name for debugging
        gameObject.name = $"LetterBlock_{letter}_{letterIndex}";
    }

    #endregion

    #region Visual Updates

    private void UpdateVisuals()
    {
        UpdateLetterDisplay();
        UpdateScoreDisplay();
        UpdateBackgroundColor();
    }

    private void UpdateLetterDisplay()
    {
        if (letterText != null)
        {
            letterText.text = letter.ToString().ToUpper();
            
            // Apply font settings
            if (letterFont != null)
            {
                letterText.font = letterFont;
            }
            
            letterText.fontSize = letterFontSize;
            letterText.color = Color.black;
            letterText.alignment = TextAlignmentOptions.Center;
        }
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString();
            scoreText.fontSize = scoreFontSize;
            scoreText.color = Color.black;
            scoreText.alignment = TextAlignmentOptions.Center;
        }
    }

    private void UpdateBackgroundColor()
    {
        if (backgroundImage == null) return;

        Color targetColor = isHighlighted ? highlightColor : defaultColor;
        
        // Get color based on letter score (higher scores = different colors)
        targetColor = GetScoreBasedColor();
        
        backgroundImage.DOColor(targetColor, animationDuration)
            .SetEase(Ease.OutQuad);
    }

    private Color GetScoreBasedColor()
    {
        if (isHighlighted)
        {
            return highlightColor;
        }

        // Color based on Scrabble-like scoring
        switch (score)
        {
            case 1: return Color.white;           // Common letters (A, E, I, O, U, L, N, S, T, R)
            case 2: return new Color(0.9f, 0.9f, 1f); // D, G
            case 3: return new Color(0.8f, 1f, 0.8f); // B, C, M, P
            case 4: return new Color(1f, 1f, 0.8f);   // F, H, V, W, Y
            case 5: return new Color(1f, 0.9f, 0.8f); // K
            case 8: return new Color(1f, 0.8f, 0.8f); // J, X
            case 10: return new Color(1f, 0.7f, 0.7f); // Q, Z
            default: return Color.white;
        }
    }

    #endregion

    #region Animation Effects

    public void PlayPlacementAnimation()
    {
        // Scale animation
        rectTransform.DOScale(originalScale * 1.2f, animationDuration * 0.5f)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                rectTransform.DOScale(originalScale, animationDuration * 0.5f)
                    .SetEase(Ease.OutQuad);
            });
        
        // Flash effect
        if (backgroundImage != null)
        {
            Color originalColor = backgroundImage.color;
            backgroundImage.DOColor(Color.white, animationDuration * 0.3f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    backgroundImage.DOColor(originalColor, animationDuration * 0.3f)
                        .SetEase(Ease.OutQuad);
                });
        }
    }

    public void PlayHighlightAnimation()
    {
        // Glow effect
        rectTransform.DOScale(originalScale * 1.1f, animationDuration)
            .SetEase(Ease.OutBack);
    }

    public void PlayUnhighlightAnimation()
    {
        // Return to normal scale
        rectTransform.DOScale(originalScale, animationDuration)
            .SetEase(Ease.OutQuad);
    }

    public void PlayScoreAnimation()
    {
        // Score text animation
        if (scoreText != null)
        {
            scoreText.transform.DOScale(Vector3.one * 1.5f, animationDuration * 0.5f)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    scoreText.transform.DOScale(Vector3.one, animationDuration * 0.5f)
                        .SetEase(Ease.OutQuad);
                });
        }
    }

    #endregion

    #region Highlight Control

    public void SetHighlighted(bool highlighted)
    {
        isHighlighted = highlighted;
        UpdateBackgroundColor();
        
        if (highlighted)
        {
            PlayHighlightAnimation();
        }
        else
        {
            PlayUnhighlightAnimation();
        }
    }

    public void SetTemporaryHighlight(float duration)
    {
        SetHighlighted(true);
        
        // Remove highlight after duration
        DOVirtual.DelayedCall(duration, () => SetHighlighted(false));
    }

    #endregion

    #region Utility

    public void ResetVisuals()
    {
        // Stop all animations
        rectTransform.DOKill();
        backgroundImage.DOKill();
        
        if (letterText != null)
        {
            letterText.transform.DOKill();
        }
        
        if (scoreText != null)
        {
            scoreText.transform.DOKill();
        }
        
        // Reset to original state
        rectTransform.localScale = originalScale;
        isHighlighted = false;
        UpdateVisuals();
    }

    public void SetAlpha(float alpha)
    {
        if (backgroundImage != null)
        {
            Color color = backgroundImage.color;
            color.a = alpha;
            backgroundImage.color = color;
        }
        
        if (letterText != null)
        {
            Color color = letterText.color;
            color.a = alpha;
            letterText.color = color;
        }
        
        if (scoreText != null)
        {
            Color color = scoreText.color;
            color.a = alpha;
            scoreText.color = color;
        }
    }

    public void SetInteractable(bool interactable)
    {
        SetAlpha(interactable ? 1f : 0.5f);
    }

    #endregion

    #region Getters

    public char Letter => letter;
    public int Score => score;
    public int Index => index;
    public bool IsHighlighted => isHighlighted;
    public Vector3 WorldPosition => rectTransform.position;

    #endregion

    #region Static Utility Methods

    /// <summary>
    /// Get the standard Scrabble score for a letter
    /// </summary>
    public static int GetStandardLetterScore(char letter)
    {
        letter = char.ToUpper(letter);
        
        switch (letter)
        {
            case 'A': case 'E': case 'I': case 'O': case 'U':
            case 'L': case 'N': case 'S': case 'T': case 'R':
                return 1;
            
            case 'D': case 'G':
                return 2;
            
            case 'B': case 'C': case 'M': case 'P':
                return 3;
            
            case 'F': case 'H': case 'V': case 'W': case 'Y':
                return 4;
            
            case 'K':
                return 5;
            
            case 'J': case 'X':
                return 8;
            
            case 'Q': case 'Z':
                return 10;
            
            default:
                return 1; // Default for any other characters
        }
    }

    /// <summary>
    /// Calculate scores for all letters in a word
    /// </summary>
    public static int[] CalculateWordScores(string word)
    {
        if (string.IsNullOrEmpty(word)) return new int[0];
        
        int[] scores = new int[word.Length];
        
        for (int i = 0; i < word.Length; i++)
        {
            scores[i] = GetStandardLetterScore(word[i]);
        }
        
        return scores;
    }

    #endregion
}
