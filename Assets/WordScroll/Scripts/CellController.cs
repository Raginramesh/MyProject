using UnityEngine;
using TMPro; // For TextMeshProUGUI
using DG.Tweening; // For animations

[RequireComponent(typeof(CanvasGroup))] // Ensure CanvasGroup exists
public class CellController : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private TextMeshProUGUI letterText;
    [SerializeField] private CanvasGroup canvasGroup;

    // Private cache for RectTransform
    private RectTransform _rectTransform;

    // Public property to access RectTransform safely
    public RectTransform RectTransform
    {
        get
        {
            // Cache the RectTransform if not already done
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }
            return _rectTransform;
        }
    }

    void Awake()
    {
        // Fallback if references not set on prefab inspector
        if (letterText == null) letterText = GetComponentInChildren<TextMeshProUGUI>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        // Cache RectTransform on Awake
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();


        if (letterText == null) Debug.LogError("CellController: TextMeshProUGUI not found!", this);
        if (canvasGroup == null) Debug.LogError("CellController: CanvasGroup not found!", this);
        if (_rectTransform == null) Debug.LogError("CellController: RectTransform not found!", this);
    }

    /// <summary>
    /// Sets the displayed letter.
    /// </summary>
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
    }

    /// <summary>
    /// Immediately makes the cell invisible (alpha = 0).
    /// </summary>
    public void FadeOutImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.DOKill(); // Kill any existing fade animations
            canvasGroup.alpha = 0f;
        }
        else
        {
            Debug.LogWarning($"CellController ({gameObject.name}): CanvasGroup component is null. Cannot FadeOutImmediate.");
        }
    }

    /// <summary>
    /// Fades the cell in (alpha 0 to 1) over a specified duration.
    /// </summary>
    public void FadeIn(float duration)
    {
        if (canvasGroup != null)
        {
            canvasGroup.DOKill(); // Kill existing fades
            // Ensure it starts from alpha 0 if called immediately after FadeOutImmediate or if currently invisible
            if (canvasGroup.alpha < 1f)
            {
                // Set alpha to 0 before starting the fade to ensure a clean start
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, duration).SetEase(Ease.Linear);
            }
            // If already fully visible, just ensure alpha is 1 (no animation needed)
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

    /// <summary>
    /// Sets the alpha directly (useful for initial state or instant visibility).
    /// </summary>
    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.DOKill(); // Kill any running fades before setting directly
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
        else
        {
            Debug.LogWarning($"CellController ({gameObject.name}): CanvasGroup component is null. Cannot SetAlpha.");
        }
    }
}