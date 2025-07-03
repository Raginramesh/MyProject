using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Word tile component that handles drag-and-drop functionality for word placement.
/// Represents a single word that can be dragged onto the grid.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class WordTile : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Word Data")]
    [SerializeField] private string word;
    [SerializeField] private int[] letterScores;
    [SerializeField] private int totalScore;
    [SerializeField] private int difficulty = 1;

    [Header("Visual References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI wordText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private Transform letterContainer;

    [Header("Letter Blocks")]
    [SerializeField] private GameObject letterBlockPrefab;
    [SerializeField] private List<LetterBlock> letterBlocks;
    [SerializeField] private float letterSpacing = 5f;

    [Header("Drag Visuals")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color hoverColor = Color.cyan;
    [SerializeField] private Color dragColor = Color.yellow;
    [SerializeField] private Color invalidColor = Color.red;
    [SerializeField] private Color validColor = Color.green;

    [Header("Animation")]
    [SerializeField] private float hoverScaleMultiplier = 1.05f;
    [SerializeField] private float dragScaleMultiplier = 1.1f;
    [SerializeField] private float animationDuration = 0.2f;

    // Drag state
    private bool isDragging = false;
    private bool isHovered = false;
    private bool isUsed = false;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Transform originalParent;
    private int originalSiblingIndex;

    // Placement state
    private Vector2Int? previewPosition;
    private PlacementOrientation previewOrientation = PlacementOrientation.Horizontal;
    private bool isValidPlacement = false;

    // References
    private DynamicGridManager gridManager;
    private WordPlacementGameManager gameManager;
    private Canvas parentCanvas;
    private GraphicRaycaster raycaster;

    // Events
    public System.Action<WordTile> OnDragStarted;
    public System.Action<WordTile> OnDragEnded;
    public System.Action<WordTile, Vector2Int, PlacementOrientation> OnPlacementAttempted;
    public System.Action<WordTile> OnWordPlaced;
    public System.Action<WordTile> OnWordRemoved;

    #region Initialization

    void Awake()
    {
        ValidateReferences();
        SetupReferences();
        originalScale = rectTransform.localScale;
    }

    private void ValidateReferences()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (wordText == null)
        {
            wordText = GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    private void SetupReferences()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        raycaster = parentCanvas?.GetComponent<GraphicRaycaster>();
        
        // Find managers
        gridManager = FindObjectOfType<DynamicGridManager>();
        gameManager = FindObjectOfType<WordPlacementGameManager>();
        
        if (gridManager == null)
        {
            Debug.LogWarning("WordTile: No DynamicGridManager found in scene!");
        }
    }

    public void Initialize(string newWord, int[] scores, int difficulty = 1)
    {
        this.word = newWord.ToUpper();
        this.letterScores = scores;
        this.difficulty = difficulty;
        this.totalScore = CalculateTotalScore();
        
        CreateLetterBlocks();
        UpdateVisuals();
        
        // Store original position and parent
        originalPosition = rectTransform.localPosition;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
    }

    #endregion

    #region Letter Block Management

    private void CreateLetterBlocks()
    {
        // Clear existing blocks
        ClearLetterBlocks();
        
        if (letterBlockPrefab == null || letterContainer == null)
        {
            Debug.LogWarning("WordTile: Missing prefab or container references for letter blocks!");
            return;
        }

        letterBlocks = new List<LetterBlock>();
        
        for (int i = 0; i < word.Length; i++)
        {
            GameObject blockObj = Instantiate(letterBlockPrefab, letterContainer);
            LetterBlock block = blockObj.GetComponent<LetterBlock>();
            
            if (block == null)
            {
                block = blockObj.AddComponent<LetterBlock>();
            }
            
            // Initialize block
            int score = (letterScores != null && i < letterScores.Length) ? letterScores[i] : 1;
            block.Initialize(word[i], score, i);
            
            letterBlocks.Add(block);
        }
        
        // Arrange letter blocks
        ArrangeLetterBlocks();
    }

    private void ClearLetterBlocks()
    {
        if (letterBlocks != null)
        {
            foreach (var block in letterBlocks)
            {
                if (block != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(block.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(block.gameObject);
                    }
                }
            }
            letterBlocks.Clear();
        }
    }

    private void ArrangeLetterBlocks()
    {
        if (letterBlocks == null || letterBlocks.Count == 0) return;

        // Simple horizontal layout
        float totalWidth = (letterBlocks.Count - 1) * letterSpacing;
        float startX = -totalWidth * 0.5f;
        
        for (int i = 0; i < letterBlocks.Count; i++)
        {
            var block = letterBlocks[i];
            var blockRect = block.GetComponent<RectTransform>();
            
            if (blockRect != null)
            {
                blockRect.anchoredPosition = new Vector2(startX + i * letterSpacing, 0);
            }
        }
    }

    #endregion

    #region Visual Updates

    private void UpdateVisuals()
    {
        if (wordText != null)
        {
            wordText.text = word;
        }

        if (scoreText != null)
        {
            scoreText.text = totalScore.ToString();
        }

        UpdateBackgroundColor();
    }

    private void UpdateBackgroundColor()
    {
        if (backgroundImage == null) return;

        Color targetColor = defaultColor;
        
        if (isUsed)
        {
            targetColor = Color.gray;
        }
        else if (isDragging)
        {
            targetColor = isValidPlacement ? validColor : invalidColor;
        }
        else if (isHovered)
        {
            targetColor = hoverColor;
        }

        backgroundImage.DOColor(targetColor, animationDuration)
            .SetEase(Ease.OutQuad);
    }

    #endregion

    #region Drag and Drop

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isUsed || canvasGroup == null) return;

        isDragging = true;
        
        // Visual feedback
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.8f;
        
        // Scale up
        rectTransform.DOScale(originalScale * dragScaleMultiplier, animationDuration)
            .SetEase(Ease.OutBack);
        
        // Move to front
        transform.SetAsLastSibling();
        
        OnDragStarted?.Invoke(this);
        
        Debug.Log($"Started dragging word: {word}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        // Follow mouse/touch
        rectTransform.position = eventData.position;
        
        // Check for valid placement
        CheckPlacementValidity(eventData.position);
        
        // Update visual feedback
        UpdateBackgroundColor();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;
        
        // Try to place the word
        bool placed = AttemptPlacement(eventData.position);
        
        if (!placed)
        {
            // Return to original position
            ReturnToOriginalPosition();
        }
        
        // Reset visual state
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        
        OnDragEnded?.Invoke(this);
        
        Debug.Log($"Ended dragging word: {word}, Placed: {placed}");
    }

    private void CheckPlacementValidity(Vector2 screenPosition)
    {
        if (gridManager == null) return;

        // Convert screen position to grid position
        Vector2Int? gridPos = GetGridPositionFromScreenPoint(screenPosition);
        
        if (gridPos.HasValue)
        {
            // Check if word can be placed at this position
            bool canPlaceHorizontal = gridManager.CanPlaceWord(word, gridPos.Value, PlacementOrientation.Horizontal);
            bool canPlaceVertical = gridManager.CanPlaceWord(word, gridPos.Value, PlacementOrientation.Vertical);
            
            if (canPlaceHorizontal || canPlaceVertical)
            {
                previewPosition = gridPos.Value;
                previewOrientation = canPlaceHorizontal ? PlacementOrientation.Horizontal : PlacementOrientation.Vertical;
                isValidPlacement = true;
                
                // TODO: Show placement preview
                ShowPlacementPreview();
            }
            else
            {
                isValidPlacement = false;
                HidePlacementPreview();
            }
        }
        else
        {
            isValidPlacement = false;
            HidePlacementPreview();
        }
    }

    private bool AttemptPlacement(Vector2 screenPosition)
    {
        if (!isValidPlacement || !previewPosition.HasValue) return false;

        // Attempt to place word on grid
        bool success = gridManager.PlaceWord(word, previewPosition.Value, previewOrientation);
        
        if (success)
        {
            // Mark as used
            isUsed = true;
            
            // Hide this tile
            gameObject.SetActive(false);
            
            // Notify game manager
            OnWordPlaced?.Invoke(this);
            
            OnPlacementAttempted?.Invoke(this, previewPosition.Value, previewOrientation);
            
            return true;
        }
        
        return false;
    }

    private void ReturnToOriginalPosition()
    {
        // Animate back to original position
        rectTransform.DOMove(originalParent.TransformPoint(originalPosition), animationDuration)
            .SetEase(Ease.OutQuad);
        
        rectTransform.DOScale(originalScale, animationDuration)
            .SetEase(Ease.OutBack);
        
        // Reset parent and sibling index
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex);
    }

    #endregion

    #region Placement Preview

    private void ShowPlacementPreview()
    {
        if (!previewPosition.HasValue) return;

        // TODO: Implement ghost tile preview on grid
        // This would show semi-transparent letters on the grid cells
        // where the word would be placed
    }

    private void HidePlacementPreview()
    {
        // TODO: Hide ghost tile preview
    }

    #endregion

    #region Hover Effects

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isUsed || isDragging) return;

        isHovered = true;
        UpdateBackgroundColor();
        
        // Scale up slightly
        rectTransform.DOScale(originalScale * hoverScaleMultiplier, animationDuration)
            .SetEase(Ease.OutBack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isUsed || isDragging) return;

        isHovered = false;
        UpdateBackgroundColor();
        
        // Scale back to normal
        rectTransform.DOScale(originalScale, animationDuration)
            .SetEase(Ease.OutBack);
    }

    #endregion

    #region Utility

    private Vector2Int? GetGridPositionFromScreenPoint(Vector2 screenPoint)
    {
        if (gridManager == null) return null;

        // Raycast to find grid cell
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = screenPoint;
        
        List<RaycastResult> raycastResults = new List<RaycastResult>();
        if (raycaster != null)
        {
            raycaster.Raycast(pointerData, raycastResults);
        }
        
        // Find grid cell in results
        foreach (var result in raycastResults)
        {
            GridCell cell = result.gameObject.GetComponent<GridCell>();
            if (cell != null)
            {
                return cell.GridPosition;
            }
        }
        
        return null;
    }

    private int CalculateTotalScore()
    {
        int total = 0;
        
        if (letterScores != null)
        {
            for (int i = 0; i < letterScores.Length; i++)
            {
                total += letterScores[i];
            }
        }
        else
        {
            total = word.Length; // Default scoring
        }
        
        return total;
    }

    #endregion

    #region Public Methods

    public void ResetTile()
    {
        isUsed = false;
        isDragging = false;
        isHovered = false;
        isValidPlacement = false;
        previewPosition = null;
        
        gameObject.SetActive(true);
        
        // Reset position and scale
        rectTransform.localPosition = originalPosition;
        rectTransform.localScale = originalScale;
        
        // Reset parent
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex);
        
        // Reset visuals
        UpdateVisuals();
    }

    public void SetUsed(bool used)
    {
        isUsed = used;
        UpdateVisuals();
        
        // Make non-interactable if used
        if (canvasGroup != null)
        {
            canvasGroup.interactable = !used;
            canvasGroup.alpha = used ? 0.5f : 1f;
        }
    }

    #endregion

    #region Getters

    public string Word => word;
    public int[] LetterScores => letterScores;
    public int TotalScore => totalScore;
    public int Difficulty => difficulty;
    public bool IsUsed => isUsed;
    public bool IsDragging => isDragging;
    public List<LetterBlock> LetterBlocks => letterBlocks;

    #endregion
}
