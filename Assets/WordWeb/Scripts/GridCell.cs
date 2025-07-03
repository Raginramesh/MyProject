using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;

/// <summary>
/// Individual grid cell component for the word placement game.
/// Handles cell appearance, letter display, and interaction.
/// </summary>
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(CanvasGroup))]
public class GridCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Visual References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI letterText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Cell Colors")]
    [SerializeField] private Color emptyCellColor = Color.white;
    [SerializeField] private Color centerCellColor = Color.yellow;
    [SerializeField] private Color occupiedCellColor = Color.gray;
    [SerializeField] private Color highlightedCellColor = Color.green;
    [SerializeField] private Color hoverCellColor = Color.cyan;

    [Header("Animation")]
    [SerializeField] private float hoverScaleMultiplier = 1.1f;
    [SerializeField] private float animationDuration = 0.2f;

    // Cell state
    private Vector2Int gridPosition;
    private char currentLetter;
    private GridCellType cellType;
    private DynamicGridManager gridManager;
    private bool isHovered = false;
    private bool isInteractable = true;

    // Visual state
    private Vector3 originalScale;
    private Color originalColor;
    private RectTransform rectTransform;

    // Events
    public System.Action<GridCell> OnCellClicked;
    public System.Action<GridCell> OnCellHovered;
    public System.Action<GridCell> OnCellUnhovered;

    #region Initialization

    void Awake()
    {
        ValidateReferences();
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;
    }

    private void ValidateReferences()
    {
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (letterText == null)
        {
            letterText = GetComponentInChildren<TextMeshProUGUI>();
        }

        // Ensure we have required components
        if (backgroundImage == null)
        {
            Debug.LogError($"GridCell ({gameObject.name}): Missing Image component!");
        }

        if (canvasGroup == null)
        {
            Debug.LogError($"GridCell ({gameObject.name}): Missing CanvasGroup component!");
        }
    }

    public void Initialize(int x, int y, DynamicGridManager manager)
    {
        gridPosition = new Vector2Int(x, y);
        gridManager = manager;
        gameObject.name = $"GridCell_{x}_{y}";
        
        // Initialize visual state
        SetCellType(GridCellType.Empty);
        ClearLetter();
        
        // Store original color
        originalColor = backgroundImage.color;
    }

    #endregion

    #region Cell State Management

    public void SetCellType(GridCellType newType)
    {
        cellType = newType;
        UpdateCellAppearance();
    }

    public void SetLetter(char letter)
    {
        currentLetter = letter;
        
        if (letterText != null)
        {
            letterText.text = letter.ToString().ToUpper();
            letterText.gameObject.SetActive(true);
            
            // Animate letter appearance
            letterText.transform.localScale = Vector3.zero;
            letterText.transform.DOScale(Vector3.one, animationDuration)
                .SetEase(Ease.OutBack);
        }
    }

    public void ClearLetter()
    {
        currentLetter = '\0';
        
        if (letterText != null)
        {
            letterText.text = "";
            letterText.gameObject.SetActive(false);
        }
    }

    private void UpdateCellAppearance()
    {
        if (backgroundImage == null) return;

        Color targetColor = GetCellColor();
        
        // Animate color change
        backgroundImage.DOColor(targetColor, animationDuration)
            .SetEase(Ease.OutQuad);
        
        // Update interactability
        isInteractable = cellType != GridCellType.Occupied;
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = isInteractable ? 1f : 0.8f;
        }
    }

    private Color GetCellColor()
    {
        if (isHovered && isInteractable)
        {
            return hoverCellColor;
        }

        switch (cellType)
        {
            case GridCellType.Empty:
                return emptyCellColor;
            case GridCellType.Center:
                return centerCellColor;
            case GridCellType.Occupied:
                return occupiedCellColor;
            case GridCellType.Highlighted:
                return highlightedCellColor;
            default:
                return emptyCellColor;
        }
    }

    #endregion

    #region Interaction

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isInteractable) return;

        isHovered = true;
        UpdateCellAppearance();
        
        // Animate scale
        rectTransform.DOScale(originalScale * hoverScaleMultiplier, animationDuration)
            .SetEase(Ease.OutBack);
        
        OnCellHovered?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isInteractable) return;

        isHovered = false;
        UpdateCellAppearance();
        
        // Animate scale back
        rectTransform.DOScale(originalScale, animationDuration)
            .SetEase(Ease.OutBack);
        
        OnCellUnhovered?.Invoke(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isInteractable) return;

        // Visual feedback
        AnimateClick();
        
        OnCellClicked?.Invoke(this);
    }

    private void AnimateClick()
    {
        // Quick scale animation for click feedback
        rectTransform.DOScale(originalScale * 0.9f, animationDuration * 0.5f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                rectTransform.DOScale(originalScale * hoverScaleMultiplier, animationDuration * 0.5f)
                    .SetEase(Ease.OutBack);
            });
    }

    #endregion

    #region Visual Effects

    public void SetHighlighted(bool highlighted)
    {
        if (highlighted)
        {
            SetCellType(GridCellType.Highlighted);
        }
        else
        {
            // Reset to appropriate type based on state
            if (currentLetter != '\0')
            {
                SetCellType(GridCellType.Occupied);
            }
            else if (gridPosition == gridManager.CenterPosition)
            {
                SetCellType(GridCellType.Center);
            }
            else
            {
                SetCellType(GridCellType.Empty);
            }
        }
    }

    public void PlayPlacementEffect()
    {
        // Flash effect
        Color flashColor = Color.white;
        backgroundImage.DOColor(flashColor, 0.1f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                backgroundImage.DOColor(GetCellColor(), 0.2f)
                    .SetEase(Ease.OutQuad);
            });
        
        // Scale effect
        rectTransform.DOScale(originalScale * 1.2f, 0.1f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                rectTransform.DOScale(originalScale, 0.2f)
                    .SetEase(Ease.OutBack);
            });
    }

    public void PlayRemovalEffect()
    {
        // Fade out effect
        canvasGroup.DOFade(0f, animationDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                canvasGroup.DOFade(1f, animationDuration)
                    .SetEase(Ease.OutQuad);
            });
    }

    #endregion

    #region Getters

    public Vector2Int GridPosition => gridPosition;
    public char CurrentLetter => currentLetter;
    public GridCellType CellType => cellType;
    public bool IsOccupied => currentLetter != '\0';
    public bool IsInteractable => isInteractable;
    public bool IsHovered => isHovered;

    #endregion

    #region Utility

    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
        UpdateCellAppearance();
    }

    public void ResetVisuals()
    {
        // Stop all animations
        rectTransform.DOKill();
        backgroundImage.DOKill();
        canvasGroup.DOKill();
        
        // Reset to default state
        rectTransform.localScale = originalScale;
        isHovered = false;
        UpdateCellAppearance();
    }

    public Vector3 GetWorldPosition()
    {
        return rectTransform.position;
    }

    public Vector2 GetScreenPosition()
    {
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, rectTransform.position);
        return screenPos;
    }

    #endregion

    #region Debug

    void OnValidate()
    {
        // Update appearance in editor
        if (backgroundImage != null && Application.isPlaying)
        {
            UpdateCellAppearance();
        }
    }

    #endregion
}
