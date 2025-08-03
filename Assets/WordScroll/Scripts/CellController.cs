using UnityEngine;
using TMPro; // For TextMeshProUGUI
using DG.Tweening; // For animations
using UnityEngine.UI; // Required for Image

[RequireComponent(typeof(CanvasGroup))] // Ensure CanvasGroup exists
[RequireComponent(typeof(Image))] // Ensure Image exists for background color
public class CellController : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private TextMeshProUGUI letterText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image backgroundImage; // Reference to the cell's Image component
    [Tooltip("TextMeshProUGUI component to display the letter's score. Optional.")]
    [SerializeField] private TextMeshProUGUI letterScoreText; // ADDED: For letter's score

    [Header("Move Tracking (from LetterCell)")]
    [Tooltip("Initial number of moves for this cell")]
    [SerializeField] private int initialMoves = 5;
    [Tooltip("Enable or disable the move reduction for this cell")]
    [SerializeField] private bool enableMoves = true;
    [Tooltip("Optional: Text component to display remaining moves")]
    [SerializeField] private TextMeshProUGUI movesTextDisplay;
    
    private int movesLeft;
    public int MovesLeft => movesLeft; // Public getter
    public bool EnableMoves => enableMoves; // Public getter for the enabled state

    [Header("Highlight Settings")]
    [SerializeField] private Color defaultColor = Color.white; // Set a default or get from Image in Awake
    [SerializeField] private float highlightScaleMultiplier = 1.05f; // Slight scale up for highlight
    private Vector3 originalScale;

    // Unique ID for tracking specific cells
    public int uniqueID { get; private set; } = -1;


    // Private cache for RectTransform
    private RectTransform _rectTransform;

    // Public property to access RectTransform safely
    public RectTransform RectTransform
    {
        get
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }
            return _rectTransform;
        }
    }

    void Awake()
    {
        if (letterText == null) letterText = GetComponentInChildren<TextMeshProUGUI>(); // Assuming main letter text is a child
        // If letterScoreText is also a child and needs specific finding logic, adjust here.
        // For simplicity, direct assignment via Inspector is preferred for letterScoreText.
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();

        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        originalScale = RectTransform.localScale;

        // Initialize move tracking (from LetterCell integration)
        movesLeft = initialMoves;
        UpdateMovesDisplay();

        if (backgroundImage != null)
        {
            defaultColor = backgroundImage.color; // Store the initial color as default
        }
        else
        {
            Debug.LogError("CellController: Image component not found! Cannot manage colors.", this);
        }

        if (letterText == null) Debug.LogError("CellController: TextMeshProUGUI for letter not found!", this);
        if (letterScoreText == null) Debug.LogWarning("CellController: TextMeshProUGUI for letter score not assigned. Score will not be displayed.", this);
        if (canvasGroup == null) Debug.LogError("CellController: CanvasGroup not found!", this);
        if (_rectTransform == null) Debug.LogError("CellController: RectTransform not found!", this);
    }

    public void SetLetter(char letter)
    {
        if (letterText != null)
        {
            letterText.text = letter.ToString();
        }
        else
        {
            Debug.LogWarning($"CellController ({gameObject.name}): LetterText component is null. Cannot set letter '{letter}'.");
        }

        // ADDED: Logic to display letter score
        if (letterScoreText != null)
        {
            if (GameManager.instance != null)
            {
                // Hide scores in Wordle-style levels
                if (GameManager.instance.IsWordleStyleLevel)
                {
                    letterScoreText.gameObject.SetActive(false);
                    // Debug.Log($"🔤 Wordle Mode: Hiding score for letter '{letter}' in cell {gameObject.name}");
                }
                else
                {
                    GameManager.ScoringMode currentMode = GameManager.instance.GetCurrentScoringModeSetting();
                    if (currentMode == GameManager.ScoringMode.ScrabbleBased)
                    {
                        int score = GameManager.instance.CalculateScoreValueForLetter(letter);
                        if (score > 0)
                        {
                            letterScoreText.text = score.ToString();
                            letterScoreText.gameObject.SetActive(true);
                        }
                        else
                        {
                            letterScoreText.gameObject.SetActive(false); // Hide if score is 0 (e.g. blank tile if you add them)
                        }
                    }
                    else // LengthBased or other modes
                    {
                        letterScoreText.gameObject.SetActive(false); // Hide score text
                    }
                }
            }
            else
            {
                // GameManager not found, hide score
                letterScoreText.gameObject.SetActive(false);
                Debug.LogWarning($"CellController ({gameObject.name}): GameManager.instance is null. Cannot retrieve letter score.");
            }
        }
    }

    /// <summary>
    /// Enhanced method to set cell data from the new CellData system
    /// This method handles all cell types including letters and blanks
    /// </summary>
    public void SetCellData(CellData cellData)
    {
        // Set the display content (letter or blank representation)
        if (letterText != null)
        {
            if (cellData.IsBlank)
            {
                letterText.text = cellData.displayContent; // Usually empty string for blanks
            }
            else
            {
                letterText.text = cellData.displayContent;
            }
        }
        else
        {
            Debug.LogWarning($"CellController ({gameObject.name}): LetterText component is null. Cannot set cell data.");
        }

        // Handle background color
        if (backgroundImage != null)
        {
            backgroundImage.color = cellData.backgroundColor;
            StoreDefaultColor(); // Update the stored default color
        }

        // Handle text color
        if (letterText != null)
        {
            letterText.color = cellData.textColor;
        }

        // Handle score display
        if (letterScoreText != null)
        {
            if (cellData.IsBlank || cellData.scoreValue <= 0)
            {
                letterScoreText.gameObject.SetActive(false);
            }
            else
            {
                // Show score for non-blank cells with positive scores
                if (GameManager.instance != null)
                {
                    // Hide scores in Wordle-style levels
                    if (GameManager.instance.IsWordleStyleLevel)
                    {
                        letterScoreText.gameObject.SetActive(false);
                        // Debug.Log($"🔤 Wordle Mode: Hiding score for letter '{letter}' in cell {gameObject.name}");
                    }
                    else
                    {
                        GameManager.ScoringMode currentMode = GameManager.instance.GetCurrentScoringModeSetting();
                        if (currentMode == GameManager.ScoringMode.ScrabbleBased)
                        {
                            letterScoreText.text = cellData.scoreValue.ToString();
                            letterScoreText.gameObject.SetActive(true);
                        }
                        else
                        {
                            letterScoreText.gameObject.SetActive(false);
                        }
                    }
                }
                else
                {
                    letterScoreText.gameObject.SetActive(false);
                }
            }
        }

        // Handle special effects (if any)
        if (cellData.hasSpecialEffect)
        {
            ApplySpecialEffect(cellData.effectIntensity);
        }
        else
        {
            RemoveSpecialEffect();
        }
    }

    /// <summary>
    /// Compatibility method that converts char to CellData and calls SetCellData
    /// This maintains backward compatibility during migration
    /// </summary>
    public void SetLetterAsData(char letter)
    {
        CellData cellData = CellData.CreateLetterCell(letter);
        SetCellData(cellData);
    }

    /// <summary>
    /// Apply special visual effects to the cell
    /// </summary>
    private void ApplySpecialEffect(float intensity)
    {
        // Example: Glow effect, color pulsing, etc.
        if (backgroundImage != null && intensity > 0f)
        {
            // Add a subtle glow or color variation
            Color glowColor = backgroundImage.color;
            glowColor.a = Mathf.Clamp01(0.5f + intensity * 0.3f);
            backgroundImage.color = glowColor;
        }
    }

    /// <summary>
    /// Remove special visual effects from the cell
    /// </summary>
    private void RemoveSpecialEffect()
    {
        // Reset any special effects to default state
        if (backgroundImage != null)
        {
            Color normalColor = backgroundImage.color;
            normalColor.a = 1f; // Reset alpha to normal
            backgroundImage.color = normalColor;
        }
    }

    public void FadeOutImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.alpha = 0f;
        }
        else
        {
            Debug.LogWarning($"CellController ({gameObject.name}): CanvasGroup component is null. Cannot FadeOutImmediate.");
        }
    }

    public void FadeIn(float duration)
    {
        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            if (canvasGroup.alpha < 1f)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, duration).SetEase(Ease.Linear);
            }
            else
            {
                canvasGroup.alpha = 1f;
            }
        }
        else
        {
            Debug.LogWarning($"CellController ({gameObject.name}): CanvasGroup component is null. Cannot FadeIn.");
        }
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
        else
        {
            Debug.LogWarning($"CellController ({gameObject.name}): CanvasGroup component is null. Cannot SetAlpha.");
        }
    }

    public void SetHighlightState(bool isHighlighted, Color newHighlightColor)
    {
        if (backgroundImage == null) return;

        backgroundImage.DOKill(); // Kill color tweens
        RectTransform.DOKill(true); // Kill scale tweens, complete them

        if (isHighlighted)
        {
            backgroundImage.DOColor(newHighlightColor, 0.15f);
            RectTransform.DOScale(originalScale * highlightScaleMultiplier, 0.15f).SetEase(Ease.OutBack);
        }
        else // isHighlighted is false
        {
            backgroundImage.DOColor(defaultColor, 0.15f); // Uses the stored defaultColor
            RectTransform.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack); // Resets to originalScale
        }
    }

    public void StoreDefaultColor()
    {
        if (backgroundImage != null)
        {
            defaultColor = backgroundImage.color;
        }
    }
    public Color GetDefaultColor()
    {
        return defaultColor;
    }

    /// <summary>
    /// Set the unique ID for this cell (used for tracking specific cells)
    /// </summary>
    public void SetUniqueID(int id)
    {
        uniqueID = id;
    }
    
    // ======= MOVE TRACKING METHODS (Integrated from LetterCell.cs) =======
    
    /// <summary>
    /// Returns true if move was successful, false if no moves left or moves are disabled
    /// </summary>
    public bool ReduceMove()
    {
        if (enableMoves) // Only reduce moves if enabled
        {
            if (movesLeft > 0)
            {
                movesLeft--;
                UpdateMovesDisplay();
                return true;
            }
            return false; // No moves left
        }
        return true; // Moves are disabled, so consider the "reduction" successful (no change)
    }
    
    /// <summary>
    /// Update the visual display of remaining moves
    /// </summary>
    private void UpdateMovesDisplay()
    {
        if (movesTextDisplay != null)
        {
            // Control visibility based on enableMoves
            movesTextDisplay.gameObject.SetActive(enableMoves);

            // Only update the text content if moves are enabled and visible
            if (enableMoves)
            {
                movesTextDisplay.text = movesLeft.ToString();
            }
        }
    }
    
    /// <summary>
    /// Method to set moves directly (for debugging or other logic)
    /// </summary>
    public void SetMoves(int newMoves)
    {
        movesLeft = newMoves;
        UpdateMovesDisplay();
    }
    
    /// <summary>
    /// Method to directly enable/disable moves
    /// </summary>
    public void SetEnableMoves(bool shouldEnable)
    {
        enableMoves = shouldEnable;
        UpdateMovesDisplay(); // Ensure the text visibility updates immediately
    }
}