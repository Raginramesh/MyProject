using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Linq; // For Select in logging


[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(Image))] // Assuming you use an Image for background color
public class WordListItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI Elements")]
    [SerializeField] private Image backgroundImage; // Main background of the word item
    [SerializeField] private GameObject letterTileMiniPrefab; // Prefab for individual letter display within this item
    [SerializeField] private Transform letterContainer; // Parent for instantiated letter tiles

    [Header("Appearance")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color dragColor = Color.yellow; // Color when dragging and placement is valid
    [SerializeField] private Color invalidDropColor = Color.red; // Color when dragging and placement is invalid

    [Header("Highlight Zone Behavior")]
    // [SerializeField] // No longer serialized, set by WordInventoryDisplay
    private bool isInHighlightZone = false;
    private Vector3 currentNormalScaleValue = Vector3.one;
    private Vector3 currentHighlightScaleValue = Vector3.one * 1.1f; // Example scale
    private float currentScaleLerpSpeed = 5f;

    [Header("Dragging Behavior")]
    [Tooltip("0-indexed offset of the letter in the word that the drag operation should align with the mouse pointer. E.g., for 'FUN', 0=F, 1=U, 2=N.")]
    [SerializeField] private int dragAnchorLetterOffset = 0;


    // Data
    private string word;
    private bool isNew; // If it's a newly drawn word, etc. (optional)

    // Runtime References (assigned via Setup or found)
    private WordPlacementValidator wordPlacementValidator;
    private WordInventory wordInventory; // Reference to the main inventory script
    private GridManager gridManager;

    // Dragging State
    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private CanvasGroup canvasGroup;
    private Vector3 originalPosition; // For ScreenSpaceOverlay, world position
    private Transform originalParent;
    private int originalSiblingIndex;
    private bool isDragging = false;
    private bool currentPlacementValid = false;

    // Caches
    private RectTransform itemRectTransform; // Cached RectTransform of this item
    private List<GameObject> currentLetterTiles = new List<GameObject>();
    private Vector3 targetScale = Vector3.one;


    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        backgroundImage = GetComponent<Image>(); // Ensure this is assigned or found
        itemRectTransform = GetComponent<RectTransform>();

        if (letterContainer == null) letterContainer = transform; // Default to self if not set
        if (backgroundImage == null) Debug.LogWarning($"WordListItemUI ({gameObject.name}): backgroundImage reference not set or found!", this);
        if (letterTileMiniPrefab == null) Debug.LogError($"WordListItemUI ({gameObject.name}): letterTileMiniPrefab reference not set!", this);

        // Find Root Canvas for dragging
        if (rootCanvas == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null) rootCanvas = parentCanvas.rootCanvas;
            if (rootCanvas == null) Debug.LogError($"WordListItemUI ({gameObject.name}): Could not find root Canvas for dragging!", this);
        }
        targetScale = transform.localScale;
        currentNormalScaleValue = transform.localScale; // Initialize from current
        // currentHighlightScaleValue remains as set in Inspector or default (e.g., transform.localScale * 1.1f)
    }

    void Update()
    {
        // Smoothly scale towards the target scale (for highlight zone effect)
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * currentScaleLerpSpeed);
        }
    }


    public void Setup(string word, bool isNew, WordPlacementValidator validator, WordInventory inventory, GridManager gridMgr)
    {
        this.word = word.ToUpper();
        this.isNew = isNew;
        this.wordPlacementValidator = validator;
        this.wordInventory = inventory;
        this.gridManager = gridMgr;

        if (this.wordPlacementValidator == null) Debug.LogError($"WordListItemUI ({this.word}): WordPlacementValidator is null in Setup.", this);
        if (this.gridManager == null) Debug.LogError($"WordListItemUI ({this.word}): GridManager is null in Setup.", this);
        // WordInventory can be null if not used in a particular scene/setup
        // if (this.wordInventory == null) Debug.LogError($"WordListItemUI ({this.word}): WordInventory is null in Setup.", this);


        PopulateLetterTiles();
        UpdateAppearance();

        // Initialize scales based on current state after setup
        targetScale = transform.localScale;
        currentNormalScaleValue = transform.localScale;
        // currentHighlightScaleValue could be set here too if it depends on setup parameters
    }

    private void PopulateLetterTiles()
    {
        foreach (GameObject oldTile in currentLetterTiles) { Destroy(oldTile); }
        currentLetterTiles.Clear();

        if (letterTileMiniPrefab == null || string.IsNullOrEmpty(word)) return;

        foreach (char letter in word)
        {
            GameObject tileInstance = Instantiate(letterTileMiniPrefab, letterContainer);
            TextMeshProUGUI textComp = tileInstance.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null)
            {
                textComp.text = letter.ToString();
                if (textComp.font == null) Debug.LogError($"WordListItemUI ({this.word}): TextMeshProUGUI in letterTileMiniPrefab instance is MISSING a Font Asset for letter '{letter}'!", tileInstance);
            }
            else
            {
                Debug.LogWarning($"WordListItemUI ({this.word}): letterTileMiniPrefab instance is missing TextMeshProUGUI child for letter '{letter}'.", tileInstance);
            }
            currentLetterTiles.Add(tileInstance);
        }
    }

    // Called by WordInventoryDisplay or similar
    public void UpdateHighlightState(RectTransform viewport, Camera canvasCam, float zoneMinNormY, float zoneMaxNormY, Vector3 normScale, Vector3 highScale, float lerpSpeed)
    {
        if (itemRectTransform == null || viewport == null) return;

        currentNormalScaleValue = normScale;
        currentHighlightScaleValue = highScale;
        currentScaleLerpSpeed = lerpSpeed; // Store the lerp speed

        float normalizedY = CalculateNormalizedViewportY(viewport, canvasCam);
        isInHighlightZone = (normalizedY >= zoneMinNormY && normalizedY <= zoneMaxNormY);

        targetScale = isInHighlightZone ? currentHighlightScaleValue : currentNormalScaleValue;
    }

    private float CalculateNormalizedViewportY(RectTransform viewport, Camera canvasCam)
    {
        if (itemRectTransform == null || viewport == null) return 0f;

        Vector3 worldCenter = itemRectTransform.TransformPoint(itemRectTransform.rect.center); // Get world center of the item
        Vector2 localPointInViewport;

        // Convert item's world center to a point local to the viewport RectTransform
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, RectTransformUtility.WorldToScreenPoint(canvasCam, worldCenter), canvasCam, out localPointInViewport))
        {
            // Normalize the y-coordinate within the viewport's height
            // localPointInViewport.y is relative to viewport's pivot.
            // If pivot is (0.5, 0.5), localPointInViewport.y ranges from -height/2 to +height/2.
            // We want 0 at bottom, 1 at top of viewport.
            return (localPointInViewport.y + viewport.rect.height * viewport.pivot.y) / viewport.rect.height;
        }
        return -1f; // Indicate failure or out of bounds
    }


    private void UpdateAppearance()
    {
        if (backgroundImage == null) return;

        if (isDragging)
        {
            backgroundImage.color = currentPlacementValid ? dragColor : invalidDropColor;
        }
        else
        {
            backgroundImage.color = normalColor;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Allow drag only if in highlight zone OR if already dragging (to allow finishing a drag if it moves out)
        if (!isInHighlightZone && !isDragging)
        {
            // Debug.Log($"BeginDrag prevented: Not in highlight zone for {word}");
            eventData.pointerDrag = null; // Cancel drag
            return;
        }
        if (string.IsNullOrEmpty(word) || wordPlacementValidator == null || gridManager == null || rootCanvas == null)
        {
            Debug.LogError($"WordListItemUI ({word}): OnBeginDrag PREVENTED. Critical refs missing. Validator: {wordPlacementValidator}, GridManager: {gridManager}, RootCanvas: {rootCanvas}", this);
            eventData.pointerDrag = null; // Prevent drag if setup is incomplete
            return;
        }


        isDragging = true;
        originalPosition = rectTransform.position; // Store original world position
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        transform.SetParent(rootCanvas.transform, true); // worldPositionStays = true
        transform.SetAsLastSibling(); // Render on top

        canvasGroup.blocksRaycasts = false; // So it doesn't block raycasts to grid
        canvasGroup.alpha = 0.8f; // Make it slightly transparent

        currentPlacementValid = false; // Reset placement validity at start of drag
        UpdateAppearance();
        // Debug.Log($"OnBeginDrag: {word}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        rectTransform.position = eventData.position; // For ScreenSpaceOverlay. Adjust if using WorldSpace camera for UI.

        Vector2Int pointerCellCoords; // This is where the ANCHOR letter (e.g., "U") should land
        // Determine the correct camera for ScreenPointToRay or ScreenPointToLocalPointInRectangle
        Camera uiCamera = (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : rootCanvas.worldCamera;

        if (gridManager.ScreenPointToGridCoords(eventData.position, uiCamera, out pointerCellCoords))
        {
            bool isHorizontal = true; // TODO: Implement orientation toggle later (e.g., right-click to rotate word)

            // Adjust the word's logical start coordinate based on the dragAnchorLetterOffset
            Vector2Int actualWordStartCoords;
            if (isHorizontal)
            {
                actualWordStartCoords = new Vector2Int(pointerCellCoords.x - dragAnchorLetterOffset, pointerCellCoords.y);
            }
            else // Vertical orientation
            {
                actualWordStartCoords = new Vector2Int(pointerCellCoords.x, pointerCellCoords.y - dragAnchorLetterOffset);
            }

            PlacementResult previewResult = wordPlacementValidator.ValidatePlacement(word, actualWordStartCoords, isHorizontal);
            // Show preview on grid based on validation result
            gridManager.ShowWordPreview(previewResult.WordCoordinates, word, previewResult.IsValid);
            currentPlacementValid = previewResult.IsValid;
        }
        else
        {
            gridManager.ClearWordPreview(); // Clear preview if pointer is off-grid
            currentPlacementValid = false;
        }
        UpdateAppearance(); // Updates the dragged item's appearance (e.g., color)
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"WordListItemUI.OnEndDrag: Initiated for word '{word}'. IsDragging: {isDragging}");
        if (!isDragging) return; // Should not happen if OnBeginDrag set it true
        isDragging = false;

        gridManager.ClearWordPreview(); // ALWAYS clear the preview visual on drop

        Vector2Int pointerCellCoordsOnDrop; // Where the ANCHOR letter was dropped
        Camera uiCamera = (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : rootCanvas.worldCamera;
        bool droppedOnGrid = gridManager.ScreenPointToGridCoords(eventData.position, uiCamera, out pointerCellCoordsOnDrop);
        bool placementSuccessful = false;

        Debug.Log($"WordListItemUI.OnEndDrag: DroppedOnGrid: {droppedOnGrid}, PointerCoordsAtDrop: {pointerCellCoordsOnDrop}");

        if (droppedOnGrid)
        {
            bool isHorizontal = true; // TODO: Orientation toggle

            // Adjust the word's logical start coordinate based on the dragAnchorLetterOffset
            Vector2Int actualWordStartCoords;
            if (isHorizontal)
            {
                actualWordStartCoords = new Vector2Int(pointerCellCoordsOnDrop.x - dragAnchorLetterOffset, pointerCellCoordsOnDrop.y);
            }
            else // Vertical
            {
                actualWordStartCoords = new Vector2Int(pointerCellCoordsOnDrop.x, pointerCellCoordsOnDrop.y - dragAnchorLetterOffset);
            }
            Debug.Log($"WordListItemUI.OnEndDrag: Calculated ActualWordStartCoords: {actualWordStartCoords} for word '{word}' with anchor offset {dragAnchorLetterOffset}");

            PlacementResult finalResult = wordPlacementValidator.ValidatePlacement(word, actualWordStartCoords, isHorizontal);
            Debug.Log($"WordListItemUI.OnEndDrag: Placement Validation for '{word}' - IsValid: {finalResult.IsValid}, Message: '{finalResult.ErrorMessage}', Coords: {(finalResult.WordCoordinates != null ? string.Join(";", finalResult.WordCoordinates.Select(c => c.ToString())) : "NULL_COORDS_LIST")}");


            if (finalResult.IsValid)
            {
                Debug.Log($"WordListItemUI.OnEndDrag: Placement IS VALID for '{word}'. Attempting to place letters.");
                bool firstLetterPlacedThisTurn = false; // To ensure ConfirmFirstWordPlaced is called only once per successful multi-letter first word
                for (int i = 0; i < finalResult.WordCoordinates.Count; i++)
                {
                    Vector2Int coord = finalResult.WordCoordinates[i];
                    // Ensure we don't try to get a letter beyond the word's length if WordCoordinates is somehow longer
                    // (e.g. if ValidatePlacement returns coordinates for a path, not just the word itself - though it shouldn't here)
                    if (i < word.Length)
                    {
                        char letter = word[i];
                        Debug.Log($"WordListItemUI.OnEndDrag: Attempting to place letter '{letter}' at {coord} (index {i} of word '{word}')");
                        bool placed = gridManager.TrySetLetter(coord, letter); // This should trigger TileData.UpdateVisuals
                        if (!placed)
                        {
                            Debug.LogError($"WordListItemUI.OnEndDrag: GridManager FAILED to set letter '{letter}' at {coord} for word '{word}'.");
                        }
                        else
                        {
                            Debug.Log($"WordListItemUI.OnEndDrag: GridManager SUCCEEDED to set letter '{letter}' at {coord}.");
                            // Check if this is the first word being placed in the game
                            if (!wordPlacementValidator.IsFirstWordPlaced())
                            {
                                if (!firstLetterPlacedThisTurn) // Only confirm once for this word placement
                                {
                                    Debug.Log("WordListItemUI.OnEndDrag: First word rule triggered. Confirming placement with validator.");
                                    wordPlacementValidator.ConfirmFirstWordPlaced();
                                    firstLetterPlacedThisTurn = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"WordListItemUI.OnEndDrag: Coordinate index {i} is out of bounds for word '{word}' (length {word.Length}). Skipping letter placement for this coord.");
                    }
                }
                Debug.Log($"WordListItemUI.OnEndDrag: Placement loop finished for '{word}'. Score: {finalResult.Score}");
                wordInventory?.UseWord(word); // Assuming wordInventory exists and should be notified
                placementSuccessful = true;
            }
            else
            {
                Debug.LogWarning($"WordListItemUI.OnEndDrag: Placement IS INVALID for '{word}' (anchor offset {dragAnchorLetterOffset}) starting at {actualWordStartCoords}. Reason: {finalResult.ErrorMessage}");
            }
        }
        else { Debug.LogWarning($"WordListItemUI.OnEndDrag: Dropped '{word}' outside of grid."); }

        // Post-placement actions
        if (placementSuccessful)
        {
            Debug.Log($"WordListItemUI.OnEndDrag: Placement successful for '{word}'. Destroying item.");
            Destroy(gameObject); // Word is used, remove from list
        }
        else
        {
            Debug.Log($"WordListItemUI.OnEndDrag: Placement FAILED for '{word}'. Resetting item position and appearance.");
            // Restore item to its original state and position
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1.0f;
            ResetPosition();
            currentPlacementValid = false; // Reset this flag
            UpdateAppearance(); // Revert color
            // Restore scale based on whether it's in highlight zone or not
            targetScale = isInHighlightZone ? currentHighlightScaleValue : currentNormalScaleValue;
            transform.localScale = targetScale; // Snap back or let Update lerp
        }
    }

    private void ResetPosition()
    {
        transform.SetParent(originalParent, false); // worldPositionStays = false to re-apply layout
        transform.SetSiblingIndex(originalSiblingIndex);
        // If originalPosition was based on world space and parent is a layout group,
        // simply re-parenting and setting sibling index is often enough for layout groups to reposition.
        // If originalPosition is critical and layout groups are not complex or you need exact previous world pos:
        // rectTransform.position = originalPosition; // This might fight with layout group if not careful
        // Debug.Log($"Reset position for {word}");
    }
}