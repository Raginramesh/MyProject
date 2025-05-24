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
            if (GameManager.instance != null)
            {
                GameManager.ScoringMode currentMode = GameManager.instance.CurrentScoringMode;
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
            else
            {
                // GameManager not found, hide score
                letterScoreText.gameObject.SetActive(false);
                Debug.LogWarning($"CellController ({gameObject.name}): GameManager.instance is null. Cannot retrieve letter score.");
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
        else
        {
            backgroundImage.DOColor(defaultColor, 0.15f);
            RectTransform.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack);
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