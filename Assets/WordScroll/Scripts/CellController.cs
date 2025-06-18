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

    [Header("Highlight Settings")]
    [SerializeField] private Color defaultColor = Color.white; // Set a default or get from Image in Awake
    [SerializeField] private float highlightScaleMultiplier = 1.05f; // Slight scale up for highlight
    private Vector3 originalScale;


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
            // Use ScoreManager for letter score
            var scoreManager = WordScroll.Managers.ScoreManager.Instance;
            if (scoreManager != null)
            {
                GameManager.ScoringMode currentMode = GameManager.instance.GetCurrentScoringModeSetting();
                if (currentMode == GameManager.ScoringMode.ScrabbleBased)
                {
                    // Only display, do NOT update the score!
                    int score = 1; // Default fallback
                    // If you have a Scrabble letter value dictionary, use it here instead of calling CalculateWordScore
                    // Avoid calling CalculateWordScore here, as it adds to the total score!
                    // Example: score = ScrabbleValues[letter];
                    letterScoreText.text = score.ToString();
                    letterScoreText.gameObject.SetActive(true);
                }
                else // LengthBased or other modes
                {
                    letterScoreText.gameObject.SetActive(false);
                }
            }
            else
            {
                letterScoreText.gameObject.SetActive(false);
            }
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
}